using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.RoadThreat;

namespace OpenFantasy.Client;

/// <summary>
/// Player-facing words for an observed hostile opportunity: a short warning line for the status area and an
/// inspectable detail for its tooltip. Both read only the published threat view, which carries only threats the player
/// currently sees (three cells, same floor, clear sightline); they grant and submit nothing.
/// </summary>
internal static class RoadThreatWarning
{
    /// <summary>The single most urgent observed threat in exploration, or null when none is in view.</summary>
    internal static LocalRoadThreatView? Urgent(LocalWorldView view) => (view.RoadThreats ?? [])
        .Where(threat => threat.State is LocalRoadThreatState.Watching or LocalRoadThreatState.InReach or LocalRoadThreatState.Recovering)
        .OrderBy(threat => threat.State == LocalRoadThreatState.InReach ? 0 : 1).ThenBy(threat => threat.DistanceSubunits)
        .FirstOrDefault();

    internal static string Line(LocalRoadThreatView threat) => threat.State switch
    {
        LocalRoadThreatState.InReach => $"Danger · Within the {threat.Name}'s reach — it attacks now",
        LocalRoadThreatState.Recovering => $"Danger · The {threat.Name} is catching its breath within reach",
        _ => FormattableString.Invariant(
            $"Danger nearby · {threat.Name} guards the road · attacks within {threat.ReachCells:0.#} cells")
    };

    /// <summary>The scenario targets only the observing player, so the words say it attacks you, not anyone.</summary>
    internal static string Detail(LocalRoadThreatView threat) =>
        FormattableString.Invariant($"{threat.Name} · level {threat.Level} · HP {threat.CurrentHp}/{threat.MaximumHp}. ")
        + FormattableString.Invariant($"It attacks you when you come within {threat.ReachCells:0.##} cells in its sight, ")
        + FormattableString.Invariant($"and is {threat.DistanceCells:0.#} cells away now. It does not chase: ")
        + "walk around it outside that reach, or retreat if it engages you. An early traveller cannot expect to win.";
}
