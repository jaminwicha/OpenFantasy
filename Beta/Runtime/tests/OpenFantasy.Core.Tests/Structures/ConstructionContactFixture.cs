using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Tests.Construction;
using OpenFantasy.Core.Tests.Gathering;
using OpenFantasy.Core.Tests.Quests;
using OpenFantasy.Core.Tests.Worlds;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Structures;

/// <summary>
/// Two funded deliveries and actual fabrication steps supply the backing. Initial separated poses,
/// terrain and the diagnostic stacked endpoint are supplied domain data, not a played trajectory,
/// elevated construction admission, impact result or published world transition.
/// </summary>
internal sealed class ConstructionContactFixture
{
    internal static readonly EntityId Player = AdmissionFixture.Id(10);
    internal static readonly EntityId Builder = AdmissionFixture.Id(11);
    internal static readonly CellCoordinate Pedestal = new(0, 0, 1);
    internal static readonly ContentHash Pin = ContentHash.Compute("construction-contact-fixture"u8);
    private static readonly EntityId SecondCustodian = Id(1);
    private static readonly EntityId SecondReservation = Id(2);

    private ConstructionContactFixture(WorldSnapshot basis)
    {
        Basis = basis;
        Lower = basis.Construction!.Projects.Single(project => project.SiteFeet.Z == 0);
        Upper = basis.Construction.Projects.Single(project => project.SiteFeet.Z == 512);
    }

    internal WorldSnapshot Basis { get; }
    internal ConstructionProject Lower { get; }
    internal ConstructionProject Upper { get; }
    internal ConstructionLoadPolicy Policy => Basis.StructuralPolicy!;
    internal EntityId LowerBase => ConstructionBearingModel.MemberIdentity(Lower.Installed!.StructureId, 0);
    internal EntityId LowerTop => ConstructionBearingModel.MemberIdentity(Lower.Installed!.StructureId, 1);
    internal EntityId UpperBase => ConstructionBearingModel.MemberIdentity(Upper.Installed!.StructureId, 0);
    internal EntityId UpperTop => ConstructionBearingModel.MemberIdentity(Upper.Installed!.StructureId, 1);

    internal static ConstructionContactFixture Create(int upperOffsetX = 0)
    {
        WorldSnapshot fabricated = FabricateBoth();
        TerrainRegionSnapshot terrain = Terrain(fabricated.Space.Scope, upperOffsetX);
        ConstructionProject[] projects = fabricated.Construction!.Projects.Select((project, index) =>
        {
            SubunitPosition site = new(index == 0 ? 0 : upperOffsetX, 0, index == 0 ? 0 : 512);
            SubunitPosition work = site.Offset(-256, 0, 0);
            return new ConstructionProject(project.ProjectId, project.Definition, project.OwnerActorId,
                project.BuilderActorId, project.QuestId, project.ProjectCustodianId, project.ReservationId,
                site, work, [new(Builder, work, work)], project.Revision, project.Progress, project.Installed);
        }).ToArray();
        var construction = new ConstructionSnapshot(fabricated.WorldId, fabricated.BranchId,
            fabricated.Construction.Revision, fabricated.Construction.Definitions, projects);
        ConstructionPhysicalSnapshot physical = ConstructionPhysicalSnapshot.Create(construction);
        ActorPresence[] actors = fabricated.Space.Actors.Select(actor => At(actor,
            actor.ActorId == Player ? new(128 + upperOffsetX, 128, 576) : new(768, 0, 256))).ToArray();
        var space = new LocalSpatialSnapshot(fabricated.Space.Scope, [terrain], actors, physical.GetStructures(),
            GroundSupportMode.SteppedTerrainAndConstruction);

        // A distinct, still-unworked excavation entitlement supplies the complete dry-profile owner.
        TerrainCell cell = terrain.GetCell(new(3, 3, 0));
        var material = new MaterialDefinition(new("contact-fixture"), Id(20), "terrain",
            new(2_000_000), new(ExcavationSite.CellVolumeMicrolitres), new(1));
        var site = new ExcavationSite(Id(21), cell.Coordinate, cell.SourceId!.Value, Id(22), Player,
            new(cell.Material!, material), GatheringFixture.Axe, 1, 0, new(2 * 256 + 128, 3 * 256 + 128, 0));
        CountedAuthorityState inventory = new(fabricated.Inventory.Custody,
            checked(fabricated.Inventory.AdmittedUnitCapacity + site.Quantity), fabricated.Inventory.Actors,
            fabricated.Inventory.Controls, fabricated.Inventory.Policies,
            fabricated.Inventory.Sources.Append(new(site.CountedSourceId, site.Material.Item, site.Quantity)),
            fabricated.Inventory.ReservationIds);
        var excavation = new ExcavationSnapshot([site]);
        GatheringSnapshot gathering = new(fabricated.WorldId, fabricated.BranchId, fabricated.AdvancingSteps,
            fabricated.Gathering!.Nodes.Select(node => new GatherNodeState(node.NodeId, node.Definition,
                node.SourceId, node.UnitCapacity, new(1024, 1024, 256), node.Revision, node.NextUnissuedUnit,
                node.RemainingSwings, node.DepletedAtStep)), fabricated.Gathering.Tools, fabricated.Gathering.Actors);
        var profile13 = new WorldSnapshot(inventory, space, fabricated.Residents, fabricated.AdmissionVersion,
            fabricated.Calendar, fabricated.Revision, fabricated.AdvancingSteps, [], gathering,
            fabricated.Production, fabricated.Progression, fabricated.Quests, construction,
            excavation: excavation, constructionPhysical: physical);
        ConstructionLoadPolicy policy = PolicyFor(profile13);
        return new(new WorldSnapshot(inventory, space, profile13.Residents, profile13.AdmissionVersion,
            profile13.Calendar, profile13.Revision, profile13.AdvancingSteps, [], profile13.Gathering,
            profile13.Production, profile13.Progression, profile13.Quests, construction,
            excavation: excavation, constructionPhysical: physical, structuralPolicy: policy));
    }

