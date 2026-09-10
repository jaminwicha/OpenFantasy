using System.Collections.ObjectModel;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Worlds;

public sealed class WorldQuestMigrationDecision
{
    internal WorldQuestMigrationDecision(EntityId causeId, WorldSnapshot candidate,
        IEnumerable<EntityId> admittedDefinitionIds, IEnumerable<EntityId> admittedEntityIds)
    {
        CauseId = causeId;
        Candidate = candidate;
        AdmittedDefinitionIds = Array.AsReadOnly(admittedDefinitionIds.Order().ToArray());
        AdmittedEntityIds = Array.AsReadOnly(admittedEntityIds.Order().ToArray());
    }

    public EntityId CauseId { get; }
    public WorldSnapshot Candidate { get; }
    public ReadOnlyCollection<EntityId> AdmittedDefinitionIds { get; }
    public ReadOnlyCollection<EntityId> AdmittedEntityIds { get; }
}

/// <summary>Pure exact-head upgrade. Only empty project custody and fresh quest/progression metadata are admitted.</summary>
public sealed class WorldQuestMigrationProcessor
{
    public WorldQuestMigrationDecision Decide(WorldSnapshot state, WorldQuestMigration request,
        Func<EntityId, bool>? historicallyOccupied = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        if (state.ProfileVersion != 3 || state.WorldId != request.WorldId || state.BranchId != request.BranchId
            || state.Revision != request.ExpectedRevision || state.AdvancingSteps != request.ExpectedAdvancingSteps
            || state.ContentHash != request.ExpectedHeadHash || state.AdmissionVersion != request.SourceAdmissionVersion)
        {
            throw new InvalidOperationException("The quest migration does not match the exact retained production head.");
        }
        bool Occupied(EntityId id) => state.IdentityIndex.Contains(id) || historicallyOccupied?.Invoke(id) == true;
        if (Occupied(request.CauseId))
        {
            throw new InvalidOperationException("The quest migration cause identity is occupied.");
        }
        WorldQuestAdmission admission = request.Admission;
        admission.Progression.ValidateActorSet(state.Inventory.Actors);
        foreach (EntityId id in admission.DefinitionIds.Concat(admission.EntityIds))
        {
            if (id == request.CauseId || Occupied(id))
            {
                throw new InvalidOperationException("A new quest identity aliases a current or historical identity.");
            }
        }
        CountedAuthorityState original = state.Inventory;
        var custody = new CustodySnapshot(state.WorldId, state.BranchId, original.Custody.Revision,
            original.Custody.Storages.Concat(admission.Projects.Select(project =>
                new CustodianStorage(project.CustodianId, project.UnitCapacity, 0))), original.Custody.Lots);
        var inventory = new CountedAuthorityState(custody, original.AdmittedUnitCapacity,
            original.Actors, original.Controls, original.Policies.Concat(admission.Projects.Select(project =>
                new CustodianAccessPolicy(project.CustodianId, project.OwnerActorId, project.DepositRule))),
            original.Sources, original.ReservationIds.Concat(admission.Projects.Select(project => project.ReservationId)));
        var candidate = new WorldSnapshot(inventory, state.Space, state.Residents, request.TargetAdmissionVersion,
            state.Calendar, checked(state.Revision + 1), state.AdvancingSteps, state.Dialogues,
            state.Gathering, state.Production, admission.Progression, admission.Quests);
        return new WorldQuestMigrationDecision(request.CauseId, candidate, admission.DefinitionIds, admission.EntityIds);
    }
}
