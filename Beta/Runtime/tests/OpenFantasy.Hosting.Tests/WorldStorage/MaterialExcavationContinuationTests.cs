using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.WorldStorage.MaterialExcavationFixture;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class MaterialExcavationContinuationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Actual_partial_world_work_survives_two_cuts_before_one_atomic_whole_cell_yield(bool npc)
    {
        var journey = new MaterialExcavationFixture(npc: npc, legacyWork: 1);
        var originalSite = journey.Excavation.Sites.Single();
        Assert.Equal(1UL, originalSite.CompletedWork);
        Assert.Equal(41UL, journey.InitialWorld.AdvancingSteps);
        Assert.Equal(10U, journey.Gathering.GetActorWork(journey.Actor).Effort);
        var originalInventory = journey.Inventory;
        ulong originalStorage = originalInventory.Custody.GetStorage(journey.Bag).Revision;
        var solid = Region(journey.State, originalSite.Cell).ComputeContentHash();
        journey.Cut();

        var partial = journey.Work();
        Assert.IsType<ExcavationWorkEvidence>(partial.Evidence);
        Assert.Equal(9, (int)Assert.Single(journey.Production.MaterialHistory!.Operations).Kind);
        Assert.Equal(2UL, journey.Excavation.Sites.Single().CompletedWork);
        Assert.Equal(originalInventory.ContentHash, journey.Inventory.ContentHash);
        Assert.Equal(solid, Region(journey.State, originalSite.Cell).ComputeContentHash());
        Assert.Equal(10U, journey.Gathering.GetActorWork(journey.Actor).Effort);
        Assert.Equal(journey.SourceWorld.ProcessIntervals, journey.Gathering.AdvancingStep);
        Assert.Equal(1UL, journey.Production.TotalMaterialOperationCount);
        journey.Cut();

        var complete = journey.Work();
        Assert.IsType<ExcavationWorkEvidence>(complete.Evidence);
        Assert.True(journey.Excavation.Sites.Single().IsComplete);
        Assert.True(Region(journey.State, originalSite.Cell).GetCell(originalSite.Cell).IsExcavated);
        Assert.Equal(new SourceInterval(originalSite.CountedSourceId, 0, 8), journey.Extracted().Source);
        Assert.Equal(0UL, journey.Extracted().Revision);
        Assert.Equal(originalInventory.Custody.Revision + 1, journey.Inventory.Custody.Revision);
        Assert.Equal(originalStorage + 1, journey.Inventory.Custody.GetStorage(journey.Bag).Revision);
        Assert.Equal(2UL, journey.Production.Revision);
        Assert.Equal(2UL, journey.Production.TotalMaterialOperationCount);
        Assert.Single(journey.Production.MaterialHistory!.Operations);
        Assert.Equal(10U, journey.Gathering.GetActorWork(journey.Actor).Effort);
        // The donor still contains its actual old partial site and solid cell. It supplies time
        // and actors; its older geometry must never overwrite the continued excavation owner.
        Assert.Equal(1UL, journey.SourceWorld.Excavation!.Sites.Single().CompletedWork);
        Assert.False(journey.SourceWorld.Space.Regions.Single(region => region.Bounds.Contains(originalSite.Cell))
            .GetCell(originalSite.Cell).IsExcavated);
        journey.Advance();
        AssertRejectedUnchanged(journey, journey.Plan(journey.Command()), WorldActionRejection.Unavailable);
    }

    [Fact]
    public void Completed_source_is_fully_retired_once_while_original_output_and_future_mining_survive_further_cuts()
    {
        var journey = new MaterialExcavationFixture(requiredWork: 1, secondSite: true);
        ulong initialStored = journey.Production.GetCombinedStoredQuantity(journey.Inventory, journey.Bag);
        ulong initialStorageRevision = journey.Inventory.Custody.GetStorage(journey.Bag).Revision;
        ulong initialCustodyRevision = journey.Inventory.Custody.Revision;
        journey.Cut();
        journey.Work();
        var original = journey.Extracted();
        EntityId firstCause = journey.NextId();
        EntityId ancestor = journey.Craft(IngredientLotKind.Source, original.LotId, 4, firstCause);
        Assert.Equal(new SourceInterval(original.Source.SourceId, 4, 8), journey.Extracted().Source);
        Assert.Equal(new ProductionOutputInterval(EntityId.DeriveSpawned(firstCause, 0), 0, 0, 4),
            journey.Production.Lots.Single(lot => lot.LotId == ancestor).Output);
        journey.Cut();

        EntityId secondCause = journey.NextId();
        EntityId second = journey.Craft(IngredientLotKind.Transformed, ancestor, 2, secondCause);
        EntityId remaining = journey.Craft(IngredientLotKind.Source, original.LotId, 4);
        Assert.DoesNotContain(journey.Inventory.Custody.Lots, lot => lot.Source.SourceId == original.Source.SourceId);
        Assert.Equal(new ProductionOutputInterval(EntityId.DeriveSpawned(firstCause, 0), 0, 2, 4),
            journey.Production.Lots.Single(lot => lot.LotId == ancestor).Output);
        journey.Cut();

        EntityId thirdCause = journey.NextId();
        EntityId third = journey.Craft(IngredientLotKind.Transformed, ancestor, 1, thirdCause);
        Assert.Equal(new ProductionOutputInterval(EntityId.DeriveSpawned(firstCause, 0), 0, 3, 4),
            journey.Production.Lots.Single(lot => lot.LotId == ancestor).Output);
        Assert.Equal(new ProductionOutputInterval(EntityId.DeriveSpawned(secondCause, 0), 0, 0, 2),
            journey.Production.Lots.Single(lot => lot.LotId == second).Output);
        Assert.Equal(new ProductionOutputInterval(EntityId.DeriveSpawned(thirdCause, 0), 0, 0, 1),
            journey.Production.Lots.Single(lot => lot.LotId == third).Output);
        Assert.Equal(4UL, journey.Production.Lots.Single(lot => lot.LotId == remaining).Quantity);
        var frontier = ProductionContinuationBasis.Capture(journey.Inventory, journey.Production);
        Assert.Equal(new SourceInterval(original.Source.SourceId, 0, 8),
            Assert.Single(frontier.RawRetirements, retirement => retirement.Source.SourceId == original.Source.SourceId).Source);
        Assert.Contains(journey.Production.Continuation!.Basis.BatchWitnesses,
            witness => witness.BatchId == EntityId.DeriveSpawned(firstCause, 0));

        journey.Work(SecondSiteId);
        Assert.All(journey.Excavation.Sites, site => Assert.True(site.IsComplete));
        Assert.All(journey.Excavation.Sites, site => Assert.True(Region(journey.State, site.Cell).GetCell(site.Cell).IsExcavated));
        Assert.Equal(new SourceInterval(SecondSourceId, 0, 8), journey.Extracted(SecondSiteId).Source);
        Assert.Equal(initialStored + 16, journey.Production.GetCombinedStoredQuantity(journey.Inventory, journey.Bag));
        Assert.Equal(initialStorageRevision + 6, journey.Inventory.Custody.GetStorage(journey.Bag).Revision);
        Assert.Equal(initialCustodyRevision + 6, journey.Inventory.Custody.Revision);
        Assert.Equal(6UL, journey.Production.TotalMaterialOperationCount);
        Assert.Equal(4UL, journey.Production.TotalBatchCount);
        Assert.Equal(0UL, journey.Production.TotalConsumedItemCount);
        Assert.Equal(0U, journey.Gathering.GetActorWork(journey.Actor).Effort);
        Assert.False(journey.Production.SupportsConsumption);

        var beforeConsumption = journey.State;
        var denied = journey.PlanConsume(IngredientLotKind.Transformed, ancestor);
        Assert.Equal(WorldActionRejection.Unavailable, denied.Rejection);
        Assert.Equal(beforeConsumption.Inventory.ContentHash, denied.CandidateInventory.ContentHash);
        Assert.Equal(beforeConsumption.Production.ContentHash, denied.CandidateProduction.ContentHash);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Mining_and_gathering_share_one_actor_opportunity_even_with_enough_effort_for_both(bool gatherFirst)
    {
        var journey = new MaterialExcavationFixture(requiredWork: 4);
        journey.Cut();
        journey.Advance(100);
        var first = gatherFirst ? journey.PlanGather(journey.GatherCommand()) : journey.Plan(journey.Command());
        journey.Accept(first);
        Assert.Equal(600U, journey.Gathering.GetActorWork(journey.Actor).Effort);
        Assert.Equal(9, (int)Assert.Single(journey.Production.MaterialHistory!.Operations).Kind);
        Assert.IsType<ExcavationWorkEvidence>(first.Evidence);
        var second = gatherFirst ? journey.Plan(journey.Command()) : journey.PlanGather(journey.GatherCommand());
        AssertRejectedUnchanged(journey, second, WorldActionRejection.RevisionConflict);
        journey.Cut();
        second = gatherFirst ? journey.Plan(journey.Command()) : journey.PlanGather(journey.GatherCommand());
        AssertRejectedUnchanged(journey, second, WorldActionRejection.RevisionConflict);

        journey.Advance(1);
        second = gatherFirst ? journey.Plan(journey.Command()) : journey.PlanGather(journey.GatherCommand());
        journey.Accept(second);
        Assert.Equal(210U, journey.Gathering.GetActorWork(journey.Actor).Effort);
        Assert.Equal(1UL, journey.Excavation.Sites.Single().CompletedWork);
        var node = journey.Gathering.GetNode(journey.GatherNodeId);
        var issued = Assert.Single(journey.Inventory.Custody.Lots, lot => lot.Source.SourceId == node.SourceId);
        Assert.Equal(new SourceInterval(node.SourceId, 0, node.NextUnissuedUnit), issued.Source);
        Assert.Equal(1UL, node.Revision);
        Assert.Equal(2UL, journey.Production.TotalMaterialOperationCount);
    }

    [Fact]
    public void A_fresh_second_actor_can_use_the_same_actual_global_frame_without_recovering_the_first_actors_effort()
    {
        var journey = new MaterialExcavationFixture(secondSite: true, secondActorOwnsSite: true);
        journey.Cut();
        journey.Advance();
        journey.Accept(journey.Plan(journey.Command()));
        ulong frame = journey.SourceWorld.ProcessIntervals;
        Assert.Equal(0U, journey.Gathering.GetActorWork(journey.Actor).Effort);
        Assert.Equal(400U, journey.Gathering.GetActorWork(journey.OtherActor).Effort);
        journey.Accept(journey.Plan(journey.Command(SecondSiteId)));
        Assert.All(journey.Excavation.Sites, site => Assert.Equal(1UL, site.CompletedWork));
        Assert.Equal(frame, journey.Gathering.AdvancingStep);
        Assert.Equal(0U, journey.Gathering.GetActorWork(journey.Actor).Effort);
        Assert.Equal(0U, journey.Gathering.GetActorWork(journey.OtherActor).Effort);
        Assert.Equal(2, journey.Production.MaterialHistory!.Operations.Count);
        AssertRejectedUnchanged(journey, journey.Plan(journey.Command(SecondSiteId)), WorldActionRejection.RevisionConflict);
    }

    [Fact]
    public void Older_donors_cannot_reset_the_global_frame_or_a_workers_spent_opportunity_after_a_cut()
    {
        var journey = new MaterialExcavationFixture(requiredWork: 4, secondSite: true, secondActorOwnsSite: true);
        journey.Cut();
        journey.Advance();
        var older = journey.SourceWorld;
        journey.Advance();
        journey.Accept(journey.Plan(journey.Command()));
        Assert.Equal(400U, journey.Gathering.GetActorWork(journey.Actor).Effort);
        journey.Cut();
        // This actor has never worked, but its otherwise eligible old frame precedes the
        // complete owner's observed frame and must not be used as a separate time source.
        AssertRejectedUnchanged(journey, journey.Plan(journey.Command(SecondSiteId), older), WorldActionRejection.RevisionConflict);
        journey.Accept(journey.Plan(journey.Command(SecondSiteId)));
        Assert.Equal(400U, journey.Gathering.GetActorWork(journey.Actor).Effort);
        Assert.Equal(400U, journey.Gathering.GetActorWork(journey.OtherActor).Effort);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Actual_but_unaccounted_donor_gathering_or_excavation_cannot_donate_changed_owners(bool mining)
    {
        var journey = new MaterialExcavationFixture(requiredWork: 1);
        journey.Cut();
        journey.Advance(80);
        var command = mining ? journey.Command() : journey.GatherCommand();
        var source = journey.SourceWorld;
        var batch = new WorldStepBatch(journey.NextId(), source.WorldId, source.BranchId, source.Revision,
            source.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [command], source.ProfileVersion);
        var actual = new WorldStepProcessor().Decide(source, batch, journey.Occupied);
        Assert.Equal(WorldActionRejection.None, Assert.Single(actual.Receipts).Rejection);
        Assert.NotEqual(source.Gathering!.ContentHash, actual.Candidate.Gathering!.ContentHash);
        AssertRejectedUnchanged(journey, journey.Plan(journey.Command(), actual.Candidate), WorldActionRejection.Unavailable);
        // The unchanged genuine donor remains valid: the refusal has spent neither effort nor rights.
        journey.Accept(journey.Plan(journey.Command()));
        Assert.Equal(new SourceInterval(journey.Excavation.Sites.Single().CountedSourceId, 0, 8), journey.Extracted().Source);
    }

    [Theory]
    [InlineData("effort")]
    [InlineData("held")]
    [InlineData("nonadvancing")]
    public void Work_requires_genuine_earned_effort_and_an_unheld_advancing_opportunity(string unavailable)
    {
        var journey = new MaterialExcavationFixture();
        journey.Cut();
        journey.Advance(unavailable == "effort" ? 1 : 40);
        var plan = journey.Plan(journey.Command(), advancing: unavailable != "nonadvancing", held: unavailable == "held");
        AssertRejectedUnchanged(journey, plan, WorldActionRejection.Unavailable);
        Assert.Equal(0U, journey.Gathering.GetActorWork(journey.Actor).Effort);
        Assert.Equal(0UL, journey.Excavation.Sites.Single().CompletedWork);
        Assert.Empty(journey.Production.MaterialHistory!.Operations);
    }

    [Fact]
    public void Unsupported_eventual_opening_prevents_even_partial_labor_and_leaves_the_real_effort_available()
    {
        var journey = new MaterialExcavationFixture(occupiedAbove: true);
        journey.Cut();
        journey.Advance();
        Assert.Equal(400U, journey.SourceWorld.Gathering!.GetActorWork(journey.Actor).Effort);
        AssertRejectedUnchanged(journey, journey.Plan(journey.Command()), WorldActionRejection.Unavailable);
        Assert.Equal(0UL, journey.Excavation.Sites.Single().CompletedWork);
        Assert.DoesNotContain(journey.Inventory.Custody.Lots, lot => lot.Source.SourceId == journey.Excavation.Sites.Single().CountedSourceId);
        Assert.False(Region(journey.State, ExcavationWorldFixture.Target).GetCell(ExcavationWorldFixture.Target).IsExcavated);
        journey.Accept(journey.PlanGather(journey.GatherCommand()));
        Assert.Equal(0U, journey.Gathering.GetActorWork(journey.Actor).Effort);
        Assert.Equal(1UL, journey.Gathering.GetNode(journey.GatherNodeId).Revision);
    }

    [Fact]
    public void Permanent_history_is_required_and_archived_partial_commands_cannot_be_reused()
    {
        var journey = new MaterialExcavationFixture(requiredWork: 4);
        journey.Cut();
        journey.Advance(100);
        var archived = journey.Command();
        Assert.Throws<ArgumentNullException>(() => new MaterialExcavationPlanner().Plan(journey.SourceWorld,
            journey.State, archived, true, false, null!));
        Assert.Throws<ArgumentNullException>(() => new MaterialExcavationPlanner().PlanGather(journey.SourceWorld,
            journey.State, journey.GatherCommand(), null!));
        journey.Accept(journey.Plan(archived));
        journey.Cut();
        journey.Advance(1);
        Assert.True(journey.Occupied(archived.CommandId));
        AssertRejectedUnchanged(journey, journey.Plan(journey.Command(commandId: archived.CommandId)), WorldActionRejection.Unavailable);
        journey.Accept(journey.Plan(journey.Command()));
        Assert.Equal(2UL, journey.Excavation.Sites.Single().CompletedWork);
        Assert.Equal(210U, journey.Gathering.GetActorWork(journey.Actor).Effort);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Historical_completion_command_or_issued_lot_collision_rolls_back_every_work_owner(bool lotCollision)
    {
        var journey = new MaterialExcavationFixture(requiredWork: 1, secondSite: true);
        journey.Cut();
        journey.Advance();
        var old = journey.Command();
        journey.Accept(journey.Plan(old));
        EntityId issued = journey.Extracted().LotId;
        journey.Craft(IngredientLotKind.Source, issued, 4);
        journey.Craft(IngredientLotKind.Source, issued, 4);
        journey.Cut();
        Assert.DoesNotContain(journey.Inventory.Custody.Lots, lot => lot.LotId == issued);
        journey.Advance();
        var repeated = journey.Command(SecondSiteId, old.CommandId);
        // Isolate the two permanent identity checks. The lot-only callback deliberately omits
        // the old command so the archive's no-longer-live issued identity must independently refuse.
        bool Historical(EntityId identity) => lotCollision
            ? identity == issued || identity != old.CommandId && journey.Occupied(identity)
            : journey.Occupied(identity);
        AssertRejectedUnchanged(journey, journey.Plan(repeated, occupied: Historical), WorldActionRejection.Unavailable);
        Assert.False(journey.Excavation.Sites.Single(site => site.SiteId == SecondSiteId).IsComplete);
        journey.Accept(journey.Plan(journey.Command(SecondSiteId)));
        Assert.Equal(new SourceInterval(SecondSourceId, 0, 8), journey.Extracted(SecondSiteId).Source);
    }

    [Fact]
    public void A_real_511_transfer_window_admits_work_512_then_cuts_without_regranting_spent_work_time()
    {
        var journey = new MaterialExcavationFixture(requiredWork: 4);
        journey.Cut();
        var initialInventory = journey.Inventory;
        var moving = initialInventory.Custody.Lots.First(lot => lot.CustodianId == journey.Bag && lot.Quantity > 1
            && lot.ReservedBy is null && lot.Protection == LotProtection.None);
        EntityId movable = moving.LotId;
        for (int index = 0; index < 511; index++)
            movable = journey.Transfer(movable, index % 2 == 0 ? journey.OtherBag : journey.Bag, 1);
        Assert.Equal(511, journey.Production.MaterialHistory!.Operations.Count);
        Assert.All(journey.Production.MaterialHistory.Operations, operation => Assert.Equal(MaterialOperationKind.Transfer, operation.Kind));
        Assert.Equal(new SourceInterval(moving.Source.SourceId, moving.Source.Start, moving.Source.Start + 1),
            journey.Inventory.Custody.GetLot(movable).Source);
        Assert.Equal(journey.OtherBag, journey.Inventory.Custody.GetLot(movable).CustodianId);
        Assert.Equal(initialInventory.Custody.Revision + 511, journey.Inventory.Custody.Revision);
        Assert.Equal(initialInventory.Custody.GetStorage(journey.Bag).Revision + 511,
            journey.Inventory.Custody.GetStorage(journey.Bag).Revision);
        Assert.Equal(initialInventory.Custody.GetStorage(journey.OtherBag).Revision + 511,
            journey.Inventory.Custody.GetStorage(journey.OtherBag).Revision);

        journey.Advance(100);
        var usedFrame = journey.SourceWorld;
        var beforeWork = journey.Inventory.ContentHash;
        journey.Accept(journey.Plan(journey.Command()));
        Assert.Equal(512, journey.Production.MaterialHistory!.Operations.Count);
        Assert.Equal(9, (int)journey.Production.MaterialHistory.Operations[^1].Kind);
        Assert.Equal(1UL, journey.Excavation.Sites.Single().CompletedWork);
        Assert.Equal(600U, journey.Gathering.GetActorWork(journey.Actor).Effort);
        Assert.Equal(beforeWork, journey.Inventory.ContentHash);
        journey.Advance(1);
        AssertRejectedUnchanged(journey, journey.Plan(journey.Command()), WorldActionRejection.Unavailable);
        Assert.Equal(600U, journey.Gathering.GetActorWork(journey.Actor).Effort);
        Assert.False(Region(journey.State, ExcavationWorldFixture.Target).GetCell(ExcavationWorldFixture.Target).IsExcavated);
        journey.Cut();
        AssertRejectedUnchanged(journey, journey.Plan(journey.Command(), usedFrame), WorldActionRejection.RevisionConflict);
        journey.Accept(journey.Plan(journey.Command()));
        Assert.Equal(513UL, journey.Production.TotalMaterialOperationCount);
        Assert.Single(journey.Production.MaterialHistory!.Operations);
        Assert.Equal(2UL, journey.Excavation.Sites.Single().CompletedWork);
        Assert.Equal(210U, journey.Gathering.GetActorWork(journey.Actor).Effort);
        Assert.Equal(beforeWork, journey.Inventory.ContentHash);
    }

    [Fact]
    public void Plans_pin_the_complete_owner_and_rejected_candidates_do_not_replace_it()
    {
        var journey = new MaterialExcavationFixture();
        var initial = journey.State;
        var cut = initial.PlanContinue(TestContext.Current.CancellationToken);
        journey.Cut();
        Assert.Throws<InvalidOperationException>(() => cut.ApplyTo(journey.State));
        journey.Advance(100);
        var stale = journey.Plan(journey.Command());
        journey.Accept(journey.Plan(journey.Command()));
        Assert.Throws<InvalidOperationException>(() => stale.ApplyTo(journey.State));
        Assert.Equal(1UL, journey.Excavation.Sites.Single().CompletedWork);
        Assert.Equal(600U, journey.Gathering.GetActorWork(journey.Actor).Effort);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => journey.State.PlanContinue(canceled.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Recapturing_a_donor_cannot_attach_newer_partial_work_production_even_when_inventory_is_identical(bool latestDonor)
    {
        var journey = new MaterialExcavationFixture();
        journey.Cut();
        journey.Work();
        journey.Cut();
        var recaptured = MaterialExcavationState.Capture(latestDonor ? journey.SourceWorld : journey.InitialWorld);
        Assert.Equal(journey.Inventory.ContentHash, recaptured.Inventory.ContentHash);
        Assert.Equal(1UL, journey.Excavation.Sites.Single().CompletedWork);
        Assert.Equal(0UL, recaptured.Excavation.Sites.Single().CompletedWork);
        Assert.Throws<ArgumentException>(() => recaptured.PlanReconcile(journey.Inventory, journey.Production, TestContext.Current.CancellationToken));
        var restarted = recaptured.PlanContinue(TestContext.Current.CancellationToken).ApplyTo(recaptured);
        var originalHash = restarted.ContentHash;
        Assert.Throws<ArgumentException>(() => restarted.PlanReconcile(journey.Inventory, journey.Production, TestContext.Current.CancellationToken));
        Assert.Equal(originalHash, restarted.ContentHash);
        Assert.Equal(0UL, restarted.Production.TotalMaterialOperationCount);
        journey.Work();
        Assert.Equal(2UL, journey.Excavation.Sites.Single().CompletedWork);
        Assert.Equal(2UL, journey.Production.TotalMaterialOperationCount);
    }

    [Fact]
    public void Genuine_donor_movement_invalidates_old_actor_pins_and_rechecks_reach_for_a_fresh_intent()
    {
        var journey = new MaterialExcavationFixture();
        journey.Cut();
        journey.Advance();
        var old = journey.Command();
        var origin = journey.SourceWorld.Space.GetActor(journey.Actor);
        var delivery = ConstructionUpgradeFixture.Delivery(journey.SourceWorld, journey.Actor);
        journey.Advance(16, [new WorldMovementInput(delivery, journey.Actor, -32, 0)]);
        var away = journey.SourceWorld.Space.GetActor(journey.Actor);
        Assert.Equal(origin.Feet.Offset(-512, 0, 0), away.Feet);
        Assert.True(away.Revision > origin.Revision);
        AssertRejectedUnchanged(journey, journey.Plan(old), WorldActionRejection.RevisionConflict);
        AssertRejectedUnchanged(journey, journey.Plan(journey.Command()), WorldActionRejection.OutOfReach);
        journey.Advance(16, [new WorldMovementInput(delivery, journey.Actor, 32, 0)]);
        Assert.Equal(origin.Feet, journey.SourceWorld.Space.GetActor(journey.Actor).Feet);
        journey.Accept(journey.Plan(journey.Command()));
        Assert.Equal(1UL, journey.Excavation.Sites.Single().CompletedWork);
        Assert.Equal(journey.SourceWorld.Space.GetActor(journey.Actor).Revision, journey.Space.GetActor(journey.Actor).Revision);
    }

    [Fact]
    public void Donor_pose_supported_only_by_the_old_floor_cannot_be_overlaid_on_a_retained_hole()
    {
        var journey = new MaterialExcavationFixture(requiredWork: 1, secondSite: true, upperDonorActor: true);
        journey.Cut();
        journey.Work();
        Assert.True(Region(journey.State, ExcavationWorldFixture.Target).GetCell(ExcavationWorldFixture.Target).IsExcavated);
        journey.Advance();
        Assert.Equal(WorldActionRejection.None, journey.Plan(journey.Command(SecondSiteId)).Rejection);
        var delivery = ConstructionUpgradeFixture.Delivery(journey.SourceWorld, journey.OtherActor);
        journey.Advance(8, [new WorldMovementInput(delivery, journey.OtherActor, 32, 0)]);
        Assert.Equal(new SubunitPosition(128, 384, 256), journey.SourceWorld.Space.GetActor(journey.OtherActor).Feet);
        Assert.False(journey.SourceWorld.Space.Regions.Single(region => region.Bounds.Contains(ExcavationWorldFixture.Target))
            .GetCell(ExcavationWorldFixture.Target).IsExcavated);
        AssertRejectedUnchanged(journey, journey.Plan(journey.Command(SecondSiteId)), WorldActionRejection.Unavailable);
        Assert.Equal(new SubunitPosition(-128, 384, 256), journey.Space.GetActor(journey.OtherActor).Feet);
        Assert.Equal(0UL, journey.Excavation.Sites.Single(site => site.SiteId == SecondSiteId).CompletedWork);
        Assert.True(Region(journey.State, ExcavationWorldFixture.Target).GetCell(ExcavationWorldFixture.Target).IsExcavated);
    }

    [Theory]
    [InlineData("load")]
    [InlineData("liquid")]
    [InlineData("care")]
    public void Actual_unsupported_owner_profiles_are_refused_at_capture(string owner)
    {
        WorldSnapshot source = owner switch
        {
            "load" => new StructuralSettlementWorldFixture(admitSettlement: false).Genesis,
            "liquid" => new TerrainLiquidBudgetFixture(8_192).CreateFreshGenesis(),
            "care" => CareJourneyAdmission.CreateEmbedded("excavation-capture-refusal", "Traveler"),
            _ => throw new ArgumentOutOfRangeException(nameof(owner))
        };
        Assert.NotNull(source.Excavation);
        if (owner == "load") Assert.NotNull(source.StructuralPolicy);
        if (owner == "liquid") Assert.NotNull(source.LiquidCells);
        if (owner == "care") Assert.NotNull(source.Care);
        var before = source.ContentHash;
        Assert.Throws<ArgumentException>(() => MaterialExcavationState.Capture(source));
        Assert.Equal(before, source.ContentHash);
    }

    [Fact]
    public void An_actual_reading_hold_is_not_a_capturable_work_source()
    {
        var fixture = new ExcavationWorldFixture();
        var world = fixture.Earn(fixture.Genesis);
        var other = world.Residents.Single(resident => resident.ActorId != fixture.Player).ActorId;
        var delivery = ConstructionUpgradeFixture.Delivery(world, fixture.Player);
        var command = new WorldActionCommand(fixture.Actions.NextIdentity(), delivery.PrincipalId, delivery.SessionId,
            fixture.Player, world.WorldId, world.BranchId, world.Space.GetActor(fixture.Player).Revision,
            new SpeakToActorIntent(other, world.Space.GetActor(other).Revision));
        var decision = fixture.Step(world, command);
        Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
        Assert.True(decision.Candidate.HasReadingHold);
        Assert.Throws<ArgumentException>(() => MaterialExcavationState.Capture(decision.Candidate));
        Assert.Equal(world.Excavation!.ContentHash, decision.Candidate.Excavation!.ContentHash);
    }

    [Fact]
    public void Actual_pending_excavation_cannot_be_captured_as_a_completed_material_boundary()
    {
        var fixture = new StructuralSettlementWorldFixture(consequenceWorkBudget: 1);
        using var files = new StorageFiles();
        var before = fixture.PrepareBeforeExcavation(files.Path);
        using var authority = SqliteWorldAuthority.OpenExisting(files.Path);
        var started = authority.Commit(fixture.NextExcavation(before));
        Assert.Empty(started.Receipts);
        Assert.NotNull(started.Candidate.PendingExcavation);
        Assert.Equal(0UL, started.Candidate.Excavation!.Sites.Single().CompletedWork);
        var persisted = authority.InspectForHost().State;
        Assert.Equal(started.Candidate.ContentHash, persisted.ContentHash);
        Assert.Throws<ArgumentException>(() => MaterialExcavationState.Capture(persisted));
        Assert.Equal(persisted.ContentHash, authority.InspectForHost().State.ContentHash);
    }

    [Fact]
    public void A_lower_craft_candidate_cannot_publish_a_batch_identity_owned_by_the_excavation_site()
    {
        EntityId cause = Id(900_000);
        EntityId site = EntityId.DeriveSpawned(cause, 0);
        var journey = new MaterialExcavationFixture(requiredWork: 1, siteId: site);
        journey.Cut();
        journey.Work();
        var raw = journey.Extracted();
        Assert.True(journey.InitialWorld.IdentityIndex.Contains(site));
        Assert.False(journey.Inventory.ContainsIdentity(site));
        Assert.False(journey.Production.ContainsIdentity(site));
        Assert.False(journey.Occupied(site));
        var recipe = journey.Production.GetRecipe(Id(21));
        var lower = new InstantCraftPlanner().Plan(journey.Inventory, journey.Production,
            ConstructionUpgradeFixture.Delivery(journey.SourceWorld, journey.Actor), journey.Actor, cause,
            new(recipe.DefinitionId, recipe.Version, journey.Production.Revision, journey.Bag,
                journey.Inventory.Custody.GetStorage(journey.Bag).Revision,
                [new(IngredientLotKind.Source, raw.LotId, raw.Revision, 4)]), journey.Occupied);
        Assert.Equal(WorldActionRejection.None, lower.Rejection);
        Assert.Equal(site, Assert.Single(lower.CandidateProduction.Batches).BatchId);
        Assert.Contains(site, lower.CreatedIds);
        var before = journey.State.ContentHash;
        Assert.Throws<ArgumentException>(() => journey.State.PlanReconcile(lower.CandidateInventory,
            lower.CandidateProduction, TestContext.Current.CancellationToken));
        Assert.Equal(before, journey.State.ContentHash);
        Assert.Equal(new SourceInterval(raw.Source.SourceId, 0, 8), journey.Extracted().Source);
        Assert.Empty(journey.Production.Batches);
    }

    [Fact]
    public void A_split_lot_alias_consumed_before_reconciliation_still_cannot_steal_the_excavation_sites_identity()
    {
        EntityId transferCause = Id(900_010);
        EntityId site = EntityId.DeriveSpawned(transferCause, 0);
        var journey = new MaterialExcavationFixture(requiredWork: 1, siteId: site);
        journey.Cut();
        journey.Work();
        var raw = journey.Extracted();
        var delivery = ConstructionUpgradeFixture.Delivery(journey.SourceWorld, journey.Actor);
        var transfer = new CommandEnvelope(transferCause, delivery.PrincipalId, delivery.SessionId, journey.Actor,
            journey.Inventory.WorldId, journey.Inventory.BranchId,
            [new(RevisionTargetKind.Branch, journey.Inventory.BranchId, journey.Inventory.Custody.Revision),
                new(RevisionTargetKind.Entity, raw.LotId, raw.Revision),
                new(RevisionTargetKind.Entity, journey.Bag, journey.Inventory.Custody.GetStorage(journey.Bag).Revision),
                new(RevisionTargetKind.Entity, journey.OtherBag, journey.Inventory.Custody.GetStorage(journey.OtherBag).Revision)],
            new(raw.LotId, journey.Bag, journey.OtherBag, 4));
        var moved = new MaterialTransferPlanner().Plan(journey.Inventory, journey.Production,
            transfer, delivery, journey.Occupied);
        Assert.Equal(WorldActionRejection.None, moved.Rejection);
        var alias = moved.CandidateInventory.Custody.GetLot(site);
        Assert.Equal(new SourceInterval(raw.Source.SourceId, 0, 4), alias.Source);
        Assert.Equal(journey.OtherBag, alias.CustodianId);
        var recipe = moved.CandidateProduction.GetRecipe(Id(21));
        var crafted = new InstantCraftPlanner().Plan(moved.CandidateInventory, moved.CandidateProduction,
            ConstructionUpgradeFixture.Delivery(journey.SourceWorld, journey.OtherActor), journey.OtherActor, Id(900_011),
            new(recipe.DefinitionId, recipe.Version, moved.CandidateProduction.Revision, journey.OtherBag,
                moved.CandidateInventory.Custody.GetStorage(journey.OtherBag).Revision,
                [new(IngredientLotKind.Source, site, alias.Revision, 4)]), journey.Occupied);
        Assert.Equal(WorldActionRejection.None, crafted.Rejection);
        Assert.DoesNotContain(crafted.CandidateInventory.Custody.Lots, lot => lot.LotId == site);
        Assert.DoesNotContain(crafted.CandidateProduction.Lots, lot => lot.LotId == site);
        Assert.Equal(site, Assert.Single(Assert.Single(crafted.CandidateProduction.Batches).Ingredients).LotId);
        Assert.Equal(3, crafted.CandidateProduction.MaterialHistory!.Operations.Count);
        var before = journey.State.ContentHash;
        Assert.Throws<ArgumentException>(() => journey.State.PlanReconcile(crafted.CandidateInventory,
            crafted.CandidateProduction, TestContext.Current.CancellationToken));
        Assert.Equal(before, journey.State.ContentHash);
        Assert.Equal(new SourceInterval(raw.Source.SourceId, 0, 8), journey.Extracted().Source);
        Assert.Empty(journey.Production.Batches);
    }

    [Fact]
    public void Final_mining_counts_derived_storage_and_failed_capacity_preserves_the_same_work_opportunity()
    {
        var journey = new MaterialExcavationFixture(requiredWork: 1, secondSite: true, primaryExtraCapacity: 12);
        journey.Cut();
        journey.Work();
        var raw = journey.Extracted();
        journey.Craft(IngredientLotKind.Source, raw.LotId, 4);
        var storage = journey.Inventory.Custody.GetStorage(journey.Bag);
        Assert.Equal(8UL, storage.UnitCapacity - journey.Inventory.Custody.GetStoredQuantity(journey.Bag));
        Assert.Equal(4UL, journey.Production.GetStoredQuantity(journey.Bag));
        Assert.Equal(4UL, storage.UnitCapacity - journey.Production.GetCombinedStoredQuantity(journey.Inventory, journey.Bag));
        journey.Advance();
        ulong availableFrame = journey.SourceWorld.ProcessIntervals;
        AssertRejectedUnchanged(journey, journey.Plan(journey.Command(SecondSiteId)), WorldActionRejection.Unavailable);
        Assert.Equal(0UL, journey.Excavation.Sites.Single(site => site.SiteId == SecondSiteId).CompletedWork);
        Assert.False(Region(journey.State, SecondCell).GetCell(SecondCell).IsExcavated);
        Assert.Equal(new SourceInterval(raw.Source.SourceId, 4, 8), journey.Extracted().Source);
        Assert.Equal(0U, journey.Gathering.GetActorWork(journey.Actor).Effort);

        journey.Transfer(raw.LotId, journey.OtherBag, 4);
        Assert.Equal(availableFrame, journey.SourceWorld.ProcessIntervals);
        journey.Accept(journey.Plan(journey.Command(SecondSiteId)));
        Assert.Equal(new SourceInterval(SecondSourceId, 0, 8), journey.Extracted(SecondSiteId).Source);
        Assert.Equal(storage.UnitCapacity, journey.Production.GetCombinedStoredQuantity(journey.Inventory, journey.Bag));
        Assert.True(Region(journey.State, SecondCell).GetCell(SecondCell).IsExcavated);
        Assert.Equal(availableFrame, journey.Gathering.AdvancingStep);
        Assert.Equal(0U, journey.Gathering.GetActorWork(journey.Actor).Effort);
    }

    [Fact]
    public void Joined_gather_uses_the_retained_mined_opening_even_while_the_actual_donor_still_has_solid_rock()
    {
        var journey = new MaterialExcavationFixture(requiredWork: 1, gatherBehindTarget: true);
        journey.Cut();
        var node = journey.Gathering.GetNode(journey.GatherNodeId);
        Assert.NotEqual(SpatialInteraction.Reachable, ResourceInteractionQueries.Query(journey.SourceWorld.Space, journey.Actor, node.Feet));
        journey.Work();
        Assert.Equal(SpatialInteraction.Reachable, ResourceInteractionQueries.Query(journey.Space, journey.Actor, node.Feet));
        Assert.NotEqual(SpatialInteraction.Reachable, ResourceInteractionQueries.Query(journey.SourceWorld.Space, journey.Actor, node.Feet));
        journey.Advance();
        var command = journey.GatherCommand();
        var donorOnly = new GatherPlanner().Plan(journey.SourceWorld, journey.SourceWorld.Inventory,
            journey.SourceWorld.Gathering!, command);
        Assert.Equal(WorldActionRejection.OutOfReach, donorOnly.Rejection);
        var joined = journey.PlanGather(command);
        journey.Accept(joined);
        var issued = journey.Inventory.Custody.GetLot(EntityId.DeriveSpawned(command.CommandId, 0));
        var after = journey.Gathering.GetNode(node.NodeId);
        Assert.Equal(new SourceInterval(node.SourceId, node.NextUnissuedUnit, after.NextUnissuedUnit), issued.Source);
        Assert.Equal(node.Revision + 1, after.Revision);
        Assert.True(Region(journey.State, ExcavationWorldFixture.Target).GetCell(ExcavationWorldFixture.Target).IsExcavated);
        Assert.False(journey.SourceWorld.Space.Regions.Single(region => region.Bounds.Contains(ExcavationWorldFixture.Target))
            .GetCell(ExcavationWorldFixture.Target).IsExcavated);
        Assert.Equal(0U, journey.Gathering.GetActorWork(journey.Actor).Effort);
        Assert.Equal(2UL, journey.Production.TotalMaterialOperationCount);
    }

    private static TerrainRegionSnapshot Region(MaterialExcavationState state, CellCoordinate cell) =>
        state.Space.Regions.Single(region => region.Bounds.Contains(cell));

    private static void AssertRejectedUnchanged(MaterialExcavationFixture journey, MaterialExcavationPlan plan,
        WorldActionRejection rejection)
    {
        Assert.Equal(rejection, plan.Rejection);
        Assert.False(plan.Succeeded);
        Assert.Empty(plan.CreatedIds);
        Assert.Null(plan.Evidence);
        Assert.Same(journey.State, plan.Candidate);
        Assert.Equal(journey.Inventory.ContentHash, plan.Candidate.Inventory.ContentHash);
        Assert.Equal(journey.Production.ContentHash, plan.Candidate.Production.ContentHash);
        Assert.Equal(journey.Gathering.ContentHash, plan.Candidate.Gathering.ContentHash);
        Assert.Equal(journey.Excavation.ContentHash, plan.Candidate.Excavation.ContentHash);
        Assert.Equal(journey.Space.Regions.Select(region => region.ComputeContentHash()),
            plan.Candidate.Space.Regions.Select(region => region.ComputeContentHash()));
    }
}