    internal ConstructionContactScene Stack(bool rider = true, long gap = 0)
    {
        TerrainRegionSnapshot original = Assert.Single(Basis.Space.Regions);
        CellCoordinate[] removed = Upper.SiteFeet.X == 0 ? [Pedestal]
            : [Pedestal, new(1, 0, 1), new(1, 0, 0)];
        TerrainRegionSnapshot terrain = original.Excavate(original.Revision, removed);
        ConstructionPhysicalSnapshot physical = Basis.ConstructionPhysical!.WithMembers(0,
            Basis.ConstructionPhysical.Members.Where(member => member.StructureId == Upper.Installed!.StructureId)
                .Select(member => member.WithState(0, member.ConditionParts,
                    new(0, 0, checked(-448 + gap)), ConstructionMemberDisposition.Resting)));
        ActorPresence[] actors = Basis.Space.Actors.Select(actor => actor.ActorId == Player
            ? At(actor, rider ? new(Upper.SiteFeet.X + 128, 128, checked(128 + gap)) : new(0, 512, 256), actor.Revision + 1)
            : actor).ToArray();
        return new(Basis, [terrain], physical, actors);
    }

    internal ConstructionLoadPolicy WithCapacity(ulong capacity)
    {
        ConstructionBearingTemplate original = Assert.Single(Policy.Templates);
        var template = new ConstructionBearingTemplate(original.DefinitionId, original.DefinitionVersion,
            original.Members.Select(member => member with { Capacity = new(capacity) }), original.Joints);
        return new(Policy.Scope, [template], Policy.Foundations, Policy.Masses);
    }

    internal static ActorPresence At(ActorPresence actor, SubunitPosition feet, ulong? revision = null) =>
        new(actor.Binding, actor.Condition, actor.Scope, feet, revision ?? actor.Revision, actor.HomePlaceId);

    internal static EntityId Id(ulong ordinal) => new(0x636f6e74616374, ordinal);

