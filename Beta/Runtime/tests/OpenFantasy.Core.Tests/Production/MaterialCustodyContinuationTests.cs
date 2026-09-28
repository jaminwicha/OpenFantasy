using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Tests.Gathering;
using OpenFantasy.Core.Tests.Worlds;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;

namespace OpenFantasy.Core.Tests.Production;

public sealed class MaterialCustodyContinuationTests
{
    [Fact]
    public void Transfer_bridge_rejects_combined_overflow_even_when_the_actual_raw_transfer_fits()
    {
        var (journey, _) = MaterialCustodyContinuationFixture.FullMixedDestination();
        var command = journey.TransferCommand(Id(70), Id(61), 1, 78100);
        var rawOnly = new CountedTransferAuthority().Decide(journey.Inventory, command,
            journey.DeliveryFor(Id(10)), new(journey.Inventory.WorldId, journey.Production.Revision + 1));
        Assert.Equal(CommandReceiptStatus.Applied, rawOnly.Receipt.Status);
        Assert.Equal(10UL, rawOnly.Candidate.Custody.GetStoredQuantity(Id(61)));
        Assert.Equal(11UL, journey.Production.GetCombinedStoredQuantity(rawOnly.Candidate, Id(61)));
        var refused = journey.PlanTransfer(command);
        Assert.Equal(WorldActionRejection.Unavailable, refused.Rejection);
        Assert.Same(journey.Inventory, refused.CandidateInventory);
        Assert.Same(journey.Production, refused.CandidateProduction);
        journey.Production.ValidateForAuthority(journey.Inventory);
    }

    [Fact]
    public void Actual_1028_material_operations_hit_the_512_envelope_twice_without_losing_custody_or_history()
    {
        var journey = MaterialCustodyContinuationFixture.Create();
        var protectedOriginal = journey.Inventory.Custody.GetLot(Id(70));
        EntityId moving = journey.Gather(80000);
        EntityId product = journey.Craft(moving, 80001);
        journey.Consume(IngredientLotKind.Transformed, product, 80002);
        Assert.Equal((2UL, 3UL, 1UL, Id(60)), Lot(journey, moving));
        for (ulong index = 0; index < 1025; index++)
        {
            EntityId destination = journey.Inventory.Custody.GetLot(moving).CustodianId == Id(60) ? Id(61) : Id(60);
            ulong command = 80003 + index;
            if (journey.Production.MaterialHistory!.Operations.Count == MaterialOperationHistory.MaximumOperations)
            {
                var full = journey.PlanTransfer(journey.TransferCommand(moving, destination, 1, command));
                Assert.Equal(WorldActionRejection.Unavailable, full.Rejection);
                Assert.Same(journey.Inventory, full.CandidateInventory);
                Assert.Same(journey.Production, full.CandidateProduction);
                journey.Cut();
            }
            Assert.Equal(moving, journey.Transfer(moving, destination, 1, command));
        }
        Assert.Equal(512, MaterialOperationHistory.MaximumOperations);
        Assert.Equal(2, journey.Closed.Count);
        Assert.All(journey.Closed, closed => Assert.Equal(512, closed.Production.MaterialHistory!.Operations.Count));
        Assert.Equal(1028UL, journey.Production.TotalMaterialOperationCount);
        Assert.Equal(1028UL, journey.Production.Revision);
        Assert.Equal(1028UL, journey.Inventory.Custody.Revision);
        Assert.Equal(1028UL, journey.Inventory.Custody.GetStorage(Id(60)).Revision);
        Assert.Equal(1025UL, journey.Inventory.Custody.GetStorage(Id(61)).Revision);
        Assert.Equal((2UL, 3UL, 1026UL, Id(61)), Lot(journey, moving));
        Assert.Equal(4, journey.Production.MaterialHistory!.Operations.Count);
        Assert.Equal(1024UL, journey.Production.Continuation!.BaseMaterialOperationCount);
        Assert.Equal(1UL, journey.Production.TotalBatchCount);
        Assert.Equal(1UL, journey.Production.TotalConsumedItemCount);
        Assert.Equal(protectedOriginal, journey.Inventory.Custody.GetLot(Id(70)));
        var retired = Assert.Single(ProductionContinuationBasis.Capture(journey.Inventory, journey.Production).RawRetirements);
        Assert.Equal(new SourceInterval(GatheringFixture.Source, 0, 2), retired.Source);
        Assert.Equal(3UL, retired.Source.Quantity + journey.Inventory.Custody.GetLot(moving).Quantity);
        AssertRetainedSegments(journey);
        journey.Reopen();
        Assert.Equal(WorldActionRejection.None,
            journey.PlanTransfer(journey.TransferCommand(moving, Id(60), 1, 82000)).Rejection);
        var old = journey.PlanTransfer(journey.TransferCommand(moving, Id(60), 1, 80003));
        Assert.Equal(WorldActionRejection.Unavailable, old.Rejection);
        Assert.Same(journey.Inventory, old.CandidateInventory);
        Assert.Same(journey.Production, old.CandidateProduction);
        // The finite repeated journey proves window semantics, not a throughput benchmark.
    }

