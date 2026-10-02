using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation.Retreat;

/// <summary>Presentation readiness for the retreat world's captured encounter choices.</summary>
internal static class LocalEncounterChoiceBoundary
{
    /// <summary>
    /// The host may still publish automatic choices at this same tactical instant. Those publications change the
    /// revision captured by every human choice. Advertise readiness only after they settle, without exposing their
    /// contents or submitting a human decision. Full Wait then holds this boundary for the undecided human.
    /// </summary>
    internal static bool IsPreparing(WorldSnapshot world)
    {
        if (world.Retreat is null || world.Pacing?.Encounter is not { } timing) return false;
        return WorldEncounterActions.DueActors(world).Any(actor =>
            (actor.Control == EncounterActorControl.Automatic || LocalAutonomousEncounterController.OwnsChoice(world, actor))
            && !WorldEncounterRetreats.HoldsActor(world, actor.ActorId)
            && !timing.Decisions.Any(decision => decision.ActorId == actor.ActorId));
    }
}
