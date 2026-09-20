using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

/// <summary>
/// Fresh calibrated content with two independent funded commitments. Every later delivery, work unit,
/// climb, ordinary movement and excavation is committed through the real world authority.
/// </summary>
internal sealed class CoupledSettlementWorldFixture
{
    internal static readonly CellCoordinate Pedestal = new(106, 324, 0);
    internal static readonly CellCoordinate LandingCell = new(106, 324, -1);
    internal static readonly CellCoordinate NorthPit = new(106, 323, 0);
    internal static readonly SubunitPosition LowerOrigin = new(106 * 256, 324 * 256, 256);
    internal static readonly SubunitPosition UpperOrigin = new(106 * 256, 325 * 256, 512);
    internal static readonly SubunitPosition LowerWork = Feet(104, 324, 256);
    internal static readonly SubunitPosition DeckArrival = Feet(105, 324, 512);
    internal static readonly SubunitPosition UpperWork = Feet(107, 324, 512);
    internal static readonly SubunitPosition RiderFeet = Feet(106, 325, 576);
    internal static readonly SubunitPosition Stance = Feet(106, 323, 0);
    internal static readonly SubunitPosition NorthApproach = Feet(106, 322, 256);
    internal static readonly SubunitPosition PlayerParking = Feet(102, 327, 256);
    internal static readonly EntityId LowerProjectId = Id(1);
    internal static readonly EntityId UpperProjectId = Id(2);
    private readonly List<WorldSnapshot> preparationHistory = [];

