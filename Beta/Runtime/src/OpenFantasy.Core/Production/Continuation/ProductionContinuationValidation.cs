using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Production;

/// <summary>Exact material-right accounting within a captured production segment.</summary>
internal static class ProductionContinuationValidation
{
    internal static void Validate(ProductionSnapshot state)
    {
        if (state.MaterialHistory is not null) { MaterialContinuationValidation.Validate(state); return; }
        ProductionContinuationContext context = state.Continuation!;
        ProductionContinuationBasis basis = context.Basis;
        ulong operations = checked((ulong)state.Batches.Count + (ulong)state.ConsumedItems.Count);
        if (state.WorldId != basis.WorldId || state.BranchId != basis.BranchId
            || state.Revision != checked(context.BaseProductionRevision + operations)
            || state.SupportsConsumption != context.SupportsConsumption
            || state.Recipes.Count != context.Recipes.Count
            || state.Recipes.Zip(context.Recipes).Any(pair => !ProductionCodecV1.EncodeRecipe(pair.First).AsSpan()
                .SequenceEqual(ProductionCodecV1.EncodeRecipe(pair.Second))))
            throw new ArgumentException("A continued segment requires exact scope, recipe admission, profile and cumulative operation revision.");
        _ = state.TotalBatchCount;
        _ = state.TotalConsumedItemCount;

        var priorBatchIds = basis.BatchWitnesses.Select(value => value.BatchId).ToHashSet();
        var priorCommands = basis.BatchWitnesses.Select(value => value.CommandId).ToHashSet();
        if (state.Batches.Any(value => priorBatchIds.Contains(value.BatchId) || priorCommands.Contains(value.CommandId))
            || state.ConsumedItems.Any(value => priorCommands.Contains(value.CommandId)))
            throw new ArgumentException("A segment cannot reuse an imported producer or its command identity.");

        bool completeWorld = context.SupportsCompleteWorld;
        Dictionary<EntityId, LotBoundary> boundaries = Boundaries(state);
        // Coordinated raw custody belongs to the world inventory and its exact issuance partition is
        // admitted by the complete world, so only transformed rights are frozen in this segment.
        var retirements = ProductionRetirementHistory.CurrentRetirements(state)
            .Where(value => !completeWorld || value.Ingredient.Source is null).ToArray();
        foreach (var retirement in retirements)
        {
            if (!boundaries.TryGetValue(retirement.Ingredient.LotId, out LotBoundary? boundary)
                || !boundary.Matches(retirement.Ingredient)
                || boundary.CustodianId != retirement.CustodianId
                || boundary.Protection != LotProtection.None || boundary.ReservedBy is not null)
                throw new ArgumentException("Retirement must spend its actual unprotected, unreserved lot with unchanged provenance and custody.");
        }
        var byLot = retirements.ToLookup(value => value.Ingredient.LotId, value => value.Ingredient);
        var live = state.Lots.ToDictionary(value => value.LotId);
        foreach (TransformedLot lot in state.Lots)
        {
            if (!boundaries.TryGetValue(lot.LotId, out LotBoundary? boundary) || boundary.Output is null)
                throw new ArgumentException("Live transformed identity must be imported or actually created by a local batch.");
        }
        foreach (LotBoundary boundary in boundaries.Values)
        {
            if (completeWorld && boundary.Source is not null) continue;
            (ulong cursor, ulong revision) = Remaining(boundary, byLot[boundary.LotId]);
            if (boundary.Source is not null) continue; // Its surviving suffix is owned by inventory.
            live.TryGetValue(boundary.LotId, out TransformedLot? remainder);
            // The construction owner validates the exact reservation of its installed output.
            LotBoundary expected = completeWorld && remainder is not null && IsLocalConstructionOutput(state, boundary)
                ? boundary with { ReservedBy = remainder.ReservedBy } : boundary;
            ValidateRemainder(expected, cursor, revision, remainder is null ? null : LotBoundary.From(remainder));
        }
    }

    private static bool IsLocalConstructionOutput(ProductionSnapshot state, LotBoundary boundary) =>
        boundary.Output is { } output
        && state.Batches.FirstOrDefault(value => value.BatchId == output.BatchId) is { } batch
        && state.Continuation!.ConstructionRecipeIds.Contains(batch.RecipeId);

