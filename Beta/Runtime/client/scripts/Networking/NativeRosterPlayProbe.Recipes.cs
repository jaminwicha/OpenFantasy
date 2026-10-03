using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Client.Networking;

/// <summary>Actual recipe controls with separate browsing, preparation and commitment.</summary>
internal sealed partial class NativeRosterPlayProbe
{
    private FarmActionOffer? _preparedRecipe;
    private readonly List<object> _recipeCaptures = [];
    private FarmRecipePanel RecipePanel => _game.GetNode<FarmRecipePanel>("FarmRecipePanel");
    private Task RecipeUntil(Func<bool> predicate, string failure) => _driver.Until(predicate, failure, 5);

    private async Task OpenRecipes()
    {
        await CloseSelectionPanel();
        MarketProbeDriver.Require(!_game.IsInventoryOpen && !_game.IsWheelOpen, "Close other inspections before Recipes.");
        var selection = Selected.Selection;
        int before = _submissions.Count;
        if (!_game.IsRecipesOpen) await _driver.Click(Hud.GetNode<Button>("%Recipes"));
        await RecipeUntil(() => _game.IsRecipesOpen && RecipePanel.CurrentModel is not null
            && _game.PresentedSelection?.Selection == selection, "The actual Recipes panel did not open for this selection.");
        MarketProbeDriver.Require(Selected.Selection == selection && _submissions.Count == before, "Opening Recipes changed scope or submitted work.");
    }

    private async Task SelectRecipe(EntityId recipe)
    {
        await OpenRecipes();
        int before = _submissions.Count;
        var selection = Selected.Selection;
        Button button = Descendants(RecipePanel).OfType<Button>().Single(value => value.HasMeta("recipe_id")
            && value.GetMeta("recipe_id").AsString() == recipe.ToString());
        await _driver.Click(button);
        await RecipeUntil(() => RecipePanel.CurrentModel?.SelectedId == recipe, "The actual recipe row did not retain its identity.");
        MarketProbeDriver.Require(Selected.Selection == selection && _submissions.Count == before && !_game.HasPendingAction,
            "Recipe browsing changed actor or submitted work.");
    }

    private async Task PrepareRecipe()
    {
        MarketProbeDriver.Require(_game.IsRecipesOpen, "Open Recipes before preparing a batch.");
        int before = _submissions.Count;
        var selection = Selected.Selection;
        await RecipeUntil(() => RecipePanel.CurrentModel?.Selected?.Offer.CanExecute == true, "The selected recipe is unavailable.");
        await _driver.Click(RecipePanel.GetNode<Button>("%PrepareRecipe"));
        await RecipeUntil(() => RecipePanel.CurrentModel?.PreparedOffer is not null, "The actual Prepare button captured no batch.");
        _preparedRecipe = RecipePanel.CurrentModel!.PreparedOffer!;
        MarketProbeDriver.Require(_preparedRecipe.Scope.Selection == selection && Selected.Selection == selection
            && MatchesProductionOffer(_preparedRecipe) && _submissions.Count == before && !_game.HasPendingAction,
            "Preparation changed scope or submitted ingredients before confirmation.");
    }

    private async Task ConfirmRecipe(string expected)
    {
        FarmActionOffer prepared = _preparedRecipe ?? throw new InvalidOperationException("No native recipe batch was prepared.");
        int before = _submissions.Count;
        Button confirm = RecipePanel.GetNode<Button>("%ConfirmRecipe");
        if (expected == "blocked")
        {
            MarketProbeDriver.Require(!_game.IsRecipesOpen || confirm.Disabled || _game.IsLocallyPaused
                || RecipePanel.CurrentModel?.PreparedOffer is null, "The expected blocked recipe is still executable.");
            if (_game.IsRecipesOpen) await _driver.Click(confirm, requireEnabled: false);
            else _driver.Tap(Key.Enter);
            await _driver.Frame();
            await _driver.Frame();
            MarketProbeDriver.Require(_submissions.Count == before && !_game.HasPendingAction, "Blocked recipe confirmation submitted work.");
            return;
        }
        MarketProbeDriver.Require(expected == "Applied" && !_game.IsLocallyPaused && _game.IsRecipesOpen
            && RecipePanel.CurrentModel?.PreparedOffer is { CanExecute: true } current && current.Scope == prepared.Scope
            && SameProductionIntent(current.Intent, prepared.Intent) && prepared.Scope.Selection == Selected.Selection
            && _game.PresentedSelection?.Selection == Selected.Selection,
            "The original prepared recipe is stale or unavailable; no replacement will be prepared.");
        await _driver.Click(confirm);
        await RecipeUntil(() => _submissions.Count == before + 1 && _submissions[^1].Receipt.IsCompleted
            && !_game.HasPendingAction && ProductionOutcomeObserved(_game.CurrentSelection, prepared)
            && ProductionOutcomeObserved(_game.PresentedSelection, prepared),
            "One recipe confirmation did not return its receipt and presented production outcome.");
        FarmSubmission submitted = _submissions[^1];
        WorldActionCommand command = WorldActionCodec.Decode(submitted.CanonicalCommandBytes());
        MarketProbeDriver.Require(!submitted.IsRetry && submitted.Receipt.IsCompletedSuccessfully
            && submitted.Receipt.Result.Status == CommandReceiptStatus.Applied
            && command.PrincipalId == prepared.Scope.PrincipalId && command.OriginSessionId == prepared.Scope.SessionId
            && command.WorldId == prepared.Scope.WorldId && command.BranchId == prepared.Scope.BranchId
            && command.ActorId == prepared.Scope.ActorId && command.ExpectedActorRevision == prepared.Scope.ExpectedActorRevision
            && command.ControlWitness == prepared.Scope.Selection!.ControlWitness && SameProductionIntent(command.Intent, prepared.Intent),
            "The recipe submission replaced the prepared ingredients or truthful operator.");
    }

