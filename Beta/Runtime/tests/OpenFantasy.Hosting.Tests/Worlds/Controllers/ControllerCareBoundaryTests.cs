using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.Controllers;

public sealed partial class ControllerJournalTests
{
    [Fact]
    public void An_acknowledged_finite_treatment_survives_disconnect_and_blocks_rejoin_until_the_helper_finishes()
    {
        using var files = new StorageFiles();
        WorldSnapshot basis = RoadThreatWorldAdmission.CreateEmbedded("controller-care", "Traveler");
        EntityId actor = basis.Residents.Single(row => row.ControllerKind == ResidentControllerKind.Player).ActorId;
        // Explicit fresh wound calibration, pinned in admission; the defeat and treatment still happen through real actions.
        WorldSnapshot genesis = WorldControllerAdmission.CreateFresh(WorldEncounterOutcomeAdmission.CreateCalibratedFresh(basis,
            new EncounterOutcomeCalibration(new Dictionary<EntityId, int> { [actor] = 1 }, [])));
        var commands = new EncounterJourneyWorldFixture();
        WorldSnapshot final;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            var state = EnterFirstEncounter(authority, genesis, commands);
            var npc = new LocalEncounterController();
            for (int step = 0; state.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome; step++)
            {
                Assert.True(step < 800, "A wounded player must reach actual defeat through ordinary paid combat.");
                if (WorldEncounterActions.HasRequiredDecisions(state))
                {
                    var choice = commands.Wait(state, actor);
                    var decision = authority.Commit(commands.Batch(state, WorldBatchKind.EncounterDecision, [choice]));
                    Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
                    state = decision.Candidate;
                }
                var automatic = npc.SelectChoices(state, commands.NextId);
                if (automatic.Count != 0)
                    state = authority.Commit(commands.Batch(state, WorldBatchKind.EncounterDecision, automatic)).Candidate;
                var kind = WorldEncounterActions.DueActors(state).Count == 0
                    ? WorldBatchKind.TacticalAdvance : WorldBatchKind.EncounterResolution;
                state = authority.Commit(commands.Batch(state, kind)).Candidate;
            }
            Assert.Equal(0, state.Progression!.GetActor(actor).CurrentHp);
            var agreement = state.Care!.Agreements.Single(row => row.PatientActorId == actor);
            var acknowledge = commands.Command(state, actor, new AcknowledgeCareIntent(agreement.AgreementId,
                state.EncounterActions!.Scope.EncounterId, state.EncounterLifecycle.Revision, state.EncounterActions.ContentHash));
            var accepted = authority.Commit(commands.Batch(state, WorldBatchKind.Care, [acknowledge]));
            Assert.Equal(WorldActionRejection.None, Assert.Single(accepted.Receipts).Rejection);
            var pending = accepted.Candidate;
            Assert.NotNull(pending.Care!.Pending);
            state = authority.Commit(Event(pending, Disconnect(pending, actor), 300)).Candidate;
            AssertGameplayUnchanged(pending, state);
            Assert.False(WorldControllerBoundaries.CanRestoreHuman(state, actor));
            Assert.Throws<AuthorityDispatchException>(() => authority.Commit(Event(state, Reconnect(state, actor), 301)));
            var treatment = Assert.IsType<WorldActionCommand>(new LocalCareController()
                .SelectTreatment(state, authority.IsIdentityOccupiedForHost));
            var treated = authority.Commit(commands.Batch(state, WorldBatchKind.Care, [treatment]));
            Assert.Equal(WorldActionRejection.None, Assert.Single(treated.Receipts).Rejection);
            state = treated.Candidate;
            Assert.Null(state.Care!.Pending);
            Assert.True(state.Progression!.GetActor(actor).CurrentHp > 0);
            Assert.Equal(EncounterLifecyclePhase.Active, state.EncounterLifecycle!.Phase);
            Assert.Equal(pending.EncounterActions!.Scope.EncounterId, state.EncounterActions!.Scope.EncounterId);
            Assert.Single(state.Production!.ConsumedItems);
            Assert.Equal(WorldControllerMode.ReconnectAllowance, state.Controllers!.Get(actor).Mode);
            Assert.True(WorldControllerBoundaries.CanRestoreHuman(state, actor));
            final = authority.Commit(Event(state, Reconnect(state, actor), 302)).Candidate;
            AssertGameplayUnchanged(state, final);
            Assert.NotNull(authority.FindReceipt(treatment, ConstructionUpgradeFixture.Delivery(final, agreement.HelperActorId)));
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
        Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
    }
}