    [Fact]
    public void Actual_issuance_transfers_reserved_deliveries_and_retirements_preserve_rights_across_two_cuts()
    {
        var journey = MaterialCustodyContinuationFixture.Create();
        var protectedOriginal = journey.Inventory.Custody.GetLot(Id(70));
        EntityId first = journey.Gather(70000);
        EntityId split = journey.Transfer(first, Id(61), 1, 70001);
        Assert.Equal((0UL, 1UL, 0UL, Id(61)), Lot(journey, split));
        Assert.Equal((1UL, 3UL, 1UL, Id(60)), Lot(journey, first));
        EntityId returned = journey.Transfer(split, Id(60), 1, 70002);
        Assert.Equal(split, returned);
        Assert.Equal((0UL, 1UL, 1UL, Id(60)), Lot(journey, returned));
        EntityId reserved = journey.Deliver(returned, 1, 70003);
        Assert.Equal(returned, reserved);
        Assert.Equal((0UL, 1UL, 2UL, Id(61)), Lot(journey, reserved));
        EntityId product = journey.Craft(first, 70004);
        journey.Consume(IngredientLotKind.Transformed, product, 70005);
        Assert.Equal(6UL, journey.Production.Revision);
        journey.Cut();

        EntityId second = journey.Gather(70007);
        product = journey.Craft(second, 70008);
        journey.Consume(IngredientLotKind.Transformed, product, 70009);
        journey.Consume(IngredientLotKind.Source, second, 70010);
        Assert.DoesNotContain(journey.Inventory.Custody.Lots, lot => lot.LotId == second);
        journey.Cut();
        Assert.Equal(200U, journey.Gathering.GetActorWork(Id(10)).Effort);
        var exhausted = journey.PlanGather(journey.GatherCommand(70011));
        Assert.Equal(WorldActionRejection.Unavailable, exhausted.Rejection);
        Assert.Same(journey.Inventory, exhausted.CandidateInventory);
        Assert.Same(journey.Production, exhausted.CandidateProduction);
        Assert.Same(journey.Gathering, exhausted.CandidateGathering);
        var worldHash = journey.SourceWorld.ContentHash;
        journey.AdvanceSourceWorld(20);
        Assert.NotEqual(worldHash, journey.SourceWorld.ContentHash);
        Assert.Equal(20UL, journey.SourceWorld.ProcessIntervals);
        Assert.Equal(400U, journey.Gathering.GetActorWork(Id(10)).Effort);
        EntityId third = journey.Gather(70012);
        EntityId secondReserved = journey.Deliver(third, 1, 70013);
        Assert.NotEqual(third, secondReserved);
        journey.Consume(IngredientLotKind.Source, third, 70014);

        Assert.Equal(13UL, journey.Production.Revision);
        Assert.Equal(13UL, journey.Inventory.Custody.Revision);
        Assert.Equal(13UL, journey.Inventory.Custody.GetStorage(Id(60)).Revision);
        Assert.Equal(4UL, journey.Inventory.Custody.GetStorage(Id(61)).Revision);
        Assert.Equal(2UL, journey.Production.TotalBatchCount);
        Assert.Equal(4UL, journey.Production.TotalConsumedItemCount);
        Assert.Equal(13UL, journey.Production.TotalMaterialOperationCount);
        Assert.Equal(protectedOriginal, journey.Inventory.Custody.GetLot(Id(70)));
        Assert.Empty(journey.Production.Lots);
        Assert.Equal((8UL, 9UL, 2UL, Id(60)), Lot(journey, third));
        Assert.Equal((6UL, 7UL, 0UL, Id(61)), Lot(journey, secondReserved));
        Assert.Equal(new[] { MaterialOperationKind.Gather, MaterialOperationKind.SourceDelivery, MaterialOperationKind.Consume },
            journey.Production.MaterialHistory!.Operations.Select(operation => operation.Kind));
        Assert.Equal(new[] { Id(70012), Id(70013), Id(70014) },
            journey.Production.MaterialHistory!.Operations.Select(operation => operation.CommandId));
        Assert.Equal(new[] { 11UL, 12UL, 13UL },
            journey.Production.MaterialHistory!.Operations.Select(operation => operation.Sequence));
        var basis = ProductionContinuationBasis.Capture(journey.Inventory, journey.Production);
        var retirement = basis.RawRetirements.Where(value => value.Source.SourceId == GatheringFixture.Source).ToArray();
        Assert.Equal(new[] { (1UL, 6UL, Id(60)), (7UL, 8UL, Id(60)) },
            retirement.Select(value => (value.Source.Start, value.Source.End, value.CustodianId)));
        var live = basis.RawLots.Where(lot => lot.Source.SourceId == GatheringFixture.Source).OrderBy(lot => lot.Source.Start).ToArray();
        Assert.Equal(new[] { (0UL, 1UL), (6UL, 7UL), (8UL, 9UL) }, live.Select(lot => (lot.Source.Start, lot.Source.End)));
        Assert.All(live.Take(2), lot => Assert.Equal(MaterialCustodyContinuationFixture.DeliveryReservation, lot.ReservedBy));
        Assert.Null(live[2].ReservedBy);
        Assert.Equal(9UL, retirement.Aggregate(0UL, (sum, value) => sum + value.Source.Quantity)
            + live.Aggregate(0UL, (sum, lot) => sum + lot.Quantity));
        Assert.Equal(9UL, journey.Gathering.GetNode(GatheringFixture.Node).NextUnissuedUnit);
        Assert.Equal(20UL, journey.Gathering.GetNode(GatheringFixture.Node).UnitCapacity);
        Assert.Equal(WorldActionRejection.Unavailable, journey.PlanConsume(IngredientLotKind.Source, reserved, 70100).Rejection);
        Assert.NotEqual(WorldActionRejection.None,
            journey.PlanTransfer(journey.TransferCommand(reserved, Id(60), 1, 70101)).Rejection);
        AssertRetainedSegments(journey);
        journey.Reopen();

        // A genuinely eligible new swing contrasts with reuse of a command from the first cut.
        journey.AdvanceSourceWorld(40);
        Assert.Equal(WorldActionRejection.None, journey.PlanGather(journey.GatherCommand(70102)).Rejection);
        Assert.Equal(WorldActionRejection.Unavailable, journey.PlanGather(journey.GatherCommand(70000)).Rejection);
    }

