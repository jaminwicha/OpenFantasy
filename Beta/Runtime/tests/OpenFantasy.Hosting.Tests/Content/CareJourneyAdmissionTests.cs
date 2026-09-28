using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Content;

public sealed class CareJourneyAdmissionTests
{
    private const string Seed = "native-care-world";
    private const string Name = "Traveler";

    [Fact]
    public void Embedded_and_filesystem_composition_are_exact_and_deterministic()
    {
        WorldSnapshot first = CareJourneyAdmission.CreateEmbedded(Seed, Name);
        WorldSnapshot repeated = CareJourneyAdmission.CreateEmbedded(Seed, Name);
        WorldSnapshot installed = CareJourneyAdmission.Create(FarmContentFixture.InstalledContentRoot(), Seed, Name);
        Assert.Equal(WorldSnapshotCodec.Encode(first), WorldSnapshotCodec.Encode(repeated));
        Assert.Equal(WorldSnapshotCodec.Encode(first), WorldSnapshotCodec.Encode(installed));
        WorldSnapshot differentSeed = CareJourneyAdmission.CreateEmbedded(Seed + "-other", Name);
        WorldSnapshot differentName = CareJourneyAdmission.CreateEmbedded(Seed, "Another traveler");
        Assert.NotEqual(first.WorldId, differentSeed.WorldId);
        Assert.NotEqual(first.ContentHash, differentName.ContentHash);
        Assert.Equal(Recipe(first).Version, Recipe(differentSeed).Version);
        Assert.Equal(first.Care!.Remedies.Single().Version, differentSeed.Care!.Remedies.Single().Version);
    }

    [Fact]
    public void Original_encounter_selection_retains_its_pre_change_canonical_vector()
    {
        WorldSnapshot original = EncounterJourneyAdmission.CreateEmbedded("encounter-lifecycle-world", "Traveler");
        Assert.Equal((ushort)24, original.ProfileVersion);
        Assert.Equal("6f9b2a6456a6f73e3d9ea9c7ae3754ff003e958e2d3ab6eab6afd0749838d620", original.ContentHash.Value);
        Assert.Null(original.Care);
    }

    [Fact]
    public void Fresh_care_preserves_stats_abilities_geography_and_unearned_owners()
    {
        WorldSnapshot original = EncounterJourneyAdmission.CreateEmbedded(Seed, Name);
        WorldSnapshot world = CareJourneyAdmission.CreateEmbedded(Seed, Name);
        Assert.Equal((ushort)25, world.ProfileVersion);
        Assert.Equal((0UL, 0UL), (world.Revision, world.AdvancingSteps));
        Assert.Equal(original.Calendar, world.Calendar);
        Assert.Equal(original.Progression!.ContentHash, world.Progression!.ContentHash);
        Assert.Equal(original.EncounterLifecycle!.ContentHash, world.EncounterLifecycle!.ContentHash);
        Assert.Equal(original.Space.Regions.Select(value => value.ComputeContentHash()),
            world.Space.Regions.Select(value => value.ComputeContentHash()));
        Assert.Equal(original.LiquidCells!.Portions, world.LiquidCells!.Portions);
        Assert.Equal(original.Quests!.ContentHash, world.Quests!.ContentHash);
        Assert.Equal(original.Construction!.ContentHash, world.Construction!.ContentHash);
        Assert.Empty(world.Production!.Batches);
        Assert.Empty(world.Production.Lots);
        Assert.Empty(world.Production.ConsumedItems);
        Assert.True(world.Production.SupportsConsumption);
        Assert.Empty(world.Care!.Acknowledgments);
        Assert.Empty(world.Care.Treatments);
        Assert.Empty(world.ResourceHistory!.History.Batches);
        Assert.Null(world.ResourceHistory.ArchiveAnchor);
        Assert.Equal(0UL, world.Inventory.Custody.Revision);
        Assert.All(world.Inventory.Custody.Storages, storage => Assert.Equal(0UL, storage.Revision));
        Assert.All(world.Space.Actors, actor => Assert.Equal(0UL, actor.Revision));
    }

