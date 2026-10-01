using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation.Retreat;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Bounded declared-opponent selection through the same ability legality used for player choices.</summary>
internal sealed class LocalEncounterController
{
    private const int MaximumCandidates = 32;
    private readonly LocalRetreatConfiguration _retreat;

    internal LocalEncounterController(LocalRetreatConfiguration? retreat = null)
    {
        _retreat = retreat ?? LocalRetreatConfiguration.None;
    }

    internal IReadOnlyList<WorldActionCommand> SelectChoices(WorldSnapshot state, Func<EntityId> newIdentity)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(newIdentity);
        List<WorldActionCommand> actions = [];
        if (state.Pacing?.Encounter is not { } timing || WorldEncounterActions.IsOutcome(state)) return actions;
        var chosen = timing.Decisions.Select(decision => decision.ActorId).ToHashSet();
        foreach (var actor in WorldEncounterActions.DueActors(state))
        {
            // A committed retreat is this actor's standing decision; it resolves without a new choice.
            if (actor.Control != EncounterActorControl.Automatic || chosen.Contains(actor.ActorId)
                || WorldEncounterRetreats.HoldsActor(state, actor.ActorId)) continue;
            if (state.GetResident(actor.ActorId).ControllerKind != ResidentControllerKind.Host)
                throw new InvalidOperationException("An automatic decision requires an admitted host resident.");
            var binding = state.Inventory.Controls.Single(value => value.ActorId == actor.ActorId);
            WorldActionIntent intent = ChooseRetreat(state, actor.ActorId) ?? ChooseIntent(state, actor.ActorId);
            actions.Add(new WorldActionCommand(newIdentity(), binding.PrincipalId, binding.SessionId, actor.ActorId,
                state.WorldId, state.BranchId, state.Space.GetActor(actor.ActorId).Revision, intent));
        }
        return actions.AsReadOnly();
    }

    /// <summary>The opted-in actor's advised retreat when wounded to its threshold and actually admissible now.</summary>
    private QueueEncounterRetreatIntent? ChooseRetreat(WorldSnapshot state, EntityId actorId)
    {
        if (!_retreat.Preferences.TryGetValue(actorId, out var preference) || !WorldEncounterRetreats.Admits(state)) return null;
        var resources = state.Progression!.GetActor(actorId);
        if ((long)resources.CurrentHp * 1000 > (long)resources.Stats.MaxHp * preference.HpPermille) return null;
        var timing = state.Pacing!.Encounter!;
        var advice = WorldEncounterRetreatAdvisor.Advise(state, actorId);
        if (!advice.IsFound) return null;
        var intent = new QueueEncounterRetreatIntent(timing.EncounterId, timing.Revision, advice.Destination, advice.Route);
        return WorldEncounterRetreats.EvaluateQueue(state, actorId, intent) == WorldActionRejection.None ? intent : null;
    }

    private static WorldActionIntent ChooseIntent(WorldSnapshot state, EntityId actorId)
    {
        var timing = state.Pacing!.Encounter!;
        if (state.EncounterActions is { } actions)
        {
            int evaluated = 0;
            var source = actions.GetCombatant(actorId);
            foreach (EntityId ability in source.AbilityIds)
            {
                foreach (var target in actions.Combatants.Where(value => value.Side != 0 && value.Side != source.Side))
                {
                    if (++evaluated > MaximumCandidates) return new QueueEncounterWaitIntent(timing.EncounterId, timing.Revision);
                    var intent = new QueueEncounterAbilityIntent(timing.EncounterId, timing.Revision, ability, target.ActorId);
                    if (WorldEncounterActions.EvaluateAbility(state, actorId, intent) == WorldActionRejection.None) return intent;
                }
            }
        }
        return new QueueEncounterWaitIntent(timing.EncounterId, timing.Revision);
    }
}
