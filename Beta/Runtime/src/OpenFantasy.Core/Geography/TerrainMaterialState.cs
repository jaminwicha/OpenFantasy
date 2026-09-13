using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>Exclusive coarse/detail representation over existing counted custody. No second quantity ledger or source mint.</summary>
public sealed class TerrainMaterialState
{
    public const int MaximumActiveCells = 4096;
    private readonly HashSet<CellCoordinate> _activeCells;

    internal TerrainMaterialState(TerrainMaterialDeposit deposit, CountedAuthorityState inventory,
        IEnumerable<CellCoordinate> activeCells)
    {
        ArgumentNullException.ThrowIfNull(deposit);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(activeCells);
        Deposit = deposit;
        Inventory = inventory;
        _activeCells = [];
        foreach (CellCoordinate cell in activeCells)
        {
            if (_activeCells.Count == MaximumActiveCells || !_activeCells.Add(cell))
            {
                throw new ArgumentException("Detailed cells must be unique and bounded.", nameof(activeCells));
            }
            _ = deposit.IntervalFor(cell);
        }
        ActiveCells = _activeCells.Order().ToList().AsReadOnly();
        ValidateBindings();
        (CoarseQuantity, DetailedQuantity, OtherQuantity) = ValidateCoverage();
    }

    public TerrainMaterialDeposit Deposit { get; }
    public CountedAuthorityState Inventory { get; }
    public ReadOnlyCollection<CellCoordinate> ActiveCells { get; }
    public ulong Revision => Inventory.Custody.Revision;
    public ContentHash ContentHash => ContentHash.Compute(TerrainMaterialCodecV1.Encode(this));
    public ulong CoarseQuantity { get; }
    public ulong DetailedQuantity { get; }
    public ulong OtherQuantity { get; }

    public static TerrainMaterialState Create(TerrainMaterialDeposit deposit, CountedAuthorityState inventory)
    {
        var state = new TerrainMaterialState(deposit, inventory, []);
        if (state.CoarseQuantity != deposit.Source.Quantity)
        {
            throw new ArgumentException("Initial material allocation requires the entire source interval in coarse custody.");
        }
        return state;
    }

    /// <summary>Rebinds a separately validated custody result. Callers own authorization and durable publication.</summary>
    public TerrainMaterialState ReconcileInventory(CountedAuthorityState inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        if (inventory.ContentHash == Inventory.ContentHash)
        {
            return this;
        }
        if (inventory.Custody.Revision <= Revision || inventory.WorldId != Inventory.WorldId
            || inventory.BranchId != Inventory.BranchId
            || Inventory.WithCustody(inventory.Custody).ContentHash != inventory.ContentHash)
        {
            throw new ArgumentException("Reconciliation requires an advancing custody result with unchanged scope and admission.");
        }
        return new(Deposit, inventory, ActiveCells);
    }

    internal bool IsActive(CellCoordinate cell) => _activeCells.Contains(cell);

    private void ValidateBindings()
    {
        if (Inventory.Custody.Storages.Any(storage => storage.Revision > Revision)
            || Inventory.Custody.Lots.Any(lot => lot.Revision > Revision))
        {
            throw new ArgumentException("Material custody entity revisions cannot exceed the complete root revision.");
        }
        if (Deposit.Terrain.Scope.WorldId != Inventory.WorldId || Deposit.Terrain.Scope.BranchId != Inventory.BranchId)
        {
            throw new ArgumentException("Terrain material and custody must share world and branch.");
        }
        foreach (EntityId identity in Deposit.ReservedIdentities)
        {
            if (Inventory.ContainsIdentity(identity))
            {
                throw new ArgumentException("Material geography cannot alias custody identities.");
            }
        }
        CustodianAccessPolicy? coarse = Inventory.Policies.SingleOrDefault(p => p.CustodianId == Deposit.CoarseCustodianId);
        CustodianAccessPolicy? fine = Inventory.Policies.SingleOrDefault(p => p.CustodianId == Deposit.FineCustodianId);
        if (coarse is null || fine is null || coarse.OwnerActorId != fine.OwnerActorId)
        {
            throw new ArgumentException("Both material custodians require the same admitted owner.");
        }
        AdmittedCountedSource? source = Inventory.Sources.SingleOrDefault(s => s.SourceId == Deposit.Source.SourceId);
        if (source is null || source.Definition != Deposit.Material.Item || source.UnitCapacity < Deposit.Source.End)
        {
            throw new ArgumentException("The material interval requires an exact admitted source and item pin.");
        }
    }

    private (ulong Coarse, ulong Detailed, ulong Other) ValidateCoverage()
    {
        ulong cursor = Deposit.Source.Start;
        ulong coarse = 0, detailed = 0, other = 0;
        SourceInterval[] activeIntervals = ActiveCells.Select(Deposit.IntervalFor).ToArray();
        foreach (CountedSourceLot lot in Inventory.Custody.Lots.Where(l => l.Source.SourceId == Deposit.Source.SourceId)
            .OrderBy(l => l.Source.Start))
        {
            SourceInterval? part = Deposit.Intersect(lot.Source);
            if (part is null)
            {
                continue;
            }
            if (part.Start != cursor)
            {
                throw new ArgumentException("Every funded material unit must remain covered exactly once by current custody.");
            }
            cursor = part.End;
            if (lot.CustodianId == Deposit.CoarseCustodianId)
            {
                if (activeIntervals.Any(a => a.Start < part.End && part.Start < a.End))
                {
                    throw new ArgumentException("Active terrain cannot also have a coarse material claim.");
                }
                coarse = checked(coarse + part.Quantity);
            }
            else if (lot.CustodianId == Deposit.FineCustodianId)
            {
                CellCoordinate cell = Deposit.CellForUnit(part.Start);
                if (!_activeCells.Contains(cell) || part != lot.Source || Deposit.IntervalFor(cell).End < part.End)
                {
                    throw new ArgumentException("Detailed lots must fit one active cell's canonical source interval.");
                }
                detailed = checked(detailed + part.Quantity);
            }
            else
            {
                other = checked(other + part.Quantity);
            }
        }
        if (cursor != Deposit.Source.End)
        {
            throw new ArgumentException("Missing or retired material needs a composed provenance contract; it cannot be regenerated.");
        }
        return (coarse, detailed, other);
    }
}
