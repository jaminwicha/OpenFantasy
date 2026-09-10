using System.Collections.ObjectModel;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Worlds;

public sealed class WorldProductionMigrationDecision
{
    internal WorldProductionMigrationDecision(EntityId causeId, WorldSnapshot candidate, IEnumerable<EntityId> admittedDefinitionIds)
    {
        CauseId = causeId;
        Candidate = candidate;
        AdmittedDefinitionIds = Array.AsReadOnly(admittedDefinitionIds.Order().ToArray());
    }

    public EntityId CauseId { get; }
    public WorldSnapshot Candidate { get; }
    public ReadOnlyCollection<EntityId> AdmittedDefinitionIds { get; }
}

/// <summary>Upgrades the rules profile while preserving every existing simulation component and clock.</summary>
public sealed class WorldProductionMigrationProcessor
{
    public WorldProductionMigrationDecision Decide(WorldSnapshot state, WorldProductionMigration request,
        Func<EntityId, bool>? historicallyOccupied = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        if (state.ProfileVersion != 2 || state.WorldId != request.WorldId || state.BranchId != request.BranchId
            || state.Revision != request.ExpectedRevision || state.AdvancingSteps != request.ExpectedAdvancingSteps
            || state.ContentHash != request.ExpectedHeadHash || state.AdmissionVersion != request.SourceAdmissionVersion)
        {
            throw new InvalidOperationException("The migration does not match the exact retained gathering head.");
        }
        bool Occupied(EntityId id) => state.IdentityIndex.Contains(id) || historicallyOccupied?.Invoke(id) == true;
        if (Occupied(request.CauseId))
        {
            throw new InvalidOperationException("The migration cause identity is already occupied.");
        }
        HashSet<EntityId> additions = [];
        void Admit(EntityId id, bool exactExistingItem = false)
        {
            if (id == request.CauseId || (!exactExistingItem && Occupied(id)))
            {
                throw new InvalidOperationException("A production definition aliases an existing or historical identity.");
            }
            if (!exactExistingItem)
            {
                additions.Add(id);
            }
        }
        foreach (CraftRecipeDefinition recipe in request.Production.Recipes)
        {
            Admit(recipe.DefinitionId);
            Admit(recipe.PhysicalRecipe.DefinitionId);
            foreach (ItemPhysicalBinding binding in recipe.Bindings)
            {
                bool existingItem = state.Inventory.Sources.Any(source => source.Definition == binding.Item);
                Admit(binding.Item.DefinitionId, existingItem);
                Admit(binding.Physical.DefinitionId);
            }
        }
        var candidate = new WorldSnapshot(state.Inventory, state.Space, state.Residents, request.TargetAdmissionVersion,
            state.Calendar, checked(state.Revision + 1), state.AdvancingSteps, state.Dialogues, state.Gathering, request.Production);
        return new WorldProductionMigrationDecision(request.CauseId, candidate, additions);
    }
}
