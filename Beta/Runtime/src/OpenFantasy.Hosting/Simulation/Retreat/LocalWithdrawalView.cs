using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation.Retreat;

public enum LocalWithdrawalReadiness { NeedsChoice, Walking, Arrived, Incapacitated }

/// <summary>Public same-side readiness, without another actor's route, destination or command identity.</summary>
public sealed record LocalWithdrawalMemberView(EntityId ActorId, LocalWithdrawalReadiness Readiness);

public sealed record LocalWithdrawalChoice(EntityId CommandId, SubunitPosition Origin, SubunitPosition Destination,
    int SamplesTaken, int RouteSamples, ulong ReadinessCharged)
{
    public bool HasArrived => SamplesTaken == RouteSamples;
}

public sealed record LocalWithdrawalRecord(EntityId ActorId, EntityId CommandId, EncounterWithdrawalEndReason Reason,
    SubunitPosition Origin, SubunitPosition Final, int SamplesTaken, int RouteSamples,
    ulong ReadinessCharged, ulong WorldSequence);

/// <summary>Only the observed actor's private choice and cancellation, accompanied by public party readiness.</summary>
public sealed class LocalWithdrawalView
{
    public LocalWithdrawalView(EntityId actorId, LocalWithdrawalChoice? choice, CancelEncounterWithdrawalIntent? cancellation,
        ReadOnlyCollection<LocalWithdrawalMemberView> members, LocalWithdrawalRecord? last)
    {
        ArgumentNullException.ThrowIfNull(members);
        ActorId = actorId;
        Choice = choice;
        Cancellation = cancellation;
        Members = Array.AsReadOnly(members.ToArray());
        Last = last;
    }

    public EntityId ActorId { get; }
    public LocalWithdrawalChoice? Choice { get; }
    public CancelEncounterWithdrawalIntent? Cancellation { get; }
    public ReadOnlyCollection<LocalWithdrawalMemberView> Members { get; }
    public LocalWithdrawalRecord? Last { get; }

    internal static LocalWithdrawalView Project(WorldSnapshot world, EntityId observer)
    {
        var state = world.Retreat!.Withdrawal!;
        LocalWithdrawalChoice? choice = null;
        CancelEncounterWithdrawalIntent? cancellation = null;
        if (state.FindMember(observer) is { } member)
        {
            choice = new(member.Command.CommandId, member.Origin, member.Intent.Destination,
                member.SamplesWalked, member.Intent.Route.Count, member.WalkedReadiness);
            var intent = new CancelEncounterWithdrawalIntent(member.Intent.EncounterId,
                world.Pacing!.Encounter!.Revision, member.Command.CommandId);
            if (!LocalEncounterChoiceBoundary.IsPreparing(world)
                && WorldEncounterRetreats.EvaluateCancellation(world, observer, intent) == WorldActionRejection.None)
                cancellation = intent;
        }
        LocalWithdrawalRecord? record = null;
        if (state.Last?.Ended.FirstOrDefault(end => end.Member.ActorId == observer) is { } ended)
        {
            var previous = ended.Member;
            record = new(observer, previous.Command.CommandId, ended.Reason, previous.Origin, previous.ExpectedFeet,
                previous.SamplesWalked, previous.Intent.Route.Count, ended.ReadinessCharged, state.Last.WorldSequence);
        }
        List<LocalWithdrawalMemberView> members = [];
        if (world.EncounterLifecycle?.Phase == EncounterLifecyclePhase.Active && world.EncounterActions is { } actions)
        {
            ushort side = actions.GetCombatant(observer).Side;
            foreach (var actor in actions.Combatants.Where(actor => side > 0 && actor.Side == side).OrderBy(actor => actor.ActorId))
            {
                var retained = state.FindMember(actor.ActorId);
                var readiness = world.Progression!.GetActor(actor.ActorId).CurrentHp == 0 ? LocalWithdrawalReadiness.Incapacitated
                    : retained is null ? LocalWithdrawalReadiness.NeedsChoice
                    : retained.HasArrived ? LocalWithdrawalReadiness.Arrived : LocalWithdrawalReadiness.Walking;
                members.Add(new(actor.ActorId, readiness));
            }
        }
        return new(observer, choice, cancellation, members.AsReadOnly(), record);
    }
}
