using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>Projects a finite network into piecewise-constant dry relief without changing its facts or assigning material rights.</summary>
public sealed class WatershedTerrainMapper
{
    public PreparedWatershedTerrain Prepare(PreparedWatershedNetwork network, WatershedTerrainMapping mapping,
        TerrainRegionBounds bounds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(bounds);
        cancellationToken.ThrowIfCancellationRequested();
        Dictionary<WatershedColumnAddress, MappedSource> sources = [];
        foreach (PreparedWatershed basin in network.Basins)
        {
            mapping.ValidateFootprint(basin.Request.Bounds);
            foreach (WatershedColumn column in basin.Columns)
            {
                cancellationToken.ThrowIfCancellationRequested();
                (long x, long y) = basin.Request.Bounds.CoordinateAt(column.Index);
                sources.Add(new(x, y), new(new(basin.BasinId, column.Index), basin.FormationId,
                    column.SubstrateParcelId, mapping.SurfaceZ(column.NaturalElevation)));
            }
        }
        var columns = new WatershedTerrainColumn[checked(bounds.Width * bounds.Depth)];
        for (int y = 0; y < bounds.Depth; y++)
        {
            for (int x = 0; x < bounds.Width; x++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CellCoordinate at = bounds.Minimum.Offset(x, y, 0);
                WatershedColumnAddress macro = mapping.MacroAddressOf(at.X, at.Y);
                if (!sources.TryGetValue(macro, out MappedSource? source))
                {
                    throw new ArgumentOutOfRangeException(nameof(bounds), "Every requested terrain column requires prepared macro coverage.");
                }
                (int offsetX, int offsetY) = mapping.OffsetWithinColumn(at.X, at.Y);
                columns[y * bounds.Width + x] = new(at.X, at.Y, source.Column, source.FormationId,
                    source.SubstrateParcelId, source.SurfaceZ, offsetX, offsetY);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new PreparedWatershedTerrain(network, mapping, bounds, columns);
    }

    private sealed record MappedSource(WatershedColumnReference Column, EntityId FormationId,
        EntityId SubstrateParcelId, long SurfaceZ);
}
