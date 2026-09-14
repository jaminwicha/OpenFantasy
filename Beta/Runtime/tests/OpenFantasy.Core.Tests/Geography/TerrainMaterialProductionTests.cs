using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Geography;

public sealed partial class TerrainMaterialRegistryTests
{
    [Fact]
    public void ActualCraftRetirementSurvivesDemotionRefinementAndArchiveReload()
    {
        var registry = WithProduction(Create().Registry);
        var initial = registry;
        RetainProduction(initial, "attached");
        registry = Refine(registry, AllocationA, new(-2, 0, -1), 1);
        var fine = Assert.Single(registry.Inventory.Custody.Lots, l => l.CustodianId == Fine);
        registry = registry.ReconcileInventory(Transfer(registry.Inventory, fine, Away, 4));
        var carried = Assert.Single(registry.Inventory.Custody.Lots, l => l.CustodianId == Away);
        var result = new TerrainMaterialCraftPlanner().Plan(registry, new(Id(107), Id(108)), Actor, Id(8000),
            MaterialCraftRequest(registry, carried.LotId, IngredientLotKind.Source));
        Assert.Equal(WorldActionRejection.None, result.Rejection);
        registry = result.ApplyTo(registry);
        Assert.Equal(4UL, registry.RetiredQuantity);
        Assert.Equal(156UL, registry.CoarseQuantity + registry.DetailedQuantity + registry.OtherQuantity);
        Assert.Equal(4UL, Assert.Single(registry.Production!.Lots).Quantity);
        registry = _planner.Demote(registry, AllocationA, [new(-2, 0, -1)], Id(2), registry.Revision).ApplyTo(registry);
        registry = Refine(registry, AllocationA, new(-2, 0, -1), 3);
        Assert.Equal(6UL, registry.DetailedQuantity);
        byte[] bytes = TerrainMaterialRegistryCodecV2.Encode(registry);
        registry = TerrainMaterialRegistryCodecV2.Decode(bytes, TestContext.Current.CancellationToken);
        Assert.Equal(bytes, TerrainMaterialRegistryCodecV2.Encode(registry));
        Assert.Equal(4UL, registry.RetiredQuantity);
        Assert.Equal(0UL, initial.RetiredQuantity);
        Assert.Throws<ArgumentException>(() => TerrainMaterialRegistryCodecV1.Encode(registry));
        Assert.Throws<ArgumentException>(() => TerrainMaterialCodecV1.Encode(registry.GetState(AllocationA)));
        RetainProduction(registry, "crafted-reloaded");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DirectCraftCannotConsumeUnextractedTerrainThroughReconciliation(bool detailed)
    {
        var registry = WithProduction(Create().Registry);
        if (detailed) registry = Refine(registry, AllocationA, new(-2, 0, -1), 1);
        EntityId custodian = detailed ? Fine : Coarse;
        var lot = Assert.Single(registry.Inventory.Custody.Lots, l => l.CustodianId == custodian);
        var request = MaterialCraftRequest(registry, lot.LotId, IngredientLotKind.Source, custodian);
        var direct = new InstantCraftPlanner().Plan(registry.Inventory, registry.Production!,
            new(Id(107), Id(108)), Actor, Id(8000), request);
        Assert.Equal(WorldActionRejection.None, direct.Rejection);
        Assert.Throws<ArgumentException>(() => registry.ReconcileProduction(direct.CandidateInventory, direct.CandidateProduction));
        var wrapped = new TerrainMaterialCraftPlanner().Plan(registry, new(Id(107), Id(108)), Actor, Id(8000), request);
        Assert.Equal(WorldActionRejection.Unavailable, wrapped.Rejection);
        Assert.Same(registry, wrapped.ApplyTo(registry));
    }

    [Fact]
    public void ChainedCraftAndPartialPrefixesRetireRawUnitsOnlyOnce()
    {
        var registry = ExtractMaterial(10);
        var raw = Assert.Single(registry.Inventory.Custody.Lots, l => l.CustodianId == Away);
        registry = CraftMaterial(registry, raw.LotId, IngredientLotKind.Source, 8000);
        registry = CraftMaterial(registry, raw.LotId, IngredientLotKind.Source, 8001);
        Assert.Equal(2UL, registry.Inventory.Custody.GetLot(raw.LotId).Quantity);
        var derived = registry.Production!.Lots[0];
        registry = CraftMaterial(registry, derived.LotId, IngredientLotKind.Transformed, 8002);
        Assert.Equal(8UL, registry.RetiredQuantity);
        Assert.Equal(3, registry.Production!.Batches.Count);
        Assert.Equal(8L, registry.Production.Lots.Sum(l => (long)l.Quantity));
        registry = _planner.Demote(registry, AllocationA, [new(-2, 0, -1)], Id(3), registry.Revision).ApplyTo(registry);
        Assert.Throws<InvalidOperationException>(() => Refine(registry, AllocationA, new(-2, 0, -1), 4));
        byte[] single = TerrainMaterialCodecV2.Encode(registry.GetState(AllocationA));
        Assert.Equal(single, TerrainMaterialCodecV2.Encode(TerrainMaterialCodecV2.Decode(single, TestContext.Current.CancellationToken)));
        RetainProduction(registry, "chained-prefixes");
    }

    [Fact]
    public void ConsumedClaimsCannotBeRemovedRewrittenOrRevivedByAnAdvancingSnapshot()
    {
        var extracted = ExtractMaterial(4);
        var raw = Assert.Single(extracted.Inventory.Custody.Lots, l => l.CustodianId == Away);
        var registry = CraftMaterial(extracted, raw.LotId, IngredientLotKind.Source, 8000);
        ProductionSnapshot proof = registry.Production!;
        CountedAuthorityState advance = CopyInventory(registry.Inventory, new(registry.Inventory.WorldId, registry.Inventory.BranchId,
            registry.Revision + 1, registry.Inventory.Custody.Storages, registry.Inventory.Custody.Lots));
        ProductionBatch batch = Assert.Single(proof.Batches);
        RetiredIngredient ingredient = Assert.Single(batch.Ingredients);
        var rewritten = new ProductionBatch(batch.BatchId, batch.CommandId, batch.ActorId, batch.CustodianId,
            batch.RecipeId, batch.RecipeVersion, [new RetiredIngredient(Id(65000), ingredient.Revision, ingredient.Definition, ingredient.Source!)]);
        var changed = new ProductionSnapshot(proof.WorldId, proof.BranchId, proof.Revision + 1, proof.Recipes, [rewritten], proof.Lots);
        changed.ValidateForAuthority(advance);
        Assert.Throws<ArgumentException>(() => registry.ReconcileProduction(advance, changed));
        var restored = CopyInventory(advance, new(proof.WorldId, proof.BranchId, advance.Custody.Revision,
            advance.Custody.Storages, advance.Custody.Lots.Append(raw)));
        var removed = new ProductionSnapshot(proof.WorldId, proof.BranchId, proof.Revision + 1, proof.Recipes, [], []);
        removed.ValidateForAuthority(restored);
        Assert.Throws<ArgumentException>(() => registry.ReconcileProduction(restored, removed));
        Assert.Throws<ArgumentException>(() => registry.ReconcileInventory(restored));
        Assert.Throws<InvalidOperationException>(() => registry.AttachProduction(removed));
        var unchangedRevision = new ProductionSnapshot(proof.WorldId, proof.BranchId, proof.Revision, proof.Recipes, [rewritten], proof.Lots);
        Assert.Throws<ArgumentException>(() => registry.ReconcileProduction(advance, unchangedRevision));
        Assert.Same(registry, registry.ReconcileProduction(registry.Inventory, proof));
    }

    [Fact]
    public void CraftedPlansPinProductionAndPreserveAuthenticationRevisionAndHistoricalRejections()
    {
        var registry = ExtractMaterial(8);
        var raw = Assert.Single(registry.Inventory.Custody.Lots, l => l.CustodianId == Away);
        var request = MaterialCraftRequest(registry, raw.LotId, IngredientLotKind.Source);
        var planner = new TerrainMaterialCraftPlanner();
        var accepted = planner.Plan(registry, new(Id(107), Id(108)), Actor, Id(8000), request);
        var changed = accepted.ApplyTo(registry);
        Assert.Throws<InvalidOperationException>(() => accepted.ApplyTo(changed));
        Assert.Equal(WorldActionRejection.NotAuthorized, planner.Plan(registry, new(Id(999), Id(998)), Actor, Id(8000), request).Rejection);
        Assert.Equal(WorldActionRejection.RevisionConflict, planner.Plan(changed, new(Id(107), Id(108)), Actor, Id(8001), request).Rejection);
        foreach (EntityId occupied in new[] { Id(8000), EntityId.DeriveSpawned(Id(8000), 0), EntityId.DeriveSpawned(Id(8000), 1) })
        {
            var blocked = planner.Plan(registry, new(Id(107), Id(108)), Actor, Id(8000), request, id => id == occupied);
            Assert.Equal(WorldActionRejection.Unavailable, blocked.Rejection);
            Assert.Same(registry, blocked.ApplyTo(registry));
        }
        foreach (EntityId occupied in new[] { Id(8000), Id(7001), changed.Production!.Batches[0].BatchId })
            Assert.Throws<InvalidOperationException>(() => _planner.Demote(changed, AllocationA, [new(-2, 0, -1)], occupied, changed.Revision));
    }

    [Fact]
    public void SharedPhysicalPinsAndAllAllocationRolesAreValidatedAgainstProduction()
    {
        var f = Create();
        var renamed = TerrainMaterialRegistry.Create(f.Terrain, f.Inventory,
            [f.Allocations[0], new(Id(7001), f.Allocations[1].Deposit)]);
        Assert.Throws<ArgumentException>(() => WithProduction(renamed));
        ItemPhysicalBinding original = f.Allocations[0].Deposit.Material;
        var different = new MaterialDefinition(original.Physical.Package, original.Physical.DefinitionId, original.Physical.Key,
            new(original.Physical.MassPerUnit.Value + 1), original.Physical.VolumePerUnit, original.Physical.Hardness);
        Assert.Throws<ArgumentException>(() => f.Registry.AttachProduction(MaterialProduction(f.Registry, new(original.Item, different))));
        var alias = new MaterialDefinition(original.Physical.Package, f.Terrain.Network.ProvinceId, "alias",
            original.Physical.MassPerUnit, original.Physical.VolumePerUnit, original.Physical.Hardness);
        Assert.Throws<ArgumentException>(() => f.Registry.AttachProduction(MaterialProduction(f.Registry, new(original.Item, alias))));
    }

    [Fact]
    public void InventoryOnlyChangesRespectCombinedRawAndDerivedStorageCapacity()
    {
        var f = Create();
        var inventory = CopyInventory(f.Inventory, new(f.Inventory.WorldId, f.Inventory.BranchId, 0,
            f.Inventory.Custody.Storages.Select(s => s.CustodianId == Away ? s with { UnitCapacity = 10 } : s), f.Inventory.Custody.Lots));
        var registry = WithProduction(TerrainMaterialRegistry.Create(f.Terrain, inventory, f.Allocations));
        registry = Refine(registry, AllocationA, new(-2, 0, -1), 1);
        var fine = Assert.Single(registry.Inventory.Custody.Lots, l => l.CustodianId == Fine);
        registry = registry.ReconcileInventory(Transfer(registry.Inventory, fine, Away, 4));
        registry = CraftMaterial(registry, Assert.Single(registry.Inventory.Custody.Lots, l => l.CustodianId == Away).LotId,
            IngredientLotKind.Source, 8000);
        registry = Refine(registry, AllocationB, new(0, 0, -1), 2);
        var neighbour = Assert.Single(registry.Inventory.Custody.Lots, l => l.CustodianId == Fine && l.Source.Start == 90);
        var rawOnlyValid = Transfer(registry.Inventory, neighbour, Away, 8);
        Assert.Throws<ArgumentException>(() => registry.ReconcileInventory(rawOnlyValid));
        Assert.Equal(4UL, registry.Production!.GetStoredQuantity(Away));
    }

    [Fact]
    public void ProductionArchiveRejectsTruncationMissingProofDowngradesAndCancellation()
    {
        var registry = ExtractMaterial(4);
        registry = CraftMaterial(registry, Assert.Single(registry.Inventory.Custody.Lots, l => l.CustodianId == Away).LotId,
            IngredientLotKind.Source, 8000);
        byte[] bytes = TerrainMaterialRegistryCodecV2.Encode(registry);
        foreach (byte[] bad in new[] { bytes[..^1], bytes.Concat(new byte[] { 0 }).ToArray() })
            Assert.Throws<FormatException>(() => TerrainMaterialRegistryCodecV2.Decode(bad, TestContext.Current.CancellationToken));
        Assert.Throws<FormatException>(() => TerrainMaterialRegistryCodecV1.Decode(bytes, TestContext.Current.CancellationToken));
        Assert.Throws<FormatException>(() => TerrainMaterialRegistryCodecV2.Decode(TerrainMaterialRegistryCodecV1.Encode(Create().Registry),
            TestContext.Current.CancellationToken));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => TerrainMaterialRegistryCodecV2.Decode(bytes, cancellation.Token));
        Assert.Throws<ArgumentException>(() => TerrainMaterialRegistryCodecV2.Encode(Create().Registry));
    }

