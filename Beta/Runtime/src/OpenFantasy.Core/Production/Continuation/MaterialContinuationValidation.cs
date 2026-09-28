using OpenFantasy.Core.Authority;

namespace OpenFantasy.Core.Production;

internal static class MaterialContinuationValidation
{
    internal static void Validate(ProductionSnapshot state)
    {
        ProductionContinuationContext context = state.Continuation!;
        MaterialOperationHistory history = state.MaterialHistory!;
        if (state.WorldId != context.Basis.WorldId || state.BranchId != context.Basis.BranchId
            || state.Revision != checked(context.BaseProductionRevision + (ulong)history.Operations.Count)
            || state.SupportsConsumption != context.SupportsConsumption
            || state.Recipes.Count != context.Recipes.Count
            || state.Recipes.Zip(context.Recipes).Any(pair => !ProductionCodecV1.EncodeRecipe(pair.First).AsSpan()
                .SequenceEqual(ProductionCodecV1.EncodeRecipe(pair.Second))))
            throw new ArgumentException("Ordered material history requires exact scope, admission and absolute revision.");
        var batches = history.Operations.OfType<MaterialCraftOperation>().Select(value => value.Batch).OrderBy(value => value.BatchId).ToArray();
        var claims = history.Operations.OfType<MaterialConsumptionOperation>().Select(value => value.Claim).OrderBy(value => value.ConsumptionId).ToArray();
        if (batches.Length != state.Batches.Count || claims.Length != state.ConsumedItems.Count
            || batches.Zip(state.Batches).Any(pair => !ProductionCodecV1.EncodeBatch(pair.First).AsSpan().SequenceEqual(ProductionCodecV1.EncodeBatch(pair.Second)))
            || claims.Zip(state.ConsumedItems).Any(pair => !ProductionCodecV2.EncodeClaim(pair.First).AsSpan().SequenceEqual(ProductionCodecV2.EncodeClaim(pair.Second))))
            throw new ArgumentException("Every batch and consumption claim requires exactly one matching ordered material operation.");
        _ = state.TotalMaterialOperationCount;
        _ = state.TotalBatchCount;
        _ = state.TotalConsumedItemCount;
    }

    internal static void ValidateInventory(ProductionSnapshot state, CountedAuthorityState inventory)
    {
        if (ProductionContinuationContext.AdmissionHash(inventory) != state.Continuation!.AuthorityAdmissionHash)
            throw new ArgumentException("Material operations cannot replace source, actor, policy, reservation or storage admission.");
        new MaterialContinuationReplay(state, inventory).Validate();
    }
}
