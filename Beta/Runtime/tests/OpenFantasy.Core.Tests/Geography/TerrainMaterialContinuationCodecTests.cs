using System.Buffers.Binary;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Tests.Construction;
using OpenFantasy.Core.Tests.Worlds;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Geography.TerrainMaterialContinuationFixture;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class TerrainMaterialContinuationCodecTests
{
    [Fact]
    public void Both_nested_archives_require_actual_representation_proofs_and_the_exact_closing_owner()
    {
        var journey = TerrainMaterialContinuationFixture.Earned();
        journey.Cut(first: true);
        journey.Demote(AllocationA, CellA, Id(800));
        journey.Refine(AllocationA, CellA, Id(801));
        var owner = journey.Registry;
        var dependencies = journey.Dependencies;
        var other = TerrainMaterialContinuationFixture.Earned();
        other.Cut(first: true);
        other.Demote(AllocationA, CellA, Id(802));
        other.Refine(AllocationA, CellA, Id(803));
        var wrongEvidence = new TerrainProductionDependencies(owner, terrainEvidence: other.Production.MaterialHistory!.TerrainEvidence);
        var missing = new TerrainProductionDependencies(owner);
        byte[] registry = TerrainMaterialRegistryCodecV4.Encode(owner);
        byte[] state = TerrainMaterialCodecV4.Encode(owner.GetState(AllocationA));
        Assert.Equal(registry, TerrainMaterialRegistryCodecV4.Encode(TerrainMaterialRegistryCodecV4.Decode(registry, dependencies, TestContext.Current.CancellationToken)));
        Assert.Equal(state, TerrainMaterialCodecV4.Encode(TerrainMaterialCodecV4.Decode(state, dependencies, TestContext.Current.CancellationToken)));
        foreach (var invalid in new[] { missing, wrongEvidence })
        {
            Assert.ThrowsAny<FormatException>(() => TerrainMaterialRegistryCodecV4.Decode(registry, invalid, TestContext.Current.CancellationToken));
            Assert.ThrowsAny<FormatException>(() => TerrainMaterialCodecV4.Decode(state, invalid, TestContext.Current.CancellationToken));
        }
        Assert.DoesNotContain(typeof(TerrainRepresentationEvidence).GetConstructors(), value => value.IsPublic);
        var proofs = journey.Production.MaterialHistory!.TerrainEvidence;
        Assert.Equal(2, proofs.Count);
        Assert.Equal(proofs[0].AfterInventoryHash, proofs[1].BeforeInventoryHash);
        Assert.Equal(proofs[0].AfterRepresentationHash, proofs[1].BeforeRepresentationHash);
        Assert.Equal(proofs[0].CatalogHash, proofs[1].CatalogHash);
        Assert.False(proofs[0].Refining);
        Assert.True(proofs[1].Refining);
        journey.Cut();
        Assert.ThrowsAny<FormatException>(() => TerrainMaterialRegistryCodecV4.Decode(registry, journey.Dependencies, TestContext.Current.CancellationToken));
        Assert.ThrowsAny<FormatException>(() => TerrainMaterialCodecV4.Decode(state, journey.Dependencies, TestContext.Current.CancellationToken));
        byte[] cut = TerrainMaterialRegistryCodecV4.Encode(journey.Registry);
        Assert.Equal(cut, TerrainMaterialRegistryCodecV4.Encode(TerrainMaterialRegistryCodecV4.Decode(cut, journey.Dependencies, TestContext.Current.CancellationToken)));
        Assert.Equal(registry, TerrainMaterialRegistryCodecV4.Encode(owner));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => TerrainMaterialRegistryCodecV4.Decode(registry, dependencies, cancellation.Token));
        Assert.Throws<OperationCanceledException>(() => owner.PlanContinue(cancellation.Token));
    }

    [Theory]
    [InlineData("command")]
    [InlineData("proof")]
    [InlineData("order")]
    [InlineData("missing")]
    public void Canonical_material_frames_cannot_rewrite_or_drop_real_representation_operations(string change)
    {
        var journey = TerrainMaterialContinuationFixture.Earned();
        journey.Cut(first: true);
        journey.Demote(AllocationA, CellA, Id(810));
        journey.Refine(AllocationA, CellA, Id(811));
        byte[] bytes = ProductionCodecV6.Encode(journey.Production);
        byte[][] production = Fields(bytes);
        byte[][] operations = Fields(production[12]);
        if (change == "order") (operations[1], operations[2]) = (operations[2], operations[1]);
        else if (change == "missing") operations = [operations[0], operations[1]];
        else
        {
            byte[][] operation = Fields(operations[1]);
            if (change == "command") operation[4] = Id(812).ToByteArray();
            else operation[5] = new byte[32];
            operations[1] = Frame(operation);
        }
        production[12] = Frame(operations);
        Assert.ThrowsAny<FormatException>(() => ProductionCodecV6.Decode(Frame(production), journey.Production.Continuation!, [], [], journey.Production.MaterialHistory!.TerrainEvidence));
        Assert.Equal(bytes, ProductionCodecV6.Encode(journey.Production));
        byte[][] outer = Fields(TerrainMaterialRegistryCodecV4.Encode(journey.Registry));
        outer[4] = Frame(production);
        Assert.ThrowsAny<FormatException>(() => TerrainMaterialRegistryCodecV4.Decode(Frame(outer), journey.Dependencies, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Exact_catalog_and_even_empty_active_cells_are_owned_facts_after_a_cut()
    {
        var journey = new TerrainMaterialContinuationFixture();
        journey.Refine(AllocationA, CellA, Id(820));
        journey.Refine(AllocationA, CellA2, Id(821));
        journey.Transfer(journey.RawAt(Fine, 10).LotId, Away, 10, Id(822));
        journey.Transfer(journey.RawAt(Fine, 20).LotId, Away, 10, Id(823));
        journey.Demote(AllocationA, CellA2, Id(824));
        Assert.Equal([CellA], journey.Registry.GetState(AllocationA).ActiveCells);
        Assert.Equal(0UL, journey.Registry.DetailedQuantity);
        byte[] legacy = TerrainMaterialRegistryCodecV3.Encode(journey.Registry);
        byte[] changedLegacy = ChangeEmptyActiveCell(legacy);
        var validDifferentBinding = TerrainMaterialRegistryCodecV3.Decode(changedLegacy, TestContext.Current.CancellationToken);
        Assert.Equal([CellA2], validDifferentBinding.GetState(AllocationA).ActiveCells);
        Assert.Equal(journey.Inventory.ContentHash, validDifferentBinding.Inventory.ContentHash);
        journey.Cut(first: true);
        byte[] continued = TerrainMaterialRegistryCodecV4.Encode(journey.Registry);
        Assert.ThrowsAny<FormatException>(() => TerrainMaterialRegistryCodecV4.Decode(ChangeEmptyActiveCell(continued), journey.Dependencies, TestContext.Current.CancellationToken));
        byte[][] renamedLegacy = Fields(legacy);
        Id(7).ToByteArray().CopyTo(renamedLegacy[3], 4);
        Assert.Contains(TerrainMaterialRegistryCodecV3.Decode(Frame(renamedLegacy), TestContext.Current.CancellationToken).Allocations,
            value => value.AllocationId == Id(7));
        byte[][] renamed = Fields(continued);
        Id(7).ToByteArray().CopyTo(renamed[3], 4);
        Assert.ThrowsAny<FormatException>(() => TerrainMaterialRegistryCodecV4.Decode(Frame(renamed), journey.Dependencies, TestContext.Current.CancellationToken));
        var generic = new ProductionSnapshot(journey.Inventory.WorldId, journey.Inventory.BranchId, 0,
            journey.Production.Recipes, [], [], []).ContinueMaterial(journey.Inventory);
        Assert.Throws<ArgumentException>(() => journey.Registry.ReconcileProduction(journey.Inventory, generic));
        Assert.Equal(continued, TerrainMaterialRegistryCodecV4.Encode(journey.Registry));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Complete_legacy_archives_remain_exact_and_cannot_reinterpret_a_terrain_segment(bool consumption)
    {
        var journey = new TerrainMaterialContinuationFixture(consumption: consumption);
        byte[] legacy = consumption ? TerrainMaterialRegistryCodecV3.Encode(journey.Registry) : TerrainMaterialRegistryCodecV2.Encode(journey.Registry);
        var old = consumption ? TerrainMaterialRegistryCodecV3.Decode(legacy, TestContext.Current.CancellationToken)
            : TerrainMaterialRegistryCodecV2.Decode(legacy, TestContext.Current.CancellationToken);
        Assert.Equal(legacy, consumption ? TerrainMaterialRegistryCodecV3.Encode(old) : TerrainMaterialRegistryCodecV2.Encode(old));
        journey.Cut(first: true);
        Assert.True(journey.Production.Continuation!.SupportsTerrainRepresentation);
        Assert.Throws<ArgumentException>(() => TerrainMaterialRegistryCodecV1.Encode(journey.Registry));
        Assert.Throws<ArgumentException>(() => TerrainMaterialRegistryCodecV2.Encode(journey.Registry));
        Assert.Throws<ArgumentException>(() => TerrainMaterialRegistryCodecV3.Encode(journey.Registry));
        Assert.Throws<ArgumentException>(() => TerrainMaterialCodecV1.Encode(journey.Registry.GetState(AllocationA)));
        Assert.Throws<ArgumentException>(() => TerrainMaterialCodecV2.Encode(journey.Registry.GetState(AllocationA)));
        Assert.Throws<ArgumentException>(() => TerrainMaterialCodecV3.Encode(journey.Registry.GetState(AllocationA)));
        Assert.Throws<ArgumentException>(() => ProductionCodecV3.Encode(journey.Production));
        Assert.Throws<ArgumentException>(() => ProductionCodecV4.Encode(journey.Production));
        Assert.Throws<ArgumentException>(() => ProductionCodecV5.Encode(journey.Production));
        byte[] bytes = TerrainMaterialRegistryCodecV4.Encode(journey.Registry);
        Assert.ThrowsAny<FormatException>(() => TerrainMaterialRegistryCodecV3.Decode(bytes, TestContext.Current.CancellationToken));
        Assert.ThrowsAny<FormatException>(() => TerrainMaterialRegistryCodecV4.Decode(legacy, journey.Dependencies, TestContext.Current.CancellationToken));
        Assert.ThrowsAny<FormatException>(() => TerrainMaterialRegistryCodecV4.Decode(bytes[..^1], journey.Dependencies, TestContext.Current.CancellationToken));
        Assert.ThrowsAny<FormatException>(() => TerrainMaterialRegistryCodecV4.Decode([.. bytes, 0], journey.Dependencies, TestContext.Current.CancellationToken));
        Assert.Equal(bytes, TerrainMaterialRegistryCodecV4.Encode(TerrainMaterialRegistryCodecV4.Decode(bytes, journey.Dependencies, TestContext.Current.CancellationToken)));
    }

    [Fact]
    public void Selected_terrain_material_preserves_actual_construction_completion_and_both_opaque_evidence_kinds()
    {
        var journey = new ConstructionContinuationFixture(readyToComplete: true, withTerrainMaterial: true);
        var legacy = journey.LegacyWorld;
        var scope = new TerrainScope(legacy.WorldId, legacy.BranchId, Id(900));
        var basin = new WatershedPlanner().Prepare(new(scope, Id(901), new(0, 0, 1, 1), new(0, 0), 0, Id(902)), TestContext.Current.CancellationToken);
        var network = new WatershedNetworkPlanner().Prepare(Id(903), [basin], [WatershedOutletClosure.ToTerminal(basin.BasinId, 1)], TestContext.Current.CancellationToken);
        var terrain = new WatershedTerrainMapper().Prepare(network, new(1, 1, 0), new(new(0, 0, 0), 1, 1, 1), TestContext.Current.CancellationToken);
        var binding = legacy.Production!.Recipes.First(value => value.Bindings.Any(item => item.Item == AdmissionFixture.Material)).BindingFor(AdmissionFixture.Material);
        var allocation = new TerrainMaterialAllocation(Id(904), new(terrain, new(basin.BasinId, 0), 1, binding, 10000,
            new(ConstructionContinuationFixture.TerrainSource, 0, 10000), ConstructionContinuationFixture.TerrainCoarse, ConstructionContinuationFixture.TerrainFine));
        var registry = TerrainMaterialRegistry.Create(terrain, legacy.Inventory, [allocation]).AttachProduction(legacy.Production);
        registry = registry.PlanContinueTerrain(legacy.Gathering, MaterialConstructionState.Capture(legacy), TestContext.Current.CancellationToken).ApplyTo(registry);
        Assert.True(registry.Production!.Continuation!.SupportsConstruction);
        Assert.True(registry.Production.Continuation.SupportsTerrainRepresentation);
        Assert.Throws<ArgumentException>(() => registry.Production.ContinueConstruction(registry.Inventory, MaterialConstructionState.Capture(legacy), legacy.Gathering));
        Assert.Equal(legacy.Inventory.ContentHash, registry.Inventory.ContentHash);
        Assert.Equal(legacy.Production.ContentHash, registry.Production.Continuation.Basis.ProductionHash);
        journey.Advance();
        var command = journey.Command();
        var completion = new MaterialConstructionPlanner().Plan(journey.SourceWorld, registry.Inventory, registry.Production,
            journey.State, journey.Space, command, true, false, journey.Occupied);
        Assert.Equal(WorldActionRejection.None, completion.Rejection);
        var proof = Assert.IsType<ConstructionCompletionEvidence>(completion.Evidence);
        Assert.Equal(20, completion.CandidatePhysical!.Members.Count);
        registry = registry.ReconcileProduction(completion.CandidateInventory, completion.CandidateProduction);
        var planner = new TerrainMaterialRegistryPlanner();
        registry = planner.Refine(registry, allocation.AllocationId, terrain, [new(0, 0, 0)], Id(905), registry.Revision, journey.Occupied).ApplyTo(registry);
        var representation = Assert.Single(registry.Production!.MaterialHistory!.TerrainEvidence);
        var dependencies = new TerrainProductionDependencies(registry, constructionEvidence: [proof], terrainEvidence: [representation]);
        byte[] bytes = TerrainMaterialRegistryCodecV4.Encode(registry);
        Assert.Equal(bytes, TerrainMaterialRegistryCodecV4.Encode(TerrainMaterialRegistryCodecV4.Decode(bytes, dependencies, TestContext.Current.CancellationToken)));
        Assert.ThrowsAny<FormatException>(() => TerrainMaterialRegistryCodecV4.Decode(bytes,
            new TerrainProductionDependencies(registry, terrainEvidence: [representation]), TestContext.Current.CancellationToken));
        Assert.ThrowsAny<FormatException>(() => TerrainMaterialRegistryCodecV4.Decode(bytes,
            new TerrainProductionDependencies(registry, constructionEvidence: [proof]), TestContext.Current.CancellationToken));
        for (int cut = 0; cut < 2; cut++)
        {
            registry = registry.PlanContinue(TestContext.Current.CancellationToken).ApplyTo(registry);
            Assert.Equal(2, registry.Production!.Continuation!.ConstructionBatches.Count);
            Assert.True(registry.Production.Continuation.SupportsTerrainRepresentation);
            Assert.Equal(10000UL, registry.DetailedQuantity);
            var current = new TerrainProductionDependencies(registry);
            byte[] saved = TerrainMaterialRegistryCodecV4.Encode(registry);
            Assert.Equal(saved, TerrainMaterialRegistryCodecV4.Encode(TerrainMaterialRegistryCodecV4.Decode(saved, current, TestContext.Current.CancellationToken)));
        }
    }

    private static byte[] ChangeEmptyActiveCell(byte[] bytes)
    {
        byte[][] fields = Fields(bytes);
        byte[] rows = fields[3];
        int bindingLength = checked((int)BinaryPrimitives.ReadUInt32BigEndian(rows.AsSpan(20, 4)));
        int countOffset = 24 + bindingLength;
        Assert.Equal(1U, BinaryPrimitives.ReadUInt32BigEndian(rows.AsSpan(countOffset, 4)));
        BinaryPrimitives.WriteInt64BigEndian(rows.AsSpan(countOffset + 4, 8), CellA2.X);
        return Frame(fields);
    }

    private static byte[][] Fields(byte[] bytes)
    {
        Assert.True(bytes.AsSpan(0, 4).SequenceEqual("OFC\0"u8));
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
        return fields;
    }

    private static byte[] Frame(byte[][] fields)
    {
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
