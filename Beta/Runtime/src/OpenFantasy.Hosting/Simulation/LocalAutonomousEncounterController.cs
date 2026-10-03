using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Conservative encounter choices for admitted human actors under autonomous host control.</summary>
internal sealed class LocalAutonomousEncounterController
{
    internal IReadOnlyList<WorldActionCommand> SelectChoices(WorldSnapshot state,
        IReadOnlyList<WorldActionCommand> selectedChoices, Func<EntityId> newIdentity)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(selectedChoices);
        ArgumentNullException.ThrowIfNull(newIdentity);
        List<WorldActionCommand> choices = [];
        if (state.Controllers is null || state.Pacing?.Encounter is not { } timing
            || WorldEncounterActions.IsOutcome(state)) return choices;

        var chosenActors = timing.Decisions.Select(choice => choice.ActorId)
            .Concat(selectedChoices.Select(choice => choice.ActorId)).ToHashSet();
        // Retreat admission permits one standing route. Reserve that slot across this entire
        // proposed batch as well as checking the already committed world state below.
        bool retreatSelected = selectedChoices.Any(choice => choice.Intent is QueueEncounterRetreatIntent);
        foreach (EncounterActorTiming actor in WorldEncounterActions.DueActors(state).OrderBy(actor => actor.ActorId))
        {
            if (!OwnsChoice(state, actor) || chosenActors.Contains(actor.ActorId)
                || WorldEncounterRetreats.HoldsActor(state, actor.ActorId)) continue;

            WorldActionIntent? intent = retreatSelected ? null : ChooseRetreat(state, actor.ActorId);
            retreatSelected |= intent is QueueEncounterRetreatIntent;
            intent ??= new QueueEncounterWaitIntent(timing.EncounterId, timing.Revision);
            var binding = state.Inventory.Controls.Single(control => control.ActorId == actor.ActorId);
            choices.Add(new WorldActionCommand(newIdentity(), binding.PrincipalId, binding.SessionId, actor.ActorId,
                state.WorldId, state.BranchId, state.Space.GetActor(actor.ActorId).Revision, intent));
        }
        return choices.AsReadOnly();
    }

    /// <summary>Temporary control changes the selector, never the character's required-human timing role.</summary>
    internal static bool OwnsChoice(WorldSnapshot state, EncounterActorTiming actor) =>
        actor.Control == EncounterActorControl.RequiredHuman
        && state.Controllers?.Get(actor.ActorId).Mode == WorldControllerMode.Autonomous
        && state.EffectiveControl?.Find(actor.ActorId)?.Witness is null;

    private static QueueEncounterRetreatIntent? ChooseRetreat(WorldSnapshot state, EntityId actorId)
    {
        if (state.Retreat is not { Active: null } || state.EncounterActions is null
            || state.EncounterLifecycle?.Phase != EncounterLifecyclePhase.Active
            || !WorldEncounterRetreats.IsSupportedSide(state, actorId)) return null;
        var advice = WorldEncounterRetreatAdvisor.Advise(state, actorId);
        if (!advice.IsFound) return null;
        var timing = state.Pacing!.Encounter!;
        var intent = new QueueEncounterRetreatIntent(timing.EncounterId, timing.Revision, advice.Destination, advice.Route);
        return WorldEncounterRetreats.EvaluateQueue(state, actorId, intent) == WorldActionRejection.None ? intent : null;
    }
}
