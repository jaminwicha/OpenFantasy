using OpenFantasy.Client;

namespace OpenFantasy.Hosting.Tests.Client.Trading;

/// <summary>The stall panel's input ownership: separate from host readiness, movement and commitment.</summary>
public sealed class MarketInputTests
{
    private static readonly FarmInputSituation Exploring = new(SessionOpen: true, ReadingHold: false, ActionPending: false);

    [Fact]
    public void The_open_stall_suppresses_walking_without_changing_readiness_and_closes_on_cancel()
    {
        var router = new FarmInputRouter();
        Assert.True(router.WorldShouldRun);
        router.SetMarketOpen(true);
        Assert.Equal(FarmInputLayer.Market, router.Layer(Exploring));
        // The panel never pauses the host: a waiting request can still be answered.
        Assert.True(router.WorldShouldRun);
        var held = new FarmDirectionalInput(0, 1, 0, 0);
        Assert.Equal(FarmMovementIntent.Neutral, router.Movement(Exploring, held));
        foreach (string close in new[] { FarmInputActions.Pause, FarmInputActions.Cancel, FarmInputActions.QuickWheel })
        {
            Assert.Equal(new FarmInputDecision(true, FarmSemanticAction.CloseMarket), router.Route(Exploring, FarmInputSignal.Press(close)));
            router.Route(Exploring, FarmInputSignal.Release(close));
        }
        // Controller B is both Cancel and Craft: in the stall it unwinds and never crafts.
        Assert.Equal(FarmSemanticAction.CloseMarket,
            router.Route(Exploring, FarmInputSignal.Press(FarmInputActions.Cancel, FarmInputActions.Craft)).Action);
        router.Route(Exploring, FarmInputSignal.Release(FarmInputActions.Cancel, FarmInputActions.Craft));
        router.SetMarketOpen(false);
        // A direction held through the panel stays neutral until released.
        Assert.Equal(FarmMovementIntent.Neutral, router.Movement(Exploring, held));
        Assert.Equal(FarmMovementIntent.Neutral, router.Movement(Exploring, FarmDirectionalInput.Neutral));
        Assert.NotEqual(FarmMovementIntent.Neutral, router.Movement(Exploring, held));
    }

    [Fact]
    public void A_focused_stall_control_owns_controller_accept_and_world_verbs_do_not_leak()
    {
        var router = new FarmInputRouter();
        router.SetMarketOpen(true);
        FarmInputDecision accept = router.Route(Exploring, FarmInputSignal.Press(FarmInputActions.Interact) with { FocusedControlAccept = true });
        Assert.Equal(new FarmInputDecision(true, FarmSemanticAction.None), accept);
        // The release belongs to the Control, which completes native activation.
        Assert.False(router.Route(Exploring, FarmInputSignal.Release(FarmInputActions.Interact)).Consumed);
        foreach (string verb in new[] { FarmInputActions.Interact, FarmInputActions.Give, FarmInputActions.Gather,
            FarmInputActions.Inventory, FarmInputActions.NextTarget })
        {
            Assert.Equal(FarmSemanticAction.None, router.Route(Exploring, FarmInputSignal.Press(verb)).Action);
            router.Route(Exploring, FarmInputSignal.Release(verb));
        }
        Assert.False(router.Permits(Exploring, FarmSemanticAction.OpenInventory));
        Assert.False(router.Permits(Exploring, FarmSemanticAction.OpenWheel));
        Assert.False(router.Permits(Exploring, FarmSemanticAction.Speak));
    }

    [Fact]
    public void Stall_commitments_need_running_time_while_inspection_and_the_reading_pause_do_not()
    {
        var router = new FarmInputRouter();
        router.SetMarketOpen(true);
        Assert.True(router.Permits(Exploring, FarmSemanticAction.ConfirmTrade));
        Assert.True(router.Permits(Exploring, FarmSemanticAction.ShowTradeLot));
        Assert.False(router.Permits(Exploring with { }, FarmSemanticAction.TradeBuy));
        Assert.True(router.Permits(Exploring, FarmSemanticAction.TogglePause));
        Assert.True(router.TogglePause());
        Assert.Equal(FarmInputLayer.Market, router.Layer(Exploring));
        Assert.False(router.WorldShouldRun);
        Assert.False(router.Permits(Exploring, FarmSemanticAction.ConfirmTrade));
        Assert.False(router.Permits(Exploring, FarmSemanticAction.ShowTradeLot));
        Assert.True(router.Permits(Exploring, FarmSemanticAction.CloseMarket));
        Assert.True(router.Permits(Exploring, FarmSemanticAction.TogglePause));
        Assert.False(router.Permits(Exploring with { ActionPending = true }, FarmSemanticAction.ConfirmTrade));
        router.SetFocused(false);
        Assert.False(router.Permits(Exploring, FarmSemanticAction.CloseMarket));
        Assert.False(router.Permits(Exploring, FarmSemanticAction.TogglePause));
    }

    [Fact]
    public void A_paused_wheel_opens_an_inspection_panel_but_still_refuses_world_actions()
    {
        var router = new FarmInputRouter(paused: true);
        router.SetWheelOpen(true);
        Assert.False(router.Permits(Exploring, FarmSemanticAction.ConfirmWheel));
        Assert.True(router.Permits(Exploring with { WheelChoiceOpensPanel = true }, FarmSemanticAction.ConfirmWheel));
        Assert.True(router.Permits(Exploring with { WheelChoiceOpensPanel = true, ActionPending = true }, FarmSemanticAction.ConfirmWheel));
        Assert.False(router.Permits(Exploring with { WheelChoiceOpensPanel = true, ReadingHold = true }, FarmSemanticAction.ConfirmWheel));
        router.TogglePause();
        Assert.True(router.Permits(Exploring, FarmSemanticAction.ConfirmWheel));
        Assert.False(router.Permits(Exploring with { ActionPending = true }, FarmSemanticAction.ConfirmWheel));
    }
}
