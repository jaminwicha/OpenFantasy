using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using static OpenFantasy.Core.Tests.Geography.ReservoirTransportFixture;
using static OpenFantasy.Core.Tests.Geography.ReservoirTransportContinuationFixture;

namespace OpenFantasy.Core.Tests.Geography;

public sealed class ReservoirTransportContinuationTests
{
    [Fact]
    public void Actual_split_delivery_crafting_recrafting_and_consumption_preserve_rights_across_three_cuts()
    {
        var journey = new ReservoirTransportContinuationFixture(split: true);
        EntityId ancestor = journey.Craft(IngredientLotKind.Source, Id(22), 4, Id(200));
        journey.Consume(IngredientLotKind.Source, Id(22), Id(201));
        journey.Schedule(Stage, Budget);
        var original = journey.State;
        var stale = original.PlanAdvance(journey.Original.Due, historicallyOccupied: journey.Occupied);
        journey.Cut(first: true);
        Assert.Throws<InvalidOperationException>(() => stale.ApplyTo(journey.State));
        Assert.Equal(original.Reservoirs.Capacity.Production!.ContentHash, journey.Production.Continuation!.Basis.ProductionHash);
        journey.Craft(IngredientLotKind.Transformed, ancestor, 2, Id(202));
        journey.Consume(IngredientLotKind.Transformed, ancestor, Id(203));
        journey.Consume(IngredientLotKind.Source, Id(23), Id(204));
        var beforeHold = journey.State;
        var hold = journey.Advance(hold: true);
        Assert.Empty(hold.CreatedIds);
        Assert.Equal(RunoffTransportStatus.Scheduled, Assert.Single(journey.State.Stages).Status);
        Assert.Equal(beforeHold.Reservoirs.Capacity.Inventory.ContentHash, journey.Inventory.ContentHash);
        Assert.Equal(beforeHold.Reservoirs.Capacity.Production!.ContentHash, journey.Production.ContentHash);
        var delivered = journey.Advance();
        var proof = Assert.Single(journey.Production.MaterialHistory!.TransportEvidence);
        Assert.Equal(Stage, proof.StageId);
        Assert.Equal(Budget, proof.ReservationId);
        Assert.Equal(ReservoirTransportEvidence.DeliveryCause(Stage), proof.CauseId);
        Assert.NotEqual(Stage, proof.CauseId);
        Assert.Equal(new WorldInstant(0, 3600), proof.CompletedAt);
        Assert.Equal(new[] { proof.CauseId, EntityId.DeriveSpawned(Stage, 0) }.Order(), delivered.CreatedIds.Order());
        Assert.Equal(RunoffTransportStatus.Completed, Assert.Single(journey.State.Stages).Status);
        Assert.Empty(journey.Capacity.Pending);
        Assert.Equal(new SourceInterval(Source, 0, 1000), journey.Inventory.Custody.GetLot(HeldLot).Source);
        Assert.Equal(1UL, journey.Inventory.Custody.GetLot(HeldLot).Revision);
        var fragment = journey.Inventory.Custody.GetLot(EntityId.DeriveSpawned(Stage, 0));
        Assert.Equal(new SourceInterval(Source, 1000, 2000), fragment.Source);
        Assert.Equal(Id(26), fragment.CustodianId);
        Assert.Equal(0UL, fragment.Revision);
        Assert.Null(fragment.ReservedBy);
        Assert.Equal(9998UL, journey.Production.GetCombinedStoredQuantity(journey.Inventory, SourceStore));
        Assert.Equal(1999UL, journey.Production.GetCombinedStoredQuantity(journey.Inventory, Receiver));
        journey.Cut();
        journey.Consume(IngredientLotKind.Source, HeldLot, Id(205));
        journey.Craft(IngredientLotKind.Source, HeldLot, 2, Id(206));
        journey.Cut();
        Assert.Equal(8UL, journey.Inventory.Custody.Revision);
        Assert.Equal(8UL, journey.Production.TotalMaterialOperationCount);
        Assert.Equal(3UL, journey.Production.TotalBatchCount);
        Assert.Equal(4UL, journey.Production.TotalConsumedItemCount);
        Assert.Equal(1UL, journey.Production.Lots.Single(l => l.LotId == ancestor).Quantity);
        Assert.Equal(new ProductionOutputInterval(EntityId.DeriveSpawned(Id(200), 0), 0, 3, 4),
            journey.Production.Lots.Single(l => l.LotId == ancestor).Output);
        Assert.Contains(journey.Production.Continuation!.Basis.BatchWitnesses, b => b.BatchId == EntityId.DeriveSpawned(Id(200), 0));
        Assert.Equal(new SourceInterval(Source, 3, 1000), journey.Inventory.Custody.GetLot(HeldLot).Source);
        Assert.Equal(fragment, journey.Inventory.Custody.GetLot(fragment.LotId));
        Assert.Equal(5UL, journey.Inventory.Custody.GetStorage(SourceStore).Revision);
        Assert.Equal(4UL, journey.Inventory.Custody.GetStorage(Receiver).Revision);
        Assert.Equal(1UL, journey.Inventory.Custody.GetStorage(Id(26)).Revision);
        Assert.Equal(new WorldInstant(0, 3600), journey.State.Boundary);
        Assert.Equal(RunoffTransportStatus.Completed, Assert.Single(journey.State.Stages).Status);
        Assert.True(journey.Occupied(proof.CauseId));
        Assert.Empty(journey.Production.MaterialHistory!.Operations);
    }

