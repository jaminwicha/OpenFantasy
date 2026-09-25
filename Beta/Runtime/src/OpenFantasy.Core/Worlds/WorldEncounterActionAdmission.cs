using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;

namespace OpenFantasy.Core.Worlds;

/// <summary>Admits physical encounter actions without changing an existing world's interpretation.</summary>
public static class WorldEncounterActionAdmission
{
    public static WorldSnapshot CreateFresh(WorldSnapshot basis, EncounterActionSnapshot actions)
    {
        ArgumentNullException.ThrowIfNull(basis);
        ArgumentNullException.ThrowIfNull(actions);
        if (basis.ProfileVersion != 22 || basis.Revision != 0 || basis.AdvancingSteps != 0
            || basis.Pacing?.Encounter is not { Revision: 0, ElapsedTacticalUnits: 0 } timing
            || timing.Decisions.Count != 0 || actions.Revision != 0 || actions.Pending.Count != 0
            || actions.LastResolution is not null || actions.Random.NextCounter != 0 || actions.Random.Exhausted)
        {
            throw new ArgumentException("Action admission requires a fresh complete coordinated encounter.");
        }
        ContentHash admission = ContentHash.Compute(CanonicalEncodingV1.Frame("world.encounter-action-admission.v1",
            basis.ContentHash.ToByteArray(), actions.ContentHash.ToByteArray()));
        var world = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterActions: actions);
        if (WorldEncounterActions.IsOutcome(world))
        {
            throw new ArgumentException("Fresh combat requires at least two admitted living opposing sides.");
        }
        _ = WorldSnapshotCodec.Encode(world);
        return world;
    }

    internal static void Validate(WorldSnapshot world)
    {
        if (world.EncounterActions is not { } actions) return;
        if (world.Pacing?.Encounter is not { } timing || actions.Scope != timing.Scope
            || actions.Revision > world.Revision
            || !actions.Combatants.Select(actor => actor.ActorId).SequenceEqual(world.Residents.Select(actor => actor.ActorId))
            || world.Space.Regions.Any(region => region.Seed != actions.Seed))
        {
            throw new ArgumentException("Encounter actions require the complete resident, clock and seeded terrain scope.");
        }
        foreach (var decision in timing.Decisions)
        {
            if (!WorldEncounterActions.CanParticipate(world, decision.ActorId))
            {
                throw new ArgumentException("Incapacitated actors and coupled bystanders cannot retain choices.");
            }
        }
        var selected = timing.Decisions.Where(decision => decision.Kind == EncounterDecisionKind.Action).ToArray();
        if (selected.Length != actions.Pending.Count)
        {
            throw new ArgumentException("Each pending action requires exactly one complete retained request.");
        }
        foreach (var command in actions.Pending)
        {
            if (!selected.Any(decision => decision.ActorId == command.ActorId && decision.DecisionId == command.CommandId
                    && decision.ActionPin == command.ContentHash))
            {
                throw new ArgumentException("A pending request must match its exact timing choice and command pin.");
            }
        }
        if (WorldEncounterActions.IsOutcome(world) && timing.Decisions.Count != 0)
        {
            throw new ArgumentException("An encounter outcome cannot retain executable pending choices.");
        }
        if (actions.LastResolution is { } last)
        {
            if (last.WorldSequence > world.Revision || last.TimingRevision > timing.Revision
                || !actions.Combatants.Any(actor => actor.ActorId == last.ActorId)
                || last.TargetActorId is { } target && !actions.Combatants.Any(actor => actor.ActorId == target)
                || last.Status == EncounterActionResolutionStatus.Damaged
                    && world.Progression!.GetActor(last.TargetActorId!.Value).CurrentHp != last.TargetHpAfter)
            {
                throw new ArgumentException("The retained action outcome disagrees with complete world state.");
            }
        }
    }
}