    internal CoupledSettlementWorldFixture(int consequenceWorkBudget = 1, int maximumAssemblies = 2)
    {
        var original = new ConstructedSupportWorldFixture();
        Actions = original.Actions;
        Player = original.Player;
        Tool = original.Tool;
        WorldSnapshot farm = original.ExcavationGenesis;
        ConstructionProject previous = farm.Construction!.Projects.Single();
        Builder = previous.BuilderActorId;
        ConstructionDefinition lower = Definition(previous.Definition, false);
        ConstructionDefinition upper = Definition(previous.Definition, true);
        DeliveryQuestDefinition firstQuest = farm.Quests!.Definitions.Single();
        var secondQuest = new DeliveryQuestDefinition(Id(3), "coupled_upper_stock", "Upper platform supplies",
            "Deliver six additional wood for the upper platform.", Builder, firstQuest.MinimumLevel,
            firstQuest.Item, 6, Id(4), Id(5), firstQuest.Reward);
        var quests = new QuestSnapshot(farm.WorldId, farm.BranchId, 0, [firstQuest, secondQuest], []);
        var construction = new ConstructionSnapshot(farm.WorldId, farm.BranchId, 0, [lower, upper],
            [new(LowerProjectId, lower, Builder, Builder, firstQuest.DefinitionId, firstQuest.DestinationCustodianId,
                firstQuest.MaterialReservationId, LowerOrigin, LowerWork,
                [new(Builder, LowerWork, Feet(104, 325, 256)), new(Player, PlayerParking, PlayerParking)]),
             new(UpperProjectId, upper, Builder, Builder, secondQuest.DefinitionId, secondQuest.DestinationCustodianId,
                secondQuest.MaterialReservationId, UpperOrigin, UpperWork,
                [new(Builder, UpperWork, Feet(107, 325, 512))])]);
        var production = new ProductionSnapshot(farm.WorldId, farm.BranchId, 0,
            farm.Production!.Recipes.Where(recipe => recipe.DefinitionId != previous.Definition.MaterialTransform.DefinitionId)
                .Concat([lower.MaterialTransform, upper.MaterialTransform]), [], []);

        EntityId bag = farm.GetResident(Player).InventoryCustodianId;
        ulong initialWood = farm.Inventory.Custody.Lots.Where(lot => lot.CustodianId == bag && lot.Definition == firstQuest.Item)
            .Aggregate(0UL, (sum, lot) => checked(sum + lot.Quantity));
        Assert.InRange(initialWood, 1UL, 11UL);
        ulong extraWood = 12 - initialWood;
        var custody = new CustodySnapshot(farm.WorldId, farm.BranchId, 0,
            farm.Inventory.Custody.Storages.Select(storage => storage.CustodianId == bag
                ? new CustodianStorage(storage.CustodianId, checked(storage.UnitCapacity + extraWood), 0) : storage)
                .Append(new(Id(4), 6, 0)),
            farm.Inventory.Custody.Lots.Append(new(Id(7), bag, firstQuest.Item, new(Id(6), 0, extraWood), 0)));
        var inventory = new CountedAuthorityState(custody, checked(farm.Inventory.AdmittedUnitCapacity + extraWood),
            farm.Inventory.Actors, farm.Inventory.Controls,
            farm.Inventory.Policies.Append(new(Id(4), Builder, CustodyDepositRule.AnyControlledActor)),
            farm.Inventory.Sources.Append(new(Id(6), firstQuest.Item, extraWood)), farm.Inventory.ReservationIds.Append(Id(5)));
        CurrencyLedger oldCurrency = farm.Progression!.Currency;
        var currency = new CurrencyLedger(farm.WorldId, farm.BranchId, 0, oldCurrency.Currency,
            checked(oldCurrency.AdmittedSupply + firstQuest.Reward.CurrencyAmount),
            oldCurrency.Accounts.Select(account => account.AccountId == firstQuest.Reward.ReserveAccountId
                ? new CurrencyAccount(account.AccountId, account.Kind, account.ActorId,
                    checked(account.Balance + firstQuest.Reward.CurrencyAmount), 0) : account));
        var progression = new ProgressionSnapshot(farm.WorldId, farm.BranchId, 0, farm.Progression.Profiles,
            farm.Progression.Curves, farm.Progression.Actors, currency);

        TerrainRegionSnapshot surface = farm.Space.Regions.Single(region => region.Bounds.Contains(Pedestal));
        CellCoordinate[] holes = Enumerable.Range(105, 3).SelectMany(x => Enumerable.Range(324, 2)
            .Select(y => new CellCoordinate(x, y, 0))).Where(cell => cell != Pedestal).Append(NorthPit).ToArray();
        TerrainRegionSnapshot carved = surface.Excavate(surface.Revision, holes);
        TerrainRegionSnapshot lowerRegion = new StratifiedTerrainGenerator().Generate(farm.Space.Scope,
            surface.Seed, surface.Rules, new(new(104, 322, -2), 5, 5, 2));
        ActorPresence[] actors = farm.Space.Actors.Select(actor => new ActorPresence(actor.Binding, actor.Condition,
            actor.Scope, actor.ActorId == Builder ? Feet(104, 325, 256) : Feet(102, 325, 256), 0, actor.HomePlaceId)).ToArray();
        WorldResident[] residents = farm.Residents.Select(resident => new WorldResident(resident.ActorId,
            resident.InventoryCustodianId, resident.DisplayName, resident.ControllerKind,
            actors.Single(actor => actor.ActorId == resident.ActorId).Feet, 0,
            resident.Comprehension, resident.ExperiencedTime, resident.OpeningUtterance)).ToArray();
        var space = new LocalSpatialSnapshot(farm.Space.Scope,
            farm.Space.Regions.Select(region => ReferenceEquals(region, surface) ? carved : region).Append(lowerRegion), actors);
        TerrainCell pedestal = surface.GetCell(Pedestal);
        Site = new(original.Site.SiteId, Pedestal, pedestal.SourceId!.Value, original.Site.CountedSourceId,
            Player, original.Site.Material, original.Site.RequiredTool, 1, 0, Stance);
        var basis = new WorldSnapshot(inventory, space, residents, farm.AdmissionVersion, farm.Calendar, 0, 0,
            gathering: farm.Gathering, production: production, progression: progression, quests: quests, construction: construction);
        WorldSnapshot physical = WorldConstructionPhysicalAdmission.CreateGenesis(WorldSteppedSupportAdmission.CreateGenesis(
            WorldConstructedSupportAdmission.CreateGenesis(WorldExcavationAdmission.CreateGenesis(basis, new([Site])))));
        WorldSnapshot structural = WorldStructuralPolicyAdmission.CreateGenesis(physical, PolicyFor(physical));
        LegacyGenesis16 = WorldConsequenceAdmission.CreateGenesis(WorldStructuralSettlementAdmission.CreateGenesis(structural,
            new StructuralSettlementPolicy(256, 256, 256, 9800)), new ConsequenceExecutionPolicy(consequenceWorkBudget));
        Genesis = WorldCoupledSettlementAdmission.CreateGenesis(LegacyGenesis16, new CoupledSettlementPolicy(maximumAssemblies));
    }

