using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation.RoadThreat;

/// <summary>What the observer can read about one observed hostile opportunity right now.</summary>
public enum LocalRoadThreatState
{
    /// <summary>Observed beyond its entry reach: passing at this distance is safe.</summary>
    Watching,
    /// <summary>Observed within its reach while it is ready: it attacks at the next unheld exploration cutoff.</summary>
    InReach,
    /// <summary>Observed within reach while still paying readiness debt, so it cannot begin a new confrontation yet.</summary>
    Recovering,
    /// <summary>This opportunity's episode is the current encounter.</summary>
    Engaged,
    /// <summary>The initiator or the observer cannot take part (zero HP).</summary>
    Incapacitated
}

/// <summary>
/// A public warning about one admitted hostile opportunity against the observer that the observer can actually see:
/// who, how strong, the reach it attacks within, how far away its body is and whether contact would begin now. It is
/// derived from the same published root as the rest of the view, uses the authority's own interaction query for
/// observation, reach and sightline, and carries no command, episode token, random state or private inventory.
/// Reading it grants nothing; the host and authority decide.
/// </summary>
public sealed record LocalRoadThreatView(EntityId ActorId, string Name, int Level, int CurrentHp, int MaximumHp,
    EntityId ScenarioId, string ScenarioName, long ReachSubunits, long DistanceSubunits, LocalRoadThreatState State)
{
    /// <summary>
    /// The supported observation bound: the existing interaction contract (same floor, centre range and clear centre
    /// sightline within 768 subunits, three cells). It is not the regional terrain observer's radius, and nothing beyond
    /// it is disclosed: an unseen initiator's name, level, HP, distance and readiness stay private.
    /// </summary>
    public const long ObservationRangeSubunits = LocalSpatialSnapshot.InteractionRangeSubunits;

    /// <summary>The reach in whole and fractional cells, for presentation.</summary>
    public double ReachCells => ReachSubunits / (double)SubunitPosition.SubunitsPerCell;

    public double DistanceCells => DistanceSubunits / (double)SubunitPosition.SubunitsPerCell;

    /// <summary>
    /// The observer's observed hostile opportunities (possibly none), or null when the world admits no hostile
    /// opportunity against the observer at all; the public view then omits the field, keeping earlier worlds' bytes.
    /// </summary>
    internal static IReadOnlyList<LocalRoadThreatView>? Project(WorldSnapshot state, EntityId observerActorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        IReadOnlyList<EncounterScenarioDefinition> scenarios = LocalRoadThreatController.HostileScenarios(state, observerActorId);
        if (scenarios.Count == 0) return null;
        ActorPresence observer = state.Space.GetActor(observerActorId);
        List<LocalRoadThreatView> views = [];
        foreach (EncounterScenarioDefinition scenario in scenarios)
        {
            if (!Observes(state.Space, observerActorId, scenario.InitiatingActorId)) continue;
            ActorPresence initiator = state.Space.GetActor(scenario.InitiatingActorId);
            var resources = state.Progression!.GetActor(scenario.InitiatingActorId);
            // Observation requires one floor, so the horizontal centre distance is the whole separation.
            long dx = initiator.Feet.X - observer.Feet.X;
            long dy = initiator.Feet.Y - observer.Feet.Y;
            long distance = (long)Math.Floor(Math.Sqrt((double)dx * dx + (double)dy * dy));
            views.Add(new LocalRoadThreatView(scenario.InitiatingActorId, state.GetResident(scenario.InitiatingActorId).DisplayName,
                resources.Level, resources.CurrentHp, resources.Stats.MaxHp, scenario.DefinitionId, scenario.DisplayName,
                scenario.EntryRangeSubunits, distance, Classify(state, scenario, observerActorId)));
        }
        return views.AsReadOnly();
    }

    /// <summary>Whether the observer currently sees the other actor under the supported observation bound.</summary>
    internal static bool Observes(LocalSpatialSnapshot space, EntityId observerActorId, EntityId observedActorId)
    {
        ArgumentNullException.ThrowIfNull(space);
        return space.QueryInteraction(observerActorId, observedActorId, ObservationRangeSubunits) == SpatialInteraction.Reachable;
    }

    private static LocalRoadThreatState Classify(WorldSnapshot state, EncounterScenarioDefinition scenario, EntityId observer)
    {
        EncounterLifecycleSnapshot lifecycle = state.EncounterLifecycle!;
        if (lifecycle.HasEncounter && lifecycle.ScenarioId == scenario.DefinitionId) return LocalRoadThreatState.Engaged;
        if (!WorldActorAvailability.CanCooperate(state, scenario.InitiatingActorId) || !WorldActorAvailability.CanCooperate(state, observer))
            return LocalRoadThreatState.Incapacitated;
        bool reachable = state.Space.QueryInteraction(scenario.InitiatingActorId, observer, scenario.EntryRangeSubunits)
            == SpatialInteraction.Reachable;
        if (!reachable) return LocalRoadThreatState.Watching;
        // Readiness is disclosed only for an observed initiator within its reach, where it decides whether contact begins.
        return WorldActorAvailability.CanAct(state, scenario.InitiatingActorId) ? LocalRoadThreatState.InReach : LocalRoadThreatState.Recovering;
    }
}
