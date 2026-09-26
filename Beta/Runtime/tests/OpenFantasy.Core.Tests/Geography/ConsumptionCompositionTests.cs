using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Geography;

public sealed partial class TerrainMaterialRegistryTests
{
    [Fact]
    public void Extracted_raw_consumption_retains_exact_source_coverage_through_representation_changes()
    {
        var before = ExtractForConsumption(4);
        var lot = Assert.Single(before.Inventory.Custody.Lots, value => value.CustodianId == Away);
        var plan = Consume(before, lot, 1);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);

        var consumed = before.ReconcileProduction(plan.CandidateInventory, plan.CandidateProduction);

        Assert.Equal(new SourceInterval(Source, 10, 11), plan.Claim!.Ingredient.Source);
        Assert.Equal(1UL, consumed.RetiredQuantity);
        Assert.Equal(3UL, consumed.OtherQuantity);
        Assert.Equal(160UL, consumed.CoarseQuantity + consumed.DetailedQuantity + consumed.OtherQuantity + consumed.RetiredQuantity);
        Assert.Equal(new SourceInterval(Source, 11, 14), consumed.Inventory.Custody.GetLot(lot.LotId).Source);
        Assert.Equal(0UL, before.RetiredQuantity);
        var demoted = _planner.Demote(consumed, AllocationA, [new(-2, 0, -1)], Id(8401), consumed.Revision).ApplyTo(consumed);
        var refined = Refine(demoted, AllocationA, new(-2, 0, -1), 8402);
        Assert.Equal(6UL, refined.DetailedQuantity);
        Assert.Equal(1UL, refined.RetiredQuantity);
        Assert.Equal(consumed.Production!.ContentHash, refined.Production!.ContentHash);
    }

    [Fact]
    public void Consuming_a_real_produced_unit_does_not_retire_its_raw_ancestry_twice()
    {
        var extracted = ExtractForConsumption(4);
        var raw = Assert.Single(extracted.Inventory.Custody.Lots, value => value.CustodianId == Away);
        var crafted = CraftMaterial(extracted, raw.LotId, IngredientLotKind.Source, 8400);
        var output = Assert.Single(crafted.Production!.Lots);
        var request = new ConsumeItemRequest(Away, crafted.Inventory.Custody.GetStorage(Away).Revision,
            crafted.Production.Revision, Actor, output.Definition,
            new(IngredientLotKind.Transformed, output.LotId, output.Revision, 2));
        var plan = new ItemConsumptionPlanner().Plan(crafted.Inventory, crafted.Production,
            new(Id(107), Id(108)), Actor, Id(8403), request);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);

        var consumed = crafted.ReconcileProduction(plan.CandidateInventory, plan.CandidateProduction);

        Assert.Equal(4UL, consumed.RetiredQuantity);
        Assert.Equal(2UL, Assert.Single(consumed.Production!.Lots).Quantity);
        Assert.Equal(new ProductionOutputInterval(output.Output.BatchId, output.Output.OutputIndex, 0, 2),
            Assert.Single(consumed.Production.ConsumedItems).Ingredient.Output);
        Assert.Equal(160UL, consumed.CoarseQuantity + consumed.DetailedQuantity + consumed.OtherQuantity + consumed.RetiredQuantity);
        Assert.Single(consumed.Production.Batches);
        ConsumptionGeographyArchiveAssertions.Retain("crafted-consumption", TerrainMaterialRegistryCodecV3.Encode(consumed));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Standalone_consumption_cannot_retire_unextracted_representation_stock(bool detailed)
    {
        var registry = WithConsumption(Create().Registry);
        if (detailed) registry = Refine(registry, AllocationA, new(-2, 0, -1), 1);
        var lot = Assert.Single(registry.Inventory.Custody.Lots, value => value.CustodianId == (detailed ? Fine : Coarse));
        var before = registry.ContentHash;
        var plan = Consume(registry, lot, 1);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        plan.CandidateProduction.ValidateForAuthority(plan.CandidateInventory);

        Assert.Throws<ArgumentException>(() => registry.ReconcileProduction(plan.CandidateInventory, plan.CandidateProduction));

        Assert.Equal(before, registry.ContentHash);
        Assert.Empty(registry.Production!.ConsumedItems);
    }

    [Fact]
    public void Another_allocations_representation_custodian_is_not_an_extraction_escape()
    {
        var fixture = Create();
        EntityId otherFine = Id(8500);
        var inventory = new CountedAuthorityState(new(fixture.Inventory.WorldId, fixture.Inventory.BranchId, 0,
            fixture.Inventory.Custody.Storages.Append(new(otherFine, 200, 0)), fixture.Inventory.Custody.Lots),
            fixture.Inventory.AdmittedUnitCapacity, fixture.Inventory.Actors, fixture.Inventory.Controls,
            fixture.Inventory.Policies.Append(new(otherFine, Actor, CustodyDepositRule.OwnerOnly)), fixture.Inventory.Sources);
        var second = fixture.Allocations[1].Deposit;
        var registry = WithConsumption(TerrainMaterialRegistry.Create(fixture.Terrain, inventory,
            [fixture.Allocations[0], new(AllocationB, Deposit(fixture.Terrain, second.MacroColumn, second.Source, fine: otherFine))]));
        registry = Refine(registry, AllocationA, new(-2, 0, -1), 1);
        var fine = Assert.Single(registry.Inventory.Custody.Lots, value => value.CustodianId == Fine);
        registry = registry.ReconcileInventory(Transfer(registry.Inventory, fine, otherFine, 4));
        var transferred = Assert.Single(registry.Inventory.Custody.Lots, value => value.CustodianId == otherFine);
        Assert.Equal(new SourceInterval(Source, 10, 14), transferred.Source);
        var plan = Consume(registry, transferred, 4);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        plan.CandidateProduction.ValidateForAuthority(plan.CandidateInventory);

        Assert.Throws<ArgumentException>(() => registry.ReconcileProduction(plan.CandidateInventory, plan.CandidateProduction));
    }

    [Fact]
    public void Fully_retired_lot_revision_must_precede_the_complete_custody_root()
    {
        var registry = ExtractForConsumption(4);
        var lot = Assert.Single(registry.Inventory.Custody.Lots, value => value.CustodianId == Away);
        var plan = Consume(registry, lot, 4);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        var claim = plan.Claim!;
        var forged = ReplaceConsumption(plan.CandidateProduction,
            new(claim.ConsumptionId, claim.CommandId, claim.ActorId, claim.RecipientActorId, claim.CustodianId,
                new RetiredIngredient(claim.Ingredient.LotId, plan.CandidateInventory.Custody.Revision,
                    claim.Ingredient.Definition, claim.Ingredient.Source!)));
        forged.ValidateForAuthority(plan.CandidateInventory);

        Assert.Throws<ArgumentException>(() => registry.ReconcileProduction(plan.CandidateInventory, forged));
    }

    [Theory]
    [InlineData("erase")]
    [InlineData("rewrite")]
    [InlineData("downgrade")]
    public void Registry_reconciliation_preserves_consumption_history_even_when_the_standalone_candidate_is_valid(string change)
    {
        var registry = ExtractForConsumption(4);
        var raw = Assert.Single(registry.Inventory.Custody.Lots, value => value.CustodianId == Away);
        var consumed = Consume(registry, raw, 4);
        registry = registry.ReconcileProduction(consumed.CandidateInventory, consumed.CandidateProduction);
        var proof = registry.Production!;
        var claim = Assert.Single(proof.ConsumedItems);
        var lots = change == "rewrite" ? registry.Inventory.Custody.Lots.AsEnumerable() : registry.Inventory.Custody.Lots.Append(raw);
        var advanced = CopyInventory(registry.Inventory, new(proof.WorldId, proof.BranchId,
            registry.Revision + 1, registry.Inventory.Custody.Storages, lots));
        ConsumedItemClaim[] claims = change == "rewrite"
            ? [new(claim.ConsumptionId, claim.CommandId, claim.ActorId, claim.RecipientActorId, claim.CustodianId,
                new RetiredIngredient(Id(8501), claim.Ingredient.Revision, claim.Ingredient.Definition, claim.Ingredient.Source!))]
            : [];
        var candidate = new ProductionSnapshot(proof.WorldId, proof.BranchId, proof.Revision + 1,
            proof.Recipes, proof.Batches, proof.Lots, change == "downgrade" ? null : claims);
        candidate.ValidateForAuthority(advanced);

        Assert.Throws<ArgumentException>(() => registry.ReconcileProduction(advanced, candidate));
        Assert.Equal(proof.ContentHash, registry.Production!.ContentHash);
    }

    [Fact]
    public void Empty_explicit_consumption_owner_cannot_silently_downgrade()
    {
        var registry = WithConsumption(Create().Registry);
        var proof = registry.Production!;
        var advanced = CopyInventory(registry.Inventory, new(proof.WorldId, proof.BranchId, 1,
            registry.Inventory.Custody.Storages, registry.Inventory.Custody.Lots));
        var legacy = new ProductionSnapshot(proof.WorldId, proof.BranchId, 1, proof.Recipes, [], []);
        legacy.ValidateForAuthority(advanced);

        Assert.Throws<ArgumentException>(() => registry.ReconcileProduction(advanced, legacy));
    }

    private TerrainMaterialRegistry ExtractForConsumption(ulong quantity)
    {
        var registry = Refine(WithConsumption(Create().Registry), AllocationA, new(-2, 0, -1), 1);
        var fine = Assert.Single(registry.Inventory.Custody.Lots, value => value.CustodianId == Fine);
        return registry.ReconcileInventory(Transfer(registry.Inventory, fine, Away, quantity));
    }

    private static TerrainMaterialRegistry WithConsumption(TerrainMaterialRegistry registry)
    {
        var legacy = MaterialProduction(registry, registry.Allocations[0].Deposit.Material);
        return registry.AttachProduction(new(legacy.WorldId, legacy.BranchId, legacy.Revision,
            legacy.Recipes, legacy.Batches, legacy.Lots, []));
    }

    private static ItemConsumptionPlan Consume(TerrainMaterialRegistry registry, CountedSourceLot lot, ulong quantity)
        => new ItemConsumptionPlanner().Plan(registry.Inventory, registry.Production!, new(Id(107), Id(108)), Actor, Id(8404),
            new(lot.CustodianId, registry.Inventory.Custody.GetStorage(lot.CustodianId).Revision,
                registry.Production!.Revision, Actor, lot.Definition,
                new(IngredientLotKind.Source, lot.LotId, lot.Revision, quantity)));

    private static ProductionSnapshot ReplaceConsumption(ProductionSnapshot basis, ConsumedItemClaim claim)
        => new(basis.WorldId, basis.BranchId, basis.Revision, basis.Recipes, basis.Batches, basis.Lots, [claim]);
}
