using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Worlds;

/// <summary>One explicitly authored kit and one additional counted carrying allowance in an existing owner's bag.</summary>
public sealed class ConstructionToolAdmission
{
    public ConstructionToolAdmission(EntityId ownerActorId, EntityId custodianId, AdmittedCountedSource source, EntityId lotId)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.UnitCapacity != 1 || new[] { ownerActorId, custodianId, source.SourceId, source.Definition.DefinitionId, lotId }
                .Distinct().Count() != 5)
        {
            throw new ArgumentException("Tool admission requires one finite new unit and distinct owner, bag, source, item and lot roles.");
        }
        OwnerActorId = ownerActorId;
        CustodianId = custodianId;
        Source = source;
        LotId = lotId;
    }

    public EntityId OwnerActorId { get; }
    public EntityId CustodianId { get; }
    public AdmittedCountedSource Source { get; }
    public EntityId LotId { get; }
    public ulong StorageCapacityIncrease => 1;
    public CountedSourceLot Lot => new(LotId, CustodianId, Source.Definition, new SourceInterval(Source.SourceId, 0, 1), 0);
}

/// <summary>Fresh construction metadata and explicit finite kit deltas; no previous progress or inventory is serialized here.</summary>
public sealed class WorldConstructionAdmission
{
    public const int MaximumTools = 32;

    public WorldConstructionAdmission(ContentHash contentVersion, ConstructionSnapshot construction,
        IEnumerable<ConstructionToolAdmission> tools)
    {
        ArgumentNullException.ThrowIfNull(contentVersion);
        ArgumentNullException.ThrowIfNull(construction);
        ArgumentNullException.ThrowIfNull(tools);
        if (construction.Revision != 0 || construction.Projects.Any(value => value.Revision != 0 || value.IsCompleted))
        {
            throw new ArgumentException("A new construction profile cannot admit already earned work or completed structures.");
        }
        SortedDictionary<EntityId, ConstructionToolAdmission> admitted = [];
        foreach (ConstructionToolAdmission tool in tools)
        {
            ArgumentNullException.ThrowIfNull(tool);
            if (admitted.Count == MaximumTools || !admitted.TryAdd(tool.Source.SourceId, tool))
            {
                throw new ArgumentException("Construction tool supply must be finite and uniquely identified.");
            }
        }
        if (admitted.Count == 0 || admitted.Values.Select(value => value.CustodianId).Distinct().Count() != admitted.Count)
        {
            throw new ArgumentException("Each admitted kit must have its own existing builder bag.");
        }
        foreach (ConstructionProject project in construction.Projects)
        {
            if (!admitted.Values.Any(value => value.OwnerActorId == project.BuilderActorId
                && value.Source.Definition == project.Definition.RequiredTool.Item))
            {
                throw new ArgumentException("Each project requires its builder's explicitly admitted exact kit.");
            }
        }
        if (admitted.Values.Any(value => !construction.Projects.Any(project =>
                project.BuilderActorId == value.OwnerActorId && project.Definition.RequiredTool.Item == value.Source.Definition)))
        {
            throw new ArgumentException("Every admitted kit must qualify an actual construction project.");
        }
        ContentVersion = contentVersion;
        Construction = construction;
        Tools = Array.AsReadOnly(admitted.Values.ToArray());
        Definitions = CollectDefinitions();
        EntityIds = CollectEntityIds();
        WorldConstructionMigrationCodecV1.EncodeAdmission(this);
    }

    public ContentHash ContentVersion { get; }
    public ConstructionSnapshot Construction { get; }
    public ReadOnlyCollection<ConstructionToolAdmission> Tools { get; }
    public EntityId WorldId => Construction.WorldId;
    public EntityId BranchId => Construction.BranchId;
    internal IReadOnlyDictionary<EntityId, (WorldIdentityRole Role, ContentHash Version)> Definitions { get; }
    internal ReadOnlyCollection<EntityId> EntityIds { get; }

