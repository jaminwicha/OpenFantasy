using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Captures only public encounter choices. Selection never reads authority or substitutes a later target.</summary>
internal static class EncounterActionOffers
{
    /// <summary>Null leaves ordinary farm offers intact; a selected confrontation or live encounter owns its actions.</summary>
    internal static IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer>? Create(LocalWorldView view,
        FarmActionScope scope, FarmTarget? selected)
    {
        Dictionary<FarmSemanticAction, FarmActionOffer> offers = [];
        if (view.Care is { } care)
        {
            offers[FarmSemanticAction.AcknowledgeCare] = AcknowledgeCare(care, scope, selected);
        }
        else if (view.IsInEncounter)
        {
            if (view.EncounterLifecycle?.Phase == EncounterLifecyclePhase.Outcome || view.Encounter!.IsOutcome)
            {
                offers[FarmSemanticAction.ReleaseEncounter] = Release(view, scope);
            }
            else
            {
                offers[FarmSemanticAction.Strike] = Strike(view, scope, selected);
                offers[FarmSemanticAction.Wait] = Wait(view, scope);
                if (view.Retreat is { } retreat)
                {
                    offers[FarmSemanticAction.Retreat] = RetreatOffers.Create(retreat, scope);
                }
            }
        }
        else if (view.EncounterLifecycle is { } lifecycle && selected is not null
            && (selected.Kind == FarmTargetKind.Encounter || lifecycle.Entries.Any(entry => entry.TargetActorId == selected.Id)))
        {
            LocalEncounterEntryView? entry = lifecycle.Entries.FirstOrDefault(value => value.TargetActorId == selected.Id);
            string? blocker = entry is null ? "This confrontation is no longer visible. Choose again."
                : !entry.CanEnter ? "This confrontation cannot begin from your current position or condition." : null;
            offers[FarmSemanticAction.EnterEncounter] = new(FarmSemanticAction.EnterEncounter, scope,
                "Confront", selected.Name, selected.Id, FarmActionOffers.NoCost, "The confrontation began.",
                blocker is null ? new EnterEncounterIntent(entry!.ScenarioId, entry.EncounterId,
                    lifecycle.EncounterCount, entry.TargetRevision) : null, blocker,
                detailText: entry?.DisplayName);
        }
        else return null;
        return new ReadOnlyDictionary<FarmSemanticAction, FarmActionOffer>(offers);
    }

    private static FarmActionOffer AcknowledgeCare(LocalCareView care, FarmActionScope scope, FarmTarget? selected)
    {
        const string detail = "Acknowledge your prior care agreement. This does not restore health or spend a dose; "
            + "the named helper must administer one real dose in a separate action when the world is ready.";
        if (care.PendingAcknowledgmentId is not null)
        {
            string blocker = care.Status switch
            {
                LocalCareStatus.Pending => "Care acknowledged. Waiting for the helper's separate treatment action.",
                LocalCareStatus.NeedsAttention => "Care needs attention. The helper's treatment is blocked.",
                _ => "Care acknowledged, but the helper cannot currently administer treatment."
            };
            return new(FarmSemanticAction.AcknowledgeCare, scope, "Acknowledge care", care.HelperName ?? "Care helper",
                care.HelperActorId, FarmActionOffers.NoCost, "", null, blocker, detailText: detail);
        }

        // A lingering opponent selection is not a care-helper selection. One named agreement is
        // unambiguous; explicit person selection and multiple agreements never silently fall back.
        LocalCareAcknowledgmentView[] choices = care.Offers.Where(offer => offer.HelperActorId == selected?.Id).ToArray();
        if (choices.Length == 0 && (selected is null || selected.Kind == FarmTargetKind.Encounter))
        {
            choices = care.Offers.ToArray();
        }
        LocalCareAcknowledgmentView? choice = choices.Length == 1 ? choices[0] : null;
        string? unavailable = choice is null
            ? choices.Length > 1 ? "Choose one care helper with an unambiguous prior agreement."
                : "Choose a helper with a prior care agreement."
            : !choice.CanAcknowledge ? "This helper cannot currently provide the agreed care." : null;
        return new(FarmSemanticAction.AcknowledgeCare, scope, "Acknowledge care",
            choice?.HelperName ?? selected?.Name ?? "Care helper", choice?.HelperActorId ?? selected?.Id,
            FarmActionOffers.NoCost, "Care acknowledged; treatment has not yet been administered.",
            unavailable is null ? choice!.Intent : null, unavailable, detailText: detail);
    }

