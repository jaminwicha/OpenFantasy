using OpenFantasy.Core.Progression;
using static OpenFantasy.Core.Tests.Progression.ProgressionFixture;

namespace OpenFantasy.Core.Tests.Progression;

public sealed class ActorResourceTransitionTests
{
    [Theory]
    [InlineData(0, 40U, 40)]
    [InlineData(7, 40U, 47)]
    [InlineData(55, 40U, 60)]
    [InlineData(59, uint.MaxValue, 60)]
    [InlineData(60, 40U, 60)]
    [InlineData(7, 0U, 7)]
    public void Restoration_adds_only_missing_health_without_changing_magic_or_progression(int hp, uint amount, int expected)
    {
        var actor = Actor(hp);
        var effect = actor.RestoreHealth(amount);

        Assert.Same(actor, effect.Before);
        Assert.Equal(ActorResourceOperation.HealthRestoration, effect.Operation);
        Assert.Equal((ulong)amount, effect.RequestedAmount);
        Assert.Equal((hp, expected, expected - hp), (effect.Health.Before, effect.Health.After, effect.Health.Delta));
        Assert.Equal((60, 60), (effect.Health.MaximumBefore, effect.Health.MaximumAfter));
        Assert.Equal((3, 3, 0), (effect.Magic.Before, effect.Magic.After, effect.Magic.Delta));
        Assert.Equal(1, effect.Candidate.Level);
        Assert.Equal(12UL, effect.Candidate.Experience);
        Assert.Equal(expected == hp ? 4UL : 5UL, effect.Candidate.Revision);
        Assert.Equal(expected != hp, effect.ChangesResources);
        if (expected == hp) Assert.Same(actor, effect.Candidate);
        Assert.Equal(hp, actor.CurrentHp);
    }

    [Theory]
    [InlineData(60, 8U, 52)]
    [InlineData(7, 40U, 0)]
    [InlineData(7, uint.MaxValue, 0)]
    [InlineData(0, 8U, 0)]
    [InlineData(7, 0U, 7)]
    public void Damage_clamps_at_zero_and_reports_actual_loss(int hp, uint amount, int expected)
    {
        var actor = Actor(hp);
        var effect = actor.TakeDamage(amount);
        Assert.Equal(ActorResourceOperation.Damage, effect.Operation);
        Assert.Equal(expected, effect.Candidate.CurrentHp);
        Assert.Equal(expected - hp, effect.Health.Delta);
        Assert.Equal(3, effect.Candidate.CurrentMp);
        Assert.Equal(expected == hp ? 4UL : 5UL, effect.Candidate.Revision);
    }

    [Theory]
    [InlineData(0U, 3, 4UL)]
    [InlineData(2U, 1, 5UL)]
    [InlineData(3U, 0, 5UL)]
    public void Magic_expenditure_is_exact_and_zero_cost_does_not_revise_actor(uint amount, int expected, ulong revision)
    {
        var actor = Actor(7);
        var effect = actor.SpendMagic(amount);
        Assert.Equal(ActorResourceOperation.MagicExpenditure, effect.Operation);
        Assert.Equal(expected, effect.Candidate.CurrentMp);
        Assert.Equal(-(int)amount, effect.Magic.Delta);
        Assert.Equal(7, effect.Candidate.CurrentHp);
        Assert.Equal(revision, effect.Candidate.Revision);
        if (amount == 0) Assert.Same(actor, effect.Candidate);
    }

    [Theory]
    [InlineData(4U)]
    [InlineData(uint.MaxValue)]
    public void Insufficient_magic_cannot_be_partially_spent(uint amount)
    {
        var actor = Actor(7);
        Assert.Throws<InvalidOperationException>(() => actor.SpendMagic(amount));
        Assert.Equal(3, actor.CurrentMp);
        Assert.Equal(4UL, actor.Revision);
    }

