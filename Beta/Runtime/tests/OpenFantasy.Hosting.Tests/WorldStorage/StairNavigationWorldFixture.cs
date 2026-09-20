using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

/// <summary>Fresh nine-tread assembly; funding, installation and all travel are ordinary committed world operations.</summary>
internal sealed class StairNavigationWorldFixture
{
    internal static readonly SubunitPosition Origin = new(106 * 256, 324 * 256, 256);
    internal static readonly SubunitPosition StairApproach = new(101 * 256 + 128, 324 * 256 + 128, 256);
    internal static readonly SubunitPosition UnderDeck = new(108 * 256 + 128, 326 * 256 + 128, 256);
    internal static readonly SubunitPosition UpperDeck = UnderDeck.Offset(0, 0, 576);
    internal static readonly SubunitPosition PlayerParking = new(105 * 256 + 128, 327 * 256 + 128, 256);
    internal ConstructionUpgradeFixture Actions { get; }
    internal WorldSnapshot LegacyGenesis { get; }
    internal WorldSnapshot Genesis { get; }
    internal EntityId Player { get; }
    internal EntityId Builder { get; }

    internal StairNavigationWorldFixture(bool physicalConstruction = false)
    {
        var dry = new ConstructedSupportWorldFixture();
        Actions = dry.Actions;
        Player = dry.Player;
        WorldSnapshot basis = dry.ExcavationGenesis;
        ConstructionProject original = basis.Construction!.Projects.Single();
        Builder = original.BuilderActorId;
        var members = Enumerable.Range(0, 9).Select(index => new MillimetreBox(-4000 + index * 875, 0, 0,
            -4000 + (index + 1) * 875, 875, (index + 1) * 250)).Concat([
                new MillimetreBox(2000, 875, 2000, 3875, 4000, 2250),
                new MillimetreBox(2000, 3750, 0, 2250, 4000, 2000),
                new MillimetreBox(3625, 3750, 0, 3875, 4000, 2000)]).ToArray();
        ItemPhysicalBinding wood = original.Definition.MaterialTransform.Bindings.Single(value => value.Physical.Key == "wood");
        ItemPhysicalBinding offcuts = original.Definition.MaterialTransform.Bindings.Single(value => value.Physical.Key == "wood_offcuts");
        var stairs = new ItemPhysicalBinding(new(new EntityId(0x737461697273, 1), ContentHash.Compute("stair-navigation-assembly"u8)),
            new(wood.Physical.Package, new(0x737461697273, 2), "stair_fixture_assembly", new(270000),
                new(members.Aggregate(0UL, (total, member) => checked(total + member.VolumeMicrolitres))), new(1)));
        // Synthetic mass/volume calibration isolates real material funding and navigation from unimplemented strength rules.
        var recipe = new CraftRecipeDefinition(original.Definition.MaterialTransform.DefinitionId,
            new(wood.Physical.Package, original.Definition.MaterialTransform.PhysicalRecipe.DefinitionId,
                "assemble_stair_fixture", new(1), null, new([wood.Physical, offcuts.Physical, stairs.Physical]),
                [new(RecipeMaterialRole.Input, wood.Physical.Pin(), 6),
                 new(RecipeMaterialRole.Output, stairs.Physical.Pin(), 1),
                 new(RecipeMaterialRole.Waste, offcuts.Physical.Pin(), 3)]),
            [wood, offcuts, stairs], [new(wood.Item, 0, new(270000)), new(wood.Item, 1, new(10000))]);
        var definition = new ConstructionDefinition(original.Definition.DefinitionId, "stair_fixture_assembly", "Stair fixture assembly",
            recipe, 0, original.Definition.RequiredTool, members, new(1), original.Definition.WorkRate);
        var project = new ConstructionProject(original.ProjectId, definition, original.OwnerActorId, Builder,
            original.QuestId, original.ProjectCustodianId, original.ReservationId, Origin, original.WorkFeet,
            [new(Builder, original.WorkFeet, UnderDeck),
             new(Player, basis.Space.GetActor(Player).Feet, new(101 * 256 + 128, 327 * 256 + 128, 256))]);
        var construction = new ConstructionSnapshot(basis.WorldId, basis.BranchId, 0, [definition], [project]);
        var production = new ProductionSnapshot(basis.WorldId, basis.BranchId, 0,
            basis.Production!.Recipes.Where(value => value.DefinitionId != recipe.DefinitionId).Append(recipe), [], []);
        ActorPresence builder = basis.Space.GetActor(Builder);
        var actors = basis.Space.Actors.Select(actor => actor.ActorId == Builder
            ? new ActorPresence(builder.Binding, builder.Condition, builder.Scope, UnderDeck, 0, builder.HomePlaceId) : actor);
        var residents = basis.Residents.Select(resident => resident.ActorId == Builder
            ? new WorldResident(resident.ActorId, resident.InventoryCustodianId, resident.DisplayName, resident.ControllerKind,
                UnderDeck, 0, resident.Comprehension, resident.ExperiencedTime, resident.OpeningUtterance) : resident);
        var authored = new WorldSnapshot(basis.Inventory, basis.Space.WithActors(actors), residents,
            basis.AdmissionVersion, basis.Calendar, 0, 0, gathering: basis.Gathering, production: production,
            progression: basis.Progression, quests: basis.Quests, construction: construction, excavation: basis.Excavation);
        LegacyGenesis = WorldConstructedSupportAdmission.CreateGenesis(authored);
        Genesis = WorldSteppedSupportAdmission.CreateGenesis(LegacyGenesis);
        if (physicalConstruction) { Genesis = WorldConstructionPhysicalAdmission.CreateGenesis(Genesis); }
    }

