using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Geography;

/// <summary>One finite ownership scope for disjoint material allocations over a single counted inventory.</summary>
public sealed class TerrainMaterialRegistry
{
    public const int MaximumAllocations = 64;
    public const int MaximumActiveCells = TerrainMaterialState.MaximumActiveCells;
    private readonly Dictionary<EntityId, TerrainMaterialState> _states = [];
    private readonly HashSet<EntityId> _allocationIdentities = [];
    private readonly HashSet<EntityId> _physicalIdentities = [];
    private readonly Lazy<ContentHash> _contentHash;
    private readonly TerrainMaterialProduction? _production;

    internal TerrainMaterialRegistry(PreparedWatershedTerrain terrain, CountedAuthorityState inventory,
        IEnumerable<TerrainMaterialAllocation> allocations, IReadOnlyDictionary<EntityId, CellCoordinate[]>? activeCells, ProductionSnapshot? production = null)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(allocations);
        Terrain = terrain;
        Inventory = inventory;
        _production = production is null ? null : new(production, inventory);
        List<TerrainMaterialAllocation> copy = [];
        foreach (TerrainMaterialAllocation allocation in allocations)
        {
            ArgumentNullException.ThrowIfNull(allocation);
            if (copy.Count == MaximumAllocations || !_allocationIdentities.Add(allocation.AllocationId))
            {
                throw new ArgumentException("Material allocations must be unique and bounded.");
            }
            copy.Add(allocation);
            _physicalIdentities.Add(allocation.Deposit.Material.Physical.DefinitionId);
        }
        if (copy.Count == 0 || activeCells is not null && (activeCells.Count != copy.Count
            || activeCells.Keys.Any(id => !_allocationIdentities.Contains(id))))
        {
            throw new ArgumentException("A registry needs allocated definitions and exact active-state membership.");
        }
        Allocations = copy.OrderBy(a => a.AllocationId).ToList().AsReadOnly();
        ValidateDefinitions();
        foreach (TerrainMaterialAllocation allocation in Allocations)
        {
            TerrainMaterialState state = activeCells is null
                ? TerrainMaterialState.Create(allocation.Deposit, inventory)
                : new(allocation.Deposit, inventory, activeCells[allocation.AllocationId], _production);
            _states.Add(allocation.AllocationId, state);
            ActiveCellCount = checked(ActiveCellCount + state.ActiveCells.Count);
            if (ActiveCellCount > MaximumActiveCells)
            {
                throw new ArgumentException("All allocations share one finite active-cell budget.");
            }
            AllocatedQuantity = checked(AllocatedQuantity + allocation.Deposit.Source.Quantity);
            CoarseQuantity = checked(CoarseQuantity + state.CoarseQuantity);
            DetailedQuantity = checked(DetailedQuantity + state.DetailedQuantity);
            OtherQuantity = checked(OtherQuantity + state.OtherQuantity);
            RetiredQuantity = checked(RetiredQuantity + state.RetiredQuantity);
        }
        _contentHash = new(() => ContentHash.Compute(Production is null ? TerrainMaterialRegistryCodecV1.Encode(this) : TerrainMaterialRegistryCodecV2.Encode(this)));
    }

    public PreparedWatershedTerrain Terrain { get; }
    public CountedAuthorityState Inventory { get; }
    public ReadOnlyCollection<TerrainMaterialAllocation> Allocations { get; }
    public ulong Revision => Inventory.Custody.Revision;
    public ContentHash ContentHash => _contentHash.Value;
    public int ActiveCellCount { get; }
    public ulong AllocatedQuantity { get; }
    public ulong CoarseQuantity { get; }
    public ulong DetailedQuantity { get; }
    public ulong OtherQuantity { get; }
    public ulong RetiredQuantity { get; }
    public ProductionSnapshot? Production => _production?.Snapshot;

    public static TerrainMaterialRegistry Create(PreparedWatershedTerrain terrain, CountedAuthorityState inventory,
        IEnumerable<TerrainMaterialAllocation> allocations) => new(terrain, inventory, allocations, null);

    public TerrainMaterialState GetState(EntityId allocationId) => _states.TryGetValue(allocationId, out var state)
        ? state : throw new ArgumentException("The material allocation is outside this registry.", nameof(allocationId));

    public bool ContainsIdentity(EntityId identity) => Inventory.ContainsIdentity(identity)
        || Terrain.Network.MaterialIdentities.Contains(identity) || _allocationIdentities.Contains(identity)
        || _physicalIdentities.Contains(identity) || (Production?.ContainsIdentity(identity) ?? false);

    /// <summary>Explicitly attaches a production proof without changing existing custody or allocation facts.</summary>
    public TerrainMaterialRegistry AttachProduction(ProductionSnapshot production)
    {
        ArgumentNullException.ThrowIfNull(production);
        if (Production is not null)
        {
            throw new InvalidOperationException("Production is already attached; use atomic reconciliation.");
        }
        return new(Terrain, Inventory, Allocations, ActiveBindings(), production);
    }

    /// <summary>Reconciles one separately authorized custody result across every allocation atomically.</summary>
    public TerrainMaterialRegistry ReconcileInventory(CountedAuthorityState inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        if (inventory.ContentHash == Inventory.ContentHash)
        {
            return this;
        }
        ValidateInventorySuccessor(inventory);
        return new(Terrain, inventory, Allocations, ActiveBindings(), Production);
    }

    /// <summary>Publishes a separately validated custody/production candidate under retained allocation ownership.</summary>
    public TerrainMaterialRegistry ReconcileProduction(CountedAuthorityState inventory, ProductionSnapshot production)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(production);
        if (_production is null)
        {
            throw new InvalidOperationException("Attach production before planning a combined transition.");
        }
        if (inventory.ContentHash == Inventory.ContentHash && production.ContentHash == Production!.ContentHash)
        {
            return this;
        }
        ValidateInventorySuccessor(inventory);
        _production.ValidateSuccessor(production);
        return new(Terrain, inventory, Allocations, ActiveBindings(), production);
    }

    private void ValidateInventorySuccessor(CountedAuthorityState inventory)
    {
        if (inventory.Custody.Revision <= Revision || inventory.WorldId != Inventory.WorldId
            || inventory.BranchId != Inventory.BranchId
            || Inventory.WithCustody(inventory.Custody).ContentHash != inventory.ContentHash)
        {
            throw new ArgumentException("Reconciliation requires advancing custody with unchanged scope and admission.");
        }
    }

    internal TerrainMaterialRegistry WithMaterial(EntityId allocationId, TerrainMaterialState candidate)
    {
        _ = GetState(allocationId);
        Dictionary<EntityId, CellCoordinate[]> active = ActiveBindings();
        active[allocationId] = candidate.ActiveCells.ToArray();
        return new(Terrain, candidate.Inventory, Allocations, active, Production);
    }

    private Dictionary<EntityId, CellCoordinate[]> ActiveBindings() =>
        _states.ToDictionary(pair => pair.Key, pair => pair.Value.ActiveCells.ToArray());

    private void ValidateDefinitions()
    {
        ContentHash? terrainHash = null;
        Dictionary<EntityId, ItemPhysicalBinding> itemBindings = [];
        Dictionary<EntityId, ContentHash> physicalVersions = [];
        HashSet<EntityId> coarse = [], fine = [];
        foreach (TerrainMaterialAllocation allocation in Allocations)
        {
            TerrainMaterialDeposit deposit = allocation.Deposit;
            if (!ReferenceEquals(deposit.Terrain, Terrain)
                && deposit.Terrain.ComputeContentHash() != (terrainHash ??= Terrain.ComputeContentHash()))
            {
                throw new ArgumentException("Every allocation requires the exact registry terrain basis.");
            }
            if (Inventory.ContainsIdentity(allocation.AllocationId) || Terrain.Network.MaterialIdentities.Contains(allocation.AllocationId)
                || _physicalIdentities.Contains(allocation.AllocationId) || (Production?.ContainsIdentity(allocation.AllocationId) ?? false))
            {
                throw new ArgumentException("Allocation identities cannot alias another registry dependency role.");
            }
            ItemPhysicalBinding material = deposit.Material;
            if (itemBindings.TryGetValue(material.Item.DefinitionId, out var binding)
                && (binding.Item != material.Item || binding.Physical.DefinitionId != material.Physical.DefinitionId
                    || binding.Physical.Version != material.Physical.Version)
                || physicalVersions.TryGetValue(material.Physical.DefinitionId, out var version) && version != material.Physical.Version)
            {
                throw new ArgumentException("Material calibrations must use one exact item binding and physical definition version.");
            }
            itemBindings[material.Item.DefinitionId] = material;
            physicalVersions[material.Physical.DefinitionId] = material.Physical.Version;
            coarse.Add(deposit.CoarseCustodianId);
            fine.Add(deposit.FineCustodianId);
        }
        if (coarse.Overlaps(fine))
        {
            throw new ArgumentException("A shared custodian cannot represent both coarse and detailed terrain.");
        }
        if (Production is not null && Production.Batches.Any(batch => (coarse.Contains(batch.CustodianId)
            || fine.Contains(batch.CustodianId)) && batch.Ingredients.Any(ingredient => ingredient.Source is { } source
                && Allocations.Any(allocation => allocation.Deposit.Intersect(source) is not null))))
        {
            throw new ArgumentException("Production cannot consume allocated sources in any terrain representation custodian.");
        }
        for (int i = 0; i < Allocations.Count; i++)
        {
            for (int j = i + 1; j < Allocations.Count; j++)
            {
                ValidateDisjoint(Allocations[i].Deposit, Allocations[j].Deposit);
            }
        }
    }

    private static void ValidateDisjoint(TerrainMaterialDeposit first, TerrainMaterialDeposit second)
    {
        bool spaceOverlap = first.Minimum.X <= second.Maximum.X && second.Minimum.X <= first.Maximum.X
            && first.Minimum.Y <= second.Maximum.Y && second.Minimum.Y <= first.Maximum.Y
            && first.Minimum.Z <= second.Maximum.Z && second.Minimum.Z <= first.Maximum.Z;
        bool sourceOverlap = first.Source.SourceId == second.Source.SourceId && first.Source.Start < second.Source.End
            && second.Source.Start < first.Source.End;
        if (spaceOverlap || sourceOverlap)
        {
            throw new ArgumentException("Allocations cannot overlap physical support or reuse funded source units.");
        }
    }
}
