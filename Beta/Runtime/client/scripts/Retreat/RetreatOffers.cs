using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Hosting.Simulation.Retreat;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>
/// The Retreat choice of the explicit retreat world, captured from one public offer: the exact route, destination,
/// timing revision and actor revision. Inspection explains where it leads, how long the walk takes, what readiness it
/// charges and why it is unavailable; confirming submits that captured route once. It never chooses a different route
/// or retries.
/// </summary>
internal static class RetreatOffers
{
    internal const string Detail = "Commit to the full route. Opponents act between steps; blocked ground stops you.";

    internal static FarmActionOffer Create(LocalControlledRetreatView retreat, FarmActionScope scope) =>
        Create(retreat.Offer, retreat.Progress, retreat.Withdrawal, scope);

    internal static FarmActionOffer Create(LocalRetreatView retreat, FarmActionScope scope) =>
        Create(retreat.Offer, retreat.Progress, retreat.Withdrawal, scope);

    private static FarmActionOffer Create(LocalRetreatOffer? offered, LocalRetreatProgress? progress,
        LocalWithdrawalView? withdrawal, FarmActionScope scope)
    {
        string detail = Detail + PartyStatus(withdrawal);
        if (withdrawal?.Choice is { } choice)
        {
            string status = choice.HasArrived ? "Reached destination; waiting for the party."
                : FormattableString.Invariant($"Step {choice.SamplesTaken} of {choice.RouteSamples} walked.");
            return new FarmActionOffer(FarmSemanticAction.Retreat, scope, "Stop retreat",
                Destination(choice.Origin, choice.Destination), null, "No new cost; paid recovery remains",
                "Your withdrawal choice ended; paid recovery remains.", withdrawal.Cancellation,
                withdrawal.Cancellation is null ? "This choice cannot be cancelled at the current boundary." : null,
                detailText: status + PartyStatus(withdrawal)
                    + " Stopping ends only your choice. It does not move you or refund recovery."
                    + (choice.HasArrived ? " When ready, choose Wait to keep your arrival, or another action to end it." : ""));
        }
        if (progress is not null)
        {
            return new FarmActionOffer(FarmSemanticAction.Retreat, scope, "Retreat", Destination(progress.Origin, progress.Destination),
                null, FormattableString.Invariant($"Step {progress.SamplesTaken} of {progress.RouteSamples}"), "", null,
                FormattableString.Invariant($"Retreating: step {progress.SamplesTaken} of {progress.RouteSamples} walked."),
                detailText: detail);
        }
        if (offered is not { } offer)
        {
            return new FarmActionOffer(FarmSemanticAction.Retreat, scope, "Retreat", "No retreat", null, "Retreat unavailable", "",
                null, "This encounter is not ready for a choice.", detailText: detail);
        }
        string? blocker = Blocker(offer);
        string reach = offer.OpponentsInReach == 0 ? "No opponent reaches you now."
            : FormattableString.Invariant($"{offer.OpponentsInReach} opponent(s) can reach you now.");
        bool groundBlocked = offer.BlockedReason is LocalRetreatBlockedReason.NoClearGround or LocalRetreatBlockedReason.SearchLimit;
        return new FarmActionOffer(FarmSemanticAction.Retreat, scope, "Retreat",
            offer.CanQueue ? Destination(offer.Origin, offer.Destination) : groundBlocked ? "No safe ground" : "Retreat unavailable", null,
            offer.CanQueue ? Steps(offer) : FormattableString.Invariant($"{Seconds(offer.SampleDelay)} s readiness per step"),
            "Your retreat was committed; each step is walked at your own turns.",
            blocker is null ? new QueueEncounterRetreatIntent(offer.EncounterId, offer.TimingRevision, offer.Destination, offer.Route) : null,
            blocker, detailText: detail + " " + (offer.CanQueue ? Charges(offer) : reach));
    }

    private static string PartyStatus(LocalWithdrawalView? withdrawal)
    {
        if (withdrawal is null || withdrawal.Members.Count == 0) return "";
        int Count(LocalWithdrawalReadiness state) => withdrawal.Members.Count(member => member.Readiness == state);
        return FormattableString.Invariant($" Party: {Count(LocalWithdrawalReadiness.Arrived)}/{withdrawal.Members.Count} arrived, {Count(LocalWithdrawalReadiness.Walking)} walking, {Count(LocalWithdrawalReadiness.NeedsChoice)} need a route, {Count(LocalWithdrawalReadiness.Incapacitated)} incapacitated. Everyone must arrive and be clear of opponents.");
    }

    /// <summary>Destination named by its direction and cell offset from where the retreat starts.</summary>
    internal static string Destination(SubunitPosition origin, SubunitPosition destination)
    {
        long dx = destination.X - origin.X;
        long dy = destination.Y - origin.Y;
        string vertical = dy < 0 ? "north" : dy > 0 ? "south" : "";
        string horizontal = dx > 0 ? "east" : dx < 0 ? "west" : "";
        string direction = vertical.Length != 0 && horizontal.Length != 0 ? vertical + "-" + horizontal : vertical + horizontal;
        return FormattableString.Invariant($"To cell ({destination.Cell.X}, {destination.Cell.Y}), {direction}");
    }

    /// <summary>The walk's tick-quantised duration and the recovery still owed when it ends.</summary>
    internal static string Steps(LocalRetreatOffer offer) => FormattableString.Invariant(
        $"{offer.Samples} steps · {Seconds(offer.WalkingUnits)} s walk · {Seconds(offer.ExitRecovery)} s recovery");

