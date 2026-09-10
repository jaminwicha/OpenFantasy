using OpenFantasy.Core.Production;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Selects owned ingredients from the public view; the authority rechecks every selection.</summary>
internal static class FarmCrafting
{
    internal static LocalCraftRecipeView? RationsRecipe(LocalWorldView view) =>
        view.Crafting?.Recipes.SingleOrDefault(recipe => recipe.Key == "craft_rations");

    internal static CraftRequest RationsRequest(LocalWorldView view)
    {
        LocalCraftRecipeView recipe = RationsRecipe(view)
            ?? throw new InvalidOperationException("This world has no admitted ration recipe.");
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
                throw new InvalidOperationException("Pack two berries and one wood to make two rations.");
            }
        }
        return new CraftRequest(recipe.RecipeId, recipe.Version, view.Crafting!.Revision,
            view.PlayerInventory.CustodianId, view.PlayerInventory.Revision, selected);
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
