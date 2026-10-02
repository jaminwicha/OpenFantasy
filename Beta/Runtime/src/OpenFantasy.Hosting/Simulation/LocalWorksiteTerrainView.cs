using System.Collections.ObjectModel;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Complete committed occupancy inside these bounds. Outside cells remain unknown, not empty.</summary>
public sealed class LocalWorksiteTerrainView
{
    /// <summary>Rehydrates immutable presentation values without consulting or constructing authority state.</summary>
    internal LocalWorksiteTerrainView(TerrainRegionBounds bounds,
        ulong revision,
        ReadOnlyCollection<bool> occupancy)
    {
        Bounds = bounds;
        Revision = revision;
        Occupancy = new LocalTerrainOccupancy(occupancy);
    }

    internal LocalWorksiteTerrainView(TerrainRegionSnapshot region)
    {
        Bounds = region.Bounds;
        Revision = region.Revision;
        Occupancy = new LocalTerrainOccupancy(region.Cells.Select(cell => cell.IsSolid));
    }

    public TerrainRegionBounds Bounds { get; }
    public ulong Revision { get; }

    /// <summary>Solid flags in Bounds.CoordinateAt(index) order, including known empty cells.</summary>
    public ReadOnlyCollection<bool> Occupancy { get; }

    /// <summary>Returns committed occupancy; an out-of-bounds coordinate is rejected.</summary>
    public bool IsSolid(CellCoordinate cell) => Occupancy[Bounds.IndexOf(cell)];
}
