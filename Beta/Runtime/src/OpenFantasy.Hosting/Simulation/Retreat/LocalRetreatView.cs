using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation.Retreat;

public enum LocalRetreatBlockedReason
{
    None,
    NotActive,
    Outcome,
    Held,
    Incapacitated,
    Bystander,
    Recovering,
    DecisionQueued,
    Committed,
    AllyRemains,
    NoGroundLocomotion,
    NoClearGround,
    SearchLimit,
    Retreating,
    PreparingChoices,
    Arrived
}

/// <summary>
/// The observer's offered retreat at one timing boundary: the captured route and destination, the readiness each
/// sample charges, and how many living opponents reach the observer now. The walk itself takes
/// <see cref="WalkingUnits"/> of tactical time (each later sample waits for its charge rounded up to whole ticks); the
/// last sample's charge remains as exit recovery. It is a read-only offer; queue admission and every walked sample
/// revalidate against actual state.
/// </summary>
public sealed record LocalRetreatOffer(EntityId EncounterId, ulong TimingRevision, ulong ActorRevision,
    SubunitPosition Origin, SubunitPosition Destination, ReadOnlyCollection<EncounterRetreatDirection> Route,
    ulong SampleDelay, int OpponentsInReach, LocalRetreatBlockedReason BlockedReason)
{
    public bool CanQueue => BlockedReason == LocalRetreatBlockedReason.None;
    public int Samples => Route.Count;
    /// <summary>The readiness all samples charge together.</summary>
    public ulong TotalDelay => checked(SampleDelay * (ulong)Route.Count);
    /// <summary>Tactical time from the first to the last sample: each later sample follows one quantised charge.</summary>
    public ulong WalkingUnits => Route.Count == 0 ? 0 : checked(QuantisedSampleUnits * (ulong)(Route.Count - 1));
    /// <summary>The last sample's charge, still owed after disengaging and paid by ordinary exploration time.</summary>
    public ulong ExitRecovery => Route.Count == 0 ? 0 : SampleDelay;
    private ulong QuantisedSampleUnits => checked((SampleDelay + EncounterTimingSnapshot.UnitsPerTick - 1)
        / EncounterTimingSnapshot.UnitsPerTick * EncounterTimingSnapshot.UnitsPerTick);
}

/// <summary>The observer's own committed route in progress.</summary>
public sealed record LocalRetreatProgress(SubunitPosition Origin, SubunitPosition Destination,
    int SamplesTaken, int RouteSamples, ulong ReadinessCharged);

/// <summary>The observer's own latest terminal retreat record.</summary>
public sealed record LocalRetreatRecord(EntityId ActorId, EntityId CommandId, EncounterRetreatStatus Status,
    EncounterRetreatReason Reason, SubunitPosition Origin, SubunitPosition Final, int SamplesTaken, int RouteSamples,
    ulong ReadinessCharged, ulong WorldSequence);

/// <summary>
/// Retreat observations of the explicit retreat interpretation; null in every other world. Only the observer's own
/// route and record are projected. Collective worlds also expose same-side readiness status. Another actor's
/// route, destination, charged readiness and command identity are never disclosed.
/// </summary>
public sealed class LocalRetreatView
{
    /// <summary>Rehydrates immutable presentation values without consulting or constructing authority state.</summary>
    internal LocalRetreatView(LocalRetreatOffer? offer,
        LocalRetreatProgress? progress,
        LocalRetreatRecord? last,
        bool otherWithdrawing,
        LocalWithdrawalView? withdrawal = null)
    {
        Offer = offer;
        Progress = progress;
        Last = last;
        OtherWithdrawing = otherWithdrawing;
        Withdrawal = withdrawal;
    }

    private LocalRetreatView(WorldSnapshot world, EntityId observerId, EncounterRetreatState retreat)
    {
        if (retreat.Withdrawal is { } collective)
        {
            Withdrawal = LocalWithdrawalView.Project(world, observerId);
            OtherWithdrawing = collective.Parties.SelectMany(party => party.Members)
                .Any(member => member.ActorId != observerId && !member.HasArrived);
            if (world.Pacing?.Encounter is { } current && world.EncounterActions is not null)
                Offer = ProjectOffer(world, observerId, current, retreat);
            return;
        }
        if (retreat.Active is { } active)
        {
            if (active.ActorId == observerId)
            {
                Progress = new LocalRetreatProgress(active.Origin, active.Intent.Destination, active.SamplesTaken,
                    active.Intent.Route.Count, active.ReadinessCharged);
            }
            else
            {
                OtherWithdrawing = true;
            }
        }
        if (retreat.Last is { } last && last.ActorId == observerId)
        {
            Last = new LocalRetreatRecord(last.ActorId, last.CommandId, last.Status, last.Reason, last.Origin, last.Final,
                last.SamplesTaken, last.RouteSamples, last.ReadinessCharged, last.WorldSequence);
        }
        if (world.Pacing?.Encounter is { } timing && world.EncounterActions is not null)
        {
            Offer = ProjectOffer(world, observerId, timing, retreat);
        }
    }

