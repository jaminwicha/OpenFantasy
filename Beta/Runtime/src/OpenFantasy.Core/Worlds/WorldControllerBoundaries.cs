using OpenFantasy.Core.Encounters;

namespace OpenFantasy.Core.Worlds;

/// <summary>Game-state conditions for restoring human input; observing these conditions grants no control.</summary>
public static class WorldControllerBoundaries
{
    public static bool CanRestoreHuman(WorldSnapshot state, EntityId actorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Controllers?.Assignments.Any(row => row.ActorId == actorId) == true
            && state.PendingExcavation is null && state.Care?.Pending is null
            && state.EncounterLifecycle?.Phase is not EncounterLifecyclePhase.Confrontation and not EncounterLifecyclePhase.Staging
            && state.Pacing?.Encounter?.Decisions.Any(decision => decision.ActorId == actorId) != true
            && !WorldEncounterRetreats.HoldsActor(state, actorId);
    }
}
