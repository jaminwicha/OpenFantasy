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
        if (view.IsInEncounter)
        {
            if (view.EncounterLifecycle?.Phase == EncounterLifecyclePhase.Outcome || view.Encounter!.IsOutcome)
            {
                offers[FarmSemanticAction.ReleaseEncounter] = Release(view, scope);
            }
            else
            {
                offers[FarmSemanticAction.Strike] = Strike(view, scope, selected);
                offers[FarmSemanticAction.Wait] = Wait(view, scope);
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
        LocalEncounterActionBlockedReason.Held => "The world is held; finish the current interaction first.",
        LocalEncounterActionBlockedReason.OutOfReach => "That target is out of reach.",
        _ => "This attack is unavailable against the selected target."
    };
}