    internal WorldSnapshot FundAndBuild(string path, WorldSnapshot? genesis = null)
    {
        WorldSnapshot world = Actions.CreateSource(path, completeQuest: true, genesis: genesis ?? Genesis);
        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(path);
        world = Actions.Act(authority, world, Player, new CloseDialogueIntent(world.Dialogues.Single().DialogueId));
        var controller = new LocalNpcController();
        for (int step = 0; world.Construction!.Projects.Single().Installed is null; step++)
        {
            Assert.True(step < 128, "The funded stair assembly must be completed by actual NPC work.");
            var decision = authority.Commit(Actions.Batch(world, controller.SelectActions(world, Actions.NextIdentity),
                controller.SelectMovement(world)));
            Assert.All(decision.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
            world = decision.Candidate;
        }
        return world;
    }

    internal WorldStepBatch NextStep(WorldSnapshot world, EntityId actor, SubunitPosition destination,
        out SubunitPosition waypoint)
    {
        GroundRouteResult route = world.Space.QueryWalkingRoute(actor, destination);
        Assert.True(route.Status == GroundRouteStatus.Reachable,
            $"Walking from {world.Space.GetActor(actor).Feet} to {destination} returned {route.Status} after {route.ExpandedNodes} expansions.");
        waypoint = Assert.IsType<SubunitPosition>(route.NextWaypoint);
        SubunitPosition current = world.Space.GetActor(actor).Feet;
        return Actions.Batch(world, [], [new(ConstructionUpgradeFixture.Delivery(world, actor), actor,
            waypoint.X - current.X, waypoint.Y - current.Y)]);
    }

    internal WorldSnapshot CommitStep(SqliteWorldAuthority authority, WorldSnapshot world, WorldStepBatch batch,
        EntityId actor, SubunitPosition expectedFeet)
    {
        Assert.Empty(batch.Actions);
        WorldStepDecision decision = authority.Commit(batch);
        Assert.Empty(decision.Receipts);
        WorldSnapshot next = decision.Candidate;
        Assert.Equal(expectedFeet, next.Space.GetActor(actor).Feet);
        Assert.Equal(world.Space.GetActor(actor).Revision + 1, next.Space.GetActor(actor).Revision);
        Assert.Equal(world.AdvancingSteps + 1, next.AdvancingSteps);
        Assert.Equal(world.Calendar.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep), next.Calendar);
        Assert.Equal(Math.Min(GatherRulesV1.MaximumEffort,
            world.Gathering!.GetActorWork(actor).Effort + GatherRulesV1.EffortPerAdvancingStep),
            next.Gathering!.GetActorWork(actor).Effort);
        Assert.InRange(Math.Abs(expectedFeet.Z - world.Space.GetActor(actor).Feet.Z), 0, 64);
        Assert.Equal(world.Construction!.ContentHash, next.Construction!.ContentHash);
        Assert.Equal(world.Inventory.ContentHash, next.Inventory.ContentHash);
        return next;
    }

    internal WorldSnapshot WalkTo(SqliteWorldAuthority authority, WorldSnapshot world, EntityId actor,
        SubunitPosition destination, Action<WorldSnapshot>? observe = null)
    {
        for (int step = 0; world.Space.GetActor(actor).Feet != destination; step++)
        {
            Assert.True(step < 256, "Ordinary stair travel must finish within its finite sample budget.");
            WorldStepBatch batch = NextStep(world, actor, destination, out SubunitPosition next);
            world = CommitStep(authority, world, batch, actor, next);
            observe?.Invoke(world);
        }
        return world;
    }
}
