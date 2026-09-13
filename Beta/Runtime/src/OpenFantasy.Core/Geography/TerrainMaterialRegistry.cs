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

    internal TerrainMaterialRegistry(PreparedWatershedTerrain terrain, CountedAuthorityState inventory,
        IEnumerable<TerrainMaterialAllocation> allocations, IReadOnlyDictionary<EntityId, CellCoordinate[]>? activeCells)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(allocations);
        Terrain = terrain;
        Inventory = inventory;
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
                : new(allocation.Deposit, inventory, activeCells[allocation.AllocationId]);
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
        }
        _contentHash = new(() => ContentHash.Compute(TerrainMaterialRegistryCodecV1.Encode(this)));
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

    public static TerrainMaterialRegistry Create(PreparedWatershedTerrain terrain, CountedAuthorityState inventory,
        IEnumerable<TerrainMaterialAllocation> allocations) => new(terrain, inventory, allocations, null);

    public TerrainMaterialState GetState(EntityId allocationId) => _states.TryGetValue(allocationId, out var state)
        ? state : throw new ArgumentException("The material allocation is outside this registry.", nameof(allocationId));

    public bool ContainsIdentity(EntityId identity) => Inventory.ContainsIdentity(identity)
        || Terrain.Network.MaterialIdentities.Contains(identity) || _allocationIdentities.Contains(identity)
        || _physicalIdentities.Contains(identity);

    /// <summary>Reconciles one separately authorized custody result across every allocation atomically.</summary>
    public TerrainMaterialRegistry ReconcileInventory(CountedAuthorityState inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        if (inventory.ContentHash == Inventory.ContentHash)
        {
            return this;
        }
        foreach (TerrainMaterialState state in _states.Values)
        {
            _ = state.ReconcileInventory(inventory);
        }
        return new(Terrain, inventory, Allocations, ActiveBindings());
    }

    internal TerrainMaterialRegistry WithMaterial(EntityId allocationId, TerrainMaterialState candidate)
    {
        _ = GetState(allocationId);
        Dictionary<EntityId, CellCoordinate[]> active = ActiveBindings();
        active[allocationId] = candidate.ActiveCells.ToArray();
        return new(Terrain, candidate.Inventory, Allocations, active);
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
                || _physicalIdentities.Contains(allocation.AllocationId))
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
