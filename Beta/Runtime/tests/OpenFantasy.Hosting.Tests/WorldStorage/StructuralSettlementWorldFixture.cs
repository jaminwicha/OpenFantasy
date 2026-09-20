using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

internal enum SettlementLowerSupport { Rated, Missing, Excavated }

/// <summary>A real funded slab and rider above a one-cell excavation; all preparation travel and work are committed actions.</summary>
internal sealed class StructuralSettlementWorldFixture
{
    private readonly bool overhangCollision;
    internal static readonly CellCoordinate Pedestal = new(106, 324, 0);
    internal static readonly CellCoordinate LandingCell = new(106, 324, -1);
    internal static readonly CellCoordinate Pit = new(105, 324, 0);
    internal static readonly SubunitPosition Stance = new(105 * 256 + 128, 324 * 256 + 128, 0);
    internal static readonly SubunitPosition WestApproach = new(104 * 256 + 128, 324 * 256 + 128, 256);
    internal static readonly SubunitPosition RiderFeet = new(106 * 256 + 128, 324 * 256 + 128, 320);

    internal StructuralSettlementWorldFixture(bool admitSettlement = true,
        SettlementLowerSupport lowerSupport = SettlementLowerSupport.Rated,
        long safeActorDrop = 256, long safeMemberDrop = 256, bool overhangCollision = false)
    {
        this.overhangCollision = overhangCollision;
        var platform = new ConstructedSupportWorldFixture();
        Actions = platform.Actions;
        Player = platform.Player;
        Tool = platform.Tool;
        WorldSnapshot original = platform.ExcavationGenesis;
        Builder = original.Construction!.Projects.Single().BuilderActorId;
        TerrainRegionSnapshot surface = original.Space.Regions.Single(region => region.Bounds.Contains(Pedestal));
        TerrainCell cell = surface.GetCell(Pedestal);
        Site = new(platform.Site.SiteId, Pedestal, cell.SourceId!.Value, platform.Site.CountedSourceId,
            Player, platform.Site.Material, platform.Site.RequiredTool, 1, 0, Stance);
        TerrainRegionSnapshot carved = surface.Excavate(surface.Revision,
            overhangCollision ? [Pit, new(104, 324, 0)] : [Pit]);
        var lower = new StratifiedTerrainGenerator().Generate(original.Space.Scope, surface.Seed, surface.Rules,
            new(new(overhangCollision ? 103 : 104, 324, -2), overhangCollision ? 4
                : lowerSupport == SettlementLowerSupport.Missing ? 2 : 3, 1, 2));
        if (lowerSupport == SettlementLowerSupport.Excavated)
        {
            lower = lower.Excavate(lower.Revision, [LandingCell]);
        }
        var space = new LocalSpatialSnapshot(original.Space.Scope,
            original.Space.Regions.Select(region => ReferenceEquals(region, surface) ? carved : region).Append(lower), original.Space.Actors);
        (ConstructionSnapshot construction, ProductionSnapshot production) = overhangCollision
            ? CreateOverhang(original) : (original.Construction!, original.Production!);
        var basis = new WorldSnapshot(original.Inventory, space, original.Residents, original.AdmissionVersion,
            original.Calendar, 0, 0, gathering: original.Gathering, production: production,
            progression: original.Progression, quests: original.Quests, construction: construction);
        WorldSnapshot extraction = WorldExcavationAdmission.CreateGenesis(basis, new([Site]));
        WorldSnapshot physical = WorldConstructionPhysicalAdmission.CreateGenesis(
            WorldSteppedSupportAdmission.CreateGenesis(WorldConstructedSupportAdmission.CreateGenesis(extraction)));
        LegacyGenesis14 = WorldStructuralPolicyAdmission.CreateGenesis(physical, PolicyFor(physical));
        Genesis = admitSettlement ? WorldStructuralSettlementAdmission.CreateGenesis(LegacyGenesis14,
            new StructuralSettlementPolicy(256, safeActorDrop, safeMemberDrop, 9800)) : LegacyGenesis14;
    }

    internal ConstructionUpgradeFixture Actions { get; }
    internal WorldSnapshot Genesis { get; }
    internal WorldSnapshot LegacyGenesis14 { get; }
    internal EntityId Player { get; }
    internal EntityId Builder { get; }
    internal EntityId Tool { get; }
    internal ExcavationSite Site { get; }

