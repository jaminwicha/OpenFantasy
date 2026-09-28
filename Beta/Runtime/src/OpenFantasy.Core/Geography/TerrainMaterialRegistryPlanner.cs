using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Content;

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
        if (registry.Production?.IsContinued == true)
            return Represent(registry, allocationId, TerrainMaterialPlanner.SelectRefine(basis, window, cells),
                causeId, expectedRevision, true, window.ComputeContentHash(), historicallyOccupied);
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
        if (registry.Production?.IsContinued == true)
            return Represent(registry, allocationId, TerrainMaterialPlanner.SelectDemote(basis, cells),
                causeId, expectedRevision, false, null, historicallyOccupied);
        TerrainMaterialPlan plan = _materials.Demote(basis, cells, causeId, expectedRevision,
            id => registry.ContainsIdentity(id) || (historicallyOccupied?.Invoke(id) ?? false));
        return new(registry, registry.WithMaterial(allocationId, plan.ApplyTo(basis)));
    }
    private static TerrainMaterialRegistryPlan Represent(TerrainMaterialRegistry registry, EntityId allocationId,
        CellCoordinate[] cells, EntityId causeId, ulong expectedRevision, bool refining, ContentHash? windowHash,
        Func<EntityId, bool>? historical)
    {
        ArgumentNullException.ThrowIfNull(historical);
        ProductionSnapshot production = registry.Production!;
        MaterialContinuationCommit.Require(registry.Inventory, production, historical);
        TerrainRepresentationBoundary boundary = production.MaterialHistory!.TerrainRepresentation
            ?? throw new ArgumentException("Terrain representation requires its explicit V6 capability.");
        if (production.MaterialHistory.Operations.Count == MaterialOperationHistory.MaximumOperations)
            throw new ArgumentException("The bounded material operation window is full; retain a new boundary first.");
        bool Occupied(EntityId id) => registry.ContainsIdentity(id) || historical(id);
        var state = registry.GetState(allocationId);
        var result = new TerrainMaterialPartition(registry.Inventory, state.Deposit, state.ActiveCells, cells,
            causeId, expectedRevision, Occupied).BuildRaw(refining);
        if (!MaterialContinuationCommit.HasCapacity(result.Inventory, production))
            throw new ArgumentException("Combined material stock exceeds the actual lot or storage bound.");
        var evidence = new TerrainRepresentationEvidence(registry, boundary, result, allocationId, causeId, refining, cells, windowHash);
        var operation = new MaterialTerrainRepresentationOperation(checked(production.Revision + 1), evidence);
        var material = MaterialContinuationCommit.Prepare(result.Inventory, production, operation);
        return new(registry, registry.WithRepresentation(allocationId, result, material.CandidateProduction));
    }
}