    private async Task CloseRecipes()
    {
        int before = _submissions.Count;
        if (_game.IsRecipesOpen) await _driver.Click(RecipePanel.GetNode<Button>("%CloseRecipes"));
        await RecipeUntil(() => !_game.IsRecipesOpen, "The actual Close button did not close Recipes.");
        MarketProbeDriver.Require(_submissions.Count == before && !_game.HasPendingAction, "Closing Recipes submitted work.");
    }

    private async Task CancelRecipe()
    {
        MarketProbeDriver.Require(_game.IsRecipesOpen, "Controller cancellation requires an open Recipes panel.");
        int before = _submissions.Count;
        bool paused = _game.IsLocallyPaused;
        _driver.Joy(JoyButton.B);
        await RecipeUntil(() => !_game.IsRecipesOpen, "Controller cancellation did not close Recipes.");
        MarketProbeDriver.Require(_submissions.Count == before && !_game.HasPendingAction
            && _game.IsLocallyPaused == paused && _game.SelectedRecipeLibrary.PreparedOffer is null,
            "Recipe cancellation submitted work, retained a prepared batch or changed pause.");
    }

    private async Task CaptureRecipes(string label)
    {
        MarketProbeDriver.Require(_game.IsRecipesOpen && label.Length is > 0 and <= 64 && _recipeCaptures.Count < 64,
            "Recipe capture requires an open library and bounded label.");
        await Capture(5, "-recipes");
        Control root = RecipePanel.GetNode<Control>("%RecipeRoot");
        MarketProbeDriver.Require(GetViewport().GetVisibleRect().Encloses(root.GetGlobalRect()), "Recipes extends beyond the viewport.");
        var labels = new[] { "RecipeTitle", "RecipeInputs", "RecipeOutputs", "RecipeAvailability", "RecipeTime", "RecipeNotice" }
            .Select(name => RecipePanel.GetNode<Label>("%" + name)).Where(value => value.IsVisibleInTree()).ToArray();
        foreach (Label value in labels)
        {
            MarketProbeDriver.Require(value.GetLineCount() == value.GetVisibleLineCount()
                && root.GetGlobalRect().Encloses(value.GetGlobalRect()), "A recipe description is clipped.");
            RequireRecipeScrollVisible(value);
        }
        var buttons = Descendants(RecipePanel).OfType<Button>().Where(value => value.IsVisibleInTree()).ToArray();
        foreach (Button button in buttons)
        {
            MarketProbeDriver.Require(GetViewport().GetVisibleRect().Encloses(button.GetGlobalRect()), "A recipe control is clipped.");
            RequireRecipeScrollVisible(button);
        }
        _recipeCaptures.Add(new { Sequence = _sequence, Label = label, Image = $"{_sequence:D4}-recipes.png",
            Selection = SelectionEvidence(), Recipes = RecipeEvidence(), Bounds = Bounds(root.GetGlobalRect()),
            Text = labels.Select(value => new { Name = value.Name.ToString(), value.Text, Bounds = Bounds(value.GetGlobalRect()),
                Lines = value.GetLineCount(), VisibleLines = value.GetVisibleLineCount() }).ToArray(),
            Buttons = buttons.Select(value => new { Name = value.Name.ToString(), value.Text, value.Disabled,
                Bounds = Bounds(value.GetGlobalRect()) }).ToArray() });
    }

    private static void RequireRecipeScrollVisible(Control control)
    {
        for (Node? parent = control.GetParent(); parent is not null; parent = parent.GetParent())
            if (parent is ScrollContainer scroll)
                MarketProbeDriver.Require(scroll.GetGlobalRect().Encloses(control.GetGlobalRect()),
                    "A recipe control or description is clipped by its actual scroll viewport.");
    }

    private object? RecipeEvidence()
    {
        if (_game.CurrentSelection is not { } observation) return null;
        var model = _game.SelectedRecipeLibrary;
        var controlled = observation.ControlledView;
        return new
        {
            Actor = observation.Selection.ActorId.ToString(), observation.Selection.Generation, Open = _game.IsRecipesOpen,
            InventoryDisclosed = controlled is null || controlled.Inventory is not null,
            RecoveryDisclosed = controlled is null || controlled.Recovery is not null,
            CombatDisclosed = controlled is null || controlled.Combat is not null,
            ProductionDisclosed = controlled is null || controlled.Production is not null,
            Selected = model?.SelectedId?.ToString(), Notice = model?.Notice,
            Entries = model?.Entries.Select(entry => new { Id = entry.RecipeId.ToString(), entry.Key, entry.Label,
                entry.InputsText, entry.OutputsText, Offer = ProductionOfferEvidence(entry.Offer) }).ToArray() ?? [],
            Prepared = ProductionOfferEvidence(model?.PreparedOffer), Captured = ProductionOfferEvidence(_preparedRecipe)
        };
    }
}
