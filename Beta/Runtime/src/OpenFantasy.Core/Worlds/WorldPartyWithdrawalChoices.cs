using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Progression;

namespace OpenFantasy.Core.Worlds;

/// <summary>Choice ownership and cancellation; the encounter processor authenticates and publishes these changes.</summary>
internal static class WorldPartyWithdrawalChoices
{
    internal static EncounterRetreatState Accept(WorldSnapshot world, EncounterRetreatState retreat, WorldActionCommand command)
    {
        var state = retreat.Withdrawal!;
        var actions = world.EncounterActions!;
        ushort side = actions.GetCombatant(command.ActorId).Side;
        var party = state.Parties.SingleOrDefault(value => value.Side == side)
            ?? EncounterWithdrawalParty.Fresh(actions.Scope, side, actions.Combatants.Where(value => value.Side == side).Select(value => value.ActorId));
        var member = new EncounterWithdrawalMember(command, world.Space.GetActor(command.ActorId).Feet,
            world.Pacing!.Encounter!.GetActor(command.ActorId).NormalizedSpeed, 0);
        return retreat.WithWithdrawal(state.WithParty(party.RecordAccepted(party.Revision, member)));
    }

    internal static WorldActionRejection ValidateCancellation(WorldSnapshot world, EncounterRetreatState? retreat,
        EntityId actorId, CancelEncounterWithdrawalIntent intent)
    {
        if (retreat?.Withdrawal?.FindMember(actorId) is not { } member || intent.EncounterId != member.Intent.EncounterId)
            return WorldActionRejection.Unavailable;
        if (intent.ExpectedTimingRevision != world.Pacing!.Encounter!.Revision || intent.WithdrawalCommandId != member.Command.CommandId)
            return WorldActionRejection.RevisionConflict;
        // An arrived person's separate queued Wait is real accepted work, not cancelled by this request.
        if (member.HasArrived && world.Pacing.Encounter.Decisions.Any(value => value.ActorId == actorId))
            return WorldActionRejection.Unavailable;
        return WorldActionRejection.None;
    }

    internal static EncounterRetreatState Cancel(EncounterRetreatState retreat, EntityId actorId, ulong sequence)
    {
        var state = retreat.Withdrawal!;
        return retreat.WithWithdrawal(state.End(sequence, [new(state.FindMember(actorId)!, EncounterWithdrawalEndReason.ChoiceChanged)]));
    }

    internal static EncounterRetreatState ObserveCombat(WorldSnapshot world, EncounterRetreatState retreat,
        EncounterActionSnapshot actions, ProgressionSnapshot progression, ulong sequence)
    {
        var state = retreat.Withdrawal!;
        bool outcome = actions.Combatants.Where(actor => actor.Side != 0 && progression.GetActor(actor.ActorId).CurrentHp > 0)
            .Select(actor => actor.Side).Distinct().Count() <= 1;
        var ended = state.Parties.SelectMany(party => party.Members)
            .Where(member => outcome || progression.GetActor(member.ActorId).CurrentHp == 0)
            .Select(member => new EncounterWithdrawalEnd(member, progression.GetActor(member.ActorId).CurrentHp == 0
                ? EncounterWithdrawalEndReason.Incapacitated : EncounterWithdrawalEndReason.EncounterEnded)).ToArray();
        if (ended.Length != 0) return retreat.WithWithdrawal(state.End(sequence, ended, closeEncounter: outcome));
        if (outcome && state.Parties.Count != 0) return retreat.WithWithdrawal(new(checked(state.Revision + 1), [], state.Last));
        return retreat;
    }
}
