using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Geography.ReservoirTransportFixture;
using static OpenFantasy.Core.Tests.Actors.BodyDecodingTestFrames;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class ConsumptionGeographyCodecTests
{
    [Fact]
    public void Explicit_versions_retain_consumption_through_forcing_capacity_reservoir_and_scheduled_transport()
    {
        var fixture = new ReservoirTransportFixture();
        var original = fixture.Scheduled();
        var enabled = EnableConsumption(fixture.State);
        enabled = enabled.PlanSchedule(Stage, Budget, fixture.Due, fixture.Routes).ApplyTo(enabled);
        var capacity = enabled.Reservoirs.Capacity;
        var inventory = capacity.Inventory;
        var lot = Assert.Single(inventory.Custody.Lots, value => value.CustodianId == Receiver);
        var plan = new ItemConsumptionPlanner().Plan(inventory, capacity.Production!, new(Id(7), Id(8)), Owner, Id(1000),
            new(Receiver, inventory.Custody.GetStorage(Receiver).Revision, capacity.Production!.Revision, Owner, Water,
                new(IngredientLotKind.Source, lot.LotId, lot.Revision, 7)));
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        var physicalPlan = enabled.Reservoirs.PlanApply(capacity.PlanReconcile(plan.CandidateInventory,
            plan.CandidateProduction, TestContext.Current.CancellationToken));
        var consumed = enabled.PlanApply(physicalPlan).ApplyTo(enabled);
        var consumedCapacity = consumed.Reservoirs.Capacity;
        var runoff = Assert.Single(consumedCapacity.Pending);
        var claim = Assert.Single(consumedCapacity.Production!.ConsumedItems);
        Assert.Equal(7UL, claim.Ingredient.Quantity);
        Assert.Equal(993UL, consumedCapacity.Inventory.Custody.GetStoredQuantity(Receiver));
        Assert.Equal(1000UL, runoff.TotalInputUnits);
        Assert.Equal(Assert.Single(enabled.Stages).FundingHash, Assert.Single(consumed.Stages).FundingHash);

        ConsumptionGeographyArchiveAssertions.Split(Assert.Single(original.Reservoirs.Capacity.Pending), runoff,
            WatershedRunoffCodecV1.Encode, WatershedRunoffCodecV2.Encode,
            (bytes, token) => WatershedRunoffCodecV1.Decode(bytes, token),
            (bytes, token) => WatershedRunoffCodecV2.Decode(bytes, token));
        ConsumptionGeographyArchiveAssertions.Split(original.Reservoirs.Capacity, consumedCapacity,
            WatershedRunoffCapacityCodecV1.Encode, WatershedRunoffCapacityCodecV2.Encode,
            (bytes, token) => WatershedRunoffCapacityCodecV1.Decode(bytes, token),
            (bytes, token) => WatershedRunoffCapacityCodecV2.Decode(bytes, token));
        ConsumptionGeographyArchiveAssertions.Split(original.Reservoirs, consumed.Reservoirs,
            LiquidReservoirCodecV1.Encode, LiquidReservoirCodecV2.Encode,
            (bytes, token) => LiquidReservoirCodecV1.Decode(bytes, token),
            (bytes, token) => LiquidReservoirCodecV2.Decode(bytes, token));
        ConsumptionGeographyArchiveAssertions.Split(original, consumed,
            ReservoirTransportCodecV1.Encode, ReservoirTransportCodecV2.Encode,
            (bytes, token) => ReservoirTransportCodecV1.Decode(bytes, token),
            (bytes, token) => ReservoirTransportCodecV2.Decode(bytes, token));

        Assert.Equal(ContentHash.Compute(WatershedRunoffCodecV2.EncodeRequest(runoff.Request)), runoff.Request.ContentHash);
        Assert.Equal(ContentHash.Compute(WatershedRunoffCodecV2.Encode(runoff)), runoff.ContentHash);
        Assert.Equal(ContentHash.Compute(WatershedRunoffCapacityCodecV2.Encode(consumedCapacity)), consumedCapacity.ContentHash);
        Assert.Equal(ContentHash.Compute(LiquidReservoirCodecV2.Encode(consumed.Reservoirs)), consumed.Reservoirs.ContentHash);
        Assert.Equal(ContentHash.Compute(ReservoirTransportCodecV2.Encode(consumed)), consumed.ContentHash);
        var restored = ReservoirTransportCodecV2.Decode(ReservoirTransportCodecV2.Encode(consumed), TestContext.Current.CancellationToken);
        Assert.Equal(claim.ConsumptionId, Assert.Single(restored.Reservoirs.Capacity.Production!.ConsumedItems).ConsumptionId);
        Assert.Equal(consumed.Reservoirs.Projections.Select(value => value.ExistingUnits),
            restored.Reservoirs.Projections.Select(value => value.ExistingUnits));
    }

    [Fact]
    public void Request_archive_and_nested_owners_refuse_missing_or_downgraded_consumption_evidence()
    {
        var fixture = new ReservoirTransportFixture();
        var enabled = EnableConsumption(fixture.State);
        var capacity = enabled.Reservoirs.Capacity;
        var request = Assert.Single(capacity.Pending).Request;
        byte[] bytes = WatershedRunoffCodecV2.EncodeRequest(request);
        Assert.Equal(bytes, WatershedRunoffCodecV2.EncodeRequest(WatershedRunoffCodecV2.DecodeRequest(bytes, TestContext.Current.CancellationToken)));
        Assert.Throws<ArgumentException>(() => WatershedRunoffCodecV2.EncodeRequest(Assert.Single(fixture.State.Reservoirs.Capacity.Pending).Request));
        byte[] legacyProduction = ProductionCodecV1.Encode(new(request.Inventory.WorldId, request.Inventory.BranchId, 0, [], [], []));
        foreach (byte[] replacement in new[] { Array.Empty<byte>(), legacyProduction })
        {
            Assert.ThrowsAny<FormatException>(() => WatershedRunoffCodecV2.DecodeRequest(Replace(bytes, 4, replacement), TestContext.Current.CancellationToken));
            Assert.ThrowsAny<FormatException>(() => WatershedRunoffCapacityCodecV2.Decode(
                Replace(WatershedRunoffCapacityCodecV2.Encode(capacity), 3, replacement), TestContext.Current.CancellationToken));
        }
        Assert.ThrowsAny<FormatException>(() => LiquidReservoirCodecV2.Decode(Replace(LiquidReservoirCodecV2.Encode(enabled.Reservoirs),
            1, WatershedRunoffCapacityCodecV1.Encode(fixture.State.Reservoirs.Capacity)), TestContext.Current.CancellationToken));
        Assert.ThrowsAny<FormatException>(() => ReservoirTransportCodecV2.Decode(Replace(ReservoirTransportCodecV2.Encode(enabled),
            3, LiquidReservoirCodecV1.Encode(fixture.State.Reservoirs)), TestContext.Current.CancellationToken));
        foreach (byte[] bad in new[] { bytes[..^1], bytes.Concat(new byte[] { 0 }).ToArray(),
            Replace(bytes, 0, Text("geography.watershed.runoff-request.v1")) })
            Assert.ThrowsAny<FormatException>(() => WatershedRunoffCodecV2.DecodeRequest(bad, TestContext.Current.CancellationToken));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => WatershedRunoffCodecV2.DecodeRequest(bytes, cancellation.Token));
    }

    [Fact]
    public void Even_empty_consumption_ownership_has_a_distinct_archive_and_cannot_be_erased()
    {
        var legacy = new ReservoirTransportFixture().State;
        var enabled = EnableConsumption(legacy);
        Assert.Empty(enabled.Reservoirs.Capacity.Production!.ConsumedItems);
        Assert.NotEqual(legacy.ContentHash, enabled.ContentHash);
        Assert.Throws<ArgumentException>(() => WatershedRunoffCodecV1.Encode(Assert.Single(enabled.Reservoirs.Capacity.Pending)));
        Assert.Throws<ArgumentException>(() => WatershedRunoffCapacityCodecV1.Encode(enabled.Reservoirs.Capacity));
        Assert.Throws<ArgumentException>(() => LiquidReservoirCodecV1.Encode(enabled.Reservoirs));
        Assert.Throws<ArgumentException>(() => ReservoirTransportCodecV1.Encode(enabled));
        Assert.Equal(ReservoirTransportCodecV2.Encode(enabled), ReservoirTransportCodecV2.Encode(
            ReservoirTransportCodecV2.Decode(ReservoirTransportCodecV2.Encode(enabled), TestContext.Current.CancellationToken)));
    }

    private static ReservoirTransportState EnableConsumption(ReservoirTransportState original)
    {
        // Fresh component composition; ordinary reconciliation cannot change the production interpretation.
        var request = Assert.Single(original.Reservoirs.Capacity.Pending).Request;
        var production = new ProductionSnapshot(request.Inventory.WorldId, request.Inventory.BranchId, 0, [], [], [], []);
        var prepared = new WatershedRunoffPlanner().Prepare(new(request.Network, request.Mapping, request.Inventory,
            production, request.ReservationId, request.OwnerActorId, request.Start, request.End, request.Material,
            request.Inputs, request.Terminals), TestContext.Current.CancellationToken);
        var reservoirs = LiquidReservoirState.Create(WatershedRunoffCapacityState.Create(prepared), original.Reservoirs.Geometries);
        return ReservoirTransportState.Create(reservoirs, original.Connections, original.Boundary);
    }
}

