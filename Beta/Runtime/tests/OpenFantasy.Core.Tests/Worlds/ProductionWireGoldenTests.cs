using System.Text.Json;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Tests.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Worlds;

public sealed class ProductionWireGoldenTests
{
    [Fact]
    public void IndependentActionAndStepBytesPreserveClaimsAndTypedIngredientFamilies()
    {
        using JsonDocument fixture = Read();
        WorldActionCommand raw = Action(6000, 10,
            [new(IngredientLotKind.Source, Id(70), 3, 2), new(IngredientLotKind.Source, Id(72), 4, 1)]);
        WorldActionCommand derived = Action(6001, 11,
            [new(IngredientLotKind.Transformed, Id(9000), 6, 1), new(IngredientLotKind.Source, Id(9001), 0, 1)]);
        AssertVector(fixture, "rawAction", WorldActionCodecV3.Encode(raw));
        AssertVector(fixture, "derivedAction", WorldActionCodecV3.Encode(derived));
        WorldMovementInput[] movement = [new(new TrustedCommandDelivery(Id(20), Id(30)), Id(10), -32, 0),
            new(new TrustedCommandDelivery(Id(21), Id(31)), Id(11), 0, 32)];
        var ordinary = new WorldStepBatch(Id(7000), Id(1), Id(2), 80, 77, WorldBatchKind.AdvancingStep,
            movement.Reverse(), [derived, raw], 3);
        var held = new WorldStepBatch(Id(7000), Id(1), Id(2), 80, 77, WorldBatchKind.HeldInteraction, [], [raw], 3);
        AssertVector(fixture, "advancingBatch", WorldStepCodecV3.Encode(ordinary));
        AssertVector(fixture, "heldBatch", WorldStepCodecV3.Encode(held));
        foreach (string key in new[] { "rawAction", "derivedAction" })
        {
            byte[] bytes = Bytes(fixture, key);
            Assert.Equal(bytes, WorldActionCodecV3.Encode(WorldActionCodecV3.Decode(bytes)));
            Assert.Equal(bytes, WorldActionCodec.Encode(WorldActionCodec.Decode(bytes)));
            Assert.Throws<FormatException>(() => WorldActionCodecV2.Decode(bytes));
        }
        foreach (string key in new[] { "advancingBatch", "heldBatch" })
        {
            byte[] bytes = Bytes(fixture, key);
            Assert.Equal(bytes, WorldStepCodecV3.Encode(WorldStepCodecV3.Decode(bytes)));
            Assert.Equal(bytes, WorldStepCodec.Encode(WorldStepCodec.Decode(bytes)));
            Assert.Throws<FormatException>(() => WorldStepCodecV2.Decode(bytes));
        }
    }

    [Fact]
    public void IndependentMigrationFrameBindsTheEmptyComponentAndComputedTargetAdmission()
    {
        using JsonDocument fixture = Read();
        ProductionSnapshot production = InstantCraftFixture.Create().Production;
        ContentHash source = Hash(fixture, "sourceAdmission");
        ContentHash target = WorldProductionMigration.ComputeTargetAdmissionVersion(source, production);
        Assert.Equal(Hash(fixture, "targetAdmission"), target);
        var request = new WorldProductionMigration(Id(8000), Id(1), Id(2), 80, 77,
            Hash(fixture, "expectedHead"), source, target, production);
        byte[] expected = Bytes(fixture, "migration");
        AssertVector(fixture, "migration", WorldProductionMigrationCodecV1.Encode(request));
        WorldProductionMigration restored = WorldProductionMigrationCodecV1.Decode(expected);
        Assert.Equal(expected, WorldProductionMigrationCodecV1.Encode(restored));
        Assert.Equal(0UL, restored.Production.Revision);
        Assert.Empty(restored.Production.Batches);
        Assert.Empty(restored.Production.Lots);
        Assert.Equal(source, restored.SourceAdmissionVersion);
    }

    [Fact]
    public void EveryPrefixTruncationAndIndependentMalformedCaseFailsClosed()
    {
        using JsonDocument fixture = Read();
        foreach ((string key, string kind) in new[] { ("rawAction", "action"), ("advancingBatch", "batch"), ("migration", "migration") })
        {
            byte[] bytes = Bytes(fixture, key);
            for (int length = 0; length < bytes.Length; length++)
            {
                byte[] incomplete = bytes[..length];
                Assert.ThrowsAny<FormatException>(() => Decode(kind, incomplete));
            }
        }
        foreach (JsonElement example in fixture.RootElement.GetProperty("malformed").EnumerateArray())
        {
            byte[] bytes = Convert.FromHexString(example.GetProperty("hex").GetString()!);
            Assert.ThrowsAny<FormatException>(() => Decode(example.GetProperty("kind").GetString()!, bytes));
        }
        Assert.ThrowsAny<FormatException>(() => WorldActionCodecV3.Decode(new byte[WorldActionCodec.MaximumCommandByteLength + 1]));
        Assert.ThrowsAny<FormatException>(() => WorldStepCodecV3.Decode(new byte[WorldStepCodecV3.MaximumBatchByteLength + 1]));
        Assert.ThrowsAny<FormatException>(() => WorldProductionMigrationCodecV1.Decode(new byte[WorldProductionMigrationCodecV1.MaximumMigrationByteLength + 1]));
    }

    private static void Decode(string kind, byte[] bytes)
    {
        switch (kind)
        {
            case "action": WorldActionCodecV3.Decode(bytes); break;
            case "batch": WorldStepCodecV3.Decode(bytes); break;
            case "migration": WorldProductionMigrationCodecV1.Decode(bytes); break;
            default: throw new InvalidOperationException("Unknown wire fixture kind.");
        }
    }

    private static WorldActionCommand Action(ulong command, ulong actor, IEnumerable<IngredientRequest> ingredients)
    {
        CraftRecipeDefinition recipe = InstantCraftFixture.Recipe();
        var request = new CraftRequest(recipe.DefinitionId, recipe.Version, 5, Id(60), 7, ingredients);
        return new WorldActionCommand(Id(command), Id(20), Id(30), Id(actor), Id(1), Id(2), 9, new CraftIntent(request));
    }

    private static EntityId Id(ulong value) => new(0, value);
    private static JsonDocument Read() => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "world-production-v1.json")));
    private static byte[] Bytes(JsonDocument fixture, string key) => Convert.FromHexString(fixture.RootElement.GetProperty(key).GetProperty("hex").GetString()!);
    private static ContentHash Hash(JsonDocument fixture, string key) => new(fixture.RootElement.GetProperty(key).GetString()!);
    private static void AssertVector(JsonDocument fixture, string key, byte[] bytes)
    {
        Assert.Equal(Bytes(fixture, key), bytes);
        Assert.Equal(fixture.RootElement.GetProperty(key).GetProperty("sha256").GetString(), ContentHash.Compute(bytes).Value);
    }
}
