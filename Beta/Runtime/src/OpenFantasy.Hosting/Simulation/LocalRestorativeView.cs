using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json.Serialization;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Known self-use policy, public effect and an exact owned selection only while eligible.</summary>
public sealed record LocalRestorativeUseView(EntityId DefinitionId, EntityId ItemDefinitionId, string ItemName,
    uint HealHitPoints, uint ExpectedHealingHp, ulong ExplorationMilliseconds, RestorativeAvailability Availability)
{
    public UseRestorativeItemIntent? Intent => Availability.Intent;
    public bool CanUse => Availability.CanUse;
    public RestorativeAvailabilityReason Reason => Availability.Reason;
    public WorldActionRejection Rejection => Availability.Rejection;
}

/// <summary>The patient's own permission scope. No supplier stock, credentials or executable supplier request is exposed.</summary>
public sealed record LocalCarePermissionView(EntityId PermissionId, EntityId HelperActorId, string HelperName,
    bool IsActive, bool CanRevoke, RestorativeAvailabilityReason Reason, WorldActionRejection Rejection,
    RevokeCarePermissionIntent? Intent)
{
    // Explicit presentation codecs carry this extension; legacy diagnostic and wire shapes stay intact.
    [JsonIgnore]
    public LocalCarePermissionDescription? Description { get; init; }
}

/// <summary>The patient's known grant terms, without a helper's stock, current health or executable use.</summary>
public sealed record LocalCarePermissionDescription(EntityId UseDefinitionId, EntityId ItemDefinitionId,
    string ItemName, uint HealHitPoints, ulong ExplorationMilliseconds, RestorativeDeliveryMode DeliveryMode);

/// <summary>Ordinary self-use and patient-owned withdrawal; known blocked choices remain inspectable.</summary>
public sealed class LocalRestorativeView
{
    internal LocalRestorativeView(IEnumerable<LocalRestorativeUseView> selfUseOffers,
        IEnumerable<LocalCarePermissionView> permissionOffers)
    {
        SelfUseOffers = Array.AsReadOnly(selfUseOffers.ToArray());
        PermissionOffers = Array.AsReadOnly(permissionOffers.ToArray());
    }

    public ReadOnlyCollection<LocalRestorativeUseView> SelfUseOffers { get; }
    public ReadOnlyCollection<LocalCarePermissionView> PermissionOffers { get; }

    internal static LocalRestorativeView? Project(WorldSnapshot world, EntityId playerActorId)
    {
        if (!LocalProfileBindings.AdmitsOrdinaryCare(world) || world.Care?.Ordinary is not { } ordinary
            || !world.Inventory.Actors.Contains(playerActorId)) return null;
        var actor = world.Progression!.GetActor(playerActorId);
        var offers = ordinary.Definitions.Where(value => value.DeliveryMode == RestorativeDeliveryMode.SelfUse)
            .Select(definition =>
            {
                var remedy = world.Care.GetRemedy(definition.RemedyDefinitionId);
                string name = ItemName(world, remedy);
                return new LocalRestorativeUseView(definition.DefinitionId, remedy.Item.DefinitionId, name,
                    remedy.RestoredHp, Math.Min(remedy.RestoredHp, (uint)(actor.Stats.MaxHp - actor.CurrentHp)),
                    definition.ExplorationMilliseconds,
                    WorldRestorativeActions.PrepareAvailability(world, playerActorId, playerActorId, definition.DefinitionId));
            });
        var permissions = ordinary.Permissions.Where(value => value.PatientActorId == playerActorId)
            .Select(permission =>
            {
                RestorativeAvailabilityReason reason = WithdrawalReason(world, playerActorId, permission);
                bool canRevoke = reason == RestorativeAvailabilityReason.Available;
                var definition = ordinary.GetDefinition(permission.UseDefinitionId);
                var remedy = world.Care.GetRemedy(definition.RemedyDefinitionId);
                return new LocalCarePermissionView(permission.PermissionId, permission.HelperActorId,
                    world.GetResident(permission.HelperActorId).DisplayName, permission.IsActive, canRevoke, reason,
                    canRevoke ? WorldActionRejection.None : WorldActionRejection.Unavailable,
                    canRevoke ? new(permission.PermissionId, permission.Revision, permission.GrantVersion) : null)
                {
                    Description = new(definition.DefinitionId, remedy.Item.DefinitionId,
                        ItemName(world, remedy), remedy.RestoredHp,
                        definition.ExplorationMilliseconds, definition.DeliveryMode)
                };
            });
        return new(offers, permissions);
    }

    private static string ItemName(WorldSnapshot world, RestorativeRemedyDefinition remedy)
    {
        string? key = world.Production!.Recipes.SelectMany(recipe => recipe.Bindings)
            .FirstOrDefault(binding => binding.Item == remedy.Item)?.Physical.Key;
        return key is null ? "Restorative item"
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(key.Replace('_', ' '));
    }

    private static RestorativeAvailabilityReason WithdrawalReason(WorldSnapshot world, EntityId patient, CarePermission permission)
    {
        if (world.Care!.Pending is not null) return RestorativeAvailabilityReason.PendingAftermathCare;
        if (world.EncounterLifecycle?.Phase != EncounterLifecyclePhase.Exploration)
            return RestorativeAvailabilityReason.EncounterContext;
        if (world.PendingExcavation is not null) return RestorativeAvailabilityReason.ResolvingConsequences;
        if (world.HasReadingHold) return RestorativeAvailabilityReason.ReadingHold;
        if (world.Progression!.GetActor(patient).CurrentHp <= 0) return RestorativeAvailabilityReason.RecipientIncapacitated;
        if (world.EncounterLifecycle.GetDebt(patient).RemainingDelay != 0) return RestorativeAvailabilityReason.ActionDebt;
        return permission.IsActive ? RestorativeAvailabilityReason.Available : RestorativeAvailabilityReason.PermissionRevoked;
    }
}
