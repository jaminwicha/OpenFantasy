using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>Trusted representation planning over funded custody; authorization, history and publication belong to the caller.</summary>
public sealed class TerrainMaterialPlanner
{
    public const int MaximumCellsPerPlan = 256;

    public TerrainMaterialPlan Refine(TerrainMaterialState state, PreparedWatershedTerrain window,
        IEnumerable<CellCoordinate> cells, EntityId causeId, ulong expectedRevision,
        Func<EntityId, bool>? historicallyOccupied = null)
    {
        CellCoordinate[] selected = SelectRefine(state, window, cells);
        return new TerrainMaterialPartition(state, selected, causeId, expectedRevision, historicallyOccupied).Build(refining: true);
    }

    internal static CellCoordinate[] SelectRefine(TerrainMaterialState state, PreparedWatershedTerrain window,
        IEnumerable<CellCoordinate> cells)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(window);
        if (window.Network.ComputeContentHash() != state.Deposit.NetworkHash
            || window.Mapping.Version != state.Deposit.Terrain.Mapping.Version)
        {
            throw new ArgumentException("Detail must use the exact admitted network and mapping.", nameof(window));
        }
        CellCoordinate[] selected = ReadCells(cells);
        foreach (CellCoordinate cell in selected)
        {
            if (!window.GetCell(cell).IsSubstrate || window.GetColumn(cell.X, cell.Y).MacroColumn != state.Deposit.MacroColumn)
            {
                throw new ArgumentException("Only substrate in the allocated macro parcel can acquire detailed material.");
            }
            _ = state.Deposit.IntervalFor(cell);
            if (state.IsActive(cell))
            {
                throw new InvalidOperationException("The cell is already detailed.");
            }
        }
        return selected;
    }

    public TerrainMaterialPlan Demote(TerrainMaterialState state, IEnumerable<CellCoordinate> cells,
        EntityId causeId, ulong expectedRevision, Func<EntityId, bool>? historicallyOccupied = null)
    {
        CellCoordinate[] selected = SelectDemote(state, cells);
        return new TerrainMaterialPartition(state, selected, causeId, expectedRevision, historicallyOccupied).Build(refining: false);
    }

    internal static CellCoordinate[] SelectDemote(TerrainMaterialState state, IEnumerable<CellCoordinate> cells)
    {
        ArgumentNullException.ThrowIfNull(state);
        CellCoordinate[] selected = ReadCells(cells);
        foreach (CellCoordinate cell in selected)
        {
            if (!state.IsActive(cell))
            {
                throw new InvalidOperationException("Only an active detail cell can be demoted.");
            }
        }
        return selected;
    }

    private static CellCoordinate[] ReadCells(IEnumerable<CellCoordinate> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        HashSet<CellCoordinate> selected = [];
        foreach (CellCoordinate cell in cells)
        {
            if (selected.Count == MaximumCellsPerPlan || !selected.Add(cell))
            {
                throw new ArgumentException("Material operations require unique cells within the finite batch limit.", nameof(cells));
            }
        }
        if (selected.Count == 0)
        {
            throw new ArgumentException("A material operation needs at least one cell.", nameof(cells));
        }
        return selected.Order().ToArray();
    }
}