    private static void ValidateWorldInventory(ProductionSnapshot state, CountedAuthorityState inventory)
    {
        ProductionContinuationContext context = state.Continuation!;
        ProductionContinuationBasis basis = context.Basis;
        ulong operations = checked((ulong)state.Batches.Count + (ulong)state.ConsumedItems.Count);
        // Transfers, gathering, delivery and extraction advance custody through their own owners, so
        // the craft and consumption total is a lower bound here and never a second operation counter.
        if (inventory.Custody.Revision < checked(basis.CustodyRevision + operations))
            throw new ArgumentException("Coordinated custody cannot precede its retained craft and consumption operations.");
        if (ProductionContinuationContext.AdmissionHash(inventory) != context.AuthorityAdmissionHash)
            throw new ArgumentException("Changing continued source, control or custody admission requires its integrated authority contract.");
        var operationsByCustodian = state.Batches.Select(value => value.CustodianId)
            .Concat(state.ConsumedItems.Select(value => value.CustodianId)).ToLookup(value => value);
        if (context.Storages.Count != inventory.Custody.Storages.Count)
            throw new ArgumentException("Continued material operations cannot silently replace storage admission.");
        var currentStorages = inventory.Custody.Storages.ToDictionary(value => value.CustodianId);
        foreach (CustodianStorage baseline in context.Storages)
        {
            if (!currentStorages.TryGetValue(baseline.CustodianId, out CustodianStorage? current)
                || current.UnitCapacity != baseline.UnitCapacity
                || current.Revision < checked(baseline.Revision + (ulong)operationsByCustodian[baseline.CustodianId].Count()))
                throw new ArgumentException("Every storage preserves its capacity and at least its local operation revisions.");
        }
        var custodians = inventory.Policies.Select(value => value.CustodianId).ToHashSet();
        if (basis.RawRetirements.Any(value => !custodians.Contains(value.CustodianId))
            || basis.BatchWitnesses.Any(value => !inventory.Actors.Contains(value.ActorId)
                || !custodians.Contains(value.CustodianId)))
            throw new ArgumentException("Imported live provenance retains its actual actor and custodian bindings.");
    }

    internal static void ValidateInventory(ProductionSnapshot state, CountedAuthorityState inventory)
    {
        if (state.MaterialHistory is not null) { MaterialContinuationValidation.ValidateInventory(state, inventory); return; }
        ProductionContinuationContext context = state.Continuation!;
        if (context.SupportsCompleteWorld) { ValidateWorldInventory(state, inventory); return; }
        ProductionContinuationBasis basis = context.Basis;
        ulong operations = checked((ulong)state.Batches.Count + (ulong)state.ConsumedItems.Count);
        if (inventory.Custody.Revision != checked(basis.CustodyRevision + operations))
            throw new ArgumentException("Continued craft and consumption retain the exact cumulative custody revision.");
        if (ProductionContinuationContext.AdmissionHash(inventory) != context.AuthorityAdmissionHash)
            throw new ArgumentException("Changing continued source, control or custody admission requires its integrated authority contract.");
        var operationsByCustodian = state.Batches.Select(value => value.CustodianId)
            .Concat(state.ConsumedItems.Select(value => value.CustodianId)).ToLookup(value => value);
        if (context.Storages.Count != inventory.Custody.Storages.Count)
            throw new ArgumentException("Continued material operations cannot silently replace storage admission.");
        var currentStorages = inventory.Custody.Storages.ToDictionary(value => value.CustodianId);
        foreach (CustodianStorage baseline in context.Storages)
        {
            if (!currentStorages.TryGetValue(baseline.CustodianId, out CustodianStorage? current)
                || current.UnitCapacity != baseline.UnitCapacity
                || current.Revision != checked(baseline.Revision + (ulong)operationsByCustodian[baseline.CustodianId].Count()))
                throw new ArgumentException("Every storage preserves its capacity and exact cumulative local operation revision.");
        }
        var custodians = inventory.Policies.Select(value => value.CustodianId).ToHashSet();
        if (basis.RawRetirements.Any(value => !custodians.Contains(value.CustodianId))
            || basis.BatchWitnesses.Any(value => !inventory.Actors.Contains(value.ActorId)
                || !custodians.Contains(value.CustodianId)))
            throw new ArgumentException("Imported live provenance retains its actual actor and custodian bindings.");
        var boundaries = basis.RawLots.ToDictionary(value => value.LotId, LotBoundary.From);
        var byLot = ProductionRetirementHistory.CurrentRetirements(state)
            .ToLookup(value => value.Ingredient.LotId, value => value.Ingredient);
        var live = inventory.Custody.Lots.ToDictionary(value => value.LotId);
        if (live.Keys.Any(value => !boundaries.ContainsKey(value)))
            throw new ArgumentException("New raw issuance or transfer requires a separately integrated source authority contract.");
        foreach (LotBoundary boundary in boundaries.Values)
        {
            (ulong cursor, ulong revision) = Remaining(boundary, byLot[boundary.LotId]);
            live.TryGetValue(boundary.LotId, out CountedSourceLot? remainder);
            ValidateRemainder(boundary, cursor, revision, remainder is null ? null : LotBoundary.From(remainder));
        }
    }

