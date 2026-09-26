using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Progression;

public enum ActorRewardRejection
{
    None = 0,
    Unavailable = 1,
    RevisionConflict = 2,
    CapacityExceeded = 3
}

/// <summary>Private candidate only. Quest completion and this reward must commit atomically in the calling authority.</summary>
public sealed class ActorRewardPlan
{
    internal ActorRewardPlan(ProgressionSnapshot candidate, ActorRewardRejection rejection, IEnumerable<int> levelsGained,
        ActorResourceTransition? actorTransition = null)
    {
        Candidate = candidate;
        Rejection = rejection;
        LevelsGained = Array.AsReadOnly(levelsGained.ToArray());
        ActorTransition = actorTransition;
    }

    public bool Succeeded => Rejection == ActorRewardRejection.None;
    public ProgressionSnapshot Candidate { get; }
    public ActorRewardRejection Rejection { get; }
    public ReadOnlyCollection<int> LevelsGained { get; }
    public ActorResourceTransition? ActorTransition { get; }
}

/// <summary>
/// Applies an already authorized definition to immutable state. This pure planner does not authenticate,
/// authorize a quest, enforce its one-time completion, or persist an acknowledgement.
/// </summary>
public static class ActorRewardPlanner
{
    public static ActorRewardPlan Plan(ProgressionSnapshot state, EntityId actorId,
        ulong expectedActorRevision, ActorRewardDefinition reward)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(reward);
        ActorProgressionState? actor = state.Actors.FirstOrDefault(value => value.ActorId == actorId);
        CurrencyAccount? reserve = state.Currency.Accounts.FirstOrDefault(value => value.AccountId == reward.ReserveAccountId);
        if (actor is null || reserve is null || reserve.Kind != CurrencyAccountKind.Reserve
            || reward.Currency.DefinitionId != state.Currency.Currency.DefinitionId
            || reward.Currency.Version != state.Currency.Currency.Version
            || reserve.Balance < reward.CurrencyAmount || state.ContainsIdentity(reward.DefinitionId))
        {
            return Reject(state, ActorRewardRejection.Unavailable);
        }
        if (actor.Revision != expectedActorRevision)
        {
            return Reject(state, ActorRewardRejection.RevisionConflict);
        }
        try
        {
            ActorResourceTransition transition = actor.GainExperience(reward.Experience);
            ActorProgressionState nextActor = transition.Candidate;
            CurrencyAccount recipient = state.Currency.GetActorAccount(actorId);
            CurrencyLedger nextCurrency = reward.CurrencyAmount == 0 ? state.Currency
                : state.Currency.PayReward(reserve, recipient, reward.CurrencyAmount);
            ProgressionSnapshot candidate = state.WithActorState(nextActor, nextCurrency);
            return new ActorRewardPlan(candidate, ActorRewardRejection.None,
                Enumerable.Range(actor.Level + 1, nextActor.Level - actor.Level), transition);
        }
        catch (OverflowException)
        {
            return Reject(state, ActorRewardRejection.CapacityExceeded);
        }
    }

    private static ActorRewardPlan Reject(ProgressionSnapshot state, ActorRewardRejection rejection) =>
        new(state, rejection, []);
}
