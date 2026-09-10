using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Tests.Production;

internal static class InstantCraftFixture
{
    internal static EntityId Id(ulong value) => new(0, value);
    internal static readonly CountedItemDefinition Berries = new(Id(5001), ContentHash.Compute("craft-test-berries"u8));
    internal static readonly CountedItemDefinition Wood = new(Id(5002), ContentHash.Compute("craft-test-wood"u8));
    internal static readonly CountedItemDefinition Rations = new(Id(5003), ContentHash.Compute("craft-test-rations"u8));
    internal static readonly CountedItemDefinition Offcuts = new(Id(5004), ContentHash.Compute("craft-test-offcuts"u8));
    internal static readonly TrustedCommandDelivery Delivery = new(Id(20), Id(30));

    internal static CraftRecipeDefinition Recipe(CountedItemDefinition? wood = null, CountedItemDefinition? berries = null)
    {
        PackageId package = new("openfantasy.crafting-tests");
        MaterialDefinition[] materials =
        [
            new(package, Id(5101), "berries", new(20000), new(30000), new(1)),
            new(package, Id(5102), "wood", new(50000), new(100000), new(8)),
            new(package, Id(5103), "rations", new(40000), new(45000), new(2)),
            new(package, Id(5104), "offcuts", new(10000), new(25000), new(0))
        ];
        var physical = new MaterialRecipeDefinition(package, Id(5201), "craft_rations", new WorkUnits(1),
            new ContentHash("c384027c76855ade482105233cadd07ea930a265f73e12beab852de1cc756491"),
            new MaterialCatalog(materials), [new(RecipeMaterialRole.Input, materials[0].Pin(), 2),
                new(RecipeMaterialRole.Input, materials[1].Pin(), 1), new(RecipeMaterialRole.Output, materials[2].Pin(), 2),
                new(RecipeMaterialRole.Waste, materials[3].Pin(), 1)]);
        CountedItemDefinition berryItem = berries ?? Berries;
        CountedItemDefinition woodItem = wood ?? Wood;
        return new CraftRecipeDefinition(Id(5202), physical,
            [new(berryItem, materials[0]), new(woodItem, materials[1]), new(Rations, materials[2]), new(Offcuts, materials[3])],
            [new(berryItem, 0, new(20000)), new(woodItem, 0, new(20000)), new(woodItem, 1, new(10000))]);
    }

    internal static CraftRecipeDefinition Repack(CraftRecipeDefinition first)
    {
        MaterialDefinition ration = first.BindingFor(Rations).Physical;
        var physical = new MaterialRecipeDefinition(ration.Package, Id(5203), "repack", new(1), null,
            new MaterialCatalog([ration]), [new(RecipeMaterialRole.Input, ration.Pin(), 1), new(RecipeMaterialRole.Output, ration.Pin(), 1)]);
        return new CraftRecipeDefinition(Id(5204), physical, [new(Rations, ration)], [new(Rations, 0, new(40000))]);
    }

    internal static (CountedAuthorityState Inventory, ProductionSnapshot Production) Create(ulong capacity = 64,
        bool chained = false, LotProtection protection = LotProtection.None)
    {
        CraftRecipeDefinition recipe = Recipe();
        var custody = new CustodySnapshot(Id(1), Id(2), 0, [new(Id(60), capacity, 0), new(Id(61), capacity, 0)],
            [new(Id(70), Id(60), Berries, new SourceInterval(Id(71), 0, 6), 0, protection),
                new(Id(72), Id(60), Wood, new SourceInterval(Id(73), 0, 4), 0)]);
        var inventory = new CountedAuthorityState(custody, 10, [Id(10), Id(11)],
            [new(Id(20), Id(30), Id(10)), new(Id(21), Id(31), Id(11))],
            [new(Id(60), Id(10), CustodyDepositRule.OwnerOnly), new(Id(61), Id(11), CustodyDepositRule.OwnerOnly)],
            [new(Id(71), Berries, 6), new(Id(73), Wood, 4)]);
        var production = new ProductionSnapshot(Id(1), Id(2), 0, chained ? [recipe, Repack(recipe)] : [recipe], [], []);
        production.ValidateForAuthority(inventory);
        return (inventory, production);
    }

    internal static CraftRequest Request(CountedAuthorityState inventory, ProductionSnapshot production,
        IEnumerable<IngredientRequest>? ingredients = null, CraftRecipeDefinition? recipe = null) =>
        new((recipe ?? production.Recipes[0]).DefinitionId, (recipe ?? production.Recipes[0]).Version,
            production.Revision, Id(60), inventory.Custody.GetStorage(Id(60)).Revision,
            ingredients ?? [new(IngredientLotKind.Source, Id(70), inventory.Custody.GetLot(Id(70)).Revision, 2),
                new(IngredientLotKind.Source, Id(72), inventory.Custody.GetLot(Id(72)).Revision, 1)]);

    internal static CraftPlan Craft(CountedAuthorityState inventory, ProductionSnapshot production, ulong command = 6000,
        CraftRequest? request = null) => new InstantCraftPlanner().Plan(inventory, production, Delivery, Id(10), Id(command),
            request ?? Request(inventory, production));
}
