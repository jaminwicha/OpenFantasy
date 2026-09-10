using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Tests.Gathering;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;

namespace OpenFantasy.Core.Tests.Worlds;

internal static class ProductionWorldFixture
{
    internal static readonly CountedItemDefinition Product = new(Id(1004), ContentHash.Compute("world-product"u8));

    internal static ProductionSnapshot Ledger(WorldSnapshot basis)
    {
        var package = new PackageId("world-craft-test");
        var input = new MaterialDefinition(package, Id(1001), "input", new MassMilligrams(10), new VolumeMicrolitres(20), new HardnessRating(1));
        var output = new MaterialDefinition(package, Id(1002), "output", new MassMilligrams(20), new VolumeMicrolitres(30), new HardnessRating(2));
        var physical = new MaterialRecipeDefinition(package, Id(1003), "compress", new WorkUnits(1), null,
            new MaterialCatalog([input, output]), [new(RecipeMaterialRole.Input, input.Pin(), 2), new(RecipeMaterialRole.Output, output.Pin(), 1)]);
        var recipe = new CraftRecipeDefinition(Id(1005), physical, [new(Material, input), new(Product, output)],
            [new(Material, 0, new MassMilligrams(20))]);
        return new ProductionSnapshot(basis.WorldId, basis.BranchId, 0, [recipe], [], []);
    }

    internal static WorldProductionMigration Migration(WorldSnapshot basis, ulong cause = 1100)
    {
        ProductionSnapshot production = Ledger(basis);
        return new WorldProductionMigration(Id(cause), basis.WorldId, basis.BranchId, basis.Revision, basis.AdvancingSteps,
            basis.ContentHash, basis.AdmissionVersion, WorldProductionMigration.ComputeTargetAdmissionVersion(basis.AdmissionVersion, production), production);
    }

    internal static WorldSnapshot Create(ulong storageCapacity = 64)
    {
        WorldSnapshot basis = GatheringFixture.Create(storageCapacity: storageCapacity);
        return new WorldProductionMigrationProcessor().Decide(basis, Migration(basis)).Candidate;
    }

    internal static WorldActionCommand Craft(WorldSnapshot world, ulong command = 1200, IEnumerable<IngredientRequest>? selections = null)
    {
        CraftRecipeDefinition recipe = world.Production!.Recipes.Single();
        var request = new CraftRequest(recipe.DefinitionId, recipe.Version, world.Production.Revision, Id(60),
            world.Inventory.Custody.GetStorage(Id(60)).Revision, selections ?? [new(IngredientLotKind.Source, Id(70), 0, 2)]);
        return StepFixture.Action(world, command, new CraftIntent(request));
    }

    internal static WorldStepBatch Batch(WorldSnapshot world, ulong cause = 1300, params WorldActionCommand[] actions) =>
        new(Id(cause), world.WorldId, world.BranchId, world.Revision, world.AdvancingSteps,
            world.HasReadingHold ? WorldBatchKind.HeldInteraction : WorldBatchKind.AdvancingStep, [], actions, 3);
}