    [Fact]
    public void Favorite_transfer_preserves_its_rule_while_delivery_and_consumption_remain_stricter()
    {
        var journey = MaterialCustodyContinuationFixture.Create(reserveProtectedStock: false);
        var original = journey.Inventory.Custody.GetLot(Id(70));
        var delivery = new MaterialSourceDeliveryPlanner().Plan(journey.Inventory, journey.Production,
            journey.DeliveryFor(Id(10)), Id(10), Id(71000), journey.DeliveryRequest(original.LotId, 1), journey.IsOccupied);
        Assert.Equal(WorldActionRejection.Unavailable, delivery.Rejection);
        Assert.Equal(WorldActionRejection.Unavailable, journey.PlanConsume(IngredientLotKind.Source, original.LotId, 71001).Rejection);
        EntityId moved = journey.Transfer(original.LotId, Id(61), original.Quantity, 71002);
        Assert.Equal(original.LotId, moved);
        Assert.Equal(LotProtection.Favorite, journey.Inventory.Custody.GetLot(moved).Protection);
        Assert.Equal(1UL, journey.Inventory.Custody.GetLot(moved).Revision);
        journey.Cut();
        journey.Reopen();
        Assert.Equal(Id(61), journey.Inventory.Custody.GetLot(moved).CustodianId);
        Assert.Equal(LotProtection.Favorite, journey.Inventory.Custody.GetLot(moved).Protection);
    }

