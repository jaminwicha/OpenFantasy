using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using static OpenFantasy.Core.Tests.Geography.ReservoirTransportFixture;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class ReservoirTransportContinuationCompatibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Legacy_transport_vectors_retain_real_schedule_delivery_split_block_and_cancel(bool consumption)
    {
        foreach (bool split in new[] { false, true })
        {
            var fixture = new ReservoirTransportFixture(freeSource: 10000, splitDestinations: split);
            var initial = consumption ? EnableProduction(fixture.State) : fixture.State;
            var scheduled = initial.PlanSchedule(Stage, Budget, fixture.Due, fixture.Routes).ApplyTo(initial);
            var completed = scheduled.PlanAdvance(fixture.Due).ApplyTo(scheduled);
            Assert.Equal(RunoffTransportStatus.Completed, Assert.Single(completed.Stages).Status);
            Assert.Empty(completed.Reservoirs.Capacity.Pending);
            Assert.Contains(Budget, completed.Reservoirs.Capacity.Closed);
            Assert.DoesNotContain(completed.Reservoirs.Capacity.Inventory.Custody.Lots, lot => lot.ReservedBy == Budget);
            Assert.Equal(Receiver, completed.Reservoirs.Capacity.Inventory.Custody.GetLot(HeldLot).CustodianId);
            if (split)
                Assert.Equal(new OpenFantasy.Core.Custody.SourceInterval(Source, 1000, 2000),
                    completed.Reservoirs.Capacity.Inventory.Custody.GetLot(EntityId.DeriveSpawned(Stage, 0)).Source);
            Retain($"{(consumption ? "v2" : "v1")}-{(split ? "split" : "single")}-initial", initial, consumption);
            Retain($"{(consumption ? "v2" : "v1")}-{(split ? "split" : "single")}-scheduled", scheduled, consumption);
            Retain($"{(consumption ? "v2" : "v1")}-{(split ? "split" : "single")}-completed", completed, consumption);
        }
        var blockedFixture = new ReservoirTransportFixture(receiverUnits: 1001);
        var basis = consumption ? EnableProduction(blockedFixture.State) : blockedFixture.State;
        var pending = basis.PlanSchedule(Stage, Budget, blockedFixture.Due, blockedFixture.Routes).ApplyTo(basis);
        var blocked = pending.PlanAdvance(blockedFixture.Due).ApplyTo(pending);
        Assert.Equal(RunoffTransportStatus.BlockedByHead, Assert.Single(blocked.Stages).Status);
        var canceled = blocked.PlanCancel(Budget).ApplyTo(blocked);
        Assert.Equal(RunoffTransportStatus.Canceled, Assert.Single(canceled.Stages).Status);
        Retain($"{(consumption ? "v2" : "v1")}-blocked", blocked, consumption);
        Retain($"{(consumption ? "v2" : "v1")}-canceled", canceled, consumption);
    }

    private static ReservoirTransportState EnableProduction(ReservoirTransportState original)
    {
        var request = Assert.Single(original.Reservoirs.Capacity.Pending).Request;
        var production = new ProductionSnapshot(request.Inventory.WorldId, request.Inventory.BranchId, 0, [], [], [], []);
        var prepared = new WatershedRunoffPlanner().Prepare(new(request.Network, request.Mapping, request.Inventory,
            production, request.ReservationId, request.OwnerActorId, request.Start, request.End, request.Material,
            request.Inputs, request.Terminals), TestContext.Current.CancellationToken);
        return ReservoirTransportState.Create(LiquidReservoirState.Create(WatershedRunoffCapacityState.Create(prepared),
            original.Reservoirs.Geometries), original.Connections, original.Boundary);
    }

    private static void Retain(string name, ReservoirTransportState state, bool consumption)
    {
        byte[] bytes = consumption ? ReservoirTransportCodecV2.Encode(state) : ReservoirTransportCodecV1.Encode(state);
        var restored = consumption ? ReservoirTransportCodecV2.Decode(bytes, TestContext.Current.CancellationToken)
            : ReservoirTransportCodecV1.Decode(bytes, TestContext.Current.CancellationToken);
        Assert.Equal(bytes, consumption ? ReservoirTransportCodecV2.Encode(restored) : ReservoirTransportCodecV1.Encode(restored));
        string? directory = Environment.GetEnvironmentVariable("OPENFANTASY_TRANSPORT_EVIDENCE");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, name + ".bin"), bytes);
    }
}
