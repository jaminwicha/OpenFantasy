using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>Actual coordinated-world commands, retaining permanent identities when reopening a sealed boundary.</summary>
internal sealed class CoordinatedLiveWriterJourney
{
    private readonly ConstructionUpgradeFixture _commands = new();
    private readonly SubunitPosition _home;
    private CareWorldSession _session;
    private readonly ushort _profileVersion;

    internal CoordinatedLiveWriterJourney(bool consentedMaterial = false)
    {
        var ordinary = RestorativeJourneyAdmission.CreateEmbedded("live-material-evidence", "Traveler");
        Assert.Equal((ushort)26, ordinary.ProfileVersion);
        _session = new(consentedMaterial ? WorldConsentedMaterialAdmission.CreateFresh(ordinary)
            : WorldCoordinatedAdmission.CreateFresh(ordinary));
        _profileVersion = consentedMaterial ? (ushort)28 : (ushort)27;
        _home = World.Space.GetActor(Player).Feet;
        AssertOwnersAgree(World, _profileVersion);
    }

    internal WorldSnapshot World => _session.ReadStateForHost();
    internal EntityId Player => World.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
    internal EntityId Helper => World.Quests!.Definitions.Single().GiverActorId;

    internal WorldActionCommand Command(EntityId actor, WorldActionIntent intent)
    {
        var world = World;
        var delivery = ConstructionUpgradeFixture.Delivery(world, actor);
        return new(_commands.NextIdentity(), delivery.PrincipalId, delivery.SessionId, actor,
            world.WorldId, world.BranchId, world.Space.GetActor(actor).Revision, intent);
    }

    internal WorldStepDecision Step(IEnumerable<WorldActionCommand>? actions = null,
        IEnumerable<WorldMovementInput>? movement = null, bool accepted = true)
    {
        var before = World;
        var batch = _commands.Batch(before, actions ?? [], movement ?? []);
        var decision = _session.Commit(batch);
        Assert.Equal(batch.Actions.Count, decision.Receipts.Count);
        foreach (var action in batch.Actions)
        {
            var receipt = Assert.Single(decision.Receipts, value => value.CommandId == action.CommandId);
            Assert.Equal(action.ContentHash, receipt.CommandHash);
            Assert.Equal(before.Revision + 1, receipt.CommitSequence);
            if (accepted)
            {
                Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
                Assert.Equal(WorldActionRejection.None, receipt.Rejection);
            }
        }
        AssertOwnersAgree(World, _profileVersion);
        return decision;
    }

