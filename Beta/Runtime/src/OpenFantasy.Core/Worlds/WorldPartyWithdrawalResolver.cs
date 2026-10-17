using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>Publishes one actor's physical sample and cost, or ends the complete side when everyone can depart.</summary>
internal static class WorldPartyWithdrawalResolver
{
    internal static WorldStepDecision Resolve(WorldSnapshot world, EncounterWithdrawalMember member, ulong sequence,
        List<WorldActionReceipt> receipts, List<WorldCommittedIdentity> identities)
    {
        if (WorldEncounterActions.HasRequiredDecisions(world) || member.HasArrived
            || WorldEncounterActions.DueActors(world).FirstOrDefault()?.ActorId != member.ActorId)
            throw new InvalidOperationException("Withdrawal needs all human choices and this actor's own due boundary.");
        var retreat = world.Retreat!;
        var state = retreat.Withdrawal!;
        var basis = world.Pacing!.Encounter!;
        var charged = new EncounterTimingSnapshot(basis.Scope, checked(basis.Revision + 1), basis.ElapsedTacticalUnits,
            basis.Actors.Select(actor => actor.ActorId == member.ActorId ? actor.WithDelay(member.SampleReadiness) : actor),
            basis.Decisions.Where(decision => decision.ActorId != member.ActorId));
        var reason = Revalidate(world, member, out var moved);
        if (moved is not null)
        {
            var party = state.Parties.Single(value => value.Side == world.EncounterActions!.GetCombatant(member.ActorId).Side);
            var progressed = party.RecordMoved(party.Revision, member.ActorId, member.Command.CommandId,
                world.Space.GetActor(member.ActorId).Feet, moved.GetActor(member.ActorId).Feet);
            var next = state.WithParty(progressed);
            var arrival = progressed.GetMember(member.ActorId);
            if (arrival.HasArrived && !WorldEncounterRetreats.IsClearOfContact(world, moved, member.ActorId))
                next = next.End(sequence, [new(arrival, EncounterWithdrawalEndReason.ContactRemains)]);
            WorldSnapshot? candidate;
            try
            {
                // This intermediate complete root validates actual body, command, timing and world envelopes.
                candidate = Publish(world, sequence, moved, charged, retreat.WithWithdrawal(next));
            }
            catch (ArgumentException)
            {
                candidate = null;
            }
            if (candidate is not null)
            {
                if (progressed.AllArrived && progressed.Members.All(value =>
                    WorldEncounterActions.CanParticipate(world, value.ActorId)
                    && WorldEncounterRetreats.IsClearOfContact(world, moved, value.ActorId)))
                    candidate = Depart(world, sequence, moved, charged, next, progressed.Side);
                return new(candidate, receipts, identities, candidateFrame: WorldCanonicalFrame.Capture(candidate));
            }
            reason = EncounterWithdrawalEndReason.Unadmitted;
        }
        // A refused attempt retains the actual position, spends this boundary and ends only this route.
        var interrupted = state.End(sequence, [new(member, reason, member.SampleReadiness)]);
        var refused = Publish(world, sequence, world.Space, charged, retreat.WithWithdrawal(interrupted));
        return new(refused, receipts, identities, candidateFrame: WorldCanonicalFrame.Capture(refused));
    }

    private static EncounterWithdrawalEndReason Revalidate(WorldSnapshot world, EncounterWithdrawalMember member,
        out LocalSpatialSnapshot? moved)
    {
        moved = null;
        if (!WorldEncounterRetreats.StillControls(world, member.Command)) return EncounterWithdrawalEndReason.ControlLost;
        if (!world.Space.GetActor(member.ActorId).Condition.CanLocomote(LocomotionMode.Ground))
            return EncounterWithdrawalEndReason.NoGroundLocomotion;
        moved = WorldEncounterRetreats.WalkSample(world.Space, member.ActorId, member.Intent.Route[member.SamplesWalked], out _);
        return moved is null ? EncounterWithdrawalEndReason.Blocked : EncounterWithdrawalEndReason.Disengaged;
    }

    private static WorldSnapshot Publish(WorldSnapshot world, ulong sequence, LocalSpatialSnapshot space,
        EncounterTimingSnapshot timing, EncounterRetreatState retreat)
    {
        var lifecycle = world.EncounterLifecycle!.ObserveActive(timing, world.EncounterActions!, tacticalTick: false, isOutcome: false);
        return world.WithState(world.Inventory, space, world.Residents, world.Calendar, sequence, world.AdvancingSteps,
            world.Dialogues, world.Gathering, world.Production, world.Progression, world.Quests, world.Construction,
            world.Reservoirs, world.Transport, world.Excavation, world.ConstructionPhysical, world.LiquidCells,
            world.Pacing!.ObserveEncounter(timing), encounterLifecycle: lifecycle, retreat: retreat);
    }

    private static WorldSnapshot Depart(WorldSnapshot world, ulong sequence, LocalSpatialSnapshot space,
        EncounterTimingSnapshot charged, EncounterWithdrawalState state, ushort side)
    {
        var ended = state.Parties.SelectMany(party => party.Members.Select(member => new EncounterWithdrawalEnd(member,
            party.Side == side ? EncounterWithdrawalEndReason.Disengaged : EncounterWithdrawalEndReason.EncounterEnded))).ToArray();
        var routeIds = ended.Select(end => end.Member.Command.CommandId).ToHashSet();
        var cancelled = charged.Decisions.Select(decision => decision.DecisionId).Where(id => !routeIds.Contains(id));
        var completed = state.End(sequence, ended, closeEncounter: true, cancelledDecisions: cancelled);
        var timing = new EncounterTimingSnapshot(charged.Scope, charged.Revision, charged.ElapsedTacticalUnits, charged.Actors, []);
        var actions = world.EncounterActions!;
        if (actions.Pending.Count != 0) actions = actions.WithPending([]);
        var lifecycle = world.EncounterLifecycle!.Disengage(timing, actions);
        return world.WithState(world.Inventory, space, world.Residents, world.Calendar, sequence, world.AdvancingSteps,
            world.Dialogues, world.Gathering, world.Production, world.Progression, world.Quests, world.Construction,
            world.Reservoirs, world.Transport, world.Excavation, world.ConstructionPhysical, world.LiquidCells,
            world.Pacing!.ObserveEncounter(timing).EndEncounter(), encounterLifecycle: lifecycle,
            retreat: world.Retreat!.WithWithdrawal(completed));
    }
}
