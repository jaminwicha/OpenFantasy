using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Production;

internal static class ProductionClaimValidation
{
    internal static void Validate(ProductionSnapshot state)
    {
        var batches = state.Batches.ToDictionary(batch => batch.BatchId);
        var dependencies = new Dictionary<EntityId, HashSet<EntityId>>();
        var outputClaims = new Dictionary<(EntityId Batch, uint Index), List<(ulong Start, ulong End)>>();
        foreach (ProductionBatch batch in state.Batches)
        {
            CraftRecipeDefinition recipe = state.GetRecipe(batch.RecipeId);
            if (recipe.Version != batch.RecipeVersion)
            {
                throw new ArgumentException("A production batch requires its exact admitted recipe version.");
            }
            ValidateIngredientAmounts(recipe, batch.Ingredients);
            dependencies[batch.BatchId] = [];
            foreach (RetiredIngredient ingredient in batch.Ingredients)
            {
                if (ingredient.Output is { } output)
                {
                    AddOutputClaim(state, batches, outputClaims, output, ingredient.Definition);
                    dependencies[batch.BatchId].Add(output.BatchId);
                }
            }
        }
        foreach (TransformedLot lot in state.Lots)
        {
            AddOutputClaim(state, batches, outputClaims, lot.Output, lot.Definition);
        }
        foreach (ConsumedItemClaim claim in state.ConsumedItems)
        {
            if (claim.Ingredient.Output is { } output)
                AddOutputClaim(state, batches, outputClaims, output, claim.Ingredient.Definition);
        }
        foreach (ProductionBatch batch in state.Batches)
        {
            CraftRecipeDefinition recipe = state.GetRecipe(batch.RecipeId);
            for (uint index = 0; index < recipe.Outputs.Count; index++)
            {
                if (!outputClaims.TryGetValue((batch.BatchId, index), out var claims))
                {
                    throw new ArgumentException("Every produced output budget must remain live or explicitly retired.");
                }
                ulong cursor = 0;
                foreach (var claim in claims.OrderBy(value => value.Start))
                {
                    if (claim.Start != cursor)
                    {
                        throw new ArgumentException("Produced claims cannot overlap or silently discard output units.");
                    }
                    cursor = claim.End;
                }
                if (cursor != recipe.Outputs[(int)index].Count)
                {
                    throw new ArgumentException("Produced claims must exactly account for their complete output budget.");
                }
            }
        }
        ValidateAcyclic(dependencies);
        RetiredIngredient[] retired = RetiredItems(state).ToArray();
        ValidateLotHistory(retired, state.Lots);
        ValidateDisjointRaw(retired
            .Where(value => value.Source is not null).Select(value => (value.Source!, value.Definition)));
    }

    internal static void ValidateIngredientAmounts(CraftRecipeDefinition recipe, IEnumerable<RetiredIngredient> ingredients)
    {
        Dictionary<CountedItemDefinition, UInt128> totals = [];
        foreach (RetiredIngredient ingredient in ingredients)
        {
            totals[ingredient.Definition] = totals.GetValueOrDefault(ingredient.Definition) + ingredient.Quantity;
        }
        if (totals.Count != recipe.Inputs.Count || recipe.Inputs.Any(term =>
            totals.GetValueOrDefault(recipe.ItemFor(term)) != term.Count))
        {
            throw new ArgumentException("Retired ingredients must exactly match every pinned input count.");
        }
    }

    internal static void ValidateSourceClaims(ProductionSnapshot state, CountedAuthorityState inventory)
    {
        RetiredIngredient[] retired = RetiredItems(state).ToArray();
        foreach (RetiredIngredient ingredient in retired.Where(value => value.Source is not null))
        {
            SourceInterval interval = ingredient.Source!;
            AdmittedCountedSource? source = inventory.Sources.FirstOrDefault(value => value.SourceId == interval.SourceId);
            if (source is null || source.Definition != ingredient.Definition || interval.End > source.UnitCapacity)
            {
                throw new ArgumentException("Retired raw rights must fit an exact finite admitted source.");
            }
        }
        ValidateDisjointRaw(retired.Where(value => value.Source is not null).Select(value => (value.Source!, value.Definition))
            .Concat(inventory.Custody.Lots.Select(lot => (lot.Source, lot.Definition))));
        ILookup<EntityId, RetiredIngredient> byLot = retired.ToLookup(value => value.LotId);
        foreach (CountedSourceLot lot in inventory.Custody.Lots)
        {
            foreach (RetiredIngredient prior in byLot[lot.LotId])
            {
                if (prior.Kind != IngredientLotKind.Source || prior.Definition != lot.Definition
                    || prior.Source!.SourceId != lot.Source.SourceId || prior.Revision >= lot.Revision
                    || prior.End > lot.Source.Start)
                {
                    throw new ArgumentException("A surviving raw remainder must follow its retired prefixes and revisions.");
                }
            }
        }
    }

