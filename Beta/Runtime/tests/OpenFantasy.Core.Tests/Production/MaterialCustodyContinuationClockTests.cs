using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Tests.Gathering;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;

namespace OpenFantasy.Core.Tests.Production;

public sealed class MaterialCustodyContinuationClockTests
{
    [Fact]
    public void Bulk_idle_recovery_matches_old_one_step_rules_after_an_actual_partial_harvest()
    {
        var journey = MaterialCustodyContinuationFixture.Create();
        journey.Gather(74000);
        var before = journey.Gathering;
        Assert.Equal(600U, before.GetActorWork(Id(10)).Effort);
        Assert.Equal(3UL, before.GetNode(GatheringFixture.Node).NextUnissuedUnit);
        foreach (ulong elapsed in new ulong[] { 0, 1, 7, 39, 40, 41, 901 })
            AssertEquivalentToSteps(before, before.AdvancingStep + elapsed);
    }

    [Theory]
    [InlineData(15UL, false)]
    [InlineData(30UL, true)]
    public void Actual_depletion_recovers_once_at_the_old_threshold_only_if_a_full_cycle_remains(
        ulong capacity, bool canRegrow)
    {
        var before = Depleted(capacity);
        var node = before.GetNode(GatheringFixture.Node);
        Assert.Equal(0, node.RemainingSwings);
        Assert.Equal(15UL, node.NextUnissuedUnit);
        Assert.Equal(0U, before.GetActorWork(Id(10)).Effort);
        ulong due = node.RespawnAtAdvancingStep!.Value;
        foreach (ulong target in new[] { before.AdvancingStep, before.AdvancingStep + 1, due - 1, due, due + 1 })
            AssertEquivalentToSteps(before, target);
        var recovered = before.AdvanceTo(due).GetNode(node.NodeId);
        Assert.Equal(canRegrow ? node.Definition.MaxSwings : 0, recovered.RemainingSwings);
        Assert.Equal(canRegrow ? node.Revision + 1 : node.Revision, recovered.Revision);
        Assert.Equal(node.NextUnissuedUnit, recovered.NextUnissuedUnit);
    }

    [Fact]
    public void Equal_backward_and_ulong_max_boundaries_preserve_exact_cursors_without_effort_overflow()
    {
        var before = Depleted(30);
        Assert.Same(before, before.AdvanceTo(before.AdvancingStep));
        Assert.Throws<ArgumentOutOfRangeException>(() => before.AdvanceTo(before.AdvancingStep - 1));
        var maximum = before.AdvanceTo(ulong.MaxValue);
        Assert.Equal(ulong.MaxValue, maximum.AdvancingStep);
        Assert.All(maximum.Actors, actor => Assert.Equal(GatherRulesV1.MaximumEffort, actor.Effort));
        var oldNode = before.GetNode(GatheringFixture.Node);
        var node = maximum.GetNode(oldNode.NodeId);
        Assert.Equal(oldNode.NextUnissuedUnit, node.NextUnissuedUnit);
        Assert.Equal(oldNode.Revision + 1, node.Revision);
        Assert.Null(node.DepletedAtStep);
        Assert.Same(maximum, maximum.AdvanceTo(ulong.MaxValue));
        var nearMaximum = before.AdvanceTo(ulong.MaxValue - 2);
        AssertEquivalentToSteps(nearMaximum, ulong.MaxValue);
    }

    private static GatheringSnapshot Depleted(ulong capacity)
    {
        var journey = MaterialCustodyContinuationFixture.Create(capacity);
        for (ulong swing = 0; swing < 5; swing++)
        {
            uint effort = journey.Gathering.GetActorWork(Id(10)).Effort;
            if (effort < GatherRulesV1.EffortPerSwing)
                journey.AdvanceSourceWorld(checked((int)((GatherRulesV1.EffortPerSwing - effort)
                    / GatherRulesV1.EffortPerAdvancingStep)));
            journey.Gather(75000 + swing);
        }
        return journey.Gathering;
    }

    private static void AssertEquivalentToSteps(GatheringSnapshot before, ulong target)
    {
        var oracle = before;
        while (oracle.AdvancingStep < target) oracle = oracle.AdvanceStep(oracle.AdvancingStep + 1);
        Assert.Equal(GatheringCodecV1.Encode(oracle), GatheringCodecV1.Encode(before.AdvanceTo(target)));
    }
}