    internal WorldMaterialCareArchiveSegment CutAndReopen()
    {
        var closing = World;
        var identities = _session.OccupiedIdentities.ToArray();
        var segment = WorldMaterialCareArchiveSegment.Seal(closing);
        Assert.Equal(WorldSnapshotCodec.Encode(closing), segment.Encode());
        var retrieved = WorldMaterialCareArchiveSegment.Decode(segment.Reference, segment.Encode(), closing.Coordination!.Anchor);
        Assert.Equal(segment.Anchor.ContentHash, retrieved.Anchor.ContentHash);
        var continued = retrieved.Continued;
        Assert.Equal(WorldSnapshotCodec.Encode(segment.Continued), WorldSnapshotCodec.Encode(continued));
        Assert.Equal(closing.Revision, continued.Revision);
        Assert.Equal(closing.AdvancingSteps, continued.AdvancingSteps);
        Assert.Equal(closing.ProcessIntervals, continued.ProcessIntervals);
        Assert.Equal(closing.Calendar, continued.Calendar);
        Assert.Equal(closing.Inventory.ContentHash, continued.Inventory.ContentHash);
        Assert.Equal(closing.Gathering!.ContentHash, continued.Gathering!.ContentHash);
        Assert.Equal(closing.Excavation!.ContentHash, continued.Excavation!.ContentHash);
        Assert.Equal(closing.Construction!.ContentHash, continued.Construction!.ContentHash);
        Assert.Equal(closing.Quests!.ContentHash, continued.Quests!.ContentHash);
        Assert.Equal(closing.Progression!.ContentHash, continued.Progression!.ContentHash);
        Assert.Equal(closing.Pacing!.ContentHash, continued.Pacing!.ContentHash);
        Assert.Equal(closing.Care!.Revision, continued.Care!.Revision);
        Assert.Equal(closing.Care.Pending, continued.Care.Pending);
        Assert.Equal(closing.Production!.Revision, continued.Production!.Revision);
        Assert.Equal(closing.Production.TotalBatchCount, continued.Production.TotalBatchCount);
        Assert.Equal(closing.Production.TotalConsumedItemCount, continued.Production.TotalConsumedItemCount);
        Assert.Equal(closing.Space.Regions.Select(value => value.ComputeContentHash()),
            continued.Space.Regions.Select(value => value.ComputeContentHash()));
        Assert.Equal(closing.Space.Actors.Select(value => (value.ActorId, value.Revision, value.Feet)),
            continued.Space.Actors.Select(value => (value.ActorId, value.Revision, value.Feet)));
        Assert.Equal(closing.Space.Structures.Select(value => value.StructureId), continued.Space.Structures.Select(value => value.StructureId));
        Assert.Equal(closing.Reservoirs!.Capacity.Domain.ContentHash, continued.Reservoirs!.Capacity.Domain.ContentHash);
        Assert.Equal(closing.Reservoirs.Capacity.Revision, continued.Reservoirs.Capacity.Revision);
        Assert.Equal(closing.Reservoirs.Geometries.Select(value => value.ContentHash),
            continued.Reservoirs.Geometries.Select(value => value.ContentHash));
        Assert.Equal(closing.LiquidCells!.Portions, continued.LiquidCells!.Portions);
        Assert.Equal(closing.LiquidCells.Topology!.ContentHash, continued.LiquidCells.Topology!.ContentHash);
        Assert.Equal(closing.LiquidCells.Revision, continued.LiquidCells.Revision);
        Assert.Equal(closing.LiquidCells.Boundary, continued.LiquidCells.Boundary);
        byte[] bytes = WorldSnapshotCodec.Encode(continued);
        var reopened = WorldSnapshotCodec.DecodeCoordinated(bytes, retrieved.Anchor);
        Assert.Equal(bytes, WorldSnapshotCodec.Encode(reopened));
        _session = new(reopened, identities);
        Assert.Equal(identities.Order(), _session.OccupiedIdentities.Order());
        AssertOwnersAgree(World, _profileVersion);
        return retrieved;
    }

    internal void Walk(EntityId actor, SubunitPosition target, bool interaction = false)
    {
        for (int step = 0; ; step++)
        {
            var world = World;
            var feet = world.Space.GetActor(actor).Feet;
            if (interaction ? ResourceInteractionQueries.Query(world.Space, actor, target) == SpatialInteraction.Reachable : feet == target) return;
            Assert.True(step < 512, $"Actual coordinated path to {target} did not arrive from {feet}.");
            long dx = Math.Sign(target.X - feet.X) * WorldMovementInput.DisplacementPerStep;
            long dy = dx == 0 ? Math.Sign(target.Y - feet.Y) * WorldMovementInput.DisplacementPerStep : 0;
            Assert.True(dx != 0 || dy != 0);
            Step(movement: [new(ConstructionUpgradeFixture.Delivery(world, actor), actor, dx, dy)]);
            Assert.Equal(feet.Offset(dx, dy, 0), World.Space.GetActor(actor).Feet);
        }
    }

    internal void WalkToMiningStance()
    {
        var start = World.Space.GetActor(Player).Feet;
        Walk(Player, new(start.X, BreachFarmLayout.MiningStance.Y, start.Z));
        Walk(Player, BreachFarmLayout.MiningStance);
    }

    internal void WalkToBerries()
    {
        // Return through the actual mine approach before taking the farm's berry connector.
        // A direct southward route from the mining stance intersects the authored worksite wall.
        var miningExit = World.Space.GetActor(Player).Feet;
        Walk(Player, new(_home.X, miningExit.Y, _home.Z));
        Walk(Player, _home);
        var target = Berries().Node.Feet;
        var origin = World.Space.GetActor(Player).Feet;
        var corner = target.Y >= origin.Y ? new SubunitPosition(origin.X, target.Y, target.Z)
            : new SubunitPosition(target.X, origin.Y, target.Z);
        Walk(Player, corner);
        Walk(Player, target, interaction: true);
    }

    internal void EarnEffort()
    {
        for (int step = 0; World.Gathering!.GetActorWork(Player).Effort < GatherRulesV1.EffortPerSwing; step++)
        {
            Assert.True(step < 40, "Idle recovery must earn the original bounded swing cost.");
            Step();
        }
    }

