using OpenFantasy.Client;

namespace OpenFantasy.Hosting.Tests.Client.SharedHistoryUi;

public sealed class HistoryInputTests
{
    private static readonly FarmInputSituation World = new(true, false, false);

    [Fact]
    public void Independent_history_entry_is_read_only_while_paused_or_an_action_is_pending()
    {
        var router = new FarmInputRouter(paused: true);
        Assert.Equal(FarmSemanticAction.OpenHistory,
            router.Route(World with { ActionPending = true }, FarmInputSignal.Press(FarmInputActions.History)).Action);
        router.SetHistoryOpen(true);
        Assert.Equal(FarmInputLayer.History, router.Layer(World));
        Assert.False(router.WorldShouldRun);
        Assert.True(router.Permits(World with { ActionPending = true }, FarmSemanticAction.InspectHistory));
        Assert.False(router.Permits(World, FarmSemanticAction.Gather));
        Assert.False(router.Permits(World, FarmSemanticAction.OpenInventory));
        Assert.False(router.Permits(World, FarmSemanticAction.TogglePause));
        Assert.Equal(FarmSemanticAction.CloseHistory,
            router.Route(World, FarmInputSignal.Press(FarmInputActions.Cancel, FarmInputActions.Craft)).Action);
        Assert.True(router.Paused);
    }

    [Fact]
    public void History_unwinds_without_replaying_held_navigation_as_world_movement()
    {
        var router = new FarmInputRouter();
        var held = new FarmDirectionalInput(0, 1, 0, 0);
        router.SetHistoryOpen(true);
        Assert.True(router.WorldShouldRun);
        Assert.True(router.Movement(World, held).IsNeutral);
        Assert.Equal(FarmSemanticAction.CloseHistory,
            router.Route(World, FarmInputSignal.Press(FarmInputActions.History)).Action);
        router.CaptureBoundary(World, held);
        router.SetHistoryOpen(false);
        Assert.True(router.Route(World, FarmInputSignal.Release(FarmInputActions.History)).Consumed);
        Assert.True(router.Movement(World, held).IsNeutral);
        Assert.True(router.Movement(World, FarmDirectionalInput.Neutral).IsNeutral);
        Assert.False(router.Movement(World, held).IsNeutral);
    }

    [Fact]
    public void Native_history_button_owns_accept_release_and_context_loss_blocks_commands()
    {
        var router = new FarmInputRouter();
        router.SetHistoryOpen(true);
        var accept = FarmInputSignal.Press(FarmInputActions.Interact) with { FocusedControlAccept = true };
        Assert.Equal(new FarmInputDecision(true, FarmSemanticAction.None), router.Route(World, accept));
        Assert.False(router.Route(World, FarmInputSignal.Release(FarmInputActions.Interact)).Consumed);
        router.SetFocused(false);
        Assert.False(router.Permits(World, FarmSemanticAction.InspectHistory));
        Assert.False(router.Permits(World, FarmSemanticAction.RefreshHistory));
        Assert.False(router.Permits(World, FarmSemanticAction.OpenHistory));
    }

    [Fact]
    public void Wheel_navigation_can_open_history_without_exposing_inventory_or_trade_actions()
    {
        var router = new FarmInputRouter();
        router.SetWheelOpen(true);
        Assert.Equal(FarmSemanticAction.OpenHistory,
            router.Route(World, FarmInputSignal.Press(FarmInputActions.History)).Action);
        router.SetWheelOpen(false);
        router.SetHistoryOpen(true);
        Assert.True(router.Permits(World, FarmSemanticAction.RefreshHistory));
        Assert.True(router.Permits(World, FarmSemanticAction.NextHistory));
        Assert.False(router.Permits(World, FarmSemanticAction.ShowTradeLot));
        Assert.False(router.Permits(World, FarmSemanticAction.ProposeMaterial));
    }
}