    [Fact]
    public void External_effort_spending_cannot_be_replaced_by_implicit_source_recovery()
    {
        var journey = MaterialCustodyContinuationFixture.Create();
        journey.Gather(72000);
        journey.AdvanceSourceWorld(10);
        Assert.Equal(700U, journey.Gathering.GetActorWork(Id(10)).Effort);
        var actual = journey.Gathering;
        // Exercise actual partial excavation labor against an explicit standalone catalog.
        // This catalog is not asserted to be admitted into a complete excavation world profile.
        var world = journey.SourceWorld;
        // The actor's footprint straddles cells around (0,0); excavate beyond that
        // footprint so the complete eventual opening preserves actual support.
        var cell = new CellCoordinate(0, 1, 0);
        var region = world.Space.Regions.Single();
        Assert.True(region.GetCell(cell).IsSolid);
        var eventualSpace = new LocalSpatialSnapshot(world.Space.Scope,
            [region.Excavate(region.Revision, [cell])], world.Space.Actors,
            world.Space.Structures, world.Space.GroundSupport, world.Space.StructureSupport);
        Assert.All(actual.Nodes, node => Assert.Equal(SpatialOccupancy.Clear,
            ResourceInteractionQueries.QueryAnchor(eventualSpace, node.Feet)));
        var physical = new MaterialDefinition(new PackageId("material-custody-test"), Id(9700), "substrate",
            new(1), new(ExcavationSite.CellVolumeMicrolitres), new(1));
        var site = new ExcavationSite(Id(9701), cell, region.GetCell(cell).SourceId!.Value, Id(9702), Id(10),
            new(Material, physical), GatheringFixture.Axe, 2, 0, new(128, 128, 256), ExcavationReachMode.RaisedBankTop);
        var workCommand = StepFixture.Action(world, 72010, new ExcavateIntent(site.SiteId, site.Revision,
            region.Revision, GatheringFixture.ToolLot, 0, journey.Inventory.Custody.GetStorage(Id(60)).Revision));
        var work = new ExcavationPlanner().Plan(world, journey.Inventory, world.Space, actual,
            ProductionWorldFixture.Ledger(world), new([site]), workCommand, true, false, journey.IsOccupied);
        Assert.Equal(WorldActionRejection.None, work.Rejection);
        Assert.Null(work.SpawnedId);
        Assert.Same(journey.Inventory, work.Inventory);
        Assert.Equal(1UL, Assert.Single(work.Excavation.Sites).CompletedWork);
        var externallySpent = work.Gathering;
        Assert.Equal(300U, externallySpent.GetActorWork(Id(10)).Effort);
        Assert.Equal(actual.GetNode(GatheringFixture.Node).NextUnissuedUnit,
            externallySpent.GetNode(GatheringFixture.Node).NextUnissuedUnit);
        var command = journey.GatherCommand(72001);
        Assert.Equal(WorldActionRejection.None, journey.PlanGather(command).Rejection);
        var refused = new MaterialGatherPlanner().Plan(journey.SourceWorld, journey.Inventory, journey.Production,
            externallySpent, command, journey.IsOccupied);
        Assert.NotEqual(WorldActionRejection.None, refused.Rejection);
        Assert.Same(journey.Inventory, refused.CandidateInventory);
        Assert.Same(journey.Production, refused.CandidateProduction);
        Assert.Same(externallySpent, refused.CandidateGathering);
    }

