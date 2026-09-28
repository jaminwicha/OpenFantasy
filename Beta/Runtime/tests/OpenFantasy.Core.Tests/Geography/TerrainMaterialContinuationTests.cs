using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Geography.TerrainMaterialContinuationFixture;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class TerrainMaterialContinuationTests
{
    [Fact]
    public void Actual_extraction_transformation_and_representation_changes_preserve_exact_source_rights_across_three_cuts()
    {
        var journey = TerrainMaterialContinuationFixture.Earned();
        EntityId ancestor = Assert.Single(journey.Production.Lots).LotId;
        Assert.Equal((150UL, 3UL, 2UL, 5UL), Totals(journey.Registry));
        Assert.Equal(new[] { (10UL, 15UL) }, Retired(journey));
        var oldPlan = journey.PlanDemote(AllocationA, CellA, Id(110));
        var oldOwner = journey.Registry;
        journey.Cut(first: true);
        Assert.Throws<InvalidOperationException>(() => oldPlan.ApplyTo(journey.Registry));
        Assert.Throws<ArgumentException>(() => oldOwner.ReconcileProduction(journey.Inventory, journey.Production));
        AssertPartition(journey);

        var before = journey.Registry;
        journey.Demote(AllocationA, CellA, Id(111));
        AssertAdvance(before, journey.Registry, Coarse, Fine);
        journey.Refine(AllocationA, CellA, Id(112));
        Assert.Equal(new[] { (17UL, 20UL) }, Raw(journey, Fine));
        var aCell = journey.RawAt(Fine, 17);
        journey.Refine(AllocationB, CellB, Id(113));
        Assert.Equal(aCell, journey.Inventory.Custody.GetLot(aCell.LotId));
        EntityId movedB = journey.Transfer(journey.RawAt(Fine, 90).LotId, Away, 6, Id(114));
        journey.Craft(IngredientLotKind.Transformed, ancestor, 2, Id(115));
        journey.Consume(IngredientLotKind.Transformed, ancestor, Id(116));
        journey.Craft(IngredientLotKind.Source, movedB, 2, Id(117));
        journey.Consume(IngredientLotKind.Source, journey.RawAt(Away, 15).LotId, Id(118));
        Assert.Equal((140UL, 7UL, 5UL, 8UL), Totals(journey.Registry));
        Assert.Equal(new[] { (10UL, 16UL), (90UL, 92UL) }, Retired(journey));
        Assert.Equal(new[] { (17UL, 20UL), (96UL, 100UL) }, Raw(journey, Fine));
        Assert.Equal(new[] { (16UL, 17UL), (92UL, 96UL) }, Raw(journey, Away));
        AssertPartition(journey);
        journey.Cut();
        Assert.Equal(new ProductionOutputInterval(EntityId.DeriveSpawned(Id(102), 0), 0, 3, 4),
            journey.Production.Lots.Single(value => value.LotId == ancestor).Output);
        Assert.Contains(journey.Production.Continuation!.Basis.BatchWitnesses, value => value.BatchId == EntityId.DeriveSpawned(Id(102), 0));

        journey.Refine(AllocationA, CellA2, Id(119));
        EntityId second = journey.Transfer(journey.RawAt(Fine, 20).LotId, Away, 2, Id(120));
        journey.Craft(IngredientLotKind.Source, second, 2, Id(121));
        journey.Demote(AllocationB, CellB, Id(122));
        journey.Cut();
        Assert.Equal((134UL, 11UL, 5UL, 10UL), Totals(journey.Registry));
        Assert.Equal(new[] { (10UL, 16UL), (20UL, 22UL), (90UL, 92UL) }, Retired(journey));
        Assert.Equal(new[] { (17UL, 20UL), (22UL, 30UL) }, Raw(journey, Fine));
        Assert.Equal(new[] { (16UL, 17UL), (92UL, 96UL) }, Raw(journey, Away));
        Assert.Equal(16UL, journey.Registry.Revision);
        Assert.Equal(14UL, journey.Production.Revision);
        Assert.Equal(14UL, journey.Production.TotalMaterialOperationCount);
        Assert.Equal(4UL, journey.Production.TotalBatchCount);
        Assert.Equal(3UL, journey.Production.TotalConsumedItemCount);
        Assert.Equal(7UL, journey.Production.GetStoredQuantity(Away));
        Assert.Equal(1UL, journey.Production.Lots.Single(value => value.LotId == ancestor).Quantity);
        Assert.Contains(journey.Production.Continuation!.Basis.BatchWitnesses, value => value.BatchId == EntityId.DeriveSpawned(Id(102), 0));
        AssertPartition(journey);
        Assert.Equal(6UL, journey.Inventory.Custody.GetStorage(Coarse).Revision);
        Assert.Equal(9UL, journey.Inventory.Custody.GetStorage(Fine).Revision);
        Assert.Equal(10UL, journey.Inventory.Custody.GetStorage(Away).Revision);
    }

    [Fact]
    public void The_unchanged_512_operation_bound_refuses_the_next_real_representation_change_until_a_cut()
    {
        var journey = new TerrainMaterialContinuationFixture();
        journey.Cut(first: true);
        for (ulong index = 0; index < 512; index++)
        {
            if (index % 2 == 0) journey.Refine(AllocationA, CellA, Id(1000 + index));
            else journey.Demote(AllocationA, CellA, Id(1000 + index));
        }
        Assert.Equal(512, journey.Production.MaterialHistory!.Operations.Count);
        Assert.Equal(512, journey.Production.MaterialHistory.TerrainEvidence.Count);
        Assert.Equal(512UL, journey.Registry.Revision);
        var before = journey.Registry.ContentHash;
        Assert.ThrowsAny<ArgumentException>(() => journey.PlanRefine(AllocationA, CellA, Id(1512)));
        Assert.Equal(before, journey.Registry.ContentHash);
        journey.Cut();
        journey.Refine(AllocationA, CellA, Id(1512));
        Assert.Equal(513UL, journey.Registry.Revision);
        Assert.Equal(513UL, journey.Production.TotalMaterialOperationCount);
        Assert.Single(journey.Production.MaterialHistory!.TerrainEvidence);
        AssertPartition(journey);
        Assert.Throws<InvalidOperationException>(() => journey.PlanDemote(AllocationA, CellA, Id(1000)));
    }

    [Fact]
    public void Empty_detail_demotion_records_its_cause_and_storage_clocks_without_creating_material()
    {
        var journey = new TerrainMaterialContinuationFixture();
        journey.Refine(AllocationA, CellA, Id(200));
        EntityId moved = journey.Transfer(journey.RawAt(Fine, 10).LotId, Away, 10, Id(201));
        journey.Craft(IngredientLotKind.Source, moved, 4, Id(202));
        journey.Craft(IngredientLotKind.Source, moved, 4, Id(203));
        journey.Consume(IngredientLotKind.Source, moved, Id(204));
        journey.Cut(first: true);
        Assert.Contains(CellA, journey.Registry.GetState(AllocationA).ActiveCells);
        Assert.Equal(0UL, journey.Registry.DetailedQuantity);
        var before = journey.Registry;
        journey.Demote(AllocationA, CellA, Id(205));
        AssertAdvance(before, journey.Registry, Coarse, Fine);
        Assert.Equal(before.Inventory.Custody.Lots.Select(RawLot), journey.Inventory.Custody.Lots.Select(RawLot));
        Assert.Empty(journey.Registry.GetState(AllocationA).ActiveCells);
        var evidence = Assert.Single(journey.Production.MaterialHistory!.TerrainEvidence);
        Assert.Empty(evidence.CreatedIds);
        Assert.NotEqual(evidence.BeforeRepresentationHash, evidence.AfterRepresentationHash);
        AssertPartition(journey);
        journey.Cut();
        Assert.Throws<InvalidOperationException>(() => journey.PlanRefine(AllocationA, CellA, Id(206)));
        Assert.Throws<InvalidOperationException>(() => journey.PlanRefine(AllocationB, CellB, Id(205)));
    }

    [Fact]
    public void Demotion_evidence_includes_coalesced_coarse_lots_outside_the_selected_cell()
    {
        var journey = new TerrainMaterialContinuationFixture(splitCoarse: true);
        journey.Refine(AllocationA, CellA, Id(300));
        journey.Cut(first: true);
        var untouched = journey.RawAt(Coarse, 90);
        Assert.Equal(new[] { (20UL, 40UL), (40UL, 90UL), (90UL, 170UL) }, Raw(journey, Coarse));
        journey.Demote(AllocationA, CellA, Id(301));
        Assert.Equal(new[] { (10UL, 90UL), (90UL, 170UL) }, Raw(journey, Coarse));
        Assert.False(journey.Inventory.ContainsIdentity(Id(23)));
        Assert.False(journey.Inventory.ContainsIdentity(Id(24)));
        Assert.Equal(untouched, journey.Inventory.Custody.GetLot(untouched.LotId));
        var merged = journey.RawAt(Coarse, 10);
        Assert.Equal(journey.Registry.Revision, merged.Revision);
        Assert.Equal(EntityId.DeriveSpawned(Id(301), 0), merged.LotId);
        byte[] bytes = TerrainMaterialRegistryCodecV4.Encode(journey.Registry);
        Assert.Equal(bytes, TerrainMaterialRegistryCodecV4.Encode(TerrainMaterialRegistryCodecV4.Decode(bytes, journey.Dependencies, TestContext.Current.CancellationToken)));
        AssertPartition(journey);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Foreign_allocation_representation_custody_cannot_retire_allocated_raw_stock_before_or_after_cuts(bool fine)
    {
        var journey = new TerrainMaterialContinuationFixture(distinctRepresentationStores: true);
        journey.Refine(AllocationA, CellA, Id(400));
        EntityId destination = fine ? OtherFine : OtherCoarse;
        EntityId transferred = journey.Transfer(journey.RawAt(Fine, 10).LotId, destination, 4, Id(401));
        journey.Cut(first: true);
        journey.Cut();
        var request = journey.CraftRequest(IngredientLotKind.Source, transferred, 4);
        var direct = new InstantCraftPlanner().Plan(journey.Inventory, journey.Production, new(Client, Session), Actor, Id(402), request, journey.Occupied);
        Assert.Equal(WorldActionRejection.None, direct.Rejection);
        Assert.Throws<ArgumentException>(() => journey.Registry.ReconcileProduction(direct.CandidateInventory, direct.CandidateProduction));
        var wrapped = new TerrainMaterialCraftPlanner().Plan(journey.Registry, new(Client, Session), Actor, Id(402), request, journey.Occupied);
        Assert.Equal(WorldActionRejection.Unavailable, wrapped.Rejection);
        Assert.Same(journey.Registry, wrapped.ApplyTo(journey.Registry));
        var observation = ProductionContinuationBasis.Capture(direct.CandidateInventory, direct.CandidateProduction);
        Assert.Contains(observation.RawRetirements, value => value.CustodianId == destination && value.Source.Start == 10 && value.Source.End == 14);
        Assert.Throws<ArgumentException>(() => direct.CandidateProduction.Continue(direct.CandidateInventory));
        Assert.Throws<ArgumentException>(() => direct.CandidateProduction.ContinueMaterial(direct.CandidateInventory));
        Assert.Equal(0UL, journey.Registry.RetiredQuantity);
        AssertPartition(journey);
    }

    [Fact]
    public void Combined_produced_occupancy_blocks_a_raw_transfer_that_raw_custody_alone_could_fit()
    {
        var journey = new TerrainMaterialContinuationFixture(awayCapacity: 10);
        journey.Refine(AllocationA, CellA, Id(500));
        EntityId moved = journey.Transfer(journey.RawAt(Fine, 10).LotId, Away, 4, Id(501));
        journey.Craft(IngredientLotKind.Source, moved, 4, Id(502));
        journey.Cut(first: true);
        journey.Refine(AllocationB, CellB, Id(503));
        var command = journey.TransferCommand(journey.RawAt(Fine, 90).LotId, Away, 8, Id(504));
        var raw = new CountedTransferAuthority().Decide(journey.Inventory, command, new(Client, Session), new(journey.Inventory.WorldId, journey.Registry.Revision + 1));
        Assert.Equal(OpenFantasy.Core.Commands.CommandReceiptStatus.Applied, raw.Receipt.Status);
        var combined = new MaterialTransferPlanner().Plan(journey.Inventory, journey.Production, command, new(Client, Session), journey.Occupied);
        Assert.Equal(WorldActionRejection.Unavailable, combined.Rejection);
        Assert.Same(journey.Inventory, combined.CandidateInventory);
        Assert.Same(journey.Production, combined.CandidateProduction);
        Assert.Equal(4UL, journey.Production.GetStoredQuantity(Away));
        AssertPartition(journey);
    }

    [Fact]
    public void Permanent_causes_and_generated_identity_lookup_are_forwarded_through_terrain_wrappers()
    {
        var journey = TerrainMaterialContinuationFixture.Earned();
        journey.Cut(first: true);
        journey.Cut();
        Assert.Throws<InvalidOperationException>(() => journey.PlanDemote(AllocationA, CellA, Id(100)));
        var seen = new HashSet<EntityId>();
        EntityId collision = EntityId.DeriveSpawned(Id(600), 0);
        Assert.Throws<InvalidOperationException>(() => journey.PlanDemote(AllocationA, CellA, Id(600), identity =>
        {
            seen.Add(identity);
            return identity == collision || journey.Occupied(identity);
        }));
        Assert.Contains(collision, seen);
        EntityId transformed = journey.Production.Lots.Single().LotId;
        var craft = new TerrainMaterialCraftPlanner().Plan(journey.Registry, new(Client, Session), Actor, Id(601),
            journey.CraftRequest(IngredientLotKind.Transformed, transformed, 2), identity => identity == EntityId.DeriveSpawned(Id(601), 1));
        Assert.Equal(WorldActionRejection.Unavailable, craft.Rejection);
        Assert.Same(journey.Registry, craft.ApplyTo(journey.Registry));
        var state = journey.Registry.GetState(AllocationA);
        Assert.Throws<ArgumentException>(() => new TerrainMaterialPlanner().Demote(state, [CellA], Id(602), state.Revision, journey.Occupied));
    }

    [Fact]
    public void Fully_consumed_craft_identity_still_requires_global_history_after_a_registry_cut()
    {
        var journey = TerrainMaterialContinuationFixture.Earned();
        EntityId retiredCommand = Id(102);
        EntityId retiredOutput = Assert.Single(journey.Production.Lots).LotId;
        for (ulong index = 0; index < 4; index++)
            journey.Consume(IngredientLotKind.Transformed, retiredOutput, Id(9000 + index));
        Assert.Empty(journey.Production.Lots);
        journey.Cut(first: true);
        Assert.Empty(journey.Production.Batches);
        Assert.False(journey.Production.ContainsIdentity(retiredCommand));
        Assert.False(journey.Production.ContainsIdentity(retiredOutput));
        Assert.True(journey.Occupied(retiredCommand));
        Assert.True(journey.Occupied(retiredOutput));

        var owner = journey.Registry;
        EntityId remainingRaw = journey.RawAt(Away, 15).LotId;
        var request = journey.CraftRequest(IngredientLotKind.Source, remainingRaw, 2);
        var planner = new TerrainMaterialCraftPlanner();
        Assert.Throws<ArgumentNullException>(() => planner.Plan(owner, new(Client, Session), Actor, retiredCommand, request));
        var rejected = planner.Plan(owner, new(Client, Session), Actor, retiredCommand, request, journey.Occupied);
        Assert.Equal(WorldActionRejection.Unavailable, rejected.Rejection);
        Assert.Same(owner, rejected.ApplyTo(owner));

        EntityId freshOutput = journey.Craft(IngredientLotKind.Source, remainingRaw, 2, Id(9004));
        var produced = Assert.Single(journey.Production.Lots);
        Assert.Equal(freshOutput, produced.LotId);
        Assert.Equal(2UL, produced.Quantity);
        Assert.Equal(2UL, journey.Production.TotalBatchCount);
        AssertPartition(journey);
    }

    [Fact]
    public void Actual_produced_lots_share_the_4096_lot_limit_with_representation_fragments()
    {
        var journey = new TerrainMaterialContinuationFixture(awayCapacity: 5000, extraLots: 4089);
        journey.Refine(AllocationA, CellA, Id(700));
        EntityId first = journey.Transfer(journey.RawAt(Fine, 10).LotId, Away, 4, Id(701));
        EntityId produced = journey.Craft(IngredientLotKind.Source, first, 4, Id(702));
        EntityId second = journey.Transfer(journey.RawAt(Fine, 14).LotId, Away, 4, Id(703));
        journey.Craft(IngredientLotKind.Source, second, 4, Id(704));
        journey.Craft(IngredientLotKind.Transformed, produced, 2, Id(705));
        journey.Cut(first: true);
        journey.Refine(AllocationB, CellB, Id(706));
        Assert.Equal(4096, journey.Inventory.Custody.Lots.Count + journey.Production.Lots.Count);
        Assert.Equal(3, journey.Production.Lots.Count);
        var hash = journey.Registry.ContentHash;
        Assert.Throws<ArgumentException>(() => journey.PlanRefine(AllocationA, CellA2, Id(707)));
        Assert.Equal(hash, journey.Registry.ContentHash);
        AssertPartition(journey);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_generic_material_action_cannot_hide_a_geographic_identity_collision_in_a_generic_cut(bool network)
    {
        var journey = TerrainMaterialContinuationFixture.Earned();
        journey.Cut(first: true);
        EntityId cause = network ? journey.Registry.Terrain.Network.ProvinceId : AllocationB;
        var product = Assert.Single(journey.Production.Lots);
        var direct = new InstantCraftPlanner().Plan(journey.Inventory, journey.Production, new(Client, Session), Actor,
            cause, journey.CraftRequest(IngredientLotKind.Transformed, product.LotId, 2), journey.Occupied);
        Assert.Equal(WorldActionRejection.None, direct.Rejection);
        Assert.Throws<ArgumentException>(() => journey.Registry.ReconcileProduction(direct.CandidateInventory, direct.CandidateProduction));
        Assert.Throws<ArgumentException>(() => direct.CandidateProduction.Continue(direct.CandidateInventory));
        Assert.Throws<ArgumentException>(() => direct.CandidateProduction.ContinueMaterial(direct.CandidateInventory));
        Assert.Equal(1UL, journey.Production.TotalBatchCount);
        AssertPartition(journey);
    }

    internal static void AssertPartition(TerrainMaterialContinuationFixture journey)
    {
        var basis = ProductionContinuationBasis.Capture(journey.Inventory, journey.Production);
        var live = journey.Inventory.Custody.Lots.Where(value => value.Source.SourceId == Source)
            .SelectMany(value => Enumerable.Range(checked((int)value.Source.Start), checked((int)value.Quantity)));
        var retired = basis.RawRetirements.Where(value => value.Source.SourceId == Source)
            .SelectMany(value => Enumerable.Range(checked((int)value.Source.Start), checked((int)value.Source.Quantity)));
        Assert.Equal(Enumerable.Range(10, 160), live.Concat(retired).Where(value => value >= 10 && value < 170).Order());
        Assert.Equal(160UL, journey.Registry.CoarseQuantity + journey.Registry.DetailedQuantity + journey.Registry.OtherQuantity + journey.Registry.RetiredQuantity);
        Assert.All(basis.RawRetirements, value => Assert.Equal(journey.Material.Item, value.Definition));
        foreach (var allocation in journey.Registry.Allocations)
        {
            var state = journey.Registry.GetState(allocation.AllocationId);
            Assert.Same(journey.Inventory, state.Inventory);
            Assert.Same(journey.Production, state.Production);
            Assert.Equal(80UL, state.CoarseQuantity + state.DetailedQuantity + state.OtherQuantity + state.RetiredQuantity);
        }
    }

    private static void AssertAdvance(TerrainMaterialRegistry before, TerrainMaterialRegistry after, params EntityId[] stores)
    {
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(before.Production!.Revision + 1, after.Production!.Revision);
        Assert.Equal(before.Production.TotalMaterialOperationCount + 1, after.Production.TotalMaterialOperationCount);
        foreach (var storage in before.Inventory.Custody.Storages)
            Assert.Equal(storage.Revision + (stores.Contains(storage.CustodianId) ? 1UL : 0UL), after.Inventory.Custody.GetStorage(storage.CustodianId).Revision);
    }
    private static object RawLot(OpenFantasy.Core.Custody.CountedSourceLot value) =>
        (value.LotId, value.CustodianId, value.Definition, value.Source, value.Revision, value.Protection, value.ReservedBy);
    private static (ulong, ulong, ulong, ulong) Totals(TerrainMaterialRegistry value) => (value.CoarseQuantity, value.DetailedQuantity, value.OtherQuantity, value.RetiredQuantity);
    private static (ulong, ulong)[] Raw(TerrainMaterialContinuationFixture journey, EntityId store) => journey.Inventory.Custody.Lots
        .Where(value => value.CustodianId == store).OrderBy(value => value.Source.Start).Select(value => (value.Source.Start, value.Source.End)).ToArray();
    private static (ulong, ulong)[] Retired(TerrainMaterialContinuationFixture journey) => ProductionContinuationBasis.Capture(journey.Inventory, journey.Production)
        .RawRetirements.Select(value => (value.Source.Start, value.Source.End)).ToArray();
}
