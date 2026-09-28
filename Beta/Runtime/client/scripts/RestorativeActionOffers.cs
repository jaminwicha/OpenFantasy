using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Own-use and patient permission controls read only their permitted public projection.</summary>
internal static class RestorativeActionOffers
{
    internal static void Add(LocalWorldView view, FarmActionScope scope, FarmTarget? selected,
        IDictionary<FarmSemanticAction, FarmActionOffer> offers)
    {
        if (view.Restorative is not { } care) return;
        if (care.SelfUseOffers.Count > 0)
        {
            var use = care.SelfUseOffers.Count == 1 ? care.SelfUseOffers[0] : null;
            string? blocker = use is null ? "Choose a specific restorative item before using it." : Blocker(use.Reason);
            offers[FarmSemanticAction.UseRestorative] = new(FarmSemanticAction.UseRestorative, scope,
                use is null ? "Use restorative" : $"Use {use.ItemName}", "Yourself", view.PlayerActorId,
                use is null ? "Item cost unavailable" : FormattableString.Invariant($"1 {use.ItemName} · {use.ExplorationMilliseconds / 1000m:0.##} s world time"),
                use is null ? "" : $"You used one {use.ItemName}.", blocker is null ? use!.Intent : null, blocker,
                detailText: use is null ? null : $"Restores up to {use.HealHitPoints} HP; {use.ExpectedHealingHp} HP at your current health. "
                    + "Uses one exact dose from your own pack during exploration. Inspection spends nothing.");
        }
        if (care.PermissionOffers.Count == 0) return;
        var matches = care.PermissionOffers.Where(value => value.HelperActorId == selected?.Id).ToArray();
        var permission = matches.Length == 1 ? matches[0] : care.PermissionOffers.Count == 1 ? care.PermissionOffers[0] : null;
        string? unavailable = permission is null ? "Choose the helper whose care permission you want to withdraw."
            : !permission.IsActive ? "This care permission has already been withdrawn."
            : !permission.CanRevoke ? "You cannot change this permission in the current state." : null;
        offers[FarmSemanticAction.RevokeCarePermission] = new(FarmSemanticAction.RevokeCarePermission, scope,
            "Withdraw permission", permission?.HelperName ?? "Care helper", permission?.HelperActorId,
            "1.8 s world time · no medicine spent", "Care permission withdrawn.",
            unavailable is null ? permission!.Intent : null, unavailable,
            detailText: "Stops future ordinary aid from this helper. Past care remains recorded. "
                + "This withdrawal is permanent for this agreement. It does not cancel your separate aftermath recovery agreement.");
    }

    private static string? Blocker(RestorativeAvailabilityReason reason) => reason switch
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
