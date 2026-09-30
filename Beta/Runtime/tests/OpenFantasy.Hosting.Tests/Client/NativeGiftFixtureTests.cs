using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Client;

/// <summary>Earns a fresh native gift fixture through actual combat, care, crafting and revocation.</summary>
public sealed class NativeGiftFixtureTests
{
    [Fact]
    public async Task Actual_injury_and_two_crafted_doses_support_one_native_gift_and_an_owned_raw_withdrawal()
    {
        var ordinary = new OrdinaryCareWorldFixture();
        WorldSnapshot genesis = WorldMaterialProposalAdmission.CreateFresh(ordinary.Genesis);
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var journey = new CareWorldSession(authority);
        ordinary.ReachFullDoseInjuredExploration(journey);
        ordinary.Supply(journey, self: false);
        WorldSnapshot state = ordinary.Supply(journey, self: false);
        var permission = state.Care!.Ordinary!.Permissions.Single();
        WorldActionCommand revoke = ordinary.Journey.Command(state, ordinary.Player,
            new RevokeCarePermissionIntent(permission.PermissionId, permission.Revision, permission.GrantVersion));
        WorldSnapshot before = CareRewardJourney.Commit(journey,
            ordinary.Journey.Batch(state, WorldBatchKind.AdvancingStep, [revoke])).Candidate;
        Assert.False(before.Care!.Ordinary!.Permissions.Single().IsActive);
        Assert.Equal((ushort)30, before.ProfileVersion);
        Assert.InRange(before.Progression!.GetActor(ordinary.Player).CurrentHp, 1, 20);
        Assert.Empty(before.Care.Ordinary.Uses);
        Assert.Empty(before.Coordination!.Proposals!.Open);
        Assert.Empty(before.Coordination.Proposals.Outcomes);
        Assert.False(before.HasReadingHold);
        Assert.Null(before.PendingExcavation);
        Assert.Null(before.Care.Pending);
        Assert.True(WorldActorAvailability.CanAct(before, ordinary.Player));
        Assert.True(WorldActorAvailability.CanAct(before, ordinary.Helper));

        EntityId playerBag = before.GetResident(ordinary.Player).InventoryCustodianId;
        EntityId helperBag = before.GetResident(ordinary.Helper).InventoryCustodianId;
        var remedy = before.Care.Remedies.Single();
        TransformedLot[] doses = before.Production!.Lots.Where(value => value.CustodianId == helperBag
            && value.Definition == remedy.Item).OrderBy(value => value.LotId).ToArray();
        Assert.Equal(2, doses.Length);
        Assert.All(doses, dose =>
        {
            Assert.Equal(1UL, dose.Quantity);
            var batch = before.Production.Batches.Single(value => value.BatchId == dose.Output.BatchId);
            Assert.Equal(ordinary.Helper, batch.ActorId);
            Assert.Equal(3UL, batch.Ingredients.Aggregate(0UL, (sum, ingredient) => sum + ingredient.Quantity));
            Assert.All(batch.Ingredients, ingredient => Assert.Equal(IngredientLotKind.Source, ingredient.Kind));
        });
        Assert.DoesNotContain(before.Inventory.Custody.Lots,
            value => value.CustodianId == helperBag && value.Definition == remedy.Item);
        CountedSourceLot raw = before.Inventory.Custody.Lots.Where(value => value.CustodianId == playerBag
            && value.Definition != remedy.Item && value.ReservedBy is null && value.Protection == LotProtection.None)
            .OrderBy(value => value.LotId).First();
        EntityId recipient = ordinary.Journey.Second.TargetActorId;
        Assert.True(before.Progression.GetActor(recipient).CurrentHp > 0);
        Assert.NotEqual(ordinary.Helper, recipient);
        var preparation = Assert.IsType<LocalMaterialProposalPreparation>(
            LocalMaterialProposalPreparation.Project(before, ordinary.Player));
        var outgoing = Assert.IsType<ProposeMaterialIntent>(
            preparation.Prepare(ConsentedMaterialLotKind.CountedSource, raw.LotId, recipient, 1));
        var policy = ScheduledMaterialNpcTests.Policy(before, ordinary.Helper, ordinary.Player, lifetime: 1200);
        var controller = new LocalMaterialGiftController(before, [policy]);
        WorldActionCommand selected = Assert.Single(controller.SelectActions(before, authority.IsIdentityOccupiedForHost,
            new HashSet<EntityId>(), _ => null).Actions);
        var incoming = Assert.IsType<ProposeMaterialIntent>(selected.Intent);
        Assert.Equal((ordinary.Helper, ordinary.Player, doses[0].LotId, 1UL),
            (selected.ActorId, incoming.Terms.RecipientActorId, incoming.Terms.LotId, incoming.Terms.Quantity));
        Assert.Equal(ConsentedMaterialLotKind.Produced, incoming.Terms.LotKind);
        Assert.Empty(new LocalRestorativeController().SelectActions(before, authority.IsIdentityOccupiedForHost,
            new HashSet<EntityId>()));
        Export(authority, genesis, before, ordinary, recipient, raw, doses, selected, revoke);

        // Native files end above, before any gift exists. This independent managed continuation
        // always runs, including when fixture export has not been requested.
        var control = before.Inventory.Controls.Single(value => value.ActorId == ordinary.Player);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(authority, ordinary.Player,
            new(control.PrincipalId, control.SessionId), time, [policy]);
        await Tick(time);
        Assert.Equal(before.ContentHash, authority.ReadStateForHost().ContentHash);
        session.SetReady(true);
        await Tick(time);
        state = authority.ReadStateForHost();
        var offer = Assert.Single(state.Coordination!.Proposals!.Open);
        Assert.Equal(selected.CommandId, offer.CreationCommandId);
        Assert.Equal(before.Inventory.ContentHash, state.Inventory.ContentHash);
        Assert.Equal(before.Production.ContentHash, state.Production!.ContentHash);
        WorldActionCommand accept = ordinary.Journey.Command(state, ordinary.Player,
            Assert.Single(session.ReadView().MaterialProposals!.Offers).PrepareAcceptance());
        Assert.Equal(WorldActionRejection.None,
            (await ScheduledMaterialProposalSessionTests.Submit(session, time, accept)).Rejection);
        state = authority.ReadStateForHost();
        Assert.Equal(playerBag, state.Production!.Lots.Single(value => value.LotId == doses[0].LotId).CustodianId);
        Assert.Equal(1UL, state.Production.Lots.Where(value => value.CustodianId == helperBag
            && value.Definition == remedy.Item).Aggregate(0UL, (sum, lot) => sum + lot.Quantity));
        var create = ordinary.Journey.Command(state, ordinary.Player, outgoing);
        Assert.Equal(WorldActionRejection.None,
            (await ScheduledMaterialProposalSessionTests.Submit(session, time, create)).Rejection);
        state = authority.ReadStateForHost();
        EntityId outgoingId = WorldMaterialProposal.DeriveIdentity(create.CommandId);
        var withdraw = ordinary.Journey.Command(state, ordinary.Player, new WithdrawMaterialProposalIntent(outgoingId));
        Assert.Equal(WorldActionRejection.None,
            (await ScheduledMaterialProposalSessionTests.Submit(session, time, withdraw)).Rejection);
        state = authority.ReadStateForHost();
        Assert.Equal(WorldMaterialProposalOutcomeKind.Withdrawn, state.Coordination!.Proposals!.FindOutcome(outgoingId)!.Kind);
        Assert.Empty(state.Coordination.Proposals.Open);
        Assert.Equal(before.Progression.ContentHash, state.Progression!.ContentHash);
        Assert.Equal(before.Care.ContentHash, state.Care!.ContentHash);
        Assert.Equal(raw, state.Inventory.Custody.GetLot(raw.LotId));
        await session.StopAsync();
    }

