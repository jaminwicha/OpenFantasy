using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.Client;

public sealed class FarmTargetSelectionTests
{
    private static readonly EntityId Player = Id(10);
    private static readonly EntityId Wren = Id(11);
    private static readonly EntityId OtherOak = Id(800);
    private static readonly WorldStepProcessor Processor = new();

    private static LocalWorldView View(WorldSnapshot state) => new(state, Player);
    private static FarmActionOffers Offers(WorldSnapshot state) =>
        new(Delivery, Wren, state.Inventory.Sources.Single(source => source.SourceId == Id(71)).Definition.DefinitionId, null);

    private static WorldSnapshot TwoPlants()
    {
        WorldSnapshot state = GatheringWorldFixture.Create();
        var wood = state.Inventory.Sources.Single(source => source.SourceId == Id(71)).Definition;
        var inventory = new CountedAuthorityState(state.Inventory.Custody, 47, state.Inventory.Actors,
            state.Inventory.Controls, state.Inventory.Policies, [.. state.Inventory.Sources, new(Id(801), wood, 20)]);
        var node = new GatherNodeState(OtherOak, new GatherNodeDefinition(Id(802), GatherNodeKind.Oak, wood),
            Id(801), 20, new SubunitPosition(0, 384, 256), 0, 0, 5);
        var gathering = new GatheringSnapshot(state.WorldId, state.BranchId, 0,
            [.. state.Gathering!.Nodes, node], state.Gathering.Tools, state.Gathering.Actors);
        return new WorldSnapshot(inventory, state.Space, state.Residents, state.AdmissionVersion,
            state.Calendar, state.Revision, state.AdvancingSteps, gathering: gathering);
    }

    private static WorldStepDecision Apply(WorldSnapshot state, WorldActionCommand? action = null,
        WorldMovementInput? movement = null) => Processor.Decide(state, new WorldStepBatch(Id(80_000 + state.Revision),
            state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps, WorldBatchKind.AdvancingStep,
            movement is null ? [] : [movement], action is null ? [] : [action], state.ProfileVersion));

    [Fact]
    public void CyclingIsReadOnlyAndStableWithDuplicateNames()
    {
        WorldSnapshot state = TwoPlants();
        byte[] original = WorldSnapshotCodec.Encode(state);
        var targets = new FarmTargetSelection(Wren);
        targets.Refresh(View(state));
        targets.Cycle();
        Assert.Equal(Wren, targets.Selected!.Id);
        targets.Cycle();
        Assert.Equal(GatheringWorldFixture.NodeId, targets.Selected!.Id);
        targets.Cycle();
        Assert.Equal(OtherOak, targets.Selected!.Id);
        targets.Refresh(View(state));
        Assert.Equal(OtherOak, targets.Selected!.Id);
        targets.Cycle();
        Assert.Equal(Wren, targets.Selected!.Id);
        targets.Clear();
        Assert.Null(targets.Selected);
        Assert.Equal(original, WorldSnapshotCodec.Encode(state));
    }

    [Fact]
    public void FartherSelectedPlantOwnsTheOfferAndAnAlreadyCapturedChoiceCannotBeRetargeted()
    {
        WorldSnapshot state = TwoPlants();
        var targets = new FarmTargetSelection(Wren);
        targets.Refresh(View(state));
        targets.Cycle();
        targets.Cycle();
        targets.Cycle();
        Assert.Equal(GatheringWorldFixture.NodeId, FarmClientSession.NearestGatherNode(View(state))!.NodeId);
        FarmActionOffer captured = Offers(state).Create(View(state), targets.Selected)[FarmSemanticAction.Gather];
        targets.Cycle();
        Assert.Equal(Wren, targets.Selected!.Id);
        WorldStepDecision decision = Apply(state, captured.ToCommand(Id(90_001)));
        Assert.Equal(CommandReceiptStatus.Applied, decision.Receipts.Single().Status);
        Assert.Equal(4, decision.Candidate.Gathering!.Nodes.Single(node => node.NodeId == OtherOak).RemainingSwings);
        Assert.Equal(5, decision.Candidate.Gathering.Nodes.Single(node => node.NodeId == GatheringWorldFixture.NodeId).RemainingSwings);
    }

    [Fact]
    public void SelectionPersistsAfterMovementMakesAnotherPlantCloser()
    {
        WorldSnapshot state = TwoPlants();
        var targets = new FarmTargetSelection(Wren);
        targets.Refresh(View(state));
        targets.Cycle();
        targets.Cycle();
        for (int step = 0; step < 7; step++)
        {
            state = Apply(state, movement: new WorldMovementInput(Delivery, Player, 0, 32)).Candidate;
        }
        targets.Refresh(View(state));
        Assert.Equal(OtherOak, FarmClientSession.NearestGatherNode(View(state))!.NodeId);
        Assert.Equal(GatheringWorldFixture.NodeId, targets.Selected!.Id);
        Assert.Equal(GatheringWorldFixture.NodeId,
            Offers(state).Create(View(state), targets.Selected)[FarmSemanticAction.Gather].TargetId);
    }

