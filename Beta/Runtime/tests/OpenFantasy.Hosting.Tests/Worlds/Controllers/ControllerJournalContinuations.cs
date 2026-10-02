using OpenFantasy.Core;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.Controllers;

public sealed partial class ControllerJournalTests
{
    [Fact]
    public void Accepted_combat_choice_survives_disconnect_and_must_resolve_before_control_returns()
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = WorldControllerAdmission.CreateFresh(
            CargoOutcomeWorldAdmission.CreateEmbedded("controller-combat", "Traveler"));
        var commands = new EncounterJourneyWorldFixture();
        EntityId actor = Player(genesis);
        WorldSnapshot final;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            WorldSnapshot state = genesis;
            state = EnterFirstEncounter(authority, state, commands);
            Assert.True(WorldEncounterActions.HasRequiredDecisions(state));
            var wait = commands.Wait(state, actor);
            var queued = authority.Commit(commands.Batch(state, WorldBatchKind.EncounterDecision, [wait]));
            Assert.Equal(WorldActionRejection.None, Assert.Single(queued.Receipts).Rejection);
            state = queued.Candidate;
            var before = state;
            state = authority.Commit(Event(state, Disconnect(state, actor), 100)).Candidate;
            state = authority.Commit(Event(state, Expire(state, actor), 101)).Candidate;
            AssertGameplayUnchanged(before, state);
            Assert.False(WorldControllerBoundaries.CanRestoreHuman(state, actor));
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(Event(state, Reconnect(state, actor), 102)));
            Assert.Same(state, authority.ReadStateForHost());
            // Resolve actual accepted choices through ordinary costs; never erase the player's queued Wait.
            var automatic = new LocalEncounterController().SelectChoices(state, commands.NextId);
            if (automatic.Count != 0)
                state = authority.Commit(commands.Batch(state, WorldBatchKind.EncounterDecision, automatic)).Candidate;
            for (int resolution = 0; state.Pacing!.Encounter!.Decisions.Any(row => row.ActorId == actor); resolution++)
            {
                Assert.True(resolution < 8);
                Assert.False(WorldEncounterActions.HasRequiredDecisions(state));
                var boundary = commands.Batch(state, WorldBatchKind.EncounterResolution);
                var prepared = new WorldStepProcessor().Decide(state, boundary, authority.IsIdentityOccupiedForHost);
                state = authority.Commit(boundary).Candidate;
                Assert.Equal(prepared.Candidate.ContentHash, state.ContentHash);
            }
            Assert.True(state.Pacing!.Encounter!.Actors.Single(row => row.ActorId == actor).RemainingDelay > 0);
            Assert.True(WorldControllerBoundaries.CanRestoreHuman(state, actor));
            var paid = state;
            final = authority.Commit(Event(state, Reconnect(state, actor), 103)).Candidate;
            AssertGameplayUnchanged(paid, final);
            Assert.Equal(WorldControllerMode.Human, final.Controllers!.Get(actor).Mode);
            Assert.NotNull(authority.FindReceipt(wait, ConstructionUpgradeFixture.Delivery(final, actor)));
        }
        using var recovered = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
        Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(recovered.ReadStateForHost()));
    }

    [Fact]
    public void Actual_archive_rollover_retains_disconnected_actor_and_its_exact_episode_on_reopen()
    {
        using var files = new StorageFiles();
        WorldSnapshot genesis = Fresh(), final;
        var commands = new ConstructionUpgradeFixture();
        var humans = genesis.Controllers!.Assignments.Select(row => row.ActorId).ToArray();
        EntityId sender = humans[0];
        EntityId recipient = humans.First(actor => actor != sender
            && genesis.Space.QueryInteraction(sender, actor) == SpatialInteraction.Reachable);
        EntityId absent = humans.First(actor => actor != sender && actor != recipient);
        var lot = genesis.Inventory.Custody.Lots.First(row => row.CustodianId == genesis.GetResident(sender).InventoryCustodianId);
        var terms = new ConsentedMaterialTerms(genesis.WorldId, genesis.BranchId, sender, recipient,
            ConsentedMaterialLotKind.CountedSource, lot.LotId, lot.Definition, lot.CustodianId,
            genesis.GetResident(recipient).InventoryCustodianId, 1, lot.Revision);
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            WorldSnapshot state = authority.Commit(Event(genesis, Disconnect(genesis, absent), 200)).Candidate;
            var expected = state.Controllers!;
            WorldMaterialCareArchiveSegment? segment = null;
            void Submit(WorldActionIntent intent)
            {
                var delivery = ConstructionUpgradeFixture.Delivery(state, sender);
                var command = new WorldActionCommand(commands.NextIdentity(), delivery.PrincipalId, delivery.SessionId,
                    sender, state.WorldId, state.BranchId, state.Space.GetActor(sender).Revision, intent);
                var result = authority.Commit(commands.Batch(state, [command], []));
                Assert.Equal(WorldActionRejection.None, Assert.Single(result.Receipts).Rejection);
                if (result.SealedMaterialCareSegment is { } actual) segment = actual;
                state = result.Candidate;
                Assert.Equal(expected.ContentHash, state.Controllers!.ContentHash);
            }
            for (int offer = 0; segment is null; offer++)
            {
                Assert.True(offer < WorldMaterialProposalBook.MaximumOutcomes);
                Submit(new ProposeMaterialIntent(terms, state.AdvancingSteps + 32));
                Submit(new WithdrawMaterialProposalIntent(Assert.Single(state.Coordination!.Proposals!.Open).ProposalId));
            }
            Assert.Equal(1UL, authority.InspectForHost().Journal.MaterialCareArchiveCount);
            Assert.Equal(expected.ContentHash, segment.Continued.Controllers!.ContentHash);
            final = state;
        }
        using var recovered = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
        Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(recovered.ReadStateForHost()));
        Assert.Equal(WorldControllerMode.ReconnectAllowance, recovered.ReadStateForHost().Controllers!.Get(absent).Mode);
        var resumed = recovered.Commit(Event(final, Expire(final, absent), 201)).Candidate;
        Assert.Equal(WorldControllerMode.Autonomous, resumed.Controllers!.Get(absent).Mode);
        AssertGameplayUnchanged(final, resumed);
        Assert.True(recovered.IsIdentityOccupiedForHost(Id(200)));
        var returned = recovered.Commit(Event(resumed, Reconnect(resumed, absent), 202)).Candidate;
        Assert.Throws<AuthorityDispatchException>(() => recovered.Commit(Event(returned, Disconnect(returned, absent), 200)));
        var nextEpisode = recovered.Commit(Event(returned, Disconnect(returned, absent), 203)).Candidate;
        Assert.Throws<AuthorityDispatchException>(() => recovered.Commit(Event(nextEpisode,
            new WorldControllerEvent(absent, WorldControllerEventKind.AllowanceExpired,
                nextEpisode.Controllers!.Get(absent).Revision, Id(200), 30_000), 204)));
        Assert.Same(nextEpisode, recovered.ReadStateForHost());
        Assert.Equal(final.Controllers!.ContentHash, recovered.ReadMaterialCareArchiveForHost(0).Segment.Continued.Controllers!.ContentHash);
    }

    private static WorldSnapshot EnterFirstEncounter(SqliteWorldAuthority authority, WorldSnapshot state,
        EncounterJourneyWorldFixture commands)
    {
        EntityId actor = Player(state);
        var scenario = state.EncounterLifecycle!.Scenarios.Single(row => row.Key == EncounterJourneyAdmission.FirstScenarioKey);
        var walking = new ConstructionUpgradeFixture();
        for (int step = 0; state.Space.QueryInteraction(actor, scenario.TargetActorId) != SpatialInteraction.Reachable; step++)
        {
            Assert.True(step < 512, "The first encounter must be physically reached through ordinary movement.");
            var here = state.Space.GetActor(actor).Feet;
            var there = state.Space.GetActor(scenario.TargetActorId).Feet;
            long dx = Math.Sign(there.X - here.X) * WorldMovementInput.DisplacementPerStep;
            long dy = dx == 0 ? Math.Sign(there.Y - here.Y) * WorldMovementInput.DisplacementPerStep : 0;
            state = authority.Commit(walking.Batch(state, [],
                [new WorldMovementInput(ConstructionUpgradeFixture.Delivery(state, actor), actor, dx, dy)])).Candidate;
        }
        var entry = commands.Enter(state, scenario.DefinitionId);
        var entering = authority.Commit(commands.Batch(state, WorldBatchKind.EncounterLifecycle, [entry]));
        Assert.Equal(WorldActionRejection.None, Assert.Single(entering.Receipts).Rejection);
        state = entering.Candidate;
        for (int transition = 0; state.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Active; transition++)
        {
            Assert.True(transition < 3);
            state = authority.Commit(commands.Batch(state, WorldBatchKind.EncounterLifecycle)).Candidate;
        }
        return state;
    }
}
