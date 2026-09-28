using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>Compares qualified observation with actual ordinary SQLite publication of the same commands.</summary>
internal sealed class MaterialEvidenceJourney : IDisposable
{
    private readonly StorageFiles _files = new();
    private readonly ConstructionUpgradeFixture _commands = new();
    private readonly WorldStepProcessor _processor = new();
    internal StorageFiles Files => _files;
    internal SqliteWorldAuthority Authority { get; }
    internal WorldSnapshot World => Authority.ReadStateForHost();
    internal EntityId Player => World.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
    internal EntityId Helper => World.Quests!.Definitions.Single().GiverActorId;

    internal MaterialEvidenceJourney(WorldSnapshot? genesis = null)
    {
        Authority = SqliteWorldAuthority.CreateNew(_files.Path,
            genesis ?? RestorativeJourneyAdmission.CreateEmbedded("live-material-evidence", "Traveler"));
        Assert.Equal((ushort)26, World.ProfileVersion);
    }

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
        var observed = _processor.DecideWithMaterialEvidence(before, batch, Authority.IsIdentityOccupiedForHost);
        Assert.Equal(before.ContentHash, World.ContentHash);
        var published = Authority.Commit(batch);
        Assert.Empty(published.MaterialEvidence);
        Assert.Equal(WorldSnapshotCodec.Encode(published.Candidate), WorldSnapshotCodec.Encode(observed.Candidate));
        Assert.Equal(published.Receipts.Select(WorldReceiptCodecV1.Encode), observed.Receipts.Select(WorldReceiptCodecV1.Encode), ByteArrayComparer.Instance);
        Assert.Equal(published.Identities, observed.Identities);
        if (accepted) Assert.All(observed.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
        return observed;
    }

    internal void Walk(EntityId actor, SubunitPosition target, bool interaction = false)
    {
        for (int step = 0; ; step++)
        {
            var world = World;
            var feet = world.Space.GetActor(actor).Feet;
            if (interaction ? ResourceInteractionQueries.Query(world.Space, actor, target) == SpatialInteraction.Reachable : feet == target) return;
            Assert.True(step < 512, $"Ordinary path to {target} did not arrive from {feet}.");
            long dx = Math.Sign(target.X - feet.X) * WorldMovementInput.DisplacementPerStep;
            long dy = dx == 0 ? Math.Sign(target.Y - feet.Y) * WorldMovementInput.DisplacementPerStep : 0;
            Assert.True(dx != 0 || dy != 0);
            Step(movement: [new(ConstructionUpgradeFixture.Delivery(world, actor), actor, dx, dy)]);
            Assert.Equal(feet.Offset(dx, dy, 0), World.Space.GetActor(actor).Feet);
        }
    }

    internal void WalkToBerryConnector(SubunitPosition target)
    {
        var origin = World.Space.GetActor(Player).Feet;
        var corner = target.Y >= origin.Y ? new SubunitPosition(origin.X, target.Y, target.Z)
            : new SubunitPosition(target.X, origin.Y, target.Z);
        Walk(Player, corner);
        Walk(Player, target, interaction: true);
    }

    internal void ApproachHelper()
    {
        var goal = World.Space.GetActor(Player).Feet.Offset(0, -256, 0);
        for (int step = 0; World.Space.GetActor(Helper).Feet != goal; step++)
        {
            Assert.True(step < 256);
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
    }

    internal void EarnEffort(EntityId actor)
    {
        for (int step = 0; World.Gathering!.GetActorWork(actor).Effort < GatherRulesV1.EffortPerSwing; step++)
        {
            Assert.True(step < 40);
            Assert.Empty(Step().MaterialEvidence);
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

    internal (GatherNodeState Node, WorldActionCommand Command) GatherBerries()
    {
        var world = World;
        var recipe = world.Production!.Recipes.Single(value => value.PhysicalRecipe.Key == CareJourneyAdmission.RecipeKey);
        var node = world.Gathering!.Nodes.Single(value => value.Definition.YieldDefinition == recipe.ItemFor(recipe.Inputs.Single()));
        var tool = world.Gathering.Tools.Single(value => value.Kind == node.Definition.RequiredToolKind);
        var bag = world.GetResident(Player).InventoryCustodianId;
        var lot = world.Inventory.Custody.Lots.Single(value => value.CustodianId == bag && value.Definition == tool.Definition);
        return (node, Command(Player, new GatherIntent(node.NodeId, node.Revision, lot.LotId, lot.Revision)));
    }

    internal WorldActionCommand CraftHelperDose()
    {
        var world = World;
        var recipe = world.Production!.Recipes.Single(value => value.PhysicalRecipe.Key == CareJourneyAdmission.RecipeKey);
        var term = recipe.Inputs.Single();
        var bag = world.GetResident(Helper).InventoryCustodianId;
        var lot = world.Inventory.Custody.Lots.Single(value => value.CustodianId == bag && value.Definition == recipe.ItemFor(term));
        return Command(Helper, new CraftIntent(new(recipe.DefinitionId, recipe.Version, world.Production.Revision,
            bag, world.Inventory.Custody.GetStorage(bag).Revision, [new(IngredientLotKind.Source, lot.LotId, lot.Revision, term.Count)])));
    }

    internal WorldActionCommand PrepareQuestDelivery()
    {
        ApproachHelper();
        var world = World;
        Assert.Empty(Step([Command(Player, new SpeakToActorIntent(Helper, world.Space.GetActor(Helper).Revision))]).MaterialEvidence);
        world = World;
        var quest = world.Quests!.Definitions.Single();
        Assert.Empty(Step([Command(Player, new AcceptDeliveryQuestIntent(quest.DefinitionId, quest.Version, Helper,
            world.Space.GetActor(Helper).Revision, 0, world.Progression!.GetActor(Player).Revision))]).MaterialEvidence);
        world = World;
        var bag = world.GetResident(Player).InventoryCustodianId;
        var wood = world.Inventory.Custody.Lots.Single(value => value.CustodianId == bag && value.Definition == quest.Item);
        return Command(Player, new CompleteDeliveryQuestIntent(quest.DefinitionId, quest.Version, Helper,
            world.Space.GetActor(Helper).Revision, 1, world.Progression!.GetActor(Player).Revision,
            world.Inventory.Custody.GetStorage(bag).Revision, [new(wood.LotId, wood.Revision, quest.Quantity)]));
    }

    internal WorldStepDecision BuilderStep()
    {
        var controller = new LocalNpcController();
        return Step(controller.SelectActions(World, _commands.NextIdentity), controller.SelectMovement(World));
    }

    internal void ReopenExactly()
    {
        var expected = World;
        Authority.Dispose();
        using var reopened = SqliteWorldAuthority.OpenExisting(_files.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(expected), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
    }

    public void Dispose()
    {
        Authority.Dispose();
        _files.Dispose();
    }

    private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        internal static ByteArrayComparer Instance { get; } = new();
        public bool Equals(byte[]? x, byte[]? y) => x is not null && y is not null && x.AsSpan().SequenceEqual(y);
        public int GetHashCode(byte[] obj) => obj.Length;
    }
}