    [Fact]
    public void Helper_is_physically_nearby_stationary_and_finitely_stocked_with_original_roles()
    {
        WorldSnapshot original = EncounterJourneyAdmission.CreateEmbedded(Seed, Name);
        WorldSnapshot world = CareJourneyAdmission.CreateEmbedded(Seed, Name);
        CareAgreement agreement = Assert.Single(world.Care!.Agreements);
        WorldResident helper = world.GetResident(agreement.HelperActorId);
        WorldResident prior = original.GetResident(helper.ActorId);
        var originalBody = original.Space.GetActor(helper.ActorId);
        var actualBody = world.Space.GetActor(helper.ActorId);
        Assert.Equal(ResidentControllerKind.Host, helper.ControllerKind);
        Assert.Equal(prior.DisplayName, helper.DisplayName);
        Assert.Equal(prior.OpeningUtterance!.Text, helper.OpeningUtterance!.Text);
        Assert.Equal(prior.OpeningUtterance.DefinitionId, helper.OpeningUtterance.DefinitionId);
        Assert.Equal(originalBody.Binding.FormInstanceId, actualBody.Binding.FormInstanceId);
        Assert.Equal(originalBody.Condition.ComputeContentHash(), actualBody.Condition.ComputeContentHash());
        Assert.Equal(world.Space.GetActor(agreement.PatientActorId).Feet.Offset(0, -256, 0), actualBody.Feet);
        Assert.Equal(actualBody.Feet, helper.HomeFeet);
        Assert.Equal(0, helper.WanderRadiusCells);
        Assert.Equal(SpatialInteraction.Reachable, world.Space.QueryInteraction(helper.ActorId, agreement.PatientActorId, 256));
        Assert.DoesNotContain(new LocalNpcController().SelectMovement(world), value => value.ActorId == helper.ActorId);
        Assert.Equal(original.Inventory.Controls, world.Inventory.Controls);
        Assert.Equal(original.Inventory.AdmittedUnitCapacity + 8, world.Inventory.AdmittedUnitCapacity);
        Assert.Equal(original.Inventory.Custody.GetStorage(helper.InventoryCustodianId).UnitCapacity + 8,
            world.Inventory.Custody.GetStorage(helper.InventoryCustodianId).UnitCapacity);
        RestorativeRemedyDefinition remedy = Assert.Single(world.Care.Remedies);
        Assert.Equal(40U, remedy.RestoredHp);
        Assert.Equal(256UL, remedy.RangeSubunits);
        Assert.Equal(CareFarmSources.PotionPayloadHash, remedy.Item.Version.Value);
        CountedSourceLot potion = Assert.Single(world.Inventory.Custody.Lots,
            value => value.CustodianId == helper.InventoryCustodianId && value.Definition == remedy.Item);
        Assert.Equal(2UL, potion.Quantity);
        Assert.Equal(2UL, world.Inventory.Sources.Single(value => value.SourceId == potion.Source.SourceId).UnitCapacity);
        CountedItemDefinition berries = world.Gathering!.Nodes.Single(value => value.Definition.Kind == GatherNodeKind.Berry).Definition.YieldDefinition;
        CountedSourceLot stock = Assert.Single(world.Inventory.Custody.Lots,
            value => value.CustodianId == helper.InventoryCustodianId && value.Definition == berries);
        Assert.Equal(6UL, stock.Quantity);
        Assert.Equal(6UL, world.Inventory.Sources.Single(value => value.SourceId == stock.Source.SourceId).UnitCapacity);
        Assert.Null(potion.ReservedBy);
        Assert.Equal(LotProtection.None, potion.Protection);
    }