    [Fact]
    public void Large_health_capacity_uses_missing_amount_without_addition_overflow()
    {
        var profile = new ActorStatProfileDefinition(Id(100), "large", "Large", ActorStatProfileKind.Class,
            new(int.MaxValue, 10, 1, 1, 1, 1), ActorStatModifiers.Zero, ActorStatModifiers.Zero);
        var actor = new ActorProgressionState(Id(10), 0, profile, Curve(), 1, 0, int.MaxValue - 1, 0);
        var effect = actor.RestoreHealth(uint.MaxValue);
        Assert.Equal(int.MaxValue, effect.Candidate.CurrentHp);
        Assert.Equal(1, effect.Health.Delta);
    }

    [Fact]
    public void Exhausted_actor_revision_cannot_publish_a_resource_change()
    {
        var actor = new ActorProgressionState(Id(10), ulong.MaxValue, Squire(), Curve(), 1, 0, 7, 3);
        Assert.Throws<OverflowException>(() => actor.TakeDamage(1));
        Assert.Throws<OverflowException>(() => actor.RestoreHealth(1));
        Assert.Throws<OverflowException>(() => actor.SpendMagic(1));
        Assert.Same(actor, actor.SpendMagic(0).Candidate);
    }

    [Theory]
    [InlineData(50UL, 1, 7, 3, false)]
    [InlineData(420UL, 3, 78, 14, true)]
    public void Reward_reports_the_same_transition_without_a_second_resource_revision(
        ulong xp, int level, int hp, int mp, bool resourcesChanged)
    {
        var actor = Actor(7);
        var initial = Create(actor);
        var plan = ActorRewardPlanner.Plan(initial, actor.ActorId, actor.Revision, Reward(xp));
        Assert.True(plan.Succeeded);
        var effect = Assert.IsType<ActorResourceTransition>(plan.ActorTransition);
        Assert.Same(actor, effect.Before);
        Assert.Same(plan.Candidate.GetActor(actor.ActorId), effect.Candidate);
        Assert.Equal(ActorResourceOperation.ExperienceReward, effect.Operation);
        Assert.Equal(xp, effect.RequestedAmount);
        Assert.Equal((level, hp, mp, 5UL),
            (effect.Candidate.Level, effect.Candidate.CurrentHp, effect.Candidate.CurrentMp, effect.Candidate.Revision));
        Assert.Equal(resourcesChanged, effect.ChangesResources);
        Assert.Equal(1UL, plan.Candidate.Revision);
    }

    [Fact]
    public void Rejected_reward_exposes_no_effect()
    {
        var plan = ActorRewardPlanner.Plan(Create(Actor(7), reserveBalance: 0), Id(10), 4, Reward());
        Assert.False(plan.Succeeded);
        Assert.Null(plan.ActorTransition);
    }

    [Fact]
    public void Damage_healing_magic_and_level_gain_preserve_order_and_current_owner()
    {
        var initial = new ActorProgressionState(Id(10), 0, Squire(), Curve(), 1, 0, 60, 10);
        var damage = initial.TakeDamage(53);
        var healing = damage.Candidate.RestoreHealth(40);
        var magic = healing.Candidate.SpendMagic(7);
        var reward = ActorRewardPlanner.Plan(Create(magic.Candidate), Id(10), 3, Reward(100, 0));
        var secondAttack = reward.Candidate.GetActor(Id(10)).TakeDamage(8);

        Assert.Equal(new[] { 60, 7, 47, 47, 69, 61 }, new[] { initial.CurrentHp, damage.Candidate.CurrentHp,
            healing.Candidate.CurrentHp, magic.Candidate.CurrentHp, reward.Candidate.GetActor(Id(10)).CurrentHp,
            secondAttack.Candidate.CurrentHp });
        Assert.Equal(5UL, secondAttack.Candidate.Revision);
        Assert.Equal(12, secondAttack.Candidate.CurrentMp);
        Assert.Equal(7, damage.Health.After); // The earlier event remains true after later healing.
    }

    private static ActorProgressionState Actor(int hp) => new(Id(10), 4, Squire(), Curve(), 1, 12, hp, 3);
}
