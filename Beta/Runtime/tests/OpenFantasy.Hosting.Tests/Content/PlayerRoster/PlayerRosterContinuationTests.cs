using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Content.PlayerRoster;

public sealed class PlayerRosterContinuationTests
{
    [Fact]
    public void Four_residents_preserve_actual_funded_npc_construction_and_completed_save_recovery()
    {
        using var files = new StorageFiles();
        var commands = new ConstructionUpgradeFixture();
        WorldSnapshot genesis = PlayerRosterAdmissionTests.Four;
        var quest = Assert.Single(genesis.Quests!.Definitions);
        EntityId player = genesis.EncounterLifecycle!.Scenarios.Single(value =>
            value.Key == OpenFantasy.Hosting.Content.EncounterJourneyAdmission.FirstScenarioKey).InitiatingActorId;
        WorldSnapshot final;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            WorldSnapshot state = authority.ReadStateForHost();
            Assert.Equal(SpatialInteraction.Reachable, state.Space.QueryInteraction(player, quest.GiverActorId));
            state = commands.Act(authority, state, player, new SpeakToActorIntent(quest.GiverActorId,
                state.Space.GetActor(quest.GiverActorId).Revision));
            state = commands.Act(authority, state, player, new AcceptDeliveryQuestIntent(quest.DefinitionId,
                quest.Version, quest.GiverActorId, state.Space.GetActor(quest.GiverActorId).Revision, 0,
                state.Progression!.GetActor(player).Revision));
            EntityId bag = state.GetResident(player).InventoryCustodianId;
            var wood = Assert.Single(state.Inventory.Custody.Lots, lot => lot.CustodianId == bag && lot.Definition == quest.Item);
            state = commands.Act(authority, state, player, new CompleteDeliveryQuestIntent(quest.DefinitionId,
                quest.Version, quest.GiverActorId, state.Space.GetActor(quest.GiverActorId).Revision, 1,
                state.Progression!.GetActor(player).Revision, state.Inventory.Custody.GetStorage(bag).Revision,
                [new(wood.LotId, wood.Revision, quest.Quantity)]));
            state = commands.Act(authority, state, player, new CloseDialogueIntent(Assert.Single(state.Dialogues).DialogueId));
            Assert.Equal(ActorQuestStatus.Completed, state.Quests!.FindParticipation(player, quest.DefinitionId)!.Status);
            var supplied = state;
            var controller = new LocalNpcController();
            for (int step = 0; !Assert.Single(state.Construction!.Projects).IsCompleted; step++)
            {
                Assert.True(step < 512, $"The real builder must complete through the four-resident geometry; work={state.Construction.Projects[0].Progress.CompletedUnits}.");
                WorldStepDecision decision = authority.Commit(commands.Batch(state,
                    controller.SelectActions(state, commands.NextIdentity), controller.SelectMovement(state)));
                Assert.All(decision.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
                state = decision.Candidate;
            }
            var complete = Assert.Single(state.Construction!.Projects);
            Assert.Equal(complete.Definition.RequiredWork.Value, complete.Progress.CompletedUnits);
            Assert.Equal(ResidentControllerKind.Host, state.GetResident(complete.BuilderActorId).ControllerKind);
            Assert.Single(state.Space.Structures);
            Assert.Single(state.Production!.Batches);
            Assert.DoesNotContain(state.Inventory.Custody.Lots, lot => lot.ReservedBy == complete.ReservationId);
            var installed = Assert.Single(state.Production.Lots, lot => lot.ReservedBy == complete.ReservationId);
            Assert.Equal((1UL, complete.Installed!.GuardLotId), (installed.Quantity, installed.LotId));
            Assert.Equal(3UL, Assert.Single(state.Production.Lots, lot => lot.ReservedBy is null).Quantity);
            Assert.Equal(supplied.Quests!.ContentHash, state.Quests!.ContentHash);
            Assert.Equal(supplied.Progression!.ContentHash, state.Progression!.ContentHash);
            AssertRosterAndCatalog(genesis, state);
            foreach (var human in genesis.Residents.Where(value => value.ControllerKind == ResidentControllerKind.Player))
            {
                Assert.Equal(genesis.Space.GetActor(human.ActorId).Feet, state.Space.GetActor(human.ActorId).Feet);
                if (human.ActorId == player) continue;
                Assert.Equal(genesis.Inventory.Custody.Lots.Where(lot => lot.CustodianId == human.InventoryCustodianId)
                    .Select(lot => (lot.LotId, lot.Quantity, lot.Revision)),
                    state.Inventory.Custody.Lots.Where(lot => lot.CustodianId == human.InventoryCustodianId)
                        .Select(lot => (lot.LotId, lot.Quantity, lot.Revision)));
            }
            final = state;
        }
        using var recovered = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
        Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(recovered.ReadStateForHost()));
        AssertRosterAndCatalog(genesis, recovered.ReadStateForHost());
    }

    [Fact]
    public void Real_saved_proposals_trigger_automatic_archive_preserving_v2_roster_and_pending_offer_across_reopen()
    {
        using var files = new StorageFiles();
        var commands = new ConstructionUpgradeFixture();
        WorldSnapshot genesis = PlayerRosterAdmissionTests.Four;
        var humans = genesis.Residents.Where(value => value.ControllerKind == ResidentControllerKind.Player).ToArray();
        EntityId sender = humans[0].ActorId;
        EntityId recipient = humans.First(value => value.ActorId != sender
            && genesis.Space.QueryInteraction(sender, value.ActorId) == SpatialInteraction.Reachable).ActorId;
        var lot = genesis.Inventory.Custody.Lots.First(value => value.CustodianId == genesis.GetResident(sender).InventoryCustodianId);
        var terms = new ConsentedMaterialTerms(genesis.WorldId, genesis.BranchId, sender, recipient,
            ConsentedMaterialLotKind.CountedSource, lot.LotId, lot.Definition, lot.CustodianId,
            genesis.GetResident(recipient).InventoryCustodianId, 1, lot.Revision);
        WorldSnapshot final;
        WorldSnapshot? closing = null;
        WorldMaterialCareArchiveSegment? segment = null;
        WorldActionCommand? carriedCommand = null;
        WorldActionReceipt? carriedReceipt = null;
        WorldActionCommand? terminalCommand = null;
        WorldActionReceipt? terminalReceipt = null;
        ulong publication = 0;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            WorldActionCommand Command(WorldActionIntent intent)
            {
                WorldSnapshot state = authority.ReadStateForHost();
                var delivery = PlayerRosterAdmissionTests.Delivery(state, sender);
                return new(commands.NextIdentity(), delivery.PrincipalId, delivery.SessionId, sender,
                    state.WorldId, state.BranchId, state.Space.GetActor(sender).Revision, intent);
            }
            WorldActionReceipt Submit(WorldActionCommand command)
            {
                WorldSnapshot before = authority.ReadStateForHost();
                WorldStepDecision decision = authority.Commit(commands.Batch(before, [command], []));
                var receipt = Assert.Single(decision.Receipts);
                Assert.True(receipt.Status == CommandReceiptStatus.Applied, $"{command.Intent.GetType().Name}: {receipt.Rejection}");
                if (decision.SealedMaterialCareSegment is { } actual)
                {
                    Assert.Null(segment);
                    closing = before;
                    segment = actual;
                    publication = decision.Candidate.Revision;
                    Assert.Equal(WorldSnapshotCodec.Encode(before), actual.Encode());
                    Assert.Equal(2, actual.Continued.EncounterLifecycle!.CatalogVersion);
                    Assert.Equal(before.EncounterLifecycle!.ContentHash, actual.Continued.EncounterLifecycle.ContentHash);
                }
                AssertRosterAndCatalog(genesis, decision.Candidate);
                return receipt;
            }
            carriedCommand = Command(new ProposeMaterialIntent(terms, 1_024));
            carriedReceipt = Submit(carriedCommand);
            EntityId carried = WorldMaterialProposal.DeriveIdentity(carriedCommand.CommandId);
            // Offers pin real stock without multiplying or moving it. The normal outcome-count
            // policy eventually seals history while the first genuine offer is still open.
            for (int offer = 0; segment is null; offer++)
            {
                Assert.True(offer < WorldMaterialProposalBook.MaximumOutcomes);
                WorldSnapshot state = authority.ReadStateForHost();
                var create = Command(new ProposeMaterialIntent(terms, state.AdvancingSteps + 32));
                Submit(create);
                Submit(Command(new WithdrawMaterialProposalIntent(WorldMaterialProposal.DeriveIdentity(create.CommandId))));
            }
            Assert.Equal(1UL, authority.InspectForHost().Journal.MaterialCareArchiveCount);
            Assert.Equal(0UL, authority.InspectForHost().Journal.ResourceArchiveCount);
            Assert.NotNull(closing!.Coordination!.Proposals!.FindOpen(carried));
            Assert.NotNull(authority.ReadStateForHost().Coordination!.Proposals!.FindOpen(carried));
            Assert.Equal(genesis.Inventory.ContentHash, authority.ReadStateForHost().Inventory.ContentHash);
            terminalCommand = Command(new WithdrawMaterialProposalIntent(carried));
            terminalReceipt = Submit(terminalCommand);
            final = authority.ReadStateForHost();
            Assert.True(final.Revision > publication);
        }
        byte[] stored = File.ReadAllBytes(files.Path);
        using (var recovered = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis)))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(recovered.ReadStateForHost()));
            AssertRosterAndCatalog(genesis, recovered.ReadStateForHost());
            var archived = recovered.ReadMaterialCareArchiveForHost(0);
            Assert.Equal(segment!.Encode(), archived.Segment.Encode());
            Assert.Equal(publication, archived.PublicationSequence);
            Assert.Equal(closing!.Revision + 1, publication);
            Assert.Equal(WorldSnapshotCodec.Encode(closing), WorldSnapshotCodec.Encode(recovered.ReadRecordedStateForHost(closing.Revision).State));
            var delivery = PlayerRosterAdmissionTests.Delivery(final, sender);
            foreach (var (command, receipt) in new[] { (carriedCommand!, carriedReceipt!), (terminalCommand!, terminalReceipt!) })
            {
                Assert.True(recovered.IsIdentityOccupiedForHost(command.CommandId));
                Assert.Equal(WorldReceiptCodecV1.Encode(receipt),
                    WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(recovered.FindReceipt(command, delivery))));
            }
            Assert.Empty(final.Coordination!.Proposals!.Open);
            Assert.Equal(WorldMaterialProposalOutcomeKind.Withdrawn,
                final.Coordination.Proposals.FindOutcome(WorldMaterialProposal.DeriveIdentity(carriedCommand!.CommandId))!.Kind);
        }
        Assert.Equal(stored, File.ReadAllBytes(files.Path));
    }

    private static void AssertRosterAndCatalog(WorldSnapshot genesis, WorldSnapshot state)
    {
        Assert.Equal(genesis.AdmissionVersion, state.AdmissionVersion);
        Assert.Equal(2, state.EncounterLifecycle!.CatalogVersion);
        Assert.Equal(genesis.EncounterLifecycle!.Scenarios.Select(value => value.Version),
            state.EncounterLifecycle.Scenarios.Select(value => value.Version));
        Assert.Equal(genesis.Residents.Select(value => (value.ActorId, value.ControllerKind, value.InventoryCustodianId)),
            state.Residents.Select(value => (value.ActorId, value.ControllerKind, value.InventoryCustodianId)));
        Assert.Equal(genesis.Inventory.Controls, state.Inventory.Controls);
        Assert.Equal(4, state.Residents.Count(value => value.ControllerKind == ResidentControllerKind.Player));
    }
}
