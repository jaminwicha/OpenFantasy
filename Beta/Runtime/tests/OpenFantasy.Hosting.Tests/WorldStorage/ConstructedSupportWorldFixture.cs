using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

/// <summary>Fresh authored geometry; all funding, construction, effort and actor travel use ordinary committed actions.</summary>
internal sealed class ConstructedSupportWorldFixture
{
    internal static EntityId Id(ulong value) => new(0x737570706f727473, value);
    internal static readonly SubunitPosition PlatformOrigin = new(106 * 256, 324 * 256, 256);
    internal static readonly SubunitPosition PlatformApproach = new(105 * 256 + 128, 324 * 256 + 128, 256);
    internal static readonly SubunitPosition PlatformLanding = PlatformApproach.Offset(256, 0, 64);
    internal static readonly SubunitPosition ExcavationApproach = new(96 * 256 + 128, 325 * 256 + 128, 256);
    internal static readonly CellCoordinate ExcavationCell = new(95, 324, 0);

    internal ConstructionUpgradeFixture Actions { get; } = new();
    internal WorldSnapshot ExcavationGenesis { get; }
    internal WorldSnapshot Genesis { get; }
    internal EntityId Player { get; }
    internal EntityId Tool { get; }
    internal ExcavationSite Site { get; }

    internal ConstructedSupportWorldFixture()
    {
        WorldSnapshot farm = ConstructionFarmAdmission.CreateGenesis(Actions.QuestGenesis());
        Player = farm.Residents.Single(r => r.ControllerKind == ResidentControllerKind.Player).ActorId;
        ConstructionProject previousProject = farm.Construction!.Projects[0];
        ConstructionDefinition previous = previousProject.Definition;
        MillimetreBox[] members = [new(0, 0, 0, 1000, 1000, 250)];
        ItemPhysicalBinding wood = previous.MaterialTransform.Bindings.Single(b => b.Physical.Key == "wood");
        ItemPhysicalBinding offcuts = previous.MaterialTransform.Bindings.Single(b => b.Physical.Key == "wood_offcuts");
        var platform = new ItemPhysicalBinding(new(Id(1), ContentHash.Compute("constructed-support-platform"u8)),
            new(wood.Physical.Package, Id(2), "support_fixture_platform", new(270000),
                new(members[0].VolumeMicrolitres), new(1)));
        // This calibrated fixture proves funded geometry and contact, not a material strength model.
        var recipe = new CraftRecipeDefinition(previous.MaterialTransform.DefinitionId,
            new(wood.Physical.Package, previous.MaterialTransform.PhysicalRecipe.DefinitionId,
                "assemble_support_fixture", new(1), null, new([wood.Physical, offcuts.Physical, platform.Physical]),
                [new(RecipeMaterialRole.Input, wood.Physical.Pin(), 6),
                 new(RecipeMaterialRole.Output, platform.Physical.Pin(), 1),
                 new(RecipeMaterialRole.Waste, offcuts.Physical.Pin(), 3)]),
            [wood, offcuts, platform], [new(wood.Item, 0, new(270000)), new(wood.Item, 1, new(10000))]);
        var definition = new ConstructionDefinition(previous.DefinitionId, "support_fixture_platform", "Support fixture platform",
            recipe, 0, previous.RequiredTool, members, new(1), previous.WorkRate);
        var project = new ConstructionProject(previousProject.ProjectId, definition, previousProject.OwnerActorId,
            previousProject.BuilderActorId, previousProject.QuestId, previousProject.ProjectCustodianId,
            previousProject.ReservationId, PlatformOrigin, previousProject.WorkFeet, previousProject.RequiredAccess);
        var construction = new ConstructionSnapshot(farm.WorldId, farm.BranchId, 0, [definition], [project]);
        var production = new ProductionSnapshot(farm.WorldId, farm.BranchId, 0,
            farm.Production!.Recipes.Where(r => r.DefinitionId != recipe.DefinitionId).Append(recipe), [], []);

        TerrainRegionSnapshot original = farm.Space.Regions[0];
        TerrainRegionSnapshot western = new StratifiedTerrainGenerator().Generate(farm.Space.Scope, original.Seed,
            original.Rules, new(new(94, 323, -2), 2, 4, 7));
        western = western.Excavate(0, [new(95, 325, 0)]);
        TerrainRegionSnapshot lowerApproach = new StratifiedTerrainGenerator().Generate(farm.Space.Scope, original.Seed,
            original.Rules, new(new(96, 323, -2), 2, 4, 2));
        var space = new LocalSpatialSnapshot(farm.Space.Scope, farm.Space.Regions.Concat([western, lowerApproach]), farm.Space.Actors);
        EntityId bag = farm.GetResident(Player).InventoryCustodianId;
        CountedSourceLot tool = farm.Inventory.Custody.Lots.First(l => l.CustodianId == bag
            && farm.Gathering!.Tools.Any(t => t.Definition == l.Definition));
        Tool = tool.LotId;
        TerrainCell cell = western.GetCell(ExcavationCell);
        var substrate = new ItemPhysicalBinding(cell.Material!, new(new PackageId("openfantasy.support-excavation"),
            Id(3), "substrate", new(2_500_000_000), new(1_000_000_000), new(4)));
        Site = new(Id(4), ExcavationCell, cell.SourceId!.Value, Id(5), Player, substrate, tool.Definition,
            1, 0, new(95 * 256 + 128, 325 * 256 + 128, 0));
        var inventory = new CountedAuthorityState(farm.Inventory.Custody, checked(farm.Inventory.AdmittedUnitCapacity + 1),
            farm.Inventory.Actors, farm.Inventory.Controls, farm.Inventory.Policies,
            farm.Inventory.Sources.Append(new(Site.CountedSourceId, cell.Material!, 1)), farm.Inventory.ReservationIds);
        var basis = new WorldSnapshot(inventory, space, farm.Residents, farm.AdmissionVersion, farm.Calendar, 0, 0,
            gathering: farm.Gathering, production: production, progression: farm.Progression,
            quests: farm.Quests, construction: construction);
        ExcavationGenesis = WorldExcavationAdmission.CreateGenesis(basis, new([Site]));
        Genesis = WorldConstructedSupportAdmission.CreateGenesis(ExcavationGenesis);
    }

