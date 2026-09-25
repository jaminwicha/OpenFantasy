using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Exposure;

namespace OpenFantasy.Core.Worlds;

/// <summary>Joins persistent encounter episodes to actual residents, resources and earned clocks.</summary>
public static class WorldEncounterLifecycleAdmission
{
    public static WorldSnapshot CreateFresh(WorldSnapshot basis, EncounterLifecycleSnapshot lifecycle)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(lifecycle);
        if (basis.ProfileVersion != 22 || basis.Revision != 0 || basis.AdvancingSteps != 0
            || basis.Pacing!.Encounter is not null || lifecycle.Phase != EncounterLifecyclePhase.Exploration
            || lifecycle.Revision != 0 || lifecycle.EncounterCount != 0 || lifecycle.Actions is not null
            || lifecycle.Debts.Any(debt => debt.RemainingDelay != 0))
        {
            throw new ArgumentException("Lifecycle admission requires a fresh exploration world and unearned empty episode history.");
        }
        ContentHash admission = ContentHash.Compute(CanonicalEncodingV1.Frame("world.encounter-lifecycle.admission.v1",
            basis.ContentHash.ToByteArray(), lifecycle.ContentHash.ToByteArray()));
        var world = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: lifecycle);
        _ = WorldSnapshotCodec.Encode(world);
        return world;
    }

    internal static void Validate(WorldSnapshot world)
    {
        if (world.EncounterLifecycle is not { } lifecycle) return;
        if (world.Pacing is null || world.BaseProfileVersion != 21 || lifecycle.WorldId != world.WorldId
            || lifecycle.BranchId != world.BranchId || lifecycle.Revision != world.Revision
            || world.Space.Regions.Any(region => region.Seed != lifecycle.Seed)
            || !world.Residents.Select(actor => actor.ActorId).SequenceEqual(lifecycle.Debts.Select(debt => debt.ActorId)))
        {
            throw new ArgumentException("Lifecycle requires exact complete residents, scope, seed, revision and paced terrain ownership.");
        }
        foreach (var resident in world.Residents)
        {
            int speed = world.Progression!.GetActor(resident.ActorId).Stats.Speed;
            if (speed <= 0 || (ulong)speed * 10 > uint.MaxValue)
                throw new ArgumentException("Every lifecycle resident requires a supported positive normalized speed.");
        }
        if (lifecycle.HasEncounter)
        {
            var timing = world.Pacing.Encounter!;
            foreach (var actor in timing.Actors)
            {
                var expectedControl = world.GetResident(actor.ActorId).ControllerKind == ResidentControllerKind.Player
                    ? EncounterActorControl.RequiredHuman : EncounterActorControl.Automatic;
                if (actor.NormalizedSpeed != NormalizedSpeed(world, actor.ActorId) || actor.Control != expectedControl
                    || actor.RemainingDelay != lifecycle.GetDebt(actor.ActorId).RemainingDelay)
                {
                    throw new ArgumentException("Encounter timing must retain actual speed, controller and permanent recovery debt.");
                }
            }
            if ((lifecycle.Phase == EncounterLifecyclePhase.Outcome) != WorldEncounterActions.IsOutcome(world))
            {
                throw new ArgumentException("Lifecycle outcome must match actual living encounter sides.");
            }
            if (lifecycle.Phase is EncounterLifecyclePhase.Confrontation or EncounterLifecyclePhase.Staging
                && (timing.Revision != 0 || timing.ElapsedTacticalUnits != 0 || timing.Decisions.Count != 0
                    || lifecycle.Actions!.Revision != 0 || lifecycle.Actions.LastResolution is not null))
            {
                throw new ArgumentException("Confrontation and staging cannot conceal elapsed time, effects or queued actions.");
            }
        }
        foreach (DialogueSession dialogue in world.Dialogues)
        {
            if (!WorldActorAvailability.CanCooperate(world, dialogue.SpeakerActorId)
                || !WorldActorAvailability.CanCooperate(world, dialogue.ListenerActorId))
            {
                throw new ArgumentException("An incapacitated actor cannot retain a voluntary conversation.");
            }
        }
    }

    internal static void ValidatePacing(WorldSnapshot world)
    {
        var lifecycle = world.EncounterLifecycle!;
        var pacing = world.Pacing!;
        UInt128 elapsed = (UInt128)lifecycle.ExplorationSteps * ExposurePacingV1.ExplorationMillisecondsPerStep
            + (UInt128)lifecycle.TacticalTicks * 50;
        if (elapsed != pacing.Progress.Elapsed.TotalMilliseconds
            || (UInt128)lifecycle.ExplorationSteps + lifecycle.TacticalTicks != world.AdvancingSteps
            || lifecycle.HasEncounter != (pacing.Encounter is not null))
        {
            throw new ArgumentException("Lifecycle clocks must account for every exploration and tactical boundary exactly once.");
        }
        if (pacing.Encounter is { } timing)
        {
            UInt128 initial = (UInt128)lifecycle.ExplorationSteps * ExposurePacingV1.ExplorationMillisecondsPerStep
                + (UInt128)lifecycle.EntryTacticalTicks * 50;
            if (timing.Scope != lifecycle.Actions!.Scope || pacing.EncounterOriginExposure!.Value.TotalMilliseconds != initial
                || (UInt128)timing.ElapsedTacticalUnits != (UInt128)(lifecycle.TacticalTicks - lifecycle.EntryTacticalTicks) * EncounterTimingSnapshot.UnitsPerTick
                || !timing.Actors.Select(actor => actor.ActorId).SequenceEqual(world.Residents.Select(actor => actor.ActorId))
                || (UInt128)timing.Revision > (UInt128)world.Revision * WorldStepBatch.MaximumActorsPerBatch)
            {
                throw new ArgumentException("The current episode must retain its exact entry anchor and complete actor clock.");
            }
        }
    }

    internal static uint NormalizedSpeed(WorldSnapshot world, EntityId actorId) =>
        checked((uint)world.Progression!.GetActor(actorId).Stats.Speed * 10);
}
