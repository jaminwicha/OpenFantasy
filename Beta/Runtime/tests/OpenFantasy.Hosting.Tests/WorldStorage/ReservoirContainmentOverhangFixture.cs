using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

/// <summary>
/// Explicit fresh pits retain one generator/seed and intact separating terrain. A ground-founded
/// post carries an overhang above the wall; its hanging end enters the otherwise sealed basin.
/// Synthetic stock calibrates exact geometry and provenance, not pressure strength or joinery balance.
/// </summary>
internal sealed class ReservoirContainmentOverhangFixture
{
    private static readonly SubunitPosition Site = new(-2 * 256, 0, -2 * 256);
    private static readonly SubunitPosition Work = Feet(-3, 0);
    private static readonly SubunitPosition BuilderHome = Feet(-3, 1);
    private static readonly SubunitPosition PlayerHome = Feet(-4, 2);

    internal ReservoirWorldFixture Materials { get; } = new();
    internal ReservoirSealPolicy Policy { get; }
    internal WorldSnapshot Genesis { get; }
    internal EntityId Player => Genesis.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player).ActorId;
    internal EntityId Builder => Genesis.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Host).ActorId;

    internal ReservoirContainmentOverhangFixture(bool contained)
    {
        WorldSnapshot basis = Materials.Basis;
        var prior = basis.Construction!.Projects.Single();
        var old = prior.Definition;
        MillimetreBox[] members =
        [
            new(0, 0, 0, 250, 250, 3000),
            new(0, 0, 3000, 3000, 250, 3250),
            new(2000, 0, 1000, 2250, 250, 3000)
        ];
        ItemPhysicalBinding wood = old.MaterialTransform.Bindings.Single(binding => binding.Physical.Key == "wood");
        ItemPhysicalBinding offcut = old.MaterialTransform.Bindings.Single(binding => binding.Physical.Key == "wood_offcuts");
        var stock = new ItemPhysicalBinding(new(ReservoirWorldFixture.Id(550), ContentHash.Compute("contained-overhang-stock-v1"u8)),
            new(wood.Physical.Package, ReservoirWorldFixture.Id(551), "contained_overhang_stock", new(270000),
                new(members.Aggregate(0UL, (sum, member) => sum + member.VolumeMicrolitres)), new(1)));
        var recipe = new CraftRecipeDefinition(old.MaterialTransform.DefinitionId,
            new(wood.Physical.Package, old.MaterialTransform.PhysicalRecipe.DefinitionId, "assemble_containment_overhang",
                new(1), null, new([wood.Physical, offcut.Physical, stock.Physical]),
                [new(RecipeMaterialRole.Input, wood.Physical.Pin(), 6), new(RecipeMaterialRole.Output, stock.Physical.Pin(), 1),
                 new(RecipeMaterialRole.Waste, offcut.Physical.Pin(), 3)]),
            [wood, offcut, stock], [new(wood.Item, 0, new(270000)), new(wood.Item, 1, new(10000))]);
        var definition = new ConstructionDefinition(old.DefinitionId, "containment_overhang", "Containment overhang",
            recipe, 0, old.RequiredTool, members, new(2), old.WorkRate);
        EntityId playerId = basis.Residents.Single(resident => resident.ControllerKind == ResidentControllerKind.Player).ActorId;
        var project = new ConstructionProject(prior.ProjectId, definition, prior.OwnerActorId, prior.BuilderActorId,
            prior.QuestId, prior.ProjectCustodianId, prior.ReservationId, Site, Work,
            [new(prior.BuilderActorId, Work, BuilderHome), new(playerId, PlayerHome, PlayerHome)]);
        var construction = new ConstructionSnapshot(basis.WorldId, basis.BranchId, 0, [definition], [project]);
        var production = new ProductionSnapshot(basis.WorldId, basis.BranchId, 0,
            basis.Production!.Recipes.Where(value => value.DefinitionId != recipe.DefinitionId).Append(recipe), [], []);

        TerrainRegionSnapshot retained = basis.Space.Regions[0];
        var region = new StratifiedTerrainGenerator().Generate(basis.Space.Scope, retained.Seed, retained.Rules,
            new(new(-5, -2, -4), 11, 8, 8));
        var holes = (from x in Enumerable.Range(-4, 3)
                     from y in Enumerable.Range(-1, 5)
                     from z in Enumerable.Range(-2, 3)
                     select new CellCoordinate(x, y, z))
            .Concat([new(0, 0, -2), new(0, 0, -1), new(0, 0, 0), new(3, 0, -2), new(3, 0, -1)]).ToArray();
        region = region.Excavate(0, holes);
        Assert.True(region.GetCell(new(-1, 0, -1)).IsSolid);
        Assert.True(region.GetCell(new(-1, 0, 0)).IsSolid);
        Assert.True(region.GetCell(new(0, 0, -3)).IsSolid);
        var actors = basis.Space.Actors.Select(actor => new ActorPresence(actor.Binding, actor.Condition, actor.Scope,
            actor.ActorId == playerId ? PlayerHome : BuilderHome, 0, actor.HomePlaceId));
        var space = new LocalSpatialSnapshot(basis.Space.Scope, basis.Space.Regions.Append(region), actors);
        Assert.Equal(SpatialInteraction.Reachable,
            space.QueryInteraction(playerId, prior.BuilderActorId));
        Assert.Equal(GroundRouteStatus.Reachable,
            space.QueryGroundRoute(prior.BuilderActorId, Work).Status);
        var residents = basis.Residents.Select(resident => new WorldResident(resident.ActorId, resident.InventoryCustodianId,
            resident.DisplayName, resident.ControllerKind, resident.ActorId == playerId ? PlayerHome : BuilderHome,
            0, resident.Comprehension, resident.ExperiencedTime, resident.OpeningUtterance));
        var authored = new WorldSnapshot(basis.Inventory, space, residents, basis.AdmissionVersion, basis.Calendar, 0, 0,
            gathering: basis.Gathering, production: production, progression: basis.Progression, quests: basis.Quests,
            construction: construction);
        WatershedRunoffRequest request = Materials.Reservoirs.Capacity.Pending.Single().Request;
        var capacity = WatershedRunoffCapacityState.Create(new WatershedRunoffPlanner().Prepare(new(request.Network,
            request.Mapping, authored.Inventory, production, request.ReservationId, request.OwnerActorId, request.Start,
            request.End, request.Material, request.Inputs, request.Terminals), TestContext.Current.CancellationToken));
        var reservoirs = LiquidReservoirState.Create(capacity,
            [new(space.Scope, ReservoirBoundaryFixture.SourceReservoir, ReservoirWorldFixture.SourceStore, 0, [new(0, 0, -2)]),
             new(space.Scope, ReservoirWorldFixture.Id(31), ReservoirWorldFixture.Receiver, 0, [new(3, 0, -2)])]);
        WorldSnapshot legacy = WorldReservoirAdmission.CreateGenesis(authored, reservoirs);
        Policy = new(ReservoirWorldFixture.Water, new[]
            { retained.Rules.Materials.Surface, retained.Rules.Materials.Soil, retained.Rules.Materials.Stone }
            .Distinct().Select(material => new ReservoirSealRule(material, 1000)));
        Assert.True(new ReservoirBoundaryEvaluator().Assess(legacy, Policy, TestContext.Current.CancellationToken).IsContained);
        Genesis = contained ? WorldReservoirContainmentAdmission.CreateGenesis(legacy, Policy) : legacy;
    }

    internal WorldSnapshot PrepareLastWork(string path)
    {
        WorldSnapshot state = Materials.Actions.CreateSource(path, completeQuest: true, genesis: Genesis);
        using SqliteWorldAuthority authority = SqliteWorldAuthority.OpenExisting(path);
        state = Materials.Actions.Act(authority, state, Player, new CloseDialogueIntent(Assert.Single(state.Dialogues).DialogueId));
        var controller = new LocalNpcController();
        for (int step = 0; state.Construction!.Projects.Single().Progress.CompletedUnits == 0; step++)
        {
            Assert.True(step < 32, "The nearby funded builder must reach its stance and earn the first work unit.");
            state = authority.Commit(Materials.Actions.Batch(state,
                controller.SelectActions(state, Materials.Actions.NextIdentity), controller.SelectMovement(state))).Candidate;
        }
        Assert.Equal(1UL, state.Construction!.Projects.Single().Progress.CompletedUnits);
        Assert.Null(state.Construction!.Projects.Single().Installed);
        Assert.Equal(Work, state.Space.GetActor(Builder).Feet);
        Assert.True(state.Space.GetActor(Builder).Revision > 0);
        Assert.Empty(state.Production!.Batches);
        return state;
    }

    internal WorldActionCommand LastWork(WorldSnapshot state) => Assert.Single(new LocalNpcController()
        .SelectActions(state, Materials.Actions.NextIdentity));

    private static SubunitPosition Feet(long x, long y) => new(x * 256 + 128, y * 256 + 128, -2 * 256);
}