    internal WorldActionCommand Command(WorldSnapshot world, WorldActionIntent intent) =>
        Command(world, Player, intent);

    internal WorldActionCommand Command(WorldSnapshot world, EntityId actor, WorldActionIntent intent)
    {
        TrustedCommandDelivery delivery = ConstructionUpgradeFixture.Delivery(world, actor);
        return new(Actions.NextIdentity(), delivery.PrincipalId, delivery.SessionId, actor, world.WorldId,
            world.BranchId, world.Space.GetActor(actor).Revision, intent);
    }

    internal WorldSnapshot MoveTo(SqliteWorldAuthority authority, WorldSnapshot world, SubunitPosition destination)
    {
        for (int step = 0; world.Space.GetActor(Player).Feet != destination; step++)
        {
            Assert.True(step < 256, "The fixture's ordinary ground approach must finish within its finite movement budget.");
            GroundRouteResult route = world.Space.QueryGroundRoute(Player, destination);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            SubunitPosition next = Assert.IsType<SubunitPosition>(route.NextWaypoint);
            SubunitPosition current = world.Space.GetActor(Player).Feet;
            long dx = Math.Sign(next.X - current.X) * WorldMovementInput.DisplacementPerStep;
            long dy = dx == 0 ? Math.Sign(next.Y - current.Y) * WorldMovementInput.DisplacementPerStep : 0;
            var batch = Actions.Batch(world, [], [new(ConstructionUpgradeFixture.Delivery(world, Player), Player, dx, dy)]);
            WorldSnapshot candidate = authority.Commit(batch).Candidate;
            Assert.NotEqual(current, candidate.Space.GetActor(Player).Feet);
            world = candidate;
        }
        return world;
    }

    internal WorldSnapshot EarnEffort(SqliteWorldAuthority authority, WorldSnapshot world)
    {
        for (int step = 0; world.Gathering!.GetActorWork(Player).Effort < GatherRulesV1.EffortPerSwing; step++)
        {
            Assert.True(step < 40, "Effort must be earned through bounded ordinary advancing steps.");
            world = authority.Commit(Actions.Batch(world, [], [])).Candidate;
        }
        return world;
    }

    internal WorldSnapshot BuildPlatform(SqliteWorldAuthority authority, WorldSnapshot world)
    {
        var controller = new LocalNpcController();
        for (int step = 0; world.Construction!.Projects[0].Installed is null; step++)
        {
            Assert.True(step < 128, "The funded platform must be reached and completed by actual NPC construction work.");
            world = authority.Commit(Actions.Batch(world, controller.SelectActions(world, Actions.NextIdentity),
                controller.SelectMovement(world))).Candidate;
        }
        return world;
    }

    internal ExcavateIntent ExcavationIntent(WorldSnapshot world)
    {
        ExcavationSite site = world.Excavation!.Sites.Single();
        return new(site.SiteId, site.Revision, world.Space.Regions.Single(r => r.Bounds.Contains(site.Cell)).Revision,
            Tool, world.Inventory.Custody.GetLot(Tool).Revision,
            world.Inventory.Custody.GetStorage(world.GetResident(Player).InventoryCustodianId).Revision);
    }

    internal static WorldSnapshot WithSpace(WorldSnapshot world, LocalSpatialSnapshot space) =>
        new(world.Inventory, space, world.Residents, world.AdmissionVersion, world.Calendar, world.Revision,
            world.AdvancingSteps, world.Dialogues, world.Gathering, world.Production, world.Progression, world.Quests,
            world.Construction, excavation: world.Excavation);
}
