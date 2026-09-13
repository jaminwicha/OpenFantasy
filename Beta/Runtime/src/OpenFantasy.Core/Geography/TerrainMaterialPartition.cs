using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>Builds a whole interval partition before publishing any mutation; prefix-transfer commands are not synthetic intermediate commits.</summary>
internal sealed class TerrainMaterialPartition
{
    private readonly TerrainMaterialState _state;
    private readonly CellCoordinate[] _cells;
    private readonly EntityId _causeId;
    private readonly Func<EntityId, bool>? _historicallyOccupied;
    private readonly ulong _revision;

    internal TerrainMaterialPartition(TerrainMaterialState state, CellCoordinate[] cells, EntityId causeId,
        ulong expectedRevision, Func<EntityId, bool>? historicallyOccupied)
    {
        _state = state;
        _cells = cells;
        _causeId = causeId;
        _historicallyOccupied = historicallyOccupied;
        if (expectedRevision != state.Revision)
        {
            throw new InvalidOperationException("The material revision is stale.");
        }
        if (IsOccupied(causeId))
        {
            throw new InvalidOperationException("The accepted cause identity is already occupied.");
        }
        _revision = checked(state.Revision + 1);
    }

    internal TerrainMaterialPlan Build(bool refining)
    {
        TerrainMaterialDeposit deposit = _state.Deposit;
        EntityId from = refining ? deposit.CoarseCustodianId : deposit.FineCustodianId;
        EntityId to = refining ? deposit.FineCustodianId : deposit.CoarseCustodianId;
        SourceInterval[] intervals = _cells.Select(deposit.IntervalFor).ToArray();
        ulong[] moved = new ulong[intervals.Length];
        List<Part> parts = [];
        foreach (CountedSourceLot lot in _state.Inventory.Custody.Lots)
        {
            if (lot.CustodianId != from || lot.Source.SourceId != deposit.Source.SourceId)
            {
                parts.Add(new(lot, lot.Source, lot.CustodianId, false));
                continue;
            }
            SplitLot(lot, intervals, to, moved, parts);
        }
        if (refining && moved.Any(quantity => quantity == 0))
        {
            throw new InvalidOperationException("Each requested cell must still have some coarse-owned material to refine.");
        }
        if (!refining)
        {
            parts = CoalesceCoarse(parts);
        }
        CountedSourceLot[] lots = Materialize(parts);
        CustodianStorage[] storages = _state.Inventory.Custody.Storages.Select(storage =>
            storage.CustodianId == from || storage.CustodianId == to
                ? storage with { Revision = checked(storage.Revision + 1) } : storage).ToArray();
        var custody = new CustodySnapshot(_state.Inventory.WorldId, _state.Inventory.BranchId, _revision, storages, lots);
        IEnumerable<CellCoordinate> active = refining ? _state.ActiveCells.Concat(_cells) : _state.ActiveCells.Except(_cells);
        var candidate = new TerrainMaterialState(deposit, _state.Inventory.WithCustody(custody), active);
        return new(_state, candidate);
    }

    private static void SplitLot(CountedSourceLot lot, SourceInterval[] intervals, EntityId destination,
        ulong[] moved, List<Part> parts)
    {
        bool affected = intervals.Any(i => i.Start < lot.Source.End && lot.Source.Start < i.End);
        if (!affected)
        {
            parts.Add(new(lot, lot.Source, lot.CustodianId, false));
            return;
        }
        if (lot.Protection != LotProtection.None || lot.ReservedBy is not null)
        {
            throw new InvalidOperationException("Protected or reserved material cannot change representation.");
        }
        ulong cursor = lot.Source.Start;
        for (int i = 0; i < intervals.Length; i++)
        {
            ulong start = Math.Max(lot.Source.Start, intervals[i].Start);
            ulong end = Math.Min(lot.Source.End, intervals[i].End);
            if (start >= end)
            {
                continue;
            }
            if (cursor < start)
            {
                parts.Add(new(lot, new(lot.Source.SourceId, cursor, start), lot.CustodianId, true));
            }
            parts.Add(new(lot, new(lot.Source.SourceId, start, end), destination, true));
            moved[i] = checked(moved[i] + end - start);
            cursor = end;
        }
        if (cursor < lot.Source.End)
        {
            parts.Add(new(lot, new(lot.Source.SourceId, cursor, lot.Source.End), lot.CustodianId, true));
        }
    }

    private List<Part> CoalesceCoarse(List<Part> parts)
    {
        List<Part> result = [];
        foreach (Part part in Ordered(parts))
        {
            if (result.Count > 0 && CanCoalesce(result[^1], part))
            {
                Part previous = result[^1];
                result[^1] = previous with { Source = new(previous.Source.SourceId, previous.Source.Start, part.Source.End), Changed = true };
            }
            else
            {
                result.Add(part);
            }
        }
        return result;
    }

    private bool CanCoalesce(Part first, Part second)
    {
        TerrainMaterialDeposit deposit = _state.Deposit;
        return first.CustodianId == deposit.CoarseCustodianId && second.CustodianId == first.CustodianId
            && first.Source.SourceId == deposit.Source.SourceId && second.Source.SourceId == first.Source.SourceId
            && first.Source.End == second.Source.Start && first.Source.Start >= deposit.Source.Start && second.Source.End <= deposit.Source.End
            && first.Original.Definition == second.Original.Definition
            && first.Original.Protection == LotProtection.None && second.Original.Protection == LotProtection.None
            && first.Original.ReservedBy is null && second.Original.ReservedBy is null;
    }

    private CountedSourceLot[] Materialize(List<Part> parts)
    {
        if (parts.Count > CustodySnapshot.MaximumLots)
        {
            throw new ArgumentException("Material refinement exceeds the existing lot limit.");
        }
        List<CountedSourceLot> lots = [];
        HashSet<EntityId> spawned = [];
        ulong ordinal = 0;
        foreach (Part part in Ordered(parts))
        {
            if (!part.Changed)
            {
                lots.Add(part.Original);
                continue;
            }
            EntityId identity = EntityId.DeriveSpawned(_causeId, ordinal++);
            if (identity == _causeId || IsOccupied(identity) || !spawned.Add(identity))
            {
                throw new InvalidOperationException("A material lot identity collides with a current or historical role.");
            }
            lots.Add(new(identity, part.CustodianId, part.Original.Definition, part.Source, _revision,
                part.Original.Protection, part.Original.ReservedBy));
        }
        return lots.ToArray();
    }

    private bool IsOccupied(EntityId identity) => _state.Inventory.ContainsIdentity(identity)
        || _state.Deposit.ReservesIdentity(identity) || (_historicallyOccupied?.Invoke(identity) ?? false);

    private static IOrderedEnumerable<Part> Ordered(IEnumerable<Part> parts) =>
        parts.OrderBy(p => p.Source.SourceId).ThenBy(p => p.Source.Start).ThenBy(p => p.CustodianId);

    private sealed record Part(CountedSourceLot Original, SourceInterval Source, EntityId CustodianId, bool Changed);
}
