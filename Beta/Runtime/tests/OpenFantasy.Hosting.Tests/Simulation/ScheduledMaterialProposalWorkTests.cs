using OpenFantasy.Core;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class ScheduledMaterialProposalWorkTests
{
    [Theory]
    [InlineData(WorldActionRejection.None)]
    [InlineData(WorldActionRejection.RevisionConflict)]
    [InlineData(WorldActionRejection.OutOfReach)]
    public async Task Only_a_valid_acceptance_reserves_the_builders_work(WorldActionRejection expected)
    {
        using var live = new MaterialProposalTestSession(new OrdinaryCareWorldFixture().Genesis);
        SubunitPosition playerStart = live.State.Space.GetActor(live.Actions.Player).Feet;
        var journey = new CareWorldSession(live.Authority);
        live.Actions.Approach(journey);
        WorldActionCommand reward = live.Actions.PrepareReward(journey);
        Commit(live, WorldBatchKind.HeldInteraction, reward);
        Commit(live, WorldBatchKind.HeldInteraction, live.Actions.Command(live.State, live.Actions.Player,
            new CloseDialogueIntent(live.State.Dialogues.Single().DialogueId)));
        var project = live.State.Construction!.Projects.Single();
        var controller = new LocalNpcController();
        for (int step = 0; live.State.Space.GetActor(live.Actions.Helper).Feet != project.WorkFeet; step++)
        {
            Assert.True(step < 256, "The funded builder must reach his actual worksite through bounded movement.");
            WorldMovementInput move = Assert.Single(controller.SelectMovement(live.State), value => value.ActorId == live.Actions.Helper);
            Move(live, move);
        }
        SubunitPosition destination = new[]
        {
            project.WorkFeet.Offset(-256, 0, 0), project.WorkFeet.Offset(256, 0, 0),
            project.WorkFeet.Offset(0, -256, 0), project.WorkFeet.Offset(0, 256, 0)
        }.First(feet => live.State.Space.QueryGroundRoute(live.Actions.Player, feet).Status == GroundRouteStatus.Reachable);
        MovePlayerTo(live, destination);
        EntityId storage = live.State.GetResident(live.Actions.Helper).InventoryCustodianId;
        var lot = live.State.Inventory.Custody.Lots.First(value => value.CustodianId == storage
            && value.Quantity > 1 && value.ReservedBy is null && value.Definition != project.Definition.RequiredTool.Item);
        WorldMaterialProposal offered = live.Save(live.Terms(live.Actions.Helper, live.Actions.Player, lot.LotId));
        if (expected == WorldActionRejection.OutOfReach)
        {
            MovePlayerTo(live, playerStart);
            Assert.NotEqual(SpatialInteraction.Reachable, live.State.Space.QueryInteraction(live.Actions.Player, live.Actions.Helper));
        }
        WorldSnapshot before = live.State;
        WorldActionCommand work = Assert.Single(controller.SelectActions(before,
            () => live.Actions.Batch(before, WorldBatchKind.AdvancingStep).CauseId));
        Assert.Equal(live.Actions.Helper, work.ActorId);
        WorldActionCommand accept = live.Acceptance(offered);
        Assert.Equal(before.Space.GetActor(live.Actions.Player).Revision, accept.ExpectedActorRevision);
        if (expected == WorldActionRejection.RevisionConflict)
        {
            accept = new(accept.CommandId, accept.PrincipalId, accept.OriginSessionId, accept.ActorId,
                accept.WorldId, accept.BranchId, accept.ExpectedActorRevision + 1, accept.Intent);
        }
        WorldStepDecision isolated = live.Authority.Preview(live.Actions.Batch(before, WorldBatchKind.AdvancingStep, [accept]));
        Assert.Equal(expected, Assert.Single(isolated.Receipts).Rejection);
        Assert.Equal(before.ContentHash, live.State.ContentHash);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(live.Authority, live.Actions.Player, live.Delivery(live.Actions.Player), time);
        session.SetReady(true);
        var result = await ScheduledMaterialProposalSessionTests.Submit(session, time, accept);
        // The real builder's selected action makes the sender unavailable before the reach check.
        var committedRejection = expected == WorldActionRejection.OutOfReach ? WorldActionRejection.Unavailable : expected;
        Assert.Equal(committedRejection, result.Rejection);
        WorldSnapshot after = live.State;
        Assert.Equal((before.Revision + 1, before.AdvancingSteps + 1, before.Calendar.AddMilliseconds(1800)),
            (after.Revision, after.AdvancingSteps, after.Calendar));
        var oldProgress = before.Construction!.Projects.Single().Progress.CompletedUnits;
        var newProgress = after.Construction!.Projects.Single().Progress.CompletedUnits;
        if (expected != WorldActionRejection.None)
        {
            Assert.True(newProgress > oldProgress);
            Assert.NotNull(live.Book.FindOpen(offered.ProposalId));
            Assert.Null(live.Book.FindOutcome(offered.ProposalId));
        }
        else
        {
            Assert.Equal(oldProgress, newProgress);
            Assert.Equal(WorldMaterialProposalOutcomeKind.Accepted, live.Book.FindOutcome(offered.ProposalId)!.Kind);
        }
        session.SetReady(false);
        await session.StopAsync();
        live.Reopen();
        Assert.Equal(WorldSnapshotCodec.Encode(after), WorldSnapshotCodec.Encode(live.State));
    }

    private static void MovePlayerTo(MaterialProposalTestSession live, SubunitPosition destination)
    {
        for (int step = 0; live.State.Space.GetActor(live.Actions.Player).Feet != destination; step++)
        {
            Assert.True(step < 256, "The player must follow the actual bounded ground route.");
            var route = live.State.Space.QueryGroundRoute(live.Actions.Player, destination);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            var next = route.NextWaypoint!.Value;
            var feet = live.State.Space.GetActor(live.Actions.Player).Feet;
            long x = Math.Clamp(next.X - feet.X, -32, 32);
            long y = x == 0 ? Math.Clamp(next.Y - feet.Y, -32, 32) : 0;
            Move(live, new(live.Delivery(live.Actions.Player), live.Actions.Player, x, y));
        }
    }

    private static void Commit(MaterialProposalTestSession live, WorldBatchKind kind, WorldActionCommand action)
    {
        Assert.Equal(WorldActionRejection.None, Assert.Single(live.Authority.Commit(
            live.Actions.Batch(live.State, kind, [action])).Receipts).Rejection);
    }

    private static void Move(MaterialProposalTestSession live, WorldMovementInput movement)
    {
        WorldSnapshot state = live.State;
        var batch = new WorldStepBatch(live.Actions.Batch(state, WorldBatchKind.AdvancingStep).CauseId,
            state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps, WorldBatchKind.AdvancingStep,
            [movement], [], state.ProfileVersion);
        var original = state.Space.GetActor(movement.ActorId).Feet;
        WorldSnapshot after = live.Authority.Commit(batch).Candidate;
        Assert.Equal(original.Offset(movement.DeltaX, movement.DeltaY, 0), after.Space.GetActor(movement.ActorId).Feet);
    }
}
