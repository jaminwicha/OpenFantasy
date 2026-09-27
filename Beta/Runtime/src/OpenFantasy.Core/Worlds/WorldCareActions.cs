using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>Care legality and exact dose selection over actual participants, resources and geometry.</summary>
public static class WorldCareActions
{
    public static WorldActionRejection EvaluateAcknowledgment(WorldSnapshot world, EntityId actorId,
        AcknowledgeCareIntent intent)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(intent);
        if (world.Care is not { Pending: null } care || world.EncounterLifecycle is not { Phase: EncounterLifecyclePhase.Outcome } lifecycle
            || lifecycle.Actions!.Scope.EncounterId != intent.EncounterId
            || !care.Agreements.Any(value => value.AgreementId == intent.AgreementId)
            || care.Acknowledgments.Count == CareSnapshot.MaximumAcknowledgments
            || care.Treatments.Count == CareSnapshot.MaximumTreatments
            || world.Production!.ConsumedItems.Count == ProductionSnapshot.MaximumConsumedItems
            || world.Revision >= ulong.MaxValue - 1)
            return WorldActionRejection.Unavailable;
        var agreement = care.GetAgreement(intent.AgreementId);
        if (agreement.PatientActorId != actorId) return WorldActionRejection.NotAuthorized;
        if (intent.ExpectedLifecycleRevision != lifecycle.Revision || intent.ExpectedActionHash != lifecycle.Actions.ContentHash)
            return WorldActionRejection.RevisionConflict;
        if (lifecycle.Actions.LastResolution is not { Status: EncounterActionResolutionStatus.Damaged, TargetHpAfter: 0 } defeat
            || defeat.TargetActorId != actorId || world.Progression!.GetActor(actorId).CurrentHp != 0
            || care.Acknowledgments.Any(value => value.Defeat.DecisionCommandId == defeat.DecisionCommandId)
            || world.Pacing!.Encounter!.Revision != defeat.TimingRevision)
            return WorldActionRejection.Unavailable;
        var available = EvaluateHelper(world, agreement);
        return available != WorldActionRejection.None ? available
            : SelectDose(world, agreement) is null ? WorldActionRejection.Unavailable : WorldActionRejection.None;
    }

    public static WorldActionRejection EvaluateRemedy(WorldSnapshot world, EntityId actorId, AdministerRemedyIntent intent)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(intent);
        if (world.Care is not { Pending: { } pending } care || pending.AcknowledgmentId != intent.AcknowledgmentId
            || world.EncounterLifecycle?.Phase != EncounterLifecyclePhase.Outcome || intent.Ingredient.Quantity != 1)
            return WorldActionRejection.Unavailable;
        var agreement = care.GetAgreement(pending.AgreementId);
        if (agreement.HelperActorId != actorId) return WorldActionRejection.NotAuthorized;
        var patient = world.Progression!.GetActor(agreement.PatientActorId);
        var storage = world.Inventory.Custody.GetStorage(world.GetResident(actorId).InventoryCustodianId);
        if (care.Revision != intent.ExpectedCareRevision
            || world.Space.GetActor(agreement.PatientActorId).Revision != intent.ExpectedPatientPresenceRevision
            || patient.Revision != intent.ExpectedPatientProgressionRevision || storage.Revision != intent.ExpectedStorageRevision
            || world.Production!.Revision != intent.ExpectedProductionRevision)
            return WorldActionRejection.RevisionConflict;
        var available = EvaluateHelper(world, agreement);
        if (available != WorldActionRejection.None) return available;
        var selected = MaterialLotSelection.Select(world.Inventory, world.Production!, storage.CustodianId, intent.Ingredient);
        return selected?.Definition == care.GetRemedy(agreement.RemedyDefinitionId).Item
            ? WorldActionRejection.None : WorldActionRejection.Unavailable;
    }

    /// <summary>Captures a real owned dose. The caller still authenticates and commits the helper's command.</summary>
    public static AdministerRemedyIntent? PrepareRemedy(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Care?.Pending is not { } pending) return null;
        var agreement = world.Care.GetAgreement(pending.AgreementId);
        if (EvaluateHelper(world, agreement) != WorldActionRejection.None || SelectDose(world, agreement) is not { } dose) return null;
        return new(pending.AcknowledgmentId, world.Care.Revision,
            world.Space.GetActor(agreement.PatientActorId).Revision, world.Progression!.GetActor(agreement.PatientActorId).Revision,
            world.Inventory.Custody.GetStorage(world.GetResident(agreement.HelperActorId).InventoryCustodianId).Revision,
            world.Production!.Revision, dose);
    }

    private static WorldActionRejection EvaluateHelper(WorldSnapshot world, CareAgreement agreement)
    {
        var care = world.Care!;
        var helperId = agreement.HelperActorId;
        if (world.HasReadingHold || world.PendingExcavation is not null || world.Progression!.GetActor(agreement.PatientActorId).CurrentHp != 0
            || !WorldActorAvailability.CanAct(world, helperId) || world.EncounterActions!.GetCombatant(helperId).Side != 0
            || !world.Space.GetActor(helperId).Condition.SupportsFunction(BodyFunction.Manipulation))
            return WorldActionRejection.Unavailable;
        return world.Space.QueryInteraction(helperId, agreement.PatientActorId,
            checked((long)care.GetRemedy(agreement.RemedyDefinitionId).RangeSubunits)) == SpatialInteraction.Reachable
            ? WorldActionRejection.None : WorldActionRejection.OutOfReach;
    }

    private static IngredientRequest? SelectDose(WorldSnapshot world, CareAgreement agreement)
    {
        var definition = world.Care!.GetRemedy(agreement.RemedyDefinitionId).Item;
        EntityId custodian = world.GetResident(agreement.HelperActorId).InventoryCustodianId;
        var candidates = world.Inventory.Custody.Lots.Where(lot => lot.CustodianId == custodian && lot.Definition == definition)
            .Select(lot => new IngredientRequest(IngredientLotKind.Source, lot.LotId, lot.Revision, 1))
            .Concat(world.Production!.Lots.Where(lot => lot.CustodianId == custodian && lot.Definition == definition)
                .Select(lot => new IngredientRequest(IngredientLotKind.Transformed, lot.LotId, lot.Revision, 1)))
            .OrderBy(value => value.Kind).ThenBy(value => value.LotId);
        return candidates.FirstOrDefault(value => MaterialLotSelection.Select(world.Inventory, world.Production!, custodian, value) is not null);
    }
}