    public LocalRetreatOffer? Offer { get; }
    public LocalRetreatProgress? Progress { get; }
    public LocalRetreatRecord? Last { get; }
    /// <summary>Another combatant is walking a committed retreat; nothing else about it is projected.</summary>
    public bool OtherWithdrawing { get; }
    public LocalWithdrawalView? Withdrawal { get; }
    /// <summary>Whether a committed retreat currently holds the observer's choices.</summary>
    public bool IsRetreating => Progress is not null || Withdrawal?.Choice is { HasArrived: false };

    internal static LocalRetreatView? Project(WorldSnapshot world, EntityId observerId) =>
        world.Retreat is { } retreat ? new LocalRetreatView(world, observerId, retreat) : null;

    private static LocalRetreatOffer ProjectOffer(WorldSnapshot world, EntityId observerId, EncounterTimingSnapshot timing,
        EncounterRetreatState retreat)
    {
        ActorPresence presence = world.Space.GetActor(observerId);
        var actor = timing.GetActor(observerId);
        ulong sampleDelay = EncounterRetreatRules.SampleDelay(actor.NormalizedSpeed);
        int inReach = WorldEncounterRetreats.LivingOpponents(world, observerId).Count(opponent =>
            world.EncounterActions!.GetCombatant(opponent.ActorId).AbilityIds.Any(ability =>
                world.Space.QueryInteraction(opponent.ActorId, observerId, world.EncounterActions.GetAbility(ability).RangeSubunits)
                == SpatialInteraction.Reachable));
        LocalRetreatBlockedReason blocked = Boundary(world, observerId, timing, actor, retreat);
        EncounterRetreatAdvice? advice = null;
        if (blocked == LocalRetreatBlockedReason.None)
        {
            // The bounded search runs only at a boundary where the observer can choose; the world then holds for it.
            advice = WorldEncounterRetreatAdvisor.Advise(world, observerId);
            blocked = advice.Status switch
            {
                EncounterRetreatAdviceStatus.Found => LocalRetreatBlockedReason.None,
                EncounterRetreatAdviceStatus.NoGroundLocomotion => LocalRetreatBlockedReason.NoGroundLocomotion,
                EncounterRetreatAdviceStatus.ExpansionLimit => LocalRetreatBlockedReason.SearchLimit,
                _ => LocalRetreatBlockedReason.NoClearGround
            };
        }
        IEnumerable<EncounterRetreatDirection> route = advice is { IsFound: true } ? advice.Route : [];
        return new LocalRetreatOffer(timing.EncounterId, timing.Revision, presence.Revision, presence.Feet,
            advice is { IsFound: true } ? advice.Destination : presence.Feet, Array.AsReadOnly(route.ToArray()),
            sampleDelay, inReach, blocked);
    }

    private static LocalRetreatBlockedReason Boundary(WorldSnapshot world, EntityId observerId, EncounterTimingSnapshot timing,
        EncounterActorTiming actor, EncounterRetreatState retreat)
    {
        if (WorldEncounterActions.IsOutcome(world)) return LocalRetreatBlockedReason.Outcome;
        if (world.EncounterLifecycle?.Phase != EncounterLifecyclePhase.Active) return LocalRetreatBlockedReason.NotActive;
        if (world.HasReadingHold || world.PendingExcavation is not null) return LocalRetreatBlockedReason.Held;
        if (world.Progression!.GetActor(observerId).CurrentHp == 0) return LocalRetreatBlockedReason.Incapacitated;
        if (!WorldEncounterActions.CanParticipate(world, observerId)) return LocalRetreatBlockedReason.Bystander;
        if (retreat.Withdrawal?.FindMember(observerId) is { HasArrived: true }) return LocalRetreatBlockedReason.Arrived;
        if (retreat.Holds(observerId)) return LocalRetreatBlockedReason.Retreating;
        if (retreat.Active is not null) return LocalRetreatBlockedReason.Committed;
        if (!WorldEncounterRetreats.IsSupportedSide(world, observerId)) return LocalRetreatBlockedReason.AllyRemains;
        if (!world.Space.GetActor(observerId).Condition.CanLocomote(LocomotionMode.Ground)) return LocalRetreatBlockedReason.NoGroundLocomotion;
        if (timing.Decisions.Any(decision => decision.ActorId == observerId)) return LocalRetreatBlockedReason.DecisionQueued;
        if (!actor.IsDue) return LocalRetreatBlockedReason.Recovering;
        if (LocalEncounterChoiceBoundary.IsPreparing(world)) return LocalRetreatBlockedReason.PreparingChoices;
        return LocalRetreatBlockedReason.None;
    }
}
