using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

internal static class ProductionUpgradeFixture
{
    internal static ProductionSnapshot Admission(WorldSnapshot state)
    {
        var package = new PackageId("test.production-upgrade");
        CountedItemDefinition wood = state.Inventory.Sources.Single(source => source.SourceId == Id(71)).Definition;
        var output = new CountedItemDefinition(Id(901), ContentHash.Compute("upgrade-plank-item-v1"u8));
        var woodPhysical = new MaterialDefinition(package, Id(902), "wood", new(10), new(10), new(1));
        var plankPhysical = new MaterialDefinition(package, Id(903), "plank", new(10), new(8), new(1));
        var physical = new MaterialRecipeDefinition(package, Id(904), "make-planks", new(1), null,
            new MaterialCatalog([woodPhysical, plankPhysical]),
            [new(RecipeMaterialRole.Input, woodPhysical.Pin(), 2), new(RecipeMaterialRole.Output, plankPhysical.Pin(), 2)]);
        var recipe = new CraftRecipeDefinition(Id(905), physical,
            [new(wood, woodPhysical), new(output, plankPhysical)], [new(wood, 0, new(10))]);
        return new(state.WorldId, state.BranchId, 0, [recipe], [], []);
    }

    internal static WorldProductionMigration Request(WorldSnapshot state, EntityId? cause = null)
    {
        ProductionSnapshot production = Admission(state);
        return new(cause ?? Id(90_001), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
            state.ContentHash, state.AdmissionVersion,
            WorldProductionMigration.ComputeTargetAdmissionVersion(state.AdmissionVersion, production), production);
    }

    internal static WorldActionCommand Craft(WorldSnapshot state, ulong command = 90_002)
    {
        CraftRecipeDefinition recipe = state.Production!.Recipes.Single();
        CountedSourceLot lot = state.Inventory.Custody.GetLot(Id(70));
        return new(Id(command), Delivery.PrincipalId, Delivery.SessionId, Id(10), state.WorldId, state.BranchId,
            state.Space.GetActor(Id(10)).Revision, new CraftIntent(new CraftRequest(recipe.DefinitionId, recipe.Version,
                state.Production.Revision, Id(60), state.Inventory.Custody.GetStorage(Id(60)).Revision,
                [new(IngredientLotKind.Source, lot.LotId, lot.Revision, 2)])));
    }
}
