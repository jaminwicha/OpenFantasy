using OpenFantasy.Client;

namespace OpenFantasy.Hosting.Tests.Client;

public sealed class FarmControlAcceptTests
{
    private static readonly FarmInputSituation World = new(true, false, false);
    private static FarmInputSignal Accept => FarmInputSignal.Press(FarmInputActions.Interact)
        with { FocusedControlAccept = true };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Panel_control_receives_release_without_dispatching_gameplay(bool wheel)
    {
        var router = new FarmInputRouter();
        if (wheel) router.SetWheelOpen(true); else router.SetInventoryOpen(true);
        Assert.Equal(new FarmInputDecision(true, FarmSemanticAction.None), router.Route(World, Accept));
        Assert.Equal(new FarmInputDecision(false, FarmSemanticAction.None),
            router.Route(World, FarmInputSignal.Release(FarmInputActions.Interact)));
        router.SetInventoryOpen(false);
        router.SetWheelOpen(false);
        Assert.Equal(FarmSemanticAction.Speak, router.Route(World, Accept).Action);
    }

    [Fact]
    public void World_dialogue_and_explicit_cancel_retain_router_ownership()
    {
        var router = new FarmInputRouter();
        Assert.Equal(FarmSemanticAction.Speak, router.Route(World, Accept).Action);
        Assert.True(router.Route(World, FarmInputSignal.Release(FarmInputActions.Interact)).Consumed);
        Assert.Equal(FarmSemanticAction.CloseDialogue, router.Route(World with { ReadingHold = true }, Accept).Action);
        Assert.True(router.Route(World, FarmInputSignal.Release(FarmInputActions.Interact)).Consumed);
        router.SetInventoryOpen(true);
        Assert.Equal(FarmSemanticAction.CloseInventory, router.Route(World,
            FarmInputSignal.Press(FarmInputActions.Cancel, FarmInputActions.Craft)
                with { FocusedControlAccept = true }).Action);
        Assert.True(router.Route(World, FarmInputSignal.Release(FarmInputActions.Cancel, FarmInputActions.Craft)).Consumed);
    }

    [Fact]
    public void Missing_panel_release_cannot_turn_the_next_world_press_into_an_action()
    {
        var router = new FarmInputRouter();
        router.SetInventoryOpen(true);
        router.Route(World, Accept);
        router.SetInventoryOpen(false);
        Assert.Equal(new FarmInputDecision(true, FarmSemanticAction.None), router.Route(World, Accept));
        Assert.True(router.Route(World, FarmInputSignal.Release(FarmInputActions.Interact)).Consumed);
        Assert.Equal(FarmSemanticAction.Speak, router.Route(World, Accept).Action);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Focus_return_keeps_held_input_owned_even_if_another_accept_press_is_delivered(bool duplicate)
    {
        var router = new FarmInputRouter();
        router.SetInventoryOpen(true);
        router.Route(World, Accept);
        router.SetFocused(false);
        router.ReconcileHeldActions([FarmInputActions.Interact]);
        router.SetFocused(true);
        if (duplicate) Assert.Equal(FarmSemanticAction.None, router.Route(World, Accept).Action);
        Assert.True(router.Route(World, FarmInputSignal.Release(FarmInputActions.Interact)).Consumed);
        Assert.Equal(FarmSemanticAction.None, router.Route(World, Accept).Action);
        Assert.False(router.Route(World, FarmInputSignal.Release(FarmInputActions.Interact)).Consumed);
    }
}