    private static WorldSnapshot FabricateBoth()
    {
        QuestFixture fixture = QuestFixture.Create(funds: 400);
        WorldSnapshot original = fixture.AsWorld();
        var inventory = new CountedAuthorityState(new CustodySnapshot(original.WorldId, original.BranchId,
            original.Inventory.Custody.Revision,
            original.Inventory.Custody.Storages.Append(new(SecondCustodian, 6, 0)),
            original.Inventory.Custody.Lots.Select(lot => lot.LotId == AdmissionFixture.Id(70)
                ? new CountedSourceLot(lot.LotId, lot.CustodianId, lot.Definition,
                    new(lot.Source.SourceId, 0, 12), lot.Revision) : lot)),
            original.Inventory.AdmittedUnitCapacity + 6, original.Inventory.Actors, original.Inventory.Controls,
            original.Inventory.Policies.Append(new(SecondCustodian, Builder, CustodyDepositRule.AnyControlledActor)),
            original.Inventory.Sources.Select(source => source.SourceId == AdmissionFixture.Id(71)
                ? new AdmittedCountedSource(source.SourceId, source.Definition, 12) : source),
            original.Inventory.ReservationIds.Append(SecondReservation));
        DeliveryQuestDefinition first = fixture.Definition;
        var second = new DeliveryQuestDefinition(Id(3), "second-contact-delivery", "Second assembly supplies",
            "Supply a second independent assembly.", Builder, 1, first.Item, 6, SecondCustodian,
            SecondReservation, first.Reward);
        var quests = new QuestSnapshot(original.WorldId, original.BranchId, 0, [first, second], []);
        // Remove the unrelated compression recipe, whose fixture wood calibration differs.
        var production = new ProductionSnapshot(original.WorldId, original.BranchId, 0, [], [], []);
        WorldSnapshot world = new(inventory, original.Space, original.Residents, original.AdmissionVersion,
            original.Calendar, original.Revision, original.AdvancingSteps, original.Dialogues,
            original.Gathering, production, original.Progression, quests);
        ulong command = 600_000;
        foreach (DeliveryQuestDefinition quest in quests.Definitions)
        {
            world = Quest(world, StepFixture.Action(world, command++, new AcceptDeliveryQuestIntent(
                quest.DefinitionId, quest.Version, Builder, world.Space.GetActor(Builder).Revision, 0,
                world.Progression!.GetActor(Player).Revision)));
            CountedSourceLot wood = world.Inventory.Custody.GetLot(AdmissionFixture.Id(70));
            world = Quest(world, StepFixture.Action(world, command++, new CompleteDeliveryQuestIntent(
                quest.DefinitionId, quest.Version, Builder, world.Space.GetActor(Builder).Revision,
                world.Quests!.FindParticipation(Player, quest.DefinitionId)!.Revision,
                world.Progression!.GetActor(Player).Revision,
                world.Inventory.Custody.GetStorage(AdmissionFixture.Id(60)).Revision,
                [new(wood.LotId, wood.Revision, 6)])));
        }
        world = new(world.Inventory, world.Space, world.Residents, world.AdmissionVersion, world.Calendar,
            world.Revision, world.AdvancingSteps, [], world.Gathering, world.Production, world.Progression, world.Quests);
        ConstructionDefinition definition = Definition();
        SubunitPosition work = world.Space.GetActor(Builder).Feet;
        ConstructionProject[] projects = quests.Definitions.Select((quest, index) => new ConstructionProject(
            Id((ulong)(30 + index)), definition, Builder, Builder, quest.DefinitionId,
            quest.DestinationCustodianId, quest.MaterialReservationId, new(768, index == 0 ? -256 : 256, 256),
            work, [new(Builder, work, new(512, 256, 256)), new(Player, new(0, 0, 256), new(0, 256, 256))])).ToArray();
        var construction = new ConstructionSnapshot(world.WorldId, world.BranchId, 0, [definition], projects);
        var tool = new ConstructionToolAdmission(Builder, AdmissionFixture.Id(61),
            new(Id(40), definition.RequiredTool.Item, 1), Id(41));
        var admission = new WorldConstructionAdmission(Pin, construction, [tool]);
        var request = new WorldConstructionMigration(Id(42), world.WorldId, world.BranchId, world.Revision,
            world.AdvancingSteps, world.ContentHash, world.AdmissionVersion,
            WorldConstructionMigration.ComputeTargetAdmissionVersion(world.AdmissionVersion, admission), admission);
        world = new WorldConstructionMigrationProcessor().Decide(world, request).Candidate;
        foreach (ConstructionProject project in projects)
        {
            var intent = new WorkConstructionIntent(project.ProjectId, 0,
                world.Inventory.Custody.GetStorage(project.ProjectCustodianId).Revision, tool.LotId,
                world.Inventory.Custody.GetLot(tool.LotId).Revision);
            var action = StepFixture.Action(world, command++, intent, 11);
            var batch = new WorldStepBatch(Id(command++), world.WorldId, world.BranchId, world.Revision,
                world.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [action], 5);
            WorldStepDecision decision = new WorldStepProcessor().Decide(world, batch);
            Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
            world = decision.Candidate;
        }
        Assert.All(world.Construction!.Projects, project => Assert.True(project.IsCompleted));
        Assert.All(world.Quests!.Participations, participation => Assert.Equal(ActorQuestStatus.Completed, participation.Status));
        Assert.Equal(2, world.Production!.Batches.Count);
        Assert.Equal(400UL, world.Progression!.Currency.GetAccount(AdmissionFixture.Id(2204)).Balance);
        return world;
    }

