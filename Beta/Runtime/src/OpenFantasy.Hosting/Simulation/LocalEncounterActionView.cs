using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation.Retreat;

namespace OpenFantasy.Hosting.Simulation;

public enum LocalEncounterActionBlockedReason
{
    None,
    NotActive,
    Outcome,
    Incapacitated,
    Bystander,
    Recovering,
    DecisionQueued,
    Held,
    Unavailable,
    OutOfReach,
    /// <summary>A committed retreat is this actor's standing choice until it ends.</summary>
    Retreating,
    PreparingChoices
}

public sealed record LocalEncounterAbilityOffer(EntityId AbilityDefinitionId, string DisplayName,
    EntityId TargetActorId, long RangeSubunits, uint ManaCost, ulong ReadinessDelay,
    LocalEncounterActionBlockedReason BlockedReason)
{
    public bool CanQueue => BlockedReason == LocalEncounterActionBlockedReason.None;
}

/// <summary>
/// The observer's known choices against actors in the admitted sight-complete local scene.
/// Offers capture one timing boundary; submission still requires authenticated authority admission.
/// No other actor's grants, random stream, pending command or private content pins are projected.
/// </summary>
public sealed class LocalEncounterActionView
{
    private LocalEncounterActionView(WorldSnapshot world, EntityId observerId, EncounterTimingSnapshot timing)
    {
        EncounterId = timing.EncounterId;
        TimingRevision = timing.Revision;
        ActorRevision = world.Space.GetActor(observerId).Revision;
        var actor = timing.GetActor(observerId);
        RemainingDelay = actor.RemainingDelay;
        WaitDelay = actor.StandardDelay;
        CurrentHp = world.Progression?.GetActor(observerId).CurrentHp;
        CurrentMp = world.Progression?.GetActor(observerId).CurrentMp;
        WaitBlockedReason = BoundaryBlock(world, observerId, timing, actor);

        List<LocalEncounterAbilityOffer> offers = [];
        if (world.EncounterActions is { } actions)
        {
            foreach (EntityId abilityId in actions.GetCombatant(observerId).AbilityIds)
            {
                var ability = actions.GetAbility(abilityId);
                foreach (var target in world.Residents)
                {
                    // This is the same resource/anatomy/side/geometry check as command admission.
                    var intent = new QueueEncounterAbilityIntent(EncounterId, TimingRevision, abilityId, target.ActorId);
                    WorldActionRejection rejection = WorldEncounterActions.EvaluateAbility(world, observerId, intent);
                    var blocked = WaitBlockedReason != LocalEncounterActionBlockedReason.None ? WaitBlockedReason
                        : rejection == WorldActionRejection.None ? LocalEncounterActionBlockedReason.None
                        : rejection == WorldActionRejection.OutOfReach ? LocalEncounterActionBlockedReason.OutOfReach
                        : LocalEncounterActionBlockedReason.Unavailable;
                    offers.Add(new(abilityId, ability.DisplayName, target.ActorId, ability.RangeSubunits,
                        ability.ManaCost, ability.Delay(actor.NormalizedSpeed), blocked));
                }
            }
        }
        AbilityOffers = Array.AsReadOnly(offers.OrderBy(offer => offer.AbilityDefinitionId)
            .ThenBy(offer => offer.TargetActorId).ToArray());
    }

    public EntityId EncounterId { get; }
    public ulong TimingRevision { get; }
    public ulong ActorRevision { get; }
    public ulong RemainingDelay { get; }
    public int? CurrentHp { get; }
    public int? CurrentMp { get; }
    public ulong WaitDelay { get; }
    public LocalEncounterActionBlockedReason WaitBlockedReason { get; }
    public bool CanWait => WaitBlockedReason == LocalEncounterActionBlockedReason.None;
    public ReadOnlyCollection<LocalEncounterAbilityOffer> AbilityOffers { get; }

    internal static LocalEncounterActionView? Project(WorldSnapshot world, EntityId observerId) =>
        world.Pacing?.Encounter is { } timing ? new(world, observerId, timing) : null;

    private static LocalEncounterActionBlockedReason BoundaryBlock(WorldSnapshot world, EntityId observerId,
        EncounterTimingSnapshot timing, EncounterActorTiming actor)
    {
        if (WorldEncounterActions.IsOutcome(world)) return LocalEncounterActionBlockedReason.Outcome;
        if (world.EncounterLifecycle is { Phase: not EncounterLifecyclePhase.Active })
            return LocalEncounterActionBlockedReason.NotActive;
        if (world.HasReadingHold || world.PendingExcavation is not null) return LocalEncounterActionBlockedReason.Held;
        if (world.EncounterActions is not null && world.Progression!.GetActor(observerId).CurrentHp == 0)
            return LocalEncounterActionBlockedReason.Incapacitated;
        if (!WorldEncounterActions.CanParticipate(world, observerId)) return LocalEncounterActionBlockedReason.Bystander;
        if (WorldEncounterRetreats.HoldsActor(world, observerId)) return LocalEncounterActionBlockedReason.Retreating;
        if (!actor.IsDue) return LocalEncounterActionBlockedReason.Recovering;
        if (timing.Decisions.Any(decision => decision.ActorId == observerId)) return LocalEncounterActionBlockedReason.DecisionQueued;
        if (LocalEncounterChoiceBoundary.IsPreparing(world)) return LocalEncounterActionBlockedReason.PreparingChoices;
        return LocalEncounterActionBlockedReason.None;
    }
}
