using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class WorksiteContentTests
{
    [Fact]
    public void Fresh_worksite_is_canonical_seeded_unworked_and_retains_authentic_farm_stock()
    {
        WorldSnapshot legacy = new ConstructionUpgradeFixture().QuestGenesis("worksite-content");
        byte[] legacyBytes = WorldSnapshotCodec.Encode(legacy);
        WorldSnapshot world = WorksiteFarmAdmission.CreateEmbedded("worksite-content", "Traveler");
        byte[] bytes = WorldSnapshotCodec.Encode(world);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorksiteFarmAdmission.CreateEmbedded("worksite-content", "Traveler")));
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(bytes)));
        Assert.Equal(legacyBytes, WorldSnapshotCodec.Encode(new ConstructionUpgradeFixture().QuestGenesis("worksite-content")));
        Assert.Equal((ushort)18, world.ProfileVersion);
        Assert.Equal(ConstructionDescentMode.EventOrdered, world.DescentMode);
        Assert.Equal(0UL, world.Revision);
        Assert.Equal(0UL, world.AdvancingSteps);
        Assert.Null(world.PendingExcavation);
        Assert.Empty(world.ConstructionPhysical!.Members);
        Assert.Empty(world.Production!.Batches);
        Assert.Empty(world.Production.Lots);
        Assert.Empty(world.Quests!.Participations);
        Assert.All(world.Construction!.Projects, project =>
        {
            Assert.False(project.IsCompleted);
            Assert.Equal(0UL, project.Progress.CompletedUnits);
            Assert.Equal(200UL, project.Definition.RequiredWork.Value);
            Assert.Equal(0UL, world.Inventory.Custody.GetStoredQuantity(project.ProjectCustodianId));
        });
        Assert.All(world.Space.Regions, region =>
        {
            Assert.Equal(0UL, region.Revision);
            Assert.DoesNotContain(region.Cells, cell => cell.IsExcavated);
        });
        Assert.Single(world.Space.Regions.Select(region => region.Seed).Distinct());
        Assert.Single(world.Space.Regions.Select(region => region.Rules.Version).Distinct());
        Assert.All(world.Gathering!.Actors, work => Assert.Equal(0U, work.Effort));
        Assert.All(world.Gathering.Nodes, node => Assert.Equal(0UL, node.NextUnissuedUnit));
        EntityId player = Player(world);
        Assert.Equal(8UL, WoodQuantity(world, player));
        Assert.Equal(legacy.Space.GetActor(player).Feet, world.Space.GetActor(player).Feet);
        DeliveryQuestDefinition originalQuest = legacy.Quests!.Definitions.Single();
        DeliveryQuestDefinition stairQuest = world.Quests.Definitions.Single(quest => quest.DestinationCustodianId == originalQuest.DestinationCustodianId);
        Assert.NotEqual(originalQuest.DefinitionId, stairQuest.DefinitionId);
        Assert.Equal("Stairway supplies", stairQuest.Title);
        Assert.Equal(originalQuest.Item, stairQuest.Item);
        Assert.Equal(originalQuest.Quantity, stairQuest.Quantity);
        Assert.Equal(originalQuest.MaterialReservationId, stairQuest.MaterialReservationId);
        Assert.Equal(originalQuest.Reward.Version, stairQuest.Reward.Version);
        Assert.Equal(400UL, world.Progression!.Currency.AdmittedSupply);
        Assert.False(Cell(world, WorksiteFarmLayout.PitCell).IsSolid);
        Assert.True(Cell(world, WorksiteFarmLayout.SupportCell).IsSolid);
        Assert.Equal(SpatialOccupancy.Clear, world.Space.QueryOccupancy(player, WorksiteFarmLayout.ExcavationStance));
        Assert.Equal(96L, world.Space.Regions.Min(region => region.Bounds.Minimum.X));
        Assert.Equal(127L, world.Space.Regions.Max(region => region.Bounds.Maximum.X));
        Assert.Equal(320L, world.Space.Regions.Min(region => region.Bounds.Minimum.Y));
        Assert.Equal(351L, world.Space.Regions.Max(region => region.Bounds.Maximum.Y));
        Assert.Equal(WorksiteFarmLayout.SupportCell, world.Excavation!.Sites.Single().Cell);
        Assert.Equal(0UL, world.Excavation.Sites.Single().CompletedWork);
        Assert.Equal(3UL, world.Excavation.Sites.Single().RequiredWork);
        Assert.DoesNotContain(world.Inventory.Custody.Lots,
            lot => lot.Source.SourceId == world.Excavation.Sites.Single().CountedSourceId);

        foreach (string seed in new[] { "aldermere-worksite-v1", "worksite-journey", "another-worksite", "field-review-02" })
        {
            WorldSnapshot anotherSeed = WorksiteFarmAdmission.CreateEmbedded(seed, "Traveler");
            byte[] archive = WorldSnapshotCodec.Encode(anotherSeed);
            Assert.Equal(archive, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(archive)));
            Assert.NotEqual(world.WorldId, anotherSeed.WorldId);
            Assert.NotEqual(world.Excavation.Sites.Single().SiteId, anotherSeed.Excavation!.Sites.Single().SiteId);
            Assert.Equal(world.Construction.Definitions.Select(value => value.Version),
                anotherSeed.Construction!.Definitions.Select(value => value.Version));
            Assert.Equal(SpatialOccupancy.Clear, anotherSeed.Space.QueryOccupancy(Player(anotherSeed), WorksiteFarmLayout.ExcavationStance));
            Assert.False(Cell(anotherSeed, WorksiteFarmLayout.PitCell).IsSolid);
            Assert.True(Cell(anotherSeed, WorksiteFarmLayout.SupportCell).IsSolid);
            Assert.Equal(SpatialOccupancy.Clear, anotherSeed.Space.QueryOccupancy(Player(anotherSeed),
                new SubunitPosition(116 * 256 + 128, 323 * 256 + 128, 256)));
        }
    }

    [Fact]
    public void Real_earned_stock_builds_walkable_decks_and_support_extraction_settles_only_the_removable_deck()
    {
        using var files = new StorageFiles();
        var journey = new Journey();
        WorldSnapshot genesis = WorksiteFarmAdmission.CreateEmbedded("worksite-journey", "Traveler");
        EntityId player = Player(genesis);
        EntityId builder = genesis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Host).ActorId;
        WorldSnapshot final;
        SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(files.Path, genesis);
        try
        {
            WorldSnapshot world = journey.Walk(authority, genesis, player, new(101 * 256 + 128, 326 * 256 + 128, 256));
            for (int swing = 0; swing < 4; swing++)
            {
                world = journey.Earn(authority, world, player);
                GatherNodeState node = world.Gathering!.Nodes.Single(value => value.Definition.Kind == GatherNodeKind.Oak);
                CountedSourceLot axe = Axe(world, player);
                world = journey.Actions.Act(authority, world, player, new GatherIntent(node.NodeId, node.Revision, axe.LotId, axe.Revision));
            }
            Assert.Equal(12UL, WoodQuantity(world, player));
            Assert.Equal(4UL, world.Gathering!.Nodes.Single(value => value.Definition.Kind == GatherNodeKind.Oak).NextUnissuedUnit);
            world = journey.Walk(authority, world, player, world.Space.GetActor(builder).Feet.Offset(-256, 0, 0));
            world = journey.Fund(authority, world, player, builder);
            Assert.Equal(0UL, WoodQuantity(world, player));
            Assert.All(world.Construction!.Projects, project => Assert.Equal(6UL,
                world.Inventory.Custody.GetStoredQuantity(project.ProjectCustodianId)));
            world = journey.Walk(authority, world, player, WorksiteFarmLayout.PlayerParking);
            var npc = new LocalNpcController();
            for (int step = 0; world.Construction!.Projects.Any(project => !project.IsCompleted); step++)
            {
                Assert.True(step < 768, "Both funded projects must finish with their normal retained work and reachable stances.");
                WorldStepDecision work = authority.Commit(journey.Actions.Batch(world,
                    npc.SelectActions(world, journey.Actions.NextIdentity), npc.SelectMovement(world)));
                Assert.All(work.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
                world = work.Candidate;
            }
            Assert.Equal(2, world.Production!.Batches.Count);
            Assert.Equal(14, world.ConstructionPhysical!.Members.Count);
            Assert.All(world.Construction.Projects, project => Assert.Equal(200UL, project.Progress.CompletedUnits));
            Assert.All(world.Production.Batches, batch => Assert.Equal(6UL,
                batch.Ingredients.Aggregate(0UL, (sum, ingredient) => checked(sum + ingredient.Quantity))));
            world = journey.Walk(authority, world, player, WorksiteFarmLayout.StairApproach);
            var heights = new HashSet<long> { 256 };
            world = journey.Walk(authority, world, player, WorksiteFarmLayout.FixedDeck, state => heights.Add(state.Space.GetActor(player).Feet.Z));
            Assert.Equal(Enumerable.Range(0, 10).Select(index => 256L + index * 64), heights.Order());
            world = journey.Walk(authority, world, player, WorksiteFarmLayout.RemovableDeck);
            world = journey.Walk(authority, world, player, WorksiteFarmLayout.StairApproach);
            world = journey.Walk(authority, world, builder, WorksiteFarmLayout.RemovableDeck);
            world = journey.Walk(authority, world, player, WorksiteFarmLayout.PitApproach);
            world = journey.Earn(authority, world, player);
            world = journey.Actions.Act(authority, world, player, new ClimbIntent(0, 256, -256));
            Assert.Equal(WorksiteFarmLayout.ExcavationStance, world.Space.GetActor(player).Feet);
            for (ulong work = 1; work < 3; work++)
            {
                world = journey.Earn(authority, world, player);
                WorldSnapshot previous = world;
                world = journey.ExcavateUnit(authority, world, player);
                Assert.Equal(work, world.Excavation!.Sites.Single().CompletedWork);
                Assert.Null(world.PendingExcavation);
                Assert.Equal(previous.AdvancingSteps + 1, world.AdvancingSteps);
                Assert.Equal(previous.Inventory.ContentHash, world.Inventory.ContentHash);
                Assert.Equal(previous.ConstructionPhysical!.ContentHash, world.ConstructionPhysical!.ContentHash);
                Assert.Equal(previous.Space.GetActor(builder).Feet, world.Space.GetActor(builder).Feet);
                Assert.Equal(previous.Space.Regions.Select(region => region.ComputeContentHash()),
                    world.Space.Regions.Select(region => region.ComputeContentHash()));
                Assert.False(Cell(world, WorksiteFarmLayout.SupportCell).IsExcavated);
                Assert.DoesNotContain(world.Inventory.Custody.Lots,
                    lot => lot.Source.SourceId == world.Excavation.Sites.Single().CountedSourceId);
                if (work == 1)
                {
                    byte[] partial = WorldSnapshotCodec.Encode(world);
                    authority.Dispose();
                    authority = SqliteWorldAuthority.OpenExisting(files.Path);
                    Assert.Equal(partial, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
                    world = authority.ReadStateForHost();
                }
            }
            world = journey.Earn(authority, world, player);
            WorldSnapshot before = world;
            ExcavationSite site = before.Excavation!.Sites.Single();
            var delivery = ConstructionUpgradeFixture.Delivery(before, player);
            var command = new WorldActionCommand(journey.Actions.NextIdentity(), delivery.PrincipalId, delivery.SessionId,
                player, before.WorldId, before.BranchId, before.Space.GetActor(player).Revision,
                ExcavationIntent(before, player));
            WorldStepDecision started = authority.Commit(journey.Actions.Batch(before, [command], []));
            Assert.Empty(started.Receipts);
            world = started.Candidate;
            Assert.NotNull(world.PendingExcavation);
            Assert.Equal(before.Calendar, world.Calendar);
            WorldActionReceipt? receipt = null;
            for (int slice = 0; world.PendingExcavation is not null; slice++)
            {
                Assert.True(slice < 64, "The authored two-project scene must resolve within its retained work bound.");
                var continuation = new WorldStepBatch(journey.Actions.NextIdentity(), world.WorldId, world.BranchId,
                    world.Revision, world.AdvancingSteps, WorldBatchKind.ConsequenceContinuation, [], [], world.ProfileVersion);
                WorldStepDecision result = authority.Commit(continuation);
                world = result.Candidate;
                if (world.PendingExcavation is null) { receipt = Assert.Single(result.Receipts); }
                else { Assert.Empty(result.Receipts); }
            }
            Assert.Equal(WorldActionRejection.None, Assert.IsType<WorldActionReceipt>(receipt).Rejection);
            Assert.Equal(before.AdvancingSteps + 1, world.AdvancingSteps);
            Assert.Equal(before.Calendar.AddMilliseconds(1800), world.Calendar);
            Assert.Equal(before.Production!.ContentHash, world.Production!.ContentHash);
            Assert.Equal(before.Construction!.ContentHash, world.Construction!.ContentHash);
            EntityId moving = world.Construction.Projects.Single(project => project.Definition.Key == WorksiteFarmLayout.RemovableDeckDefinitionKey)
                .Installed!.StructureId;
            Assert.All(world.ConstructionPhysical!.Members, member => Assert.Equal(
                member.StructureId == moving ? new SubunitPosition(0, 0, -256) : default, member.Displacement));
            Assert.All(world.ConstructionPhysical.Members, member => Assert.Equal(10000U, member.ConditionParts));
            Assert.Equal(WorksiteFarmLayout.RemovableDeck.Offset(0, 0, -256), world.Space.GetActor(builder).Feet);
            Assert.Equal(BodyContractEncodingV1.EncodeCondition(before.Space.GetActor(builder).Condition),
                BodyContractEncodingV1.EncodeCondition(world.Space.GetActor(builder).Condition));
            Assert.Equal(BodyContractEncodingV1.EncodeCondition(before.Space.GetActor(player).Condition),
                BodyContractEncodingV1.EncodeCondition(world.Space.GetActor(player).Condition));
            Assert.True(Cell(world, site.Cell).IsExcavated);
            Assert.Equal(site.Quantity, Assert.Single(world.Inventory.Custody.Lots,
                lot => lot.Source.SourceId == site.CountedSourceId).Quantity);
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt!), WorldReceiptCodecV1.Encode(authority.FindReceipt(command, delivery)!));
            world = journey.Earn(authority, world, player);
            world = journey.Actions.Act(authority, world, player, new ClimbIntent(0, -256, 256));
            Assert.Equal(WorksiteFarmLayout.PitApproach, world.Space.GetActor(player).Feet);
            final = world;
        }
        finally
        {
            authority.Dispose();
        }
        byte[] closedBytes = File.ReadAllBytes(files.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(RecordedWorldReader.ReadForHost(files.Path, final.Revision).State));
        Assert.Equal(closedBytes, File.ReadAllBytes(files.Path));
    }

    private static EntityId Player(WorldSnapshot world) => world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
    private static TerrainCell Cell(WorldSnapshot world, CellCoordinate cell) => world.Space.Regions.Single(region => region.Bounds.Contains(cell)).GetCell(cell);
    private static ulong WoodQuantity(WorldSnapshot world, EntityId player) => world.Inventory.Custody.Lots
        .Where(lot => lot.CustodianId == world.GetResident(player).InventoryCustodianId && lot.Definition == world.Quests!.Definitions[0].Item)
        .Aggregate(0UL, (sum, lot) => checked(sum + lot.Quantity));
    private static CountedSourceLot Axe(WorldSnapshot world, EntityId actor) => world.Inventory.Custody.Lots.Single(lot =>
        lot.CustodianId == world.GetResident(actor).InventoryCustodianId && world.Gathering!.Tools.Any(tool => tool.Definition == lot.Definition && tool.Kind == GatherToolKind.Axe));

    private static ExcavateIntent ExcavationIntent(WorldSnapshot world, EntityId actor)
    {
        ExcavationSite site = world.Excavation!.Sites.Single();
        CountedSourceLot tool = Axe(world, actor);
        return new(site.SiteId, site.Revision, world.Space.Regions.Single(region => region.Bounds.Contains(site.Cell)).Revision,
            tool.LotId, tool.Revision, world.Inventory.Custody.GetStorage(world.GetResident(actor).InventoryCustodianId).Revision);
    }

    private sealed class Journey
    {
        internal ConstructionUpgradeFixture Actions { get; } = new();

        internal WorldSnapshot ExcavateUnit(SqliteWorldAuthority authority, WorldSnapshot basis, EntityId actor)
        {
            var delivery = ConstructionUpgradeFixture.Delivery(basis, actor);
            var command = new WorldActionCommand(Actions.NextIdentity(), delivery.PrincipalId, delivery.SessionId,
                actor, basis.WorldId, basis.BranchId, basis.Space.GetActor(actor).Revision, ExcavationIntent(basis, actor));
            WorldStepDecision decision = authority.Commit(Actions.Batch(basis, [command], []));
            WorldSnapshot world = decision.Candidate;
            if (world.PendingExcavation is { } initial)
            {
                for (ulong slice = 0; world.PendingExcavation is not null; slice++)
                {
                    Assert.True(slice < initial.MaximumContinuationCount);
                    Assert.Empty(decision.Receipts);
                    Assert.Null(authority.FindReceipt(command, delivery));
                    Assert.Equal(basis.Calendar, world.Calendar);
                    Assert.Equal(basis.AdvancingSteps, world.AdvancingSteps);
                    Assert.Equal(basis.Gathering!.GetActorWork(actor).Effort, world.Gathering!.GetActorWork(actor).Effort);
                    Assert.Equal(basis.Excavation!.ContentHash, world.Excavation!.ContentHash);
                    Assert.Equal(basis.Inventory.ContentHash, world.Inventory.ContentHash);
                    Assert.Equal(basis.Production!.ContentHash, world.Production!.ContentHash);
                    Assert.Equal(basis.ConstructionPhysical!.ContentHash, world.ConstructionPhysical!.ContentHash);
                    Assert.Equal(basis.Space.Actors.Select(value => value.Feet), world.Space.Actors.Select(value => value.Feet));
                    Assert.Equal(basis.Space.Regions.Select(region => region.ComputeContentHash()),
                        world.Space.Regions.Select(region => region.ComputeContentHash()));
                    var continuation = new WorldStepBatch(Actions.NextIdentity(), world.WorldId, world.BranchId,
                        world.Revision, world.AdvancingSteps, WorldBatchKind.ConsequenceContinuation, [], [], world.ProfileVersion);
                    decision = authority.Commit(continuation);
                    world = decision.Candidate;
                }
            }
            WorldActionReceipt receipt = Assert.Single(decision.Receipts);
            Assert.Equal(command.CommandId, receipt.CommandId);
            Assert.Equal(WorldActionRejection.None, receipt.Rejection);
            Assert.Equal(basis.Calendar.AddMilliseconds(1800), world.Calendar);
            Assert.Equal(basis.AdvancingSteps + 1, world.AdvancingSteps);
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(authority.FindReceipt(command, delivery)!));
            return world;
        }

        internal WorldSnapshot Walk(SqliteWorldAuthority authority, WorldSnapshot world, EntityId actor,
            SubunitPosition destination, Action<WorldSnapshot>? observe = null)
        {
            for (int step = 0; world.Space.GetActor(actor).Feet != destination; step++)
            {
                Assert.True(step < 512, "The worksite route must remain reachable through ordinary walking samples.");
                GroundRouteResult route = world.Space.QueryWalkingRoute(actor, destination);
                Assert.True(route.Status == GroundRouteStatus.Reachable,
                    $"Route from {world.Space.GetActor(actor).Feet} to {destination} returned {route.Status}.");
                SubunitPosition next = Assert.IsType<SubunitPosition>(route.NextWaypoint);
                SubunitPosition previous = world.Space.GetActor(actor).Feet;
                world = authority.Commit(Actions.Batch(world, [], [new(ConstructionUpgradeFixture.Delivery(world, actor),
                    actor, next.X - previous.X, next.Y - previous.Y)])).Candidate;
                Assert.Equal(next, world.Space.GetActor(actor).Feet);
                observe?.Invoke(world);
            }
            return world;
        }

        internal WorldSnapshot Earn(SqliteWorldAuthority authority, WorldSnapshot world, EntityId actor)
        {
            for (int step = 0; world.Gathering!.GetActorWork(actor).Effort < GatherRulesV1.EffortPerSwing; step++)
            {
                Assert.True(step < 40, "Effort comes only from actual advancing exploration time.");
                world = authority.Commit(Actions.Batch(world, [], [])).Candidate;
            }
            return world;
        }

        internal WorldSnapshot Fund(SqliteWorldAuthority authority, WorldSnapshot world, EntityId player, EntityId builder)
        {
            world = Actions.Act(authority, world, player, new SpeakToActorIntent(builder, world.Space.GetActor(builder).Revision));
            foreach (DeliveryQuestDefinition quest in world.Quests!.Definitions)
            {
                world = Actions.Act(authority, world, player, new AcceptDeliveryQuestIntent(quest.DefinitionId, quest.Version,
                    builder, world.Space.GetActor(builder).Revision, 0, world.Progression!.GetActor(player).Revision));
                EntityId bag = world.GetResident(player).InventoryCustodianId;
                ulong remaining = quest.Quantity;
                List<SourceLotSelection> selections = [];
                foreach (CountedSourceLot lot in world.Inventory.Custody.Lots.Where(lot => lot.CustodianId == bag && lot.Definition == quest.Item))
                {
                    ulong quantity = Math.Min(lot.Quantity, remaining);
                    if (quantity != 0) { selections.Add(new(lot.LotId, lot.Revision, quantity)); }
                    remaining -= quantity;
                }
                Assert.Equal(0UL, remaining);
                world = Actions.Act(authority, world, player, new CompleteDeliveryQuestIntent(quest.DefinitionId, quest.Version,
                    builder, world.Space.GetActor(builder).Revision, world.Quests!.FindParticipation(player, quest.DefinitionId)!.Revision,
                    world.Progression!.GetActor(player).Revision, world.Inventory.Custody.GetStorage(bag).Revision, selections));
            }
            Assert.All(world.Quests!.Participations, participation => Assert.Equal(ActorQuestStatus.Completed, participation.Status));
            Assert.Equal(400UL, world.Progression!.Currency.GetActorAccount(player).Balance);
            return Actions.Act(authority, world, player, new CloseDialogueIntent(world.Dialogues.Single().DialogueId));
        }
    }
}
