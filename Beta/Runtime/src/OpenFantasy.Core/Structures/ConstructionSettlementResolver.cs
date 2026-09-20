using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Structures;

public sealed record ConstructionSettlementResult(LocalSpatialSnapshot Space, ConstructionPhysicalSnapshot Physical);

/// <summary>Exhausts the same geometric continuation for synchronous harmless settlement.</summary>
public sealed class ConstructionSettlementResolver
{
    public ConstructionSettlementResult Resolve(LocalSpatialSnapshot space, IEnumerable<TerrainRegionSnapshot> regions,
        ConstructionPhysicalSnapshot physical, StructuralSettlementPolicy policy)
    {
        return Complete(ConstructionSettlementResolution.Begin(space, regions, physical, policy));
    }

    public ConstructionSettlementResult ResolveCoupled(WorldSnapshot basis, IEnumerable<TerrainRegionSnapshot> regions)
        => Complete(ConstructionSettlementResolution.BeginCoupled(basis, regions));

    private static ConstructionSettlementResult Complete(ConstructionSettlementResolution resolution)
    {
        while (!resolution.IsComplete)
        {
            resolution = resolution.Advance(ConstructionSettlementResolution.MaximumWorkBudget);
        }
        if (resolution.IsFailed)
        {
            throw new ArgumentException(resolution.FailureReason ?? "The geometry cannot settle under its retained calibration.");
        }
        return resolution.Result;
    }
}
