using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>Bounded immutable dry geometry candidate. It cannot replace a lived terrain snapshot or allocate material.</summary>
public sealed class PreparedWatershedTerrain
{
    internal PreparedWatershedTerrain(PreparedWatershedNetwork network, WatershedTerrainMapping mapping,
        TerrainRegionBounds bounds, WatershedTerrainColumn[] columns)
    {
        Network = network;
        Mapping = mapping;
        Bounds = bounds;
        Columns = Array.AsReadOnly((WatershedTerrainColumn[])columns.Clone());
        int count = 0;
        foreach (WatershedTerrainColumn column in Columns)
        {
            Int128 highest = Int128.Min(column.SurfaceZ, bounds.Maximum.Z);
            Int128 occupied = Int128.Max(0, highest - bounds.Minimum.Z + 1);
            count = checked(count + (int)occupied);
        }
        SubstrateCellCount = count;
    }

    public PreparedWatershedNetwork Network { get; }
    public WatershedTerrainMapping Mapping { get; }
    public TerrainRegionBounds Bounds { get; }
    public TerrainScope Scope => Network.Scope;
    public ReadOnlyCollection<WatershedTerrainColumn> Columns { get; }
    public int SubstrateCellCount { get; }

    public WatershedTerrainColumn GetColumn(long x, long y)
    {
        if (x < Bounds.Minimum.X || x > Bounds.Maximum.X || y < Bounds.Minimum.Y || y > Bounds.Maximum.Y)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "The column is outside this prepared terrain window.");
        }
        return Columns[checked((int)(y - Bounds.Minimum.Y) * Bounds.Width + (int)(x - Bounds.Minimum.X))];
    }

    public WatershedTerrainCell GetCell(CellCoordinate coordinate)
    {
        if (!Bounds.Contains(coordinate))
        {
            throw new ArgumentOutOfRangeException(nameof(coordinate), "The cell is outside this prepared terrain window.");
        }
        WatershedTerrainColumn column = GetColumn(coordinate.X, coordinate.Y);
        WatershedSubstrateAddress? ancestry = coordinate.Z <= column.SurfaceZ
            ? new(column.FormationId, column.SubstrateParcelId, Mapping.Version, coordinate) : null;
        return new WatershedTerrainCell(coordinate, column, ancestry);
    }

    public ContentHash ComputeContentHash() => ContentHash.Compute(WatershedTerrainCodecV1.Encode(this));
}
