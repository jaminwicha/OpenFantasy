using System.Buffers.Binary;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Tests.Construction;
using OpenFantasy.Core.Tests.Worlds;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Geography.ReservoirTransportFixture;
using static OpenFantasy.Core.Tests.Actors.BodyDecodingTestFrames;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class ReservoirTransportContinuationCodecTests
{
    [Fact]
    public void Every_nested_archive_requires_the_real_transport_proof_and_exact_derived_prefix()
    {
        var journey = new ReservoirTransportContinuationFixture(secondBudget: true);
        journey.Schedule(Stage, Budget);
        journey.Cut(first: true);
        journey.Craft(IngredientLotKind.Source, Id(22), 4, Id(800));
        journey.Advance();
        var sibling = new ReservoirTransportContinuationFixture(secondBudget: true);
        sibling.Schedule(Stage, Budget);
        sibling.Cut(first: true);
        sibling.Craft(IngredientLotKind.Source, Id(22), 4, Id(801));
        sibling.Advance();
        var proof = Assert.Single(journey.Production.MaterialHistory!.TransportEvidence);
        var other = Assert.Single(sibling.Production.MaterialHistory!.TransportEvidence);
        Assert.Equal(proof.BeforeInventoryHash, other.BeforeInventoryHash);
        Assert.Equal(proof.AfterInventoryHash, other.AfterInventoryHash);
        Assert.NotEqual(proof.BeforeProductionHash, other.BeforeProductionHash);
        Assert.DoesNotContain(typeof(ReservoirTransportEvidence).GetConstructors(), c => c.IsPublic);
        var missing = new GeographyProductionDependencies(journey.State);
        var wrong = new GeographyProductionDependencies(journey.State, transportEvidence: [other]);
        var correct = journey.Dependencies;
        foreach (var frame in Frames(journey.State))
        {
            Assert.Equal(frame.Bytes, frame.Reopen(frame.Bytes, correct));
            Assert.ThrowsAny<FormatException>(() => frame.Reopen(frame.Bytes, missing));
            Assert.ThrowsAny<FormatException>(() => frame.Reopen(frame.Bytes, wrong));
        }
        // Resolve a genuine sibling proof successfully, then reject its different produced-history prefix.
        // Equal before/after raw custody alone must not authorize this substituted delivery.
        byte[] hostileProduction = ReplaceProof(ProductionCodecV7.Encode(journey.Production), proof, other);
        var decoded = ProductionCodecV7.Decode(hostileProduction, correct.Context, [], [], [other]);
        Assert.Equal(hostileProduction, ProductionCodecV7.Encode(decoded));
        var prefixFailure = Assert.Throws<ArgumentException>(() => decoded.ValidateForAuthority(journey.Inventory));
        Assert.Equal("Reserved delivery requires genuine evidence for the exact preceding material owners.", prefixFailure.Message);
        foreach (var frame in Frames(journey.State).Skip(1))
        {
            var failure = Assert.ThrowsAny<FormatException>(() => frame.Reopen(ReplaceProof(frame.Bytes, proof, other), wrong));
            Assert.Contains("Reserved delivery requires genuine evidence for the exact preceding material owners.", failure.ToString());
        }
        Assert.Throws<ArgumentException>(() => new GeographyProductionDependencies(journey.Capacity));
        Assert.Throws<ArgumentException>(() => new GeographyProductionDependencies(journey.State.Reservoirs));
        byte[] archived = ReservoirTransportCodecV3.Encode(journey.State);
        var original = journey.State;
        journey.Cut();
        Assert.ThrowsAny<FormatException>(() => ReservoirTransportCodecV3.Decode(archived, journey.Dependencies, TestContext.Current.CancellationToken));
        Assert.Equal(archived, ReservoirTransportCodecV3.Encode(original));
        foreach (var frame in Frames(journey.State)) Assert.Equal(frame.Bytes, frame.Reopen(frame.Bytes, journey.Dependencies));
    }

    [Theory]
    [InlineData("status")]
    [InlineData("gate")]
    [InlineData("boundary")]
    [InlineData("revision")]
    public void Qualified_archives_reject_structurally_legal_rewrites_of_nonmaterial_transport_facts(string change)
    {
        var legacy = new ReservoirTransportContinuationFixture();
        legacy.Schedule(Stage, Budget);
        legacy.Advance();
        byte[] old = ReservoirTransportCodecV2.Encode(legacy.State);
        byte[] alternative = ChangeTransport(old, change);
        var validAlternative = ReservoirTransportCodecV2.Decode(alternative, TestContext.Current.CancellationToken);
        Assert.Equal(alternative, ReservoirTransportCodecV2.Encode(validAlternative));
        Assert.NotEqual(legacy.State.ContentHash, validAlternative.ContentHash);

        var journey = new ReservoirTransportContinuationFixture();
        journey.Schedule(Stage, Budget);
        journey.Cut(first: true);
        journey.Advance();
        byte[] bytes = ReservoirTransportCodecV3.Encode(journey.State);
        Assert.ThrowsAny<FormatException>(() => ReservoirTransportCodecV3.Decode(ChangeTransport(bytes, change), journey.Dependencies,
            TestContext.Current.CancellationToken));
        Assert.Equal(bytes, ReservoirTransportCodecV3.Encode(journey.State));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Explicit_transport_and_nested_versions_never_downgrade_the_material_interpretation(bool consumption)
    {
        var journey = new ReservoirTransportContinuationFixture(secondBudget: true, consumption: consumption);
        journey.Schedule(Stage, Budget);
        journey.Cut(first: true);
        journey.Advance();
        Assert.True(journey.Production.Continuation!.SupportsReservedTransport);
        Assert.Equal(consumption, journey.Production.SupportsConsumption);
        foreach (var frame in Frames(journey.State)) Assert.Equal(frame.Bytes, frame.Reopen(frame.Bytes, journey.Dependencies));
        Assert.Throws<ArgumentException>(() => ReservoirTransportCodecV1.Encode(journey.State));
        Assert.Throws<ArgumentException>(() => ReservoirTransportCodecV2.Encode(journey.State));
        Assert.Throws<ArgumentException>(() => LiquidReservoirCodecV3.Encode(journey.State.Reservoirs));
        Assert.Throws<ArgumentException>(() => WatershedRunoffCapacityCodecV3.Encode(journey.Capacity));
        Assert.Throws<ArgumentException>(() => WatershedRunoffCodecV3.Encode(Assert.Single(journey.Capacity.Pending)));
        Assert.Throws<ArgumentException>(() => ProductionCodecV3.Encode(journey.Production));
        Assert.Throws<ArgumentException>(() => ProductionCodecV4.Encode(journey.Production));
        Assert.Throws<ArgumentException>(() => ProductionCodecV5.Encode(journey.Production));
        Assert.Throws<ArgumentException>(() => ProductionCodecV6.Encode(journey.Production));
        byte[] bytes = ReservoirTransportCodecV3.Encode(journey.State);
        Assert.ThrowsAny<FormatException>(() => ReservoirTransportCodecV2.Decode(bytes, TestContext.Current.CancellationToken));
        Assert.ThrowsAny<FormatException>(() => ReservoirTransportCodecV3.Decode(bytes[..^1], journey.Dependencies, TestContext.Current.CancellationToken));
        Assert.ThrowsAny<FormatException>(() => ReservoirTransportCodecV3.Decode([.. bytes, 0], journey.Dependencies, TestContext.Current.CancellationToken));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => ReservoirTransportCodecV3.Decode(bytes, journey.Dependencies, canceled.Token));
        var ordinary = new LiquidMaterialContinuationFixture();
        ordinary.Cut(material: true);
        Assert.Throws<ArgumentException>(() => ReservoirTransportCodecV3.Decode(bytes, ordinary.Dependencies, TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => LiquidReservoirCodecV4.Encode(ordinary.Reservoirs));
    }

    [Fact]
    public void Actual_construction_completion_and_dry_transport_keep_both_distinct_opaque_proofs_across_cuts()
    {
        // Both empty transport custodians and finite coarse stock were authored at original genesis,
        // before the actual two-project funding and 399 work actions performed by this fixture.
        var journey = new ConstructionContinuationFixture(readyToComplete: true, withLiquidReceiver: true, withTerrainMaterial: true);
        var legacy = journey.LegacyWorld;
        var scope = new TerrainScope(legacy.WorldId, legacy.BranchId, Id(900));
        var basin = new WatershedPlanner().Prepare(new(scope, Id(901), new(0, 0, 1, 1), new(0, 0), 0, Id(902)), TestContext.Current.CancellationToken);
        var network = new WatershedNetworkPlanner().Prepare(Id(903), [basin], [WatershedOutletClosure.ToTerminal(basin.BasinId, 1)], TestContext.Current.CancellationToken);
        var material = legacy.Production!.Recipes.First(r => r.Bindings.Any(b => b.Item == AdmissionFixture.Material)).BindingFor(AdmissionFixture.Material);
        var source = new ReservoirGeometry(scope, Id(904), ConstructionContinuationFixture.TerrainFine, 1, [new(0, 0, 0)]);
        var receiver = new ReservoirGeometry(scope, Id(905), ConstructionContinuationFixture.LiquidReceiver, 1, [new(1, 0, 0)]);
        var request = new WatershedRunoffRequest(network, new(1, 1, 0), legacy.Inventory, legacy.Production,
            ConstructionContinuationFixture.LiquidRunoffReservation, ConstructionContinuationFixture.Builder,
            new(0, 0), new(0, 1), material, [WatershedRunoffInput.Dry(basin.BasinId)],
            [new(basin.BasinId, receiver.CustodianId, receiver.Capacity)]);
        var physical = LiquidReservoirState.Create(WatershedRunoffCapacityState.Create(new WatershedRunoffPlanner().Prepare(request,
            TestContext.Current.CancellationToken)), [source, receiver]);
        var state = ReservoirTransportState.Create(physical, [new(Id(906), source.ReservoirId, receiver.ReservoirId, 0, 0, 1, 0, 0, 1000)], new(0, 0));
        state = state.PlanSchedule(Id(907), request.ReservationId, new(0, 1800), []).ApplyTo(state);
        state = state.PlanContinueTransport(legacy.Gathering, MaterialConstructionState.Capture(legacy), TestContext.Current.CancellationToken).ApplyTo(state);
        Assert.True(state.Reservoirs.Capacity.Production!.Continuation!.SupportsConstruction);
        journey.Advance();
        var completion = new MaterialConstructionPlanner().Plan(journey.SourceWorld, state.Reservoirs.Capacity.Inventory,
            state.Reservoirs.Capacity.Production!, journey.State, journey.Space, journey.Command(), true, false, journey.Occupied);
        Assert.Equal(WorldActionRejection.None, completion.Rejection);
        var construction = Assert.IsType<ConstructionCompletionEvidence>(completion.Evidence);
        Assert.Equal(20, completion.CandidatePhysical!.Members.Count);
        state = state.PlanApply(state.Reservoirs.PlanApply(state.Reservoirs.Capacity.PlanReconcile(completion.CandidateInventory,
            completion.CandidateProduction, TestContext.Current.CancellationToken))).ApplyTo(state);
        var beforeDelivery = state.Reservoirs.Capacity.Inventory;
        state = state.PlanAdvance(new(0, 1800), historicallyOccupied: journey.Occupied).ApplyTo(state);
        var transport = Assert.Single(state.Reservoirs.Capacity.Production!.MaterialHistory!.TransportEvidence);
        Assert.Equal(0UL, transport.Units);
        Assert.Equal(beforeDelivery.Custody.Revision + 1, state.Reservoirs.Capacity.Inventory.Custody.Revision);
        Assert.Equal(beforeDelivery.Custody.Storages, state.Reservoirs.Capacity.Inventory.Custody.Storages);
        Assert.Equal(RunoffTransportStatus.Completed, Assert.Single(state.Stages).Status);
        var correct = new GeographyProductionDependencies(state, constructionEvidence: [construction], transportEvidence: [transport]);
        byte[] bytes = ReservoirTransportCodecV3.Encode(state);
        Assert.Equal(bytes, ReservoirTransportCodecV3.Encode(ReservoirTransportCodecV3.Decode(bytes, correct, TestContext.Current.CancellationToken)));
        Assert.ThrowsAny<FormatException>(() => ReservoirTransportCodecV3.Decode(bytes,
            new GeographyProductionDependencies(state, transportEvidence: [transport]), TestContext.Current.CancellationToken));
        Assert.ThrowsAny<FormatException>(() => ReservoirTransportCodecV3.Decode(bytes,
            new GeographyProductionDependencies(state, constructionEvidence: [construction]), TestContext.Current.CancellationToken));
        for (int index = 0; index < 2; index++)
        {
            state = state.PlanContinue(TestContext.Current.CancellationToken).ApplyTo(state);
            Assert.Equal(2, state.Reservoirs.Capacity.Production!.Continuation!.ConstructionBatches.Count);
            Assert.True(state.Reservoirs.Capacity.Production.Continuation.SupportsReservedTransport);
            byte[] cut = ReservoirTransportCodecV3.Encode(state);
            Assert.Equal(cut, ReservoirTransportCodecV3.Encode(ReservoirTransportCodecV3.Decode(cut, new GeographyProductionDependencies(state), TestContext.Current.CancellationToken)));
        }
    }

    private static IEnumerable<(byte[] Bytes, Func<byte[], GeographyProductionDependencies, byte[]> Reopen)> Frames(ReservoirTransportState state)
    {
        var production = state.Reservoirs.Capacity.Production!;
        yield return (ProductionCodecV7.Encode(production), (b, d) => ProductionCodecV7.Encode(ProductionCodecV7.Decode(b, d.Context,
            d.GatherEvidence, d.ConstructionEvidence, d.TransportEvidence)));
        yield return (ReservoirTransportCodecV3.Encode(state), (b, d) => ReservoirTransportCodecV3.Encode(ReservoirTransportCodecV3.Decode(b, d, TestContext.Current.CancellationToken)));
        yield return (LiquidReservoirCodecV4.Encode(state.Reservoirs), (b, d) => LiquidReservoirCodecV4.Encode(LiquidReservoirCodecV4.Decode(b, d, TestContext.Current.CancellationToken)));
        yield return (WatershedRunoffCapacityCodecV4.Encode(state.Reservoirs.Capacity), (b, d) => WatershedRunoffCapacityCodecV4.Encode(WatershedRunoffCapacityCodecV4.Decode(b, d, TestContext.Current.CancellationToken)));
        foreach (var pending in state.Reservoirs.Capacity.Pending)
        {
            yield return (WatershedRunoffCodecV4.Encode(pending), (b, d) => WatershedRunoffCodecV4.Encode(WatershedRunoffCodecV4.Decode(b, d, TestContext.Current.CancellationToken)));
            yield return (WatershedRunoffCodecV4.EncodeRequest(pending.Request), (b, d) => WatershedRunoffCodecV4.EncodeRequest(WatershedRunoffCodecV4.DecodeRequest(b, d, TestContext.Current.CancellationToken)));
        }
    }

    private static byte[] ReplaceProof(byte[] bytes, ReservoirTransportEvidence original, ReservoirTransportEvidence replacement)
    {
        byte[] result = bytes.ToArray();
        byte[] before = original.ContentHash.ToByteArray(), after = replacement.ContentHash.ToByteArray();
        int replacements = 0;
        for (int offset = 0; offset <= result.Length - before.Length; offset++)
        {
            if (!result.AsSpan(offset, before.Length).SequenceEqual(before)) continue;
            after.CopyTo(result, offset);
            replacements++;
            offset += before.Length - 1;
        }
        Assert.True(replacements > 0);
        return result;
    }

    private static byte[] ChangeTransport(byte[] bytes, string change)
    {
        if (change == "revision") return Replace(bytes, 1, Number(99));
        if (change == "gate")
        {
            byte[] gates = Fields(bytes)[4];
            BinaryPrimitives.WriteUInt64BigEndian(gates.AsSpan(88, 8), 2000);
            return Replace(bytes, 4, gates);
        }
        if (change == "boundary")
        {
            byte[] boundary = Fields(bytes)[2];
            BinaryPrimitives.WriteUInt32BigEndian(boundary.AsSpan(8, 4), 3600);
            return Replace(bytes, 2, boundary);
        }
        var stages = Fields(Fields(bytes)[5]);
        stages[1] = Replace(stages[1], 7, Number((ulong)RunoffTransportStatus.Canceled));
        return Replace(bytes, 5, Frame(stages));
    }
}