    /// <summary>Initial supply only; callers must independently qualify the entire fresh world genesis.</summary>
    public CountedAuthorityState CreateGenesisInventory(CountedAuthorityState original)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (original.Custody.Revision != 0 || original.Custody.Storages.Any(value => value.Revision != 0)
            || original.Custody.Lots.Any(value => value.Revision != 0))
        {
            throw new ArgumentException("Fresh supply admission cannot reset an existing inventory revision.", nameof(original));
        }
        return AddTools(original, false);
    }

    internal CountedAuthorityState CreateMigrationInventory(CountedAuthorityState original) => AddTools(original, true);

    private CountedAuthorityState AddTools(CountedAuthorityState original, bool incrementExistingRevisions)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (original.WorldId != WorldId || original.BranchId != BranchId)
        {
            throw new ArgumentException("Tool supply cannot cross world or branch scope.");
        }
        foreach (ConstructionToolAdmission tool in Tools)
        {
            if (!original.Policies.Any(value => value.CustodianId == tool.CustodianId && value.OwnerActorId == tool.OwnerActorId)
                || original.ContainsIdentity(tool.LotId) || original.ContainsIdentity(tool.Source.SourceId))
            {
                throw new ArgumentException("New tool supply requires its existing owner's bag and fresh source/lot identities.");
            }
        }
        var changed = Tools.ToDictionary(value => value.CustodianId);
        var storages = original.Custody.Storages.Select(value => changed.TryGetValue(value.CustodianId, out var tool)
            ? new CustodianStorage(value.CustodianId, checked(value.UnitCapacity + tool.StorageCapacityIncrease),
                incrementExistingRevisions ? checked(value.Revision + 1) : 0) : value);
        var custody = new CustodySnapshot(WorldId, BranchId,
            incrementExistingRevisions ? checked(original.Custody.Revision + 1) : 0,
            storages, original.Custody.Lots.Concat(Tools.Select(value => value.Lot)));
        return new CountedAuthorityState(custody, checked(original.AdmittedUnitCapacity + (ulong)Tools.Count),
            original.Actors, original.Controls, original.Policies, original.Sources.Concat(Tools.Select(value => value.Source)),
            original.ReservationIds);
    }

    private IReadOnlyDictionary<EntityId, (WorldIdentityRole Role, ContentHash Version)> CollectDefinitions()
    {
        Dictionary<EntityId, (WorldIdentityRole, ContentHash)> result = [];
        void Add(EntityId id, WorldIdentityRole role, ContentHash pin)
        {
            if (id == WorldId || id == BranchId || result.TryGetValue(id, out var known) && (known.Item1 != role || known.Item2 != pin))
            {
                throw new ArgumentException("Construction definition identities must retain exact nonaliased roles and pins.");
            }
            result[id] = (role, pin);
        }
        foreach (ConstructionDefinition definition in Construction.Definitions)
        {
            Add(definition.DefinitionId, WorldIdentityRole.ConstructionDefinition, definition.Version);
            Add(definition.RequiredTool.DefinitionId, WorldIdentityRole.ConstructionToolDefinition, definition.RequiredTool.Version);
            Add(definition.RequiredTool.Item.DefinitionId, WorldIdentityRole.ItemDefinition, definition.RequiredTool.Item.Version);
            Add(definition.WorkRate.OutputUnitId, WorldIdentityRole.ConstructionWorkUnit, definition.WorkRate.RulesVersion);
            Add(definition.MaterialTransform.DefinitionId, WorldIdentityRole.CraftRecipeDefinition, definition.MaterialTransform.Version);
            Add(definition.MaterialTransform.PhysicalRecipe.DefinitionId, WorldIdentityRole.PhysicalRecipeDefinition, definition.MaterialTransform.PhysicalRecipe.Version);
            foreach (var binding in definition.MaterialTransform.Bindings)
            {
                Add(binding.Item.DefinitionId, WorldIdentityRole.ItemDefinition, binding.Item.Version);
                Add(binding.Physical.DefinitionId, WorldIdentityRole.PhysicalMaterialDefinition, binding.Physical.Version);
            }
        }
        return new ReadOnlyDictionary<EntityId, (WorldIdentityRole, ContentHash)>(result);
    }

    private ReadOnlyCollection<EntityId> CollectEntityIds()
    {
        HashSet<EntityId> existingRoles = [WorldId, BranchId];
        foreach (ConstructionProject project in Construction.Projects)
        {
            existingRoles.UnionWith([project.BuilderActorId, project.OwnerActorId, project.QuestId,
                project.ProjectCustodianId, project.ReservationId]);
            existingRoles.UnionWith(project.RequiredAccess.Select(value => value.ActorId));
        }
        existingRoles.UnionWith(Tools.Select(value => value.CustodianId));
        if (Definitions.Keys.Any(existingRoles.Contains))
        {
            throw new ArgumentException("Construction definitions cannot alias existing participant or custody roles.");
        }
        SortedSet<EntityId> entities = [];
        foreach (EntityId id in Construction.Projects.Select(value => value.ProjectId)
            .Concat(Tools.SelectMany(value => new[] { value.Source.SourceId, value.LotId })))
        {
            if (existingRoles.Contains(id) || Definitions.ContainsKey(id) || !entities.Add(id))
            {
                throw new ArgumentException("Construction projects, tool sources and lots need fresh distinct identities.");
            }
        }
        return Array.AsReadOnly(entities.ToArray());
    }
}
