using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Resolves one committed retreat sample at the retreating actor's own due boundary. The sample is walked through the
/// ordinary movement owner and must pass complete root admission with the owners it would publish, or it is refused
/// without movement; either way the actor is charged that sample's readiness. A completed route that broke contact
/// ends the episode in the same zero-duration transaction. Nothing here advances tactical time, calendar, exposure or
/// passive processes. Only the placement's root admission is a gameplay refusal; any other failure propagates.
/// </summary>
internal static class WorldEncounterRetreatResolver
{
    internal static WorldStepDecision Resolve(WorldSnapshot world, ulong sequence, List<WorldActionReceipt> receipts,
        List<WorldCommittedIdentity> identities)
    {
        if (WorldEncounterActions.HasRequiredDecisions(world))
        {
            throw new InvalidOperationException("A committed retreat resolves only after every undecided human has chosen.");
        }
        var retreat = world.Retreat!;
        var active = retreat.Active!;
        var basis = world.Pacing!.Encounter!;
        var actorTiming = basis.GetActor(active.ActorId);
        ulong delay = EncounterRetreatRules.SampleDelay(actorTiming.NormalizedSpeed);
        // The actor's own boundary is spent: a queued retreat choice is consumed, a standing one has no timing row.
        var charged = new EncounterTimingSnapshot(basis.Scope, checked(basis.Revision + 1), basis.ElapsedTacticalUnits,
            basis.Actors.Select(value => value.ActorId == active.ActorId ? value.WithDelay(delay) : value),
            basis.Decisions.Where(value => value.ActorId != active.ActorId));
        EncounterRetreatReason refusal = Revalidate(world, active, out LocalSpatialSnapshot? moved);
        int samples = checked(active.SamplesTaken + 1);
        ulong spent = checked(active.ReadinessCharged + delay);
        if (moved is not null)
        {
            bool complete = samples == active.Intent.Route.Count;
            EncounterRetreatState placed = complete
                ? retreat.Finish(Record(active, moved, samples, spent, EncounterRetreatStatus.Interrupted, EncounterRetreatReason.ContactRemains, sequence))
                : retreat.Continue(active.Walked(delay));
            if (Admits(world, sequence, moved, charged, placed))
            {
                WorldSnapshot admitted = complete && WorldEncounterRetreats.IsClearOfContact(world, moved, active.ActorId)
                    ? Disengage(world, sequence, moved, charged, active, spent, retreat)
                    : Publish(world, sequence, moved, charged, placed);
                return new WorldStepDecision(admitted, receipts, identities, candidateFrame: WorldCanonicalFrame.Capture(admitted));
            }
            // Complete root admission (liquid envelopes, seals, custody, construction) refused this placement.
            refusal = EncounterRetreatReason.Unadmitted;
        }
        // No movement is published; the attempted sample still spends this boundary's readiness.
        var interrupted = retreat.Finish(Record(active, world.Space, active.SamplesTaken, spent,
            EncounterRetreatStatus.Interrupted, refusal, sequence));
        WorldSnapshot candidate = Publish(world, sequence, world.Space, charged, interrupted);
        return new WorldStepDecision(candidate, receipts, identities, candidateFrame: WorldCanonicalFrame.Capture(candidate));
    }

    /// <summary>Current control, body, scope and the next sample through the ordinary movement owner.</summary>
    private static EncounterRetreatReason Revalidate(WorldSnapshot world, EncounterRetreatContinuation active, out LocalSpatialSnapshot? moved)
    {
        moved = null;
        if (!WorldEncounterRetreats.StillControls(world, active.Command)) return EncounterRetreatReason.ControlLost;
        ActorPresence actor = world.Space.GetActor(active.ActorId);
        if (!actor.Condition.CanLocomote(LocomotionMode.Ground)) return EncounterRetreatReason.NoGroundLocomotion;
        if (!WorldEncounterRetreats.IsSupportedSide(world, active.ActorId)) return EncounterRetreatReason.AllyRemains;
        moved = WorldEncounterRetreats.WalkSample(world.Space, active.ActorId, active.NextDirection, out _);
        return moved is null ? EncounterRetreatReason.Blocked : EncounterRetreatReason.None;
    }

    /// <summary>The complete root admission of exactly the placement and owners this sample would publish.</summary>
    private static bool Admits(WorldSnapshot world, ulong sequence, LocalSpatialSnapshot space, EncounterTimingSnapshot timing,
        EncounterRetreatState retreat)
    {
        try
        {
            _ = Publish(world, sequence, space, timing, retreat);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static WorldSnapshot Publish(WorldSnapshot world, ulong sequence, LocalSpatialSnapshot space,
        EncounterTimingSnapshot timing, EncounterRetreatState retreat)
    {
        var actions = world.EncounterActions!;
        var lifecycle = world.EncounterLifecycle!.ObserveActive(timing, actions, tacticalTick: false, isOutcome: false);
        return world.WithState(world.Inventory, space, world.Residents, world.Calendar, sequence, world.AdvancingSteps,
            world.Dialogues, world.Gathering, world.Production, world.Progression, world.Quests, world.Construction,
            world.Reservoirs, world.Transport, world.Excavation, world.ConstructionPhysical, world.LiquidCells,
            world.Pacing!.ObserveEncounter(timing), encounterLifecycle: lifecycle, resourceHistory: world.ResourceHistory,
            retreat: retreat);
    }

    /// <summary>
    /// The completed route broke contact: other queued choices end with their identities retained, every actor's actual
    /// debt becomes permanent, the timing binding ends with exposure and carry intact and the world returns to exploration.
    /// </summary>
    private static WorldSnapshot Disengage(WorldSnapshot world, ulong sequence, LocalSpatialSnapshot space,
        EncounterTimingSnapshot charged, EncounterRetreatContinuation active, ulong spent, EncounterRetreatState retreat)
    {
        var cancelled = charged.Decisions.Select(value => value.DecisionId).ToArray();
        var timing = new EncounterTimingSnapshot(charged.Scope, charged.Revision, charged.ElapsedTacticalUnits, charged.Actors, []);
        var actions = world.EncounterActions!;
        if (actions.Pending.Count != 0) actions = actions.WithPending([]);
        var lifecycle = world.EncounterLifecycle!.Disengage(timing, actions);
        var pacing = world.Pacing!.ObserveEncounter(timing).EndEncounter();
        var outcome = Record(active, space, active.Intent.Route.Count, spent, EncounterRetreatStatus.Disengaged,
            EncounterRetreatReason.None, sequence, cancelled);
        return world.WithState(world.Inventory, space, world.Residents, world.Calendar, sequence, world.AdvancingSteps,
            world.Dialogues, world.Gathering, world.Production, world.Progression, world.Quests, world.Construction,
            world.Reservoirs, world.Transport, world.Excavation, world.ConstructionPhysical, world.LiquidCells, pacing,
            encounterLifecycle: lifecycle, resourceHistory: world.ResourceHistory, retreat: retreat.Finish(outcome));
    }

    private static EncounterRetreatOutcome Record(EncounterRetreatContinuation active, LocalSpatialSnapshot space, int samples,
        ulong spent, EncounterRetreatStatus status, EncounterRetreatReason reason, ulong sequence, IEnumerable<EntityId>? cancelled = null) =>
        new(active.ActorId, active.Command.CommandId, active.EncounterId, active.Origin, space.GetActor(active.ActorId).Feet,
            samples, active.Intent.Route.Count, spent, status, reason, sequence, cancelled);
}
