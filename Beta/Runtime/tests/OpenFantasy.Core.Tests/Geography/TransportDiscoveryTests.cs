using OpenFantasy.Core.Geography;
using static OpenFantasy.Core.Tests.Geography.ReservoirTransportFixture;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class TransportDiscoveryTests
{
    [Fact]
    public void Scheduling_and_cancellation_require_the_effective_owner_and_discovery_moves_no_stock()
    {
        var f = new ReservoirTransportFixture();
        Assert.Empty(f.State.DiscoverDue(f.Due).Stages);
        var scheduled = f.Scheduled();
        var hash = scheduled.ContentHash;
        var discovery = scheduled.DiscoverDue(f.Due);
        Assert.True(discovery.IsFor(scheduled));
        Assert.False(discovery.IsFor(f.State));
        Assert.Equal(Stage, Assert.Single(discovery.Stages).StageId);
        Assert.Equal(hash, scheduled.ContentHash);
        var canceled = scheduled.PlanCancel(Budget).ApplyTo(scheduled);
        Assert.False(discovery.IsFor(canceled));
        Assert.Empty(canceled.DiscoverDue(f.Due).Stages);
        Assert.Equal(scheduled.Reservoirs.Capacity.Inventory.ContentHash, canceled.Reservoirs.Capacity.Inventory.ContentHash);
    }

    [Fact]
    public void Holds_and_shared_throughput_keep_the_same_due_stage_until_a_later_lawful_completion()
    {
        var f = new ReservoirTransportFixture(freeSource: 10000, secondBudget: true);
        var scheduled = f.Scheduled();
        scheduled = scheduled.PlanSchedule(Id(30), SecondBudget, f.Due, f.Routes).ApplyTo(scheduled);
        var original = scheduled.DiscoverDue(f.Due);
        Assert.Equal(2, original.Stages.Count);
        var held = scheduled.PlanAdvance(f.Due, readingHold: true).ApplyTo(scheduled);
        Assert.Equal(original.Stages, held.DiscoverDue(held.Boundary).Stages);
        Assert.Same(held, held.PlanAdvance(held.Boundary).ApplyTo(held));
        var first = held.PlanAdvance(held.Boundary.AddMilliseconds(1800)).ApplyTo(held);
        var blocked = Assert.Single(first.DiscoverDue(first.Boundary.AddMilliseconds(1800)).Stages);
        Assert.Equal(Id(30), blocked.StageId);
        Assert.Equal(SecondBudget, blocked.ReservationId);
        Assert.Equal(f.Due, blocked.DueAt);
        Assert.Equal(RunoffTransportStatus.BlockedByThroughput, blocked.Status);
        Assert.Equal(original.Stages.Single(x => x.StageId == blocked.StageId).FundingHash, blocked.FundingHash);
        var last = first.PlanAdvance(first.Boundary.AddMilliseconds(1800)).ApplyTo(first);
        Assert.Empty(last.DiscoverDue(last.Boundary).Stages);
        Assert.All(last.Stages, stage => Assert.Equal(RunoffTransportStatus.Completed, stage.Status));
    }

    [Fact]
    public void Blocked_head_remains_due_without_losing_its_reservation_or_earning_extra_throughput()
    {
        var f = new ReservoirTransportFixture(receiverUnits: 1001);
        var scheduled = f.Scheduled();
        var blocked = scheduled.PlanAdvance(f.Due).ApplyTo(scheduled);
        var occurrence = Assert.Single(blocked.DiscoverDue(blocked.Boundary).Stages);
        Assert.Equal(RunoffTransportStatus.BlockedByHead, occurrence.Status);
        Assert.Equal(Budget, blocked.Reservoirs.Capacity.Inventory.Custody.GetLot(HeldLot).ReservedBy);
        Assert.Equal(scheduled.Reservoirs.ContentHash, blocked.Reservoirs.ContentHash);
        Assert.Throws<ArgumentException>(() => blocked.DiscoverDue(blocked.Boundary.AddMilliseconds(1)));
        Assert.Throws<ArgumentException>(() => blocked.DiscoverDue(blocked.Boundary.AddMilliseconds(3600)));
    }

    [Fact]
    public void Reordered_owner_enumeration_keeps_the_exact_due_order_and_transport_bytes()
    {
        var f = new ReservoirTransportFixture(freeSource: 10000, secondBudget: true);
        var scheduled = f.Scheduled();
        scheduled = scheduled.PlanSchedule(Id(30), SecondBudget, f.Due, f.Routes).ApplyTo(scheduled);
        var reversed = new ReservoirTransportState(scheduled.Reservoirs, scheduled.Connections.Reverse(),
            scheduled.Stages.Reverse(), scheduled.Revision, scheduled.Boundary);
        Assert.Equal(scheduled.DiscoverDue(f.Due).Stages.Select(x => x.StageId), reversed.DiscoverDue(f.Due).Stages.Select(x => x.StageId));
        Assert.Equal(scheduled.PlanAdvance(f.Due).ApplyTo(scheduled).ContentHash,
            reversed.PlanAdvance(f.Due).ApplyTo(reversed).ContentHash);
        Assert.False(scheduled.DiscoverDue(f.Due).IsFor(reversed));
    }
}
