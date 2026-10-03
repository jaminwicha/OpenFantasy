using OpenFantasy.Client;

namespace OpenFantasy.Hosting.Tests.Client.RestorativeChoices;

public sealed class CareChoiceInputTests
{
    private static readonly FarmInputSituation World = new(true, false, false);
    private static readonly FarmDirectionalInput Right = new(0, 1, 0, 0);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Browsing_and_closing_preserve_the_existing_readiness_policy(bool paused)
    {
        var input = new FarmInputRouter(paused);
        Assert.True(input.Permits(World, FarmSemanticAction.OpenCare));
        bool ready = input.WorldShouldRun;
        input.SetCareOpen(true);
        Assert.Equal(FarmInputLayer.Care, input.Layer(World));
        Assert.Equal(ready, input.WorldShouldRun);
        Assert.True(input.Permits(World, FarmSemanticAction.PrepareCare));
        Assert.Equal(!paused, input.Permits(World, FarmSemanticAction.ConfirmCare));
        Assert.False(input.Permits(World, FarmSemanticAction.TogglePause));
        input.SetCareOpen(false);
        Assert.Equal(paused, input.Paused);
        Assert.Equal(ready, input.WorldShouldRun);
    }

    [Fact]
    public void Menu_direction_stays_neutral_after_closing_until_released()
    {
        var input = new FarmInputRouter();
        input.SetCareOpen(true);
        Assert.True(input.Movement(World, Right).IsNeutral);
        input.CaptureBoundary(World, Right);
        input.SetCareOpen(false);
        Assert.True(input.Movement(World, Right).IsNeutral);
        Assert.True(input.Movement(World, FarmDirectionalInput.Neutral).IsNeutral);
        Assert.False(input.Movement(World, Right).IsNeutral);
    }

    [Fact]
    public void Controller_cancel_does_not_fall_through_to_quick_crafting()
    {
        var input = new FarmInputRouter();
        input.SetCareOpen(true);
        string[] names = [FarmInputActions.Cancel, FarmInputActions.Craft];
        var press = input.Route(World, new(names, true, false));
        Assert.True(press.Consumed);
        Assert.Equal(FarmSemanticAction.CloseCare, press.Action);
        input.SetCareOpen(false);
        var release = input.Route(World, new(names, false, false));
        Assert.True(release.Consumed);
        Assert.Equal(FarmSemanticAction.None, release.Action);
        Assert.Equal(FarmSemanticAction.Craft,
            input.Route(World, new([FarmInputActions.Craft], true, false)).Action);
    }

    [Fact]
    public void Focused_native_button_owns_accept_release_without_world_talk()
    {
        var input = new FarmInputRouter();
        input.SetCareOpen(true);
        var press = input.Route(World, new([FarmInputActions.Interact], true, false) { FocusedControlAccept = true });
        Assert.True(press.Consumed);
        Assert.Equal(FarmSemanticAction.None, press.Action);
        var release = input.Route(World, new([FarmInputActions.Interact], false, false));
        Assert.False(release.Consumed);
        Assert.Equal(FarmSemanticAction.None, release.Action);
    }

    [Fact]
    public void Pending_work_or_reading_blocks_commit_but_keeps_close_available()
    {
        var input = new FarmInputRouter();
        input.SetCareOpen(true);
        foreach (var blocked in new[] { World with { ActionPending = true }, World with { ReadingHold = true } })
        {
            Assert.False(input.Permits(blocked, FarmSemanticAction.PrepareCare));
            Assert.False(input.Permits(blocked, FarmSemanticAction.ConfirmCare));
            Assert.True(input.Permits(blocked, FarmSemanticAction.CloseCare));
            Assert.True(input.Movement(blocked, Right).IsNeutral);
        }
    }

    [Fact]
    public void Focus_loss_and_pending_control_cannot_confirm_a_care()
    {
        var input = new FarmInputRouter();
        input.SetCareOpen(true);
        input.SetFocused(false);
        Assert.False(input.Permits(World, FarmSemanticAction.PrepareCare));
        Assert.False(input.Permits(World, FarmSemanticAction.ConfirmCare));
        input.SetFocused(true);
        input.RequireControlResume(true);
        Assert.Equal(FarmInputLayer.ControlReturn, input.Layer(World));
        Assert.False(input.Permits(World, FarmSemanticAction.ConfirmCare));
        Assert.False(input.WorldShouldRun);
        input.RequireControlResume(false);
        Assert.False(input.Permits(World, FarmSemanticAction.ConfirmCare));
        Assert.True(input.Paused);
    }

    [Fact]
    public void Care_cannot_open_over_existing_modal_owners()
    {
        var input = new FarmInputRouter();
        input.SetInventoryOpen(true);
        Assert.False(input.Permits(World, FarmSemanticAction.OpenCare));
        input.SetInventoryOpen(false);
        input.SetWheelOpen(true);
        Assert.False(input.Permits(World, FarmSemanticAction.OpenCare));
        input.SetWheelOpen(false);
        input.SetMarketOpen(true);
        Assert.False(input.Permits(World, FarmSemanticAction.OpenCare));
    }
}
