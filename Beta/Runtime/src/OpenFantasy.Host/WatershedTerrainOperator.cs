using System.Text.Json;
using OpenFantasy.Core.Geography;
using OpenFantasy.Hosting;

namespace OpenFantasy.Host;

/// <summary>Privileged archive inspection of dry macro-to-local geometry, separate from gameplay terrain admission.</summary>
internal sealed class WatershedTerrainOperator
{
    internal string Inspect(string path)
    {
        PreparedWatershedTerrain terrain = LocalAuthorityFiles.ReadWatershedTerrain(path);
        return JsonSerializer.Serialize(new
        {
            Contract = "watershed-terrain-v1", Scope = "dry-geometry-candidate",
            ContentHash = terrain.ComputeContentHash().Value,
            NetworkHash = terrain.Network.ComputeContentHash().Value,
            MappingHash = terrain.Mapping.Version.Value,
            WorldId = terrain.Scope.WorldId.ToString(), BranchId = terrain.Scope.BranchId.ToString(),
            RealmId = terrain.Scope.RealmId.ToString(),
            terrain.Mapping.CellsPerColumn, terrain.Mapping.CellsPerElevationUnit, terrain.Mapping.ElevationDatumZ,
            terrain.Bounds, NetworkColumnCount = terrain.Network.ColumnCount, ColumnCount = terrain.Columns.Count, terrain.SubstrateCellCount,
            MinimumSurfaceZ = terrain.Columns.Min(column => column.SurfaceZ),
            MaximumSurfaceZ = terrain.Columns.Max(column => column.SurfaceZ)
        });
    }
}
