using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Production;

/// <summary>One instantaneous transformation from actual controlled contents; never an elapsed-time job.</summary>
public sealed class InstantCraftPlanner
{
    public CraftPlan Plan(CountedAuthorityState inventory, ProductionSnapshot production,
        TrustedCommandDelivery delivery, EntityId actorId, EntityId commandId, CraftRequest request,
        Func<EntityId, bool>? isIdentityOccupied = null) =>
        Plan(inventory, production, delivery, actorId, commandId, request, isIdentityOccupied, null);

    // Only complete-world composition may supply the authorization for its selected operation.
    internal CraftPlan Plan(CountedAuthorityState inventory, ProductionSnapshot production,
        TrustedCommandDelivery delivery, EntityId actorId, EntityId commandId, CraftRequest request,
        Func<EntityId, bool>? isIdentityOccupied, WorldActorAuthorization? authorization)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(request);
        if (production.IsContinued && isIdentityOccupied is null)
            throw new ArgumentException("Continued production requires permanent historical identity lookup.", nameof(isIdentityOccupied));
        CraftPlan Reject(WorldActionRejection rejection) => new(inventory, production, rejection, []);
        if ((authorization is null ? !inventory.CanControl(delivery, actorId)
            : !authorization.Matches(delivery, actorId, commandId) || !authorization.Permits(WorldControlCapabilities.Production)))
        {
            return Reject(WorldActionRejection.NotAuthorized);
        }
        CustodianAccessPolicy? policy = inventory.Policies.FirstOrDefault(value => value.CustodianId == request.CustodianId);
        if (policy is null || policy.OwnerActorId != actorId || !policy.AllowsDeposit(actorId))
        {
            return Reject(WorldActionRejection.NotAuthorized);
        }
        // This is a state-composition invariant, not a rejection of an untrusted gameplay selection.
        production.ValidateForAuthority(inventory);
        if (production.MaterialHistory?.Operations.Count == MaterialOperationHistory.MaximumOperations)
            return Reject(WorldActionRejection.Unavailable);
        CustodianStorage storage = inventory.Custody.GetStorage(request.CustodianId);
        if (request.ExpectedProductionRevision != production.Revision || request.ExpectedStorageRevision != storage.Revision)
        {
            return Reject(WorldActionRejection.RevisionConflict);
        }
        CraftRecipeDefinition? recipe = production.Recipes.FirstOrDefault(value => value.DefinitionId == request.RecipeId);
        if (recipe is null || recipe.Version != request.RecipeVersion
            || production.Continuation?.ConstructionRecipeIds.Contains(recipe.DefinitionId) == true)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        List<RetiredIngredient> retired = [];
        foreach (IngredientRequest selected in request.Ingredients)
        {
            RetiredIngredient? ingredient = MaterialLotSelection.Select(inventory, production, request.CustodianId, selected);
            if (ingredient is null)
            {
                return Reject(WorldActionRejection.Unavailable);
            }
            retired.Add(ingredient);
        }
        if (!MatchesRecipe(recipe, retired) || production.Batches.Count == ProductionSnapshot.MaximumBatches)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        UInt128 consumed = 0;
        UInt128 produced = 0;
        foreach (RetiredIngredient ingredient in retired)
        {
            consumed += ingredient.Quantity;
        }
        foreach (RecipeTerm output in recipe.Outputs)
        {
            produced += output.Count;
        }
        UInt128 after = (UInt128)production.GetCombinedStoredQuantity(inventory, request.CustodianId) - consumed + produced;
        if (after > storage.UnitCapacity)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        EntityId[] created = Enumerable.Range(0, recipe.Outputs.Count + 1)
            .Select(index => EntityId.DeriveSpawned(commandId, (ulong)index)).ToArray();
        if (created.Distinct().Count() != created.Length || created.Any(id => id == commandId
            || inventory.ContainsIdentity(id) || production.ContainsIdentity(id) || isIdentityOccupied?.Invoke(id) == true)
            || inventory.ContainsIdentity(commandId) || production.ContainsIdentity(commandId)
            || isIdentityOccupied?.Invoke(commandId) == true)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        try
        {
            return MaterialTransformCommitter.Prepare(inventory, production, actorId, commandId, request, recipe, retired, created);
        }
        catch (OverflowException)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        catch (ProductionFormatException error) when (error.Failure == ProductionFormatFailure.LimitExceeded)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
    }

    private static bool MatchesRecipe(CraftRecipeDefinition recipe, IEnumerable<RetiredIngredient> retired)
    {
        Dictionary<CountedItemDefinition, UInt128> totals = [];
        foreach (RetiredIngredient ingredient in retired)
        {
            totals[ingredient.Definition] = totals.GetValueOrDefault(ingredient.Definition) + ingredient.Quantity;
        }
        return totals.Count == recipe.Inputs.Count && recipe.Inputs.All(term =>
            totals.GetValueOrDefault(recipe.ItemFor(term)) == term.Count);
    }

}
