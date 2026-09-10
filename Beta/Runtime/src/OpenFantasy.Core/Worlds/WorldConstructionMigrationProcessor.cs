using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Worlds;

public sealed class WorldConstructionMigrationDecision
{
    internal WorldConstructionMigrationDecision(EntityId causeId, WorldSnapshot candidate,
        IEnumerable<EntityId> definitionIds, IEnumerable<EntityId> entityIds)
    {
        CauseId = causeId;
        Candidate = candidate;
        AdmittedDefinitionIds = Array.AsReadOnly(definitionIds.Order().ToArray());
        AdmittedEntityIds = Array.AsReadOnly(entityIds.Order().ToArray());
    }

    public EntityId CauseId { get; }
    public WorldSnapshot Candidate { get; }
    public ReadOnlyCollection<EntityId> AdmittedDefinitionIds { get; }
    public ReadOnlyCollection<EntityId> AdmittedEntityIds { get; }
}

/// <summary>Exact-head admission of new assembly metadata and one finite kit per builder, preserving all earlier outcomes.</summary>
public sealed class WorldConstructionMigrationProcessor
{
    public WorldConstructionMigrationDecision Decide(WorldSnapshot state, WorldConstructionMigration request,
        Func<EntityId, bool>? historicallyOccupied = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        if (state.ProfileVersion != 4 || state.WorldId != request.WorldId || state.BranchId != request.BranchId
            || state.Revision != request.ExpectedRevision || state.AdvancingSteps != request.ExpectedAdvancingSteps
            || state.ContentHash != request.ExpectedHeadHash || state.AdmissionVersion != request.SourceAdmissionVersion)
        {
            throw new InvalidOperationException("Construction admission does not match the exact retained quest head.");
        }
        bool Occupied(EntityId id) => state.IdentityIndex.Contains(id) || historicallyOccupied?.Invoke(id) == true;
        if (Occupied(request.CauseId))
        {
            throw new InvalidOperationException("The construction admission cause is occupied.");
        }
        WorldConstructionAdmission admission = request.Admission;
        var oldPins = RetainedMaterialPins(state);
        List<EntityId> addedDefinitions = [];
        foreach (var pair in admission.Definitions)
        {
            if (oldPins.TryGetValue(pair.Key, out var known) && known.Role == pair.Value.Role
                && known.Version == pair.Value.Version)
            {
                continue;
            }
            if (pair.Key == request.CauseId || Occupied(pair.Key))
            {
                throw new InvalidOperationException("A construction definition aliases an existing role or changes a retained material pin.");
            }
            addedDefinitions.Add(pair.Key);
        }
        foreach (EntityId id in admission.EntityIds)
        {
            if (id == request.CauseId || Occupied(id))
            {
                throw new InvalidOperationException("A new construction project or tool identity is occupied.");
            }
        }
        foreach (ConstructionToolAdmission tool in admission.Tools)
        {
            if (tool.CustodianId != state.GetResident(tool.OwnerActorId).InventoryCustodianId
                || Occupied(tool.Source.Definition.DefinitionId))
            {
                throw new InvalidOperationException("The authored kit must be newly defined and carried in its builder's existing primary bag.");
            }
        }
        var inventory = admission.CreateMigrationInventory(state.Inventory);
        var production = new ProductionSnapshot(state.WorldId, state.BranchId, state.Production!.Revision,
            state.Production.Recipes.Concat(admission.Construction.Definitions.Select(value => value.MaterialTransform)),
            state.Production.Batches, state.Production.Lots);
        var candidate = new WorldSnapshot(inventory, state.Space, state.Residents, request.TargetAdmissionVersion,
            state.Calendar, checked(state.Revision + 1), state.AdvancingSteps, state.Dialogues, state.Gathering,
            production, state.Progression, state.Quests, admission.Construction);
        return new WorldConstructionMigrationDecision(request.CauseId, candidate, addedDefinitions, admission.EntityIds);
    }

    private static Dictionary<EntityId, (WorldIdentityRole Role, ContentHash Version)> RetainedMaterialPins(WorldSnapshot state)
    {
        Dictionary<EntityId, (WorldIdentityRole, ContentHash)> result = [];
        foreach (var source in state.Inventory.Sources)
        {
            result[source.Definition.DefinitionId] = (WorldIdentityRole.ItemDefinition, source.Definition.Version);
        }
        foreach (var recipe in state.Production!.Recipes)
        {
            foreach (var binding in recipe.Bindings)
            {
                result[binding.Item.DefinitionId] = (WorldIdentityRole.ItemDefinition, binding.Item.Version);
                result[binding.Physical.DefinitionId] = (WorldIdentityRole.PhysicalMaterialDefinition, binding.Physical.Version);
            }
        }
        return result;
    }
}
