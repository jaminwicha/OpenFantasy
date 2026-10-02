using OpenFantasy.Client;

namespace OpenFantasy.Hosting.Tests.Client.ControlReturn;

public sealed class ControlReturnInputTests
{
    private static readonly FarmInputSituation Play = new(true, false, false);

    [Fact]
    public void Pending_return_blocks_actions_resume_and_held_walking()
    {
        var input = new FarmInputRouter();
        input.RequireControlResume(pending: true);
        Assert.True(input.Paused);
        Assert.False(input.WorldShouldRun);
        Assert.Equal(FarmInputLayer.ControlReturn, input.Layer(Play));
        Assert.All(Enum.GetValues<FarmSemanticAction>(), action => Assert.False(input.Permits(Play, action)));
        Assert.False(input.TogglePause());
        Assert.Equal(FarmMovementIntent.Neutral, input.Movement(Play, new(0, 1, 0, 0)));

        input.RequireControlResume(pending: false);
        Assert.True(input.Paused);
        Assert.False(input.WorldShouldRun);
        Assert.True(input.TogglePause());
        Assert.True(input.WorldShouldRun);
        Assert.Equal(FarmMovementIntent.Neutral, input.Movement(Play, new(0, 1, 0, 0)));
        Assert.Equal(FarmMovementIntent.Neutral, input.Movement(Play, FarmDirectionalInput.Neutral));
        Assert.Equal(new FarmMovementIntent(32, 0), input.Movement(Play, new(0, 1, 0, 0)));
    }

    [Fact]
    public void Restoration_while_unfocused_does_not_resume_on_focus_return()
    {
        var input = new FarmInputRouter();
        input.SetFocused(false);
        input.RequireControlResume(pending: false);
        Assert.False(input.WorldShouldRun);
        input.SetFocused(true);
        Assert.True(input.Paused);
        Assert.False(input.WorldShouldRun);
        Assert.True(input.TogglePause());
        Assert.True(input.WorldShouldRun);
    }

    [Fact]
    public void Pause_held_across_restoration_requires_release_and_new_press()
    {
        var input = new FarmInputRouter();
        input.RequireControlResume(pending: true);
        Assert.Equal(FarmSemanticAction.None, input.Route(Play, FarmInputSignal.Press(FarmInputActions.Pause)).Action);
        input.RequireControlResume(pending: false);
        Assert.Equal(FarmSemanticAction.None, input.Route(Play, FarmInputSignal.Press(FarmInputActions.Pause)).Action);
        Assert.True(input.Route(Play, FarmInputSignal.Release(FarmInputActions.Pause)).Consumed);
        Assert.Equal(FarmSemanticAction.TogglePause, input.Route(Play, FarmInputSignal.Press(FarmInputActions.Pause)).Action);
        Assert.True(input.Paused); // Routing expresses intention; only the explicit adapter acts on it.
    }
}
