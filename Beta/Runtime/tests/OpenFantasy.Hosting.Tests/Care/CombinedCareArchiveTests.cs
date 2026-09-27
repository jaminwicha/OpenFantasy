using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed class CombinedCareArchiveTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Actual_pending_care_archives_256_lethal_journey_changes_with_its_single_dose_successor(bool crafted)
    {
        using var files = new StorageFiles();
        var fixture = new CareWorldFixture(crafted, archiveCareBoundary: true);
        WorldSnapshot defeated;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            defeated = fixture.PrepareDefeat(new(authority));
            Assert.Equal(0UL, authority.InspectForHost().Journal.ResourceArchiveCount);
        }
        Assert.Equal(256UL, defeated.Progression!.Revision);
        Assert.Equal(ActorResourceHistory.MaximumBatches, defeated.ResourceHistory!.History.Batches.Count);
        Assert.Equal(256, defeated.ResourceHistory.History.ChangeCount);
        Assert.All(defeated.ResourceHistory.Evidence, value =>
        {
            var attack = Assert.IsType<WorldEncounterResourceEvidence>(value);
            Assert.Equal(fixture.Wolf, attack.Request.ActorId);
            Assert.Equal(fixture.Player, attack.Resolution.TargetActorId);
            Assert.Equal(1U, attack.Resolution.Damage);
        });
        Assert.Equal(256UL, defeated.EncounterActions!.Random.NextCounter);
        var lethal = defeated.EncounterActions.LastResolution!;
        Assert.Equal(1U, lethal.TargetHpBefore);
        Assert.Equal(0U, lethal.TargetHpAfter);
        Assert.Equal(0, defeated.Progression.GetActor(fixture.Player).CurrentHp);

        WorldActionCommand acknowledgment;
        WorldActionReceipt acknowledgmentReceipt;
        WorldSnapshot acknowledged;
        using (var authority = ReopenExact(files.Path, defeated))
        {
            acknowledgment = fixture.Acknowledge(defeated);
            var decision = authority.Commit(fixture.Batch(defeated, WorldBatchKind.Care, [acknowledgment]));
            acknowledgmentReceipt = Assert.Single(decision.Receipts);
            Assert.Equal(CommandReceiptStatus.Applied, acknowledgmentReceipt.Status);
            Assert.Null(decision.SealedResourceSegment);
            acknowledged = decision.Candidate;
            Assert.NotNull(acknowledged.Care!.Pending);
            Assert.Equal(acknowledgment.CommandId, acknowledged.Care.Pending.CommandId);
            Assert.Equal(defeated.ResourceHistory.ContentHash, acknowledged.ResourceHistory!.ContentHash);
            Assert.Equal(0UL, authority.InspectForHost().Journal.ResourceArchiveCount);
            Assert.Equal(defeated.Calendar, acknowledged.Calendar);
        }

        WorldActionCommand administer;
        WorldActionReceipt treatmentReceipt;
        WorldStepBatch treatmentBatch;
        WorldSnapshot treated;
        using (var authority = ReopenExact(files.Path, acknowledged))
        {
            AssertReceipt(authority, acknowledgment, fixture, acknowledgmentReceipt);
            administer = fixture.Administer(acknowledged);
            treatmentBatch = fixture.Batch(acknowledged, WorldBatchKind.Care, [administer]);
            var preview = authority.Preview(treatmentBatch);
            var previewArchive = Assert.IsType<WorldResourceArchiveSegment>(preview.SealedResourceSegment);
            Assert.Equal(WorldSnapshotCodec.Encode(acknowledged), previewArchive.Encode());
            Assert.Equal(acknowledged.ContentHash, authority.ReadStateForHost().ContentHash);
            Assert.Equal(0UL, authority.InspectForHost().Journal.ResourceArchiveCount);
            ExportBefore(authority, crafted, acknowledged, treatmentBatch, preview);

            var decision = authority.Commit(treatmentBatch);
            treated = decision.Candidate;
            treatmentReceipt = Assert.Single(decision.Receipts);
            Assert.Equal(CommandReceiptStatus.Applied, treatmentReceipt.Status);
            Assert.Equal(preview.Candidate.ContentHash, treated.ContentHash);
            Assert.Equal(previewArchive.Encode(), Assert.IsType<WorldResourceArchiveSegment>(decision.SealedResourceSegment).Encode());
            Assert.Equal(1UL, authority.InspectForHost().Journal.ResourceArchiveCount);
            Assert.Equal((ulong)previewArchive.Encode().Length, authority.InspectForHost().Journal.ResourceArchiveBytes);
            Assert.Equal(acknowledged.Revision + 1, treated.Revision);
            Assert.Equal(acknowledged.Revision, treated.ResourceHistory!.History.BasisWorldSequence);
            Assert.Equal(256UL, treated.ResourceHistory.History.Basis.Revision);
            var resource = Assert.Single(treated.ResourceHistory.History.Batches);
            Assert.Equal(administer.CommandId, resource.CommandId);
            Assert.Equal(0U, resource.CommandOrdinal);
            Assert.Equal(treated.Revision, resource.WorldSequence);
            Assert.Equal(ActorResourceOperation.HealthRestoration, Assert.Single(resource.Changes).Operation);
            Assert.IsType<WorldTreatmentResourceEvidence>(Assert.Single(treated.ResourceHistory.Evidence));
            Assert.Null(treated.Care!.Pending);
            Assert.Single(treated.Care.Treatments);
            Assert.Equal(40, treated.Progression!.GetActor(fixture.Player).CurrentHp);
            var consumption = Assert.Single(treated.Production!.ConsumedItems);
            Assert.Equal(administer.CommandId, consumption.CommandId);
            Assert.Equal(fixture.Helper, consumption.ActorId);
            Assert.Equal(fixture.Player, consumption.RecipientActorId);
            Assert.Equal(1UL, consumption.Ingredient.Quantity);
            Assert.Equal(crafted ? IngredientLotKind.Transformed : IngredientLotKind.Source, consumption.Ingredient.Kind);
            Assert.Equal(crafted ? 0UL : 1UL, LivePotions(treated, fixture));
            Assert.Equal(LivePotions(acknowledged, fixture) - 1, LivePotions(treated, fixture));
            Assert.Equal(acknowledged.Calendar, treated.Calendar);
            Assert.Equal(acknowledged.AdvancingSteps, treated.AdvancingSteps);
            Assert.Equal(acknowledged.EncounterLifecycle!.TacticalTicks, treated.EncounterLifecycle!.TacticalTicks);
            Assert.Equal(EncounterLifecyclePhase.Active, treated.EncounterLifecycle.Phase);
            Assert.True(treated.Pacing!.Encounter!.GetActor(fixture.Helper).RemainingDelay > 0);
            Assert.Equivalent(lethal, treated.EncounterActions!.LastResolution!, strict: true);
            ExportAfter(authority, crafted);
        }

        WorldSnapshot attacked;
        using (var authority = ReopenExact(files.Path, treated))
        {
            AssertReceipt(authority, acknowledgment, fixture, acknowledgmentReceipt);
            AssertReceipt(authority, administer, fixture, treatmentReceipt);
            var retrieved = authority.ReadResourceArchiveForHost(0);
            Assert.Equal(treated.Revision, retrieved.PublicationSequence);
            Assert.Equal(treated.Revision, retrieved.SourceHeadRevision);
            Assert.Equal(treated.ContentHash, retrieved.SourceHeadHash);
            Assert.Equal(WorldSnapshotCodec.Encode(acknowledged), retrieved.Segment.Encode());
            var archived = WorldSnapshotCodec.Decode(retrieved.Segment.Encode());
            Assert.Equal(256, archived.ResourceHistory!.History.Batches.Count);
            Assert.Equal(acknowledgment.CommandId, archived.Care!.Pending!.CommandId);
            Assert.Equivalent(lethal, archived.Care.Pending.Defeat, strict: true);
            Assert.Equal(WorldSnapshotCodec.Encode(treated), WorldSnapshotCodec.Encode(
                WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(treated), retrieved.Segment.Anchor)));
            Assert.Equal(acknowledged.ContentHash, authority.ReadRecordedStateForHost(acknowledged.Revision).StateHash);

            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(treatmentBatch));
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(
                fixture.Batch(treated, WorldBatchKind.Care, [administer])));
            Assert.Equal(treated.ContentHash, authority.ReadStateForHost().ContentHash);

            var oldAttack = Assert.IsType<WorldEncounterResourceEvidence>(archived.ResourceHistory.Evidence[0]);
            Assert.False(treated.IdentityIndex.Contains(oldAttack.CommandId));
            var control = treated.Inventory.Controls.Single(value => value.ActorId == fixture.Helper);
            var reused = new WorldActionCommand(oldAttack.CommandId, control.PrincipalId, control.SessionId,
                fixture.Helper, treated.WorldId, treated.BranchId, treated.Space.GetActor(fixture.Helper).Revision, administer.Intent);
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(fixture.Batch(treated, WorldBatchKind.Care, [reused])));
            Assert.Equal(treated.ContentHash, authority.ReadStateForHost().ContentHash);

            // A fresh identity does not make the pre-treatment offer current again. Its terminal
            // rejection is recorded without another dose, effect or archive publication.
            var stale = fixture.Command(treated, fixture.Helper, administer.Intent);
            var refused = authority.Commit(fixture.Batch(treated, WorldBatchKind.Care, [stale]));
            var refusal = Assert.Single(refused.Receipts);
            Assert.Equal(CommandReceiptStatus.Rejected, refusal.Status);
            Assert.Equal(WorldActionRejection.Unavailable, refusal.Rejection);
            Assert.Null(refused.SealedResourceSegment);
            Assert.Equal(treated.Progression.ContentHash, refused.Candidate.Progression!.ContentHash);
            Assert.Equal(treated.Production!.ContentHash, refused.Candidate.Production!.ContentHash);
            Assert.Equal(treated.Inventory.ContentHash, refused.Candidate.Inventory.ContentHash);
            Assert.Equal(treated.Care!.ContentHash, refused.Candidate.Care!.ContentHash);
            Assert.Equal(treated.ResourceHistory!.ContentHash, refused.Candidate.ResourceHistory!.ContentHash);
            Assert.Equal(1UL, authority.InspectForHost().Journal.ResourceArchiveCount);

            var session = new CareWorldSession(authority);
            attacked = refused.Candidate;
            for (int step = 0; attacked.Progression!.Revision == treated.Progression.Revision; step++)
            {
                Assert.True(step < 128, "A real resumed attack must occur within the existing readiness bound.");
                attacked = fixture.NextCombatBoundary(session, playerAttacks: true);
            }
            Assert.Equal(2, attacked.ResourceHistory!.History.Batches.Count);
            Assert.IsType<WorldEncounterResourceEvidence>(attacked.ResourceHistory.Evidence[^1]);
            Assert.Equal(treated.Care.ContentHash, attacked.Care!.ContentHash);
            Assert.Equal(treated.Production.ContentHash, attacked.Production!.ContentHash);
        }
        using (var authority = ReopenExact(files.Path, attacked))
        {
            AssertReceipt(authority, acknowledgment, fixture, acknowledgmentReceipt);
            AssertReceipt(authority, administer, fixture, treatmentReceipt);
            Assert.Equal(1UL, authority.InspectForHost().Journal.ResourceArchiveCount);
            Assert.Single(authority.ReadStateForHost().Production!.ConsumedItems);
        }
    }

    private static ulong LivePotions(WorldSnapshot world, CareWorldFixture fixture) =>
        world.Inventory.Custody.Lots.Where(value => value.CustodianId == fixture.HelperStorage && value.Definition == fixture.Potion)
            .Aggregate(0UL, (sum, value) => sum + value.Quantity)
        + world.Production!.Lots.Where(value => value.CustodianId == fixture.HelperStorage && value.Definition == fixture.Potion)
            .Aggregate(0UL, (sum, value) => sum + value.Quantity);

    private static SqliteWorldAuthority ReopenExact(string path, WorldSnapshot expected)
    {
        var authority = SqliteWorldAuthority.OpenExisting(path);
        try
        {
            Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            return authority;
        }
        catch
        {
            authority.Dispose();
            throw;
        }
    }

    private static void AssertReceipt(SqliteWorldAuthority authority, WorldActionCommand command,
        CareWorldFixture fixture, WorldActionReceipt expected) => Assert.Equal(WorldReceiptCodecV1.Encode(expected),
        WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(authority.FindReceipt(command, fixture.DeliveryFor(command.ActorId)))));

    private static void ExportBefore(SqliteWorldAuthority authority, bool crafted, WorldSnapshot before,
        WorldStepBatch batch, WorldStepDecision expected)
    {
        if (Environment.GetEnvironmentVariable("OPENFANTASY_COMBINED_CARE_EVIDENCE") is not { Length: > 0 } root)
            return;
        string fixture = crafted ? "crafted-care" : "raw-care";
        string directory = Path.Combine(root, fixture);
        Directory.CreateDirectory(directory);
        authority.BackupForHost(Path.Combine(directory, "before.sqlite3"));
        File.WriteAllBytes(Path.Combine(directory, "before.ofc"), WorldSnapshotCodec.Encode(before));
        File.WriteAllBytes(Path.Combine(directory, "batch.ofc"), WorldStepCodec.Encode(batch));
        File.WriteAllBytes(Path.Combine(directory, "after.ofc"), WorldSnapshotCodec.Encode(expected.Candidate));
        File.WriteAllText(Path.Combine(directory, "metadata.json"), JsonSerializer.Serialize(new
        {
            fixture,
            beforeRevision = before.Revision,
            beforeHash = before.ContentHash.Value,
            afterRevision = expected.Candidate.Revision,
            afterHash = expected.Candidate.ContentHash.Value,
            archiveCountBefore = 0,
            archiveCountAfter = 1,
            newArchiveOrdinal = 0,
            resourceBatchesBefore = before.ResourceHistory!.History.Batches.Count,
            resourceBatchesAfter = expected.Candidate.ResourceHistory!.History.Batches.Count,
            defeatBoundaryLimit = 1600
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void ExportAfter(SqliteWorldAuthority authority, bool crafted)
    {
        if (Environment.GetEnvironmentVariable("OPENFANTASY_COMBINED_CARE_EVIDENCE") is not { Length: > 0 } root)
            return;
        authority.BackupForHost(Path.Combine(root, crafted ? "crafted-care" : "raw-care", "after.sqlite3"));
    }
}
