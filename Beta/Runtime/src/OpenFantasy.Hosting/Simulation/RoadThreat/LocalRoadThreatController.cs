using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation.RoadThreat;

/// <summary>
/// Host decisions for admitted hostile opportunities: a scenario whose initiator is a host resident and whose target is
/// the session's player. At an unheld exploration cutoff the initiator proposes entry only through its own control
/// binding and current revisions, when it is alive with no readiness debt, every positive participant can take part
/// and the player stands within the scenario's entry range on a clear centre sightline (the same interaction query the
/// authority uses). The proposal is an ordinary authenticated lifecycle command; the authority re-validates it.
/// <para>
/// The decision reads only the selected root, so a reopened session behaves from recorded state: no cooldown, memory
/// or target is held here. Only the retreat-capable profile executes hostile entry, because its committed retreat is
/// the lawful exit; elsewhere such a scenario is never proposed. Player-initiated scenarios are never selected.
/// </para>
/// </summary>
internal sealed class LocalRoadThreatController
{
    private readonly EntityId _playerActorId;

    internal LocalRoadThreatController(EntityId playerActorId)
    {
        _playerActorId = playerActorId;
    }

    /// <summary>The hostile scenarios against this player, in catalog order; bounded by the lifecycle's scenario limit.</summary>
    internal static IReadOnlyList<EncounterScenarioDefinition> HostileScenarios(WorldSnapshot state, EntityId playerActorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.EncounterLifecycle is not { } lifecycle) return [];
        return lifecycle.Scenarios.Where(scenario => scenario.TargetActorId == playerActorId
            && scenario.InitiatingActorId != playerActorId
            && state.GetResident(scenario.InitiatingActorId).ControllerKind == ResidentControllerKind.Host).ToArray();
    }

    /// <summary>Whether the initiator can actually begin this scenario now, by the same facts the authority checks.</summary>
    internal static bool CanInitiate(WorldSnapshot state, EncounterScenarioDefinition scenario)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(scenario);
        if (!LocalProfileBindings.AdmitsRetreat(state) || !LocalProfileBindings.IsUnheldExploration(state)
            || state.EncounterLifecycle is not { Phase: EncounterLifecyclePhase.Exploration })
        {
            return false;
        }
        // Readiness: an initiator still paying recovery debt is not ready for a new voluntary action.
        if (!WorldActorAvailability.CanAct(state, scenario.InitiatingActorId)
            || scenario.Combatants.Where(value => value.Side != 0).Any(value => !WorldActorAvailability.CanCooperate(state, value.ActorId)))
        {
            return false;
        }
        return state.Space.QueryInteraction(scenario.InitiatingActorId, scenario.TargetActorId, scenario.EntryRangeSubunits)
            == SpatialInteraction.Reachable;
    }

    /// <summary>The first eligible hostile entry under the initiator's own control, or null when none is eligible.</summary>
    internal WorldActionCommand? SelectEntry(WorldSnapshot state, Func<EntityId> newIdentity)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(newIdentity);
        foreach (EncounterScenarioDefinition scenario in HostileScenarios(state, _playerActorId))
        {
            if (!CanInitiate(state, scenario)) continue;
            EncounterLifecycleSnapshot lifecycle = state.EncounterLifecycle!;
            ActorControlBinding binding = state.Inventory.Controls.Single(value => value.ActorId == scenario.InitiatingActorId);
            var intent = new EnterEncounterIntent(scenario.DefinitionId, EncounterEpisodeIdentity.Derive(lifecycle, scenario.DefinitionId),
                lifecycle.EncounterCount, state.Space.GetActor(scenario.TargetActorId).Revision);
            return new WorldActionCommand(newIdentity(), binding.PrincipalId, binding.SessionId, scenario.InitiatingActorId,
                state.WorldId, state.BranchId, state.Space.GetActor(scenario.InitiatingActorId).Revision, intent);
        }
        return null;
    }
}
