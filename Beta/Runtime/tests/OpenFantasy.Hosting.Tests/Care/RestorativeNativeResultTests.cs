using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>Authenticates closed native-result journals; native input/rendering evidence belongs to the runner.</summary>
public sealed class RestorativeNativeResultTests
{
    [Fact]
    public void Opt_in_native_restorative_results_replay_exact_real_doses_permissions_and_resource_boundaries()
    {
        if (Environment.GetEnvironmentVariable("OPENFANTASY_RESTORATIVE_NATIVE_RESULTS") is not { Length: > 0 } manifestPath) return;
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        var journeys = manifest.RootElement.GetProperty("journeys").EnumerateArray().ToArray();
        Assert.Equal(new[] { "crafted-controller", "helper", "raw-keyboard", "raw-pointer", "revoke" },
            journeys.Select(value => Text(value, "label")).OrderBy(value => value, StringComparer.Ordinal).ToArray());
        Assert.Equal(new[] { "crafted-self", "helper", "raw-self", "revoke" },
            journeys.Select(value => Text(value, "name")).Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray());
        Assert.Equal(journeys.Length, journeys.Select(value => Text(value, "database")).Distinct(StringComparer.Ordinal).Count());
        foreach (var journey in journeys) Verify(journey);
    }

    private static void Verify(JsonElement journey)
    {
        string name = Text(journey, "name");
        var expectedRoute = Text(journey, "label") switch
        {
            "raw-pointer" => (Name: "raw-self", Mode: "pointer"),
            "raw-keyboard" => (Name: "raw-self", Mode: "keyboard"),
            "crafted-controller" => (Name: "crafted-self", Mode: "controller"),
            "helper" => (Name: "helper", Mode: "helper"),
            "revoke" => (Name: "revoke", Mode: "revoke"),
            _ => throw new InvalidDataException("Unsupported native restorative route.")
        };
        Assert.Equal(expectedRoute.Name, name);
        string databasePath = Text(journey, "database");
        string fixturePath = Text(journey, "fixtureDirectory");
        Assert.True(Path.IsPathFullyQualified(databasePath));
        Assert.True(Path.IsPathFullyQualified(fixturePath));
        Assert.True(File.Exists(Text(journey, "activeReport")));
        Assert.True(File.Exists(Text(journey, "reopenReport")));
        using var activeReport = JsonDocument.Parse(File.ReadAllBytes(Text(journey, "activeReport")));
        using var reopenReport = JsonDocument.Parse(File.ReadAllBytes(Text(journey, "reopenReport")));
        Assert.Equal("native-restorative-v1", Text(activeReport.RootElement, "Kind"));
        Assert.Equal("native-restorative-v1", Text(reopenReport.RootElement, "Kind"));
        Assert.Equal("passed", Text(activeReport.RootElement, "Status"));
        Assert.Equal("passed", Text(reopenReport.RootElement, "Status"));
        Assert.Equal(expectedRoute.Mode, Text(activeReport.RootElement, "Mode"));
        Assert.Equal("reopen", Text(reopenReport.RootElement, "Mode"));
        using var metadataDocument = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixturePath, "metadata.json")));
        var metadata = metadataDocument.RootElement;
        Assert.Equal(name, Text(metadata, "fixture"));
        Assert.True(metadata.GetProperty("sourceStat").GetBoolean());
        var fixture = WorldSnapshotCodec.Decode(File.ReadAllBytes(Path.Combine(fixturePath, "before.ofc")));
        Assert.Equal(metadata.GetProperty("beforeRevision").GetUInt64(), fixture.Revision);
        Assert.Equal(Text(metadata, "beforeHash"), fixture.ContentHash.Value);
        EntityId player = EntityId.Parse(Text(metadata, "playerActorId"));
        EntityId helper = EntityId.Parse(Text(metadata, "helperActorId"));
        Assert.Equal(Text(metadata, "worldId"), fixture.WorldId.ToString());
        Assert.Equal(Text(metadata, "branchId"), fixture.BranchId.ToString());
        Assert.Equal(Text(metadata, "admissionHash"), fixture.AdmissionVersion.Value);

        StoredWorldCommit[] commits;
        StoredWorldCommandOutcome[] outcomes;
        using (var stored = WorldDatabase.Open(databasePath, create: false))
        {
            commits = stored.ReadJournal(fixture.Revision).ToArray();
            outcomes = stored.ReadCommandOutcomes(fixture.Revision).ToArray();
        }
        Assert.NotEmpty(commits);
        var ordinary = outcomes.Select(value => (Row: value, Command: WorldActionCodec.Decode(value.Command)))
            .Where(value => value.Command.Intent is UseRestorativeItemIntent or RevokeCarePermissionIntent).ToArray();
        var selected = Assert.Single(ordinary);
        var command = selected.Command;
        var receipt = WorldReceiptCodecV1.Decode(selected.Row.Receipt);
        Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
        Assert.Equal(selected.Row.Sequence, receipt.CommitSequence);
        Assert.Equal(command.CommandId, receipt.CommandId);
        Assert.Equal(command.ContentHash, receipt.CommandHash);
        var committed = commits.Single(value => value.Sequence == selected.Row.Sequence);
        var batch = WorldStepCodec.Decode(committed.AcceptedBatch);
        Assert.Equal(WorldBatchKind.AdvancingStep, batch.Kind);
        Assert.Equal(WorldActionCodec.Encode(command), WorldActionCodec.Encode(Assert.Single(batch.Actions)));
        Assert.Equal(Text(metadata, "expectedActorId"), command.ActorId.ToString());
        Assert.Equal(fixture.WorldId, command.WorldId);
        Assert.Equal(fixture.BranchId, command.BranchId);

        // Each historical read replays the closed journal and authenticates receipts, identities,
        // checkpoints and the final root before returning this exact immutable boundary.
        var before = RecordedWorldReader.ReadForHost(databasePath, selected.Row.Sequence - 1);
        var after = RecordedWorldReader.ReadForHost(databasePath, selected.Row.Sequence);
        Assert.Equal(before.SourceHeadHash, after.SourceHeadHash);
        Assert.Equal(before.SourceHeadRevision, after.SourceHeadRevision);
        Assert.Equal(committed.BeforeHash, before.StateHash);
        Assert.Equal(committed.AfterHash, after.StateHash);
        Assert.Equal(batch.ExpectedRevision, before.State.Revision);
        Assert.Equal(before.State.Calendar.AddMilliseconds(1_800), after.State.Calendar);
        Assert.Equal(before.State.AdvancingSteps + 1, after.State.AdvancingSteps);
        Assert.Equal(before.State.Space.Actors.Select(value => (value.ActorId, value.Feet, value.Revision)),
            after.State.Space.Actors.Select(value => (value.ActorId, value.Feet, value.Revision)));
        Assert.Equal(fixture.Space.Actors.Select(value => (value.ActorId, value.Feet)),
            after.State.Space.Actors.Select(value => (value.ActorId, value.Feet)));
        Assert.Equal(fixture.Residents.Select(value => value.ActorId), after.State.Residents.Select(value => value.ActorId));
        var control = before.State.Inventory.Controls.Single(value => value.ActorId == command.ActorId);
        Assert.Equal(control.PrincipalId, command.PrincipalId);
        Assert.Equal(control.SessionId, command.OriginSessionId);
        Assert.Equal(before.State.Space.GetActor(command.ActorId).Revision, command.ExpectedActorRevision);
        Assert.Equal(before.State.Care!.Acknowledgments.Count, after.State.Care!.Acknowledgments.Count);
        Assert.Equal(before.State.Care.Treatments.Count, after.State.Care.Treatments.Count);
        Assert.Null(after.State.Care.Pending);
        Assert.Equivalent(before.State.Progression!.Currency, after.State.Progression!.Currency, strict: true);
        Assert.Equal(before.State.Progression.GetActor(player).CurrentMp, after.State.Progression.GetActor(player).CurrentMp);
        foreach (var actor in before.State.Progression.Actors.Where(value => value.ActorId != player))
            Assert.Equivalent(actor, after.State.Progression.GetActor(actor.ActorId), strict: true);

        if (name == "revoke") VerifyRevocation(fixture, before.State, after.State, command, player);
        else VerifyUse(metadata, name, fixture, before.State, after.State, command, player, helper);
        Assert.Equal(metadata.GetProperty("expectedHp").GetInt32(), after.State.Progression.GetActor(player).CurrentHp);
        Assert.Equal(metadata.GetProperty("expectedMp").GetInt32(), after.State.Progression.GetActor(player).CurrentMp);
        Assert.Equal(metadata.GetProperty("expectedOrdinaryUses").GetInt32(), after.State.Care.Ordinary!.Uses.Count);
        Assert.Equal(metadata.GetProperty("expectedConsumedDoses").GetInt32(), after.State.Production!.ConsumedItems.Count);
        Assert.Equal(metadata.GetProperty("expectedPermissionActive").GetBoolean(), after.State.Care.Ordinary.Permissions.Single().IsActive);

        byte[] closedBytes = File.ReadAllBytes(databasePath);
        using (var recovered = SqliteWorldAuthority.OpenExisting(databasePath))
        {
            var final = recovered.ReadStateForHost();
            Assert.Equal(after.SourceHeadHash, final.ContentHash);
            Assert.Equal(after.SourceHeadRevision, final.Revision);
            Assert.Equal(WorldSnapshotCodec.Encode(fixture), WorldSnapshotCodec.Encode(recovered.ReadRecordedStateForHost(fixture.Revision).State));
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(
                recovered.FindReceipt(command, new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId)))));
            Assert.Equal(after.State.Care.ContentHash, final.Care!.ContentHash);
            Assert.Equal(after.State.Production.ContentHash, final.Production!.ContentHash);
            Assert.Equal(after.State.Progression.ContentHash, final.Progression!.ContentHash);
            Assert.Equal(after.State.Inventory.ContentHash, final.Inventory.ContentHash);
            Assert.True(recovered.IsIdentityOccupiedForHost(command.CommandId));
        }
        Assert.Equal(closedBytes, File.ReadAllBytes(databasePath));
    }

    private static void VerifyUse(JsonElement metadata, string name, WorldSnapshot fixture, WorldSnapshot before,
        WorldSnapshot after, WorldActionCommand command, EntityId player, EntityId helper)
    {
        var intent = Assert.IsType<UseRestorativeItemIntent>(command.Intent);
        Assert.Equal(player, intent.RecipientActorId);
        Assert.Equal(name == "helper" ? helper : player, command.ActorId);
        Assert.Equal(name == "helper" ? ResidentControllerKind.Host : ResidentControllerKind.Player,
            before.GetResident(command.ActorId).ControllerKind);
        Assert.Equal(WorldActionRejection.None, WorldRestorativeActions.Evaluate(before, command.ActorId, intent));
        Assert.Equal(Text(metadata, "expectedIngredientKind"), intent.Ingredient.Kind.ToString());
        Assert.Equal(Text(metadata, "expectedIngredientLotId"), intent.Ingredient.LotId.ToString());
        Assert.Equal(metadata.GetProperty("expectedIngredientRevision").GetUInt64(), intent.Ingredient.ExpectedRevision);
        Assert.Equal(1UL, intent.Ingredient.Quantity);
        Assert.Equal(Text(metadata, "expectedUseDefinitionId"), intent.UseDefinitionId.ToString());
        Assert.Equal(Text(metadata, "expectedUseDefinitionVersion"), intent.UseDefinitionVersion.Value);
        var use = Assert.Single(after.Care!.Ordinary!.Uses, value => value.WorldSequence > fixture.Revision);
        Assert.Equal(command.CommandId, use.CommandId);
        Assert.Equal(after.Revision, use.WorldSequence);
        Assert.Equal(0U, use.CommandOrdinal);
        Assert.Equal(command.ActorId, use.ActorId);
        Assert.Equal(player, use.RecipientActorId);
        var consumed = Assert.Single(after.Production!.ConsumedItems, value => !fixture.Production!.ConsumedItems
            .Any(old => old.ConsumptionId == value.ConsumptionId));
        Assert.Equal(use.ConsumptionId, consumed.ConsumptionId);
        Assert.Equal(command.CommandId, consumed.CommandId);
        Assert.Equal(command.ActorId, consumed.ActorId);
        Assert.Equal(player, consumed.RecipientActorId);
        Assert.Equal(before.GetResident(command.ActorId).InventoryCustodianId, consumed.CustodianId);
        Assert.Equal(intent.Ingredient.LotId, consumed.Ingredient.LotId);
        Assert.Equal(intent.Ingredient.Kind, consumed.Ingredient.Kind);
        Assert.Equal(intent.Ingredient.ExpectedRevision, consumed.Ingredient.Revision);
        Assert.Equal(1UL, consumed.Ingredient.Quantity);
        var patient = before.Progression!.GetActor(player);
        Assert.Equal(Math.Min(patient.Stats.MaxHp, patient.CurrentHp + 40), after.Progression!.GetActor(player).CurrentHp);
        Assert.Equal(patient.Revision + 1, after.Progression.GetActor(player).Revision);
        Assert.Equal(before.Care!.Ordinary!.Permissions.Single().Version, after.Care.Ordinary.Permissions.Single().Version);
        if (name == "helper")
        {
            Assert.Equal(Text(metadata, "expectedHelperCommandId"), command.CommandId.ToString());
            var permission = before.Care.Ordinary.Permissions.Single();
            Assert.True(permission.IsActive);
            Assert.Equal(permission.PermissionId, use.PermissionId);
            Assert.Equal(permission.GrantVersion, use.PermissionGrantVersion);
            Assert.Equal(permission.Revision, use.PermissionRevision);
        }
        else Assert.Null(use.PermissionId);
        if (intent.Ingredient.Kind == IngredientLotKind.Transformed)
        {
            var dose = before.Production!.Lots.Single(value => value.LotId == intent.Ingredient.LotId);
            var crafted = before.Production.Batches.Single(value => value.BatchId == dose.Output.BatchId);
            Assert.Equal(command.ActorId, crafted.ActorId);
            Assert.Equal(Text(metadata, "expectedProductionBatchId"), crafted.BatchId.ToString());
            Assert.Equal(Text(metadata, "expectedProductionCommandId"), crafted.CommandId.ToString());
            Assert.Equal(Text(metadata, "expectedProductionRecipeId"), crafted.RecipeId.ToString());
            Assert.Equal(3UL, crafted.Ingredients.Aggregate(0UL, (total, value) => total + value.Quantity));
            Assert.DoesNotContain(after.Production.Lots, value => value.LotId == dose.LotId);
        }
        else
        {
            var dose = before.Inventory.Custody.Lots.Single(value => value.LotId == intent.Ingredient.LotId);
            Assert.Equal(Text(metadata, "expectedSourceId"), dose.Source.SourceId.ToString());
            Assert.Equal(metadata.GetProperty("expectedSourceStart").GetUInt64(), dose.Source.Start);
            Assert.Equal(metadata.GetProperty("expectedSourceEnd").GetUInt64(), dose.Source.End);
            Assert.DoesNotContain(after.Inventory.Custody.Lots, value => value.LotId == dose.LotId);
        }
    }

    private static void VerifyRevocation(WorldSnapshot fixture, WorldSnapshot before, WorldSnapshot after,
        WorldActionCommand command, EntityId player)
    {
        var intent = Assert.IsType<RevokeCarePermissionIntent>(command.Intent);
        Assert.Equal(player, command.ActorId);
        var prior = before.Care!.Ordinary!.GetPermission(intent.PermissionId);
        Assert.True(prior.IsActive);
        Assert.Equal(prior.Revision, intent.ExpectedPermissionRevision);
        Assert.Equal(prior.GrantVersion, intent.ExpectedPermissionGrantVersion);
        var permission = after.Care!.Ordinary!.GetPermission(intent.PermissionId);
        var revoked = Assert.IsType<CarePermissionRevocation>(permission.Revocation);
        Assert.Equal(command.CommandId, revoked.CommandId);
        Assert.Equal(player, revoked.PatientActorId);
        Assert.Equal(after.Revision, revoked.WorldSequence);
        Assert.True(revoked.WorldSequence > fixture.Revision);
        Assert.Equal(0U, revoked.CommandOrdinal);
        Assert.False(permission.IsActive);
        Assert.Equal(prior.GrantVersion, permission.GrantVersion);
        Assert.Equal(before.Care.Ordinary.Uses.Count, after.Care.Ordinary.Uses.Count);
        Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
        Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
        Assert.Equal(before.Progression!.ContentHash, after.Progression!.ContentHash);
        Assert.Equal(before.ResourceHistory!.ContentHash, after.ResourceHistory!.ContentHash);
    }

    private static string Text(JsonElement value, string property) => value.GetProperty(property).GetString()
        ?? throw new InvalidDataException($"Native verification requires {property}.");
}
