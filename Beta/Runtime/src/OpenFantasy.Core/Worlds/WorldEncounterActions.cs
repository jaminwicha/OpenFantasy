using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>Actual combat eligibility over one admitted world's resources, anatomy and geometry.</summary>
public static class WorldEncounterActions
{
    public static IReadOnlyList<EncounterActorTiming> DueActors(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var timing = world.Pacing?.Encounter ?? throw new ArgumentException("An encounter clock is required.");
        return timing.DueActors.Where(actor => CanParticipate(world, actor.ActorId)).ToArray();
    }

    public static bool HasRequiredDecisions(WorldSnapshot world)
    {
        var timing = world.Pacing!.Encounter!;
        return DueActors(world).Any(actor => actor.Control == EncounterActorControl.RequiredHuman
            && !timing.Decisions.Any(decision => decision.ActorId == actor.ActorId));
    }

    public static bool IsOutcome(WorldSnapshot world) => world.EncounterActions is { } actions
        && actions.Combatants.Where(actor => actor.Side != 0 && world.Progression!.GetActor(actor.ActorId).CurrentHp > 0)
            .Select(actor => actor.Side).Distinct().Count() <= 1;

    public static bool CanParticipate(WorldSnapshot world, EntityId actorId) => world.EncounterActions is not { } actions
        || actions.GetCombatant(actorId).Side != 0 && world.Progression!.GetActor(actorId).CurrentHp > 0;

    public static WorldActionRejection EvaluateAbility(WorldSnapshot world, EntityId actorId, QueueEncounterAbilityIntent intent)
    {
        if (world.EncounterActions is not { } actions || !CanParticipate(world, actorId)
            || !actions.Combatants.Any(actor => actor.ActorId == intent.TargetActorId)
            || !actions.GetCombatant(actorId).AbilityIds.Contains(intent.AbilityDefinitionId))
        {
            return WorldActionRejection.Unavailable;
        }
        var actor = world.Progression!.GetActor(actorId);
        var target = world.Progression.GetActor(intent.TargetActorId);
        var ability = actions.GetAbility(intent.AbilityDefinitionId);
        var sourceSide = actions.GetCombatant(actorId).Side;
        var targetSide = actions.GetCombatant(intent.TargetActorId).Side;
        if (actorId == intent.TargetActorId || targetSide == 0 || sourceSide == targetSide || target.CurrentHp == 0
            || actor.Level < ability.MinimumLevel || (uint)actor.CurrentMp < ability.ManaCost
            || !world.Space.GetActor(actorId).Condition.SupportsFunction(ability.RequiredFunction))
        {
            return WorldActionRejection.Unavailable;
        }
        return world.Space.QueryInteraction(actorId, intent.TargetActorId, ability.RangeSubunits) == SpatialInteraction.Reachable
            ? WorldActionRejection.None : WorldActionRejection.OutOfReach;
    }
}
