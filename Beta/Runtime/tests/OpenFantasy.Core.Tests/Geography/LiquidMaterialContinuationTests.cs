using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Geography.LiquidMaterialContinuationFixture;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class LiquidMaterialContinuationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pending_funding_survives_actual_material_work_three_cuts_and_capacity_only_cancellation(bool material)
    {
        var journey = new LiquidMaterialContinuationFixture();
        var initial = journey.Reservoirs;
        var held = journey.Inventory.Custody.GetLot(HeldLot);
        Assert.Equal(1000UL, Assert.Single(journey.Capacity.Receivers).ProjectedUnits);
        Assert.Equal(1_000_000_000UL, Assert.Single(journey.Capacity.Receivers).ProjectedVolume.Value);
        EntityId survivor = journey.Craft(IngredientLotKind.Source, RawLot, 4, Id(100));
        journey.Consume(IngredientLotKind.Source, RawLot, Id(101));
        Assert.Equal(99UL, Assert.Single(journey.Capacity.Receivers).ExistingUnits);
        Assert.Equal(999UL, Assert.Single(journey.Capacity.Receivers).ProjectedUnits);
        Assert.Equal(1UL, journey.Production.TotalBatchCount);
        Assert.Equal(1UL, journey.Production.TotalConsumedItemCount);

        var before = journey.Reservoirs;
        var preparation = Assert.Single(before.Capacity.Pending);
        var cancel = before.Capacity.PlanCancel(Budget);
        var reservoirCancel = before.PlanApply(cancel);
        journey.Cut(material);
        AssertCutPreserved(before, journey.Reservoirs);
        Assert.Throws<InvalidOperationException>(() => cancel.ApplyTo(journey.Capacity));
        Assert.Throws<InvalidOperationException>(() => reservoirCancel.ApplyTo(journey.Reservoirs));
        Assert.Throws<InvalidOperationException>(() => preparation.EnsureCurrent(Assert.Single(journey.Capacity.Pending).Request));
        Assert.Throws<ArgumentException>(() => journey.Capacity.PlanAdmit(preparation));
        Assert.Throws<ArgumentException>(() => before.Capacity.PlanReconcile(journey.Inventory, journey.Production, TestContext.Current.CancellationToken));
        Assert.Equal(material, journey.Production.Continuation!.SupportsMaterialOperations);
        Assert.Single(journey.Production.Continuation.Basis.BatchWitnesses);

        AssertUnspendable(journey, HeldLot, Id(102));
        EntityId recrafted = journey.Craft(IngredientLotKind.Transformed, survivor, 2, Id(103));
        journey.Consume(IngredientLotKind.Transformed, survivor, Id(104));
        Assert.Equal(98UL, Assert.Single(journey.Capacity.Receivers).ExistingUnits);
        before = journey.Reservoirs;
        journey.Cut();
        AssertCutPreserved(before, journey.Reservoirs);
        Assert.Equal(2, journey.Production.Continuation!.Basis.BatchWitnesses.Count);
        Assert.Equal(3UL, journey.Production.Lots.Single(value => value.LotId == survivor).Output.Start);
        Assert.Equal(4UL, journey.Production.Lots.Single(value => value.LotId == survivor).Output.End);
        Assert.Contains(journey.Production.Lots, value => value.LotId == recrafted && value.Quantity == 2);

        journey.Craft(IngredientLotKind.Source, RawLot, 4, Id(105));
        journey.Consume(IngredientLotKind.Source, RawLot, Id(106));
        before = journey.Reservoirs;
        journey.Cut();
        AssertCutPreserved(before, journey.Reservoirs);
        Assert.Equal(3UL, journey.Production.TotalBatchCount);
        Assert.Equal(3UL, journey.Production.TotalConsumedItemCount);
        Assert.Equal(6UL, journey.Production.Revision);
        Assert.Equal(6UL, journey.Production.TotalMaterialOperationCount);
        Assert.Equal(97UL, Assert.Single(journey.Capacity.Receivers).ExistingUnits);
        Assert.Equal(997UL, Assert.Single(journey.Capacity.Receivers).ProjectedUnits);
        Assert.Equal(997_000_000UL, Assert.Single(journey.Capacity.Receivers).ProjectedVolume.Value);
        Assert.Equal(held, journey.Inventory.Custody.GetLot(HeldLot));
        Assert.Contains(journey.Production.Lots, value => value.LotId == survivor && value.Quantity == 1);
        Assert.Contains(journey.Production.Continuation!.Basis.BatchWitnesses,
            value => value.BatchId == EntityId.DeriveSpawned(Id(100), 0));

        AssertPermanentIdentityLookup(journey);
        Assert.Throws<ArgumentException>(() => journey.PlaceAll(new(Reservoir, new(0, 0, 0))));
        byte[] inventory = AuthorityEncodingV1.Encode(journey.Inventory);
        byte[] production = ProductionBytes(journey.Production);
        ulong revision = journey.Capacity.Revision;
        journey.Apply(journey.Capacity.PlanCancel(Budget));
        Assert.Empty(journey.Capacity.Pending);
        Assert.Equal([Budget], journey.Capacity.Closed);
        Assert.Equal(revision + 1, journey.Capacity.Revision);
        Assert.Equal(97UL, Assert.Single(journey.Capacity.Receivers).ProjectedUnits);
        Assert.Equal(0UL, Assert.Single(journey.Capacity.Receivers).IncomingUnits);
        Assert.Equal(inventory, AuthorityEncodingV1.Encode(journey.Inventory));
        Assert.Equal(production, ProductionBytes(journey.Production));
        Assert.Equal(held, journey.Inventory.Custody.GetLot(HeldLot));
        AssertUnspendable(journey, HeldLot, Id(109));
        Assert.Throws<ArgumentException>(() => journey.Capacity.PlanCancel(Budget));
        Assert.Equal(initial.Capacity.Domain.ContentHash, journey.Capacity.Domain.ContentHash);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Raw_and_earned_produced_water_actually_fall_and_spread_after_two_further_cuts(bool material)
    {
        var journey = new LiquidMaterialContinuationFixture(pending: false);
        EntityId produced = journey.Craft(IngredientLotKind.Source, RawLot, 4, Id(200));
        journey.Cut(material);
        var cell = new LiquidCellKey(Reservoir, new(0, 0, 2));
        LiquidCellState placement = journey.PlaceAll(cell);
        var planner = new LiquidCellPlanner();
        var stale = planner.PlanAdvance(placement, new(0, 1800));
        var originalUnits = Units(placement).ToArray();
        var originalDomain = placement.DomainCells.ToArray();
        byte[] authority = AuthorityEncodingV1.Encode(journey.Inventory);
        for (int cut = 0; cut < 2; cut++)
        {
            var before = journey.Reservoirs;
            var oldPlacement = placement;
            journey.Cut();
            AssertCutPreserved(before, journey.Reservoirs);
            placement = placement.Rebind(journey.Reservoirs);
            Assert.Equal(oldPlacement.Portions, placement.Portions);
            Assert.Equal(oldPlacement.Boundary, placement.Boundary);
            Assert.Equal(oldPlacement.Revision, placement.Revision);
            Assert.Equal(originalDomain, placement.DomainCells);
            Assert.Throws<InvalidOperationException>(() => stale.ApplyTo(placement));
            byte[] bytes = LiquidCellStateCodecV1.Encode(placement);
            placement = LiquidCellStateCodecV1.Decode(bytes, journey.Reservoirs);
            Assert.Equal(bytes, LiquidCellStateCodecV1.Encode(placement));
            placement = planner.PlanAdvance(placement, placement.Boundary.AddMilliseconds(1800)).ApplyTo(placement);
            Assert.Equal(originalUnits, Units(placement));
            stale = planner.PlanAdvance(placement, placement.Boundary.AddMilliseconds(1800));
        }
        // Both cuts preserve time; the two actual planner publications advance exactly two steps.
        Assert.Equal(2UL, placement.Revision);
        Assert.Equal(new WorldInstant(0, 3600), placement.Boundary);
        Assert.Contains(placement.Portions, value => value.Cell.Coordinate.Z == 0);
        Assert.Contains(placement.Portions, value => value.Cell.Coordinate.X == 1);
        Assert.Contains(placement.Portions, value => value.Origin == LiquidStockOrigin.ProducedLot
            && value.OriginId == produced && value.Cell.Coordinate.Z < 2);
        Assert.Contains(placement.Portions, value => value.Origin == LiquidStockOrigin.RawSource && value.Cell.Coordinate.Z < 2);
        Assert.Contains(placement.Portions, value => value.Origin == LiquidStockOrigin.RawSource && value.Cell.Coordinate.X == 1);
        Assert.Equal(authority, AuthorityEncodingV1.Encode(journey.Inventory));
        Assert.Equal(1UL, journey.Production.TotalBatchCount);
        Assert.Single(journey.Production.Continuation!.Basis.BatchWitnesses);
        Assert.Equal(10UL, Assert.Single(journey.Capacity.Receivers).ExistingUnits);
        Assert.Equal(10UL, journey.Inventory.Custody.GetStorage(Store).UnitCapacity);
        Assert.Empty(journey.Capacity.Pending);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Existing_detailed_placement_cannot_silently_relabel_consumed_or_recrafted_water(bool craft)
    {
        var journey = new LiquidMaterialContinuationFixture(pending: false);
        EntityId produced = journey.Craft(IngredientLotKind.Source, RawLot, 4, Id(300));
        journey.Cut(material: true);
        var placement = journey.PlaceAll(new(Reservoir, new(0, 0, 2)));
        byte[] before = LiquidCellStateCodecV1.Encode(placement);
        if (craft) journey.Craft(IngredientLotKind.Transformed, produced, 2, Id(301));
        else journey.Consume(IngredientLotKind.Transformed, produced, Id(301));
        Assert.Throws<ArgumentException>(() => placement.Rebind(journey.Reservoirs));
        Assert.Equal(before, LiquidCellStateCodecV1.Encode(placement));
        Assert.Equal(craft ? 10UL : 9UL, Assert.Single(journey.Capacity.Receivers).ExistingUnits);
        Assert.Equal(craft ? 2UL : 1UL, journey.Production.TotalBatchCount);
    }

    [Fact]
    public void Ordinary_reconciliation_cannot_hide_a_context_swap_inside_a_real_successor_operation()
    {
        var journey = new LiquidMaterialContinuationFixture();
        journey.Craft(IngredientLotKind.Source, RawLot, 4, Id(400));
        journey.Cut(material: true);
        var original = journey.Capacity;
        journey.Cut();
        var plan = journey.PlanConsume(IngredientLotKind.Source, RawLot, Id(401));
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        Assert.Throws<ArgumentException>(() => original.PlanReconcile(plan.CandidateInventory, plan.CandidateProduction, TestContext.Current.CancellationToken));
        journey.Reconcile(plan.CandidateInventory, plan.CandidateProduction);
        Assert.Equal(99UL, Assert.Single(journey.Capacity.Receivers).ExistingUnits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_real_initial_owner_without_history_can_select_continuation_without_inventing_a_clock_or_operation(bool material)
    {
        var journey = new LiquidMaterialContinuationFixture();
        var before = journey.Reservoirs;
        journey.Cut(material);
        AssertCutPreserved(before, journey.Reservoirs);
        Assert.Equal(0UL, journey.Production.Revision);
        Assert.Equal(0UL, journey.Production.TotalMaterialOperationCount);
        Assert.Equal(0UL, journey.Inventory.Custody.Revision);
        journey.Consume(IngredientLotKind.Source, RawLot, Id(450));
        Assert.Equal(1UL, journey.Production.TotalMaterialOperationCount);
        Assert.Equal(1UL, journey.Inventory.Custody.Revision);
        Assert.Equal(999UL, Assert.Single(journey.Capacity.Receivers).ProjectedUnits);
        Assert.Equal(new WorldInstant(-10, 0), Assert.Single(journey.Capacity.Pending).Request.Start);
        Assert.Equal(new WorldInstant(-9, 0), Assert.Single(journey.Capacity.Pending).Request.End);
    }

    [Fact]
    public void A_material_cut_does_not_authorize_reserved_gate_transport()
    {
        var fixture = new ReservoirTransportFixture();
        var original = fixture.State.Reservoirs;
        var request = Assert.Single(original.Capacity.Pending).Request;
        var production = new ProductionSnapshot(request.Inventory.WorldId, request.Inventory.BranchId, 0, [], [], [], []);
        var prepared = new WatershedRunoffPlanner().Prepare(new(request.Network, request.Mapping, request.Inventory,
            production, request.ReservationId, request.OwnerActorId, request.Start, request.End, request.Material, request.Inputs, request.Terminals), TestContext.Current.CancellationToken);
        var reservoirs = LiquidReservoirState.Create(WatershedRunoffCapacityState.Create(prepared), original.Geometries);
        var transport = ReservoirTransportState.Create(reservoirs, fixture.State.Connections, fixture.State.Boundary);
        var physical = reservoirs.PlanApply(reservoirs.Capacity.PlanContinueMaterial(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => transport.PlanApply(physical));
        Assert.Throws<ArgumentException>(() => ReservoirTransportState.Create(physical.Candidate,
            transport.Connections, transport.Boundary));
        Assert.Equal(reservoirs.ContentHash, transport.Reservoirs.ContentHash);
        Assert.Empty(transport.Stages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Actual_unreserved_receipt_cannot_spend_pending_count_or_physical_headroom_after_a_cut(bool physicalLimit)
    {
        var journey = new LiquidMaterialContinuationFixture(receiverUnitCapacity: physicalLimit ? 2000UL : 1000UL,
            receiverSpill: physicalLimit ? 1 : 2);
        journey.Cut(material: true);
        var lot = journey.Inventory.Custody.GetLot(FreeLot);
        var command = new CommandEnvelope(Id(470), Client, Session, Owner,
            journey.Inventory.WorldId, journey.Inventory.BranchId,
            [new(RevisionTargetKind.Branch, journey.Inventory.BranchId, journey.Inventory.Custody.Revision),
                new(RevisionTargetKind.Entity, lot.LotId, lot.Revision),
                new(RevisionTargetKind.Entity, SourceStore, journey.Inventory.Custody.GetStorage(SourceStore).Revision),
                new(RevisionTargetKind.Entity, Store, journey.Inventory.Custody.GetStorage(Store).Revision)],
            new(FreeLot, SourceStore, Store, lot.Quantity));
        var transfer = new MaterialTransferPlanner().Plan(journey.Inventory, journey.Production, command,
            new(Client, Session), journey.IsOccupied);
        Assert.Equal(WorldActionRejection.None, transfer.Rejection);
        Assert.Equal(110UL, transfer.CandidateProduction.GetCombinedStoredQuantity(transfer.CandidateInventory, Store));
        Assert.Equal(1010UL, 900 + transfer.CandidateProduction.GetCombinedStoredQuantity(transfer.CandidateInventory, Store));
        var before = journey.Reservoirs;
        Assert.Throws<ArgumentException>(() => journey.Capacity.PlanReconcile(transfer.CandidateInventory, transfer.CandidateProduction, TestContext.Current.CancellationToken));
        Assert.Same(before, journey.Reservoirs);
        Assert.Equal(0UL, journey.Inventory.Custody.Revision);
        Assert.Equal(0UL, journey.Production.TotalMaterialOperationCount);
        Assert.Equal(1000UL, Assert.Single(journey.Capacity.Receivers).ProjectedUnits);
        if (physicalLimit)
        {
            Assert.True(1010UL < journey.Inventory.Custody.GetStorage(Store).UnitCapacity);
            Assert.Equal(1_000_000_000UL, Assert.Single(journey.Capacity.Receivers).Capacity.Value);
        }
        else
        {
            Assert.True(1_010_000_000UL < Assert.Single(journey.Capacity.Receivers).Capacity.Value);
            Assert.Equal(1000UL, journey.Inventory.Custody.GetStorage(Store).UnitCapacity);
        }
        journey.Apply(journey.Capacity.PlanCancel(Budget));
        journey.Reconcile(transfer.CandidateInventory, transfer.CandidateProduction);
        Assert.Equal(110UL, Assert.Single(journey.Capacity.Receivers).ProjectedUnits);
        Assert.Equal(1UL, journey.Production.TotalMaterialOperationCount);
        Assert.Equal(Store, journey.Inventory.Custody.GetLot(FreeLot).CustodianId);
        Assert.Equal(Budget, journey.Inventory.Custody.GetLot(HeldLot).ReservedBy);
    }

    internal static void AssertCutPreserved(LiquidReservoirState before, LiquidReservoirState after)
    {
        Assert.Equal(before.Capacity.Domain.ContentHash, after.Capacity.Domain.ContentHash);
        Assert.Equal(before.Capacity.Revision, after.Capacity.Revision);
        Assert.Equal(before.Capacity.Closed, after.Capacity.Closed);
        Assert.Equal(before.Capacity.Receivers, after.Capacity.Receivers);
        Assert.Equal(before.Capacity.Inventory.ContentHash, after.Capacity.Inventory.ContentHash);
        Assert.Equal(before.Geometries.Select(value => value.ContentHash), after.Geometries.Select(value => value.ContentHash));
        Assert.Equal(before.Capacity.Production!.Lots.Select(Lot), after.Capacity.Production!.Lots.Select(Lot));
        Assert.Equal(before.Capacity.Pending.Count, after.Capacity.Pending.Count);
        for (int index = 0; index < before.Capacity.Pending.Count; index++)
        {
            var a = before.Capacity.Pending[index];
            var b = after.Capacity.Pending[index];
            Assert.NotEqual(a.ContentHash, b.ContentHash);
            Assert.Equal(a.Request.ReservationId, b.Request.ReservationId);
            Assert.Equal(a.Request.OwnerActorId, b.Request.OwnerActorId);
            Assert.Equal(a.Request.Start, b.Request.Start);
            Assert.Equal(a.Request.End, b.Request.End);
            Assert.Equal(a.Request.Inputs.Select(value => (value.BasinId, value.DepthMicrometres, value.Source, value.CustodianId)),
                b.Request.Inputs.Select(value => (value.BasinId, value.DepthMicrometres, value.Source, value.CustodianId)));
            Assert.Equal(a.Request.Terminals, b.Request.Terminals);
            Assert.Equal(a.TotalInputUnits, b.TotalInputUnits);
            Assert.Equal(a.TotalInputVolume, b.TotalInputVolume);
            Assert.Equal(a.Terminals, b.Terminals);
            Assert.Equal(a.Receivers, b.Receivers);
            foreach (var basin in a.Request.Network.Basins)
                for (int column = 0; column < basin.Request.Bounds.ColumnCount; column++)
                    Assert.Equal(a.ContributingUnits(basin.BasinId, column), b.ContributingUnits(basin.BasinId, column));
        }
        for (int index = 0; index < before.Projections.Count; index++)
        {
            var a = before.Projections[index];
            var b = after.Projections[index];
            Assert.Equal(a.Geometry.ContentHash, b.Geometry.ContentHash);
            Assert.Equal(a.ExistingUnits, b.ExistingUnits);
            Assert.Equal(a.ReservedIncomingUnits, b.ReservedIncomingUnits);
            AssertFill(a.ActualFill, b.ActualFill);
            AssertFill(a.ProjectedFill, b.ProjectedFill);
        }
    }

    private static void AssertFill(ReservoirFill before, ReservoirFill after)
    {
        Assert.Equal(before.Volume, after.Volume);
        Assert.Equal(before.SurfaceMetres, after.SurfaceMetres);
        Assert.Equal(before.SurfaceBillionths, after.SurfaceBillionths);
        Assert.Equal(before.RemainderMicrolitres, after.RemainderMicrolitres);
        Assert.Equal(before.Columns, after.Columns);
    }

    private static object Lot(TransformedLot value) => (value.LotId, value.CustodianId, value.Definition,
        value.Output, value.Revision, value.Protection, value.ReservedBy);

    private static void AssertUnspendable(LiquidMaterialContinuationFixture journey, EntityId held, EntityId command)
    {
        var consume = journey.PlanConsume(IngredientLotKind.Source, held, command);
        var craft = journey.PlanCraft(IngredientLotKind.Source, held, 4, command);
        Assert.Equal(WorldActionRejection.Unavailable, consume.Rejection);
        Assert.Equal(WorldActionRejection.Unavailable, craft.Rejection);
        Assert.Same(journey.Inventory, consume.CandidateInventory);
        Assert.Same(journey.Production, consume.CandidateProduction);
        Assert.Same(journey.Inventory, craft.CandidateInventory);
        Assert.Same(journey.Production, craft.CandidateProduction);
    }

    private static void AssertPermanentIdentityLookup(LiquidMaterialContinuationFixture journey)
    {
        var visited = new HashSet<EntityId>();
        var plan = journey.PlanCraft(IngredientLotKind.Source, RawLot, 2, Id(108), identity =>
        {
            visited.Add(identity);
            return identity == EntityId.DeriveSpawned(Id(108), 1) || journey.IsOccupied(identity);
        });
        Assert.Equal(WorldActionRejection.Unavailable, plan.Rejection);
        Assert.Contains(EntityId.DeriveSpawned(Id(108), 1), visited);
        Assert.Equal(WorldActionRejection.Unavailable,
            journey.PlanConsume(IngredientLotKind.Source, RawLot, Id(101)).Rejection);
        Assert.Equal(WorldActionRejection.Unavailable,
            journey.PlanCraft(IngredientLotKind.Source, RawLot, 4, Id(100)).Rejection);
    }

    private static IEnumerable<(EntityId Reservoir, LiquidStockOrigin Origin, EntityId Identity, ulong Unit)> Units(LiquidCellState state) =>
        state.Portions.SelectMany(portion => Enumerable.Range(0, checked((int)portion.Quantity))
            .Select(offset => (portion.Cell.ReservoirId, portion.Origin, portion.OriginId, portion.Start + (ulong)offset)))
            .OrderBy(value => value.ReservoirId).ThenBy(value => value.Origin).ThenBy(value => value.OriginId).ThenBy(value => value.Item4);

    private static byte[] ProductionBytes(ProductionSnapshot production) => production.Continuation!.SupportsMaterialOperations
        ? ProductionCodecV4.Encode(production) : ProductionCodecV3.Encode(production);
}
