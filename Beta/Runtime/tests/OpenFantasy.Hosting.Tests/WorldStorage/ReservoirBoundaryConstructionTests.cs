using OpenFantasy.Core;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ReservoirBoundaryConstructionTests
{
    [Theory]
    [InlineData("sealed", true)]
    [InlineData("slit", false)]
    [InlineData("displaced", false)]
    [InlineData("intruding", false)]
    public void OnlyCompletedMaterialWithContinuousExactFacesCanContainTheNominalVessel(string variant, bool contained)
    {
        var f = new ReservoirBoundaryFixture();
        var (genesis, policy) = WithTankProject(f, variant);
        using var files = new StorageFiles();
        var unbuilt = new ReservoirBoundaryEvaluator().Assess(genesis, policy, TestContext.Current.CancellationToken);
        Assert.False(unbuilt.IsContained);
        Assert.Equal(0, unbuilt.Results.Sum(r => r.ConstructedSealedFaces));
        var world = f.Materials.Actions.CreateSource(files.Path, completeQuest: true, genesis: genesis);
        byte[] before = WorldSnapshotCodec.Encode(world);
        ulong beforeWorkRevision;
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            var player = world.Residents.Single(r => r.ControllerKind == ResidentControllerKind.Player).ActorId;
            world = f.Materials.Actions.Act(authority, world, player, new CloseDialogueIntent(world.Dialogues.Single().DialogueId));
            beforeWorkRevision = world.Revision;
            var controller = new LocalNpcController();
            for (int i = 0; world.Construction!.Projects[0].Installed is null; i++)
            {
                Assert.True(i < 128, "The bounded tank project must be reached and completed by actual NPC work.");
                world = authority.Commit(f.Materials.Actions.Batch(world,
                    controller.SelectActions(world, f.Materials.Actions.NextIdentity), controller.SelectMovement(world))).Candidate;
            }
            Assert.Single(world.Production!.Batches);
            Assert.Equal(1UL, world.Construction.Projects[0].Progress.CompletedUnits);
            Assert.Single(world.Space.Structures);
            Assert.NotEqual(before, WorldSnapshotCodec.Encode(world));
            var report = new ReservoirBoundaryEvaluator().Assess(world, policy, TestContext.Current.CancellationToken);
            var tank = report.Results.Single(r => r.ReservoirId == ReservoirBoundaryFixture.SourceReservoir);
            Assert.Equal(contained, report.IsContained);
            Assert.Equal(contained ? 8 : 6, tank.ConstructedSealedFaces);
            Assert.Equal(1, tank.TerrainSealedFaces);
            if (variant == "intruding")
            {
                Assert.Equal(2, tank.Failures[ReservoirBoundaryFailure.InteriorConstruction]);
            }
            if (contained)
            {
                var tooThin = new ReservoirSealPolicy(policy.Liquid, policy.Rules.Select(r =>
                    new ReservoirSealRule(r.Material, r.MinimumThicknessMillimetres == 1 ? 2U : r.MinimumThicknessMillimetres)));
                Assert.False(new ReservoirBoundaryEvaluator().Assess(world, tooThin, TestContext.Current.CancellationToken).IsContained);
                // The installed output, not its input wood, is the sealing material pin.
                var inputOnly = new ReservoirSealPolicy(policy.Liquid, f.Policy.Rules.Append(new(
                    world.Construction.Projects[0].Definition.MaterialTransform.ItemFor(world.Construction.Projects[0].Definition.MaterialTransform.Inputs[0]), 1)));
                Assert.False(new ReservoirBoundaryEvaluator().Assess(world, inputOnly, TestContext.Current.CancellationToken).IsContained);
            }
            foreach (ulong revision in new[] { world.Revision, beforeWorkRevision, world.Revision })
            {
                var historical = authority.ReadRecordedStateForHost(revision).State;
                Assert.Equal(revision == world.Revision && contained,
                    new ReservoirBoundaryEvaluator().Assess(historical, policy, TestContext.Current.CancellationToken).IsContained);
            }
        }
        byte[] saved = File.ReadAllBytes(files.Path);
        using (var reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(contained, new ReservoirBoundaryEvaluator().Assess(reopened.InspectForHost().State,
                policy, TestContext.Current.CancellationToken).IsContained);
        }
        Assert.Equal(saved, File.ReadAllBytes(files.Path));
        string? directory = Environment.GetEnvironmentVariable("OPENFANTASY_RESERVOIR_BOUNDARY_EVIDENCE");
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
            File.Copy(files.Path, Path.Combine(directory, variant + "-construction.sqlite"), true);
            File.WriteAllBytes(Path.Combine(directory, variant + "-policy.bin"), ReservoirSealPolicyCodecV1.Encode(policy));
            File.WriteAllText(Path.Combine(directory, variant + "-revisions.json"), System.Text.Json.JsonSerializer.Serialize(new
            {
                BeforeWorkRevision = beforeWorkRevision, CompletedRevision = world.Revision, IsContained = contained,
                WorldHash = world.ContentHash.ToString(), ReservoirHash = world.Reservoirs!.ContentHash.ToString(), PolicyHash = policy.Version.ToString()
            }));
        }
    }

    private static (WorldSnapshot World, ReservoirSealPolicy Policy) WithTankProject(ReservoirBoundaryFixture fixture, string variant)
    {
        var basis = fixture.World;
        var oldProject = basis.Construction!.Projects[0];
        var old = oldProject.Definition;
        List<MillimetreBox> members = variant == "slit"
            ? [new(-1, 0, 0, 0, 499, 2000), new(-1, 500, 0, 0, 1000, 2000)]
            : [new(variant == "displaced" ? -2 : variant == "intruding" ? 0 : -1, 0, 0,
                variant == "displaced" ? -1 : variant == "intruding" ? 1 : 0, 1000, 2000)];
        long left = variant == "displaced" ? -2 : -1;
        members.AddRange([new(1000, 0, 0, 1001, 1000, 2000), new(left, -1, 0, 1001, 0, 2000),
            new(left, 1000, 0, 1001, 1001, 2000)]);
        var wood = old.MaterialTransform.Bindings.Single(b => b.Physical.Key == "wood");
        var offcut = old.MaterialTransform.Bindings.Single(b => b.Physical.Key == "wood_offcuts");
        var sheet = new ItemPhysicalBinding(new(ReservoirWorldFixture.Id(501), ContentHash.Compute(System.Text.Encoding.UTF8.GetBytes(variant))),
            new(wood.Physical.Package, ReservoirWorldFixture.Id(502), "boundary_fixture_sheet", new(270000),
                new(members.Aggregate(0UL, (n, b) => n + b.VolumeMicrolitres)), new(1)));
        // Synthetic calibrated sheet stock tests geometry and provenance; no pressure strength or production art is claimed.
        var recipe = new CraftRecipeDefinition(old.MaterialTransform.DefinitionId,
            new(wood.Physical.Package, old.MaterialTransform.PhysicalRecipe.DefinitionId, "assemble_boundary_fixture", new(1), null,
                new([wood.Physical, offcut.Physical, sheet.Physical]),
                [new(RecipeMaterialRole.Input, wood.Physical.Pin(), 6), new(RecipeMaterialRole.Output, sheet.Physical.Pin(), 1),
                 new(RecipeMaterialRole.Waste, offcut.Physical.Pin(), 3)]),
            [wood, offcut, sheet], [new(wood.Item, 0, new(270000)), new(wood.Item, 1, new(10000))]);
        var definition = new ConstructionDefinition(old.DefinitionId, "boundary_fixture_tank", "Boundary fixture tank", recipe,
            0, old.RequiredTool, members, new(1), old.WorkRate);
        var project = new ConstructionProject(oldProject.ProjectId, definition, oldProject.OwnerActorId, oldProject.BuilderActorId,
            oldProject.QuestId, oldProject.ProjectCustodianId, oldProject.ReservationId, new(106 * 256, 324 * 256, 256),
            oldProject.WorkFeet, oldProject.RequiredAccess);
        var construction = new ConstructionSnapshot(basis.WorldId, basis.BranchId, 0, [definition], [project]);
        var production = new ProductionSnapshot(basis.WorldId, basis.BranchId, 0,
            basis.Production!.Recipes.Where(r => r.DefinitionId != recipe.DefinitionId).Append(recipe), [], []);
        var request = basis.Reservoirs!.Capacity.Pending[0].Request;
        var capacity = WatershedRunoffCapacityState.Create(new WatershedRunoffPlanner().Prepare(new(request.Network, request.Mapping,
            basis.Inventory, production, request.ReservationId, request.OwnerActorId, request.Start, request.End,
            request.Material, request.Inputs, request.Terminals), TestContext.Current.CancellationToken));
        var geometries = basis.Reservoirs.Geometries.Select(g => g.ReservoirId == ReservoirBoundaryFixture.SourceReservoir
            ? new ReservoirGeometry(g.Scope, g.ReservoirId, g.CustodianId, 3, [new(106, 324, 1)]) : g);
        var world = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, basis.AdmissionVersion, basis.Calendar,
            0, 0, gathering: basis.Gathering, production: production, progression: basis.Progression, quests: basis.Quests,
            construction: construction, reservoirs: LiquidReservoirState.Create(capacity, geometries));
        return (world, new(ReservoirWorldFixture.Water, fixture.Policy.Rules.Append(new(sheet.Item, 1))));
    }
}
