using OpenFantasy.Core.Content;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Actors.BodyDecodingTestFrames;

namespace OpenFantasy.Core.Tests.Geography;

public sealed partial class TerrainMaterialRegistryTests
{
    [Fact]
    public void Consumption_archives_retain_extracted_raw_disposition_in_registry_and_each_allocation()
    {
        var raw = Create().Registry;
        var legacy = WithProduction(raw);
        var proof = legacy.Production!;
        var registry = raw.AttachProduction(new(proof.WorldId, proof.BranchId, proof.Revision,
            proof.Recipes, proof.Batches, proof.Lots, []));
        registry = Refine(registry, AllocationA, new(-2, 0, -1), 1);
        var fine = Assert.Single(registry.Inventory.Custody.Lots, lot => lot.CustodianId == Fine);
        registry = registry.ReconcileInventory(Transfer(registry.Inventory, fine, Away, 4));
        var carried = Assert.Single(registry.Inventory.Custody.Lots, lot => lot.CustodianId == Away);
        var plan = new ItemConsumptionPlanner().Plan(registry.Inventory, registry.Production!, new(Id(107), Id(108)), Actor, Id(8100),
            new(Away, registry.Inventory.Custody.GetStorage(Away).Revision, registry.Production!.Revision,
                Actor, carried.Definition, new(IngredientLotKind.Source, carried.LotId, carried.Revision, 3)));
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        registry = registry.ReconcileProduction(plan.CandidateInventory, plan.CandidateProduction);
        Assert.Equal(3UL, registry.RetiredQuantity);
        Assert.Equal(157UL, registry.CoarseQuantity + registry.DetailedQuantity + registry.OtherQuantity);
        ConsumptionGeographyArchiveAssertions.Split(legacy, registry,
            TerrainMaterialRegistryCodecV2.Encode, TerrainMaterialRegistryCodecV3.Encode,
            (bytes, token) => TerrainMaterialRegistryCodecV2.Decode(bytes, token),
            (bytes, token) => TerrainMaterialRegistryCodecV3.Decode(bytes, token));
        ConsumptionGeographyArchiveAssertions.Split(legacy.GetState(AllocationA), registry.GetState(AllocationA),
            TerrainMaterialCodecV2.Encode, TerrainMaterialCodecV3.Encode,
            (bytes, token) => TerrainMaterialCodecV2.Decode(bytes, token),
            (bytes, token) => TerrainMaterialCodecV3.Decode(bytes, token));
        byte[] archive = TerrainMaterialRegistryCodecV3.Encode(registry);
        var restored = TerrainMaterialRegistryCodecV3.Decode(archive, TestContext.Current.CancellationToken);
        Assert.Equal(ContentHash.Compute(archive), restored.ContentHash);
        Assert.Equal(3UL, restored.RetiredQuantity);
        Assert.Equal(plan.Claim!.ConsumptionId, Assert.Single(restored.Production!.ConsumedItems).ConsumptionId);
        Assert.Equal(ContentHash.Compute(TerrainMaterialCodecV3.Encode(restored.GetState(AllocationA))), restored.GetState(AllocationA).ContentHash);
        Assert.Same(restored.Production, restored.GetState(AllocationA).Production);
        Assert.Same(restored.Production, restored.GetState(AllocationB).Production);
        Assert.Throws<ArgumentException>(() => TerrainMaterialRegistryCodecV1.Encode(registry));
        Assert.Throws<ArgumentException>(() => TerrainMaterialCodecV1.Encode(registry.GetState(AllocationA)));
    }

    [Fact]
    public void Empty_enabled_terrain_owner_cannot_be_relabelled_or_have_its_production_proof_removed()
    {
        var raw = Create().Registry;
        var legacy = WithProduction(raw);
        var proof = legacy.Production!;
        var registry = raw.AttachProduction(new(proof.WorldId, proof.BranchId, proof.Revision,
            proof.Recipes, proof.Batches, proof.Lots, []));
        byte[] archive = TerrainMaterialRegistryCodecV3.Encode(registry);
        Assert.Empty(registry.Production!.ConsumedItems);
        Assert.NotEqual(legacy.ContentHash, registry.ContentHash);
        foreach (byte[] replacement in new[] { Array.Empty<byte>(), ProductionCodecV1.Encode(proof) })
        {
            Assert.ThrowsAny<FormatException>(() => TerrainMaterialRegistryCodecV3.Decode(Replace(archive, 4, replacement),
                TestContext.Current.CancellationToken));
            Assert.ThrowsAny<FormatException>(() => TerrainMaterialCodecV3.Decode(
                Replace(TerrainMaterialCodecV3.Encode(registry.GetState(AllocationA)), 4, replacement), TestContext.Current.CancellationToken));
        }
        Assert.Throws<ArgumentException>(() => TerrainMaterialRegistryCodecV2.Encode(registry));
        Assert.Throws<ArgumentException>(() => TerrainMaterialCodecV2.Encode(registry.GetState(AllocationA)));
        Assert.Throws<ArgumentException>(() => TerrainMaterialRegistryCodecV3.Encode(raw));
        Assert.Throws<ArgumentException>(() => TerrainMaterialCodecV3.Encode(raw.GetState(AllocationA)));
        Assert.Equal(archive, TerrainMaterialRegistryCodecV3.Encode(TerrainMaterialRegistryCodecV3.Decode(archive,
            TestContext.Current.CancellationToken)));
    }
}
