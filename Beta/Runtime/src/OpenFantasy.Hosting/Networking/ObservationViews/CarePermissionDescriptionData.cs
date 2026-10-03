using System.Text.Json.Serialization;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Networking.ObservationViews;

/// <summary>Preserves the original permission row while explicitly opting into its known care terms.</summary>
internal sealed record CarePermissionDescriptionData(EntityId PermissionId, EntityId HelperActorId, string HelperName,
    bool IsActive, bool CanRevoke, RestorativeAvailabilityReason Reason, WorldActionRejection Rejection,
    RevokeCarePermissionIntent? Intent,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] LocalCarePermissionDescription? Description = null)
{
    internal static CarePermissionDescriptionData From(LocalCarePermissionView view, bool includeDescription) =>
        new(view.PermissionId, view.HelperActorId, view.HelperName, view.IsActive, view.CanRevoke,
            view.Reason, view.Rejection, view.Intent, includeDescription ? view.Description : null);

    internal LocalCarePermissionView ToView() =>
        new(PermissionId, HelperActorId, HelperName, IsActive, CanRevoke, Reason, Rejection, Intent)
        { Description = Description };
}

/// <summary>Validates disclosed terms and scope, not the authority's authenticated catalog relationship.</summary>
internal static class CarePermissionDescriptionValidation
{
    internal static bool HasDescriptions(LocalWorldView view) =>
        view.Restorative?.PermissionOffers.Any(permission => permission.Description is not null) == true;

    internal static bool HasDescriptions(LocalControlledActorView? view) =>
        view?.CareDisposition?.PermissionOffers.Any(permission => permission.Description is not null) == true;

    internal static void Validate(LocalWorldView view, bool required)
    {
        if (view.Restorative is not { } restorative) return;
        ValidateUses(restorative.SelfUseOffers);
        Validate(restorative.PermissionOffers, required);
    }

    internal static void Validate(LocalControlledActorView view, bool required)
    {
        if (view.Recovery is { } recovery) ValidateUses(recovery.SelfUseOffers);
        if (view.CareDisposition is { } disposition) Validate(disposition.PermissionOffers, required);
    }

    private static void ValidateUses(IEnumerable<LocalRestorativeUseView> uses)
    {
        var identities = new HashSet<EntityId>();
        foreach (var use in uses)
            if (!identities.Add(use.DefinitionId)) throw new FormatException("Duplicate care use definition.");
    }

    private static void Validate(IEnumerable<LocalCarePermissionView> permissions, bool required)
    {
        var identities = new HashSet<EntityId>();
        foreach (var permission in permissions)
        {
            if (!identities.Add(permission.PermissionId)) throw new FormatException("Duplicate care permission.");
            if (permission.Intent is { } intent && intent.PermissionId != permission.PermissionId)
                throw new FormatException("Care withdrawal names another permission.");
            if (permission.Description is not { } description)
            {
                if (required) throw new FormatException("Missing known care permission terms.");
                continue;
            }
            if (description.UseDefinitionId == default || description.ItemDefinitionId == default
                || description.UseDefinitionId == description.ItemDefinitionId
                || string.IsNullOrWhiteSpace(description.ItemName) || description.ItemName.Length > 120
                || description.HealHitPoints is 0 or > int.MaxValue
                || description.ExplorationMilliseconds != RestorativeUseDefinition.ExplorationDurationMilliseconds
                || description.DeliveryMode is not RestorativeDeliveryMode.LivingAssistance
                    and not RestorativeDeliveryMode.IncapacitatedAssistance)
                throw new FormatException("Invalid known care permission terms.");
        }
    }
}