    [Fact]
    public void RepresentationSplitsRespectCombinedLotBudgetAt4096LiveLots()
    {
        var f = Create();
        var item = f.Allocations[0].Deposit.Material.Item;
        var extras = Enumerable.Range(0, 4090).Select(i => new CountedSourceLot(Id((ulong)(100000 + i)), Away, item,
            new(Source, (ulong)(200 + i), (ulong)(201 + i)), 0));
        var inventory = new CountedAuthorityState(new(f.Inventory.WorldId, f.Inventory.BranchId, 0,
            f.Inventory.Custody.Storages.Select(s => s with { UnitCapacity = 5000 }), f.Inventory.Custody.Lots.Concat(extras)),
            5000, f.Inventory.Actors, f.Inventory.Controls, f.Inventory.Policies, [new(Source, item, 5000)]);
        var registry = Refine(WithProduction(TerrainMaterialRegistry.Create(f.Terrain, inventory, f.Allocations)),
            AllocationA, new(-2, 0, -1), 1);
        var fine = Assert.Single(registry.Inventory.Custody.Lots, l => l.CustodianId == Fine);
        registry = registry.ReconcileInventory(Transfer(registry.Inventory, fine, Away, 4));
        var raw = Assert.Single(registry.Inventory.Custody.Lots, l => l.CustodianId == Away && l.Source.Start == 10);
        registry = CraftMaterial(registry, raw.LotId, IngredientLotKind.Source, 8000);
        var proof = registry.Production!;
        var derived = Assert.Single(proof.Lots);
        var split = Enumerable.Range(0, 4).Select(i => new TransformedLot(Id((ulong)(95000 + i)), Away, item,
            new(derived.Output.BatchId, derived.Output.OutputIndex, (ulong)i, (ulong)i + 1), 0));
        var production = new ProductionSnapshot(proof.WorldId, proof.BranchId, proof.Revision + 1, proof.Recipes, proof.Batches, split);
        var next = CopyInventory(registry.Inventory, new(proof.WorldId, proof.BranchId, registry.Revision + 1,
            registry.Inventory.Custody.Storages, registry.Inventory.Custody.Lots));
        registry = registry.ReconcileProduction(next, production);
        Assert.Equal(4096, registry.Inventory.Custody.Lots.Count + registry.Production!.Lots.Count);
        var hash = registry.ContentHash;
        Assert.Throws<ArgumentException>(() => Refine(registry, AllocationB, new(0, 0, -1), 3));
        Assert.Equal(hash, registry.ContentHash);
        RetainProduction(registry, "combined-lot-cap");
    }

