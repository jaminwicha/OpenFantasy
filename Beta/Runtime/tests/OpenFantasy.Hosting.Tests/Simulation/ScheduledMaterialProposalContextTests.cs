using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class ScheduledMaterialProposalContextTests
{
    [Theory]
    [InlineData(EncounterLifecyclePhase.Confrontation, false)]
    [InlineData(EncounterLifecyclePhase.Active, false)]
    [InlineData(EncounterLifecyclePhase.Outcome, false)]
    [InlineData(EncounterLifecyclePhase.Outcome, true)]
    public async Task Encounter_and_pending_care_refuse_proposals_before_selection_without_mutation_or_loop_failure(
        EncounterLifecyclePhase phase, bool acknowledgeCare)
    {
        using var live = new MaterialProposalTestSession(new OrdinaryCareWorldFixture().Genesis);
        var journey = new CareWorldSession(live.Authority);
        if (phase == EncounterLifecyclePhase.Outcome)
        {
            live.Actions.PrepareDefeat(journey);
            if (acknowledgeCare)
            {
                CareRewardJourney.Commit(journey, live.Actions.Batch(live.State, WorldBatchKind.Care,
                    [live.Actions.Acknowledge(live.State)]));
            }
        }
        else
        {
            live.Actions.Approach(journey);
            WorldSnapshot state = live.State;
            var scenario = live.Actions.First;
            var enter = live.Actions.Command(state, live.Actions.Player, new EnterEncounterIntent(scenario.DefinitionId,
                EncounterEpisodeIdentity.Derive(state.EncounterLifecycle!, scenario.DefinitionId),
                state.EncounterLifecycle!.EncounterCount, state.Space.GetActor(scenario.TargetActorId).Revision));
            CareRewardJourney.Commit(journey, live.Actions.Batch(state, WorldBatchKind.EncounterLifecycle, [enter]));
            if (phase == EncounterLifecyclePhase.Active)
            {
                for (int stage = 0; stage < 2; stage++)
                {
                    CareRewardJourney.Commit(journey, live.Actions.Batch(live.State, WorldBatchKind.EncounterLifecycle));
                }
            }
        }
        WorldSnapshot before = live.State;
        Assert.Equal(phase, before.EncounterLifecycle!.Phase);
        Assert.Equal(acknowledgeCare, before.Care!.Pending is not null);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(live.Authority, live.Actions.Player, live.Delivery(live.Actions.Player), time);
        session.SetReady(true);
        foreach (WorldActionIntent intent in new WorldActionIntent[]
        {
            new ProposeMaterialIntent(live.Terms(live.Actions.Player, live.Actions.Helper), before.AdvancingSteps + 64),
            new DeclineMaterialProposalIntent(live.Actions.Helper),
            new WithdrawMaterialProposalIntent(live.Actions.Helper)
        })
        {
            var command = live.Actions.Command(before, live.Actions.Player, intent);
            var refusal = await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitAction(command));
            Assert.Equal("Saved material proposals require their admitted unheld exploration boundary.", refusal.Message);
            Assert.Null(live.Service.FindReceipt(command, live.Delivery(live.Actions.Player)));
        }
        Assert.Equal(before.ContentHash, live.State.ContentHash);
        Assert.False(session.Completion.IsCompleted);
        await Tick(time);
        Assert.False(session.Completion.IsCompleted);
        Assert.Equal(before.Calendar, live.State.Calendar);
        Assert.Empty(live.State.Coordination!.Proposals!.Open);
    }
}
