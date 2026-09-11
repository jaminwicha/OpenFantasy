using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.Client;

public sealed class FarmQuickWheelTests
{
    private static readonly FarmInputSituation World = new(true, false, false);
    private static readonly FarmDirectionalInput Right = new(0, 1, 0, 0);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WheelOwnsShortcutsCancelAndHeldMovementWithoutChangingPause(bool paused)
    {
        var router = new FarmInputRouter(paused);
        Assert.Equal(FarmSemanticAction.OpenWheel, router.Route(World, FarmInputSignal.Press(FarmInputActions.QuickWheel)).Action);
        router.SetWheelOpen(true);
        router.Route(World, FarmInputSignal.Release(FarmInputActions.QuickWheel));
        Assert.Equal(!paused, router.WorldShouldRun);
        Assert.Equal(FarmInputLayer.Wheel, router.Layer(World));
        Assert.True(router.Movement(World, Right).IsNeutral);
        Assert.Equal(!paused, router.Permits(World, FarmSemanticAction.ConfirmWheel));
        foreach (string action in new[] { FarmInputActions.Interact, FarmInputActions.Craft, FarmInputActions.Gather,
            FarmInputActions.Give, FarmInputActions.Inventory, FarmInputActions.NextTarget })
        {
            Assert.Equal(FarmSemanticAction.None, router.Route(World, FarmInputSignal.Press(action)).Action);
            router.Route(World, FarmInputSignal.Release(action));
        }
        Assert.Equal(FarmSemanticAction.CloseWheel, router.Route(World,
            FarmInputSignal.Press(FarmInputActions.Cancel, FarmInputActions.Craft)).Action);
        router.CaptureBoundary(World, Right);
        router.SetWheelOpen(false);
        Assert.Equal(paused, router.Paused);
        Assert.True(router.Route(World, FarmInputSignal.Release(FarmInputActions.Cancel, FarmInputActions.Craft)).Consumed);
        if (paused) router.TogglePause();
        Assert.True(router.Movement(World, Right).IsNeutral);
        router.Movement(World, FarmDirectionalInput.Neutral);
        Assert.False(router.Movement(World, Right).IsNeutral);
    }

    [Fact]
    public void PendingDialogueInventoryFocusAndUnavailableCannotOpenOrConfirmWheel()
    {
        var router = new FarmInputRouter();
        Assert.False(router.Permits(World with { ActionPending = true }, FarmSemanticAction.OpenWheel));
        Assert.False(router.Permits(World with { ReadingHold = true }, FarmSemanticAction.OpenWheel));
        router.SetInventoryOpen(true);
        Assert.False(router.Permits(World, FarmSemanticAction.OpenWheel));
        router.SetInventoryOpen(false);
        router.SetWheelOpen(true);
        Assert.False(router.Permits(World with { ActionPending = true }, FarmSemanticAction.ConfirmWheel));
        router.SetFocused(false);
        Assert.False(router.Permits(World, FarmSemanticAction.ConfirmWheel));
        router.SetFocused(true);
        router.MarkUnavailable();
        Assert.False(router.Permits(World, FarmSemanticAction.ConfirmWheel));
    }

    [Fact]
    public void SlotsOmitUnadmittedDomainsAndNeverSelectByOpeningOrRefreshing()
    {
        WorldSnapshot state = GatheringWorldFixture.Create();
        var offers = Offers(state);
        var wheel = new FarmQuickWheelModel(offers.Create(new LocalWorldView(state, Id(10))));
        Assert.Null(wheel.Selected);
        Assert.Null(wheel.Slots[2]); // No production domain in the gathering profile.
        wheel.Refresh(target => offers.Create(new LocalWorldView(state, Id(10)), target));
        Assert.Null(wheel.Selected);
        wheel.Select(2);
        Assert.Null(wheel.Selected);
        wheel.Select(1);
        Assert.Equal(FarmSemanticAction.Gather, wheel.Selected!.Action);
    }

    [Fact]
    public void SamePinnedPlantRefreshesAfterActualHarvestAndPreservesOldCapturedCommand()
    {
        WorldSnapshot state = GatheringWorldFixture.Create();
        var offers = Offers(state);
        var wheel = new FarmQuickWheelModel(offers.Create(new LocalWorldView(state, Id(10))));
        wheel.Select(1);
        FarmActionOffer old = wheel.Selected!.Offer;
        WorldStepDecision harvest = new WorldStepProcessor().Decide(state,
            GatheringWorldFixture.Batch(state, GatheringWorldFixture.Swing(state)));
        var view = new LocalWorldView(harvest.Candidate, Id(10));
        wheel.Refresh(target => offers.Create(view, target));
        Assert.Equal(old.TargetId, wheel.Selected.Offer.TargetId);
        Assert.True(wheel.Selected.Offer.CanExecute);
        Assert.Equal(1UL, Assert.IsType<OpenFantasy.Core.Gathering.GatherIntent>(wheel.Selected.Offer.Intent).ExpectedNodeRevision);
        Assert.True(old.CanExecute);
        WorldStepDecision stale = new WorldStepProcessor().Decide(harvest.Candidate,
            GatheringWorldFixture.Batch(harvest.Candidate, old.ToCommand(Id(900_001))));
        Assert.Equal(WorldActionRejection.RevisionConflict, stale.Receipts.Single().Rejection);
        Assert.Equal(view.PlayerInventory.Quantity, new LocalWorldView(stale.Candidate, Id(10)).PlayerInventory.Quantity);
    }

    [Fact]
    public void MissingChangedOrForeignOffersCannotRebindTheInspectedSlot()
    {
        WorldSnapshot state = GatheringWorldFixture.Create();
        var offers = Offers(state).Create(new LocalWorldView(state, Id(10)));
        foreach (int change in Enumerable.Range(0, 5))
        {
            var wheel = new FarmQuickWheelModel(offers);
            wheel.Select(1);
            FarmActionOffer original = wheel.Selected!.Offer;
            var altered = offers.ToDictionary();
            if (change == 0) altered.Remove(FarmSemanticAction.Gather);
            else altered[FarmSemanticAction.Gather] = new FarmActionOffer(original.Action,
                change == 1 ? original.Scope with { ActorId = Id(999) } : original.Scope,
                change == 2 ? "Different operation" : original.Label, original.TargetName,
                change == 3 ? Id(998) : original.TargetId, original.CostText, "", original.Intent, null,
                dialogueId: change == 4 ? Id(997) : null);
            wheel.Refresh(_ => altered);
            Assert.Equal(1, wheel.SelectedIndex);
            Assert.Equal(original.TargetId, wheel.Selected.Offer.TargetId);
            Assert.False(wheel.Selected.Offer.CanExecute);
            Assert.Null(wheel.Selected.Offer.Intent);
        }
    }

    private static FarmActionOffers Offers(WorldSnapshot state) => new(Delivery, Id(11),
        state.Inventory.Sources.Single(source => source.SourceId == Id(71)).Definition.DefinitionId, null);
}
