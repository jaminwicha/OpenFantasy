using OpenFantasy.Client;

namespace OpenFantasy.Hosting.Tests.Client.RecipeLibrary;

public sealed class RecipeInputTests
{
    private static readonly FarmInputSituation World = new(true, false, false);
    private static readonly FarmDirectionalInput Right = new(0, 1, 0, 0);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Browsing_and_closing_preserve_the_existing_readiness_policy(bool paused)
    {
        var input = new FarmInputRouter(paused);
        Assert.True(input.Permits(World, FarmSemanticAction.OpenRecipes));
        bool ready = input.WorldShouldRun;
        input.SetRecipesOpen(true);
        Assert.Equal(FarmInputLayer.Recipes, input.Layer(World));
        Assert.Equal(ready, input.WorldShouldRun);
        Assert.True(input.Permits(World, FarmSemanticAction.PrepareRecipe));
        Assert.Equal(!paused, input.Permits(World, FarmSemanticAction.ConfirmRecipe));
        Assert.False(input.Permits(World, FarmSemanticAction.TogglePause));
        input.SetRecipesOpen(false);
        Assert.Equal(paused, input.Paused);
        Assert.Equal(ready, input.WorldShouldRun);
    }

    [Fact]
    public void Menu_direction_stays_neutral_after_closing_until_released()
    {
        var input = new FarmInputRouter();
        input.SetRecipesOpen(true);
        Assert.True(input.Movement(World, Right).IsNeutral);
        input.CaptureBoundary(World, Right);
        input.SetRecipesOpen(false);
        Assert.True(input.Movement(World, Right).IsNeutral);
        Assert.True(input.Movement(World, FarmDirectionalInput.Neutral).IsNeutral);
        Assert.False(input.Movement(World, Right).IsNeutral);
    }

    [Fact]
    public void Controller_cancel_does_not_fall_through_to_quick_crafting()
    {
        var input = new FarmInputRouter();
        input.SetRecipesOpen(true);
        string[] names = [FarmInputActions.Cancel, FarmInputActions.Craft];
        var press = input.Route(World, new(names, true, false));
        Assert.True(press.Consumed);
        Assert.Equal(FarmSemanticAction.CloseRecipes, press.Action);
        input.SetRecipesOpen(false);
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
        input.SetRecipesOpen(true);
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
        input.SetRecipesOpen(true);
        foreach (var blocked in new[] { World with { ActionPending = true }, World with { ReadingHold = true } })
        {
            Assert.False(input.Permits(blocked, FarmSemanticAction.PrepareRecipe));
            Assert.False(input.Permits(blocked, FarmSemanticAction.ConfirmRecipe));
            Assert.True(input.Permits(blocked, FarmSemanticAction.CloseRecipes));
            Assert.True(input.Movement(blocked, Right).IsNeutral);
        }
    }

    [Fact]
    public void Focus_loss_and_pending_control_cannot_confirm_a_recipe()
    {
        var input = new FarmInputRouter();
        input.SetRecipesOpen(true);
        input.SetFocused(false);
        Assert.False(input.Permits(World, FarmSemanticAction.PrepareRecipe));
        Assert.False(input.Permits(World, FarmSemanticAction.ConfirmRecipe));
        input.SetFocused(true);
        input.RequireControlResume(true);
        Assert.Equal(FarmInputLayer.ControlReturn, input.Layer(World));
        Assert.False(input.Permits(World, FarmSemanticAction.ConfirmRecipe));
        Assert.False(input.WorldShouldRun);
        input.RequireControlResume(false);
        Assert.False(input.Permits(World, FarmSemanticAction.ConfirmRecipe));
        Assert.True(input.Paused);
    }

    [Fact]
    public void Recipes_cannot_open_over_existing_modal_owners()
    {
        var input = new FarmInputRouter();
        input.SetInventoryOpen(true);
        Assert.False(input.Permits(World, FarmSemanticAction.OpenRecipes));
        input.SetInventoryOpen(false);
        input.SetWheelOpen(true);
        Assert.False(input.Permits(World, FarmSemanticAction.OpenRecipes));
        input.SetWheelOpen(false);
        input.SetMarketOpen(true);
        Assert.False(input.Permits(World, FarmSemanticAction.OpenRecipes));
    }
}
