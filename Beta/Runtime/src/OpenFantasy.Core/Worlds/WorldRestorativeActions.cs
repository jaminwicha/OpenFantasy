using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>Ordinary delivery eligibility and exact owned-dose selection; never a publication or control grant.</summary>
public static class WorldRestorativeActions
{
    public static WorldActionRejection Evaluate(WorldSnapshot world, EntityId actorId, UseRestorativeItemIntent intent) =>
        Assess(world, actorId, intent).Rejection;

    /// <summary>Explains the captured selection without substituting another dose or refreshing its revisions.</summary>
    public static RestorativeAvailability Assess(WorldSnapshot world, EntityId actorId, UseRestorativeItemIntent intent)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(intent);
        if (world.Care is null || world.Production is null || world.Progression is null)
            return Blocked(RestorativeAvailabilityReason.UnsupportedProfile);
        return Assess(world, world.Inventory, world.Space, world.Production, world.Progression, world.Care,
            actorId, intent.RecipientActorId, intent.UseDefinitionId, intent.PermissionId, intent, world.HasReadingHold);
    }

    /// <summary>Captures only the explicitly selected policy and permission; actual supplier control is checked on dispatch.</summary>
    public static UseRestorativeItemIntent? Prepare(WorldSnapshot world, EntityId actorId, EntityId recipientActorId,
        EntityId definitionId, EntityId? permissionId = null) =>
        PrepareAvailability(world, actorId, recipientActorId, definitionId, permissionId).Intent;

    /// <summary>Explains a known choice even when blocked; only an eligible exact owned dose produces an intent.</summary>
    public static RestorativeAvailability PrepareAvailability(WorldSnapshot world, EntityId actorId, EntityId recipientActorId,
        EntityId definitionId, EntityId? permissionId = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Care is null || world.Production is null || world.Progression is null)
            return Blocked(RestorativeAvailabilityReason.UnsupportedProfile);
        return Assess(world, world.Inventory, world.Space, world.Production, world.Progression, world.Care,
            actorId, recipientActorId, definitionId, permissionId, null, world.HasReadingHold);
    }

    // The resolver passes progressively accumulated owners here, so a second writer sees the
    // first writer's consumed dose, revisions and retained-history capacity before publication.
    internal static WorldActionRejection Evaluate(WorldSnapshot basis, CountedAuthorityState inventory,
        LocalSpatialSnapshot space, ProductionSnapshot production, ProgressionSnapshot progression, CareSnapshot care,
        EntityId actorId, UseRestorativeItemIntent intent, bool readingHold) =>
        Assess(basis, inventory, space, production, progression, care, actorId, intent.RecipientActorId,
            intent.UseDefinitionId, intent.PermissionId, intent, readingHold).Rejection;

    private static RestorativeAvailability Assess(WorldSnapshot basis, CountedAuthorityState inventory,
        LocalSpatialSnapshot space, ProductionSnapshot production, ProgressionSnapshot progression, CareSnapshot care,
        EntityId actorId, EntityId recipientActorId, EntityId definitionId, EntityId? permissionId,
        UseRestorativeItemIntent? captured, bool readingHold)
    {
        if (basis.ProfileVersion is not 26 and not 27 and not 28 and not 30 and not 32 and not 33 and not 34 and not 35 and not 36 and not 37 and not 38 and not 39 and not 40 and not 41 and not 42 || care.Ordinary is not { } ordinary)
            return Blocked(RestorativeAvailabilityReason.UnsupportedProfile);
        if (care.Pending is not null) return Blocked(RestorativeAvailabilityReason.PendingAftermathCare);
        if (basis.EncounterLifecycle?.Phase != EncounterLifecyclePhase.Exploration)
            return Blocked(RestorativeAvailabilityReason.EncounterContext);
        if (basis.PendingExcavation is not null) return Blocked(RestorativeAvailabilityReason.ResolvingConsequences);
        if (readingHold) return Blocked(RestorativeAvailabilityReason.ReadingHold);
        if (!inventory.Actors.Contains(actorId) || !inventory.Actors.Contains(recipientActorId))
            return Blocked(RestorativeAvailabilityReason.UnknownActor);
        var definition = ordinary.Definitions.SingleOrDefault(value => value.DefinitionId == definitionId);
        if (definition is null) return Blocked(RestorativeAvailabilityReason.UnknownDefinition);
        if (captured is not null && definition.Version != captured.UseDefinitionVersion)
            return Blocked(RestorativeAvailabilityReason.StaleSelection, WorldActionRejection.RevisionConflict);
        var supplier = progression.GetActor(actorId);
        var recipient = progression.GetActor(recipientActorId);
        var presence = space.GetActor(recipientActorId);
        var storage = inventory.Custody.GetStorage(basis.GetResident(actorId).InventoryCustodianId);
        if (captured is not null && (presence.Revision != captured.ExpectedPatientPresenceRevision
            || recipient.Revision != captured.ExpectedPatientProgressionRevision
            || storage.Revision != captured.ExpectedStorageRevision || production.Revision != captured.ExpectedProductionRevision))
            return Blocked(RestorativeAvailabilityReason.StaleSelection, WorldActionRejection.RevisionConflict);
        if (!WorldRecoveryAdmission.SupportsRecipient(basis, definition, recipient.CurrentHp))
            return Blocked(definition.DeliveryMode == RestorativeDeliveryMode.IncapacitatedAssistance
                ? RestorativeAvailabilityReason.RecoveryNotRequired : RestorativeAvailabilityReason.RecipientIncapacitated);
        if (supplier.CurrentHp <= 0) return Blocked(RestorativeAvailabilityReason.SupplierIncapacitated);
        if (recipient.CurrentHp == recipient.Stats.MaxHp) return Blocked(RestorativeAvailabilityReason.FullHealth);
        if (basis.EncounterLifecycle.GetDebt(actorId).RemainingDelay != 0)
            return Blocked(RestorativeAvailabilityReason.ActionDebt);
        if (!space.GetActor(actorId).Condition.SupportsFunction(BodyFunction.Manipulation))
            return Blocked(RestorativeAvailabilityReason.ManipulationUnavailable);
        if (presence.Binding.Kind != ActorFormKind.Corporeal
            || !definition.SupportsForm(presence.Binding.DefinitionId, presence.Binding.DefinitionVersion))
            return Blocked(RestorativeAvailabilityReason.UnsupportedForm);
        CarePermission? permission = null;
        if (definition.DeliveryMode == RestorativeDeliveryMode.SelfUse)
        {
            if (actorId != recipientActorId || permissionId is not null)
                return Blocked(RestorativeAvailabilityReason.PermissionMismatch, WorldActionRejection.NotAuthorized);
        }
        else
        {
            if (actorId == recipientActorId)
                return Blocked(RestorativeAvailabilityReason.PermissionMismatch, WorldActionRejection.NotAuthorized);
            if (permissionId is not { } selectedPermissionId)
                return Blocked(RestorativeAvailabilityReason.PermissionRequired, WorldActionRejection.NotAuthorized);
            permission = ordinary.Permissions.SingleOrDefault(value => value.PermissionId == selectedPermissionId);
            if (permission is null || permission.PatientActorId != recipientActorId || permission.HelperActorId != actorId
                || permission.UseDefinitionId != definition.DefinitionId || permission.UseDefinitionVersion != definition.Version)
                return Blocked(RestorativeAvailabilityReason.PermissionMismatch, WorldActionRejection.NotAuthorized);
            if (captured is not null && (permission.Revision != captured.ExpectedPermissionRevision
                || permission.GrantVersion != captured.PermissionGrantVersion))
                return Blocked(RestorativeAvailabilityReason.StaleSelection, WorldActionRejection.RevisionConflict);
            if (!permission.IsActive)
                return Blocked(RestorativeAvailabilityReason.PermissionRevoked, WorldActionRejection.NotAuthorized);
        }
        var remedy = care.GetRemedy(definition.RemedyDefinitionId);
        if (actorId != recipientActorId && space.QueryInteraction(actorId, recipientActorId,
            checked((long)remedy.RangeSubunits)) != SpatialInteraction.Reachable)
            return Blocked(RestorativeAvailabilityReason.OutOfReach, WorldActionRejection.OutOfReach);
        if (captured is null)
        {
            var dose = OwnedDoses(inventory, production, storage.CustodianId, remedy)
                .FirstOrDefault(value => MaterialLotSelection.Select(inventory, production, storage.CustodianId, value) is not null);
            if (dose is null) return Blocked(RestorativeAvailabilityReason.NoOwnedDose);
            captured = new(recipientActorId, definitionId, definition.Version, permissionId, permission?.GrantVersion,
                permission?.Revision, presence.Revision, recipient.Revision, storage.Revision, production.Revision, dose);
        }
        var selected = MaterialLotSelection.Select(inventory, production, storage.CustodianId, captured.Ingredient);
        if (selected?.Definition != remedy.Item || captured.Ingredient.Quantity != 1)
        {
            // Lot revisions historically reject as Unavailable, unlike owner revisions. Keep
            // that receipt meaning while explaining a stale captured lot honestly to callers.
            ulong? currentRevision = captured.Ingredient.Kind == IngredientLotKind.Source
                ? inventory.Custody.Lots.FirstOrDefault(value => value.LotId == captured.Ingredient.LotId
                    && value.CustodianId == storage.CustodianId)?.Revision
                : production.Lots.FirstOrDefault(value => value.LotId == captured.Ingredient.LotId
                    && value.CustodianId == storage.CustodianId)?.Revision;
            if (currentRevision is { } revision && revision != captured.Ingredient.ExpectedRevision)
                return Blocked(RestorativeAvailabilityReason.StaleSelection);
            return Blocked(RestorativeAvailabilityReason.NoOwnedDose);
        }
        // Resource segment rotation retains both of these histories. It cannot create headroom.
        if (production.ConsumedItems.Count >= ProductionSnapshot.MaximumConsumedItems)
            return Blocked(RestorativeAvailabilityReason.MaterialHistoryCapacity);
        if (ordinary.Uses.Count >= OrdinaryCareSnapshot.MaximumUses)
            return Blocked(RestorativeAvailabilityReason.OrdinaryHistoryCapacity);
        return new(RestorativeAvailabilityReason.Available, WorldActionRejection.None, captured);
    }

    private static IEnumerable<IngredientRequest> OwnedDoses(CountedAuthorityState inventory, ProductionSnapshot production,
        EntityId storage, RestorativeRemedyDefinition remedy) =>
        inventory.Custody.Lots.Where(value => value.CustodianId == storage && value.Definition == remedy.Item)
            .Select(value => new IngredientRequest(IngredientLotKind.Source, value.LotId, value.Revision, 1))
            .Concat(production.Lots.Where(value => value.CustodianId == storage && value.Definition == remedy.Item)
                .Select(value => new IngredientRequest(IngredientLotKind.Transformed, value.LotId, value.Revision, 1)))
            .OrderBy(value => value.Kind).ThenBy(value => value.LotId);

    private static RestorativeAvailability Blocked(RestorativeAvailabilityReason reason,
        WorldActionRejection rejection = WorldActionRejection.Unavailable) => new(reason, rejection);
}
