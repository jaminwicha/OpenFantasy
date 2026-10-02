using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.EncounterOutcome;

namespace OpenFantasy.Client;

/// <summary>
/// Player-facing wording of the profile-35 outcome projection. It reads only the public views and states only what they
/// project: every disabled choice carries its reason, care is named only when the care view admits it, a missing care
/// view means only that no care is offered (not that no agreement exists), and a pending request is the patient's own,
/// never a helper's commitment. Accepting an outcome asks the world to continue; the world completes the encounter at its
/// next ready boundary once every required player has read it and no care is pending, which closes aftermath care, so
/// care is chosen before accepting. Compact forms fit the HUD's status, hint and inline preview; the outcome card and
/// tooltips carry the full explanations. Nothing here grants or sends anything.
/// </summary>
internal static class EncounterOutcomeText
{
    /// <summary>The footer hint after a completed defeat; the card carries the full explanation and Save and leave.</summary>
    internal const string TerminalHint = "Incapacitated\nEsc: pause";

    /// <summary>The completion policy that makes a completed defeat terminal (not a statement about physical reach).</summary>
    internal const string CompletionPolicy = "This encounter is complete: aftermath care applies only before its outcome is "
        + "accepted, and ordinary care cannot treat an incapacitated (0 HP) character";

    /// <summary>The completion condition every statement about accepting carries.</summary>
    internal const string CompletionCondition = "once every required player has read it and no care is pending";

    /// <summary>How the reading beside offered care is chosen, since E / A then asks for care.</summary>
    internal const string ReadingBesideCareRoutes = "E / A asks for care. Choose this with the pointer, keyboard focus or the "
        + "Actions wheel (V).";

    internal static string Title(LocalEncounterOutcomeView outcome) => outcome.Standing switch
    {
        LocalOutcomeStanding.YourSideStands => "Victory",
        LocalOutcomeStanding.OpponentStands => "Defeated",
        LocalOutcomeStanding.NoSideStands => "No side is standing",
        _ => outcome.Result == EncounterOutcomeResult.NoSurvivors ? "No side is standing" : "Encounter decided"
    };

    /// <summary>The primary deliberate choice: accepting the result (it never heals, rewards or moves anyone).</summary>
    internal static string ReadLabel(LocalEncounterOutcomeView outcome) => outcome.Standing switch
    {
        LocalOutcomeStanding.OpponentStands or LocalOutcomeStanding.NoSideStands => "Accept defeat",
        LocalOutcomeStanding.YourSideStands when outcome.Restriction == LocalOutcomeRestriction.Incapacitated => "Accept outcome",
        _ => "Continue"
    };

    internal static string? ReadBlocker(LocalEncounterOutcomeView outcome, LocalCareView? care = null)
    {
        if (outcome.AcknowledgmentMode == LocalOutcomeAcknowledgmentMode.ControllerAcceptance)
            return ControllerOutcomeText.Blocker(outcome, care);
        if (outcome.Acknowledgment is not null) return null;
        if (!outcome.ObserverIsParticipant)
        {
            return outcome.Stage == LocalOutcomeStage.Completed ? "This encounter between others is complete."
                : "This fight is between others: you have no reading to give. The world waits for its players to read the result.";
        }
        return outcome.Stage switch
        {
            LocalOutcomeStage.AwaitingOthers => outcome.ReadersRemaining == 1
                ? "You have read this outcome. Waiting for one other player to read it."
                : $"You have read this outcome. Waiting for {outcome.ReadersRemaining} other players to read it.",
            LocalOutcomeStage.CarePending => PendingCare(care),
            LocalOutcomeStage.Completing => "Every player has read this outcome. The world is completing the encounter.",
            LocalOutcomeStage.Completed => "This encounter is complete.",
            _ => "This outcome is not awaiting your reading."
        };
    }

    /// <summary>The concise acceptance consequence for the wheel; the outcome card carries the full explanation.</summary>
    internal static string ChoiceDetail(LocalEncounterOutcomeView outcome, LocalCareView? care)
    {
        if (outcome.AcknowledgmentMode == LocalOutcomeAcknowledgmentMode.ControllerAcceptance)
            return ControllerOutcomeText.ChoiceDetail(outcome, care);
        string consequence = outcome.Restriction == LocalOutcomeRestriction.Incapacitated
            ? "Ends this fight after all required readings, with no pending care. You stay incapacitated; your save is kept."
            : "Returns to exploration after all required readings, with no pending care. Injuries and recovery remain; no rewards or loot.";
        return AvailableHelper(care) is { } helper
            ? consequence + $" {helper} can help now; accepting declines that care."
            : consequence;
    }

