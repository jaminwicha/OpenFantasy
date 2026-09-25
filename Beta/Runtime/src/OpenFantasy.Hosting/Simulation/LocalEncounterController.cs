using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Bounded declared-opponent selection through the same ability legality used for player choices.</summary>
internal sealed class LocalEncounterController
{
    private const int MaximumCandidates = 32;

    internal IReadOnlyList<WorldActionCommand> SelectChoices(WorldSnapshot state, Func<EntityId> newIdentity)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(newIdentity);
        List<WorldActionCommand> actions = [];
        if (state.Pacing?.Encounter is not { } timing || WorldEncounterActions.IsOutcome(state)) return actions;
        var chosen = timing.Decisions.Select(decision => decision.ActorId).ToHashSet();
        foreach (var actor in WorldEncounterActions.DueActors(state))
        {
            if (actor.Control != EncounterActorControl.Automatic || chosen.Contains(actor.ActorId)) continue;
            if (state.GetResident(actor.ActorId).ControllerKind != ResidentControllerKind.Host)
                throw new InvalidOperationException("An automatic decision requires an admitted host resident.");
            var binding = state.Inventory.Controls.Single(value => value.ActorId == actor.ActorId);
            WorldActionIntent intent = ChooseIntent(state, actor.ActorId);
            actions.Add(new WorldActionCommand(newIdentity(), binding.PrincipalId, binding.SessionId, actor.ActorId,
                state.WorldId, state.BranchId, state.Space.GetActor(actor.ActorId).Revision, intent));
        }
        return actions.AsReadOnly();
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
