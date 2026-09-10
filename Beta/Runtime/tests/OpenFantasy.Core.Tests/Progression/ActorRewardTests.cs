using OpenFantasy.Core.Progression;
using static OpenFantasy.Core.Tests.Progression.ProgressionFixture;

namespace OpenFantasy.Core.Tests.Progression;

public sealed class ActorRewardTests
{
    [Fact]
    public void SourceRewardRecomputesTwoLevelsRefillsVitalsAndConservesTheReserve()
    {
        ActorProgressionState damaged = new(Id(10), 0, Squire(), Curve(), 1, 0, 7, 3);
        ProgressionSnapshot initial = Create(damaged);
        ActorRewardPlan plan = ActorRewardPlanner.Plan(initial, Id(10), 0, Reward());
        Assert.True(plan.Succeeded);
        Assert.Equal(ActorRewardRejection.None, plan.Rejection);
        Assert.Equal(new[] { 2, 3 }, plan.LevelsGained);
        ActorProgressionState actor = plan.Candidate.GetActor(Id(10));
        Assert.Equal(3, actor.Level);
        Assert.Equal(38UL, actor.Experience);
        Assert.Equal(new ActorStatValues(78, 14, 18, 6, 12, 10), actor.Stats);
        Assert.Equal(78, actor.CurrentHp);
        Assert.Equal(14, actor.CurrentMp);
        Assert.Equal(1UL, actor.Revision);
        Assert.Equal(1UL, plan.Candidate.Revision);
        Assert.Equal(1UL, plan.Candidate.Currency.Revision);
        Assert.Equal(200UL, plan.Candidate.Currency.AdmittedSupply);
        Assert.Equal(200UL, plan.Candidate.GetAccount(Id(201)).Balance);
        Assert.Equal(0UL, plan.Candidate.GetAccount(Id(200)).Balance);
        Assert.Equal(1UL, plan.Candidate.GetAccount(Id(201)).Revision);
        Assert.Equal(1UL, plan.Candidate.GetAccount(Id(200)).Revision);
        Assert.Same(initial.GetActor(Id(11)), plan.Candidate.GetActor(Id(11)));
        Assert.Same(initial.GetAccount(Id(202)), plan.Candidate.GetAccount(Id(202)));
        Assert.Equal(200UL, initial.GetAccount(Id(200)).Balance);
        Assert.Equal(7, initial.GetActor(Id(10)).CurrentHp);
        Assert.Equal(plan.Candidate.ContentHash,
            ProgressionCodecV1.Decode(ProgressionCodecV1.Encode(plan.Candidate)).ContentHash);
    }

    [Fact]
    public void SubthresholdExperienceAndCurrencyDoNotHealAnActor()
    {
        var damaged = new ActorProgressionState(Id(10), 0, Squire(), Curve(), 1, 0, 7, 3);
        ActorRewardPlan result = ActorRewardPlanner.Plan(Create(damaged), Id(10), 0, Reward(99));
        Assert.True(result.Succeeded);
        Assert.Empty(result.LevelsGained);
        Assert.Equal(1, result.Candidate.GetActor(Id(10)).Level);
        Assert.Equal(99UL, result.Candidate.GetActor(Id(10)).Experience);
        Assert.Equal(7, result.Candidate.GetActor(Id(10)).CurrentHp);
        Assert.Equal(3, result.Candidate.GetActor(Id(10)).CurrentMp);
    }

    [Fact]
    public void ExactThresholdAdvancesWithoutRoundingAndExperienceOnlyLeavesAccountsUntouched()
    {
        ProgressionSnapshot initial = Create();
        ActorRewardPlan result = ActorRewardPlanner.Plan(initial, Id(10), 0, Reward(100, 0));
        Assert.True(result.Succeeded);
        Assert.Equal(new[] { 2 }, result.LevelsGained);
        Assert.Equal(0UL, result.Candidate.GetActor(Id(10)).Experience);
        Assert.Equal(69, result.Candidate.GetActor(Id(10)).CurrentHp);
        Assert.Same(initial.Currency, result.Candidate.Currency);
    }

    [Fact]
    public void LastLevelRetainsResidualExperienceAndDoesNotRefillWithoutAnotherLevel()
    {
        var atCap = new ActorProgressionState(Id(10), 0, Squire(), Curve(), 10, 9000, 7, 3);
        ActorRewardPlan result = ActorRewardPlanner.Plan(Create(atCap), Id(10), 0, Reward());
        Assert.True(result.Succeeded);
        Assert.Equal(10, result.Candidate.GetActor(Id(10)).Level);
        Assert.Equal(9420UL, result.Candidate.GetActor(Id(10)).Experience);
        Assert.Equal(7, result.Candidate.GetActor(Id(10)).CurrentHp);
        Assert.Equal(3, result.Candidate.GetActor(Id(10)).CurrentMp);
        Assert.Empty(result.LevelsGained);
    }

    [Fact]
    public void LargeRewardTraversesEveryAuditedThresholdAndRetainsSurplus()
    {
        var result = ActorRewardPlanner.Plan(Create(), Id(10), 0, Reward(11140, 0));
        Assert.True(result.Succeeded);
        Assert.Equal(Enumerable.Range(2, 9), result.LevelsGained);
        Assert.Equal(10, result.Candidate.GetActor(Id(10)).Level);
        Assert.Equal(38UL, result.Candidate.GetActor(Id(10)).Experience);
        Assert.Equal(141, result.Candidate.GetActor(Id(10)).CurrentHp);
        Assert.Equal(28, result.Candidate.GetActor(Id(10)).CurrentMp);
    }

