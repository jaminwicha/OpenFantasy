using Godot;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Explains authoritative encounter time and visible condition without owning either.</summary>
internal sealed class FarmEncounterStatus(Label label)
{
    private sealed record LocalRoadThreatViewLine(string Line, string Detail);

    internal void Present(LocalWorldView view, bool paused)
    {
        label.Visible = view.EncounterLifecycle is not null;
        if (view.EncounterLifecycle is not { } lifecycle) return;
        string status = lifecycle.Phase switch
        {
            EncounterLifecyclePhase.Exploration => lifecycle.PlayerCanAct
                ? "Explore freely · Select a wolf to inspect an encounter."
                : "Recovering · Exploration time restores readiness; injuries remain.",
            EncounterLifecyclePhase.Confrontation => "Encounter · Taking notice",
            EncounterLifecyclePhase.Staging => "Encounter · Holding positions",
            EncounterLifecyclePhase.Outcome => view.Care is { } care ? DescribeCare(care)
                : lifecycle.ReleaseActionHash is not null
                ? "Encounter over · Continue exploring when ready."
                : "Incapacitated · No aid is available in this encounter.",
            _ => view.Retreat?.Progress is { } retreat
                ? FormattableString.Invariant($"Retreating · step {retreat.SamplesTaken} of {retreat.RouteSamples}")
                : view.Encounter?.PlayerDecisionQueued == true ? "Choice queued · Waiting for resolution"
                : view.Encounter?.PlayerIsDue == true ? "Choose your action · World waiting"
                : "Recovering · World time continues until a choice is needed"
        };
        if (paused && view.Care is not null) status = "Paused · " + status;
        else if (paused) status = lifecycle.Phase == EncounterLifecyclePhase.Exploration
            ? "Paused · Exploration held" : "Paused · Encounter held";
        var own = view.EncounterActors.FirstOrDefault(actor => actor.ActorId == view.PlayerActorId);
        string opponents = string.Join(" · ", view.EncounterActors.Where(actor => actor.Side != 0 && actor.Side != own?.Side)
            .Select(actor => view.Actors.Single(person => person.ActorId == actor.ActorId).DisplayName
                + (actor.IsIncapacitated ? " · incapacitated" : $" · HP {actor.CurrentHp}/{actor.MaximumHp}")));
        // An observed hostile opportunity is announced before contact; reading it grants and sends nothing.
        LocalRoadThreatViewLine? danger = lifecycle.Phase == EncounterLifecyclePhase.Exploration
            && RoadThreatWarning.Urgent(view) is { } threat ? new(RoadThreatWarning.Line(threat), RoadThreatWarning.Detail(threat)) : null;
        label.Text = (danger is null ? "" : danger.Line + "\n") + status + (opponents.Length == 0 ? "" : "\n" + opponents);
        label.TooltipText = (danger is null ? "" : danger.Detail + "\n")
            + $"Remaining recovery: {lifecycle.PlayerRecoveryDelay / 100.0:0.##} seconds of advancing world time. "
            + "Reading an action does not spend it. A queued choice resolves through the host; closing a menu cannot undo it.";
    }

    private static string DescribeCare(LocalCareView care) => care.Status switch
    {
        LocalCareStatus.Available => "Incapacitated · Help is available. Inspect care to continue.",
        LocalCareStatus.Pending => $"{care.HelperName ?? "Your helper"} is preparing to help · World waiting",
        LocalCareStatus.NeedsAttention => "Care is interrupted · Help is still needed.",
        _ => "Incapacitated · Nearby help is unavailable."
    };
}
