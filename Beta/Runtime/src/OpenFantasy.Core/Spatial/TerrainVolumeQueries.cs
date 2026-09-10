using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Spatial;

/// <summary>Reads complete bounded full-cube volume and footprint dependencies, including region borders.</summary>
internal sealed class TerrainVolumeQueries(IReadOnlyList<TerrainRegionSnapshot> regions)
{
    internal TerrainCell? FindCell(CellCoordinate coordinate)
    {
        foreach (TerrainRegionSnapshot region in regions)
        {
            if (region.Bounds.Contains(coordinate))
            {
                return region.GetCell(coordinate);
            }
        }
        return null;
    }

    internal SpatialOccupancy Query(BodyVolume volume, bool requireGroundSupport = true)
    {
        CellCoordinate minimum = volume.MinimumCell;
        CellCoordinate maximum = volume.MaximumCell;
        bool missing = false;
        bool blocked = false;
        bool supported = volume.MinimumZ % SubunitPosition.SubunitsPerCell == 0;
        for (long z = minimum.Z; z <= maximum.Z; z++)
        {
            for (long y = minimum.Y; y <= maximum.Y; y++)
            {
                for (long x = minimum.X; x <= maximum.X; x++)
                {
                    TerrainCell? cell = FindCell(new CellCoordinate(x, y, z));
                    missing |= cell is null;
                    blocked |= cell?.IsSolid == true;
                }
            }
        }
        // All admitted subunit coordinates divide by 256, so subtracting one cell cannot overflow.
        long supportZ = minimum.Z - 1;
        if (requireGroundSupport)
        {
            for (long y = minimum.Y; y <= maximum.Y; y++)
            {
                for (long x = minimum.X; x <= maximum.X; x++)
                {
                    TerrainCell? support = FindCell(new CellCoordinate(x, y, supportZ));
                    missing |= support is null;
                    supported &= support?.IsSolid == true;
                }
            }
        }
        if (missing)
        {
            return SpatialOccupancy.Unavailable;
        }
        if (blocked)
        {
            return SpatialOccupancy.BlockedTerrain;
        }
        return !requireGroundSupport || supported ? SpatialOccupancy.Clear : SpatialOccupancy.Unsupported;
    }
}