    private static FarmActionOffer Strike(LocalWorldView view, FarmActionScope scope, FarmTarget? selected)
    {
        LocalEncounterActionView? actions = view.EncounterActions;
        EntityId? targetId = selected?.Id;
        if (targetId is null)
        {
            ushort? playerSide = view.EncounterActors.FirstOrDefault(actor => actor.ActorId == view.PlayerActorId)?.Side;
            EntityId[] opponents = view.EncounterActors.Where(actor => actor.Side != 0 && actor.Side != playerSide)
                .Select(actor => actor.ActorId).ToArray();
            if (opponents.Length == 1) targetId = opponents[0];
        }
        LocalActorView? target = view.Actors.FirstOrDefault(actor => actor.ActorId == targetId);
        LocalEncounterAbilityOffer? ability = actions?.AbilityOffers.FirstOrDefault(offer => offer.TargetActorId == targetId);
        string? blocker = target is null ? "Choose a visible encounter target."
            : ability is null ? "You have no available attack against this target."
            : Blocker(ability.BlockedReason);
        return new(FarmSemanticAction.Strike, scope, ability?.DisplayName ?? "Strike", target?.DisplayName ?? selected?.Name ?? "No target",
            targetId, ability is null ? "Attack cost unavailable" : $"{ability.ManaCost} MP · {RecoveryCost(ability.ReadinessDelay)}",
            "Your attack was queued.", blocker is null ? new QueueEncounterAbilityIntent(actions!.EncounterId,
                actions.TimingRevision, ability!.AbilityDefinitionId, ability.TargetActorId) : null, blocker,
            detailText: RecoveryDetail);
    }

    private static FarmActionOffer Wait(LocalWorldView view, FarmActionScope scope)
    {
        LocalEncounterActionView? actions = view.EncounterActions;
        string? blocker = actions is null ? "This encounter is not ready for a choice." : Blocker(actions.WaitBlockedReason);
        return new(FarmSemanticAction.Wait, scope, "Wait", "Your readiness", null,
            actions is null ? "Wait cost unavailable" : RecoveryCost(actions.WaitDelay), "Your wait was queued.",
            blocker is null ? new QueueEncounterWaitIntent(actions!.EncounterId, actions.TimingRevision) : null, blocker,
            detailText: RecoveryDetail);
    }

    private const string RecoveryDetail = "Recovery uses advancing world time; time spent choosing does not pay it.";
    private static string RecoveryCost(ulong delay) => FormattableString.Invariant($"{delay / 100m:0.##} s recovery");

    private static FarmActionOffer Release(LocalWorldView view, FarmActionScope scope)
    {
        var token = view.EncounterLifecycle?.ReleaseActionHash;
        string? blocker = token is null ? "You cannot return to exploration from this outcome." : null;
        return new(FarmSemanticAction.ReleaseEncounter, scope, "Continue", "Exploration", null, FarmActionOffers.NoCost,
            "You returned to exploration; remaining recovery is retained.",
            token is null ? null : new ReleaseEncounterIntent(view.Encounter!.EncounterId, token), blocker);
    }

    private static string? Blocker(LocalEncounterActionBlockedReason reason) => reason switch
    {
        LocalEncounterActionBlockedReason.None => null,
        LocalEncounterActionBlockedReason.NotActive => "The confrontation is still being prepared.",
        LocalEncounterActionBlockedReason.Outcome => "This encounter has ended.",
        LocalEncounterActionBlockedReason.Incapacitated => "You are incapacitated and cannot choose an action.",
        LocalEncounterActionBlockedReason.Bystander => "You are not participating in this confrontation.",
        LocalEncounterActionBlockedReason.Recovering => "You are recovering; wait until you are ready.",
        LocalEncounterActionBlockedReason.DecisionQueued => "Your choice is already queued.",
        LocalEncounterActionBlockedReason.Retreating => "You are already retreating.",
        LocalEncounterActionBlockedReason.PreparingChoices => "Preparing this turn's choices…",
        LocalEncounterActionBlockedReason.Held => "The world is held; finish the current interaction first.",
        LocalEncounterActionBlockedReason.OutOfReach => "That target is out of reach.",
        _ => "This attack is unavailable against the selected target."
    };
}