    private static Dictionary<EntityId, LotBoundary> Boundaries(ProductionSnapshot state)
    {
        ProductionContinuationBasis basis = state.Continuation!.Basis;
        var result = new Dictionary<EntityId, LotBoundary>();
        void Add(LotBoundary boundary)
        {
            if (!result.TryAdd(boundary.LotId, boundary))
                throw new ArgumentException("Local outputs cannot reuse imported or other created lot identities.");
        }
        foreach (CountedSourceLot lot in basis.RawLots) Add(LotBoundary.From(lot));
        foreach (TransformedLot lot in basis.TransformedLots) Add(LotBoundary.From(lot));
        foreach (ProductionBatch batch in state.Batches)
        {
            CraftRecipeDefinition recipe = state.GetRecipe(batch.RecipeId);
            for (int index = 0; index < recipe.Outputs.Count; index++)
            {
                RecipeTerm output = recipe.Outputs[index];
                Add(new(EntityId.DeriveSpawned(batch.CommandId, checked((ulong)index + 1)), batch.CustodianId,
                    recipe.ItemFor(output), null, new(batch.BatchId, (uint)index, 0, output.Count),
                    0, LotProtection.None, null));
            }
        }
        return result;
    }

    private static (ulong Cursor, ulong Revision) Remaining(LotBoundary boundary, IEnumerable<RetiredIngredient> retirements)
    {
        ulong cursor = boundary.Start;
        ulong revision = boundary.Revision;
        foreach (RetiredIngredient retirement in retirements.OrderBy(value => value.Revision))
        {
            if (!boundary.Matches(retirement) || retirement.Start != cursor || retirement.End > boundary.End
                || retirement.Revision != revision)
                throw new ArgumentException("A retained lot requires exact contiguous prefix spending and consecutive revisions.");
            cursor = retirement.End;
            revision = checked(revision + 1);
        }
        return (cursor, revision);
    }

    private static void ValidateRemainder(LotBoundary boundary, ulong cursor, ulong revision, LotBoundary? remainder)
    {
        if (cursor == boundary.End)
        {
            if (remainder is not null)
                throw new ArgumentException("A completely spent lot cannot remain live.");
            return;
        }
        if (remainder is null || remainder.Start != cursor || remainder.End != boundary.End
            || remainder.Revision != revision || remainder.CustodianId != boundary.CustodianId
            || remainder.Definition != boundary.Definition || remainder.Source?.SourceId != boundary.Source?.SourceId
            || remainder.Output?.BatchId != boundary.Output?.BatchId || remainder.Output?.OutputIndex != boundary.Output?.OutputIndex
            || remainder.Protection != boundary.Protection || remainder.ReservedBy != boundary.ReservedBy)
            throw new ArgumentException("A remaining lot must preserve exact rights, identity, custody, revision and protection.");
    }

    private sealed record LotBoundary(EntityId LotId, EntityId CustodianId, CountedItemDefinition Definition,
        SourceInterval? Source, ProductionOutputInterval? Output, ulong Revision, LotProtection Protection, EntityId? ReservedBy)
    {
        internal ulong Start => Source?.Start ?? Output!.Start;
        internal ulong End => Source?.End ?? Output!.End;
        internal static LotBoundary From(CountedSourceLot lot) => new(lot.LotId, lot.CustodianId, lot.Definition,
            lot.Source, null, lot.Revision, lot.Protection, lot.ReservedBy);
        internal static LotBoundary From(TransformedLot lot) => new(lot.LotId, lot.CustodianId, lot.Definition,
            null, lot.Output, lot.Revision, lot.Protection, lot.ReservedBy);
        internal bool Matches(RetiredIngredient ingredient) => Definition == ingredient.Definition
            && Source?.SourceId == ingredient.Source?.SourceId && Output?.BatchId == ingredient.Output?.BatchId
            && Output?.OutputIndex == ingredient.Output?.OutputIndex;
    }
}
