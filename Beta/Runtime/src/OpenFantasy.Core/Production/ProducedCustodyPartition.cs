using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Production;

/// <summary>
/// Replays every imported or locally produced lot through its retirements and its recorded custody
/// movements in lot-revision order. The live lots must equal the replayed partition exactly, so a
/// balanced edit without matching ordered evidence is refused. Producer batch, output index and
/// interval never change; only the custodian and the prefix cursor move.
/// </summary>
internal sealed class ProducedCustodyPartition
{
    private readonly ProductionSnapshot _state;
    private readonly Dictionary<EntityId, LotReplay> _lots = [];
    private readonly Dictionary<EntityId, Queue<(EntityId CustodianId, RetiredIngredient Ingredient)>> _retirements = [];

    private ProducedCustodyPartition(ProductionSnapshot state)
    {
        _state = state;
        IEnumerable<TransformedLot> imported = state.Continuation?.Basis.TransformedLots.AsEnumerable()
            ?? Array.Empty<TransformedLot>();
        foreach (TransformedLot lot in imported)
        {
            Add(new LotReplay(lot.LotId, lot.CustodianId, lot.Definition, lot.Output.BatchId, lot.Output.OutputIndex,
                lot.Output.Start, lot.Output.End, lot.Revision, lot.Protection, lot.ReservedBy));
        }
        Dictionary<EntityId, TransformedLot> live = state.Lots.ToDictionary(value => value.LotId);
        foreach (ProductionBatch batch in state.Batches)
        {
            CraftRecipeDefinition recipe = state.GetRecipe(batch.RecipeId);
            for (int index = 0; index < recipe.Outputs.Count; index++)
            {
                EntityId lotId = EntityId.DeriveSpawned(batch.CommandId, checked((ulong)index + 1));
                // The construction owner validates the exact reservation of its installed output.
                // Before the first cut no context names the construction recipes, as in ordinary admission.
                bool mayInstall = state.Continuation is not { } context || context.ConstructionRecipeIds.Contains(batch.RecipeId);
                EntityId? reservedBy = mayInstall && live.TryGetValue(lotId, out TransformedLot? installed)
                    ? installed.ReservedBy : null;
                Add(new LotReplay(lotId, batch.CustodianId, recipe.ItemFor(recipe.Outputs[index]), batch.BatchId,
                    (uint)index, 0, recipe.Outputs[index].Count, 0, LotProtection.None, reservedBy));
            }
        }
        foreach (var group in ProductionRetirementHistory.CurrentRetirements(state)
            .Where(value => value.Ingredient.Output is not null).GroupBy(value => value.Ingredient.LotId))
        {
            _retirements[group.Key] = new(group.OrderBy(value => value.Ingredient.Revision));
        }
    }

