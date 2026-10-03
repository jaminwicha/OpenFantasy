using Godot;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Recipes own a local inspection context; the existing session submits a separately captured batch.</summary>
public partial class FarmGame
{
    private FarmRecipePanel _recipes = null!;
    private readonly FarmRecipeLibraryModel _recipeLibrary = new();
    internal bool IsRecipesOpen => _input.RecipesOpen;
    internal FarmRecipeLibraryModel SelectedRecipeLibrary => _recipeLibrary;

    private void ReadyRecipes()
    {
        _recipes = GD.Load<PackedScene>("res://scenes/FarmRecipePanel.tscn").Instantiate<FarmRecipePanel>();
        AddChild(_recipes);
        _recipes.ActionRequested += Perform;
        _recipes.SelectionRequested += identity =>
        {
            if (!_input.Permits(Situation(), FarmSemanticAction.PrepareRecipe)) return;
            if (RefreshRecipeChoice() && _recipeLibrary.Select(identity)) PresentRecipePanel();
        };
    }

    private void OpenRecipes()
    {
        if (_session is null) return;
        try
        {
            // Capture the current selection before exposing any recipe belonging to it.
            var observation = _session.ReadSelection();
            ObserveActorSelection(observation);
            ObserveControlReturn(observation?.ControlStatus ?? ParticipantControl);
            if (!_input.Permits(Situation(), FarmSemanticAction.OpenRecipes)) return;
            _session.RefreshRecipes(_recipeLibrary, observation);
        }
        catch (InvalidOperationException error)
        {
            _recipeLibrary.Clear();
            _hud.Message(error.Message);
            return;
        }
        if (_recipeLibrary.Entries.Count == 0)
        {
            _hud.Message("No crafting recipes are available for this character.");
            return;
        }
        _recipeLibrary.DiscardReview();
        _input.SetRecipesOpen(true);
        _recipes.Open(_recipeLibrary);
        _hud.SetCovered(true);
        try { SendMovement(0, 0); }
        catch (InvalidOperationException error)
        {
            CloseRecipes();
            _hud.Message(error.Message);
            return;
        }
        PresentRecipePanel();
        _recipes.FocusSelection();
    }

    private void CloseRecipes()
    {
        _input.CaptureBoundary(Situation(), ReadWalkingInput());
        _input.SetRecipesOpen(false);
        _recipeLibrary.DiscardReview();
        _hud.SetCovered(false);
        _recipes.Close();
    }

    private void RefreshRecipes(LocalSelectedPlayerObservation? observation)
    {
        if (!_input.RecipesOpen || _session is null) return;
        _session.RefreshRecipes(_recipeLibrary, observation);
        if (_recipeLibrary.Entries.Count == 0)
        {
            CloseRecipes();
            _hud.Message("Crafting recipes are no longer available for this character.");
            return;
        }
        PresentRecipePanel();
    }

    private void PresentRecipePanel()
    {
        if (_session is null || !_input.RecipesOpen) return;
        _recipes.Present(_recipeLibrary, _session.World.ReadRunState(), _session.IsRemote, _input.Paused,
            _input.Permits(Situation(), FarmSemanticAction.PrepareRecipe),
            _input.Permits(Situation(), FarmSemanticAction.ConfirmRecipe));
    }

    private void PrepareRecipe()
    {
        if (!RefreshRecipeChoice() || !_input.Permits(Situation(), FarmSemanticAction.PrepareRecipe)) return;
        _recipeLibrary.Prepare();
        PresentRecipePanel();
    }

    private void ConfirmRecipe()
    {
        FarmActionOffer? prepared = _recipeLibrary.PreparedOffer;
        // A fresh session read invalidates stale captures without replacing them.
        if (!RefreshRecipeChoice() || !_input.Permits(Situation(), FarmSemanticAction.ConfirmRecipe) || prepared is null
            || !ReferenceEquals(prepared, _recipeLibrary.PreparedOffer)) return;
        _recipeLibrary.DiscardReview();
        Submit(prepared);
        _recipeLibrary.DiscardReview(_action is null ? "The batch could not be submitted. Prepare it again."
            : "The prepared batch was submitted. Waiting for its result…");
        PresentRecipePanel();
    }

    private bool RefreshRecipeChoice()
    {
        if (_session is null || !_input.RecipesOpen) return false;
        try
        {
            var observation = _session.ReadSelection();
            ObserveActorSelection(observation);
            ObserveControlReturn(observation?.ControlStatus ?? ParticipantControl);
            RefreshRecipes(observation);
            return _input.RecipesOpen;
        }
        catch (InvalidOperationException error)
        {
            InterruptRecipeReview("The current recipe view is unavailable. " + error.Message);
            return false;
        }
    }

    private void InterruptRecipeReview(string reason)
    {
        _recipeLibrary.DiscardReview(reason);
        PresentRecipePanel();
    }
}
