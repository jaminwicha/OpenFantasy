using System.Text.Json;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;
using static OpenFantasy.Core.Tests.Production.InstantCraftFixture;

namespace OpenFantasy.Core.Tests.Production;

public sealed class ProductionGoldenTests
{
    [Fact]
    public void IndependentPythonVectorsMatchDefinitionsAndRealCommittedCandidates()
    {
        using JsonDocument json = Read();
        var initial = Create();
        CraftPlan first = Craft(initial.Inventory, initial.Production);
        AssertVector(json, "recipe", ProductionCodecV1.EncodeRecipe(Recipe()));
        AssertVector(json, "repackRecipe", ProductionCodecV1.EncodeRecipe(Repack(Recipe())));
        AssertVector(json, "empty", ProductionCodecV1.Encode(initial.Production));
        AssertVector(json, "crafted", ProductionCodecV1.Encode(first.CandidateProduction));
        var chainedInitial = Create(chained: true);
        CraftPlan chainFirst = Craft(chainedInitial.Inventory, chainedInitial.Production);
        TransformedLot ration = chainFirst.CandidateProduction.Lots.Single(value => value.Definition == Rations);
        CraftPlan second = Craft(chainFirst.CandidateInventory, chainFirst.CandidateProduction, 6001,
            Request(chainFirst.CandidateInventory, chainFirst.CandidateProduction,
                [new(IngredientLotKind.Transformed, ration.LotId, 0, 1)], Repack(Recipe())));
        AssertVector(json, "chained", ProductionCodecV1.Encode(second.CandidateProduction));
    }

    [Fact]
    public void IndependentMalformedFramesFailClosed()
    {
        using JsonDocument json = Read();
        foreach (JsonElement example in json.RootElement.GetProperty("malformed").EnumerateArray())
        {
            byte[] bytes = Convert.FromHexString(example.GetProperty("hex").GetString()!);
            Assert.Throws<ProductionFormatException>(() => ProductionCodecV1.Decode(bytes));
        }
    }

    private static void AssertVector(JsonDocument json, string key, byte[] actual)
    {
        JsonElement vector = json.RootElement.GetProperty(key);
        byte[] expected = Convert.FromHexString(vector.GetProperty("hex").GetString()!);
        Assert.Equal(expected, actual);
        Assert.Equal(vector.GetProperty("sha256").GetString(), ContentHash.Compute(actual).Value);
        if (key is "empty" or "crafted" or "chained")
        {
            Assert.Equal(expected, ProductionCodecV1.Encode(ProductionCodecV1.Decode(expected)));
        }
    }

    private static JsonDocument Read() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "production-ledger-v1.json")));
}