    /// <summary>The full explanation of accepting, shown on the outcome card.</summary>
    internal static string ReadDetail(LocalEncounterOutcomeView outcome, LocalCareView? care = null)
    {
        if (outcome.AcknowledgmentMode == LocalOutcomeAcknowledgmentMode.ControllerAcceptance)
            return ControllerOutcomeText.Detail(outcome, care);
        string detail = outcome.Standing switch
        {
            LocalOutcomeStanding.Observer => "This fight is between others; you have no reading to give. The world completes it "
                + "once its players have read the result.",
            LocalOutcomeStanding.YourSideStands when outcome.Restriction == LocalOutcomeRestriction.Incapacitated =>
                "Your side won, but you are incapacitated (0 HP). Accepting lets the world end this fight "
                + CompletionCondition + "; you stay unable to move or act and nothing heals, revives or rewards you. "
                + "Your save is kept.",
            LocalOutcomeStanding.YourSideStands => "Your side is still standing. Continuing returns you to exploration "
                + CompletionCondition + ". Injuries and recovery remain; nothing is rewarded or looted.",
            LocalOutcomeStanding.OpponentStands => "You are incapacitated (0 HP) and cannot move or act. Accepting the defeat lets "
                + "the world end this fight once every required player has read it; you stay incapacitated afterwards. Your save is kept.",
            _ => "Every combatant is incapacitated. Accepting lets the world end the fight once every required player has read it; "
                + "nobody is revived or rewarded."
        };
        return AvailableHelper(care) is { } helper && outcome.ObserverIsParticipant
            ? detail + $" {helper} can help now: ask for care first. Accepting declines that care; the world completes the "
                + $"encounter at its next ready boundary {CompletionCondition}."
            : detail;
    }

    /// <summary>The compact encounter status line at an unfinalized outcome (HUD and card).</summary>
    internal static string Status(LocalEncounterOutcomeView outcome, LocalCareView? care)
    {
        if (outcome.AcknowledgmentMode == LocalOutcomeAcknowledgmentMode.ControllerAcceptance)
            return ControllerOutcomeText.Status(outcome, care);
        string title = Title(outcome);
        if (!outcome.ObserverIsParticipant) return $"{title} · This fight is between others · World waiting for its players";
        if (outcome.Stage == LocalOutcomeStage.CarePending)
        {
            string requested = care?.HelperName is { } named ? "Care requested from " + named : "Care requested";
            return care?.Status switch
            {
                LocalCareStatus.Unavailable => $"{title} · {requested} · The requested care is unavailable now · The fight stays open",
                LocalCareStatus.NeedsAttention => $"{title} · {requested} · Treatment needs attention · World waiting",
                _ => $"{title} · {requested} · Treatment can proceed · World waiting"
            };
        }
        string reason = outcome.Restriction == LocalOutcomeRestriction.Incapacitated
            ? "You are incapacitated (0 HP) and cannot move or act"
            : "World waiting while you read the result";
        string aid = outcome.Restriction == LocalOutcomeRestriction.Incapacitated ? " · " + CareReason(care) : "";
        string waiting = outcome.Stage == LocalOutcomeStage.AwaitingOthers ? " · Waiting for other players" : "";
        return $"{title} · {reason}{aid}{waiting}";
    }

    /// <summary>Why care is or is not offered to the incapacitated observer, exactly as far as the care view states it.</summary>
    internal static string CareReason(LocalCareView? care)
    {
        // A missing or empty view only shows that nothing is offered here; it does not establish that no agreement exists.
        if (care is null || care.Offers.Count == 0 && care.PendingAcknowledgmentId is null) return "No care is offered to you";
        if (AvailableHelper(care) is { } helper) return $"{helper} can help (care)";
        if (care.PendingAcknowledgmentId is not null) return care.HelperName is { } named ? "Care requested from " + named : "Care requested";
        LocalCareAcknowledgmentView refused = care.Offers[0];
        return refused.Rejection switch
        {
            WorldActionRejection.OutOfReach => $"{refused.HelperName} is out of reach",
            WorldActionRejection.RevisionConflict => $"{refused.HelperName}'s care offer is out of date",
            WorldActionRejection.NotAuthorized => $"{refused.HelperName}'s care does not cover you",
            _ => $"{refused.HelperName} cannot treat you now"
        };
    }

