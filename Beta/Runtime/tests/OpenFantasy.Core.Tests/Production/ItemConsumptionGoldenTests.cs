using System.Text.Json;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Production.InstantCraftFixture;

namespace OpenFantasy.Core.Tests.Production;

public sealed class ItemConsumptionGoldenTests
{
    [Fact]
    public void IndependentBytesMatchRawProducedAndInterleavedCraftingCandidates()
    {
        using JsonDocument vectors = Read();
        var initial = Create();
        var enabled = new ProductionSnapshot(initial.Production.WorldId, initial.Production.BranchId,
            0, initial.Production.Recipes, [], [], []);
        AssertVector(vectors, "enabled", enabled);

        var raw = Consume(initial.Inventory, enabled, 8000, new(IngredientLotKind.Source, Id(70), 0, 2), Berries);
        AssertVector(vectors, "raw", raw.CandidateProduction);

        var craft = Craft(initial.Inventory, enabled);
        Assert.Equal(WorldActionRejection.None, craft.Rejection);
        var produced = Consume(craft.CandidateInventory, craft.CandidateProduction, 8001,
            new(IngredientLotKind.Transformed, EntityId.DeriveSpawned(Id(6000), 1), 0, 1), Rations);
        AssertVector(vectors, "produced", produced.CandidateProduction);

        var laterCraft = Craft(raw.CandidateInventory, raw.CandidateProduction);
        Assert.Equal(WorldActionRejection.None, laterCraft.Rejection);
        var mixed = Consume(laterCraft.CandidateInventory, laterCraft.CandidateProduction, 8001,
            new(IngredientLotKind.Transformed, EntityId.DeriveSpawned(Id(6000), 1), 0, 1), Rations);
        AssertVector(vectors, "mixed", mixed.CandidateProduction);
        Assert.Equal(2, mixed.CandidateProduction.ConsumedItems.Count);
        Assert.Equal(4UL, mixed.CandidateInventory.Custody.GetLot(Id(70)).Source.Start);
    }

    [Fact]
    public void IndependentOverlappingRawRetirementAndMissingOutputFramesAreRefused()
    {
        using JsonDocument vectors = Read();
        foreach (JsonElement example in vectors.RootElement.GetProperty("malformed").EnumerateArray())
        {
            byte[] bytes = Convert.FromHexString(example.GetProperty("hex").GetString()!);
            Assert.Throws<ProductionFormatException>(() => ProductionCodecV2.Decode(bytes));
        }
    }

    private static ItemConsumptionPlan Consume(CountedAuthorityState inventory, ProductionSnapshot production,
        ulong command, IngredientRequest ingredient, CountedItemDefinition definition)
    {
        var request = new ConsumeItemRequest(Id(60), inventory.Custody.GetStorage(Id(60)).Revision,
            production.Revision, Id(11), definition, ingredient);
        var plan = new ItemConsumptionPlanner().Plan(inventory, production, Delivery, Id(10), Id(command), request);
        Assert.Equal(WorldActionRejection.None, plan.Rejection);
        plan.CandidateProduction.ValidateForAuthority(plan.CandidateInventory);
        return plan;
    }

    private static void AssertVector(JsonDocument vectors, string key, ProductionSnapshot state)
    {
        JsonElement vector = vectors.RootElement.GetProperty(key);
        byte[] expected = Convert.FromHexString(vector.GetProperty("hex").GetString()!);
        byte[] actual = ProductionCodecV2.Encode(state);
        Assert.Equal(expected, actual);
        Assert.Equal(vector.GetProperty("sha256").GetString(), state.ContentHash.Value);
        Assert.Equal(expected, ProductionCodecV2.Encode(ProductionCodecV2.Decode(expected)));
    }

    private static JsonDocument Read() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "production-consumption-v2.json")));
}