    [Fact]
    public void Stale_transfer_and_historically_occupied_split_or_gather_identity_spend_nothing()
    {
        var journey = MaterialCustodyContinuationFixture.Create();
        EntityId gathered = journey.Gather(73000);
        var stale = journey.TransferCommand(gathered, Id(61), 1, 73001);
        journey.Transfer(gathered, Id(61), 1, 73002);
        Assert.NotEqual(WorldActionRejection.None, journey.PlanTransfer(stale).Rejection);
        var split = journey.TransferCommand(gathered, Id(61), 1, 73003);
        Assert.Equal(WorldActionRejection.None, journey.PlanTransfer(split).Rejection);
        var occupied = journey.PlanTransfer(split, identity => identity == EntityId.DeriveSpawned(split.CommandId, 0));
        Assert.Equal(WorldActionRejection.Unavailable, occupied.Rejection);
        Assert.Same(journey.Inventory, occupied.CandidateInventory);
        Assert.Same(journey.Production, occupied.CandidateProduction);
        journey.AdvanceSourceWorld(10);
        var actual = journey.Gathering;
        Assert.Equal(10UL, actual.AdvancingStep);
        Assert.Equal(700U, actual.GetActorWork(Id(10)).Effort);
        var gather = journey.GatherCommand(73004);
        Assert.Equal(WorldActionRejection.None, journey.PlanGather(gather).Rejection);
        var blockedGather = journey.PlanGather(gather, identity => identity == EntityId.DeriveSpawned(gather.CommandId, 0));
        Assert.Equal(WorldActionRejection.Unavailable, blockedGather.Rejection);
        Assert.Same(actual, blockedGather.CandidateGathering);
        var afterIdleTransfer = journey.PlanTransfer(journey.TransferCommand(gathered, Id(61), 1, 73005));
        Assert.Equal(WorldActionRejection.None, afterIdleTransfer.Rejection);
        Assert.Null(afterIdleTransfer.CandidateGathering);
    }

    private static (ulong Start, ulong End, ulong Revision, EntityId Custodian) Lot(MaterialCustodyContinuationFixture journey, EntityId id)
    {
        var lot = journey.Inventory.Custody.GetLot(id);
        return (lot.Source.Start, lot.Source.End, lot.Revision, lot.CustodianId);
    }

    private static void AssertRetainedSegments(MaterialCustodyContinuationFixture journey)
    {
        Assert.Equal(2, journey.Closed.Count);
        foreach (var closed in journey.Closed)
        {
            var restored = ProductionCodecV4.Decode(closed.Bytes, closed.Production.Continuation!,
                closed.Production.MaterialHistory!.GatherEvidence.ToArray());
            restored.ValidateForAuthority(closed.Inventory);
            Assert.Equal(closed.Bytes, ProductionCodecV4.Encode(restored));
            var next = restored.Continue(closed.Inventory);
            Assert.Equal(restored.Revision, next.Revision);
            Assert.Equal(restored.MaterialHistory!.Gathering!.ContentHash, next.MaterialHistory!.Gathering!.ContentHash);
        }
    }
}