    private static void Export(SqliteWorldAuthority authority, WorldSnapshot genesis, WorldSnapshot before,
        OrdinaryCareWorldFixture ordinary, EntityId recipient, CountedSourceLot raw, TransformedLot[] doses,
        WorldActionCommand selected, WorldActionCommand revoke)
    {
        if (Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_GIFT_FIXTURES") is not { Length: > 0 } requested) return;
        string root = Path.GetFullPath(requested);
        Assert.False(Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any(),
            "Native gift fixture export requires a fresh or empty directory; existing evidence is never overwritten.");
        Directory.CreateDirectory(root);
        string save = Path.Combine(root, "save.sqlite");
        string genesisPath = Path.Combine(root, "genesis.ofc");
        string beforePath = Path.Combine(root, "before.ofc");
        string metadataPath = Path.Combine(root, "metadata.json");
        authority.BackupForHost(save);
        File.WriteAllBytes(genesisPath, WorldSnapshotCodec.Encode(genesis));
        File.WriteAllBytes(beforePath, WorldSnapshotCodec.Encode(before));
        var metadata = new
        {
            kind = "native-material-gift-fixture-v1", profileVersion = before.ProfileVersion,
            setup = "Fresh profile30; actual source-stat combat injury, aftermath recovery, victory, two helper crafts and patient revocation. Native begins before gift creation.",
            worldId = before.WorldId.ToString(), branchId = before.BranchId.ToString(),
            genesisHash = genesis.ContentHash.Value, beforeHash = before.ContentHash.Value,
            beforeRevision = before.Revision, beforeAdvancingSteps = before.AdvancingSteps,
            playerId = ordinary.Player.ToString(), helperId = ordinary.Helper.ToString(),
            recipientId = recipient.ToString(), recipientName = before.GetResident(recipient).DisplayName,
            recipientScenario = ordinary.Journey.Second.Key,
            rawLotId = raw.LotId.ToString(), rawQuantity = raw.Quantity, rawRevision = raw.Revision,
            rawSourceId = raw.Source.SourceId.ToString(), rawStart = raw.Source.Start, rawEnd = raw.Source.End,
            rawCustodianId = raw.CustodianId.ToString(),
            incomingCommandId = selected.CommandId.ToString(),
            incomingProposalId = WorldMaterialProposal.DeriveIdentity(selected.CommandId).ToString(),
            incomingLotId = doses[0].LotId.ToString(),
            helperDoses = doses.Select(dose => new
            {
                lotId = dose.LotId.ToString(), custodianId = dose.CustodianId.ToString(), dose.Quantity, dose.Revision,
                batchId = dose.Output.BatchId.ToString(), dose.Output.OutputIndex, dose.Output.Start, dose.Output.End,
                craftCommandId = before.Production!.Batches.Single(batch => batch.BatchId == dose.Output.BatchId).CommandId.ToString()
            }).ToArray(),
            permissionId = before.Care!.Ordinary!.Permissions.Single().PermissionId.ToString(),
            revocationCommandId = revoke.CommandId.ToString(),
            beforeHp = before.Progression!.GetActor(ordinary.Player).CurrentHp,
            beforeOrdinaryUses = before.Care.Ordinary.Uses.Count,
            beforeConsumedItems = before.Production!.ConsumedItems.Count,
            retainedDoses = 1, offerLifetimeSteps = 1200,
            expected = new[]
            {
                new { mode = "keyboard", incoming = "Accepted", outgoing = "Withdrawn", movedDoses = 1 },
                new { mode = "controller", incoming = "Accepted", outgoing = "Withdrawn", movedDoses = 1 },
                new { mode = "pointer", incoming = "Declined", outgoing = "Withdrawn", movedDoses = 0 }
            },
            limits = "No accepted-dose healing, retained-save migration, archive-cut, physical-device or human acceptance claim. The player's original raw dose remains present."
        };
        File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(new
        {
            kind = "native-material-gift-fixtures-v1", save, genesis = genesisPath, before = beforePath, metadata = metadataPath
        }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal(WorldSnapshotCodec.Encode(before),
            WorldSnapshotCodec.Encode(RecordedWorldReader.ReadForHost(save, before.Revision).State));
    }
}
