using OpenFantasy.Core.Encounters;

namespace OpenFantasy.Core.Worlds;

/// <summary>Game-state conditions for restoring human input; observing these conditions grants no control.</summary>
public static class WorldControllerBoundaries
{
    public static bool CanRestoreHuman(WorldSnapshot state, EntityId actorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Controllers?.Assignments.Any(row => row.ActorId == actorId) == true
            && state.PendingExcavation is null && PermitsPendingCareDecision(state, actorId)
            && state.EncounterLifecycle?.Phase is not EncounterLifecyclePhase.Confrontation and not EncounterLifecyclePhase.Staging
            && state.Pacing?.Encounter?.Decisions.Any(decision => decision.ActorId == actorId) != true
            && !WorldEncounterRetreats.HoldsActor(state, actorId);
    }

    private static bool PermitsPendingCareDecision(WorldSnapshot state, EntityId actorId)
    {
        if (state.Care?.Pending is not { } pending) return true;
        if (!state.Care.SupportsDispositions) return false;
        var agreement = state.Care.GetAgreement(pending.AgreementId);
        return actorId == agreement.PatientActorId || actorId == agreement.HelperActorId;
    }
}
