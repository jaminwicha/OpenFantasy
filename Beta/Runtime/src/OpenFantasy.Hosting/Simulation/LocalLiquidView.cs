using System.Collections.ObjectModel;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Actual occupied cell geometry, without reservoir identities or material lineage.</summary>
public sealed record LocalLiquidCellView(CellCoordinate Coordinate, ulong VolumeMicrolitres);

/// <summary>
/// Committed liquid and terrain geometry in this admitted sight-complete locality. This is neither
/// an equilibrium surface nor a global visibility, immersion, water-use or replenishment grant.
/// </summary>
public sealed class LocalLiquidView
{
    public const ulong CellCapacityMicrolitres = 1_000_000_000UL;

    internal LocalLiquidView(WorldSnapshot state)
    {
        var liquid = state.LiquidCells
            ?? throw new ArgumentException("A liquid view requires retained detailed placement.", nameof(state));
        var amounts = new SortedDictionary<CellCoordinate, ulong>();
        foreach (var entry in liquid.Quantities)
        {
            ulong volume = checked(entry.Value * liquid.VolumePerUnit.Value);
            ulong total = checked(amounts.GetValueOrDefault(entry.Key.Coordinate) + volume);
            if (total > CellCapacityMicrolitres)
            {
                throw new ArgumentException("Overlapping liquid identities exceed the cell's geometric capacity.", nameof(state));
            }
            if (total > 0)
            {
                amounts[entry.Key.Coordinate] = total;
            }
        }
        Cells = Array.AsReadOnly(amounts.Select(entry => new LocalLiquidCellView(entry.Key, entry.Value)).ToArray());
        Terrain = Array.AsReadOnly(state.Space.Regions.Select(region => new LocalLiquidTerrainView(region)).ToArray());
    }

    public ReadOnlyCollection<LocalLiquidCellView> Cells { get; }
    public ReadOnlyCollection<LocalLiquidTerrainView> Terrain { get; }
}
