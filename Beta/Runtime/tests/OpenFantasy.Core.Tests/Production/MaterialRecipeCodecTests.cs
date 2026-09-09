using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Tests.Production;

public sealed class MaterialRecipeCodecTests
{
    [Fact]
    public void MaterialAndRecipeBytesMatchIndependentPythonReference()
    {
        MaterialDefinition[] materials = ProductionFixture.Materials();
        var values = ProductionFixture.Calibration.GetProperty("materials").EnumerateArray().ToArray();
        for (int index = 0; index < materials.Length; index++)
        {
            Assert.Equal(values[index].GetProperty("canonicalHex").GetString(),
                Convert.ToHexString(MaterialRecipeCodecV1.EncodeMaterial(materials[index])).ToLowerInvariant());
            Assert.Equal(values[index].GetProperty("version").GetString(), materials[index].Version.Value);
        }
        MaterialRecipeDefinition recipe = ProductionFixture.Create();
        Assert.Equal(ProductionFixture.Bytes("recipeHex"), MaterialRecipeCodecV1.Encode(recipe));
        Assert.Equal(ProductionFixture.Read().GetProperty("recipeVersion").GetString(), recipe.Version.Value);
        Assert.Equal(ProductionFixture.Bytes("recipeHex"), MaterialRecipeCodecV1.Encode(
            MaterialRecipeCodecV1.Decode(ProductionFixture.Bytes("recipeHex"))));
    }

    [Fact]
    public void OriginalRecipeCountsRemainSeparateFromNewPhysicalCalibrationAndWaste()
    {
        var source = ProductionFixture.Read().GetProperty("sourceRecipe").GetProperty("payload");
        Assert.Equal("craft_rations", source.GetProperty("id").GetString());
        Assert.Equal("berries", source.GetProperty("inputs")[0].GetProperty("item").GetString());
        Assert.Equal(2, source.GetProperty("inputs")[0].GetProperty("count").GetInt32());
        Assert.Equal("wood", source.GetProperty("inputs")[1].GetProperty("item").GetString());
        Assert.Equal(1, source.GetProperty("inputs")[1].GetProperty("count").GetInt32());
        Assert.Equal("rations", source.GetProperty("output").GetString());
        Assert.Equal(2, source.GetProperty("outputCount").GetInt32());
        Assert.False(source.TryGetProperty("waste", out _));
        Assert.False(source.TryGetProperty("work", out _));
        MaterialRecipeDefinition recipe = ProductionFixture.Create();
        Assert.Equal(ProductionFixture.SourceHash(), recipe.SourceRecipePayloadHash);
        Assert.NotEqual(recipe.Version, recipe.SourceRecipePayloadHash);
        Assert.Equal(new ulong[] { 2, 1, 2, 1 }, recipe.Terms.Select(term => term.Count));
        Assert.Equal(RecipeMaterialRole.Waste, recipe.Terms[^1].Role);
        Assert.Contains("new authored", ProductionFixture.Calibration.GetProperty("status").GetString());
    }

    [Fact]
    public void EveryMalformedGoldenRecipeRejectsWithSpecificDiagnostics()
    {
        foreach (var value in ProductionFixture.Read().GetProperty("invalidRecipes").EnumerateArray())
        {
            byte[] bytes = Convert.FromHexString(value.GetProperty("hex").GetString()!);
            var failure = Assert.Throws<ProductionFormatException>(() => MaterialRecipeCodecV1.Decode(bytes));
            Assert.True(failure.Failure.ToString() == value.GetProperty("failure").GetString(),
                $"{value.GetProperty("name").GetString()}: {failure.Failure}");
        }
    }

    [Fact]
    public void EveryTruncatedPrefixAndOversizedFrameRejects()
    {
        byte[] bytes = ProductionFixture.Bytes("recipeHex");
        for (int length = 0; length < bytes.Length; length++)
        {
            byte[] prefix = bytes[..length];
            Assert.Throws<ProductionFormatException>(() => MaterialRecipeCodecV1.Decode(prefix));
        }
        var failure = Assert.Throws<ProductionFormatException>(() => MaterialRecipeCodecV1.Decode(
            new byte[MaterialRecipeCodecV1.MaximumRecipeByteLength + 1]));
        Assert.Equal(ProductionFormatFailure.LimitExceeded, failure.Failure);
    }

    [Fact]
    public void EncodedAndDecodedBytesCannotMutateAcceptedDefinitions()
    {
        byte[] bytes = ProductionFixture.Bytes("recipeHex");
        MaterialRecipeDefinition recipe = MaterialRecipeCodecV1.Decode(bytes);
        ContentHash version = recipe.Version;
        Array.Fill(bytes, (byte)0xff);
        byte[] encoded = MaterialRecipeCodecV1.Encode(recipe);
        Array.Fill(encoded, (byte)0);
        Assert.Equal(version, recipe.Version);
        Assert.Equal(ProductionFixture.Bytes("recipeHex"), MaterialRecipeCodecV1.Encode(recipe));
    }

    [Fact]
    public void AbsentSourceLineageIsRepresentableWithoutInventingAnOriginalPayload()
    {
        MaterialRecipeDefinition authored = new(ProductionFixture.Package, new EntityId(0, 500), "authored_rations",
            new WorkUnits(120), null, new MaterialCatalog(ProductionFixture.Materials()), ProductionFixture.Terms());
        MaterialRecipeDefinition restored = MaterialRecipeCodecV1.Decode(MaterialRecipeCodecV1.Encode(authored));
        Assert.Null(restored.SourceRecipePayloadHash);
        Assert.Equal(authored.Version, restored.Version);
    }
}