    [Fact]
    public void DepletionAndDisappearanceBlockWithoutSubstitutionAndRegrowthRestoresTheSameIdentity()
    {
        WorldSnapshot state = TwoPlants();
        var targets = new FarmTargetSelection(Wren);
        targets.Refresh(View(state));
        targets.Cycle();
        targets.Cycle();
        for (ulong swing = 0; swing < 5; swing++)
        {
            for (int rest = 0; state.Gathering!.GetActorWork(Player).Effort < GatherRulesV1.EffortPerSwing && rest < 50; rest++)
            {
                state = Apply(state).Candidate;
            }
            var node = state.Gathering.Nodes.Single(node => node.NodeId == GatheringWorldFixture.NodeId);
            var command = new WorldActionCommand(Id(90_100 + swing), Delivery.PrincipalId, Delivery.SessionId,
                Player, state.WorldId, state.BranchId, state.Space.GetActor(Player).Revision,
                new GatherIntent(node.NodeId, node.Revision, GatheringWorldFixture.ToolLotId,
                    state.Inventory.Custody.GetLot(GatheringWorldFixture.ToolLotId).Revision));
            WorldStepDecision harvest = Apply(state, command);
            Assert.Equal(CommandReceiptStatus.Applied, harvest.Receipts.Single().Status);
            state = harvest.Candidate;
        }
        targets.Refresh(View(state));
        FarmActionOffer depleted = Offers(state).Create(View(state), targets.Selected)[FarmSemanticAction.Gather];
        Assert.False(depleted.CanExecute);
        Assert.Equal(GatheringWorldFixture.NodeId, depleted.TargetId);
        ulong absentAt = state.Gathering!.Nodes.Single(node => node.NodeId == GatheringWorldFixture.NodeId).DepletedAtStep!.Value
            + GatherRulesV1.StumpSteps;
        for (int wait = 0; state.AdvancingSteps < absentAt && wait < 100; wait++)
        {
            state = Apply(state).Candidate;
        }
        Assert.True(state.AdvancingSteps >= absentAt);
        targets.Refresh(View(state));
        Assert.Null(targets.Selected!.Feet);
        Assert.Equal("Oak", targets.Selected.Name);
        FarmActionOffer absent = Offers(state).Create(View(state), targets.Selected)[FarmSemanticAction.Gather];
        Assert.False(absent.CanExecute);
        Assert.Null(absent.Intent);
        Assert.Equal(GatheringWorldFixture.NodeId, absent.TargetId);
        Assert.Equal(OtherOak, FarmClientSession.NearestGatherNode(View(state))!.NodeId);
        ulong inventory = View(state).PlayerInventory.Quantity;
        for (int wait = 0; state.Gathering!.Nodes.Single(node => node.NodeId == GatheringWorldFixture.NodeId).RemainingSwings == 0
            && wait < 1000; wait++)
        {
            state = Apply(state).Candidate;
        }
        targets.Refresh(View(state));
        Assert.Equal(GatheringWorldFixture.NodeId, targets.Selected.Id);
        Assert.NotNull(targets.Selected.Feet);
        Assert.True(Offers(state).Create(View(state), targets.Selected)[FarmSemanticAction.Gather].CanExecute);
        Assert.Equal(inventory, View(state).PlayerInventory.Quantity);
    }

    [Fact]
    public void IncompatibleTargetsDoNotChangeTheVerbAndChangingControlledActorClearsSelection()
    {
        WorldSnapshot state = TwoPlants();
        var targets = new FarmTargetSelection(Wren);
        targets.Refresh(View(state));
        targets.Cycle();
        Assert.False(Offers(state).Create(View(state), targets.Selected)[FarmSemanticAction.Gather].CanExecute);
        targets.Cycle();
        var offers = Offers(state).Create(View(state), targets.Selected);
        Assert.False(offers[FarmSemanticAction.Speak].CanExecute);
        Assert.False(offers[FarmSemanticAction.Give].CanExecute);
        Assert.Throws<InvalidOperationException>(() => offers[FarmSemanticAction.Speak].ToCommand(Id(91_001)));
        targets.Refresh(new LocalWorldView(state, Wren));
        Assert.Null(targets.Selected);
        targets.Refresh(View(state));
        Assert.Null(targets.Selected);
        Assert.True(Offers(state).Create(View(state))[FarmSemanticAction.Speak].CanExecute);
    }

    [Fact]
    public void TargetInputsRespectContextAndHeldPressOwnership()
    {
        var world = new FarmInputSituation(true, false, false);
        var router = new FarmInputRouter();
        Assert.Equal(FarmSemanticAction.NextTarget, router.Route(world, FarmInputSignal.Press(FarmInputActions.NextTarget)).Action);
        Assert.Equal(FarmSemanticAction.None, router.Route(world, FarmInputSignal.Repeat(FarmInputActions.NextTarget)).Action);
        router.TogglePause();
        router.Route(world, FarmInputSignal.Press(FarmInputActions.ClearTarget));
        router.TogglePause();
        Assert.Equal(FarmSemanticAction.None, router.Route(world, FarmInputSignal.Press(FarmInputActions.ClearTarget)).Action);
        router.Route(world, FarmInputSignal.Release(FarmInputActions.ClearTarget));
        Assert.Equal(FarmSemanticAction.ClearTarget, router.Route(world, FarmInputSignal.Press(FarmInputActions.ClearTarget)).Action);
        foreach (FarmSemanticAction action in new[] { FarmSemanticAction.NextTarget, FarmSemanticAction.ClearTarget })
        {
            Assert.False(router.Permits(world with { ReadingHold = true }, action));
            Assert.False(router.Permits(world with { ActionPending = true }, action));
            Assert.False(router.Permits(world with { SessionOpen = false }, action));
        }
        router.SetFocused(false);
        Assert.False(router.Permits(world, FarmSemanticAction.NextTarget));
        router.MarkUnavailable();
        Assert.False(router.Permits(world, FarmSemanticAction.ClearTarget));
    }
}
