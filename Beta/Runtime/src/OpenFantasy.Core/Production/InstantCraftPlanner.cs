using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Production;

/// <summary>One instantaneous transformation from actual controlled contents; never an elapsed-time job.</summary>
public sealed class InstantCraftPlanner
{
    public CraftPlan Plan(CountedAuthorityState inventory, ProductionSnapshot production,
        TrustedCommandDelivery delivery, EntityId actorId, EntityId commandId, CraftRequest request)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(request);
        CraftPlan Reject(WorldActionRejection rejection) => new(inventory, production, rejection, []);
        if (!inventory.CanControl(delivery, actorId))
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
        CustodianStorage storage = inventory.Custody.GetStorage(request.CustodianId);
        if (request.ExpectedProductionRevision != production.Revision || request.ExpectedStorageRevision != storage.Revision)
        {
            return Reject(WorldActionRejection.RevisionConflict);
        }
        CraftRecipeDefinition? recipe = production.Recipes.FirstOrDefault(value => value.DefinitionId == request.RecipeId);
        if (recipe is null || recipe.Version != request.RecipeVersion)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        List<RetiredIngredient> retired = [];
        foreach (IngredientRequest selected in request.Ingredients)
        {
            RetiredIngredient? ingredient = SelectIngredient(inventory, production, request.CustodianId, selected);
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
            || inventory.ContainsIdentity(id) || production.ContainsIdentity(id))
            || inventory.ContainsIdentity(commandId) || production.ContainsIdentity(commandId))
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        try
        {
            return Prepare(inventory, production, actorId, commandId, request, recipe, retired, created);
        }
        catch (OverflowException)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
    }

    private static RetiredIngredient? SelectIngredient(CountedAuthorityState inventory, ProductionSnapshot production,
        EntityId custodianId, IngredientRequest selected)
    {
        if (selected.Kind == IngredientLotKind.Source)
        {
            CountedSourceLot? lot = inventory.Custody.Lots.FirstOrDefault(value => value.LotId == selected.LotId);
            if (lot is null || lot.CustodianId != custodianId || lot.Revision != selected.ExpectedRevision
                || lot.Protection != LotProtection.None || lot.ReservedBy is not null || selected.Quantity > lot.Quantity)
            {
                return null;
            }
            return new RetiredIngredient(lot.LotId, lot.Revision, lot.Definition,
                new SourceInterval(lot.Source.SourceId, lot.Source.Start, checked(lot.Source.Start + selected.Quantity)));
        }
        TransformedLot? derived = production.Lots.FirstOrDefault(value => value.LotId == selected.LotId);
        if (derived is null || derived.CustodianId != custodianId || derived.Revision != selected.ExpectedRevision
            || derived.Protection != LotProtection.None || derived.ReservedBy is not null || selected.Quantity > derived.Quantity)
        {
            return null;
        }
        return new RetiredIngredient(derived.LotId, derived.Revision, derived.Definition,
            new ProductionOutputInterval(derived.Output.BatchId, derived.Output.OutputIndex,
                derived.Output.Start, checked(derived.Output.Start + selected.Quantity)));
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

    private static CraftPlan Prepare(CountedAuthorityState inventory, ProductionSnapshot production, EntityId actorId,
        EntityId commandId, CraftRequest request, CraftRecipeDefinition recipe, IReadOnlyList<RetiredIngredient> retired,
        EntityId[] created)
    {
        var selected = retired.ToDictionary(value => value.LotId);
        List<CountedSourceLot> rawLots = [];
        foreach (CountedSourceLot lot in inventory.Custody.Lots)
        {
            if (!selected.TryGetValue(lot.LotId, out RetiredIngredient? used))
            {
                rawLots.Add(lot);
            }
            else if (used.Quantity < lot.Quantity)
            {
                rawLots.Add(new CountedSourceLot(lot.LotId, lot.CustodianId, lot.Definition,
                    new SourceInterval(lot.Source.SourceId, used.End, lot.Source.End), checked(lot.Revision + 1),
                    lot.Protection, lot.ReservedBy));
            }
        }
        List<TransformedLot> derivedLots = [];
        foreach (TransformedLot lot in production.Lots)
        {
            if (!selected.TryGetValue(lot.LotId, out RetiredIngredient? used))
            {
                derivedLots.Add(lot);
            }
            else if (used.Quantity < lot.Quantity)
            {
                derivedLots.Add(new TransformedLot(lot.LotId, lot.CustodianId, lot.Definition,
                    new ProductionOutputInterval(lot.Output.BatchId, lot.Output.OutputIndex, used.End, lot.Output.End),
                    checked(lot.Revision + 1), lot.Protection, lot.ReservedBy));
            }
        }
        if (rawLots.Count + derivedLots.Count + recipe.Outputs.Count > ProductionSnapshot.MaximumLots)
        {
            return new CraftPlan(inventory, production, WorldActionRejection.Unavailable, []);
        }
        var batch = new ProductionBatch(created[0], commandId, actorId, request.CustodianId,
            recipe.DefinitionId, recipe.Version, retired);
        for (int index = 0; index < recipe.Outputs.Count; index++)
        {
            RecipeTerm output = recipe.Outputs[index];
            derivedLots.Add(new TransformedLot(created[index + 1], request.CustodianId, recipe.ItemFor(output),
                new ProductionOutputInterval(batch.BatchId, (uint)index, 0, output.Count), 0));
        }
        var custody = new CustodySnapshot(inventory.WorldId, inventory.BranchId, checked(inventory.Custody.Revision + 1),
            inventory.Custody.Storages.Select(storage => storage.CustodianId == request.CustodianId
                ? storage with { Revision = checked(storage.Revision + 1) } : storage), rawLots);
        CountedAuthorityState candidateInventory = inventory.WithCustody(custody);
        var candidateProduction = new ProductionSnapshot(production.WorldId, production.BranchId, checked(production.Revision + 1),
            production.Recipes, [.. production.Batches, batch], derivedLots);
        candidateProduction.ValidateForAuthority(candidateInventory);
        return new CraftPlan(candidateInventory, candidateProduction, WorldActionRejection.None, created);
    }
}
