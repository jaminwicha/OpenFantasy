using OpenFantasy.Core;
using OpenFantasy.Core.Geography;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ReservoirTransportTests
{
    internal static readonly EntityId Connection = ReservoirWorldFixture.Id(600);
    internal static readonly EntityId Stage = ReservoirWorldFixture.Id(601);

    internal static ReservoirTransportState Transport(ReservoirWorldFixture fixture, ulong throughput = 600) =>
        ReservoirTransportState.Create(fixture.Reservoirs,
            [new ReservoirConnection(Connection, ReservoirWorldFixture.Id(30), ReservoirWorldFixture.Id(31),
                0, 0, 1, 0, 0, throughput)], fixture.Basis.Calendar);

    [Fact]
    public void ADeclaredDueStageMovesExactSourceStockAndClosesItsBudgetAsCompleted()
    {
        var fixture = new ReservoirWorldFixture();
        var state = Transport(fixture);
        var due = fixture.Basis.Calendar.AddMilliseconds(3600);
        state = state.PlanSchedule(Stage, ReservoirWorldFixture.Reservation, due,
            [new(fixture.Reservoirs.Capacity.Domain.Network.Basins[0].BasinId, Connection)]).ApplyTo(state);
        var before = state.PlanAdvance(due.AddMilliseconds(-1800)).ApplyTo(state);
        Assert.Equal(state.Reservoirs.ContentHash, before.Reservoirs.ContentHash);
        state = before;
        var final = state.PlanAdvance(due).ApplyTo(state);
        Assert.Equal(RunoffTransportStatus.Completed, Assert.Single(final.Stages).Status);
        Assert.Equal(700UL, final.Reservoirs.Projections.Single(p => p.Geometry.CustodianId == ReservoirWorldFixture.SourceStore).ExistingUnits);
        Assert.Equal(650UL, final.Reservoirs.Projections.Single(p => p.Geometry.CustodianId == ReservoirWorldFixture.Receiver).ExistingUnits);
        var moved = final.Reservoirs.Capacity.Inventory.Custody.GetLot(ReservoirWorldFixture.Id(10));
        Assert.Equal(ReservoirWorldFixture.Receiver, moved.CustodianId);
        Assert.Null(moved.ReservedBy);
        Assert.Equal(fixture.Reservoirs.Capacity.Inventory.Custody.GetLot(moved.LotId).Source, moved.Source);
        Assert.Empty(final.Reservoirs.Capacity.Pending);
        Assert.Contains(ReservoirWorldFixture.Reservation, final.Reservoirs.Capacity.Closed);
        Assert.Equal(final.ContentHash, final.PlanAdvance(due).ApplyTo(final).ContentHash);
    }
}
