using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;
using static OpenFantasy.Hosting.Tests.Simulation.EncounterOutcome.TwoHumanOutcomeWorld;
using static OpenFantasy.Hosting.Tests.Worlds.EncounterOutcome.EncounterOutcomeJourneyTests;

namespace OpenFantasy.Hosting.Tests.Simulation.SharedPlayers;

public sealed class SharedCareSchedulingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_queued_patients_care_precedes_observer_traffic_and_accepted_treatment_cannot_be_starved(bool reopen)
    {
        using var files = new SessionFiles();
        WorldSnapshot source = RoadThreatWorldAdmission.CreateEmbedded("shared-care-priority", "Traveler");
        EntityId player = source.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        EntityId bystander = source.EncounterLifecycle!.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.SecondScenarioKey).TargetActorId;
        WorldSnapshot genesis = SharedOutcomeCalibration.WithObserverCare(WorldEncounterOutcomeAdmission.CreateCalibratedFresh(source,
            new EncounterOutcomeCalibration(new Dictionary<EntityId, int> { [player] = 1 }, [bystander])));
        var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        var time = new ManualSessionTimeProvider();
        var host = new LocalWorldSession(authority, player, DeliveryOf(genesis, player), time);
        LocalPlayerConnection observer = await host.ConnectPlayerAsync(bystander, DeliveryOf(genesis, bystander));
        try
        {
            host.SetReady(true);
            observer.SetReady(true);
            var scenario = genesis.EncounterLifecycle!.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.FirstScenarioKey);
            var entry = host.ReadView().EncounterLifecycle!.Entries.Single(value => value.ScenarioId == scenario.DefinitionId);
            Task<WorldActionReceipt> entered = host.SubmitAction(Command(player, new EnterEncounterIntent(scenario.DefinitionId,
                entry.EncounterId, genesis.EncounterLifecycle.EncounterCount, entry.TargetRevision)));
            await Tick(time);
            Assert.Equal(WorldActionRejection.None, (await entered).Rejection);
            for (int cutoff = 0; authority.ReadStateForHost().EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome; cutoff++)
            {
                Assert.True(cutoff < 2_000);
                WorldSnapshot state = authority.ReadStateForHost();
                Task<WorldActionReceipt>? waiting = null;
                if (state.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Active && AutomaticChoicesSettled(state)
                    && WorldEncounterActions.DueActors(state).Any(actor => actor.ActorId == player)
                    && state.Pacing!.Encounter!.Decisions.All(value => value.ActorId != player))
                    waiting = host.SubmitAction(Command(player, Wait(state)));
                await Tick(time);
                if (waiting is not null) Assert.Equal(WorldActionRejection.None, (await waiting).Rejection);
            }
            Assert.Equal(0, authority.ReadStateForHost().Progression!.GetActor(player).CurrentHp);
            Task<WorldActionReceipt> reading = host.SubmitAction(Command(player, host.ReadView().EncounterOutcome!.Acknowledgment!));
            await Tick(time);
            Assert.Equal(WorldActionRejection.None, (await reading).Rejection);
            WorldSnapshot read = authority.ReadStateForHost();
            Assert.True(WorldEncounterOutcomes.IsReadyToFinalize(read));
            var choice = Assert.Single(host.ReadView().Care!.Offers, value => value.CanAcknowledge).Intent;
            CareAgreement otherAgreement = genesis.Care!.Agreements.Single(value => value.PatientActorId == bystander);
            Task<WorldActionReceipt> unrelated = observer.SubmitAction(Command(bystander,
                new AcknowledgeCareIntent(otherAgreement.AgreementId, read.EncounterActions!.Scope.EncounterId,
                    read.EncounterLifecycle!.Revision, read.EncounterActions.ContentHash)));
            Task<WorldActionReceipt> accepted = host.SubmitAction(Command(player, choice));
            await Tick(time);
            await Until(() => accepted.IsCompleted);
            Assert.Equal(WorldActionRejection.None, (await accepted).Rejection);
            Assert.False(unrelated.IsCompleted);
            WorldSnapshot pending = authority.ReadStateForHost();
            Assert.NotNull(pending.Care!.Pending);
            Assert.Equal(pending.Revision, pending.Care.Pending.WorldSequence);
            Assert.Equal(0UL, authority.ReadStateForHost().EncounterOutcome!.FinalizedCount);
            Task<WorldActionReceipt> stale = host.SubmitAction(Command(player, choice));
            await Tick(time);
            Assert.Equal(WorldActionRejection.Unavailable, (await stale).Rejection);
            WorldSnapshot refused = authority.ReadStateForHost();
            Assert.Equal(pending.Revision + 1, refused.Revision);
            Assert.Equal(pending.Care.ContentHash, refused.Care!.ContentHash);
            Assert.Equal(pending.Inventory.ContentHash, refused.Inventory.ContentHash);
            Assert.Equal(pending.Progression!.ContentHash, refused.Progression!.ContentHash);
            Assert.False(unrelated.IsCompleted);
            if (reopen)
            {
                await observer.DisposeAsync();
                await host.DisposeAsync();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => unrelated);
                authority = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(genesis));
                Assert.Equal(refused.ContentHash, authority.ReadStateForHost().ContentHash);
                time = new ManualSessionTimeProvider();
                host = new LocalWorldSession(authority, player, DeliveryOf(genesis, player), time);
                observer = await host.ConnectPlayerAsync(bystander, DeliveryOf(genesis, bystander));
                host.SetReady(true);
                observer.SetReady(true);
                unrelated = observer.SubmitAction(Command(bystander,
                    new AcknowledgeCareIntent(otherAgreement.AgreementId, read.EncounterActions.Scope.EncounterId,
                        read.EncounterLifecycle.Revision, read.EncounterActions.ContentHash)));
            }
            Task<WorldActionReceipt> repeated = host.SubmitAction(Command(player, choice));
            await Tick(time);
            WorldSnapshot treated = authority.ReadStateForHost();
            Assert.Null(treated.Care!.Pending);
            Assert.Single(treated.Care.Treatments);
            Assert.True(treated.Progression!.GetActor(player).CurrentHp > 0);
            Assert.Equal(EncounterLifecyclePhase.Active, treated.EncounterLifecycle!.Phase);
            Assert.Equal(read.EncounterActions.Scope.EncounterId, treated.EncounterActions!.Scope.EncounterId);
            Assert.Equal(read.Calendar, treated.Calendar);
            Assert.False(unrelated.IsCompleted);
            Assert.False(repeated.IsCompleted);
            await Tick(time);
            bool observerReceipt = await WasRejected(unrelated);
            bool patientReceipt = await WasRejected(repeated);
            Assert.True(observerReceipt || patientReceipt);
            Assert.Single(authority.ReadStateForHost().Care!.Treatments);
            Assert.True((await host.VerifyRecordedHistoryAsync()).ReproducesHead);
        }
        finally
        {
            await observer.DisposeAsync();
            await host.DisposeAsync();
        }

        WorldActionCommand Command(EntityId actor, WorldActionIntent intent)
        {
            WorldSnapshot state = authority.ReadStateForHost();
            var delivery = DeliveryOf(state, actor);
            return new WorldActionCommand(Retreat.RetreatJourneyWorld.NewId(), delivery.PrincipalId, delivery.SessionId,
                actor, state.WorldId, state.BranchId, state.Space.GetActor(actor).Revision, intent);
        }
    }

    private static async Task<bool> WasRejected(Task<WorldActionReceipt> action)
    {
        try
        {
            Assert.NotEqual(WorldActionRejection.None, (await action).Rejection);
            return true;
        }
        catch (AuthorityDispatchException)
        {
            return false;
        }
    }
}
