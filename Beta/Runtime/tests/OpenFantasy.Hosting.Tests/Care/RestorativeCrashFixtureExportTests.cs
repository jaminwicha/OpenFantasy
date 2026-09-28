using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>Actual managed transactions exported for independent process-kill storage qualification.</summary>
public sealed class RestorativeCrashFixtureExportTests
{
    [Theory]
    [InlineData("raw-self")]
    [InlineData("crafted-self")]
    [InlineData("helper")]
    [InlineData("revoke")]
    public void Earned_ordinary_care_transactions_preserve_exact_replay_and_duplicate_boundaries(string name)
    {
        var fixture = new OrdinaryCareWorldFixture();
        using var files = new StorageFiles();
        WorldSnapshot before;
        WorldStepBatch batch;
        WorldStepDecision accepted;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            var session = new CareWorldSession(authority);
            var injured = fixture.ReachFullDoseInjuredExploration(session);
            before = name switch
            {
                "crafted-self" => RestorativeNativeFixtureExportTests.CraftPlayerDose(fixture, session, authority),
                "helper" or "revoke" => fixture.Supply(session, self: false),
                _ => fixture.Supply(session, self: true)
            };
            Assert.Equal(injured.Progression!.ContentHash, before.Progression!.ContentHash);
            Assert.Equivalent(fixture.Genesis.Progression!.GetActor(fixture.Player).Stats,
                before.Progression.GetActor(fixture.Player).Stats, strict: true);
            WorldActionCommand selected;
            if (name == "revoke")
            {
                // Revocation is tested with otherwise executable, actually stocked assistance.
                Assert.Single(new LocalRestorativeController().SelectActions(before,
                    authority.IsIdentityOccupiedForHost, new HashSet<EntityId>()));
                var permission = before.Care!.Ordinary!.Permissions.Single();
                selected = fixture.Journey.Command(before, fixture.Player,
                    new RevokeCarePermissionIntent(permission.PermissionId, permission.Revision, permission.GrantVersion));
            }
            else if (name == "helper")
                selected = Assert.Single(new LocalRestorativeController().SelectActions(before,
                    authority.IsIdentityOccupiedForHost, new HashSet<EntityId>()));
            else
            {
                var definition = before.Care!.Ordinary!.Definitions.Single(value => value.DeliveryMode == RestorativeDeliveryMode.SelfUse);
                selected = fixture.Journey.Command(before, fixture.Player, Assert.IsType<UseRestorativeItemIntent>(
                    WorldRestorativeActions.Prepare(before, fixture.Player, fixture.Player, definition.DefinitionId)));
            }
            batch = fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep, [selected]);
            accepted = Publish(name, authority, before, batch, fixture.Player, fixture.Helper, sourceStats: true);
        }
        VerifyReopened(files.Path, before, accepted, batch);
    }

    [Fact]
    public void Actual_second_ordinary_dose_and_first_resource_archive_publish_in_one_exact_transaction()
    {
        var fixture = new OrdinaryCareArchiveFixture();
        using var files = new StorageFiles();
        WorldSnapshot before;
        WorldStepBatch batch;
        WorldStepDecision accepted;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            var session = new CareWorldSession(authority);
            var at255 = fixture.Prepare255(session);
            var first = fixture.Journey.Command(at255, fixture.Journey.Helper, fixture.Aid(at255));
            var firstDecision = CareWorldFixture.Commit(session,
                fixture.Journey.Batch(at255, WorldBatchKind.AdvancingStep, [first]));
            before = firstDecision.Candidate;
            Assert.Null(firstDecision.SealedResourceSegment);
            Assert.Equal(256, before.ResourceHistory!.History.Batches.Count);
            Assert.Null(before.ResourceHistory.ArchiveAnchor);
            Assert.Single(before.Care!.Ordinary!.Uses);
            Assert.Single(before.Production!.ConsumedItems);
            Assert.Equal(1UL, before.Inventory.Custody.GetLot(fixture.Journey.PotionLot).Quantity);
            Assert.Equal(at255.Progression!.GetActor(fixture.Journey.Player).CurrentHp + 40,
                before.Progression!.GetActor(fixture.Journey.Player).CurrentHp);
            var second = fixture.Journey.Command(before, fixture.Journey.Helper, fixture.Aid(before));
            batch = fixture.Journey.Batch(before, WorldBatchKind.AdvancingStep, [second]);
            accepted = Publish("archive-helper", authority, before, batch,
                fixture.Journey.Player, fixture.Journey.Helper, sourceStats: false);
            Assert.Equal(WorldSnapshotCodec.Encode(before),
                Assert.IsType<WorldResourceArchiveSegment>(accepted.SealedResourceSegment).Encode());
            Assert.Equal(2, accepted.Candidate.Care!.Ordinary!.Uses.Count);
            Assert.Equal(2, accepted.Candidate.Production!.ConsumedItems.Count);
            Assert.DoesNotContain(accepted.Candidate.Inventory.Custody.Lots,
                value => value.LotId == fixture.Journey.PotionLot);
            Assert.Single(accepted.Candidate.ResourceHistory!.History.Batches);
            Assert.Single(accepted.Candidate.ResourceHistory.Evidence);
            Assert.Equal(before.Progression.Revision, accepted.Candidate.ResourceHistory.History.Basis.Revision);
            Assert.Equal(0UL, Assert.IsType<WorldResourceArchiveSegment>(accepted.SealedResourceSegment).Reference.Ordinal);
        }
        VerifyReopened(files.Path, before, accepted, batch);
    }

    private static WorldStepDecision Publish(string name, SqliteWorldAuthority authority, WorldSnapshot before,
        WorldStepBatch batch, EntityId player, EntityId helper, bool sourceStats)
    {
        Assert.Equal((ushort)26, before.ProfileVersion);
        Assert.Equal(WorldBatchKind.AdvancingStep, batch.Kind);
        var command = Assert.Single(batch.Actions);
        var control = before.Inventory.Controls.Single(value => value.ActorId == command.ActorId);
        Assert.Equal(control.PrincipalId, command.PrincipalId);
        Assert.Equal(control.SessionId, command.OriginSessionId);
        Assert.True(WorldActorAvailability.CanAct(before, command.ActorId));
        Assert.Null(before.Care!.Pending);
        Assert.False(before.HasReadingHold);
        Assert.Null(before.PendingExcavation);
        var preview = authority.Preview(batch);
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(preview.Receipts).Status);
        Assert.Equal(before.ContentHash, authority.ReadStateForHost().ContentHash);
        ulong archiveCount = authority.InspectForHost().Journal.ResourceArchiveCount;
        Assert.Equal(0UL, archiveCount);
        ExportBefore(name, authority, before, batch, preview, player, helper, sourceStats, archiveCount);
        var accepted = authority.Commit(batch);
        var after = accepted.Candidate;
        Assert.Equal(preview.Candidate.ContentHash, after.ContentHash);
        Assert.Equal(WorldReceiptCodecV1.EncodeBatch(preview.Receipts), WorldReceiptCodecV1.EncodeBatch(accepted.Receipts));
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
        Assert.Equal(before.Calendar.AddMilliseconds(1_800), after.Calendar);
        Assert.Equal(before.Space.Actors, after.Space.Actors);
        Assert.Equal(before.Progression!.Currency, after.Progression!.Currency);
        Assert.Equal(before.Progression.GetActor(player).CurrentMp, after.Progression.GetActor(player).CurrentMp);
        Assert.Equal(before.Care.Acknowledgments, after.Care!.Acknowledgments);
        Assert.Equal(before.Care.Treatments, after.Care.Treatments);
        Assert.Null(after.Care.Pending);
        if (command.Intent is UseRestorativeItemIntent use)
        {
            Assert.Equal(WorldActionRejection.None, WorldRestorativeActions.Evaluate(before, command.ActorId, use));
            Assert.Equal(name is "raw-self" or "archive-helper" ? IngredientLotKind.Source : IngredientLotKind.Transformed,
                use.Ingredient.Kind);
            Assert.Equal(name.EndsWith("self", StringComparison.Ordinal) ? player : helper, command.ActorId);
            Assert.Equal(player, use.RecipientActorId);
            Assert.Equal(before.Care.Ordinary!.Uses.Count + 1, after.Care.Ordinary!.Uses.Count);
            var recorded = Assert.Single(after.Care.Ordinary.Uses, value => value.CommandId == command.CommandId);
            var consumed = Assert.Single(after.Production!.ConsumedItems, value => value.CommandId == command.CommandId);
            Assert.Equal(before.Production!.ConsumedItems.Count + 1, after.Production.ConsumedItems.Count);
            Assert.Equal(EntityId.DeriveSpawned(command.CommandId, 1), recorded.UseId);
            Assert.Equal(EntityId.DeriveSpawned(command.CommandId, 0), consumed.ConsumptionId);
            Assert.Equal(recorded.ConsumptionId, consumed.ConsumptionId);
            Assert.Equal(after.Revision, recorded.WorldSequence);
            Assert.Equal(0U, recorded.CommandOrdinal);
            Assert.Equal(use.Ingredient.LotId, consumed.Ingredient.LotId);
            Assert.Equal(use.Ingredient.ExpectedRevision, consumed.Ingredient.Revision);
            Assert.Equal(1UL, consumed.Ingredient.Quantity);
            Assert.Equal(command.ActorId, consumed.ActorId);
            Assert.Equal(player, consumed.RecipientActorId);
            Assert.Equal(before.GetResident(command.ActorId).InventoryCustodianId, consumed.CustodianId);
            Assert.Equal(before.Progression.GetActor(player).CurrentHp + 40, after.Progression.GetActor(player).CurrentHp);
            Assert.Equal(before.Progression.GetActor(player).Revision + 1, after.Progression.GetActor(player).Revision);
            Assert.Equal(before.Care.Ordinary.Permissions, after.Care.Ordinary.Permissions);
        }
        else
        {
            var revoke = Assert.IsType<RevokeCarePermissionIntent>(command.Intent);
            var permission = after.Care.Ordinary!.GetPermission(revoke.PermissionId);
            Assert.False(permission.IsActive);
            Assert.Equal(command.CommandId, permission.Revocation!.CommandId);
            Assert.Equal(after.Revision, permission.Revocation.WorldSequence);
            Assert.Equal(0U, permission.Revocation.CommandOrdinal);
            Assert.Equal(before.Care.Ordinary!.Uses, after.Care.Ordinary.Uses);
            Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
            Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
            Assert.Equal(before.Progression.ContentHash, after.Progression.ContentHash);
            Assert.Equal(before.ResourceHistory!.ContentHash, after.ResourceHistory!.ContentHash);
            Assert.Empty(new LocalRestorativeController().SelectActions(after, authority.IsIdentityOccupiedForHost,
                new HashSet<EntityId>()));
        }
        Assert.Equal(name == "archive-helper", accepted.SealedResourceSegment is not null);
        Assert.Equal(name == "archive-helper" ? 1UL : 0UL, authority.InspectForHost().Journal.ResourceArchiveCount);
        if (EvidenceRoot() is { } directory)
            authority.BackupForHost(Path.Combine(directory, name, "after.sqlite3"));
        return accepted;
    }

    private static void VerifyReopened(string path, WorldSnapshot before, WorldStepDecision expected, WorldStepBatch batch)
    {
        byte[] closed = File.ReadAllBytes(path);
        using (var recovered = SqliteWorldAuthority.OpenExisting(path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(expected.Candidate), WorldSnapshotCodec.Encode(recovered.ReadStateForHost()));
            Assert.Equal(before.ContentHash, recovered.ReadRecordedStateForHost(before.Revision).StateHash);
            var command = Assert.Single(batch.Actions);
            var receipt = Assert.IsType<WorldActionReceipt>(recovered.FindReceipt(command,
                new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId)));
            Assert.Equal(WorldReceiptCodecV1.Encode(Assert.Single(expected.Receipts)), WorldReceiptCodecV1.Encode(receipt));
            Assert.True(recovered.IsIdentityOccupiedForHost(command.CommandId));
            Assert.Throws<AuthorityDispatchException>(() => recovered.Commit(batch));
            Assert.Equal(expected.Candidate.ContentHash, recovered.ReadStateForHost().ContentHash);
            if (expected.SealedResourceSegment is { } segment)
            {
                var retrieved = recovered.ReadResourceArchiveForHost(segment.Reference.Ordinal);
                Assert.Equal(segment.Encode(), retrieved.Segment.Encode());
                Assert.Equal(WorldSnapshotCodec.Encode(before), retrieved.Segment.Encode());
                Assert.Equal(expected.Candidate.Revision, retrieved.PublicationSequence);
            }
        }
        Assert.Equal(closed, File.ReadAllBytes(path));
    }

    private static void ExportBefore(string name, SqliteWorldAuthority authority, WorldSnapshot before,
        WorldStepBatch batch, WorldStepDecision expected, EntityId player, EntityId helper, bool sourceStats, ulong archiveCount)
    {
        if (EvidenceRoot() is not { } root) return;
        string directory = Path.Combine(root, name);
        Directory.CreateDirectory(directory);
        authority.BackupForHost(Path.Combine(directory, "before.sqlite3"));
        File.WriteAllBytes(Path.Combine(directory, "before.ofc"), WorldSnapshotCodec.Encode(before));
        File.WriteAllBytes(Path.Combine(directory, "batch.ofc"), WorldStepCodec.Encode(batch));
        File.WriteAllBytes(Path.Combine(directory, "after.ofc"), WorldSnapshotCodec.Encode(expected.Candidate));
        var command = Assert.Single(batch.Actions);
        var use = command.Intent as UseRestorativeItemIntent;
        var beforePatient = before.Progression!.GetActor(player);
        var afterPatient = expected.Candidate.Progression!.GetActor(player);
        File.WriteAllText(Path.Combine(directory, "metadata.json"), JsonSerializer.Serialize(new
        {
            fixture = name, profileVersion = before.ProfileVersion, sourceStats,
            setupContract = sourceStats ? "Actual original-stat defeat, finite aftermath care, deliberate Wait, victory, and legal owned stock or real three-berry crafting."
                : "Explicit fresh stress statistics and finite two-Potion stock; 254 actual bites, victory, and one actual prior ordinary dose reach 256 resource batches.",
            worldId = before.WorldId.ToString(), branchId = before.BranchId.ToString(),
            beforeRevision = before.Revision, beforeHash = before.ContentHash.Value,
            afterRevision = expected.Candidate.Revision, afterHash = expected.Candidate.ContentHash.Value,
            archiveCountBefore = archiveCount, archiveCountAfter = archiveCount + (expected.SealedResourceSegment is null ? 0UL : 1UL),
            newArchiveOrdinal = expected.SealedResourceSegment?.Reference.Ordinal,
            playerActorId = player.ToString(), helperActorId = helper.ToString(), selectedActorId = command.ActorId.ToString(),
            commandId = command.CommandId.ToString(), commandHash = command.ContentHash.Value,
            selectedAction = command.Intent.GetType().Name, ingredientKind = use?.Ingredient.Kind.ToString(),
            ingredientLotId = use?.Ingredient.LotId.ToString(), ingredientRevision = use?.Ingredient.ExpectedRevision,
            beforeHp = beforePatient.CurrentHp, afterHp = afterPatient.CurrentHp,
            beforeMp = beforePatient.CurrentMp, afterMp = afterPatient.CurrentMp,
            beforeUses = before.Care!.Ordinary!.Uses.Count, afterUses = expected.Candidate.Care!.Ordinary!.Uses.Count,
            beforeConsumed = before.Production!.ConsumedItems.Count, afterConsumed = expected.Candidate.Production!.ConsumedItems.Count,
            beforeCareRevision = before.Care.Revision, afterCareRevision = expected.Candidate.Care.Revision,
            beforePermissionActive = before.Care.Ordinary.Permissions.Single().IsActive,
            afterPermissionActive = expected.Candidate.Care.Ordinary.Permissions.Single().IsActive
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string? EvidenceRoot() => Environment.GetEnvironmentVariable("OPENFANTASY_RESTORATIVE_CRASH_EVIDENCE") is { Length: > 0 } root
        ? root : null;
}