    private static WorldSnapshot Quest(WorldSnapshot world, WorldActionCommand command)
    {
        DeliveryQuestPlan plan = new DeliveryQuestPlanner().Plan(world, world.Inventory, world.Production!,
            world.Progression!, world.Quests!, command, world.Dialogues);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        return new(plan.CandidateInventory, world.Space, world.Residents, world.AdmissionVersion, world.Calendar,
            world.Revision + 1, world.AdvancingSteps, world.Dialogues, world.Gathering, world.Production,
            plan.CandidateProgression, plan.CandidateQuests);
    }

    private static ConstructionDefinition Definition()
    {
        ConstructionDefinition old = ConstructionFixture.Definition(sourceItem: AdmissionFixture.Material);
        var package = new PackageId("contact-fixture");
        ItemPhysicalBinding wood = old.MaterialTransform.BindingFor(AdmissionFixture.Material);
        ItemPhysicalBinding oldOutput = old.MaterialTransform.BindingFor(old.MaterialTransform.ItemFor(old.MaterialTransform.Outputs[0]));
        ItemPhysicalBinding waste = old.MaterialTransform.BindingFor(old.MaterialTransform.ItemFor(old.MaterialTransform.Outputs[1]));
        MillimetreBox[] members = [new(0, 0, 0, 1000, 1000, 125), new(0, 0, 125, 1000, 1000, 250)];
        var output = new MaterialDefinition(package, oldOutput.Physical.DefinitionId, "contact-slab",
            new(270_000), new(250_000_000), new(1));
        var physical = new MaterialRecipeDefinition(package, old.MaterialTransform.PhysicalRecipe.DefinitionId,
            "contact-assembly", new(1), null, new MaterialCatalog([wood.Physical, output, waste.Physical]),
            [new(RecipeMaterialRole.Input, wood.Physical.Pin(), 6), new(RecipeMaterialRole.Output, output.Pin(), 1),
                new(RecipeMaterialRole.Waste, waste.Physical.Pin(), 3)]);
        var recipe = new CraftRecipeDefinition(old.MaterialTransform.DefinitionId, physical,
            [wood, new(oldOutput.Item, output), waste], [new(wood.Item, 0, new(270_000)), new(wood.Item, 1, new(10_000))]);
        return new(old.DefinitionId, "test/contact-slab", "Contact slab", recipe, 0, old.RequiredTool,
            members, new(1), old.WorkRate);
    }

    private static TerrainRegionSnapshot Terrain(TerrainScope scope, int upperOffsetX)
    {
        var material = new CountedItemDefinition(Id(19), Pin);
        var rules = new StratifiedTerrainRules(1, 0, 1, new(material, material, material));
        TerrainRegionSnapshot region = new StratifiedTerrainGenerator().Generate(scope, Id(18), rules,
            new(new(-2, -2, -1), 8, 8, 7));
        CellCoordinate[] holes = region.Cells.Where(cell => cell.IsSolid
            && (cell.Coordinate.Z == 1 && cell.Coordinate != Pedestal
                && (upperOffsetX == 0 || cell.Coordinate != new CellCoordinate(1, 0, 1))
                || cell.Coordinate == new CellCoordinate(0, 0, 0)))
            .Select(cell => cell.Coordinate).ToArray();
        return region.Excavate(region.Revision, holes);
    }

    private static ConstructionLoadPolicy PolicyFor(WorldSnapshot world)
    {
        ConstructionDefinition definition = Assert.Single(world.Construction!.Definitions);
        var template = new ConstructionBearingTemplate(definition.DefinitionId, definition.Version,
            definition.Members.Select((_, index) => new StructuralMemberRating((uint)index, new(10_000_000), 1000)),
            [new(1, 0, StructuralJointKind.VerticalBearing)]);
        StableTerrainBearing[] foundations = world.Space.Regions.SelectMany(region => region.Cells)
            .Where(cell => cell.IsSolid).Select((cell, index) => new StableTerrainBearing(Id((ulong)(1000 + index)),
                cell.Coordinate, cell.SourceId!.Value, cell.Material!, new(100_000_000))).ToArray();
        var bodies = world.Space.Actors.Select(actor => new StructuralBodyMass(actor.Binding.DefinitionId,
            actor.Binding.DefinitionVersion, new(1_000))).DistinctBy(body => body.DefinitionId);
        var masses = new StructuralMassCatalog(bodies,
            [new(GatheringFixture.Axe, new(7)), new(definition.RequiredTool.Item, new(11)),
                new(new CountedItemDefinition(Id(90), Pin), new(3))]);
        return new(world.Space.Scope, [template], foundations, masses);
    }
}