    /// <summary>
    /// The two short footer hint lines at an unfinalized outcome. The keyboard E / pad A choice is named exactly: care when
    /// care is offered, otherwise the reading. V only opens the choices; the reading beside care is committed on its own
    /// button or wheel sector, whose inspection states what accepting declines.
    /// </summary>
    internal static string Hint(LocalEncounterOutcomeView outcome, LocalCareView? care)
    {
        bool careOffered = care is not null && (care.PendingAcknowledgmentId is not null || care.Offers.Any(offer => offer.CanAcknowledge));
        if (outcome.Acknowledgment is null) return Title(outcome) + "\n" + CompactBlocker(outcome);
        return careOffered
            ? $"{Title(outcome)}\nE / A: care · V: choices"
            : $"{Title(outcome)}\nE / A: {ReadLabel(outcome).ToLowerInvariant()}";
    }

    /// <summary>The full exploration explanation after a completed defeat, shown on the outcome card.</summary>
    internal static string Terminal(LocalEncounterOutcomeView outcome) =>
        $"{Title(outcome)} · You are incapacitated (0 HP): no movement or action is possible · {CompletionPolicy} "
        + "· Save and leave keeps this world";

    /// <summary>The compact HUD status after a completed defeat; its tooltip and the card carry <see cref="Terminal"/>.</summary>
    internal static string TerminalStatus(LocalEncounterOutcomeView outcome) =>
        $"{Title(outcome)} · Incapacitated (0 HP) · Encounter complete";

    /// <summary>
    /// The outcome card's full body. At a completed defeat: the terminal explanation and participants. Otherwise: the
    /// participants, the reading's full explanation (or why it is unavailable) and, for an incapacitated participant
    /// without admitted care, the care view's own reason. The HUD status line carries the compact form of the same facts.
    /// </summary>
    internal static string Card(LocalEncounterOutcomeView outcome, LocalCareView? care)
    {
        string participants = string.Join("\n", outcome.Participants.Select(actor => Participant(actor, outcome.Result)));
        if (outcome.IsTerminal) return Terminal(outcome) + "\n" + participants;
        bool careReason = outcome.ObserverIsParticipant && outcome.Restriction == LocalOutcomeRestriction.Incapacitated
            && outcome.Stage != LocalOutcomeStage.CarePending && AvailableHelper(care) is null;
        return (participants.Length == 0 ? "" : participants + "\n") + (ReadBlocker(outcome, care) ?? ReadDetail(outcome, care))
            + (careReason ? "\nCare: " + CareReason(care) + "." : "");
    }

    /// <summary>One participant: an outcome fact (its side's standing) and present condition only where it is observed.</summary>
    internal static string Participant(LocalOutcomeParticipantView actor, EncounterOutcomeResult result)
    {
        string name = actor.IsObserver ? "You" : actor.Name;
        string side = result == EncounterOutcomeResult.NoSurvivors ? "no side standing"
            : actor.OnStandingSide ? "standing side" : "defeated side";
        string condition = actor.Condition is not { } now ? "condition not in view"
            : now.IsIncapacitated ? "incapacitated (0 HP)" : $"HP {now.CurrentHp}/{now.MaxHp}";
        return $"{name} · {side} · {condition}";
    }

    /// <summary>
    /// The one-line inline preview of an outcome or care choice; the control's tooltip, the wheel inspector and the outcome
    /// card carry its full explanation.
    /// </summary>
    internal static string InlinePreview(FarmActionOffer offer) =>
        $"{offer.Label}: {offer.TargetName} · {offer.CostText}" + (offer.CanExecute ? "" : " · Unavailable: see the outcome card");

    /// <summary>Whether a reading is presented beside care: E / A then asks for care, so the reading control names no key.</summary>
    internal static bool ReadingBesideCare(IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> offers) =>
        offers.ContainsKey(FarmSemanticAction.AcknowledgeCare) && offers.ContainsKey(FarmSemanticAction.ReleaseEncounter);