    /// <summary>The readiness every step charges, which opponents' turns run against.</summary>
    internal static string Charges(LocalRetreatOffer offer) => FormattableString.Invariant(
        $"Readiness: {Seconds(offer.SampleDelay)} s/step; {Seconds(offer.TotalDelay)} s total.");

    private static string Seconds(ulong units) => FormattableString.Invariant($"{units / 100m:0.##}");

    private static string? Blocker(LocalRetreatOffer offer) => offer.BlockedReason switch
    {
        LocalRetreatBlockedReason.None => null,
        LocalRetreatBlockedReason.NotActive => "The confrontation is still being prepared.",
        LocalRetreatBlockedReason.Outcome => "This encounter has ended.",
        LocalRetreatBlockedReason.Held => "The world is held; finish the current interaction first.",
        LocalRetreatBlockedReason.Incapacitated => "You are incapacitated and cannot retreat.",
        LocalRetreatBlockedReason.Bystander => "You are not participating in this confrontation.",
        LocalRetreatBlockedReason.Recovering => "You are recovering; wait until you are ready.",
        LocalRetreatBlockedReason.DecisionQueued => "Your choice is already queued.",
        LocalRetreatBlockedReason.PreparingChoices => "Preparing this turn's choices…",
        LocalRetreatBlockedReason.Arrived => "You reached your destination; the complete party must arrive and clear contact.",
        LocalRetreatBlockedReason.Retreating => "You are already retreating.",
        LocalRetreatBlockedReason.Committed => "Another combatant is withdrawing; only one retreat can be under way.",
        LocalRetreatBlockedReason.AllyRemains => "Retreat is only for a lone combatant against one other side; an ally would be left behind.",
        LocalRetreatBlockedReason.NoGroundLocomotion => "You cannot walk, so you cannot retreat.",
        LocalRetreatBlockedReason.SearchLimit => "No retreat was found within the search bound.",
        _ => "No walkable ground nearby is out of every opponent's reach."
    };

    /// <summary>The one message announcing the observer's newly ended retreat.</summary>
    internal static string Describe(LocalRetreatRecord record) => record.Status switch
    {
        EncounterRetreatStatus.Disengaged => FormattableString.Invariant(
            $"You broke away after {record.SamplesTaken} steps. The encounter is over; recovery continues while you explore."),
        EncounterRetreatStatus.Interrupted => FormattableString.Invariant(
            $"Your retreat stopped after {record.SamplesTaken} of {record.RouteSamples} steps: {Reason(record.Reason)} Choose again."),
        _ => FormattableString.Invariant($"Your retreat ended after {record.SamplesTaken} of {record.RouteSamples} steps: {Reason(record.Reason)}")
    };

    internal static string Describe(LocalWithdrawalRecord record) => record.Reason switch
    {
        EncounterWithdrawalEndReason.Disengaged => "Your party broke away. Recovery continues while you explore.",
        EncounterWithdrawalEndReason.ChoiceChanged => "Your withdrawal choice ended. Paid recovery remains.",
        EncounterWithdrawalEndReason.Incapacitated => "Your retreat stopped because you were incapacitated; your party has not escaped.",
        EncounterWithdrawalEndReason.EncounterEnded => "Your retreat ended with the encounter.",
        EncounterWithdrawalEndReason.Blocked => "Your next step was blocked. Its recovery was charged; choose again when ready.",
        EncounterWithdrawalEndReason.Unadmitted => "Your next step could not be entered. Its recovery was charged; choose again when ready.",
        EncounterWithdrawalEndReason.NoGroundLocomotion => "Your retreat stopped because you can no longer walk.",
        EncounterWithdrawalEndReason.ContactRemains => "Your retreat stopped because an opponent can still reach you.",
        _ => "Your retreat ended. Already paid recovery remains."
    };

    private static string Reason(EncounterRetreatReason reason) => reason switch
    {
        EncounterRetreatReason.Blocked => "the next ground is blocked.",
        EncounterRetreatReason.Unadmitted => "the next ground cannot be entered.",
        EncounterRetreatReason.ControlLost => "you no longer control this character.",
        EncounterRetreatReason.NoGroundLocomotion => "you can no longer walk.",
        EncounterRetreatReason.ContactRemains => "an opponent can still reach you.",
        EncounterRetreatReason.AllyRemains => "an ally would be left behind.",
        EncounterRetreatReason.Incapacitated => "you were incapacitated.",
        EncounterRetreatReason.EncounterOutcome => "the encounter was decided.",
        _ => "it ended."
    };
}

/// <summary>Announces each newly ended retreat of the observer exactly once; a record present at opening is not news.</summary>
internal sealed class RetreatOutcomeNotice
{
    private ulong? _seen;

    internal string? Observe(LocalRetreatView? retreat)
    {
        ulong? current = retreat?.Withdrawal?.Last?.WorldSequence ?? retreat?.Last?.WorldSequence;
        if (_seen is null)
        {
            _seen = current ?? 0;
            return null;
        }
        if (current is not { } sequence || sequence == _seen) return null;
        _seen = sequence;
        return retreat?.Withdrawal?.Last is { } collective ? RetreatOffers.Describe(collective) : RetreatOffers.Describe(retreat!.Last!);
    }
}
