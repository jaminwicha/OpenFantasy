using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Spatial;

/// <summary>Exact union coverage of the complete swept footprint; never treats rounded collision area as material.</summary>
internal static class FootingQuery
{
    internal static SpatialOccupancy QueryStepped(TerrainVolumeQueries terrain,
        IReadOnlyList<SpatialStructure> structures, BodyVolume volume, long maximumDrop)
    {
        SpatialOccupancy coverage = QueryBand(terrain, structures, volume,
            checked(volume.MinimumZ - maximumDrop), volume.MinimumZ);
        if (coverage != SpatialOccupancy.Clear)
        {
            return coverage;
        }
        return CandidateHeights(terrain, structures, volume, volume.MinimumZ, volume.MinimumZ, out _).Count != 0
            ? SpatialOccupancy.Clear : SpatialOccupancy.Unsupported;
    }

    internal static SortedSet<long> CandidateHeights(TerrainVolumeQueries terrain,
        IReadOnlyList<SpatialStructure> structures, BodyVolume footprint, long minimumZ, long maximumZ,
        out bool missingTerrain)
    {
        missingTerrain = false;
        SortedSet<long> heights = [];
        const long ticks = StructureBearingSurface.TicksPerSubunit;
        long firstPlane = SubunitPosition.CellOf(minimumZ);
        if (minimumZ % SubunitPosition.SubunitsPerCell != 0) firstPlane++;
        long lastPlane = SubunitPosition.CellOf(maximumZ);
        for (long plane = firstPlane; plane <= lastPlane; plane++)
        {
            bool found = false;
            for (long y = footprint.MinimumCell.Y; y <= footprint.MaximumCell.Y; y++)
            {
                for (long x = footprint.MinimumCell.X; x <= footprint.MaximumCell.X; x++)
                {
                    TerrainCell? support = terrain.FindCell(new(x, y, plane - 1));
                    missingTerrain |= support is null;
                    found |= support?.IsSolid == true;
                }
            }
            if (found) heights.Add(checked(plane * SubunitPosition.SubunitsPerCell));
        }
        foreach (StructureBearingSurface surface in structures.SelectMany(value => value.BearingSurfaces))
        {
            if (surface.StandingZ >= minimumZ && surface.StandingZ <= maximumZ
                && surface.Bounds.MinU < (Int128)footprint.MaximumX * ticks
                && surface.Bounds.MaxU > (Int128)footprint.MinimumX * ticks
                && surface.Bounds.MinV < (Int128)footprint.MaximumY * ticks
                && surface.Bounds.MaxV > (Int128)footprint.MinimumY * ticks)
            {
                heights.Add(surface.StandingZ);
            }
        }
        return heights;
    }

    internal static SpatialOccupancy Query(TerrainVolumeQueries terrain,
        IReadOnlyList<SpatialStructure> structures, BodyVolume volume) =>
        QueryBand(terrain, structures, volume, volume.MinimumZ, volume.MinimumZ);

    internal static SpatialOccupancy QueryBand(TerrainVolumeQueries terrain,
        IReadOnlyList<SpatialStructure> structures, BodyVolume volume, long minimumStandingZ, long maximumStandingZ)
    {
        const long ticks = StructureBearingSurface.TicksPerSubunit;
        var footprint = new BoundaryRectangle((Int128)volume.MinimumX * ticks, (Int128)volume.MinimumY * ticks,
            (Int128)volume.MaximumX * ticks, (Int128)volume.MaximumY * ticks);
        List<BoundaryRectangle> patches = [];
        CellCoordinate minimum = volume.MinimumCell;
        CellCoordinate maximum = volume.MaximumCell;
        long firstPlane = SubunitPosition.CellOf(minimumStandingZ);
        if (minimumStandingZ % SubunitPosition.SubunitsPerCell != 0)
        {
            firstPlane++;
        }
        long lastPlane = SubunitPosition.CellOf(maximumStandingZ);
        for (long plane = firstPlane; plane <= lastPlane; plane++)
        {
            for (long y = minimum.Y; y <= maximum.Y; y++)
            {
                for (long x = minimum.X; x <= maximum.X; x++)
                {
                    TerrainCell? support = terrain.FindCell(new(x, y, plane - 1));
                    if (support is null)
                    {
                        return SpatialOccupancy.Unavailable;
                    }
                    if (support.IsSolid)
                    {
                        const long cellTicks = SubunitPosition.SubunitsPerCell * ticks;
                        patches.Add(new((Int128)x * cellTicks, (Int128)y * cellTicks,
                            ((Int128)x + 1) * cellTicks, ((Int128)y + 1) * cellTicks));
                    }
                }
            }
        }
        foreach (StructureBearingSurface surface in structures.SelectMany(structure => structure.BearingSurfaces))
        {
            BoundaryRectangle bounds = surface.Bounds;
            if (surface.StandingZ >= minimumStandingZ && surface.StandingZ <= maximumStandingZ
                && bounds.MinU < footprint.MaxU && bounds.MaxU > footprint.MinU
                && bounds.MinV < footprint.MaxV && bounds.MaxV > footprint.MinV)
            {
                patches.Add(bounds);
            }
        }
        if (patches.Count > BoundarySurfaceCoverage.MaximumRectangles)
        {
            return SpatialOccupancy.Unavailable;
        }
        Int128 required = checked((footprint.MaxU - footprint.MinU) * (footprint.MaxV - footprint.MinV));
        return BoundarySurfaceCoverage.CoveredArea(footprint, patches) == required
            ? SpatialOccupancy.Clear : SpatialOccupancy.Unsupported;
    }
}
