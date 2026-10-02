using System.Collections.ObjectModel;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Committed local occupancy; outside these known bounds remains unknown.</summary>
public sealed class LocalLiquidTerrainView
{
    /// <summary>Rehydrates immutable presentation values without consulting or constructing authority state.</summary>
    internal LocalLiquidTerrainView(TerrainRegionBounds bounds,
        ulong revision,
        ReadOnlyCollection<bool> occupancy)
    {
        Bounds = bounds;
        Revision = revision;
        Occupancy = new LocalTerrainOccupancy(occupancy);
    }

    internal LocalLiquidTerrainView(TerrainRegionSnapshot region)
    {
        Bounds = region.Bounds;
        Revision = region.Revision;
        Occupancy = new LocalTerrainOccupancy(region.Cells.Select(cell => cell.IsSolid));
    }

    public TerrainRegionBounds Bounds { get; }
    public ulong Revision { get; }
    public ReadOnlyCollection<bool> Occupancy { get; }
    public bool IsSolid(CellCoordinate cell) => Occupancy[Bounds.IndexOf(cell)];
}
