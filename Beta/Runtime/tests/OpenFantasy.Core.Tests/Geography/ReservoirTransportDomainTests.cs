using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Custody;
using static OpenFantasy.Core.Tests.Geography.ReservoirTransportFixture;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class ReservoirTransportDomainTests
{
    [Theory]
    [InlineData(1000UL, true)]
    [InlineData(1001UL, false)]
    [InlineData(2000UL, false)]
    public void ActualPostTransferHeadsRespectSingleMicrolitreRemainders(ulong receiverUnits, bool completes)
    {
        var f = new ReservoirTransportFixture(receiverUnits: receiverUnits);
        var state = f.Scheduled();
        var result = state.PlanAdvance(f.Due).ApplyTo(state);
        Assert.Equal(completes ? RunoffTransportStatus.Completed : RunoffTransportStatus.BlockedByHead, result.Stages[0].Status);
        Assert.Equal(state.Reservoirs.Capacity.Inventory.Custody.Lots.Aggregate(0UL, (sum, l) => sum + l.Quantity),
            result.Reservoirs.Capacity.Inventory.Custody.Lots.Aggregate(0UL, (sum, l) => sum + l.Quantity));
        if (!completes)
        {
            Assert.Equal(state.Reservoirs.ContentHash, result.Reservoirs.ContentHash);
            Assert.Equal(Budget, result.Reservoirs.Capacity.Inventory.Custody.GetLot(HeldLot).ReservedBy);
        }
    }

    [Fact]
    public void SharedGateThroughputAndPauseUseOnlyOneExactSelectedStepWithoutCatchup()
    {
        var f = new ReservoirTransportFixture(freeSource: 10000, secondBudget: true);
        var state = f.Scheduled();
        state = state.PlanSchedule(Id(30), SecondBudget, f.Due, f.Routes).ApplyTo(state);
        var held = state.PlanAdvance(f.Due, readingHold: true).ApplyTo(state);
        Assert.Equal(state.Reservoirs.ContentHash, held.Reservoirs.ContentHash);
        Assert.All(held.Stages, s => Assert.Equal(RunoffTransportStatus.Scheduled, s.Status));
        var first = held.PlanAdvance(f.Due.AddMilliseconds(1800)).ApplyTo(held);
        Assert.Equal(RunoffTransportStatus.Completed, first.Stages.Single(s => s.StageId == Stage).Status);
        Assert.Equal(RunoffTransportStatus.BlockedByThroughput, first.Stages.Single(s => s.StageId == Id(30)).Status);
        Assert.Equal(first.ContentHash, first.PlanAdvance(first.Boundary).ApplyTo(first).ContentHash);
        Assert.Throws<ArgumentException>(() => first.PlanAdvance(first.Boundary.AddMilliseconds(1)));
        Assert.Throws<ArgumentException>(() => first.PlanAdvance(first.Boundary.AddMilliseconds(3600)));
        var final = first.PlanAdvance(first.Boundary.AddMilliseconds(1800)).ApplyTo(first);
        Assert.All(final.Stages, s => Assert.Equal(RunoffTransportStatus.Completed, s.Status));
        Assert.Empty(final.Reservoirs.Capacity.Pending);
    }

    [Fact]
    public void OneHeldLotSplitAcrossDifferentDrainageTerminalsPreservesEverySourceUnitAndIdentity()
    {
        var f = new ReservoirTransportFixture(freeSource: 10000, receiverUnits: 0, splitDestinations: true);
        var state = f.Scheduled();
        var plan = state.PlanAdvance(f.Due);
        var final = plan.ApplyTo(state);
        Assert.Equal(RunoffTransportStatus.Completed, final.Stages[0].Status);
        Assert.Equal(EntityId.DeriveSpawned(Stage, 0), Assert.Single(plan.CreatedIds));
        var delivered = final.Reservoirs.Capacity.Inventory.Custody.Lots.Where(l => l.Source.Start < 2000).OrderBy(l => l.Source.Start).ToArray();
        Assert.Equal(2, delivered.Length);
        Assert.Equal(new SourceInterval(Source, 0, 1000), delivered[0].Source);
        Assert.Equal(new SourceInterval(Source, 1000, 2000), delivered[1].Source);
        Assert.Equal(Receiver, delivered[0].CustodianId);
        Assert.Equal(Id(26), delivered[1].CustodianId);
        Assert.Equal(HeldLot, delivered[0].LotId);
        Assert.Equal(plan.CreatedIds[0], delivered[1].LotId);
        Assert.All(delivered, l => Assert.Null(l.ReservedBy));
        var blocked = state.PlanAdvance(f.Due, historicallyOccupied: id => id == plan.CreatedIds[0]).ApplyTo(state);
        Assert.Equal(RunoffTransportStatus.BlockedByMaterial, blocked.Stages[0].Status);
        Assert.Equal(state.Reservoirs.ContentHash, blocked.Reservoirs.ContentHash);
        Assert.Throws<InvalidOperationException>(() => plan.ApplyTo(blocked));
    }

    [Fact]
    public void OneBlockedGateRollsBackTheWholeMultiDestinationStageIncludingFragments()
    {
        var f = new ReservoirTransportFixture(freeSource: 1000, receiverUnits: 1001, splitDestinations: true);
        var state = f.Scheduled();
        var plan = state.PlanAdvance(f.Due);
        var blocked = plan.ApplyTo(state);
        Assert.Equal(RunoffTransportStatus.BlockedByHead, blocked.Stages[0].Status);
        Assert.Empty(plan.CreatedIds);
        Assert.Equal(state.Reservoirs.ContentHash, blocked.Reservoirs.ContentHash);
        Assert.Equal(state.Reservoirs.Capacity.Inventory.Custody.Lots, blocked.Reservoirs.Capacity.Inventory.Custody.Lots);
        Assert.Equal(state.Reservoirs.Capacity.ContentHash, blocked.Reservoirs.Capacity.ContentHash);
    }

    [Fact]
    public void CancellationRecordsItsOwnOutcomeAndNeverReleasesSourceHoldsOrMovesWater()
    {
        var f = new ReservoirTransportFixture();
        var state = f.Scheduled();
        var canceled = state.PlanCancel(Budget).ApplyTo(state);
        Assert.Equal(RunoffTransportStatus.Canceled, canceled.Stages[0].Status);
        Assert.Equal(state.Reservoirs.Capacity.Inventory.ContentHash, canceled.Reservoirs.Capacity.Inventory.ContentHash);
        Assert.Equal(Budget, canceled.Reservoirs.Capacity.Inventory.Custody.GetLot(HeldLot).ReservedBy);
        Assert.Equal(canceled.Reservoirs.ContentHash, canceled.PlanAdvance(f.Due).ApplyTo(canceled).Reservoirs.ContentHash);
        Assert.Throws<ArgumentException>(() => canceled.PlanSchedule(Id(31), Budget, f.Due, f.Routes));
    }

    [Fact]
    public void GatesRequireActualAdjacentPortsAndCannotAliasAnOpposingOpening()
    {
        var f = new ReservoirTransportFixture();
        var original = f.State.Connections[0];
        Assert.Throws<ArgumentException>(() => new ReservoirConnection(Id(31), SourceGeometry, ReceiverGeometry, 0, 0, 2, 0, 0, 1000));
        Assert.Throws<ArgumentException>(() => ReservoirTransportState.Create(f.State.Reservoirs,
            [original, new(Id(31), ReceiverGeometry, SourceGeometry, 1, 0, 0, 0, 0, 1000)], f.State.Boundary));
        Assert.Throws<ArgumentException>(() => ReservoirTransportState.Create(f.State.Reservoirs,
            [new(Gate, SourceGeometry, ReceiverGeometry, 0, 0, 1, 0, 1, 1000)], f.State.Boundary));
        Assert.Throws<ArgumentException>(() => f.State.PlanSchedule(Stage, Budget, f.Due,
            [new(f.Routes[0].BasinId, Id(31))]));
        Assert.Throws<ArgumentException>(() => new ReservoirTransportFixture(maximumUnits: 999).Scheduled());
        Assert.Throws<ArgumentException>(() => f.State.PlanSchedule(Stage, Budget, new(0, 1799), f.Routes));
        Assert.Throws<ArgumentException>(() => f.Scheduled().PlanSchedule(Id(31), Budget, f.Due, f.Routes));
    }
}
