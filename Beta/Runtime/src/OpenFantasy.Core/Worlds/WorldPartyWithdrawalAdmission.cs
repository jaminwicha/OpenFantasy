using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;

namespace OpenFantasy.Core.Worlds;

/// <summary>Complete-world binding of independently committed routes. Fresh selection never upgrades a lived save.</summary>
public static class WorldPartyWithdrawalAdmission
{
    public const ushort ProfileVersion = 41;

    public static WorldSnapshot CreateFresh(WorldSnapshot basis)
    {
        ArgumentNullException.ThrowIfNull(basis);
        if (basis.ProfileVersion != WorldDelegatedControlAdmission.ProfileVersion || basis.Revision != 0
            || basis.AdvancingSteps != 0 || basis.HasReadingHold || basis.PendingExcavation is not null
            || basis.Coordination?.Anchor is not null || basis.Retreat is not { Active: null, AcceptedCount: 0, Last: null }
            || basis.EncounterLifecycle is not { Phase: EncounterLifecyclePhase.Exploration, EncounterCount: 0 })
            throw new ArgumentException("Collective withdrawal fresh admission requires an unplayed effective-control world.");
        var admission = ContentHash.Compute(CanonicalEncodingV1.Frame("world.party-withdrawal-admission.v1",
            basis.ContentHash.ToByteArray(), EncounterWithdrawalState.RulesPin.ToByteArray()));
        var world = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle,
            care: basis.Care, resourceHistory: WorldResourceHistory.CreateFresh(admission, basis.Progression!),
            coordination: basis.Coordination, retreat: basis.Retreat.WithWithdrawal(EncounterWithdrawalState.Empty),
            encounterOutcome: basis.EncounterOutcome, controllers: basis.Controllers, recovery: basis.Recovery,
            controlPermissions: basis.ControlPermissions, effectiveControl: basis.EffectiveControl);
        _ = WorldSnapshotCodec.Encode(world);
        return world;
    }

    internal static void Validate(WorldSnapshot world)
    {
        if (world.Retreat?.Withdrawal is not { } withdrawal) return;
        if (world.EffectiveControl is null || world.EncounterOutcome is null || world.Recovery is null
            || world.Revision == 0 && withdrawal.Revision != 0)
            throw new ArgumentException("Collective withdrawal requires the complete effective-control interpretation.");
        if (withdrawal.Last is { } last)
        {
            if (last.WorldSequence > world.Revision) throw new ArgumentException("Withdrawal outcome exceeds committed time.");
            foreach (var end in last.Ended) ValidateCommandScope(world, end.Member);
        }
        if (withdrawal.Parties.Count == 0) return;
        if (world.EncounterLifecycle?.Phase != EncounterLifecyclePhase.Active || world.Pacing?.Encounter is not { } timing
            || world.EncounterActions is not { } actions || actions.Combatants.Where(actor => actor.Side != 0)
                .Select(actor => actor.Side).Distinct().Count() != 2)
            throw new ArgumentException("Withdrawal side books belong to an active two-side encounter.");
        foreach (var party in withdrawal.Parties)
        {
            if (party.Scope != timing.Scope || !party.RequiredActors.SequenceEqual(actions.Combatants
                .Where(actor => actor.Side == party.Side).Select(actor => actor.ActorId).Order()))
                throw new ArgumentException("Withdrawal must retain the complete actual side, including downed people.");
            foreach (var member in party.Members)
            {
                ValidateCommandScope(world, member);
                if (!WorldEncounterActions.CanParticipate(world, member.ActorId)
                    || world.Space.GetActor(member.ActorId).Feet != member.ExpectedFeet
                    || timing.GetActor(member.ActorId).NormalizedSpeed != member.NormalizedSpeed
                    || member.Intent.ExpectedTimingRevision >= timing.Revision
                    || world.Space.GetActor(member.ActorId).Revision < checked(member.Command.ExpectedActorRevision + (ulong)member.SamplesWalked))
                    throw new ArgumentException("Withdrawal progress must agree with actual body, speed and accepted timing.");
                var decision = timing.Decisions.SingleOrDefault(value => value.ActorId == member.ActorId);
                if (member.SamplesWalked == 0 ? decision is null || !IsQueuedChoice(member, decision)
                    : member.HasArrived ? decision is not null && decision.Kind != EncounterDecisionKind.Wait
                    : decision is not null)
                    throw new ArgumentException("Walking retains its exact initial choice; arrived people make ordinary Wait decisions.");
            }
        }
    }

    private static void ValidateCommandScope(WorldSnapshot world, EncounterWithdrawalMember member)
    {
        if (member.Command.WorldId != world.WorldId || member.Command.BranchId != world.BranchId
            || world.Residents.All(actor => actor.ActorId != member.ActorId)
            || !WorldActorCommandAuthority.ValidateRecordedEvidence(world.Inventory, member.Command))
            throw new ArgumentException("Withdrawal evidence must retain this world's actual actor and original submitter.");
    }

    internal static bool IsQueuedChoice(EncounterWithdrawalMember member, EncounterDecision decision) =>
        member.SamplesWalked == 0 && decision.Kind == EncounterDecisionKind.Action && decision.ActorId == member.ActorId
        && decision.DecisionId == member.Command.CommandId && decision.ActionPin == member.Command.ContentHash;
}