    internal ConstructionUpgradeFixture Actions { get; }
    internal WorldSnapshot Genesis { get; }
    internal WorldSnapshot LegacyGenesis16 { get; }
    internal EntityId Player { get; }
    internal EntityId Builder { get; }
    internal EntityId Tool { get; }
    internal ExcavationSite Site { get; }
    internal IReadOnlyList<WorldSnapshot> PreparationHistory => preparationHistory;

    internal WorldSnapshot PrepareBeforeExcavation(string path)
    {
        WorldSnapshot world = Actions.CreateSource(path, genesis: Genesis);
        preparationHistory.Add(world);
        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(path);
        world = WalkTo(authority, world, Player, Feet(103, 325, 256));
        Assert.Equal(SpatialInteraction.Reachable, world.Space.QueryInteraction(Player, Builder));
        world = Actions.Act(authority, world, Player, new SpeakToActorIntent(Builder, world.Space.GetActor(Builder).Revision));
        foreach (DeliveryQuestDefinition quest in world.Quests!.Definitions)
        {
            world = Actions.Act(authority, world, Player, new AcceptDeliveryQuestIntent(quest.DefinitionId, quest.Version,
                Builder, world.Space.GetActor(Builder).Revision, 0, world.Progression!.GetActor(Player).Revision));
            ulong remaining = quest.Quantity;
            List<SourceLotSelection> selections = [];
            EntityId bag = world.GetResident(Player).InventoryCustodianId;
            foreach (CountedSourceLot lot in world.Inventory.Custody.Lots.Where(lot => lot.CustodianId == bag
                && lot.Definition == quest.Item && lot.ReservedBy is null))
            {
                ulong quantity = Math.Min(lot.Quantity, remaining);
                if (quantity != 0) { selections.Add(new(lot.LotId, lot.Revision, quantity)); }
                remaining -= quantity;
            }
            Assert.Equal(0UL, remaining);
            world = Actions.Act(authority, world, Player, new CompleteDeliveryQuestIntent(quest.DefinitionId, quest.Version,
                Builder, world.Space.GetActor(Builder).Revision,
                world.Quests!.FindParticipation(Player, quest.DefinitionId)!.Revision,
                world.Progression!.GetActor(Player).Revision, world.Inventory.Custody.GetStorage(bag).Revision, selections));
        }
        Assert.Equal(400UL, world.Progression!.Currency.GetActorAccount(Player).Balance);
        Assert.All(world.Quests!.Participations, participation => Assert.Equal(ActorQuestStatus.Completed, participation.Status));
        world = Actions.Act(authority, world, Player, new CloseDialogueIntent(world.Dialogues.Single().DialogueId));
        world = WalkTo(authority, world, Player, PlayerParking);
        preparationHistory.Add(world);
        world = Build(authority, world, LowerProjectId);
        preparationHistory.Add(world);
        Assert.Equal(LowerWork, world.Space.GetActor(Builder).Feet);
        world = EarnEffort(authority, world, Builder);
        world = Actions.Act(authority, world, Builder, new ClimbIntent(256, 0, 256));
        Assert.Equal(DeckArrival, world.Space.GetActor(Builder).Feet);
        preparationHistory.Add(world);
        world = Build(authority, world, UpperProjectId);
        preparationHistory.Add(world);
        world = WalkTo(authority, world, Builder, RiderFeet);
        world = WalkTo(authority, world, Player, NorthApproach);
        world = EarnEffort(authority, world, Player);
        world = Actions.Act(authority, world, Player, new ClimbIntent(0, 256, -256));
        Assert.Equal(Stance, world.Space.GetActor(Player).Feet);
        Assert.Equal(RiderFeet, world.Space.GetActor(Builder).Feet);
        world = EarnEffort(authority, world, Player);
        Assert.All(world.Construction!.Projects, project => Assert.True(project.IsCompleted));
        Assert.Equal(2, world.Production!.Batches.Count);
        Assert.Equal(3, world.ConstructionPhysical!.Members.Count);
        Assert.All(world.ConstructionPhysical.Members, member => Assert.Equal(default, member.Displacement));
        Assert.Equal(0UL, world.Excavation!.Sites.Single().CompletedWork);
        preparationHistory.Add(world);
        return world;
    }

