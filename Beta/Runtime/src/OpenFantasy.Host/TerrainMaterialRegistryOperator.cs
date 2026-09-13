using System.Text.Json;
using OpenFantasy.Core.Geography;
using OpenFantasy.Hosting;

namespace OpenFantasy.Host;

/// <summary>Read-only inspection of normalized material allocation ownership, outside live world admission.</summary>
internal sealed class TerrainMaterialRegistryOperator
{
    internal string Inspect(string path)
    {
        TerrainMaterialRegistry registry = LocalAuthorityFiles.ReadMaterialRegistry(path);
        return JsonSerializer.Serialize(new
        {
            Contract = "terrain-material-registry-v1", Scope = "standalone-shared-material-ownership",
            ContentHash = registry.ContentHash.Value, registry.Revision,
            NetworkColumns = registry.Terrain.Network.ColumnCount, Allocations = registry.Allocations.Count,
            registry.ActiveCellCount, registry.AllocatedQuantity, registry.CoarseQuantity, registry.DetailedQuantity,
            registry.OtherQuantity, LotCount = registry.Inventory.Custody.Lots.Count,
            AllocationStates = registry.Allocations.Select(a => new
            {
                AllocationId = a.AllocationId.ToString(), SourceId = a.Deposit.Source.SourceId.ToString(),
                a.Deposit.Source.Start, a.Deposit.Source.End, ActiveCells = registry.GetState(a.AllocationId).ActiveCells.Count
            }).ToArray()
        });
    }
}
