using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class WatercourseFarmContentTests
{
    [Theory]
    [InlineData("aldermere-watercourse-v1")]
    [InlineData("watercourse-second-seed")]
    [InlineData("channel-03")]
    public void Fresh_authored_channel_is_canonical_sealed_and_preserves_the_real_farm(string seed)
    {
        WorldSnapshot world = WatercourseFarmAdmission.CreateEmbedded(seed, "Traveler");
        byte[] bytes = WorldSnapshotCodec.Encode(world);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WatercourseFarmAdmission.CreateEmbedded(seed, "Traveler")));
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));
        WorldSnapshot farm = ConstructionFarmAdmission.CreateGenesis(new ConstructionUpgradeFixture().QuestGenesis(seed));
        byte[] farmBytes = WorldSnapshotCodec.Encode(farm);
        Assert.Equal((ushort)20, world.ProfileVersion);
        Assert.Equal(0UL, world.Revision);
        Assert.Equal(0UL, world.AdvancingSteps);
        Assert.Equal(farm.Calendar, world.Calendar);
        Assert.NotEqual(farm.AdmissionVersion, world.AdmissionVersion);
        Assert.Equal(farm.Gathering!.ContentHash, world.Gathering!.ContentHash);
        Assert.Equal(farm.Production!.ContentHash, world.Production!.ContentHash);
        Assert.Equal(farm.Progression!.ContentHash, world.Progression!.ContentHash);
        Assert.Equal(farm.Quests!.ContentHash, world.Quests!.ContentHash);
        Assert.Equal(farm.Construction!.ContentHash, world.Construction!.ContentHash);
        Assert.Empty(world.Production.Batches);
        Assert.Empty(world.Quests.Participations);
        Assert.False(Assert.Single(world.Construction.Projects).IsCompleted);
        Assert.Empty(world.Space.Structures);
        foreach (var resident in farm.Residents)
        {
            var actual = world.GetResident(resident.ActorId);
            Assert.Equal(resident.InventoryCustodianId, actual.InventoryCustodianId);
            Assert.Equal(resident.HomeFeet, actual.HomeFeet);
            Assert.Equal(resident.ExperiencedTime, actual.ExperiencedTime);
            Assert.Equal(farm.Space.GetActor(resident.ActorId).Feet, world.Space.GetActor(resident.ActorId).Feet);
            Assert.Equal(farm.Inventory.Custody.GetStorage(resident.InventoryCustodianId),
                world.Inventory.Custody.GetStorage(resident.InventoryCustodianId));
        }
        Assert.All(farm.Inventory.Custody.Lots, lot => Assert.Contains(lot, world.Inventory.Custody.Lots));
        Assert.All(farm.Inventory.Sources, source => Assert.Contains(source, world.Inventory.Sources));
        Assert.Equal(farm.Inventory.AdmittedUnitCapacity + WatercourseFarmLayout.InitialWaterUnits,
            world.Inventory.AdmittedUnitCapacity);
        var reservoir = Assert.Single(world.Reservoirs!.Geometries);
        Assert.Equal(13, reservoir.Columns.Count);
        Assert.Equal(20, world.LiquidCells!.DomainCells.Count);
        Assert.Equal(6, world.LiquidCells.Portions.Count);
        Assert.Equal(25UL, world.LiquidCells.Profile.DownwardUnitsPerFace);
        Assert.Equal(4UL, world.LiquidCells.Profile.LateralUnitsPerFace);
        Assert.Empty(world.Reservoirs.Capacity.Pending);
        Assert.Single(world.Reservoirs.Capacity.Closed);
        AssertConserved(world.LiquidCells);
        var waterLot = Assert.Single(world.Inventory.Custody.Lots, lot => lot.CustodianId == reservoir.CustodianId);
        Assert.Equal(0UL, waterLot.Source.Start);
        Assert.Equal(WatercourseFarmLayout.InitialWaterUnits, waterLot.Source.End);
        Assert.DoesNotContain(world.Residents, resident => resident.InventoryCustodianId == reservoir.CustodianId);
        Assert.Single(world.Space.Regions.Select(region => region.Seed).Distinct());
        Assert.Single(world.Space.Regions.Select(region => region.Rules.Version).Distinct());
        Assert.Equal(20, world.Space.Regions.Sum(region => region.Cells.Count(cell => cell.IsExcavated)));
        Assert.All(world.LiquidCells.DomainCells, cell => Assert.False(Cell(world, cell.Coordinate).IsSolid));
        foreach (ReservoirColumn column in reservoir.Columns)
        {
            Assert.True(Cell(world, new(column.X, column.Y, column.BottomZ - 1)).IsSolid);
        }
        var containment = new ReservoirBoundaryEvaluator().Assess(world, world.ReservoirSeals!,
            TestContext.Current.CancellationToken);
        Assert.True(containment.Completed);
        Assert.True(containment.IsContained);
        Assert.Equal(0L, Assert.Single(containment.Results).ConstructedSealedFaces);
        SubunitPosition previousBank = world.Space.GetActor(Player(world)).Feet;
        foreach (SubunitPosition bank in new[] { WatercourseFarmLayout.HeadBank,
            WatercourseFarmLayout.NorthBank, WatercourseFarmLayout.PoolBank })
        {
            Assert.Equal(SpatialOccupancy.Clear, world.Space.QueryOccupancy(Player(world), bank));
            Assert.Equal(GroundRouteStatus.Reachable, world.Space.QueryGroundRoute(Player(world), previousBank, bank).Status);
            previousBank = bank;
        }
        Assert.Equal(farmBytes, WorldSnapshotCodec.Encode(ConstructionFarmAdmission.CreateGenesis(
            new ConstructionUpgradeFixture().QuestGenesis(seed))));
    }

    [Fact]
    public void Finite_head_stock_still_moves_after_one_minute_of_selected_local_steps_without_refill()
    {
        WorldSnapshot world = WatercourseFarmAdmission.CreateEmbedded("watercourse-duration", "Traveler");
        LiquidCellState initial = world.LiquidCells!;
        LiquidCellState state = initial;
        var planner = new LiquidCellPlanner();
        // 1,200 selected 50ms local steps. This tests the authored calibration, not elapsed machine time.
        for (int step = 0; step < 1200; step++)
        {
            state = planner.PlanAdvance(state, state.Boundary.AddMilliseconds(1800)).ApplyTo(state);
        }
        AssertConserved(state);
        Assert.Equal(initial.ReservoirHash, state.ReservoirHash);
        Assert.Equal(1200UL, state.Revision);
        Assert.Contains(state.Quantities, pair => pair.Key.Coordinate.Z == -1 && pair.Value > 0);
        // The sole upper-to-lower face passes at most four litres each step, so at least 1,200
        // litres remain above the drop after 1,200 steps, independently of planner allocation order.
        ulong upper = state.Quantities.Where(pair => pair.Key.Coordinate.X < 116)
            .Aggregate(0UL, (sum, pair) => checked(sum + pair.Value));
        Assert.InRange(upper, 1200UL, WatercourseFarmLayout.InitialWaterUnits - 1);
        LiquidCellState next = planner.PlanAdvance(state, state.Boundary.AddMilliseconds(1800)).ApplyTo(state);
        Assert.False(state.Portions.SequenceEqual(next.Portions));
        AssertConserved(next);
        Assert.Equal(initial.ContentHash, world.LiquidCells!.ContentHash);
    }

    [Fact]
    public void Ordinary_walk_dialogue_funded_work_and_flow_share_one_exact_durable_world()
    {
        var actions = new ConstructionUpgradeFixture();
        WorldSnapshot genesis = WatercourseFarmAdmission.CreateEmbedded("watercourse-journey", "Traveler");
        EntityId player = Player(genesis);
        EntityId builder = genesis.Quests!.Definitions.Single().GiverActorId;
        using var files = new StorageFiles();
        WorldSnapshot final;
        WorldSnapshot funded;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            WorldSnapshot world = genesis;
            for (int step = 0; world.Space.QueryInteraction(player, builder) != SpatialInteraction.Reachable; step++)
            {
                Assert.True(step < 128);
                SubunitPosition feet = world.Space.GetActor(player).Feet;
                SubunitPosition target = world.Space.GetActor(builder).Feet;
                long dx = Math.Sign(target.X - feet.X) * 32;
                long dy = dx == 0 ? Math.Sign(target.Y - feet.Y) * 32 : 0;
                world = authority.Commit(actions.Batch(world, [], [new(ConstructionUpgradeFixture.Delivery(world, player), player, dx, dy)])).Candidate;
            }
            WorldSnapshot beforeHold = world;
            world = actions.Act(authority, world, player, new SpeakToActorIntent(builder, world.Space.GetActor(builder).Revision));
            Assert.True(world.HasReadingHold);
            Assert.Equal(beforeHold.Calendar.AddMilliseconds(1800), world.Calendar);
            Assert.Equal(beforeHold.LiquidCells!.Portions, world.LiquidCells!.Portions);
            var quest = world.Quests!.Definitions.Single();
            world = actions.Act(authority, world, player, new AcceptDeliveryQuestIntent(quest.DefinitionId, quest.Version,
                builder, world.Space.GetActor(builder).Revision, 0, world.Progression!.GetActor(player).Revision));
            EntityId bag = world.GetResident(player).InventoryCustodianId;
            var wood = world.Inventory.Custody.Lots.Single(lot => lot.CustodianId == bag && lot.Definition == quest.Item);
            WorldSnapshot held = world;
            world = actions.Act(authority, world, player, new CompleteDeliveryQuestIntent(quest.DefinitionId, quest.Version,
                builder, world.Space.GetActor(builder).Revision, 1, world.Progression!.GetActor(player).Revision,
                world.Inventory.Custody.GetStorage(bag).Revision, [new(wood.LotId, wood.Revision, quest.Quantity)]));
            Assert.Equal(ActorQuestStatus.Completed, Assert.Single(world.Quests!.Participations).Status);
            Assert.Equal(held.Calendar, world.Calendar);
            Assert.Equal(held.AdvancingSteps, world.AdvancingSteps);
            Assert.Equal(held.LiquidCells!.Portions, world.LiquidCells!.Portions);
            Assert.Equal(6UL, world.Inventory.Custody.GetStoredQuantity(world.Construction!.Projects.Single().ProjectCustodianId));
            funded = world;
            world = actions.Act(authority, world, player, new CloseDialogueIntent(world.Dialogues.Single().DialogueId));
            Assert.Equal(funded.Calendar, world.Calendar);
            Assert.Equal(funded.LiquidCells!.Portions, world.LiquidCells!.Portions);
            var expectedFlow = new LiquidCellPlanner().PlanAdvance(world.LiquidCells, world.Calendar.AddMilliseconds(1800))
                .ApplyTo(world.LiquidCells);
            world = authority.Commit(actions.Batch(world, [], [])).Candidate;
            Assert.Equal(expectedFlow.Portions, world.LiquidCells!.Portions);

            // Actual ordinary samples reach both banks via dry ground and remain outside the water.
            world = Walk(authority, actions, world, WatercourseFarmLayout.HeadBank);
            world = Walk(authority, actions, world, WatercourseFarmLayout.NorthBank);
            world = Walk(authority, actions, world, WatercourseFarmLayout.PoolBank);
            WorldSnapshot atPool = world;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                world = authority.Commit(actions.Batch(world, [],
                    [new(ConstructionUpgradeFixture.Delivery(world, player), player, -32, 0)])).Candidate;
            }
            Assert.InRange(world.Space.GetActor(player).Feet.X, 120 * 256L, WatercourseFarmLayout.PoolBank.X);
            Assert.Equal(atPool.Space.GetActor(player).Feet.Y, world.Space.GetActor(player).Feet.Y);
            Assert.Equal(256, world.Space.GetActor(player).Feet.Z);
            Assert.True(new ReservoirBoundaryEvaluator().Assess(world, world.ReservoirSeals!,
                TestContext.Current.CancellationToken).IsContained);
            world = Walk(authority, actions, world, WatercourseFarmLayout.PoolBank);

            var npcs = new LocalNpcController();
            for (int step = 0; world.Space.GetActor(builder).Feet != world.Construction!.Projects.Single().WorkFeet; step++)
            {
                Assert.True(step < 64, "Wren must reach the retained work stance through ordinary movement.");
                Assert.Empty(npcs.SelectActions(world, actions.NextIdentity));
                world = authority.Commit(actions.Batch(world, [], npcs.SelectMovement(world))).Candidate;
            }
            // At the distant bank the inherited search budget cannot prove the player's current
            // route to the old farm exits. The authored exits themselves remain reachable.
            var waitingProject = world.Construction!.Projects.Single();
            var proposedStructure = waitingProject.Definition.CreateStructure(EntityId.DeriveSpawned(actions.NextIdentity(),
                checked((ulong)waitingProject.Definition.MaterialTransform.Outputs.Count + 1)), waitingProject.SiteFeet);
            GroundAccessRequirement[] currentAccess = waitingProject.RequiredAccess.Select(value =>
                new GroundAccessRequirement(value.ActorId, world.Space.GetActor(value.ActorId).Feet, value.To)).ToArray();
            Assert.Equal(StructurePlacementStatus.Prepared,
                world.Space.PrepareStructurePlacement(proposedStructure, waitingProject.RequiredAccess).Status);
            Assert.Equal(StructurePlacementStatus.AccessLimit, world.Space.PrepareStructurePlacement(proposedStructure,
                waitingProject.RequiredAccess.Concat(currentAccess).Distinct()).Status);
            LocalSpatialSnapshot proposedSpace = world.Space.WithStructures(world.Space.Structures.Append(proposedStructure));
            Assert.All(currentAccess.Where(value => value.ActorId == player), requirement =>
                Assert.Equal(GroundRouteStatus.ExpansionLimit,
                    proposedSpace.QueryGroundRoute(player, requirement.From, requirement.To).Status));
            Assert.Empty(npcs.SelectActions(world, actions.NextIdentity));
            Assert.Equal(0UL, waitingProject.Progress.CompletedUnits);
            Assert.Equal(6UL, world.Inventory.Custody.GetStoredQuantity(waitingProject.ProjectCustodianId));

            // An actual authorized work attempt receives the same conservative refusal, retaining
            // all labor/material while this ordinary advancing interval still flows water once.
            var delivery = ConstructionUpgradeFixture.Delivery(world, builder);
            var tool = world.Inventory.Custody.Lots.Single(lot =>
                lot.CustodianId == world.GetResident(builder).InventoryCustodianId
                && lot.Definition == waitingProject.Definition.RequiredTool.Item);
            var refusedWork = new WorldActionCommand(actions.NextIdentity(), delivery.PrincipalId, delivery.SessionId,
                builder, world.WorldId, world.BranchId, world.Space.GetActor(builder).Revision,
                new WorkConstructionIntent(waitingProject.ProjectId, waitingProject.Revision,
                    world.Inventory.Custody.GetStorage(waitingProject.ProjectCustodianId).Revision, tool.LotId, tool.Revision));
            WorldSnapshot beforeRefusal = world;
            WorldStepDecision refused = authority.Commit(actions.Batch(world, [refusedWork], []));
            Assert.Equal(WorldActionRejection.Unavailable, Assert.Single(refused.Receipts).Rejection);
            world = refused.Candidate;
            Assert.Equal(beforeRefusal.Construction!.ContentHash, world.Construction!.ContentHash);
            Assert.Equal(beforeRefusal.Inventory.ContentHash, world.Inventory.ContentHash);
            Assert.Equal(beforeRefusal.Production!.ContentHash, world.Production!.ContentHash);
            Assert.Equal(beforeRefusal.Calendar.AddMilliseconds(1800), world.Calendar);
            Assert.Equal(beforeRefusal.AdvancingSteps + 1, world.AdvancingSteps);
            LiquidCellState refusedStepFlow = new LiquidCellPlanner().PlanAdvance(beforeRefusal.LiquidCells!, world.Calendar)
                .ApplyTo(beforeRefusal.LiquidCells!);
            Assert.Equal(LiquidCellStateCodecV1.Encode(refusedStepFlow), LiquidCellStateCodecV1.Encode(world.LiquidCells!));

            world = Walk(authority, actions, world, new(WatercourseFarmLayout.PoolBank.X,
                WatercourseFarmLayout.NorthBank.Y, WatercourseFarmLayout.NorthBank.Z));
            world = Walk(authority, actions, world, WatercourseFarmLayout.NorthBank);
            for (int step = 0; !world.Construction!.Projects.Single().IsCompleted; step++)
            {
                Assert.True(step < 256, "The original funded sapling guard must remain reachable and buildable.");
                IReadOnlyList<WorldActionCommand> work = npcs.SelectActions(world, actions.NextIdentity);
                var project = world.Construction.Projects.Single();
                if (world.Space.GetActor(builder).Feet == project.WorkFeet && work.Count == 0)
                {
                    Assert.Fail(DescribeUnavailableWork(world, actions));
                }
                WorldStepDecision result = authority.Commit(actions.Batch(world, work, npcs.SelectMovement(world)));
                Assert.All(result.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
                world = result.Candidate;
            }
            Assert.Equal(200UL, world.Construction!.Projects.Single().Progress.CompletedUnits);
            Assert.Single(world.Production!.Batches);
            Assert.Single(world.Space.Structures);
            AssertConserved(world.LiquidCells!);
            Assert.All(world.Residents, resident => Assert.Equal(
                genesis.GetResident(resident.ActorId).ExperiencedTime.Add(LocalExposureDuration.FromMilliseconds(
                    checked(world.AdvancingSteps * 1800))), resident.ExperiencedTime));
            Assert.All(world.Space.Actors, actor => Assert.Equal(
                BodyContractEncodingV1.EncodeCondition(genesis.Space.GetActor(actor.ActorId).Condition),
                BodyContractEncodingV1.EncodeCondition(actor.Condition)));
            Assert.Contains(world.LiquidCells!.Quantities, pair => pair.Key.Coordinate.Z == -1 && pair.Value > 0);
            final = world;
        }
        byte[] closed = File.ReadAllBytes(files.Path);
        using (SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
            foreach (WorldSnapshot expected in new[] { funded, genesis, final })
            {
                RecordedWorldState recorded = reopened.ReadRecordedStateForHost(expected.Revision);
                Assert.Equal(final.Revision, recorded.SourceHeadRevision);
                Assert.Equal(final.ContentHash, recorded.SourceHeadHash);
                Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(recorded.State));
            }
        }
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
    }

    private static WorldSnapshot Walk(SqliteWorldAuthority authority, ConstructionUpgradeFixture actions,
        WorldSnapshot world, SubunitPosition destination)
    {
        EntityId player = Player(world);
        for (int step = 0; world.Space.GetActor(player).Feet != destination; step++)
        {
            Assert.True(step < 256, "The authored bank is reachable through bounded ordinary movement.");
            GroundRouteResult route = world.Space.QueryGroundRoute(player, destination);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            SubunitPosition next = Assert.IsType<SubunitPosition>(route.NextWaypoint);
            SubunitPosition feet = world.Space.GetActor(player).Feet;
            long dx = Math.Clamp(next.X - feet.X, -32, 32);
            long dy = dx == 0 ? Math.Clamp(next.Y - feet.Y, -32, 32) : 0;
            world = authority.Commit(actions.Batch(world, [],
                [new(ConstructionUpgradeFixture.Delivery(world, player), player, dx, dy)])).Candidate;
            Assert.Equal(feet.Offset(dx, dy, 0), world.Space.GetActor(player).Feet);
        }
        return world;
    }

    private static void AssertConserved(LiquidCellState cells)
    {
        Assert.Single(cells.Portions.Select(portion => portion.OriginId).Distinct());
        ulong cursor = 0;
        foreach (LiquidCellPortion portion in cells.Portions.OrderBy(portion => portion.Start))
        {
            Assert.Equal(LiquidStockOrigin.RawSource, portion.Origin);
            Assert.Equal(cursor, portion.Start);
            cursor = portion.End;
        }
        Assert.Equal(WatercourseFarmLayout.InitialWaterUnits, cursor);
        Assert.Equal(WatercourseFarmLayout.InitialWaterUnits,
            cells.Quantities.Values.Aggregate(0UL, (sum, amount) => checked(sum + amount)));
        Assert.All(cells.Quantities.Values, amount => Assert.InRange(amount, 1UL, 1000UL));
    }

    private static string DescribeUnavailableWork(WorldSnapshot world, ConstructionUpgradeFixture actions)
    {
        var project = world.Construction!.Projects.Single();
        var structure = project.Definition.CreateStructure(EntityId.DeriveSpawned(actions.NextIdentity(),
            checked((ulong)project.Definition.MaterialTransform.Outputs.Count + 1)), project.SiteFeet);
        GroundAccessRequirement[] access = project.RequiredAccess.Concat(project.RequiredAccess.Select(value =>
            new GroundAccessRequirement(value.ActorId, world.Space.GetActor(value.ActorId).Feet, value.To))).Distinct().ToArray();
        StructurePlacementResult placement = world.Space.PrepareStructurePlacement(structure, access);
        LocalSpatialSnapshot proposed = world.Space.WithStructures(world.Space.Structures.Append(structure));
        string routes = string.Join("; ", access.Select(value =>
            $"{world.GetResident(value.ActorId).DisplayName} {value.From} -> {value.To}: "
            + proposed.QueryGroundRoute(value.ActorId, value.From, value.To).Status));
        return $"No ordinary work offered at work={project.Progress.CompletedUnits}, "
            + $"builder={world.Space.GetActor(project.BuilderActorId).Feet}, required={project.WorkFeet}, "
            + $"stock={world.Inventory.Custody.GetStoredQuantity(project.ProjectCustodianId)}, "
            + $"held={world.HasReadingHold}, placement={placement.Status}. Access: {routes}";
    }

    private static EntityId Player(WorldSnapshot world) => world.Residents.Single(resident =>
        resident.ControllerKind == ResidentControllerKind.Player).ActorId;

    private static TerrainCell Cell(WorldSnapshot world, CellCoordinate cell) =>
        world.Space.Regions.Single(region => region.Bounds.Contains(cell)).GetCell(cell);
}
