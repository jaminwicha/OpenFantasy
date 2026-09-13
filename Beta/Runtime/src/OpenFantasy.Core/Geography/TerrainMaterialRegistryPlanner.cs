using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>Coordinates existing material partition operations under registry-wide identity and resource ownership.</summary>
public sealed class TerrainMaterialRegistryPlanner
{
    private readonly TerrainMaterialPlanner _materials = new();

    public TerrainMaterialRegistryPlan Refine(TerrainMaterialRegistry registry, EntityId allocationId,
        PreparedWatershedTerrain window, IEnumerable<CellCoordinate> cells, EntityId causeId, ulong expectedRevision,
        Func<EntityId, bool>? historicallyOccupied = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        TerrainMaterialState basis = registry.GetState(allocationId);
        TerrainMaterialPlan plan = _materials.Refine(basis, window, cells, causeId, expectedRevision,
            id => registry.ContainsIdentity(id) || (historicallyOccupied?.Invoke(id) ?? false));
        return new(registry, registry.WithMaterial(allocationId, plan.ApplyTo(basis)));
    }

    public TerrainMaterialRegistryPlan Demote(TerrainMaterialRegistry registry, EntityId allocationId,
        IEnumerable<CellCoordinate> cells, EntityId causeId, ulong expectedRevision,
        Func<EntityId, bool>? historicallyOccupied = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        TerrainMaterialState basis = registry.GetState(allocationId);
        TerrainMaterialPlan plan = _materials.Demote(basis, cells, causeId, expectedRevision,
            id => registry.ContainsIdentity(id) || (historicallyOccupied?.Invoke(id) ?? false));
        return new(registry, registry.WithMaterial(allocationId, plan.ApplyTo(basis)));
    }
}