    private static string CompactBlocker(LocalEncounterOutcomeView outcome)
    {
        if (outcome.AcknowledgmentMode == LocalOutcomeAcknowledgmentMode.ControllerAcceptance)
            return ControllerOutcomeText.CompactBlocker(outcome);
        if (!outcome.ObserverIsParticipant) return "Between others · nothing to read";
        return outcome.Stage switch
        {
            LocalOutcomeStage.AwaitingOthers => "Read · waiting for others",
            LocalOutcomeStage.CarePending => "Care requested",
            LocalOutcomeStage.Completing => "Completing",
            LocalOutcomeStage.Completed => "Complete",
            _ => "Nothing to read now"
        };
    }

    private static string? AvailableHelper(LocalCareView? care) =>
        care?.Offers.FirstOrDefault(offer => offer.CanAcknowledge)?.HelperName;

    private static string PendingCare(LocalCareView? care)
    {
        string requested = care?.HelperName is { } named ? "You requested care from " + named : "You requested care";
        return care?.Status switch
        {
            LocalCareStatus.Unavailable => $"{requested}; the requested care is unavailable now. The fight stays open, unfinished, "
                + "while that request is pending.",
            LocalCareStatus.NeedsAttention => $"{requested}; the treatment needs attention before it can proceed.",
            _ => $"{requested}: if that treatment happens it resumes this same fight, so the fight is not over."
        };
    }
}

/// <summary>
/// Controller-world wording describes the character's recorded acceptance, never the current person's reading or
/// authorship of a care request. A current human assignment cannot establish who submitted a historical acknowledgment.
/// </summary>
internal static class ControllerOutcomeText
{
    private const string RecoveryExplanation = "Your character remains incapacitated (0 HP). A capable helper with your prior recovery permission can treat them after this fight, using medicine and actual work. Rescue is not guaranteed; accepting does not heal or move anyone.";
    private const string CompletionCondition = "once every required character has acknowledged it and no care is pending";

    internal static string? Blocker(LocalEncounterOutcomeView outcome, LocalCareView? care)
    {
        if (outcome.Stage == LocalOutcomeStage.Completed && outcome.AllowsAssistedRecovery
            && outcome.ObserverIsParticipant && outcome.Restriction == LocalOutcomeRestriction.Incapacitated)
            return RecoveryExplanation;
        if (outcome.Acknowledgment is not null) return null;
        if (!outcome.ObserverIsParticipant)
        {
            return outcome.Stage == LocalOutcomeStage.Completed ? "This encounter between others is complete."
                : "This fight is between others: your character has no acknowledgment to give. "
                    + "The world waits for the required characters' acceptance.";
        }
        return outcome.Stage switch
        {
            LocalOutcomeStage.AwaitingOthers => (outcome.ObserverHasRead
                ? "Your character's acknowledgment is recorded. " : "The outcome is awaiting acceptance. ")
                + (outcome.ReadersRemaining == 1 ? "Waiting for one remaining character's acknowledgment."
                    : $"Waiting for {outcome.ReadersRemaining} remaining characters' acknowledgments."),
            LocalOutcomeStage.CarePending => PendingCare(care),
            LocalOutcomeStage.Completing => "Every required character's acknowledgment is recorded. The world is completing the encounter.",
            LocalOutcomeStage.Completed => outcome.ObserverHasRead
                ? "This encounter is complete. Your character's acknowledgment is recorded."
                : "This encounter is complete.",
            _ => "This outcome is not awaiting your character's acknowledgment."
        };
    }

    internal static string ChoiceDetail(LocalEncounterOutcomeView outcome, LocalCareView? care)
    {
        string consequence = outcome.AllowsAssistedRecovery && outcome.Restriction == LocalOutcomeRestriction.Incapacitated
            ? "Ends this fight after all required acknowledgments, with no pending care. You stay incapacitated; separately permitted aid can still restore HP later."
            : outcome.Restriction == LocalOutcomeRestriction.Incapacitated
            ? "Ends this fight after all required acknowledgments, with no pending care. Your character stays incapacitated; your save is kept."
            : "Returns to exploration after all required acknowledgments, with no pending care. Injuries and recovery remain; no rewards or loot.";
        return AvailableHelper(care) is { } helper
            ? consequence + $" {helper} can help now; accepting declines that care." : consequence;
    }