internal static class ConsumptionGeographyArchiveAssertions
{
    internal static void Split<T>(T legacy, T enabled, Func<T, byte[]> oldEncode, Func<T, byte[]> newEncode,
        Func<byte[], CancellationToken, T> oldDecode, Func<byte[], CancellationToken, T> newDecode)
    {
        byte[] before = oldEncode(legacy), after = newEncode(enabled);
        var token = TestContext.Current.CancellationToken;
        Assert.Equal(before, oldEncode(oldDecode(before, token)));
        Assert.Equal(after, newEncode(newDecode(after, token)));
        Assert.Throws<ArgumentException>(() => oldEncode(enabled));
        Assert.Throws<ArgumentException>(() => newEncode(legacy));
        Assert.ThrowsAny<FormatException>(() => oldDecode(after, token));
        Assert.ThrowsAny<FormatException>(() => newDecode(before, token));
        // A changed outer version cannot reinterpret a differently owned nested archive.
        Assert.ThrowsAny<FormatException>(() => oldDecode(Replace(after, 0, Fields(before)[0]), token));
        Assert.ThrowsAny<FormatException>(() => newDecode(Replace(before, 0, Fields(after)[0]), token));
        byte[] overlongField = (byte[])after.Clone();
        BinaryPrimitives.WriteUInt32BigEndian(overlongField.AsSpan(12), uint.MaxValue);
        foreach (byte[] bad in new[] { after[..^1], after.Concat(new byte[] { 0 }).ToArray(), overlongField })
            Assert.ThrowsAny<FormatException>(() => newDecode(bad, token));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => newDecode(after, cancellation.Token));
        Retain(typeof(T).Name + "-legacy", before);
        Retain(typeof(T).Name + "-new", after);
    }

    internal static void Retain(string name, byte[] bytes)
    {
        string? path = Environment.GetEnvironmentVariable("OPENFANTASY_CONSUMPTION_GEOGRAPHY_EVIDENCE");
        if (string.IsNullOrWhiteSpace(path)) return;
        Directory.CreateDirectory(path);
        File.WriteAllBytes(Path.Combine(path, name + ".bin"), bytes);
    }
}
