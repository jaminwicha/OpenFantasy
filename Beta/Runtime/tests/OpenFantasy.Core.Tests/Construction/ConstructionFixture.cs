using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Tests.Construction;

internal static class ConstructionFixture
{
    internal static EntityId Id(ulong value) => new(0, checked(10_000 + value));
    internal static readonly ContentHash Pin = ContentHash.Compute("construction-fixture"u8);

    internal static MillimetreBox[] Members() =>
    [
        new(0, 0, 0, 75, 5, 10),
        new(85, 0, 0, 315, 5, 10),
        new(325, 0, 0, 400, 5, 10),
        new(0, 0, 10, 400, 5, 246),
        new(75, -60, 0, 85, 65, 10),
        new(315, -60, 0, 325, 65, 10),
        new(75, -60, 10, 85, 0, 20),
        new(75, 5, 10, 85, 65, 20),
        new(315, -60, 10, 325, 0, 20),
        new(315, 5, 10, 325, 65, 20)
    ];

    internal static ConstructionDefinition Definition(IEnumerable<MillimetreBox>? members = null, CountedItemDefinition? sourceItem = null)
    {
        var package = new PackageId("construction-fixture");
        var wood = new MaterialDefinition(package, Id(10), "physical_wood", new(50_000), new(100_000), new(1));
        var guard = new MaterialDefinition(package, Id(11), "physical_guard", new(270_000), new(540_000), new(1));
        var offcuts = new MaterialDefinition(package, Id(12), "physical_offcuts", new(10_000), new(25_000), new(1));
        var woodItem = sourceItem ?? new CountedItemDefinition(Id(20), Pin);
        var guardItem = new CountedItemDefinition(Id(21), Pin);
        var offcutItem = new CountedItemDefinition(Id(22), Pin);
        var physical = new MaterialRecipeDefinition(package, Id(30), "physical_guard_assembly", new(1), null,
            new MaterialCatalog([wood, guard, offcuts]), [
                new(RecipeMaterialRole.Input, wood.Pin(), 6),
                new(RecipeMaterialRole.Output, guard.Pin(), 1),
                new(RecipeMaterialRole.Waste, offcuts.Pin(), 3)]);
        var recipe = new CraftRecipeDefinition(Id(31), physical,
            [new(woodItem, wood), new(guardItem, guard), new(offcutItem, offcuts)],
            [new(woodItem, 0, new(270_000)), new(woodItem, 1, new(10_000))]);
        var tool = new ConstructionToolDefinition(Id(40), new(Id(41), Pin), ConstructionToolOperation.CutCrossSlots);
        return new ConstructionDefinition(Id(50), "construction/guard", "Seedling guard", recipe, 0, tool,
            members ?? Members(), new(200), new ExposureRate(Id(60), Pin, 1, 1_800));
    }
}