    internal static string Detail(LocalEncounterOutcomeView outcome, LocalCareView? care)
    {
        string detail = outcome.AllowsAssistedRecovery && outcome.ObserverIsParticipant
            && outcome.Restriction == LocalOutcomeRestriction.Incapacitated
            ? RecoveryExplanation + $" The encounter ends {CompletionCondition}."
            : outcome.Standing switch
        {
            LocalOutcomeStanding.Observer => "This fight is between others; your character has no acknowledgment to give. "
                + $"The world completes it {CompletionCondition}.",
            LocalOutcomeStanding.YourSideStands when outcome.Restriction == LocalOutcomeRestriction.Incapacitated =>
                "Your side won, but your character is incapacitated (0 HP). Accepting lets the world end this fight "
                + CompletionCondition + "; your character stays unable to move or act and nothing heals, revives or rewards them. Your save is kept.",
            LocalOutcomeStanding.YourSideStands => "Your side is still standing. Accepting returns your character to exploration "
                + CompletionCondition + ". Injuries and recovery remain; nothing is rewarded or looted.",
            LocalOutcomeStanding.OpponentStands => "Your character is incapacitated (0 HP) and cannot move or act. Accepting the defeat lets "
                + "the world end this fight " + CompletionCondition + "; your character stays incapacitated afterwards. Your save is kept.",
            _ => "Every combatant is incapacitated. Accepting lets the world end the fight " + CompletionCondition
                + "; nobody is revived or rewarded."
        };
        return AvailableHelper(care) is { } helper && outcome.ObserverIsParticipant
            ? detail + $" {helper} can help now: ask for care first. Accepting declines that care; the world completes the "
                + $"encounter at its next ready boundary {CompletionCondition}." : detail;
    }

    internal static string Status(LocalEncounterOutcomeView outcome, LocalCareView? care)
    {
        string title = EncounterOutcomeText.Title(outcome);
        if (outcome.Stage == LocalOutcomeStage.Completed) return outcome.AllowsAssistedRecovery
                && outcome.Restriction == LocalOutcomeRestriction.Incapacitated
            ? $"{title} · Encounter complete · Incapacitated · Permitted aid can restore HP"
            : $"{title} · Encounter complete";
        if (!outcome.ObserverIsParticipant) return $"{title} · This fight is between others · World waiting for acceptance";
        if (outcome.Stage == LocalOutcomeStage.CarePending)
        {
            string requested = care?.HelperName is { } named ? "Care requested from " + named : "Care requested";
            return care?.Status switch
            {
                LocalCareStatus.Unavailable => $"{title} · {requested} · The requested care is unavailable now · The fight stays open",
                LocalCareStatus.NeedsAttention => $"{title} · {requested} · Treatment needs attention · World waiting",
                _ => $"{title} · {requested} · Treatment pending · World waiting"
            };
        }
        if (outcome.Stage == LocalOutcomeStage.Completing) return $"{title} · Acknowledgments recorded · Completing encounter";
        string reason = outcome.Restriction == LocalOutcomeRestriction.Incapacitated
            ? "Your character is incapacitated (0 HP) and cannot move or act"
            : outcome.ObserverHasRead ? "Your character's acknowledgment is recorded" : "World waiting for your character's acceptance";
        string aid = outcome.Restriction == LocalOutcomeRestriction.Incapacitated ? " · " + EncounterOutcomeText.CareReason(care) : "";
        string waiting = outcome.Stage == LocalOutcomeStage.AwaitingOthers ? " · Waiting for remaining acknowledgments" : "";
        return $"{title} · {reason}{aid}{waiting}";
    }

    internal static string CompactBlocker(LocalEncounterOutcomeView outcome)
    {
        if (!outcome.ObserverIsParticipant) return "Between others · no acknowledgment needed";
        return outcome.Stage switch
        {
            LocalOutcomeStage.AwaitingOthers => "Waiting for remaining acknowledgments",
            LocalOutcomeStage.CarePending => "Care requested",
            LocalOutcomeStage.Completing => "Completing",
            LocalOutcomeStage.Completed => "Complete",
            _ => "No acknowledgment available now"
        };
    }

    private static string? AvailableHelper(LocalCareView? care) =>
        care?.Offers.FirstOrDefault(offer => offer.CanAcknowledge)?.HelperName;

    private static string PendingCare(LocalCareView? care)
    {
        string requested = care?.HelperName is { } named ? "Care was requested from " + named : "Care was requested";
        return care?.Status switch
        {
            LocalCareStatus.Unavailable => $"{requested}; the requested care is unavailable now. The fight stays open, unfinished, "
                + "while that request is pending.",
            LocalCareStatus.NeedsAttention => $"{requested}; the treatment needs attention before it can proceed.",
            _ => $"{requested}: if that treatment happens it resumes this same fight, so the fight is not over."
        };
    }
}