    internal WorldSnapshot PrepareBeforeExcavation(string path)
    {
        WorldSnapshot world = Actions.CreateSource(path, completeQuest: true, genesis: Genesis);
        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(path);
        world = Actions.Act(authority, world, Player, new CloseDialogueIntent(world.Dialogues.Single().DialogueId));
        var controller = new LocalNpcController();
        for (int step = 0; world.Construction!.Projects.Single().Installed is null; step++)
        {
            Assert.True(step < 128, "The slab must be completed through actual funded NPC work.");
            WorldStepDecision result = authority.Commit(Actions.Batch(world,
                controller.SelectActions(world, Actions.NextIdentity), controller.SelectMovement(world)));
            Assert.All(result.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
            world = result.Candidate;
        }
        Assert.Equal(overhangCollision ? 2 : 1, world.ConstructionPhysical!.Members.Count);
        Assert.Single(world.Production!.Batches);
        if (!overhangCollision)
        {
            world = WalkTo(authority, world, Builder, RiderFeet);
            world = EnterPit(authority, world);
            Assert.Equal(RiderFeet, world.Space.GetActor(Builder).Feet);
        }
        else
        {
            // The retained access witnesses are checked while both actors are on the original floor.
            // Enter the trench outside the completed overhang, then walk beneath its clear underside.
            world = WalkTo(authority, world, Player, WestApproach.Offset(-256, 0, 0));
            world = EarnEffort(authority, world);
            world = Actions.Act(authority, world, Player, new ClimbIntent(256, 0, -256));
            world = WalkTo(authority, world, Player, Stance);
        }
        Assert.Equal(Stance, world.Space.GetActor(Player).Feet);
        world = EarnEffort(authority, world);
        Assert.Equal(0UL, world.Excavation!.Sites.Single().CompletedWork);
        return world;
    }

    private WorldSnapshot EnterPit(SqliteWorldAuthority authority, WorldSnapshot world)
    {
        world = WalkTo(authority, world, Player, WestApproach);
        world = EarnEffort(authority, world);
        return Actions.Act(authority, world, Player, new ClimbIntent(256, 0, -256));
    }

    internal WorldStepBatch NextExcavation(WorldSnapshot world)
    {
        ExcavationSite site = world.Excavation!.Sites.Single();
        TerrainRegionSnapshot region = world.Space.Regions.Single(value => value.Bounds.Contains(site.Cell));
        var tool = world.Inventory.Custody.GetLot(Tool);
        var delivery = ConstructionUpgradeFixture.Delivery(world, Player);
        var command = new WorldActionCommand(Actions.NextIdentity(), delivery.PrincipalId, delivery.SessionId,
            Player, world.WorldId, world.BranchId, world.Space.GetActor(Player).Revision,
            new ExcavateIntent(site.SiteId, site.Revision, region.Revision, Tool, tool.Revision,
                world.Inventory.Custody.GetStorage(world.GetResident(Player).InventoryCustodianId).Revision));
        return Actions.Batch(world, [command], []);
    }

    private WorldSnapshot WalkTo(SqliteWorldAuthority authority, WorldSnapshot world, EntityId actorId, SubunitPosition destination)
    {
        for (int step = 0; world.Space.GetActor(actorId).Feet != destination; step++)
        {
            Assert.True(step < 256, "The rider and excavator must reach their poses through ordinary bounded walking.");
            GroundRouteResult route = world.Space.QueryWalkingRoute(actorId, destination);
            Assert.True(route.Status == GroundRouteStatus.Reachable,
                $"Route from {world.Space.GetActor(actorId).Feet} to {destination} returned {route.Status}.");
            SubunitPosition next = Assert.IsType<SubunitPosition>(route.NextWaypoint);
            ActorPresence actor = world.Space.GetActor(actorId);
            world = authority.Commit(Actions.Batch(world, [], [new(ConstructionUpgradeFixture.Delivery(world, actorId),
                actorId, next.X - actor.Feet.X, next.Y - actor.Feet.Y)])).Candidate;
            Assert.Equal(next, world.Space.GetActor(actorId).Feet);
        }
        return world;
    }

    private WorldSnapshot EarnEffort(SqliteWorldAuthority authority, WorldSnapshot world)
    {
        for (int step = 0; world.Gathering!.GetActorWork(Player).Effort < GatherRulesV1.EffortPerSwing; step++)
        {
            Assert.True(step < 40, "Effort must be earned within forty ordinary advancing steps.");
            world = authority.Commit(Actions.Batch(world, [], [])).Candidate;
        }
        return world;
    }

    private static ConstructionLoadPolicy PolicyFor(WorldSnapshot world)
    {
        ConstructionDefinition definition = world.Construction!.Definitions.Single();
        // Explicit high fixture ratings accommodate the newly carried 1m³ rock; these are not gameplay balance.
        const ulong capacity = 10_000_000_000;
        var joints = definition.Members.SelectMany((upper, upperIndex) => definition.Members
            .Select((lower, lowerIndex) => (lower, lowerIndex))
            .Where(value => upper.MinZ > 0 && upper.MinZ == value.lower.MaxZ
                && Math.Min(upper.MaxX, value.lower.MaxX) > Math.Max(upper.MinX, value.lower.MinX)
                && Math.Min(upper.MaxY, value.lower.MaxY) > Math.Max(upper.MinY, value.lower.MinY))
            .Select(value => new StructuralMemberJoint((uint)upperIndex, (uint)value.lowerIndex, StructuralJointKind.VerticalBearing)));
        var template = new ConstructionBearingTemplate(definition.DefinitionId, definition.Version,
            definition.Members.Select((_, index) => new StructuralMemberRating((uint)index, new(capacity), 2000)), joints);
        var foundations = world.Space.Regions.SelectMany(region => region.Cells)
            .Where(cell => cell.IsSolid && cell.Coordinate.Z is -1 or 0)
            .Select(cell => new StableTerrainBearing(EntityId.DeriveSpawned(cell.SourceId!.Value, 900000), cell.Coordinate,
                cell.SourceId.Value, cell.Material!, new(capacity)));
        var bodies = world.Space.Actors.Select(actor => actor.Condition.Definition).DistinctBy(body => body.DefinitionId)
            .Select(body => new StructuralBodyMass(body.DefinitionId, body.Version, new(70_000_000)));
        var known = world.Production!.Recipes.SelectMany(recipe => recipe.Bindings).Select(binding => binding.Item).ToHashSet();
        var supplemental = world.Inventory.Sources.Select(source => source.Definition).Where(item => !known.Contains(item)).Distinct()
            .Select(item => new StructuralItemMass(item, world.Excavation!.Sites.Any(site => site.Material.Item == item)
                ? world.Excavation.Sites.First(site => site.Material.Item == item).Material.Physical.MassPerUnit : new(1_000_000)));
        return new(world.Space.Scope, [template], foundations, new(bodies, supplemental));
    }

    private static (ConstructionSnapshot, ProductionSnapshot) CreateOverhang(WorldSnapshot world)
    {
        ConstructionProject previousProject = world.Construction!.Projects.Single();
        ConstructionDefinition previous = previousProject.Definition;
        MillimetreBox[] members = [new(0, 0, 0, 1000, 1000, 1000), new(-1000, 0, 1000, 1000, 1000, 1250)];
        CraftRecipeDefinition oldRecipe = previous.MaterialTransform;
        ItemPhysicalBinding wood = oldRecipe.Bindings.Single(binding => binding.Physical.Key == "wood");
        ItemPhysicalBinding offcuts = oldRecipe.Bindings.Single(binding => binding.Physical.Key == "wood_offcuts");
        ItemPhysicalBinding oldOutput = oldRecipe.BindingFor(oldRecipe.ItemFor(oldRecipe.Outputs[(int)previous.InstalledOutputIndex]));
        var output = new ItemPhysicalBinding(new(oldOutput.Item.DefinitionId, ContentHash.Compute("settlement-overhang"u8)),
            new(oldOutput.Physical.Package, oldOutput.Physical.DefinitionId, "settlement_overhang",
                oldOutput.Physical.MassPerUnit, new(members.Aggregate(0UL, (sum, member) => checked(sum + member.VolumeMicrolitres))),
                oldOutput.Physical.Hardness));
        var recipe = new CraftRecipeDefinition(oldRecipe.DefinitionId,
            new(wood.Physical.Package, oldRecipe.PhysicalRecipe.DefinitionId, "assemble_settlement_overhang", new(1), null,
                new([wood.Physical, offcuts.Physical, output.Physical]),
                [new(RecipeMaterialRole.Input, wood.Physical.Pin(), 6), new(RecipeMaterialRole.Output, output.Physical.Pin(), 1),
                 new(RecipeMaterialRole.Waste, offcuts.Physical.Pin(), 3)]), [wood, offcuts, output], oldRecipe.Allocations);
        var definition = new ConstructionDefinition(previous.DefinitionId, "settlement_overhang", "Settlement collision overhang",
            recipe, 0, previous.RequiredTool, members, previous.RequiredWork, previous.WorkRate);
        var project = new ConstructionProject(previousProject.ProjectId, definition, previousProject.OwnerActorId,
            previousProject.BuilderActorId, previousProject.QuestId, previousProject.ProjectCustodianId,
            previousProject.ReservationId, previousProject.SiteFeet, previousProject.WorkFeet,
            previousProject.RequiredAccess);
        return (new(world.WorldId, world.BranchId, 0, [definition], [project]),
            new(world.WorldId, world.BranchId, 0,
                world.Production!.Recipes.Where(value => value.DefinitionId != recipe.DefinitionId).Append(recipe), [], []));
    }
}
