using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Hosting.Simulation;

public sealed record LocalCraftTermView(EntityId DefinitionId, ulong Quantity, RecipeMaterialRole Role);

/// <summary>Public recipe requirements. Batch histories and source claims remain private to the authority.</summary>
public sealed class LocalCraftRecipeView
{
    /// <summary>Rehydrates immutable presentation values without consulting or constructing authority state.</summary>
    internal LocalCraftRecipeView(EntityId recipeId,
        ContentHash version,
        string key,
        ReadOnlyCollection<LocalCraftTermView> terms)
    {
        RecipeId = recipeId;
        Version = version;
        Key = key;
        Terms = Array.AsReadOnly(terms.ToArray());
    }

    internal LocalCraftRecipeView(CraftRecipeDefinition recipe)
    {
        RecipeId = recipe.DefinitionId;
        Version = recipe.Version;
        Key = recipe.PhysicalRecipe.Key;
        Terms = Array.AsReadOnly(recipe.PhysicalRecipe.Terms.Select(term =>
            new LocalCraftTermView(recipe.ItemFor(term).DefinitionId, term.Count, term.Role)).ToArray());
    }

    public EntityId RecipeId { get; }
    public ContentHash Version { get; }
    public string Key { get; }
    public ReadOnlyCollection<LocalCraftTermView> Terms { get; }
}

/// <summary>A concurrency token and admitted local recipes; the token grants no crafting authority.</summary>
public sealed class LocalCraftingView
{
    /// <summary>Rehydrates immutable presentation values without consulting or constructing authority state.</summary>
    internal LocalCraftingView(ulong revision,
        ReadOnlyCollection<LocalCraftRecipeView> recipes)
    {
        Revision = revision;
        Recipes = Array.AsReadOnly(recipes.ToArray());
    }

    internal LocalCraftingView(ProductionSnapshot production, Func<EntityId, bool>? isConstructionRecipe = null)
    {
        Revision = production.Revision;
        Recipes = Array.AsReadOnly(production.Recipes.Where(recipe => isConstructionRecipe?.Invoke(recipe.DefinitionId) != true)
            .Select(recipe => new LocalCraftRecipeView(recipe)).ToArray());
    }

    public ulong Revision { get; }
    public ReadOnlyCollection<LocalCraftRecipeView> Recipes { get; }
}