    [Fact]
    public void UnassignedNpcUsesTheSameRewardAlgorithmWithoutAcquiringAnInventedClass()
    {
        var result = ActorRewardPlanner.Plan(Create(), Id(11), 0, Reward());
        Assert.True(result.Succeeded);
        ActorProgressionState npc = result.Candidate.GetActor(Id(11));
        Assert.Equal(ActorStatProfileKind.Unassigned, npc.Profile.Kind);
        Assert.Equal(3, npc.Level);
        Assert.Equal(38UL, npc.Experience);
        Assert.Equal(new ActorStatValues(1, 0, 0, 0, 0, 1), npc.Stats);
        Assert.Equal(200UL, result.Candidate.Currency.GetActorAccount(Id(11)).Balance);
        Assert.Equal(0UL, result.Candidate.Currency.GetActorAccount(Id(10)).Balance);
    }

    [Fact]
    public void MissingReserveFundsRejectBothExperienceAndCurrencyWithoutPartialChanges()
    {
        ProgressionSnapshot initial = Create(reserveBalance: 199);
        var rejected = ActorRewardPlanner.Plan(initial, Id(10), 0, Reward());
        Assert.Equal(ActorRewardRejection.Unavailable, rejected.Rejection);
        Assert.Same(initial, rejected.Candidate);
        Assert.Empty(rejected.LevelsGained);
        Assert.Equal(0UL, initial.GetActor(Id(10)).Experience);
    }

    [Fact]
    public void CurrentActorRevisionRejectsARepeatBeforeASecondProgressionAward()
    {
        var initial = Create(reserveBalance: 400);
        var first = ActorRewardPlanner.Plan(initial, Id(10), 0, Reward());
        var stale = ActorRewardPlanner.Plan(first.Candidate, Id(10), 0, Reward());
        Assert.Equal(ActorRewardRejection.RevisionConflict, stale.Rejection);
        Assert.Same(first.Candidate, stale.Candidate);
        // A fresh revision can intentionally award the same definition again. Exactly-once quest
        // completion belongs to the world transaction, not to this reusable reward arithmetic.
        var authorizedAgain = ActorRewardPlanner.Plan(first.Candidate, Id(10), 1, Reward());
        Assert.True(authorizedAgain.Succeeded);
        Assert.Equal(400UL, authorizedAgain.Candidate.GetAccount(Id(201)).Balance);
    }

    [Fact]
    public void WrongCurrencyPinAndActorAccountAsReserveCannotPayRewards()
    {
        var initial = Create();
        CurrencyDefinition changed = new(Gil().DefinitionId, "gil", "Altered", "gil");
        var wrongPin = new ActorRewardDefinition(Id(130), "reward", 420, changed, Id(200), 200);
        var wrongRole = new ActorRewardDefinition(Id(130), "reward", 420, Gil(), Id(201), 0);
        Assert.Equal(ActorRewardRejection.Unavailable, ActorRewardPlanner.Plan(initial, Id(10), 0, wrongPin).Rejection);
        Assert.Equal(ActorRewardRejection.Unavailable, ActorRewardPlanner.Plan(initial, Id(10), 0, wrongRole).Rejection);
        Assert.Equal(ActorRewardRejection.Unavailable, ActorRewardPlanner.Plan(initial, Id(999), 0, Reward()).Rejection);
    }

    [Fact]
    public void RevisionOrExperienceOverflowRejectsTheWholeCandidate()
    {
        var actorAtMaximumRevision = new ActorProgressionState(Id(10), ulong.MaxValue, Squire(), Curve(), 1, 0, 60, 10);
        var maximumExperience = new ActorProgressionState(Id(10), 0, Squire(), Curve(), 10, ulong.MaxValue, 60, 10);
        ProgressionSnapshot[] states =
        [
            Create(actorAtMaximumRevision), Create(maximumExperience),
            Create(snapshotRevision: ulong.MaxValue), Create(currencyRevision: ulong.MaxValue),
            Create(reserveRevision: ulong.MaxValue), Create(playerAccountRevision: ulong.MaxValue)
        ];
        foreach (ProgressionSnapshot initial in states)
        {
            var result = ActorRewardPlanner.Plan(initial, Id(10), initial.GetActor(Id(10)).Revision, Reward());
            Assert.Equal(ActorRewardRejection.CapacityExceeded, result.Rejection);
            Assert.Same(initial, result.Candidate);
            Assert.Equal(200UL, initial.GetAccount(Id(200)).Balance);
        }
    }

    [Fact]
    public void StatOverflowDoesNotDebitCurrencyOrPartiallyAwardExperience()
    {
        ActorStatProfileDefinition profile = new(Id(100), "overflow-test", "Overflow", ActorStatProfileKind.Class,
            new(60, 10, 12, 4, 8, 8), new(int.MaxValue, 0, 0, 0, 0, 0), ActorStatModifiers.Zero);
        var actor = new ActorProgressionState(Id(10), 0, profile, Curve(), 1, 0, 60, 10);
        ProgressionSnapshot initial = Create(actor);
        var result = ActorRewardPlanner.Plan(initial, Id(10), 0, Reward());
        Assert.Equal(ActorRewardRejection.CapacityExceeded, result.Rejection);
        Assert.Same(initial, result.Candidate);
    }
}