    [Fact]
    public void Original_recipe_reuses_actual_berries_and_requires_real_finite_crafting()
    {
        using var files = new StorageFiles();
        WorldSnapshot world = CareJourneyAdmission.CreateEmbedded(Seed, Name);
        CareAgreement agreement = Assert.Single(world.Care!.Agreements);
        WorldResident helper = world.GetResident(agreement.HelperActorId);
        CraftRecipeDefinition recipe = Recipe(world);
        Assert.Equal(CareFarmSources.RecipePayloadHash, recipe.PhysicalRecipe.SourceRecipePayloadHash!.Value);
        RecipeTerm input = Assert.Single(recipe.Inputs), output = Assert.Single(recipe.Outputs);
        Assert.Equal((3UL, 1UL), (input.Count, output.Count));
        CountedItemDefinition berries = recipe.ItemFor(input), potion = recipe.ItemFor(output);
        Assert.Equal(world.Gathering!.Nodes.Single(value => value.Definition.Kind == GatherNodeKind.Berry).Definition.YieldDefinition, berries);
        Assert.Equal(world.Care.Remedies.Single().Item, potion);
        Assert.Equal(new MassMilligrams(60_000), recipe.PhysicalRecipe.InputMass);
        Assert.Equal(recipe.PhysicalRecipe.InputMass, recipe.PhysicalRecipe.OutputMass);
        Assert.Equal(new MassMilligrams(0), recipe.PhysicalRecipe.WasteMass);
        Assert.Equal(new VolumeMicrolitres(90_000), recipe.BindingFor(potion).Physical.VolumePerUnit);
        var otherBerryBinding = world.Production!.Recipes.Where(value => value.DefinitionId != recipe.DefinitionId)
            .SelectMany(value => value.Bindings).First(value => value.Item == berries);
        Assert.Equal(otherBerryBinding.Physical.Version, recipe.BindingFor(berries).Physical.Version);
        CountedSourceLot lot = world.Inventory.Custody.Lots.Single(value => value.CustodianId == helper.InventoryCustodianId && value.Definition == berries);
        var control = world.Inventory.Controls.Single(value => value.ActorId == helper.ActorId);
        var intent = new CraftIntent(new(recipe.DefinitionId, recipe.Version, world.Production.Revision,
            helper.InventoryCustodianId, world.Inventory.Custody.GetStorage(helper.InventoryCustodianId).Revision,
            [new(IngredientLotKind.Source, lot.LotId, lot.Revision, 3)]));
        var command = new WorldActionCommand(new(0xcafef00d00000000, 1), control.PrincipalId, control.SessionId,
            helper.ActorId, world.WorldId, world.BranchId, world.Space.GetActor(helper.ActorId).Revision, intent);
        WorldSnapshot crafted;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, world))
        {
            var result = authority.Commit(new(new(0xcafef00d00000000, 2), world.WorldId, world.BranchId, 0, 0,
                WorldBatchKind.AdvancingStep, [], [command], world.ProfileVersion));
            Assert.Equal(CommandReceiptStatus.Applied, Assert.Single(result.Receipts).Status);
            crafted = result.Candidate;
        }
        Assert.Equal(3UL, crafted.Inventory.Custody.Lots.Where(value => value.CustodianId == helper.InventoryCustodianId
            && value.Definition == berries).Aggregate(0UL, (sum, value) => sum + value.Quantity));
        Assert.Equal(2UL, crafted.Inventory.Custody.Lots.Single(value => value.Definition == potion).Quantity);
        Assert.Equal(1UL, Assert.Single(crafted.Production!.Lots, value => value.Definition == potion).Quantity);
        Assert.Single(crafted.Production.Batches);
        Assert.Empty(crafted.Production.ConsumedItems);
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(crafted), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
    }

    [Fact]
    public void Complete_fresh_canonical_root_opens_and_reopens_in_schema_seven()
    {
        using var files = new StorageFiles();
        WorldSnapshot world = CareJourneyAdmission.CreateEmbedded(Seed, Name);
        byte[] expected = WorldSnapshotCodec.Encode(world);
        Assert.Equal(expected, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(expected)));
        using (SqliteWorldAuthority.CreateNew(files.Path, world)) { }
        using (WorldDatabase stored = WorldDatabase.Open(files.Path, create: false))
        {
            Assert.Equal(7, stored.SchemaVersion);
            Assert.Equal(expected, stored.ReadGenesis());
            Assert.Empty(stored.ReadJournal());
            Assert.Empty(stored.ReadResourceArchives());
        }
        using (var authority = SqliteWorldAuthority.OpenExisting(files.Path))
            Assert.Equal(expected, WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
        Assert.Equal(expected, WorldSnapshotCodec.Encode(RecordedWorldReader.ReadForHost(files.Path, 0).State));
    }

    [Theory]
    [InlineData("legacy/aldermere-first-loop/records.json")]
    [InlineData("legacy/aldermere-first-loop/source-support/data/items.json")]
    [InlineData("legacy/aldermere-first-loop/source-support/data/recipes.json")]
    public void Changed_source_closure_is_rejected_without_embedded_fallback(string relative)
    {
        using var files = new FarmContentFixture();
        string path = files.PathFor(relative);
        byte[] bytes = File.ReadAllBytes(path);
        bytes[^1] ^= 1;
        File.WriteAllBytes(path, bytes);
        Assert.Throws<InvalidDataException>(() => CareJourneyAdmission.Create(files.ContentRoot, Seed, Name));
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    private static CraftRecipeDefinition Recipe(WorldSnapshot world) => world.Production!.Recipes
        .Single(value => value.PhysicalRecipe.Key == CareJourneyAdmission.RecipeKey);
}
