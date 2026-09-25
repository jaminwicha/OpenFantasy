using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class TerrainLiquidBreachRejectionTests
{
    [Fact]
    public void Exterior_wall_refuses_before_partial_labor_while_ordinary_time_and_movement_commit()
    {
        var actions = new ConstructionUpgradeFixture();
        WorldSnapshot genesis = Fresh(exteriorWall: true);
        using var files = new StorageFiles();
        WorldSnapshot before, after;
        WorldActionCommand command;
        WorldActionReceipt receipt;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, genesis))
        {
            before = Earn(authority, actions, genesis);
            var site = Assert.Single(before.Excavation!.Sites);
            Assert.Equal(new CellCoordinate(116, 330, 0), site.Cell);
            Assert.Equal(new SubunitPosition(116 * 256 + 128, 331 * 256 + 128, 256), site.Stance);
            Assert.Equal(site.Stance, before.Space.GetActor(site.OwnerActorId).Feet);
            Assert.Equal(400U, before.Gathering!.GetActorWork(site.OwnerActorId).Effort);

            // The proposed hole is spatially clear, but would remove an actual receiver wall.
            // This separates containment refusal from unsupported feet or missing earned effort.
            var changedSpace = ExcavatedSpace(before, site.Cell);
            Assert.Throws<ArgumentException>(() => TerrainLiquidTopology.Create(before.Reservoirs!,
                changedSpace.Regions, before.ReservoirSeals!));
            command = Mine(actions, before);
            var movement = new WorldMovementInput(ConstructionUpgradeFixture.Delivery(before, site.OwnerActorId),
                site.OwnerActorId, 32, 0);
            WorldStepDecision decision = Reject(authority, actions, before, command,
                WorldActionRejection.Unavailable, movement);
            after = decision.Candidate;
            receipt = Assert.Single(decision.Receipts);
            Assert.Equal(0UL, Assert.Single(after.Excavation!.Sites).CompletedWork);
            Assert.Equal(site.Stance.Offset(32, 0, 0), after.Space.GetActor(site.OwnerActorId).Feet);
            Assert.Equal(410U, after.Gathering!.GetActorWork(site.OwnerActorId).Effort);
            Assert.DoesNotContain(after.Inventory.Custody.Lots, lot => lot.Source.SourceId == site.CountedSourceId);
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(
                authority.FindReceipt(command, ConstructionUpgradeFixture.Delivery(after, site.OwnerActorId))!));
        }

        byte[] closed = File.ReadAllBytes(files.Path);
        using (var reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(after), WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
            foreach (WorldSnapshot expected in new[] { before, genesis, after })
            {
                var recorded = reopened.ReadRecordedStateForHost(expected.Revision);
                Assert.Equal(after.ContentHash, recorded.SourceHeadHash);
                Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(recorded.State));
            }
            Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(
                reopened.FindReceipt(command, ConstructionUpgradeFixture.Delivery(after, command.ActorId))!));
            Assert.Throws<AuthorityDispatchException>(() => reopened.Commit(actions.Batch(after, [command], [])));
            Assert.Equal(after.ContentHash, reopened.InspectForHost().State.ContentHash);
        }
        Assert.Equal(closed, File.ReadAllBytes(files.Path));
    }

    [Fact]
    public void No_effort_stale_site_and_terrain_or_wrong_tool_leave_all_action_effects_unpublished()
    {
        var actions = new ConstructionUpgradeFixture();
        WorldSnapshot world = Fresh();
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, world);
        world = Reject(authority, actions, world, Mine(actions, world), WorldActionRejection.Unavailable).Candidate;
        world = Earn(authority, actions, world);
        WorldActionCommand first = Mine(actions, world);
        WorldStepDecision worked = authority.Commit(actions.Batch(world, [first], []));
        Assert.Equal(WorldActionRejection.None, Assert.Single(worked.Receipts).Rejection);
        world = worked.Candidate;
        Assert.Equal(1UL, world.Excavation!.Sites.Single().CompletedWork);
        Assert.True(Cell(world, BreachFarmLayout.DividerCell).IsSolid);
        world = Earn(authority, actions, world);

        foreach (string field in new[] { "site", "terrain", "tool-revision", "wrong-tool" })
        {
            WorldActionCommand current = Mine(actions, world);
            var intent = (ExcavateIntent)current.Intent;
            var old = (ExcavateIntent)first.Intent;
            var other = world.Inventory.Custody.Lots.First(lot => lot.CustodianId == world.GetResident(current.ActorId).InventoryCustodianId
                && lot.Definition != world.Excavation!.Sites.Single().RequiredTool);
            var altered = intent with
            {
                ExpectedSiteRevision = field == "site" ? old.ExpectedSiteRevision : intent.ExpectedSiteRevision,
                ExpectedTerrainRevision = field == "terrain" ? checked(intent.ExpectedTerrainRevision - 1) : intent.ExpectedTerrainRevision,
                ExpectedToolLotRevision = field == "tool-revision" ? checked(intent.ExpectedToolLotRevision + 1)
                    : field == "wrong-tool" ? other.Revision : intent.ExpectedToolLotRevision,
                ToolLotId = field == "wrong-tool" ? other.LotId : intent.ToolLotId
            };
            WorldActionRejection rejection = field is "tool-revision" or "wrong-tool"
                ? WorldActionRejection.Unavailable : WorldActionRejection.RevisionConflict;
            world = Reject(authority, actions, world, Replace(current, altered), rejection).Candidate;
            Assert.Equal(1UL, world.Excavation!.Sites.Single().CompletedWork);
        }
    }

    [Fact]
    public void Reading_hold_refuses_even_reachable_funded_work_without_advancing_liquid_or_effort()
    {
        var actions = new ConstructionUpgradeFixture();
        WorldSnapshot world = Fresh(nearSpeaker: true);
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, world);
        world = Earn(authority, actions, world);
        EntityId player = Player(world);
        EntityId speaker = world.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Host).ActorId;
        Assert.Equal(SpatialInteraction.Reachable, world.Space.QueryInteraction(player, speaker));
        world = actions.Act(authority, world, player, new SpeakToActorIntent(speaker, world.Space.GetActor(speaker).Revision));
        Assert.True(world.HasReadingHold);
        WorldSnapshot before = world;
        world = Reject(authority, actions, world, Mine(actions, world), WorldActionRejection.Unavailable).Candidate;
        Assert.Equal(before.Calendar, world.Calendar);
        Assert.Equal(before.Gathering!.ContentHash, world.Gathering!.ContentHash);
        Assert.Equal(before.LiquidCells!.ContentHash, world.LiquidCells!.ContentHash);
        Assert.Equal(0UL, world.Excavation!.Sites.Single().CompletedWork);
        world = actions.Act(authority, world, player, new CloseDialogueIntent(Assert.Single(world.Dialogues).DialogueId));
        Assert.False(world.HasReadingHold);
        Assert.Equal(before.Calendar, world.Calendar);
        Assert.Equal(before.LiquidCells!.ContentHash, world.LiquidCells!.ContentHash);
    }

    [Fact]
    public void Full_destination_refuses_final_extraction_without_hole_stock_or_spent_effort()
    {
        var actions = new ConstructionUpgradeFixture();
        WorldSnapshot world = Fresh(fullBag: true);
        using var files = new StorageFiles();
        using var authority = SqliteWorldAuthority.CreateNew(files.Path, world);
        world = Earn(authority, actions, world);
        EntityId bag = world.GetResident(Player(world)).InventoryCustodianId;
        Assert.Equal(world.Inventory.Custody.GetStoredQuantity(bag), world.Inventory.Custody.GetStorage(bag).UnitCapacity);
        Assert.Equal(1UL, world.Excavation!.Sites.Single().RequiredWork);
        Assert.NotEqual(0UL, world.Inventory.Custody.GetStorage(bag).Revision);
        WorldActionCommand current = Mine(actions, world);
        // This old storage pin is stale against the explicitly authored constrained store.
        var stale = ((ExcavateIntent)current.Intent) with
        {
            ExpectedDestinationRevision = world.Inventory.Custody.GetStorage(bag).Revision - 1
        };
        world = Reject(authority, actions, world, Replace(current, stale), WorldActionRejection.RevisionConflict).Candidate;
        world = Reject(authority, actions, world, Mine(actions, world), WorldActionRejection.Unavailable).Candidate;
        Assert.Equal(0UL, world.Excavation!.Sites.Single().CompletedWork);
        Assert.True(Cell(world, BreachFarmLayout.DividerCell).IsSolid);
        Assert.DoesNotContain(world.Inventory.Custody.Lots, lot => lot.Source.SourceId == world.Excavation!.Sites.Single().CountedSourceId);
    }

    [Fact]
    public void Unchanged_open_cells_do_not_allow_a_stale_complete_terrain_topology_pin()
    {
        WorldSnapshot original = BreachFarmAdmission.CreateEmbedded("breach-topology-refusal", "Traveler");
        byte[] before = WorldSnapshotCodec.Encode(original);
        // A separate authored genesis proposal changes remote terrain outside the wet envelope.
        // It must still rederive its complete dependency pin; this is not a live extraction grant.
        var changedSpace = ExcavatedSpace(original, new(126, 349, 0));
        Assert.Throws<ArgumentException>(() => Copy(original, changedSpace, original.Inventory,
            original.Excavation!, original.Reservoirs!, original.LiquidCells!, original.AdmissionVersion));
        var topology = TerrainLiquidTopology.Create(original.Reservoirs!, changedSpace.Regions, original.ReservoirSeals!);
        Assert.Equal(original.LiquidCells!.Topology!.OpenCells, topology.OpenCells);
        Assert.NotEqual(original.LiquidCells!.Topology!.ContentHash, topology.ContentHash);
        LiquidCellState cells = original.LiquidCells.RebindTerrain(original.Reservoirs!, topology);
        var admitted = Copy(original, changedSpace, original.Inventory, original.Excavation!, original.Reservoirs!, cells,
            ContentHash.Compute(before.Concat(topology.ContentHash.ToByteArray()).ToArray()));
        Assert.Equal(WorldSnapshotCodec.Encode(admitted), WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(admitted))));
        Assert.Equal(before, WorldSnapshotCodec.Encode(original));
    }

    private static WorldStepDecision Reject(SqliteWorldAuthority authority, ConstructionUpgradeFixture actions,
        WorldSnapshot before, WorldActionCommand command, WorldActionRejection rejection, WorldMovementInput? movement = null)
    {
        Assert.True(before.Inventory.CanControl(ConstructionUpgradeFixture.Delivery(before, command.ActorId), command.ActorId));
        WorldStepDecision decision = authority.Commit(actions.Batch(before, [command], movement is null ? [] : [movement]));
        Assert.Equal(rejection, Assert.Single(decision.Receipts).Rejection);
        Assert.Equal(new[] { WorldCommittedIdentityKind.CommitCause, WorldCommittedIdentityKind.Command, WorldCommittedIdentityKind.Result },
            decision.Identities.Select(identity => identity.Kind).Order());
        Assert.DoesNotContain(decision.Identities, identity => identity.Identity == EntityId.DeriveSpawned(command.CommandId, 0));
        Assert.Equal(WorldSnapshotCodec.Encode(ExpectedRefusal(before, movement)), WorldSnapshotCodec.Encode(decision.Candidate));
        Assert.Equal(before.Inventory.ContentHash, decision.Candidate.Inventory.ContentHash);
        Assert.Equal(before.Excavation!.ContentHash, decision.Candidate.Excavation!.ContentHash);
        Assert.Equal(before.LiquidCells!.Topology!.ContentHash, decision.Candidate.LiquidCells!.Topology!.ContentHash);
        return decision;
    }

    private static WorldSnapshot ExpectedRefusal(WorldSnapshot before, WorldMovementInput? movement)
    {
        bool advancing = !before.HasReadingHold;
        var space = before.Space;
        if (movement is not null)
        {
            var move = space.PrepareMove(movement.ActorId, space.GetActor(movement.ActorId).Revision, movement.DeltaX, movement.DeltaY);
            Assert.Equal(SpatialMoveStatus.Prepared, move.Status);
            space = space.WithActors(space.Actors.Select(actor => actor.ActorId == movement.ActorId ? move.Candidate! : actor));
        }
        WorldInstant calendar = advancing ? before.Calendar.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep) : before.Calendar;
        ulong steps = before.AdvancingSteps + (advancing ? 1UL : 0UL);
        var residents = before.Residents.Select(resident => advancing ? new WorldResident(resident.ActorId,
            resident.InventoryCustodianId, resident.DisplayName, resident.ControllerKind, resident.HomeFeet,
            resident.WanderRadiusCells, resident.Comprehension,
            resident.ExperiencedTime.Add(ExposurePacingV1.FromExplorationSteps(1)), resident.OpeningUtterance) : resident);
        LiquidCellState cells = advancing ? new LiquidCellPlanner().PlanAdvance(before.LiquidCells!, calendar).ApplyTo(before.LiquidCells!)
            : before.LiquidCells!;
        return new(before.Inventory, space, residents, before.AdmissionVersion, calendar, before.Revision + 1, steps,
            before.Dialogues, advancing ? before.Gathering!.AdvanceStep(steps) : before.Gathering,
            before.Production, before.Progression, before.Quests, before.Construction, reservoirs: before.Reservoirs,
            excavation: before.Excavation, reservoirSeals: before.ReservoirSeals, liquidCells: cells);
    }

    private static WorldSnapshot Earn(SqliteWorldAuthority authority, ConstructionUpgradeFixture actions, WorldSnapshot world)
    {
        for (int step = 0; world.Gathering!.GetActorWork(Player(world)).Effort < GatherRulesV1.EffortPerSwing; step++)
        {
            Assert.True(step < 40, "One swing requires at most forty real ordinary earning steps.");
            world = authority.Commit(actions.Batch(world, [], [])).Candidate;
        }
        return world;
    }

    private static WorldSnapshot Fresh(bool exteriorWall = false, bool fullBag = false, bool nearSpeaker = false)
    {
        WorldSnapshot original = BreachFarmAdmission.CreateEmbedded("breach-rejection", "Traveler");
        Assert.Equal(0UL, original.Revision);
        Assert.Equal(0UL, original.AdvancingSteps);
        Assert.False(original.HasReadingHold);
        ExcavationSite old = Assert.Single(original.Excavation!.Sites);
        CellCoordinate cell = exteriorWall ? new(116, 330, 0) : old.Cell;
        SubunitPosition stance = exteriorWall ? new(116 * 256 + 128, 331 * 256 + 128, 256) : old.Stance;
        TerrainCell terrain = Cell(original, cell);
        Assert.Equal(old.Material.Item, terrain.Material);
        var site = new ExcavationSite(old.SiteId, cell, terrain.SourceId!.Value, old.CountedSourceId, old.OwnerActorId,
            old.Material, old.RequiredTool, fullBag ? 1UL : old.RequiredWork, 0, stance, ExcavationReachMode.RaisedBankTop);
        var excavation = new ExcavationSnapshot([site]);
        // These poses are authored only before a new authority exists. Every later pose is an ordinary action.
        var space = original.Space.WithActors(original.Space.Actors.Select(actor => actor.ActorId == site.OwnerActorId
            ? new ActorPresence(actor.Binding, actor.Condition, actor.Scope, stance, actor.Revision, actor.HomePlaceId)
            : nearSpeaker ? new ActorPresence(actor.Binding, actor.Condition, actor.Scope, stance.Offset(-256, 0, 0), actor.Revision, actor.HomePlaceId) : actor));
        CountedAuthorityState inventory = original.Inventory;
        LiquidReservoirState reservoirs = original.Reservoirs!;
        LiquidCellState cells = original.LiquidCells!;
        if (fullBag)
        {
            EntityId bag = original.GetResident(site.OwnerActorId).InventoryCustodianId;
            ulong actual = inventory.Custody.GetStoredQuantity(bag);
            var custody = new CustodySnapshot(original.WorldId, original.BranchId, inventory.Custody.Revision + 1,
                inventory.Custody.Storages.Select(storage => storage.CustodianId == bag
                    ? storage with { UnitCapacity = actual, Revision = storage.Revision + 1 } : storage), inventory.Custody.Lots);
            inventory = new(custody, inventory.AdmittedUnitCapacity, inventory.Actors, inventory.Controls,
                inventory.Policies, inventory.Sources, inventory.ReservationIds);
            var capacity = reservoirs.Capacity.PlanReconcile(inventory, original.Production, TestContext.Current.CancellationToken);
            reservoirs = reservoirs.PlanApply(capacity).ApplyTo(reservoirs);
            cells = cells.Rebind(reservoirs);
        }
        var admission = ContentHash.Compute(original.ContentHash.ToByteArray().Concat(ExcavationCodecV2.Encode(excavation))
            .Concat(inventory.ContentHash.ToByteArray()).Concat(BitConverter.GetBytes(nearSpeaker)).ToArray());
        return Copy(original, space, inventory, excavation, reservoirs, cells, admission);
    }

    private static WorldSnapshot Copy(WorldSnapshot basis, LocalSpatialSnapshot space, CountedAuthorityState inventory,
        ExcavationSnapshot excavation, LiquidReservoirState reservoirs, LiquidCellState cells, ContentHash admission) =>
        new(inventory, space, basis.Residents, admission, basis.Calendar, basis.Revision, basis.AdvancingSteps,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: reservoirs, excavation: excavation, reservoirSeals: basis.ReservoirSeals, liquidCells: cells);

    private static LocalSpatialSnapshot ExcavatedSpace(WorldSnapshot world, CellCoordinate cell)
    {
        var old = world.Space.Regions.Single(region => region.Bounds.Contains(cell));
        var changed = old.Excavate(old.Revision, [cell]);
        return new(world.Space.Scope, world.Space.Regions.Select(region => ReferenceEquals(region, old) ? changed : region),
            world.Space.Actors, world.Space.Structures, world.Space.GroundSupport, world.Space.StructureSupport);
    }

    private static WorldActionCommand Mine(ConstructionUpgradeFixture actions, WorldSnapshot world)
    {
        var site = Assert.Single(world.Excavation!.Sites);
        EntityId actor = Player(world);
        var delivery = ConstructionUpgradeFixture.Delivery(world, actor);
        var tool = world.Inventory.Custody.Lots.Single(lot => lot.CustodianId == world.GetResident(actor).InventoryCustodianId
            && lot.Definition == site.RequiredTool && lot.ReservedBy is null);
        return new(actions.NextIdentity(), delivery.PrincipalId, delivery.SessionId, actor, world.WorldId, world.BranchId,
            world.Space.GetActor(actor).Revision, new ExcavateIntent(site.SiteId, site.Revision,
                world.Space.Regions.Single(region => region.Bounds.Contains(site.Cell)).Revision, tool.LotId, tool.Revision,
                world.Inventory.Custody.GetStorage(world.GetResident(actor).InventoryCustodianId).Revision));
    }

    private static WorldActionCommand Replace(WorldActionCommand command, ExcavateIntent intent) =>
        new(command.CommandId, command.PrincipalId, command.OriginSessionId, command.ActorId, command.WorldId,
            command.BranchId, command.ExpectedActorRevision, intent);
    private static EntityId Player(WorldSnapshot world) => world.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player).ActorId;
    private static TerrainCell Cell(WorldSnapshot world, CellCoordinate cell) => world.Space.Regions.Single(region => region.Bounds.Contains(cell)).GetCell(cell);
}