    internal static void Validate(ProductionSnapshot state, IEnumerable<ProducedCustodyMovement> movements)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(movements);
        if (state.MaterialHistory is not null)
        {
            throw new ArgumentException("Ordered material history validates custody through its own operation replay.", nameof(state));
        }
        var partition = new ProducedCustodyPartition(state);
        foreach (ProducedCustodyMovement movement in movements)
        {
            ArgumentNullException.ThrowIfNull(movement);
            partition.Move(movement);
        }
        partition.Close();
    }

    private void Add(LotReplay lot)
    {
        if (!_lots.TryAdd(lot.LotId, lot))
        {
            throw new ArgumentException("Produced lots and split children require distinct permanent identities.");
        }
    }

    private void Move(ProducedCustodyMovement movement)
    {
        if (!_lots.TryGetValue(movement.LotId, out LotReplay? lot))
        {
            throw new ArgumentException("Custody evidence must name an actual imported, produced or moved lot.");
        }
        RetireUntil(lot, movement.LotRevision);
        if (movement.ExpectedDefinition is { } definition && definition != lot.Definition)
        {
            throw new ArgumentException("Custody evidence must retain the actual item definition and version.");
        }
        if (lot.Revision != movement.LotRevision || lot.CustodianId != movement.SourceCustodianId
            || lot.ReservedBy is not null || (lot.Protection & LotProtection.Equipped) != 0
            || movement.Quantity > lot.Remaining || movement.IsSplit == (movement.Quantity == lot.Remaining))
        {
            throw new ArgumentException("Custody evidence must move available unreserved stock from its actual custodian at the approved lot revision.");
        }
        var actualOutput = new ProductionOutputInterval(lot.BatchId, lot.OutputIndex, lot.Cursor,
            checked(lot.Cursor + movement.Quantity));
        if (movement.ExpectedOutput is { } expected && expected != actualOutput)
        {
            throw new ArgumentException("Custody evidence must retain the actual producer and exact moved prefix.");
        }
        if (movement.IsSplit)
        {
            ulong boundary = checked(lot.Cursor + movement.Quantity);
            Add(new LotReplay(movement.MovedLotId, movement.DestinationCustodianId, lot.Definition, lot.BatchId,
                lot.OutputIndex, lot.Cursor, boundary, 0, lot.Protection, null));
            lot.Cursor = boundary;
        }
        else
        {
            lot.CustodianId = movement.DestinationCustodianId;
        }
        lot.Revision = checked(lot.Revision + 1);
    }

    private void RetireUntil(LotReplay lot, ulong? revision)
    {
        if (!_retirements.TryGetValue(lot.LotId, out var pending))
        {
            return;
        }
        while (pending.Count != 0 && (revision is null || pending.Peek().Ingredient.Revision < revision))
        {
            var (custodianId, ingredient) = pending.Dequeue();
            ProductionOutputInterval output = ingredient.Output!;
            if (ingredient.Revision != lot.Revision || custodianId != lot.CustodianId
                || ingredient.Definition != lot.Definition || output.BatchId != lot.BatchId
                || output.OutputIndex != lot.OutputIndex || output.Start != lot.Cursor || output.End > lot.End
                || lot.Protection != LotProtection.None || lot.ReservedBy is not null)
            {
                throw new ArgumentException("Retirement must spend the exact available prefix held by its actual custodian at that lot revision.");
            }
            lot.Cursor = output.End;
            lot.Revision = checked(lot.Revision + 1);
        }
    }

    private void Close()
    {
        foreach (EntityId retired in _retirements.Keys)
        {
            if (!_lots.ContainsKey(retired))
            {
                throw new ArgumentException("Retirement must spend an actual imported, produced or moved lot.");
            }
        }
        Dictionary<EntityId, TransformedLot> live = _state.Lots.ToDictionary(value => value.LotId);
        foreach (LotReplay lot in _lots.Values)
        {
            RetireUntil(lot, null);
            live.Remove(lot.LotId, out TransformedLot? remainder);
            if (lot.Remaining == 0 ? remainder is not null : remainder is null || !lot.Matches(remainder))
            {
                throw new ArgumentException("Live produced lots must equal the partition replayed from retirements and ordered custody evidence.");
            }
        }
        if (live.Count != 0)
        {
            throw new ArgumentException("Live produced identity must be imported, actually created or moved by ordered custody evidence.");
        }
    }

    private sealed class LotReplay(EntityId lotId, EntityId custodianId, CountedItemDefinition definition,
        EntityId batchId, uint outputIndex, ulong cursor, ulong end, ulong revision, LotProtection protection,
        EntityId? reservedBy)
    {
        internal EntityId LotId { get; } = lotId;
        internal EntityId CustodianId { get; set; } = custodianId;
        internal CountedItemDefinition Definition { get; } = definition;
        internal EntityId BatchId { get; } = batchId;
        internal uint OutputIndex { get; } = outputIndex;
        internal ulong Cursor { get; set; } = cursor;
        internal ulong End { get; } = end;
        internal ulong Revision { get; set; } = revision;
        internal LotProtection Protection { get; } = protection;
        internal EntityId? ReservedBy { get; } = reservedBy;
        internal ulong Remaining => End - Cursor;

        internal bool Matches(TransformedLot lot) => lot.CustodianId == CustodianId && lot.Definition == Definition
            && lot.Output.BatchId == BatchId && lot.Output.OutputIndex == OutputIndex && lot.Output.Start == Cursor
            && lot.Output.End == End && lot.Revision == Revision && lot.Protection == Protection
            && lot.ReservedBy == ReservedBy;
    }
}
