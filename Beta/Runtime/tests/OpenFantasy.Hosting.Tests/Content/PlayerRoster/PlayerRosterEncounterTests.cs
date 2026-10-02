using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.Content.PlayerRoster;

public sealed class PlayerRosterEncounterTests
{
    [Fact]
    public void New_human_enters_real_shared_encounter_and_all_four_submit_their_own_required_choices()
    {
        WorldSnapshot state = PlayerRosterAdmissionTests.Four;
        var scenario = state.EncounterLifecycle!.Scenarios.Single(value =>
            value.Key == EncounterJourneyAdmission.FirstScenarioKey + "_player_2");
        EntityId actor = scenario.InitiatingActorId;
        ulong identity = 1;
        EntityId NextId() => new(0x524f53544552454e, identity++);
        WorldActionCommand Command(EntityId owner, WorldActionIntent intent)
        {
            var delivery = PlayerRosterAdmissionTests.Delivery(state, owner);
            return new(NextId(), delivery.PrincipalId, delivery.SessionId, owner, state.WorldId,
                state.BranchId, state.Space.GetActor(owner).Revision, intent);
        }
        WorldStepDecision Apply(WorldBatchKind kind, WorldActionCommand[]? actions = null,
            WorldMovementInput[]? movement = null)
        {
            var batch = new WorldStepBatch(NextId(), state.WorldId, state.BranchId, state.Revision,
                state.AdvancingSteps, kind, movement ?? [], actions ?? [], state.ProfileVersion);
            WorldStepDecision result = new WorldStepProcessor().Decide(state, batch);
            state = result.Candidate;
            return result;
        }
        void Walk(EntityId mover, SubunitPosition destination, Func<bool> arrived)
        {
            for (int step = 0; !arrived(); step++)
            {
                Assert.True(step < 512, $"Player {mover} must reach the party through bounded ground movement.");
                var route = state.Space.QueryGroundRoute(mover, destination);
                Assert.True(route.Status == GroundRouteStatus.Reachable,
                    $"Player {mover} route to {destination} failed: {route.Status}.");
                Assert.NotNull(route.NextWaypoint);
                var feet = state.Space.GetActor(mover).Feet;
                var next = route.NextWaypoint!.Value;
                long dx = Math.Clamp(next.X - feet.X, -32, 32);
                long dy = dx != 0 ? 0 : Math.Clamp(next.Y - feet.Y, -32, 32);
                Apply(WorldBatchKind.AdvancingStep, movement:
                    [new(PlayerRosterAdmissionTests.Delivery(state, mover), mover, dx, dy)]);
                Assert.Equal(feet.Offset(dx, dy, 0), state.Space.GetActor(mover).Feet);
            }
        }
        WorldActionReceipt Enter() => Assert.Single(Apply(WorldBatchKind.EncounterLifecycle,
            [Command(actor, new EnterEncounterIntent(scenario.DefinitionId,
                EncounterEpisodeIdentity.Derive(state.EncounterLifecycle!, scenario.DefinitionId),
                state.EncounterLifecycle!.EncounterCount, state.Space.GetActor(scenario.TargetActorId).Revision))]).Receipts);
        bool InReach(EntityId participant) => participant == actor || state.Space.QueryInteraction(actor, participant,
            scenario.EntryRangeSubunits) == SpatialInteraction.Reachable;
        EntityId[] humans = state.Residents.Where(value => value.ControllerKind == ResidentControllerKind.Player)
            .Select(value => value.ActorId).ToArray();

        // Walk the admitted body through actual advancing transactions; no position or range bypass.
        var target = state.Space.GetActor(scenario.TargetActorId).Feet;
        SubunitPosition approach = new[] { target.Offset(256, 0, 0), target.Offset(-256, 0, 0),
                target.Offset(0, 256, 0), target.Offset(0, -256, 0) }
            .Select(feet => (Feet: feet, Route: state.Space.QueryGroundRoute(actor, feet)))
            .Where(value => value.Route.Status == GroundRouteStatus.Reachable)
            .OrderBy(value => value.Route.Waypoints.Count).First().Feet;
        Walk(actor, approach, () => InReach(scenario.TargetActorId));

        // Target proximity alone cannot enroll distant party members. This is a real refused
        // entry transaction, followed by every distant human walking under their own control.
        EntityId[] distant = humans.Where(human => !InReach(human)).ToArray();
        Assert.NotEmpty(distant);
        WorldActionReceipt refused = Enter();
        Assert.True(refused.Status == CommandReceiptStatus.Rejected && refused.Rejection == WorldActionRejection.OutOfReach,
            $"A dispersed party must refuse entry: status={refused.Status}, rejection={refused.Rejection}.");
        Assert.Equal(EncounterLifecyclePhase.Exploration, state.EncounterLifecycle!.Phase);
        Assert.Null(state.Pacing!.Encounter);
        var center = state.Space.GetActor(actor).Feet;
        SubunitPosition[] assemblyPositions = (from x in new[] { -256L, 0L, 256L }
            from y in new[] { -256L, 0L, 256L }
            where x != 0 || y != 0
            select center.Offset(x, y, 0)).ToArray();
        while (humans.Any(human => !InReach(human)))
        {
            var routes = (from human in humans.Where(human => !InReach(human))
                from destination in assemblyPositions
                let route = state.Space.QueryGroundRoute(human, destination)
                where route.Status == GroundRouteStatus.Reachable
                select (Human: human, Destination: destination, Route: route))
                .OrderBy(value => value.Route.Waypoints.Count).ThenBy(value => value.Human)
                .ThenBy(value => value.Destination.X).ThenBy(value => value.Destination.Y).ToArray();
            Assert.True(routes.Length != 0, "Every remaining human needs an actual unoccupied ground route to the shared encounter.");
            var selected = routes[0];
            Walk(selected.Human, selected.Destination, () => state.Space.GetActor(selected.Human).Feet == selected.Destination);
            Assert.True(InReach(selected.Human), $"Player {selected.Human} reached {selected.Destination} but lacks encounter reach/sightline.");
        }
        Assert.All(scenario.Combatants.Where(value => value.Side != 0), participant => Assert.True(InReach(participant.ActorId)));
        WorldActionReceipt entered = Enter();
        Assert.True(entered.Status == CommandReceiptStatus.Applied,
            $"The physically assembled party must enter: status={entered.Status}, rejection={entered.Rejection}.");
        Apply(WorldBatchKind.EncounterLifecycle);
        Apply(WorldBatchKind.EncounterLifecycle);
        Assert.Equal(EncounterLifecyclePhase.Active, state.EncounterLifecycle!.Phase);
        Assert.Equal(4, humans.Length);
        Assert.All(state.Pacing!.Encounter!.Actors.Where(value => humans.Contains(value.ActorId)),
            value => Assert.Equal(EncounterActorControl.RequiredHuman, value.Control));
        Assert.Empty(state.Pacing.Encounter.Decisions);
        foreach (EntityId human in humans)
        {
            int previous = state.Pacing!.Encounter!.Decisions.Count;
            var chosen = Apply(WorldBatchKind.EncounterDecision,
                [Command(human, new QueueEncounterWaitIntent(state.Pacing.Encounter.EncounterId,
                    state.Pacing.Encounter.Revision))]);
            WorldActionReceipt receipt = Assert.Single(chosen.Receipts);
            Assert.True(receipt.Status == CommandReceiptStatus.Applied,
                $"Player {human}'s own choice failed: status={receipt.Status}, rejection={receipt.Rejection}.");
            Assert.Equal(previous + 1, state.Pacing!.Encounter!.Decisions.Count);
            Assert.Contains(state.Pacing.Encounter.Decisions, value => value.ActorId == human);
        }
        Assert.Equal(WorldSnapshotCodec.Encode(state), WorldSnapshotCodec.Encode(
            WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(state))));
    }
}
