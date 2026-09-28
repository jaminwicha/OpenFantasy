using System.Buffers.Binary;
using System.Text;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Tests.Construction;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Tests.Production;
using OpenFantasy.Core.Tests.Worlds;
using static OpenFantasy.Core.Tests.Geography.LiquidMaterialContinuationFixture;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class LiquidMaterialContinuationCodecTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void All_nested_continued_frames_require_the_exact_context_and_roundtrip_after_actual_operations(bool material, bool consumption)
    {
        var journey = new LiquidMaterialContinuationFixture(consumption: consumption);
        EntityId made = journey.Craft(IngredientLotKind.Source, RawLot, 4, Id(500));
        journey.Cut(material);
        var oldContext = journey.Dependencies;
        journey.Craft(IngredientLotKind.Transformed, made, 2, Id(501));
        if (consumption) journey.Consume(IngredientLotKind.Source, RawLot, Id(502));
        journey.Cut();
        journey.Craft(IngredientLotKind.Source, RawLot, 2, Id(503));
        var dependencies = journey.Dependencies;
        var prepared = Assert.Single(journey.Capacity.Pending);
        var roundtrips = Frames(journey.Reservoirs, dependencies);
        foreach (var (bytes, reopen, wrongContext) in roundtrips)
        {
            Assert.Equal(bytes, reopen(bytes));
            Assert.ThrowsAny<FormatException>(() => wrongContext(bytes, oldContext));
            Assert.ThrowsAny<ArgumentException>(() => wrongContext(bytes, null!));
            Assert.ThrowsAny<FormatException>(() => reopen(bytes[..^1]));
            Assert.ThrowsAny<FormatException>(() => reopen([.. bytes, 0]));
            byte[] changed = RewriteProduction(bytes, fields => fields[5] = new byte[32]);
            Assert.NotEqual(bytes, changed);
            Assert.ThrowsAny<FormatException>(() => reopen(changed));
        }
        Assert.Throws<ArgumentException>(() => WatershedRunoffCodecV1.Encode(prepared));
        Assert.Throws<ArgumentException>(() => WatershedRunoffCodecV2.Encode(prepared));
        Assert.Throws<ArgumentException>(() => WatershedRunoffCodecV2.EncodeRequest(prepared.Request));
        Assert.Throws<ArgumentException>(() => WatershedRunoffCapacityCodecV1.Encode(journey.Capacity));
        Assert.Throws<ArgumentException>(() => WatershedRunoffCapacityCodecV2.Encode(journey.Capacity));
        Assert.Throws<ArgumentException>(() => LiquidReservoirCodecV1.Encode(journey.Reservoirs));
        Assert.Throws<ArgumentException>(() => LiquidReservoirCodecV2.Encode(journey.Reservoirs));
        Assert.ThrowsAny<FormatException>(() => WatershedRunoffCodecV1.Decode(WatershedRunoffCodecV3.Encode(prepared), TestContext.Current.CancellationToken));
        Assert.ThrowsAny<FormatException>(() => WatershedRunoffCodecV2.Decode(WatershedRunoffCodecV3.Encode(prepared), TestContext.Current.CancellationToken));
        Assert.ThrowsAny<FormatException>(() => WatershedRunoffCapacityCodecV1.Decode(WatershedRunoffCapacityCodecV3.Encode(journey.Capacity), TestContext.Current.CancellationToken));
        Assert.ThrowsAny<FormatException>(() => WatershedRunoffCapacityCodecV2.Decode(WatershedRunoffCapacityCodecV3.Encode(journey.Capacity), TestContext.Current.CancellationToken));
        Assert.ThrowsAny<FormatException>(() => LiquidReservoirCodecV1.Decode(LiquidReservoirCodecV3.Encode(journey.Reservoirs), TestContext.Current.CancellationToken));
        Assert.ThrowsAny<FormatException>(() => LiquidReservoirCodecV2.Decode(LiquidReservoirCodecV3.Encode(journey.Reservoirs), TestContext.Current.CancellationToken));
        Assert.ThrowsAny<FormatException>(() => LiquidReservoirCodecV3.Decode(LiquidReservoirCodecV3.Encode(journey.Reservoirs),
            new GeographyProductionDependencies(journey.Capacity), TestContext.Current.CancellationToken));
        var current = journey.Capacity.CreateRequest(prepared.Request.ReservationId, prepared.Request.Start,
            prepared.Request.End, prepared.Request.Inputs);
        prepared.EnsureCurrent(current);
        var restored = LiquidReservoirCodecV3.Decode(LiquidReservoirCodecV3.Encode(journey.Reservoirs), dependencies, TestContext.Current.CancellationToken);
        Assert.Equal(journey.Capacity.ContentHash, restored.Capacity.ContentHash);
        Assert.Equal(journey.Production.ContentHash, restored.Capacity.Production!.ContentHash);
        Assert.Equal(AuthorityEncodingV1.Encode(journey.Inventory), AuthorityEncodingV1.Encode(restored.Capacity.Inventory));
        Assert.Equal(journey.Production.TotalBatchCount, restored.Capacity.Production.TotalBatchCount);
        Assert.Equal(journey.Production.TotalConsumedItemCount, restored.Capacity.Production.TotalConsumedItemCount);
        Assert.Equal(journey.Production.TotalMaterialOperationCount, restored.Capacity.Production.TotalMaterialOperationCount);
    }

    [Fact]
    public void Nested_material_frames_require_actual_opaque_gather_evidence_even_when_the_liquid_receiver_is_empty()
    {
        // Gathering belongs to its real source world; this unrelated empty receiving owner
        // must still authenticate all nested material operations. No water issuance is invented.
        var journey = MaterialCustodyContinuationFixture.Create();
        var reservoirs = WrapInitialGatherOwner(journey);
        reservoirs = reservoirs.PlanApply(reservoirs.Capacity.PlanContinueMaterial(journey.Gathering, cancellationToken: TestContext.Current.CancellationToken)).ApplyTo(reservoirs);
        Assert.Equal(journey.Production.ContentHash, reservoirs.Capacity.Production!.ContentHash);
        journey.Gather(88000);
        reservoirs = reservoirs.PlanApply(reservoirs.Capacity.PlanReconcile(journey.Inventory, journey.Production, TestContext.Current.CancellationToken)).ApplyTo(reservoirs);
        var actual = new GeographyProductionDependencies(reservoirs, journey.Production.MaterialHistory!.GatherEvidence.ToArray());
        var missing = new GeographyProductionDependencies(reservoirs, []);
        var other = MaterialCustodyContinuationFixture.Create();
        other.Gather(88001);
        var wrong = new GeographyProductionDependencies(reservoirs, other.Production.MaterialHistory!.GatherEvidence.ToArray());
        Assert.Single(actual.GatherEvidence);
        Assert.DoesNotContain(reservoirs.Capacity.Inventory.Custody.Lots, value => value.CustodianId == AdmissionFixture.Id(61));
        foreach (var (bytes, reopen, decodeWith) in Frames(reservoirs, actual))
        {
            Assert.Equal(bytes, reopen(bytes));
            Assert.ThrowsAny<FormatException>(() => decodeWith(bytes, missing));
            Assert.ThrowsAny<FormatException>(() => decodeWith(bytes, wrong));
        }
        byte[] original = LiquidReservoirCodecV3.Encode(reservoirs);
        journey.Cut();
        var cut = reservoirs.PlanApply(reservoirs.Capacity.PlanContinue(TestContext.Current.CancellationToken)).ApplyTo(reservoirs);
        Assert.Equal(journey.Production.ContentHash, cut.Capacity.Production!.ContentHash);
        var after = new GeographyProductionDependencies(cut);
        Assert.Equal(LiquidReservoirCodecV3.Encode(cut), LiquidReservoirCodecV3.Encode(
            LiquidReservoirCodecV3.Decode(LiquidReservoirCodecV3.Encode(cut), after, TestContext.Current.CancellationToken)));
        Assert.Equal(original, LiquidReservoirCodecV3.Encode(reservoirs));
    }

    [Fact]
    public void Actual_construction_completion_evidence_is_required_through_every_nested_geography_frame()
    {
        var journey = new ConstructionContinuationFixture(readyToComplete: true, withLiquidReceiver: true);
        var legacy = journey.LegacyWorld;
        var scope = new TerrainScope(legacy.WorldId, legacy.BranchId, Id(750));
        var basin = new WatershedPlanner().Prepare(new(scope, Id(751), new(0, 0, 1, 1), new(0, 0), 0, Id(752)), TestContext.Current.CancellationToken);
        var network = new WatershedNetworkPlanner().Prepare(Id(753), [basin], [WatershedOutletClosure.ToTerminal(basin.BasinId, 1)], TestContext.Current.CancellationToken);
        var geometry = new ReservoirGeometry(scope, Id(754), ConstructionContinuationFixture.LiquidReceiver, 1, [new(0, 0, 0)]);
        var binding = legacy.Production!.Recipes.First(value => value.Bindings.Any(item => item.Item == AdmissionFixture.Material))
            .BindingFor(AdmissionFixture.Material);
        var request = new WatershedRunoffRequest(network, new(1, 1, 0), legacy.Inventory, legacy.Production,
            ConstructionContinuationFixture.LiquidRunoffReservation, ConstructionContinuationFixture.Builder, new(0, 0), new(0, 1), binding,
            [WatershedRunoffInput.Dry(basin.BasinId)], [new(basin.BasinId, geometry.CustodianId, geometry.Capacity)]);
        var reservoirs = LiquidReservoirState.Create(WatershedRunoffCapacityState.Create(new WatershedRunoffPlanner().Prepare(request, TestContext.Current.CancellationToken)), [geometry]);
        reservoirs = reservoirs.PlanApply(reservoirs.Capacity.PlanContinueConstruction(MaterialConstructionState.Capture(legacy), legacy.Gathering, TestContext.Current.CancellationToken))
            .ApplyTo(reservoirs);
        Assert.Equal(journey.Inventory.ContentHash, reservoirs.Capacity.Inventory.ContentHash);
        Assert.Equal(journey.Production.ContentHash, reservoirs.Capacity.Production!.ContentHash);
        Assert.True(reservoirs.Capacity.Production.Continuation!.SupportsConstruction);
        Assert.Equal(199UL, journey.Construction.FindProject(ConstructionContinuationFixture.ProjectB)!.Revision);
        Assert.Equal(6UL, Assert.Single(journey.Inventory.Custody.Lots,
            value => value.ReservedBy == ConstructionContinuationFixture.ReserveB).Quantity);
        Assert.DoesNotContain(journey.Inventory.Custody.Lots,
            value => value.ReservedBy == ConstructionContinuationFixture.LiquidRunoffReservation);

        var completion = journey.Work();
        var proof = Assert.IsType<ConstructionCompletionEvidence>(completion.Evidence);
        Assert.Equal(20, journey.Physical.Members.Count);
        reservoirs = reservoirs.PlanApply(reservoirs.Capacity.PlanReconcile(journey.Inventory, journey.Production, TestContext.Current.CancellationToken)).ApplyTo(reservoirs);
        Assert.Equal(0UL, Assert.Single(reservoirs.Capacity.Receivers).ExistingUnits);
        Assert.Equal(0UL, Assert.Single(reservoirs.Capacity.Receivers).IncomingUnits);
        var genuine = new GeographyProductionDependencies(reservoirs, [], [proof]);
        var missing = new GeographyProductionDependencies(reservoirs, [], []);
        var other = new ConstructionContinuationFixture(readyToComplete: true, withLiquidReceiver: true);
        other.Advance();
        var differentCompletion = other.Work();
        var wrongProof = Assert.IsType<ConstructionCompletionEvidence>(differentCompletion.Evidence);
        Assert.NotEqual(proof.ContentHash, wrongProof.ContentHash);
        var wrong = new GeographyProductionDependencies(reservoirs, [], [wrongProof]);
        foreach (var (bytes, reopen, decodeWith) in Frames(reservoirs, genuine))
        {
            Assert.Equal(bytes, reopen(bytes));
            Assert.ThrowsAny<FormatException>(() => decodeWith(bytes, missing));
            Assert.ThrowsAny<FormatException>(() => decodeWith(bytes, wrong));
        }
        byte[] original = LiquidReservoirCodecV3.Encode(reservoirs);
        var cut = reservoirs.PlanApply(reservoirs.Capacity.PlanContinue(TestContext.Current.CancellationToken)).ApplyTo(reservoirs);
        Assert.Equal(2, cut.Capacity.Production!.Continuation!.ConstructionBatches.Count);
        Assert.Empty(cut.Capacity.Production.MaterialHistory!.ConstructionEvidence);
        var after = new GeographyProductionDependencies(cut);
        Assert.Equal(LiquidReservoirCodecV3.Encode(cut), LiquidReservoirCodecV3.Encode(
            LiquidReservoirCodecV3.Decode(LiquidReservoirCodecV3.Encode(cut), after, TestContext.Current.CancellationToken)));
        Assert.ThrowsAny<FormatException>(() => LiquidReservoirCodecV3.Decode(original, after, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Complete_history_codecs_keep_their_own_exact_roundtrip_and_refuse_explicit_continued_encoding(bool consumption)
    {
        var journey = new LiquidMaterialContinuationFixture(consumption: consumption);
        journey.Craft(IngredientLotKind.Source, RawLot, 4, Id(600));
        if (consumption) journey.Consume(IngredientLotKind.Source, RawLot, Id(601));
        var prepared = Assert.Single(journey.Capacity.Pending);
        if (consumption)
        {
            byte[] runoff = WatershedRunoffCodecV2.Encode(prepared);
            byte[] capacity = WatershedRunoffCapacityCodecV2.Encode(journey.Capacity);
            byte[] reservoirs = LiquidReservoirCodecV2.Encode(journey.Reservoirs);
            Assert.Equal(runoff, WatershedRunoffCodecV2.Encode(WatershedRunoffCodecV2.Decode(runoff, TestContext.Current.CancellationToken)));
            Assert.Equal(capacity, WatershedRunoffCapacityCodecV2.Encode(WatershedRunoffCapacityCodecV2.Decode(capacity, TestContext.Current.CancellationToken)));
            Assert.Equal(reservoirs, LiquidReservoirCodecV2.Encode(LiquidReservoirCodecV2.Decode(reservoirs, TestContext.Current.CancellationToken)));
        }
        else
        {
            byte[] runoff = WatershedRunoffCodecV1.Encode(prepared);
            byte[] capacity = WatershedRunoffCapacityCodecV1.Encode(journey.Capacity);
            byte[] reservoirs = LiquidReservoirCodecV1.Encode(journey.Reservoirs);
            Assert.Equal(runoff, WatershedRunoffCodecV1.Encode(WatershedRunoffCodecV1.Decode(runoff, TestContext.Current.CancellationToken)));
            Assert.Equal(capacity, WatershedRunoffCapacityCodecV1.Encode(WatershedRunoffCapacityCodecV1.Decode(capacity, TestContext.Current.CancellationToken)));
            Assert.Equal(reservoirs, LiquidReservoirCodecV1.Encode(LiquidReservoirCodecV1.Decode(reservoirs, TestContext.Current.CancellationToken)));
        }
        Assert.Throws<ArgumentException>(() => WatershedRunoffCodecV3.Encode(prepared));
        Assert.Throws<ArgumentException>(() => WatershedRunoffCapacityCodecV3.Encode(journey.Capacity));
        Assert.Throws<ArgumentException>(() => LiquidReservoirCodecV3.Encode(journey.Reservoirs));
    }

    [Fact]
    public void Generic_material_cuts_cannot_launder_an_invalid_retired_revision_into_geography()
    {
        var journey = new LiquidMaterialContinuationFixture();
        var request = Assert.Single(journey.Capacity.Pending).Request;
        var raw = journey.Inventory.Custody.GetLot(RawLot);
        var consume = new ItemConsumptionPlanner().Plan(journey.Inventory, journey.Production, new(Client, Session), Owner, Id(710),
            new(Store, 0, 0, Owner, raw.Definition, new(IngredientLotKind.Source, raw.LotId, raw.Revision, raw.Quantity)));
        Assert.Equal(WorldActionRejection.None, consume.Rejection);
        var actual = consume.Claim!;
        var forgedRevision = new ConsumedItemClaim(actual.ConsumptionId, actual.CommandId, actual.ActorId,
            actual.RecipientActorId, actual.CustodianId,
            new(actual.Ingredient.LotId, consume.CandidateInventory.Custody.Revision,
                actual.Ingredient.Definition, actual.Ingredient.Source!));
        var proposed = new ProductionSnapshot(journey.Production.WorldId, journey.Production.BranchId,
            consume.CandidateProduction.Revision, journey.Production.Recipes, [], [], [forgedRevision]);
        proposed.ValidateForAuthority(consume.CandidateInventory); // Generic structure alone does not prove the geographic revision rule.
        Assert.Throws<ArgumentException>(() => Prepare(request, consume.CandidateInventory, proposed));
        for (int cut = 0; cut < 2; cut++)
        {
            proposed = proposed.Continue(consume.CandidateInventory);
            proposed.ValidateForAuthority(consume.CandidateInventory);
            Assert.Throws<ArgumentException>(() => Prepare(request, consume.CandidateInventory, proposed));
        }
        Assert.Equal(0UL, journey.Capacity.Revision);
        Assert.Equal(1000UL, Assert.Single(journey.Capacity.Receivers).ProjectedUnits);
    }

    [Fact]
    public void Retired_geographic_identity_collisions_cannot_be_hidden_by_generic_cuts_or_new_reservoir_geometry()
    {
        var journey = new LiquidMaterialContinuationFixture();
        var request = Assert.Single(journey.Capacity.Pending).Request;
        EntityId conflictingCommand = request.Network.ProvinceId;
        var consume = journey.PlanConsume(IngredientLotKind.Source, RawLot, conflictingCommand);
        Assert.Equal(WorldActionRejection.None, consume.Rejection);
        Assert.Throws<ArgumentException>(() => Prepare(request, consume.CandidateInventory, consume.CandidateProduction));
        var generic = consume.CandidateProduction;
        for (int cut = 0; cut < 2; cut++)
        {
            generic = generic.Continue(consume.CandidateInventory);
            Assert.Throws<ArgumentException>(() => Prepare(request, consume.CandidateInventory, generic));
        }

        // This ordinary consumed command is geography-compatible, but cannot later become a
        // new reservoir identity once its command history has left the active segment.
        EntityId retiredCommand = Id(720);
        journey.Consume(IngredientLotKind.Source, RawLot, retiredCommand);
        var geometry = journey.Reservoirs.Geometries.Select(value => value.ReservoirId == Reservoir
            ? new ReservoirGeometry(value.Scope, retiredCommand, value.CustodianId, value.SpillZ, value.Columns) : value).ToArray();
        Assert.Throws<ArgumentException>(() => LiquidReservoirState.Create(journey.Capacity, geometry));
        for (int cut = 0; cut < 2; cut++)
        {
            journey.Cut();
            Assert.Throws<ArgumentException>(() => LiquidReservoirState.Create(journey.Capacity, geometry));
            byte[] bytes = LiquidReservoirCodecV3.Encode(journey.Reservoirs);
            Assert.Equal(bytes, LiquidReservoirCodecV3.Encode(LiquidReservoirCodecV3.Decode(bytes, journey.Dependencies, TestContext.Current.CancellationToken)));
        }
    }

    [Fact]
    public void Nested_reservoir_restore_requires_exact_qualified_geometry_even_for_valid_equal_capacity_support()
    {
        var journey = new LiquidMaterialContinuationFixture();
        byte[] complete = LiquidReservoirCodecV2.Encode(journey.Reservoirs);
        var movedLegacy = LiquidReservoirCodecV2.Decode(ShiftFirstGeometry(complete, "geography.liquid-reservoirs.v2"), TestContext.Current.CancellationToken);
        Assert.Equal(journey.Reservoirs.Geometries[0].Capacity, movedLegacy.Geometries[0].Capacity);
        Assert.Equal(journey.Reservoirs.Geometries[0].Columns[0].X + 10, movedLegacy.Geometries[0].Columns[0].X);
        Assert.Equal(journey.Reservoirs.Capacity.ContentHash, movedLegacy.Capacity.ContentHash);
        journey.Cut();
        byte[] bytes = LiquidReservoirCodecV3.Encode(journey.Reservoirs);
        byte[] shifted = ShiftFirstGeometry(bytes, "geography.liquid-reservoirs.v3");
        Assert.ThrowsAny<FormatException>(() => LiquidReservoirCodecV3.Decode(shifted, journey.Dependencies, TestContext.Current.CancellationToken));
        Assert.ThrowsAny<FormatException>(() => LiquidReservoirCodecV3.Decode(bytes,
            new GeographyProductionDependencies(journey.Capacity), TestContext.Current.CancellationToken));
        Assert.Equal(bytes, LiquidReservoirCodecV3.Encode(LiquidReservoirCodecV3.Decode(bytes, journey.Dependencies, TestContext.Current.CancellationToken)));
    }

    private static byte[] ShiftFirstGeometry(byte[] bytes, string rootTag) => RewriteNamedFrame(bytes, rootTag, root =>
    {
        byte[] collection = root[2];
        int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(collection.AsSpan(4, 4)));
        byte[] geometry = collection.AsSpan(8, length).ToArray();
        byte[] shifted = RewriteNamedFrame(geometry, "geography.reservoir-geometry.v1", fields =>
        {
            long x = BinaryPrimitives.ReadInt64BigEndian(fields[7]);
            BinaryPrimitives.WriteInt64BigEndian(fields[7], x + 10);
        });
        Assert.Equal(length, shifted.Length);
        shifted.CopyTo(collection, 8);
    });

    private static PreparedWatershedRunoff Prepare(WatershedRunoffRequest basis, CountedAuthorityState inventory, ProductionSnapshot production) =>
        new WatershedRunoffPlanner().Prepare(new(basis.Network, basis.Mapping, inventory, production,
            basis.ReservationId, basis.OwnerActorId, basis.Start, basis.End, basis.Material, basis.Inputs, basis.Terminals), TestContext.Current.CancellationToken);

    private static LiquidReservoirState WrapInitialGatherOwner(MaterialCustodyContinuationFixture journey)
    {
        var scope = new TerrainScope(journey.Inventory.WorldId, journey.Inventory.BranchId, Id(700));
        var basin = new WatershedPlanner().Prepare(new(scope, Id(701), new(0, 0, 1, 1), new(0, 0), 0, Id(702)), TestContext.Current.CancellationToken);
        var network = new WatershedNetworkPlanner().Prepare(Id(703), [basin], [WatershedOutletClosure.ToTerminal(basin.BasinId, 1)], TestContext.Current.CancellationToken);
        var geometry = new ReservoirGeometry(scope, Id(704), AdmissionFixture.Id(61), 1, [new(0, 0, 0)]);
        var binding = Assert.Single(journey.Production.Recipes).BindingFor(AdmissionFixture.Material);
        Assert.Equal(0UL, journey.Production.Revision);
        var initial = new ProductionSnapshot(journey.Inventory.WorldId, journey.Inventory.BranchId, 0,
            journey.Production.Recipes, [], [], []);
        var request = new WatershedRunoffRequest(network, new(1, 1, 0), journey.Inventory, initial,
            MaterialCustodyContinuationFixture.DeliveryReservation, AdmissionFixture.Id(11), new(0, 0), new(0, 1), binding,
            [WatershedRunoffInput.Dry(basin.BasinId)], [new(basin.BasinId, geometry.CustodianId, geometry.Capacity)]);
        return LiquidReservoirState.Create(WatershedRunoffCapacityState.Create(new WatershedRunoffPlanner().Prepare(request, TestContext.Current.CancellationToken)), [geometry]);
    }

    private static IEnumerable<(byte[] Bytes, Func<byte[], byte[]> Reopen,
        Action<byte[], GeographyProductionDependencies> DecodeWith)> Frames(LiquidReservoirState state, GeographyProductionDependencies dependencies)
    {
        var prepared = Assert.Single(state.Capacity.Pending);
        yield return (WatershedRunoffCodecV3.EncodeRequest(prepared.Request),
            bytes => WatershedRunoffCodecV3.EncodeRequest(WatershedRunoffCodecV3.DecodeRequest(bytes, dependencies, TestContext.Current.CancellationToken)),
            (bytes, other) => WatershedRunoffCodecV3.DecodeRequest(bytes, other, TestContext.Current.CancellationToken));
        yield return (WatershedRunoffCodecV3.Encode(prepared),
            bytes => WatershedRunoffCodecV3.Encode(WatershedRunoffCodecV3.Decode(bytes, dependencies, TestContext.Current.CancellationToken)),
            (bytes, other) => WatershedRunoffCodecV3.Decode(bytes, other, TestContext.Current.CancellationToken));
        yield return (WatershedRunoffCapacityCodecV3.Encode(state.Capacity),
            bytes => WatershedRunoffCapacityCodecV3.Encode(WatershedRunoffCapacityCodecV3.Decode(bytes, dependencies, TestContext.Current.CancellationToken)),
            (bytes, other) => WatershedRunoffCapacityCodecV3.Decode(bytes, other, TestContext.Current.CancellationToken));
        yield return (LiquidReservoirCodecV3.Encode(state),
            bytes => LiquidReservoirCodecV3.Encode(LiquidReservoirCodecV3.Decode(bytes, dependencies, TestContext.Current.CancellationToken)),
            (bytes, other) => LiquidReservoirCodecV3.Decode(bytes, other, TestContext.Current.CancellationToken));
    }

    // An independent canonical frame editor changes a semantic nested field without relying
    // on a checksum failure or exposing runtime constructors for opaque contexts/evidence.
    private static byte[] RewriteProduction(byte[] bytes, Action<byte[][]> rewrite) =>
        RewriteNamedFrame(bytes, "production.snapshot", rewrite);

    private static byte[] RewriteNamedFrame(byte[] bytes, string tag, Action<byte[][]> rewrite)
    {
        if (bytes.Length < 12 || !bytes.AsSpan(0, 4).SequenceEqual("OFC\0"u8)) return bytes;
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)));
        var fields = new byte[count][];
        int offset = 12;
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4)));
            offset += 4;
            fields[index] = bytes.AsSpan(offset, length).ToArray();
            offset += length;
        }
        Assert.Equal(bytes.Length, offset);
        if (fields[0].SequenceEqual(Encoding.UTF8.GetBytes(tag))) rewrite(fields);
        else fields = fields.Select(field => RewriteNamedFrame(field, tag, rewrite)).ToArray();
        using var output = new MemoryStream();
        output.Write("OFC\0"u8);
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(number, 1);
        output.Write(number);
        BinaryPrimitives.WriteUInt32BigEndian(number, (uint)fields.Length);
        output.Write(number);
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(number, (uint)field.Length);
            output.Write(number);
            output.Write(field);
        }
        return output.ToArray();
    }
}
