using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Own-use and patient permission controls read only their permitted public projection.</summary>
internal static class RestorativeActionOffers
{
    internal static void Add(LocalWorldView view, FarmActionScope scope, FarmTarget? selected,
        IDictionary<FarmSemanticAction, FarmActionOffer> offers)
    {
        Add(FarmCarePresentation.From(view), scope, selected, offers);
    }

    internal static void Add(FarmCarePresentation care, FarmActionScope scope, FarmTarget? selected,
        IDictionary<FarmSemanticAction, FarmActionOffer> offers)
    {
        if (care.Remedies.Count > 0)
        {
            var use = care.Remedies.Count == 1 ? care.Remedies[0] : null;
            offers[FarmSemanticAction.UseRestorative] = use is not null ? Use(scope, care.ActorName, use)
                : new(FarmSemanticAction.UseRestorative, scope, "Use restorative", care.ActorName, care.ActorId,
                    "Item cost unavailable", "", null, "Choose a specific restorative item in the Care library.");
        }
        if (care.Permissions.Count > 0)
        {
            var matches = care.Permissions.Where(value => value.HelperActorId == selected?.Id).ToArray();
            var permission = matches.Length == 1 ? matches[0] : care.Permissions.Count == 1 ? care.Permissions[0] : null;
            offers[FarmSemanticAction.RevokeCarePermission] = permission is not null ? Withdraw(scope, permission)
                : new(FarmSemanticAction.RevokeCarePermission, scope, "Withdraw permission", "Care helper", null,
                    "1.8 s world time · no medicine spent", "", null,
                    "Choose one exact aid permission in the Care library.");
        }
        if (care.HasChoices)
            offers[FarmSemanticAction.OpenCare] = FarmActionOffer.Panel(FarmSemanticAction.OpenCare, scope,
                "Care library", care.ActorName, care.ActorId,
                "Inspect known medicines and aid permissions for this character. Select one, review, then confirm. Inspection spends nothing.", null);
    }

    internal static FarmActionOffer Use(FarmActionScope scope, string actorName, LocalRestorativeUseView use)
    {
        string? blocker = Blocker(use.Reason);
        return new(FarmSemanticAction.UseRestorative, scope, $"Use {use.ItemName}", actorName, scope.ActorId,
            FormattableString.Invariant($"1 {use.ItemName} · {use.ExplorationMilliseconds / 1000m:0.##} s world time"),
            $"You used one {use.ItemName}.", blocker is null ? use.Intent : null, blocker,
            detailText: $"Restores up to {use.HealHitPoints} HP; {use.ExpectedHealingHp} HP at your current health. "
                + "Uses one exact dose from your own pack during exploration. Inspection spends nothing.");
    }

    internal static string PermissionLabel(LocalCarePermissionView permission) => permission.Description is { } description
        ? $"{permission.HelperName} · {description.ItemName} · {description.HealHitPoints} HP · {DeliveryLabel(description.DeliveryMode)}"
        : $"{permission.HelperName} · aid permission (details unavailable)";

    internal static FarmActionOffer Withdraw(FarmActionScope scope, LocalCarePermissionView permission)
    {
        string? blocker = !permission.IsActive ? "This care permission has already been withdrawn."
            : !permission.CanRevoke ? Blocker(permission.Reason) ?? "You cannot change this permission in the current state." : null;
        string description = permission.Description is { } terms
            ? $"Allows {permission.HelperName} to provide {terms.ItemName} aid ({DeliveryLabel(terms.DeliveryMode)}), restoring up to {terms.HealHitPoints} HP "
                + FormattableString.Invariant($"over {terms.ExplorationMilliseconds / 1000m:0.##} s of world time. ")
            : "The description of this exact agreement is unavailable in this observation. ";
        return new(FarmSemanticAction.RevokeCarePermission, scope, "Withdraw permission", permission.HelperName,
            permission.HelperActorId, "1.8 s world time · no medicine spent", "Care permission withdrawn.",
            blocker is null ? permission.Intent : null, blocker,
            detailText: description + "Stops future ordinary aid covered by this agreement. Past care remains recorded. "
                + "This withdrawal is permanent for this agreement. It does not cancel your separate aftermath recovery agreement.");
    }

    private static string DeliveryLabel(RestorativeDeliveryMode mode) => mode switch
    {
        RestorativeDeliveryMode.LivingAssistance => "living aid",
        RestorativeDeliveryMode.IncapacitatedAssistance => "incapacitated aid",
        RestorativeDeliveryMode.SelfUse => "self use",
        _ => "delivery details unavailable"
    };

    internal static string? Blocker(RestorativeAvailabilityReason reason) => reason switch
    {
        RestorativeAvailabilityReason.Available => null,
        RestorativeAvailabilityReason.FullHealth => "Your health is already full. No dose is needed.",
        RestorativeAvailabilityReason.NoOwnedDose => "You have no available dose of this medicine in your pack.",
        RestorativeAvailabilityReason.MaterialHistoryCapacity or RestorativeAvailabilityReason.OrdinaryHistoryCapacity =>
            "World storage needs attention before another use. Your medicine is safe.",
        RestorativeAvailabilityReason.ReadingHold => "Finish the current conversation before using medicine.",
        RestorativeAvailabilityReason.EncounterContext => "This use is available during exploration, after the encounter ends.",
        RestorativeAvailabilityReason.PendingAftermathCare => "The accepted recovery treatment must finish first.",
        RestorativeAvailabilityReason.ResolvingConsequences => "Wait for the current world changes to finish.",
        RestorativeAvailabilityReason.ActionDebt => "You are still recovering from your last action.",
        RestorativeAvailabilityReason.SupplierIncapacitated or RestorativeAvailabilityReason.RecipientIncapacitated =>
            "You cannot use this medicine while incapacitated. Use the available recovery choices.",
        RestorativeAvailabilityReason.ManipulationUnavailable => "Your current condition prevents handling this medicine.",
        RestorativeAvailabilityReason.UnsupportedForm => "This medicine cannot be delivered to your current form.",
        RestorativeAvailabilityReason.StaleSelection => "This exact dose or its recipient changed. Choose again.",
        RestorativeAvailabilityReason.PermissionRequired or RestorativeAvailabilityReason.PermissionMismatch or RestorativeAvailabilityReason.PermissionRevoked =>
            "This use does not have the required permission.",
        RestorativeAvailabilityReason.OutOfReach => "The selected recipient is out of reach.",
        _ => "This restorative use is unavailable."
    };
}