    [Fact]
    public void RetiredLotIdentitiesAndCompleteProductionProofGuardRepresentationPlans()
    {
        var registry = ExtractMaterial(4);
        var raw = Assert.Single(registry.Inventory.Custody.Lots, l => l.CustodianId == Away);
        registry = CraftMaterial(registry, raw.LotId, IngredientLotKind.Source, 8000);
        Assert.False(registry.Inventory.ContainsIdentity(raw.LotId));
        Assert.Throws<InvalidOperationException>(() => _planner.Demote(registry, AllocationA, [new(-2, 0, -1)], raw.LotId, registry.Revision));
        var plan = _planner.Demote(registry, AllocationA, [new(-2, 0, -1)], Id(2), registry.Revision);
        var changed = CraftMaterial(registry, Assert.Single(registry.Production!.Lots).LotId, IngredientLotKind.Transformed, 8001);
        Assert.Throws<InvalidOperationException>(() => plan.ApplyTo(changed));
        var callback = EntityId.DeriveSpawned(Id(2), 0);
        Assert.Throws<InvalidOperationException>(() => _planner.Demote(registry, AllocationA, [new(-2, 0, -1)], Id(2),
            registry.Revision, id => id == callback));
    }

    [Fact]
    public void RawRegistryArchiveRemainsByteExactAcrossProductionExtension()
    {
        using var fixture = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "terrain-material-registry-v1-compatibility.json")));
        byte[] bytes = Convert.FromHexString(fixture.RootElement.GetProperty("hex").GetString()!);
        Assert.Equal(fixture.RootElement.GetProperty("sha256").GetString(), ContentHash.Compute(bytes).Value);
        var decoded = TerrainMaterialRegistryCodecV1.Decode(bytes, TestContext.Current.CancellationToken);
        Assert.Null(decoded.Production);
        Assert.Equal(bytes, TerrainMaterialRegistryCodecV1.Encode(decoded));
    }

    private TerrainMaterialRegistry ExtractMaterial(ulong quantity)
    {
        var registry = Refine(WithProduction(Create().Registry), AllocationA, new(-2, 0, -1), 1);
        var fine = Assert.Single(registry.Inventory.Custody.Lots, l => l.CustodianId == Fine);
        return registry.ReconcileInventory(Transfer(registry.Inventory, fine, Away, quantity));
    }

    private static TerrainMaterialRegistry CraftMaterial(TerrainMaterialRegistry registry, EntityId lotId, IngredientLotKind kind, ulong command)
    {
        var plan = new TerrainMaterialCraftPlanner().Plan(registry, new(Id(107), Id(108)), Actor, Id(command),
            MaterialCraftRequest(registry, lotId, kind));
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        return plan.ApplyTo(registry);
    }

    private static TerrainMaterialRegistry WithProduction(TerrainMaterialRegistry registry)
        => registry.AttachProduction(MaterialProduction(registry, registry.Allocations[0].Deposit.Material));

    private static ProductionSnapshot MaterialProduction(TerrainMaterialRegistry registry, ItemPhysicalBinding binding)
    {
        var physical = new MaterialRecipeDefinition(binding.Physical.Package, Id(7000), "shape_rock", new(1), null,
            new MaterialCatalog([binding.Physical]), [new(RecipeMaterialRole.Input, binding.Physical.Pin(), 4),
                new(RecipeMaterialRole.Output, binding.Physical.Pin(), 4)]);
        var recipe = new CraftRecipeDefinition(Id(7001), physical, [binding],
            [new(binding.Item, 0, binding.Physical.MassPerUnit)]);
        return new(registry.Inventory.WorldId, registry.Inventory.BranchId, 0, [recipe], [], []);
    }

    private static CraftRequest MaterialCraftRequest(TerrainMaterialRegistry registry, EntityId lotId,
        IngredientLotKind kind, EntityId? custodian = null)
    {
        ProductionSnapshot production = registry.Production!;
        CraftRecipeDefinition recipe = production.Recipes[0];
        EntityId storage = custodian ?? Away;
        ulong revision = kind == IngredientLotKind.Source ? registry.Inventory.Custody.GetLot(lotId).Revision
            : production.Lots.Single(l => l.LotId == lotId).Revision;
        return new(recipe.DefinitionId, recipe.Version, production.Revision, storage,
            registry.Inventory.Custody.GetStorage(storage).Revision, [new(kind, lotId, revision, 4)]);
    }

    private static void RetainProduction(TerrainMaterialRegistry registry, string name)
    {
        string? path = Environment.GetEnvironmentVariable("OPENFANTASY_MATERIAL_PRODUCTION_EVIDENCE");
        if (string.IsNullOrWhiteSpace(path)) return;
        Directory.CreateDirectory(path);
        File.WriteAllBytes(Path.Combine(path, name + ".bin"), TerrainMaterialRegistryCodecV2.Encode(registry));
    }
}
