using OpenFantasy.Core.Production;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Either the exact owned lots a ration batch would consume, or the known reason none can be chosen.</summary>
internal sealed record FarmCraftSelection(CraftRequest? Request, string? BlockingReason);

/// <summary>Selects owned ingredients from the public view; the authority rechecks every selection.</summary>
internal static class FarmCrafting
{
    internal const string MissingRecipe = "This world has no admitted ration recipe.";
    internal const string MissingIngredients = "Pack two berries and one wood to make two rations.";

    internal static LocalCraftRecipeView? RationsRecipe(LocalWorldView view) =>
        view.Crafting?.Recipes.SingleOrDefault(recipe => recipe.Key == "craft_rations");

    internal static CraftRequest RationsRequest(LocalWorldView view)
    {
        FarmCraftSelection selection = SelectRations(view);
        return selection.Request ?? throw new InvalidOperationException(selection.BlockingReason);
    }

    /// <summary>Lots are taken in the view's stable lot order until each input term is covered.</summary>
    internal static FarmCraftSelection SelectRations(LocalWorldView view)
    {
        LocalCraftRecipeView? recipe = RationsRecipe(view);
        if (recipe is null)
        {
            return new FarmCraftSelection(null, MissingRecipe);
        }
        List<IngredientRequest> selected = [];
        foreach (LocalCraftTermView term in recipe.Terms.Where(term => term.Role == RecipeMaterialRole.Input))
        {
            ulong remaining = term.Quantity;
            foreach (LocalItemLotView lot in view.PlayerInventory.Lots.Where(lot => lot.DefinitionId == term.DefinitionId))
            {
                ulong quantity = Math.Min(remaining, lot.Quantity);
                if (quantity != 0)
                {
                    selected.Add(new IngredientRequest(lot.Provenance, lot.LotId, lot.Revision, quantity));
                    remaining -= quantity;
                }
                if (remaining == 0)
                {
                    break;
                }
            }
            if (remaining != 0)
            {
                return new FarmCraftSelection(null, MissingIngredients);
            }
        }
        return new FarmCraftSelection(new CraftRequest(recipe.RecipeId, recipe.Version, view.Crafting!.Revision,
            view.PlayerInventory.CustodianId, view.PlayerInventory.Revision, selected), null);
    }

    internal static bool HasIngredients(LocalWorldView view)
    {
        LocalCraftRecipeView? recipe = RationsRecipe(view);
        return recipe is not null && recipe.Terms.Where(term => term.Role == RecipeMaterialRole.Input).All(term =>
            view.PlayerInventory.Lots.Where(lot => lot.DefinitionId == term.DefinitionId).Aggregate(0UL,
                (sum, lot) => checked(sum + lot.Quantity)) >= term.Quantity);
    }

    internal static ulong Quantity(LocalWorldView view, RecipeMaterialRole role)
    {
        var definitions = RationsRecipe(view)?.Terms.Where(term => term.Role == role)
            .Select(term => term.DefinitionId).ToHashSet();
        return definitions is null ? 0 : view.PlayerInventory.Lots.Where(lot => definitions.Contains(lot.DefinitionId))
            .Aggregate(0UL, (sum, lot) => checked(sum + lot.Quantity));
    }
}