    internal WorldActionCommand Mine()
    {
        var world = World;
        var site = Assert.Single(world.Excavation!.Sites);
        var bag = world.GetResident(Player).InventoryCustodianId;
        var tool = world.Inventory.Custody.Lots.Single(value => value.CustodianId == bag && value.Definition == site.RequiredTool);
        return Command(Player, new ExcavateIntent(site.SiteId, site.Revision,
            world.Space.Regions.Single(value => value.Bounds.Contains(site.Cell)).Revision, tool.LotId, tool.Revision,
            world.Inventory.Custody.GetStorage(bag).Revision));
    }

    internal (GatherNodeState Node, WorldActionCommand Command) Berries()
    {
        var world = World;
        var recipe = world.Production!.Recipes.Single(value => value.PhysicalRecipe.Key == CareJourneyAdmission.RecipeKey);
        var node = world.Gathering!.Nodes.Single(value => value.Definition.YieldDefinition == recipe.ItemFor(recipe.Inputs.Single()));
        var tool = world.Gathering.Tools.Single(value => value.Kind == node.Definition.RequiredToolKind);
        var bag = world.GetResident(Player).InventoryCustodianId;
        var lot = world.Inventory.Custody.Lots.Single(value => value.CustodianId == bag && value.Definition == tool.Definition);
        return (node, Command(Player, new GatherIntent(node.NodeId, node.Revision, lot.LotId, lot.Revision)));
    }

    internal WorldActionCommand PrepareQuestDelivery()
    {
        var goal = World.Space.GetActor(Player).Feet.Offset(0, -256, 0);
        for (int step = 0; World.Space.GetActor(Helper).Feet != goal; step++)
        {
            Assert.True(step < 256, "The helper must approach through actual ground navigation.");
            var world = World;
            var route = world.Space.QueryGroundRoute(Helper, goal);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            var waypoint = Assert.IsType<SubunitPosition>(route.NextWaypoint);
            var feet = world.Space.GetActor(Helper).Feet;
            long dx = Math.Clamp(waypoint.X - feet.X, -32, 32);
            long dy = dx == 0 ? Math.Clamp(waypoint.Y - feet.Y, -32, 32) : 0;
            Step(movement: [new(ConstructionUpgradeFixture.Delivery(world, Helper), Helper, dx, dy)]);
            Assert.Equal(feet.Offset(dx, dy, 0), World.Space.GetActor(Helper).Feet);
        }
        Step([Command(Player, new SpeakToActorIntent(Helper, World.Space.GetActor(Helper).Revision))]);
        var quest = World.Quests!.Definitions.Single();
        Step([Command(Player, new AcceptDeliveryQuestIntent(quest.DefinitionId, quest.Version, Helper,
            World.Space.GetActor(Helper).Revision, 0, World.Progression!.GetActor(Player).Revision))]);
        var current = World;
        var bag = current.GetResident(Player).InventoryCustodianId;
        var wood = current.Inventory.Custody.Lots.Single(value => value.CustodianId == bag && value.Definition == quest.Item);
        return Command(Player, new CompleteDeliveryQuestIntent(quest.DefinitionId, quest.Version, Helper,
            current.Space.GetActor(Helper).Revision, 1, current.Progression!.GetActor(Player).Revision,
            current.Inventory.Custody.GetStorage(bag).Revision, [new(wood.LotId, wood.Revision, quest.Quantity)]));
    }

    internal WorldStepDecision BuilderStep()
    {
        var controller = new LocalNpcController();
        return Step(controller.SelectActions(World, _commands.NextIdentity), controller.SelectMovement(World));
    }

    internal static void AssertOwnersAgree(WorldSnapshot world, ushort profileVersion = 27)
    {
        Assert.Equal(profileVersion, world.ProfileVersion);
        Assert.NotNull(world.Coordination);
        Assert.NotNull(world.Care!.Ordinary);
        Assert.NotNull(world.ResourceHistory);
        Assert.NotNull(world.EncounterLifecycle);
        Assert.NotNull(world.Pacing);
        Assert.NotNull(world.Gathering);
        Assert.NotNull(world.Excavation);
        Assert.NotNull(world.Construction);
        Assert.NotNull(world.Quests);
        Assert.NotNull(world.LiquidCells!.Topology);
        Assert.NotNull(world.ReservoirSeals);
        Assert.Equal(world.Inventory.ContentHash, world.Reservoirs!.Capacity.Inventory.ContentHash);
        Assert.Equal(world.Production!.ContentHash, world.Reservoirs.Capacity.Production!.ContentHash);
        Assert.Equal(world.Reservoirs.ContentHash, world.LiquidCells.ReservoirHash);
        Assert.Equal(world.ProcessIntervals, world.Gathering!.AdvancingStep);
    }
}