    private static IEnumerable<RetiredIngredient> RetiredItems(ProductionSnapshot state) =>
        state.Batches.SelectMany(batch => batch.Ingredients).Concat(state.ConsumedItems.Select(claim => claim.Ingredient));

    private static void ValidateDisjointRaw(IEnumerable<(SourceInterval Interval, CountedItemDefinition Definition)> claims)
    {
        foreach (var source in claims.GroupBy(value => value.Interval.SourceId))
        {
            ulong end = 0;
            CountedItemDefinition? definition = null;
            foreach (var claim in source.OrderBy(value => value.Interval.Start))
            {
                if (claim.Interval.Start < end || definition is not null && definition != claim.Definition)
                {
                    throw new ArgumentException("Raw source units cannot remain live and retired or be consumed more than once.");
                }
                end = claim.Interval.End;
                definition = claim.Definition;
            }
        }
    }

    private static void AddOutputClaim(ProductionSnapshot state, Dictionary<EntityId, ProductionBatch> batches,
        Dictionary<(EntityId Batch, uint Index), List<(ulong Start, ulong End)>> claims,
        ProductionOutputInterval output, CountedItemDefinition item)
    {
        if (!batches.TryGetValue(output.BatchId, out ProductionBatch? batch))
        {
            throw new ArgumentException("A transformed claim cannot invent an absent production batch.");
        }
        CraftRecipeDefinition recipe = state.GetRecipe(batch.RecipeId);
        if (output.OutputIndex >= recipe.Outputs.Count || output.End > recipe.Outputs[(int)output.OutputIndex].Count
            || recipe.ItemFor(recipe.Outputs[(int)output.OutputIndex]) != item)
        {
            throw new ArgumentException("A transformed claim must fit its exact batch output definition and finite interval.");
        }
        var key = (output.BatchId, output.OutputIndex);
        if (!claims.TryGetValue(key, out var entries))
        {
            entries = [];
            claims[key] = entries;
        }
        entries.Add((output.Start, output.End));
    }

    private static void ValidateAcyclic(Dictionary<EntityId, HashSet<EntityId>> dependencies)
    {
        HashSet<EntityId> complete = [];
        while (complete.Count != dependencies.Count)
        {
            EntityId[] ready = dependencies.Where(pair => !complete.Contains(pair.Key)
                && pair.Value.All(complete.Contains)).Select(pair => pair.Key).ToArray();
            if (ready.Length == 0)
            {
                throw new ArgumentException("Production ingredient lineage cannot contain cycles.");
            }
            complete.UnionWith(ready);
        }
    }

    private static void ValidateLotHistory(IEnumerable<RetiredIngredient> ingredients, IEnumerable<TransformedLot> live)
    {
        Dictionary<EntityId, TransformedLot> liveById = live.ToDictionary(value => value.LotId);
        foreach (var lot in ingredients.GroupBy(value => value.LotId))
        {
            RetiredIngredient? previous = null;
            foreach (RetiredIngredient current in lot.OrderBy(value => value.Revision))
            {
                if (previous is not null && (current.Kind != previous.Kind || current.Definition != previous.Definition
                    || current.Revision == previous.Revision || current.Start < previous.End
                    || current.Source?.SourceId != previous.Source?.SourceId
                    || current.Output?.BatchId != previous.Output?.BatchId || current.Output?.OutputIndex != previous.Output?.OutputIndex))
                {
                    throw new ArgumentException("Retired prefixes require consistent lot lineage and strictly increasing revisions.");
                }
                previous = current;
            }
            liveById.TryGetValue(lot.Key, out TransformedLot? remainder);
            if (remainder is not null && (previous!.Kind != IngredientLotKind.Transformed
                || previous.Definition != remainder.Definition || previous.Revision >= remainder.Revision
                || previous.End > remainder.Output.Start || previous.Output!.BatchId != remainder.Output.BatchId
                || previous.Output.OutputIndex != remainder.Output.OutputIndex))
            {
                throw new ArgumentException("A surviving transformed remainder must follow its retired prefixes.");
            }
        }
    }
}
