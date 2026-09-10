using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;

namespace OpenFantasy.Hosting.Tests.Content;

public sealed class ProductionFarmAdmissionTests
{
    [Fact]
    public void Adds_only_empty_production_metadata_and_preserves_a_real_progressed_gathered_world()
    {
        WorldSnapshot world = GatherOneWood();
        byte[] before = WorldSnapshotCodec.Encode(world);
        ProductionSnapshot production = ProductionFarmAdmission.CreateProduction(world);
        Assert.Equal(world.WorldId, production.WorldId);
        Assert.Equal(world.BranchId, production.BranchId);
        Assert.Equal(0UL, production.Revision);
        Assert.Single(production.Recipes);
        Assert.Empty(production.Batches);
        Assert.Empty(production.Lots);
        Assert.Equal(before, WorldSnapshotCodec.Encode(world));
        Assert.Equal(41UL, world.AdvancingSteps);
        Assert.Equal(4, world.Gathering!.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Oak).RemainingSwings);
        Assert.Equal(1UL, world.Gathering.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Oak).NextUnissuedUnit);
        production.ValidateForAuthority(world.Inventory);
    }

    [Fact]
    public void Original_input_item_pins_remain_distinct_from_their_physical_calibrations()
    {
        WorldSnapshot world = Create();
        CraftRecipeDefinition recipe = Assert.Single(ProductionFarmAdmission.CreateProduction(world).Recipes);
        ItemPhysicalBinding wood = Binding(recipe, "wood");
        ItemPhysicalBinding berries = Binding(recipe, "berries");
        Assert.Equal("f20a24bbcf04b36ebcb90a1d04ca58c0", wood.Item.DefinitionId.ToString());
        Assert.Equal("c603d104fd21c40819d8dc0f6a4f5f3bc976936f7ac2c2cfe6ffefd62820614d", wood.Item.Version.Value);
        Assert.Equal("2038079b78c92d2f77fb1443eed73661", berries.Item.DefinitionId.ToString());
        Assert.Equal("2a558e18e3a30b09446b6f0b25da2c2bcbca7ef8676459eab071ff27c8a286c6", berries.Item.Version.Value);
        foreach (ItemPhysicalBinding binding in recipe.Bindings)
        {
            Assert.NotEqual(binding.Item.DefinitionId, binding.Physical.DefinitionId);
            Assert.NotEqual(binding.Item.Version, binding.Physical.Version);
        }
        // Independently encoded by the Python admission oracle, including every physical field and identity.
        Assert.Equal("e594cf1f89749215d8b74c707ec91ee285735127fbb4e14f83fe908f39ff8d47", wood.Physical.Version.Value);
        Assert.Equal("f25cdef0c11b9b739a6d884f7962ef870313cee8690ba6c659b25c339d57f146", berries.Physical.Version.Value);
        Assert.Equal("9a627795124ec0fad5fc265d7b3d7eaa29148f4bef6700a0adf7f212813a8c8b", Binding(recipe, "rations").Physical.Version.Value);
        Assert.Equal("c5edc482168cfc861a8a22183a037bf3555a394386999ebc1b3a861a0f7ce675", Binding(recipe, "wood_offcuts").Physical.Version.Value);
        Assert.DoesNotContain(world.Inventory.Sources, source => source.Definition == Binding(recipe, "rations").Item);
        Assert.DoesNotContain(world.Inventory.Sources, source => source.Definition == Binding(recipe, "wood_offcuts").Item);
    }

    [Fact]
    public void Recipe_retains_source_counts_and_accounts_for_every_food_and_packaging_milligram()
    {
        CraftRecipeDefinition recipe = Assert.Single(ProductionFarmAdmission.CreateProduction(Create()).Recipes);
        Assert.Equal(1UL, recipe.PhysicalRecipe.RequiredWork.Value);
        Assert.NotEqual(recipe.DefinitionId, recipe.PhysicalRecipe.DefinitionId);
        Assert.Equal("c384027c76855ade482105233cadd07ea930a265f73e12beab852de1cc756491",
            recipe.PhysicalRecipe.SourceRecipePayloadHash!.Value);
        Assert.Equal(new MassMilligrams(90_000), recipe.PhysicalRecipe.InputMass);
        Assert.Equal(new MassMilligrams(80_000), recipe.PhysicalRecipe.OutputMass);
        Assert.Equal(new MassMilligrams(10_000), recipe.PhysicalRecipe.WasteMass);
        Assert.Equal(2UL, Assert.Single(recipe.Inputs, term => recipe.ItemFor(term) == Binding(recipe, "berries").Item).Count);
        Assert.Equal(1UL, Assert.Single(recipe.Inputs, term => recipe.ItemFor(term) == Binding(recipe, "wood").Item).Count);
        Assert.Equal(RecipeMaterialRole.Output, recipe.Outputs[0].Role);
        Assert.Equal(2UL, recipe.Outputs[0].Count);
        Assert.Equal(Binding(recipe, "rations").Item, recipe.ItemFor(recipe.Outputs[0]));
        Assert.Equal(RecipeMaterialRole.Waste, recipe.Outputs[1].Role);
        Assert.Equal(1UL, recipe.Outputs[1].Count);
        Assert.Equal(Binding(recipe, "wood_offcuts").Item, recipe.ItemFor(recipe.Outputs[1]));
        Assert.Equal(3, recipe.Allocations.Count);
        Assert.Equal(20_000UL, Allocation(recipe, "berries", 0));
        Assert.Equal(20_000UL, Allocation(recipe, "wood", 0));
        Assert.Equal(10_000UL, Allocation(recipe, "wood", 1));
    }

    [Fact]
    public void Recipe_definitions_are_stable_across_seeds_and_embedded_filesystem_bytes_match()
    {
        WorldSnapshot world = Create();
        ProductionSnapshot first = ProductionFarmAdmission.CreateProduction(world);
        ProductionSnapshot repeated = ProductionFarmAdmission.CreateProduction(world);
        ProductionSnapshot files = ProductionFarmAdmission.CreateProduction(FarmContentFixture.InstalledContentRoot(), world);
        ProductionSnapshot other = ProductionFarmAdmission.CreateProduction(Create("other seed"));
        Assert.Equal(first.ContentHash, repeated.ContentHash);
        Assert.Equal(first.ContentHash, files.ContentHash);
        Assert.NotEqual(first.WorldId, other.WorldId);
        Assert.Equal(first.Recipes[0].DefinitionId, other.Recipes[0].DefinitionId);
        Assert.Equal(first.Recipes[0].Version, other.Recipes[0].Version);
        Assert.Equal(first.Recipes[0].Bindings.Select(binding => binding.Item), other.Recipes[0].Bindings.Select(binding => binding.Item));
        Assert.Contains("OpenFantasy.Content/production-farm/admission-v1.json",
            typeof(ProductionFarmAdmission).Assembly.GetManifestResourceNames().Select(name => name.Replace('\\', '/')));
        Assert.Equal("7419a27a9936c8157c98ab99537b2d4433b4d891a6ad9e61320346b551dfe7e1",
            ProductionFarmAdmission.AdmissionVersion.Value);
    }

    [Fact]
    public void Unsupported_profiles_and_null_world_reject_without_creating_a_component()
    {
        Assert.Throws<ArgumentNullException>(() => ProductionFarmAdmission.CreateProduction(null!));
        Assert.Throws<ArgumentException>(() => ProductionFarmAdmission.CreateProduction(FarmWorldAdmission.CreateEmbedded("river", "Orin")));
        WorldSnapshot world = Create();
        WorldSnapshot wrongAdmission = new(world.Inventory, world.Space, world.Residents, ContentHash.Compute("wrong-admission"u8),
            world.Calendar, world.Revision, world.AdvancingSteps, world.Dialogues, world.Gathering);
        Assert.Throws<ArgumentException>(() => ProductionFarmAdmission.CreateProduction(wrongAdmission));
    }

    [Fact]
    public void A_structurally_valid_world_with_changed_source_item_version_cannot_borrow_the_original_calibration()
    {
        WorldSnapshot world = Create();
        CountedItemDefinition original = world.Gathering!.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Oak).Definition.YieldDefinition;
        var different = new CountedItemDefinition(original.DefinitionId, ContentHash.Compute("different-wood"u8));
        CountedSourceLot[] lots = world.Inventory.Custody.Lots.Select(lot => lot.Definition != original ? lot
            : new CountedSourceLot(lot.LotId, lot.CustodianId, different, lot.Source, lot.Revision, lot.Protection, lot.ReservedBy)).ToArray();
        var custody = new CustodySnapshot(world.WorldId, world.BranchId, world.Inventory.Custody.Revision,
            world.Inventory.Custody.Storages, lots);
        var inventory = new CountedAuthorityState(custody, world.Inventory.AdmittedUnitCapacity, world.Inventory.Actors,
            world.Inventory.Controls, world.Inventory.Policies, world.Inventory.Sources.Select(source => source.Definition != original
                ? source : new AdmittedCountedSource(source.SourceId, different, source.UnitCapacity)));
        var gathering = new GatheringSnapshot(world.WorldId, world.BranchId, world.AdvancingSteps,
            world.Gathering.Nodes.Select(node => node.Definition.YieldDefinition != original ? node : new GatherNodeState(node.NodeId,
                new GatherNodeDefinition(node.Definition.DefinitionId, node.Definition.Kind, different), node.SourceId,
                node.UnitCapacity, node.Feet, node.Revision, node.NextUnissuedUnit, node.RemainingSwings, node.DepletedAtStep)),
            world.Gathering.Tools, world.Gathering.Actors);
        var changed = new WorldSnapshot(inventory, world.Space, world.Residents, world.AdmissionVersion,
            world.Calendar, world.Revision, world.AdvancingSteps, world.Dialogues, gathering);
        Assert.Throws<ArgumentException>(() => ProductionFarmAdmission.CreateProduction(changed));
    }

    [Fact]
    public void A_new_recipe_identity_cannot_alias_an_existing_place()
    {
        WorldSnapshot world = Create();
        EntityId collision = ProductionFarmAdmission.CreateProduction(world).Recipes[0].DefinitionId;
        ActorPresence actor = world.Space.Actors[0];
        var changed = new ActorPresence(actor.Binding, actor.Condition, actor.Scope, actor.Feet, actor.Revision, collision);
        var space = new LocalSpatialSnapshot(world.Space.Scope, world.Space.Regions,
            world.Space.Actors.Select(value => value.ActorId == actor.ActorId ? changed : value));
        var validWorld = new WorldSnapshot(world.Inventory, space, world.Residents, world.AdmissionVersion,
            world.Calendar, world.Revision, world.AdvancingSteps, world.Dialogues, world.Gathering);
        Assert.Throws<InvalidDataException>(() => ProductionFarmAdmission.CreateProduction(validWorld));
    }

    [Theory]
    [InlineData("production-farm/admission-v1.json")]
    [InlineData("adventure-farm/admission-v1.json")]
    [InlineData("legacy/aldermere-first-loop/records.json")]
    [InlineData("legacy/aldermere-first-loop/source-support/data/recipes.json")]
    [InlineData("legacy/aldermere-first-loop/source-support/src/Sim/World.Systems.cs.txt")]
    [InlineData("legacy/aldermere-first-loop/assets/units/node_berry.png")]
    public void Changed_new_manifest_or_original_source_closure_rejects(string relativePath)
    {
        WorldSnapshot world = Create();
        using var fixture = new ProductionContentFixture();
        File.AppendAllText(fixture.PathFor(relativePath), " ");
        Assert.Throws<InvalidDataException>(() => ProductionFarmAdmission.CreateProduction(fixture.ContentRoot, world));
    }

    [Fact]
    public void Oversized_or_missing_manifest_rejects_instead_of_using_a_default_recipe()
    {
        WorldSnapshot world = Create();
        using var fixture = new ProductionContentFixture();
        string path = fixture.PathFor("production-farm/admission-v1.json");
        File.WriteAllBytes(path, new byte[16_385]);
        Assert.Throws<InvalidDataException>(() => ProductionFarmAdmission.CreateProduction(fixture.ContentRoot, world));
        File.Delete(path);
        Assert.Throws<FileNotFoundException>(() => ProductionFarmAdmission.CreateProduction(fixture.ContentRoot, world));
    }

    private static ItemPhysicalBinding Binding(CraftRecipeDefinition recipe, string key) =>
        recipe.Bindings.Single(binding => binding.Physical.Key == key);

    private static ulong Allocation(CraftRecipeDefinition recipe, string inputKey, uint outputIndex) =>
        recipe.Allocations.Single(value => value.InputItem == Binding(recipe, inputKey).Item && value.OutputIndex == outputIndex)
            .MassPerOutputUnit.Value;

    private static WorldSnapshot Create(string seed = "river") => GatheringFarmAdmission.CreateEmbedded(seed, "Orin");

    private static WorldSnapshot GatherOneWood()
    {
        WorldSnapshot world = Create();
        EntityId actor = world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        ActorControlBinding control = world.Inventory.Controls.Single(value => value.ActorId == actor);
        var processor = new WorldStepProcessor();
        for (ulong index = 0; index < 40; index++)
        {
            WorldMovementInput[] movement = index < 3
                ? [new(new TrustedCommandDelivery(control.PrincipalId, control.SessionId), actor, 0, 32)] : [];
            var step = new WorldStepBatch(new EntityId(0, 100_000 + index), world.WorldId, world.BranchId,
                world.Revision, world.AdvancingSteps, WorldBatchKind.AdvancingStep, movement, [], world.ProfileVersion);
            world = processor.Decide(world, step).Candidate;
        }
        GatherNodeState oak = world.Gathering!.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Oak);
        GatherToolDefinition axe = world.Gathering.Tools.Single(tool => tool.Kind == GatherToolKind.Axe);
        CountedSourceLot tool = world.Inventory.Custody.Lots.Single(lot => lot.Definition == axe.Definition);
        var command = new WorldActionCommand(new EntityId(0, 200_000), control.PrincipalId, control.SessionId, actor,
            world.WorldId, world.BranchId, world.Space.GetActor(actor).Revision,
            new GatherIntent(oak.NodeId, oak.Revision, tool.LotId, tool.Revision));
        var batch = new WorldStepBatch(new EntityId(0, 100_040), world.WorldId, world.BranchId,
            world.Revision, world.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [command], world.ProfileVersion);
        WorldStepDecision decision = processor.Decide(world, batch);
        Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(decision.Receipts).Status);
        return decision.Candidate;
    }

    private sealed class ProductionContentFixture : IDisposable
    {
        private readonly FarmContentFixture _basis = new();

        internal ProductionContentFixture()
        {
            foreach (string directory in new[] { "adventure-farm", "production-farm" })
            {
                Directory.CreateDirectory(PathFor(directory));
                File.Copy(Path.Combine(FarmContentFixture.InstalledContentRoot(), directory, "admission-v1.json"),
                    PathFor(directory + "/admission-v1.json"));
            }
        }

        internal string ContentRoot => _basis.ContentRoot;
        internal string PathFor(string relative) => _basis.PathFor(relative);
        public void Dispose() => _basis.Dispose();
    }
}