    internal WorldStepBatch NextExcavation(WorldSnapshot world)
    {
        ExcavationSite site = world.Excavation!.Sites.Single();
        TerrainRegionSnapshot region = world.Space.Regions.Single(value => value.Bounds.Contains(site.Cell));
        CountedSourceLot tool = world.Inventory.Custody.GetLot(Tool);
        TrustedCommandDelivery delivery = ConstructionUpgradeFixture.Delivery(world, Player);
        var action = new WorldActionCommand(Actions.NextIdentity(), delivery.PrincipalId, delivery.SessionId,
            Player, world.WorldId, world.BranchId, world.Space.GetActor(Player).Revision,
            new ExcavateIntent(site.SiteId, site.Revision, region.Revision, Tool, tool.Revision,
                world.Inventory.Custody.GetStorage(world.GetResident(Player).InventoryCustodianId).Revision));
        return Actions.Batch(world, [action], []);
    }

    internal WorldStepBatch Continue(WorldSnapshot world, EntityId? cause = null) => new(cause ?? Actions.NextIdentity(),
        world.WorldId, world.BranchId, world.Revision, world.AdvancingSteps,
        WorldBatchKind.ConsequenceContinuation, [], [], world.ProfileVersion);

    private WorldSnapshot Build(SqliteWorldAuthority authority, WorldSnapshot world, EntityId projectId)
    {
        var controller = new LocalNpcController();
        for (int step = 0; !world.Construction!.FindProject(projectId)!.IsCompleted; step++)
        {
            Assert.True(step < 256, "The admitted NPC must approach and perform each funded construction commitment.");
            WorldStepDecision result = authority.Commit(Actions.Batch(world,
                controller.SelectActions(world, Actions.NextIdentity), controller.SelectMovement(world)));
            Assert.All(result.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
            world = result.Candidate;
        }
        return world;
    }

    private WorldSnapshot WalkTo(SqliteWorldAuthority authority, WorldSnapshot world, EntityId actorId, SubunitPosition destination)
    {
        for (int step = 0; world.Space.GetActor(actorId).Feet != destination; step++)
        {
            Assert.True(step < 512, "All fixture travel must finish through finite ordinary walking samples.");
            GroundRouteResult route = world.Space.QueryWalkingRoute(actorId, destination);
            Assert.True(route.Status == GroundRouteStatus.Reachable,
                $"Route from {world.Space.GetActor(actorId).Feet} to {destination} returned {route.Status} after {route.ExpandedNodes} expansions.");
            SubunitPosition next = Assert.IsType<SubunitPosition>(route.NextWaypoint);
            ActorPresence current = world.Space.GetActor(actorId);
            world = authority.Commit(Actions.Batch(world, [], [new(ConstructionUpgradeFixture.Delivery(world, actorId),
                actorId, next.X - current.Feet.X, next.Y - current.Feet.Y)])).Candidate;
            Assert.Equal(next, world.Space.GetActor(actorId).Feet);
        }
        return world;
    }

    private WorldSnapshot EarnEffort(SqliteWorldAuthority authority, WorldSnapshot world, EntityId actorId)
    {
        for (int step = 0; world.Gathering!.GetActorWork(actorId).Effort < GatherRulesV1.EffortPerSwing; step++)
        {
            Assert.True(step < 40, "Climb and excavation effort must be earned through ordinary advancing time.");
            world = authority.Commit(Actions.Batch(world, [], [])).Candidate;
        }
        return world;
    }

    private static ConstructionDefinition Definition(ConstructionDefinition previous, bool upper)
    {
        MillimetreBox[] members = upper ? [new(0, 0, 0, 1000, 1000, 250)]
            : [new(0, 0, 0, 1000, 1000, 750), new(-1000, 0, 750, 2000, 2000, 1000)];
        ItemPhysicalBinding wood = previous.MaterialTransform.Bindings.Single(binding => binding.Physical.Key == "wood");
        ItemPhysicalBinding offcuts = previous.MaterialTransform.Bindings.Single(binding => binding.Physical.Key == "wood_offcuts");
        ulong offset = upper ? 200UL : 100UL;
        string key = upper ? "coupled_upper" : "coupled_lower";
        var output = new ItemPhysicalBinding(new(Id(offset), ContentHash.Compute(System.Text.Encoding.UTF8.GetBytes(key))),
            new(wood.Physical.Package, Id(offset + 1), key, new(270_000),
                new(members.Aggregate(0UL, (sum, member) => checked(sum + member.VolumeMicrolitres))), new(1)));
        var recipe = new CraftRecipeDefinition(upper ? Id(offset + 2) : previous.MaterialTransform.DefinitionId,
            new(wood.Physical.Package, upper ? Id(offset + 3) : previous.MaterialTransform.PhysicalRecipe.DefinitionId,
                "assemble_" + key, new(1), null, new([wood.Physical, offcuts.Physical, output.Physical]),
                [new(RecipeMaterialRole.Input, wood.Physical.Pin(), 6), new(RecipeMaterialRole.Output, output.Physical.Pin(), 1),
                    new(RecipeMaterialRole.Waste, offcuts.Physical.Pin(), 3)]), [wood, offcuts, output],
            [new(wood.Item, 0, new(270_000)), new(wood.Item, 1, new(10_000))]);
        // Synthetic calibration isolates actual conserved backing and contact from gameplay material strength.
        return new(upper ? Id(offset + 4) : previous.DefinitionId, key, upper ? "Upper platform" : "Post and lower deck",
            recipe, 0, previous.RequiredTool, members, new(1), previous.WorkRate);
    }

    private static ConstructionLoadPolicy PolicyFor(WorldSnapshot world)
    {
        const ulong capacity = 10_000_000_000;
        ConstructionBearingTemplate[] templates = world.Construction!.Definitions.Select(definition =>
            new ConstructionBearingTemplate(definition.DefinitionId, definition.Version,
                definition.Members.Select((_, index) => new StructuralMemberRating((uint)index, new(capacity), 3000)),
                definition.Members.SelectMany((upper, upperIndex) => definition.Members.Select((lower, lowerIndex) => (lower, lowerIndex))
                    .Where(value => upper.MinZ > 0 && upper.MinZ == value.lower.MaxZ
                        && Math.Min(upper.MaxX, value.lower.MaxX) > Math.Max(upper.MinX, value.lower.MinX)
                        && Math.Min(upper.MaxY, value.lower.MaxY) > Math.Max(upper.MinY, value.lower.MinY))
                    .Select(value => new StructuralMemberJoint((uint)upperIndex, (uint)value.lowerIndex,
                        StructuralJointKind.VerticalBearing))))).ToArray();
        StableTerrainBearing[] foundations = world.Space.Regions.SelectMany(region => region.Cells)
            .Where(cell => cell.IsSolid && cell.Coordinate.Z is -1 or 0)
            .Select(cell => new StableTerrainBearing(EntityId.DeriveSpawned(cell.SourceId!.Value, 900000), cell.Coordinate,
                cell.SourceId.Value, cell.Material!, new(capacity))).ToArray();
        var bodies = world.Space.Actors.Select(actor => actor.Condition.Definition).DistinctBy(body => body.DefinitionId)
            .Select(body => new StructuralBodyMass(body.DefinitionId, body.Version, new(70_000_000)));
        var known = world.Production!.Recipes.SelectMany(recipe => recipe.Bindings).Select(binding => binding.Item).ToHashSet();
        var supplemental = world.Inventory.Sources.Select(source => source.Definition).Where(item => !known.Contains(item)).Distinct()
            .Select(item => new StructuralItemMass(item, world.Excavation!.Sites.Any(site => site.Material.Item == item)
                ? world.Excavation.Sites.First(site => site.Material.Item == item).Material.Physical.MassPerUnit : new(1_000_000)));
        return new(world.Space.Scope, templates, foundations, new(bodies, supplemental));
    }

    private static SubunitPosition Feet(long x, long y, long z) => new(x * 256 + 128, y * 256 + 128, z);
    private static EntityId Id(ulong ordinal) => new(0x636f75706c6564, ordinal);
}
