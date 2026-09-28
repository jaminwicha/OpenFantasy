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
    public static WorldActionRejection Evaluate(WorldSnapshot world, EntityId actorId, UseRestorativeItemIntent intent)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(intent);
        if (world.Care is null || world.Production is null || world.Progression is null)
            return WorldActionRejection.Unavailable;
        return Evaluate(world, world.Inventory, world.Space, world.Production, world.Progression, world.Care,
            actorId, intent, world.HasReadingHold);
    }

    /// <summary>Captures only the explicitly selected policy and permission; actual supplier control is checked on dispatch.</summary>
    public static UseRestorativeItemIntent? Prepare(WorldSnapshot world, EntityId actorId, EntityId recipientActorId,
        EntityId definitionId, EntityId? permissionId = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Care?.Ordinary is not { } ordinary || !world.Inventory.Actors.Contains(actorId)
            || !world.Inventory.Actors.Contains(recipientActorId)) return null;
        var definition = ordinary.Definitions.SingleOrDefault(value => value.DefinitionId == definitionId);
        if (definition is null) return null;
        var permission = permissionId is { } id ? ordinary.Permissions.SingleOrDefault(value => value.PermissionId == id) : null;
        if (permissionId is not null && permission is null) return null;
        var remedy = world.Care.GetRemedy(definition.RemedyDefinitionId);
        EntityId storage = world.GetResident(actorId).InventoryCustodianId;
        var candidates = world.Inventory.Custody.Lots.Where(value => value.CustodianId == storage && value.Definition == remedy.Item)
            .Select(value => new IngredientRequest(IngredientLotKind.Source, value.LotId, value.Revision, 1))
            .Concat(world.Production!.Lots.Where(value => value.CustodianId == storage && value.Definition == remedy.Item)
                .Select(value => new IngredientRequest(IngredientLotKind.Transformed, value.LotId, value.Revision, 1)))
            .OrderBy(value => value.Kind).ThenBy(value => value.LotId);
        foreach (var dose in candidates)
        {
            var intent = new UseRestorativeItemIntent(recipientActorId, definitionId, definition.Version, permissionId,
                permission?.GrantVersion, permission?.Revision, world.Space.GetActor(recipientActorId).Revision,
                world.Progression!.GetActor(recipientActorId).Revision, world.Inventory.Custody.GetStorage(storage).Revision,
                world.Production.Revision, dose);
            if (Evaluate(world, actorId, intent) == WorldActionRejection.None) return intent;
        }
        return null;
    }

    internal static WorldActionRejection Evaluate(WorldSnapshot basis, CountedAuthorityState inventory,
        LocalSpatialSnapshot space, ProductionSnapshot production, ProgressionSnapshot progression, CareSnapshot care,
        EntityId actorId, UseRestorativeItemIntent intent, bool readingHold)
    {
        if (basis.ProfileVersion != 26 || care.Ordinary is not { } ordinary || care.Pending is not null
            || basis.EncounterLifecycle?.Phase != EncounterLifecyclePhase.Exploration || basis.PendingExcavation is not null
            || readingHold || !inventory.Actors.Contains(actorId) || !inventory.Actors.Contains(intent.RecipientActorId))
            return WorldActionRejection.Unavailable;
        var definition = ordinary.Definitions.SingleOrDefault(value => value.DefinitionId == intent.UseDefinitionId);
        if (definition is null) return WorldActionRejection.Unavailable;
        if (definition.Version != intent.UseDefinitionVersion) return WorldActionRejection.RevisionConflict;
        var supplier = progression.GetActor(actorId);
        var recipient = progression.GetActor(intent.RecipientActorId);
        var presence = space.GetActor(intent.RecipientActorId);
        var storage = inventory.Custody.GetStorage(basis.GetResident(actorId).InventoryCustodianId);
        if (presence.Revision != intent.ExpectedPatientPresenceRevision || recipient.Revision != intent.ExpectedPatientProgressionRevision
            || storage.Revision != intent.ExpectedStorageRevision || production.Revision != intent.ExpectedProductionRevision)
            return WorldActionRejection.RevisionConflict;
        if (supplier.CurrentHp <= 0 || recipient.CurrentHp <= 0 || recipient.CurrentHp == recipient.Stats.MaxHp
            || basis.EncounterLifecycle.GetDebt(actorId).RemainingDelay != 0
            || !space.GetActor(actorId).Condition.SupportsFunction(BodyFunction.Manipulation)
            || presence.Binding.Kind != ActorFormKind.Corporeal
            || !definition.SupportsForm(presence.Binding.DefinitionId, presence.Binding.DefinitionVersion))
            return WorldActionRejection.Unavailable;
        if (definition.DeliveryMode == RestorativeDeliveryMode.SelfUse)
        {
            if (actorId != intent.RecipientActorId || intent.PermissionId is not null)
                return WorldActionRejection.NotAuthorized;
        }
        else
        {
            if (actorId == intent.RecipientActorId || intent.PermissionId is not { } permissionId)
                return WorldActionRejection.NotAuthorized;
            var permission = ordinary.Permissions.SingleOrDefault(value => value.PermissionId == permissionId);
            if (permission is null || permission.PatientActorId != intent.RecipientActorId || permission.HelperActorId != actorId
                || permission.UseDefinitionId != definition.DefinitionId || permission.UseDefinitionVersion != definition.Version)
                return WorldActionRejection.NotAuthorized;
            if (permission.Revision != intent.ExpectedPermissionRevision || permission.GrantVersion != intent.PermissionGrantVersion)
                return WorldActionRejection.RevisionConflict;
            if (!permission.IsActive) return WorldActionRejection.NotAuthorized;
        }
        var remedy = care.GetRemedy(definition.RemedyDefinitionId);
        if (actorId != intent.RecipientActorId && space.QueryInteraction(actorId, intent.RecipientActorId,
            checked((long)remedy.RangeSubunits)) != SpatialInteraction.Reachable) return WorldActionRejection.OutOfReach;
        var selected = MaterialLotSelection.Select(inventory, production, storage.CustodianId, intent.Ingredient);
        return selected?.Definition == remedy.Item && intent.Ingredient.Quantity == 1
            ? WorldActionRejection.None : WorldActionRejection.Unavailable;
    }
}