    [Fact]
    public void A_cut_retains_actual_completed_canceled_and_blocked_journal_facts_without_retroactive_material_operations()
    {
        var completed = new ReservoirTransportContinuationFixture(secondBudget: true);
        completed.Schedule(Stage, Budget);
        completed.Schedule(Id(300), SecondBudget, new(0, 3600));
        completed.Cancel(SecondBudget);
        completed.Advance();
        var facts = StageFacts(completed.State);
        Assert.Equal(RunoffTransportStatus.Completed, completed.State.Stages.Single(s => s.StageId == Stage).Status);
        Assert.Equal(RunoffTransportStatus.Canceled, completed.State.Stages.Single(s => s.StageId == Id(300)).Status);
        completed.Cut(first: true);
        Assert.Equal(facts, StageFacts(completed.State));
        Assert.Equal(0UL, completed.Production.TotalMaterialOperationCount);
        Assert.Equal(1UL, completed.Inventory.Custody.Revision);
        completed.Advance(hold: true);
        completed.Cut();
        Assert.Equal(facts, StageFacts(completed.State));
        Assert.Equal(new WorldInstant(0, 3600), completed.State.Boundary);

        var blocked = new ReservoirTransportContinuationFixture(freeSource: 1000, receiverUnits: 1001);
        blocked.Schedule(Stage, Budget);
        blocked.Advance();
        Assert.Equal(RunoffTransportStatus.BlockedByHead, Assert.Single(blocked.State.Stages).Status);
        var blockedFacts = StageFacts(blocked.State);
        blocked.Cut(first: true);
        Assert.Equal(blockedFacts, StageFacts(blocked.State));
        Assert.Equal(Budget, blocked.Inventory.Custody.GetLot(HeldLot).ReservedBy);
        Assert.Single(blocked.Capacity.Pending);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Actual_produced_water_counts_in_exact_final_head_and_consuming_one_unit_can_block_delivery(bool consume)
    {
        var journey = new ReservoirTransportContinuationFixture(freeSource: 1000);
        var output = journey.Craft(IngredientLotKind.Source, Id(22), 4, Id(400));
        journey.Schedule(Stage, Budget);
        journey.Cut(first: true);
        if (consume) journey.Consume(IngredientLotKind.Transformed, output, Id(401));
        var before = journey.Inventory.ContentHash;
        journey.Advance();
        Assert.Equal(consume ? RunoffTransportStatus.BlockedByHead : RunoffTransportStatus.Completed,
            Assert.Single(journey.State.Stages).Status);
        if (consume)
        {
            Assert.Equal(before, journey.Inventory.ContentHash);
            Assert.Empty(journey.Production.MaterialHistory!.TransportEvidence);
            Assert.Equal(Budget, journey.Inventory.Custody.GetLot(HeldLot).ReservedBy);
        }
        else
        {
            Assert.Equal(996UL, journey.Inventory.Custody.GetStoredQuantity(SourceStore));
            Assert.Equal(4UL, journey.Production.GetStoredQuantity(SourceStore));
            Assert.Equal(1000UL, journey.State.Reservoirs.Projections.Single(p => p.Geometry.CustodianId == SourceStore).ExistingUnits);
            Assert.Equal(2000UL, journey.State.Reservoirs.Projections.Single(p => p.Geometry.CustodianId == Receiver).ExistingUnits);
        }
    }

    [Fact]
    public void Multi_route_head_failure_rolls_back_every_fragment_cause_and_proof_without_spending_sibling_throughput()
    {
        var journey = new ReservoirTransportContinuationFixture(split: true, secondBudget: true, receiverUnits: 22001);
        journey.Schedule(Stage, Budget);
        journey.Schedule(Id(500), SecondBudget);
        journey.Cut(first: true);
        var oldHeld = journey.Inventory.Custody.GetLot(HeldLot);
        var plan = journey.Advance();
        Assert.Equal(RunoffTransportStatus.BlockedByHead, journey.State.Stages.Single(s => s.StageId == Stage).Status);
        Assert.Equal(RunoffTransportStatus.Completed, journey.State.Stages.Single(s => s.StageId == Id(500)).Status);
        Assert.Equal(oldHeld, journey.Inventory.Custody.GetLot(HeldLot));
        Assert.Equal(0UL, journey.Inventory.Custody.GetStoredQuantity(Id(26)));
        Assert.False(journey.Inventory.ContainsIdentity(EntityId.DeriveSpawned(Stage, 0)));
        Assert.DoesNotContain(ReservoirTransportEvidence.DeliveryCause(Stage), plan.CreatedIds);
        Assert.Equal(new[] { ReservoirTransportEvidence.DeliveryCause(Id(500)) }, plan.CreatedIds);
        Assert.Equal(Id(500), Assert.Single(journey.Production.MaterialHistory!.TransportEvidence).StageId);
        Assert.Equal(1UL, journey.Production.Revision);
        Assert.Equal(1UL, journey.Inventory.Custody.Revision);
        Assert.Equal(12000UL, journey.Inventory.Custody.GetStoredQuantity(SourceStore));
        Assert.Equal(23001UL, journey.Inventory.Custody.GetStoredQuantity(Receiver));
        journey.Cut();
        journey.Consume(IngredientLotKind.Source, Id(23), Id(501), 4002);
        var next = journey.Advance();
        Assert.All(journey.State.Stages, s => Assert.Equal(RunoffTransportStatus.Completed, s.Status));
        Assert.Contains(ReservoirTransportEvidence.DeliveryCause(Stage), next.CreatedIds);
        Assert.Contains(EntityId.DeriveSpawned(Stage, 0), next.CreatedIds);
        Assert.Empty(journey.Capacity.Pending);
        Assert.Equal(10000UL, journey.Inventory.Custody.GetStoredQuantity(SourceStore));
        Assert.Equal(19999UL, journey.Inventory.Custody.GetStoredQuantity(Receiver));
        Assert.Equal(1000UL, journey.Inventory.Custody.GetStoredQuantity(Id(26)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shared_throughput_or_the_actual_512_material_boundary_never_grants_a_second_delivery_at_the_same_time(bool fullMaterialWindow)
    {
        var journey = new ReservoirTransportContinuationFixture(secondBudget: true, maximumUnits: fullMaterialWindow ? 2000UL : 1000UL);
        journey.Schedule(Stage, Budget);
        journey.Schedule(Id(600), SecondBudget);
        journey.Cut(first: true);
        if (fullMaterialWindow)
        {
            EntityId moving = Id(22);
            for (ulong index = 0; index < 511; index++)
                moving = journey.Transfer(moving, index % 2 == 0 ? Receiver : SourceStore, 1, Id(10000 + index));
            Assert.Equal(511, journey.Production.MaterialHistory!.Operations.Count);
        }
        var first = journey.Advance();
        Assert.Equal(RunoffTransportStatus.Completed, journey.State.Stages.Single(s => s.StageId == Stage).Status);
        Assert.Equal(fullMaterialWindow ? RunoffTransportStatus.BlockedByMaterial : RunoffTransportStatus.BlockedByThroughput,
            journey.State.Stages.Single(s => s.StageId == Id(600)).Status);
        Assert.Equal(SecondBudget, journey.Inventory.Custody.GetLot(Id(24)).ReservedBy);
        Assert.Equal(new[] { ReservoirTransportEvidence.DeliveryCause(Stage) }, first.CreatedIds);
        Assert.Equal(fullMaterialWindow ? 512 : 1, journey.Production.MaterialHistory!.Operations.Count);
        var before = journey.State;
        journey.Cut();
        var same = journey.Advance(journey.State.Boundary);
        Assert.Empty(same.CreatedIds);
        Assert.Equal(before.Boundary, journey.State.Boundary);
        Assert.Equal(before.Reservoirs.Capacity.Inventory.ContentHash, journey.Inventory.ContentHash);
        Assert.Equal(SecondBudget, journey.Inventory.Custody.GetLot(Id(24)).ReservedBy);
        var second = journey.Advance();
        Assert.Equal(new[] { ReservoirTransportEvidence.DeliveryCause(Id(600)) }, second.CreatedIds);
        Assert.All(journey.State.Stages, s => Assert.Equal(RunoffTransportStatus.Completed, s.Status));
        Assert.Null(journey.Inventory.Custody.GetLot(Id(24)).ReservedBy);
        Assert.Equal(fullMaterialWindow ? 513UL : 2UL, journey.Production.TotalMaterialOperationCount);
        Assert.Single(journey.Production.MaterialHistory!.TransportEvidence);
        Assert.Empty(journey.Capacity.Pending);
    }

    [Fact]
    public void Permanent_history_is_required_before_callback_wrapping_and_archived_commands_cannot_be_scheduled_as_stages()
    {
        var journey = new ReservoirTransportContinuationFixture();
        journey.Consume(IngredientLotKind.Source, Id(22), Id(700));
        journey.Cut(first: true);
        Assert.False(journey.Production.ContainsIdentity(Id(700)));
        Assert.True(journey.Occupied(Id(700)));
        var before = journey.State.ContentHash;
        Assert.Throws<ArgumentNullException>(() => journey.State.PlanSchedule(Stage, Budget, journey.Original.Due, journey.Original.Routes));
        Assert.Throws<ArgumentNullException>(() => journey.State.PlanAdvance(journey.State.Boundary));
        Assert.Throws<ArgumentNullException>(() => journey.State.PlanAdvance(journey.Original.Due, readingHold: true));
        Assert.ThrowsAny<ArgumentException>(() => journey.State.PlanSchedule(Id(700), Budget, journey.Original.Due, journey.Original.Routes, journey.Occupied));
        Assert.Equal(before, journey.State.ContentHash);
        journey.Schedule(Stage, Budget);
        journey.Advance();
        Assert.Equal(RunoffTransportStatus.Completed, Assert.Single(journey.State.Stages).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_historical_delivery_cause_or_fragment_collision_keeps_the_entire_pending_delivery_funded(bool fragment)
    {
        var journey = new ReservoirTransportContinuationFixture(split: true);
        journey.Schedule(Stage, Budget);
        journey.Cut(first: true);
        EntityId occupied = fragment ? EntityId.DeriveSpawned(Stage, 0) : ReservoirTransportEvidence.DeliveryCause(Stage);
        var plan = journey.State.PlanAdvance(journey.Original.Due, historicallyOccupied: id => id == occupied || journey.Occupied(id));
        Assert.Empty(plan.CreatedIds);
        Assert.Equal(journey.Inventory.ContentHash, plan.Candidate.Reservoirs.Capacity.Inventory.ContentHash);
        Assert.Equal(journey.Production.ContentHash, plan.Candidate.Reservoirs.Capacity.Production!.ContentHash);
        Assert.Equal(RunoffTransportStatus.BlockedByMaterial, Assert.Single(plan.Candidate.Stages).Status);
        Assert.Single(plan.Candidate.Reservoirs.Capacity.Pending);
        Assert.Equal(Budget, plan.Candidate.Reservoirs.Capacity.Inventory.Custody.GetLot(HeldLot).ReservedBy);
        // A separately prepared, unpoisoned actual owner proves readiness and funding are otherwise valid.
        var control = new ReservoirTransportContinuationFixture(split: true);
        control.Schedule(Stage, Budget);
        control.Cut(first: true);
        control.Advance();
        Assert.Equal(RunoffTransportStatus.Completed, Assert.Single(control.State.Stages).Status);
    }

    [Fact]
    public void Only_the_actual_transport_owner_can_cut_its_coupled_material_history()
    {
        var journey = new ReservoirTransportContinuationFixture();
        journey.Schedule(Stage, Budget);
        var inline = journey.State;
        var foreignCapacity = inline.Reservoirs.Capacity.PlanContinueMaterial(cancellationToken: TestContext.Current.CancellationToken).ApplyTo(inline.Reservoirs.Capacity);
        var foreignReservoir = inline.Reservoirs.PlanApply(inline.Reservoirs.Capacity.PlanContinueMaterial(cancellationToken: TestContext.Current.CancellationToken)).ApplyTo(inline.Reservoirs);
        Assert.ThrowsAny<ArgumentException>(() => ReservoirTransportState.Create(foreignReservoir, inline.Connections, inline.Boundary));
        Assert.True(foreignCapacity.Production!.IsContinued);
        journey.Cut(first: true);
        var before = journey.State.ContentHash;
        Assert.ThrowsAny<ArgumentException>(() => journey.Production.Continue(journey.Inventory));
        Assert.ThrowsAny<ArgumentException>(() => journey.Production.ContinueMaterial(journey.Inventory));
        Assert.ThrowsAny<ArgumentException>(() => journey.Capacity.PlanContinue(cancellationToken: TestContext.Current.CancellationToken));
        Assert.ThrowsAny<ArgumentException>(() => journey.Capacity.PlanContinueMaterial(cancellationToken: TestContext.Current.CancellationToken));
        Assert.ThrowsAny<ArgumentException>(() => ReservoirTransportState.Create(journey.State.Reservoirs, journey.State.Connections, journey.State.Boundary));
        Assert.Equal(before, journey.State.ContentHash);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => journey.State.PlanContinue(canceled.Token));
    }
}
