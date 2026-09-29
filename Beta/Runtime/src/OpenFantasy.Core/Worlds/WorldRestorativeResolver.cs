using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>Prepares one authenticated ordinary operation against the current candidates of a selected world step.</summary>
internal sealed class WorldRestorativeResolver
{
    internal WorldRestorativePlan Resolve(WorldSnapshot basis, CountedAuthorityState inventory, LocalSpatialSnapshot space,
        ProductionSnapshot production, ProgressionSnapshot progression, CareSnapshot care, WorldResourceHistory history,
        WorldActionCommand command, WorldBatchKind kind, bool readingHold, ulong sequence, uint ordinal, Func<EntityId, bool> occupied)
    {
        WorldRestorativePlan Reject(WorldActionRejection reason) => new(inventory, production, progression, care, history, reason, []);
        if (!inventory.CanControl(new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId), command.ActorId))
            return Reject(WorldActionRejection.NotAuthorized);
        if (space.GetActor(command.ActorId).Revision != command.ExpectedActorRevision) return Reject(WorldActionRejection.RevisionConflict);
        if (kind != WorldBatchKind.AdvancingStep || basis.ProfileVersion is not 26 and not 27 and not 28 || care.Ordinary is not { } ordinary
            || basis.EncounterLifecycle?.Phase != EncounterLifecyclePhase.Exploration || care.Pending is not null
            || basis.PendingExcavation is not null || readingHold || progression.GetActor(command.ActorId).CurrentHp <= 0
            || basis.EncounterLifecycle.GetDebt(command.ActorId).RemainingDelay != 0)
            return Reject(WorldActionRejection.Unavailable);
        try
        {
            if (command.Intent is RevokeCarePermissionIntent revoke)
            {
                var permission = ordinary.Permissions.SingleOrDefault(value => value.PermissionId == revoke.PermissionId);
                if (permission is null || permission.PatientActorId != command.ActorId) return Reject(WorldActionRejection.NotAuthorized);
                if (permission.Revision != revoke.ExpectedPermissionRevision || permission.GrantVersion != revoke.ExpectedPermissionGrantVersion)
                    return Reject(WorldActionRejection.RevisionConflict);
                if (!permission.IsActive) return Reject(WorldActionRejection.Unavailable);
                var revocation = new CarePermissionRevocation(EntityId.DeriveSpawned(command.CommandId, 0), command.CommandId,
                    permission.PermissionId, command.ActorId, sequence, ordinal);
                if (occupied(revocation.RevocationId)) return Reject(WorldActionRejection.Unavailable);
                return new(inventory, production, progression, care.WithOrdinary(ordinary.Revoke(revocation)), history,
                    WorldActionRejection.None, [revocation.RevocationId]);
            }
            if (command.Intent is not UseRestorativeItemIntent intent) throw new ArgumentException("Unsupported ordinary care intent.");
            var eligibility = WorldRestorativeActions.Evaluate(basis, inventory, space, production, progression, care,
                command.ActorId, intent, readingHold);
            if (eligibility != WorldActionRejection.None) return Reject(eligibility);
            EntityId useId = EntityId.DeriveSpawned(command.CommandId, 1), consumptionId = EntityId.DeriveSpawned(command.CommandId, 0);
            if (useId == consumptionId || occupied(useId) || occupied(consumptionId)) return Reject(WorldActionRejection.Unavailable);
            var definition = ordinary.GetDefinition(intent.UseDefinitionId);
            var remedy = care.GetRemedy(definition.RemedyDefinitionId);
            var plan = new RestorativeItemPlanner().Plan(inventory, production, progression,
                new(command.PrincipalId, command.OriginSessionId), command.ActorId, command.CommandId,
                new(basis.GetResident(command.ActorId).InventoryCustodianId, intent.ExpectedStorageRevision,
                    intent.ExpectedProductionRevision, intent.RecipientActorId, remedy.Item, intent.Ingredient),
                intent.ExpectedPatientProgressionRevision, remedy.RestoredHp,
                // The world has already reserved this selected command. Keep every other current
                // and historical identity occupied for the nested material operation.
                production.IsContinued ? identity => identity != command.CommandId && occupied(identity) : null);
            if (plan.Rejection != WorldActionRejection.None) return Reject(plan.Rejection);
            var transition = plan.ActorTransition!;
            var use = new RestorativeUse(useId, command.CommandId, command.ActorId, intent.RecipientActorId,
                definition.DefinitionId, definition.Version, intent.PermissionId, intent.PermissionGrantVersion,
                intent.ExpectedPermissionRevision, plan.Claim!.ConsumptionId, sequence, ordinal, transition.Before.Revision,
                transition.Before.Stats.MaxHp, transition.Before.CurrentHp, transition.Candidate.CurrentHp, definition.ExplorationMilliseconds);
            var effect = ActorResourceChange.FromTransition(0, new(use.UseId, remedy.DefinitionId, remedy.Version), transition);
            var resources = history.Append(new(sequence, ordinal, command.CommandId, [effect]), new WorldRestorativeUseResourceEvidence(command, useId));
            return new(plan.CandidateInventory, plan.CandidateProduction, plan.CandidateProgression,
                care.WithOrdinary(ordinary.RecordUse(use)), resources, WorldActionRejection.None, [consumptionId, useId]);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
    }
}

internal sealed record WorldRestorativePlan(CountedAuthorityState Inventory, ProductionSnapshot Production,
    ProgressionSnapshot Progression, CareSnapshot Care, WorldResourceHistory ResourceHistory,
    WorldActionRejection Rejection, IReadOnlyList<EntityId> CreatedIds);
