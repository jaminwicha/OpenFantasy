using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Production;

/// <summary>A committed transformation's complete ingredient retirement and pinned output budget.</summary>
public sealed class ProductionBatch
{
    public ProductionBatch(EntityId batchId, EntityId commandId, EntityId actorId, EntityId custodianId,
        EntityId recipeId, ContentHash recipeVersion, IEnumerable<RetiredIngredient> ingredients)
    {
        ArgumentNullException.ThrowIfNull(recipeVersion);
        if (batchId != EntityId.DeriveSpawned(commandId, 0))
        {
            throw new ArgumentException("A production batch must use its command's deterministic batch identity.");
        }
        Ingredients = ProductionCollections.Copy(ingredients, MaterialRecipeDefinition.MaximumTermCount, value => value.LotId);
        if (Ingredients.Count == 0)
        {
            throw new ArgumentException("A production batch cannot create outputs without retired ingredients.");
        }
        BatchId = batchId;
        CommandId = commandId;
        ActorId = actorId;
        CustodianId = custodianId;
        RecipeId = recipeId;
        RecipeVersion = recipeVersion;
    }

    public EntityId BatchId { get; }
    public EntityId CommandId { get; }
    public EntityId ActorId { get; }
    public EntityId CustodianId { get; }
    public EntityId RecipeId { get; }
    public ContentHash RecipeVersion { get; }
    public ReadOnlyCollection<RetiredIngredient> Ingredients { get; }
}
