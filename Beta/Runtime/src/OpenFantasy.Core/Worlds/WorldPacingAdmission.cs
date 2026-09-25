using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Exposure;

namespace OpenFantasy.Core.Worlds;

/// <summary>Joins retained pacing to a complete fresh terrain/liquid world without reinterpreting a saved history.</summary>
public static class WorldPacingAdmission
{
    public static WorldSnapshot CreateFresh(WorldSnapshot basis, EntityId? encounterId = null)
    {
        ArgumentNullException.ThrowIfNull(basis);
        if (basis.ProfileVersion != 21 || basis.Revision != 0 || basis.AdvancingSteps != 0 || basis.HasReadingHold
            || basis.Residents.Any(resident => resident.ExperiencedTime != LocalExposureDuration.Zero))
        {
            throw new ArgumentException("Pacing admission requires a fresh unheld complete terrain/liquid basis.", nameof(basis));
        }
        WorldPacingState pacing = WorldPacingState.Begin(basis.WorldId, basis.BranchId, basis.Calendar);
        if (encounterId is { } encounter)
        {
            var timing = new EncounterTimingSnapshot(new EncounterTimingScope(basis.WorldId, basis.BranchId, encounter),
                0, 0, basis.Residents.Select(resident => new EncounterActorTiming(resident.ActorId,
                    NormalizedSpeed(basis, resident.ActorId), 0, Control(resident))), []);
            pacing = pacing.StartEncounter(timing);
        }
        ContentHash admission = ContentHash.Compute(CanonicalEncodingV1.Frame("world.pacing.admission.v1",
            basis.ContentHash.ToByteArray(), WorldPacingState.ProcessRate.RulesVersion.ToByteArray(),
            WorldPacingCodecV1.Encode(pacing)));
        return new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: pacing);
    }

    internal static void Validate(WorldSnapshot world)
    {
        if (world.Pacing is not { } pacing) return;
        if (world.BaseProfileVersion != 21 || pacing.WorldId != world.WorldId || pacing.BranchId != world.BranchId
            || pacing.Calendar != world.Calendar || world.HasReadingHold && pacing.Encounter is not null
            || world.Residents.Any(resident => resident.ExperiencedTime != pacing.Progress.Elapsed))
        {
            throw new ArgumentException("Pacing must own the exact clock of its complete terrain/liquid world.");
        }
        if (world.EncounterLifecycle is not null)
        {
            WorldEncounterLifecycleAdmission.ValidatePacing(world);
            return;
        }
        UInt128 milliseconds = pacing.Progress.Elapsed.TotalMilliseconds;
        UInt128 expectedSteps;
        if (pacing.Encounter is { } encounter)
        {
            LocalExposureDuration origin = pacing.EncounterOriginExposure!.Value;
            if (origin.TotalMilliseconds % ExposurePacingV1.ExplorationMillisecondsPerStep != 0
                || !encounter.Actors.Select(actor => actor.ActorId).SequenceEqual(world.Residents.Select(resident => resident.ActorId))
                || encounter.Decisions.Any(decision => decision.Kind != EncounterDecisionKind.Wait && world.EncounterActions is null))
            {
                throw new ArgumentException("The coordinated timing owner requires complete residents and admitted Wait choices.");
            }
            foreach (EncounterActorTiming actor in encounter.Actors)
            {
                if (actor.NormalizedSpeed != NormalizedSpeed(world, actor.ActorId)
                    || actor.Control != Control(world.GetResident(actor.ActorId)))
                {
                    throw new ArgumentException("Tactical speed and controller policy must match actual resident owners.");
                }
            }
            expectedSteps = origin.TotalMilliseconds / ExposurePacingV1.ExplorationMillisecondsPerStep
                + encounter.ElapsedTacticalUnits / EncounterTimingSnapshot.UnitsPerTick;
            if ((UInt128)encounter.Revision > (UInt128)world.Revision * WorldStepBatch.MaximumActorsPerBatch)
            {
                throw new ArgumentException("Tactical revision exceeds possible selected world boundaries.");
            }
        }
        else
        {
            if (milliseconds % ExposurePacingV1.ExplorationMillisecondsPerStep != 0)
            {
                throw new ArgumentException("An exploration-only clock requires complete selected exploration intervals.");
            }
            expectedSteps = pacing.ProcessIntervals;
        }
        if (expectedSteps != world.AdvancingSteps)
        {
            throw new ArgumentException("Selected advancing boundaries must exactly match retained world pacing.");
        }
    }

    private static uint NormalizedSpeed(WorldSnapshot world, EntityId actorId)
    {
        var actor = world.Progression?.Actors.Single(value => value.ActorId == actorId)
            ?? throw new ArgumentException("Timing requires actual admitted actor progression.");
        if (actor.CurrentHp == 0 && world.EncounterActions is null || actor.Stats.Speed <= 0)
        {
            throw new ArgumentException("This timing interpretation requires living capable actors with positive speed.");
        }
        // This version maps ten legacy speed points to normalized speed 100. Full ability costs and
        // damage semantics require their own admitted content interpretation; a Wait cannot grant them.
        return checked((uint)actor.Stats.Speed * 10);
    }

    private static EncounterActorControl Control(WorldResident resident) => resident.ControllerKind == ResidentControllerKind.Player
        ? EncounterActorControl.RequiredHuman : EncounterActorControl.Automatic;
}
