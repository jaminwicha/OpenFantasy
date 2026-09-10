using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Production;

public sealed class CraftRequest
{
    public CraftRequest(EntityId recipeId, ContentHash recipeVersion, ulong expectedProductionRevision,
        EntityId custodianId, ulong expectedStorageRevision, IEnumerable<IngredientRequest> ingredients)
    {
        ArgumentNullException.ThrowIfNull(recipeVersion);
        Ingredients = ProductionCollections.Copy(ingredients, MaterialRecipeDefinition.MaximumTermCount, value => value.LotId);
        if (Ingredients.Count == 0)
        {
            throw new ArgumentException("Crafting requires explicit ingredient selections.");
        }
        RecipeId = recipeId;
        RecipeVersion = recipeVersion;
        ExpectedProductionRevision = expectedProductionRevision;
        CustodianId = custodianId;
        ExpectedStorageRevision = expectedStorageRevision;
    }

    public EntityId RecipeId { get; }
    public ContentHash RecipeVersion { get; }
    public ulong ExpectedProductionRevision { get; }
    public EntityId CustodianId { get; }
    public ulong ExpectedStorageRevision { get; }
    public ReadOnlyCollection<IngredientRequest> Ingredients { get; }
}
