using System.Collections.ObjectModel;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Committed local occupancy; outside these known bounds remains unknown.</summary>
public sealed class LocalLiquidTerrainView
{
    internal LocalLiquidTerrainView(TerrainRegionSnapshot region)
    {
        Bounds = region.Bounds;
        Revision = region.Revision;
        Occupancy = Array.AsReadOnly(region.Cells.Select(cell => cell.IsSolid).ToArray());
    }

    public TerrainRegionBounds Bounds { get; }
    public ulong Revision { get; }
    public ReadOnlyCollection<bool> Occupancy { get; }
    public bool IsSolid(CellCoordinate cell) => Occupancy[Bounds.IndexOf(cell)];
}
