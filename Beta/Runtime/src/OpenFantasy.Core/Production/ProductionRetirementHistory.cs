namespace OpenFantasy.Core.Production;

/// <summary>Direct material retirements and append-only evidence shared by composed material owners.</summary>
internal static class ProductionRetirementHistory
{
    // A consumed produced unit retires an output interval. Its raw ancestry is already present in
    // the producing batches; expanding that ancestry here would count the source material twice.
    internal static IEnumerable<(EntityId CustodianId, RetiredIngredient Ingredient)> Retirements(ProductionSnapshot state)
    {
        if (state.IsContinued)
            throw new ArgumentException("Complete retirement records require the archived segments of a continued owner.", nameof(state));
        return CurrentRetirements(state);
    }

    internal static IEnumerable<(EntityId CustodianId, RetiredIngredient Ingredient)> CurrentRetirements(ProductionSnapshot state)
    {
        foreach (var batch in state.Batches)
        {
            foreach (var ingredient in batch.Ingredients)
            {
                yield return (batch.CustodianId, ingredient);
            }
        }
        foreach (var claim in state.ConsumedItems)
        {
            yield return (claim.CustodianId, claim.Ingredient);
        }
    }

    internal static void ValidateSuccessor(ProductionSnapshot prior, ProductionSnapshot next)
    {
        if (prior.ContentHash == next.ContentHash)
        {
            return;
        }
        // The consumption profile selects the archive format and hash of every composed owner. Ordinary
        // reconciliation can neither drop it (erasing retained claims) nor acquire it (silently
        // re-versioning a legacy archive); a profile change is an explicit composition, not a successor.
        if (next.Revision <= prior.Revision || next.WorldId != prior.WorldId || next.BranchId != prior.BranchId
            || prior.Continuation?.ContentHash != next.Continuation?.ContentHash
            || prior.SupportsConsumption != next.SupportsConsumption
            || next.Recipes.Count != prior.Recipes.Count
            || prior.Recipes.Any(recipe => !next.Recipes.Any(r => r.DefinitionId == recipe.DefinitionId && r.Version == recipe.Version)))
        {
            throw new ArgumentException("Production reconciliation requires advancing state, an unchanged consumption profile and unchanged recipe admission.");
        }
        if (prior.MaterialHistory is { } history)
        {
            if (next.MaterialHistory is not { } retained || retained.Operations.Count < history.Operations.Count
                || history.Operations.Zip(retained.Operations).Any(pair => !MaterialOperationCodec.Encode(pair.First).AsSpan()
                    .SequenceEqual(MaterialOperationCodec.Encode(pair.Second))))
                throw new ArgumentException("Material reconciliation cannot remove, reorder or rewrite its retained typed operations.");
        }
        Dictionary<EntityId, ProductionBatch> batches = next.Batches.ToDictionary(b => b.BatchId);
        foreach (var batch in prior.Batches)
        {
            if (!batches.TryGetValue(batch.BatchId, out var retained)
                || !ProductionCodecV1.EncodeBatch(batch).AsSpan().SequenceEqual(ProductionCodecV1.EncodeBatch(retained)))
            {
                throw new ArgumentException("Production reconciliation cannot remove or rewrite retained batch history.");
            }
        }
        Dictionary<EntityId, ConsumedItemClaim> claims = next.ConsumedItems.ToDictionary(c => c.ConsumptionId);
        foreach (var claim in prior.ConsumedItems)
        {
            if (!claims.TryGetValue(claim.ConsumptionId, out var retained)
                || !ProductionCodecV2.EncodeClaim(claim).AsSpan().SequenceEqual(ProductionCodecV2.EncodeClaim(retained)))
            {
                throw new ArgumentException("Production reconciliation cannot remove or rewrite retained consumption history.");
            }
        }
    }
}
