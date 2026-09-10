using System.Text.Json;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;

namespace OpenFantasy.Core.Tests.Quests;

public sealed class QuestWireTests
{
    [Fact]
    public void QuestFramesMatchTheIndependentPythonOracle()
    {
        QuestFixture initial = QuestFixture.Create();
        QuestFixture accepted = initial.Apply(initial.Plan(initial.Accept()));
        QuestFixture completed = accepted.Apply(accepted.Plan(accepted.Complete()));
        Dictionary<string, byte[]> encoded = new()
        {
            ["wood_delivery"] = QuestCodecV1.EncodeDefinition(initial.Definition),
            ["empty"] = QuestCodecV1.Encode(initial.Quests),
            ["accepted"] = QuestCodecV1.Encode(accepted.Quests),
            ["completed"] = QuestCodecV1.Encode(completed.Quests),
            ["accept"] = WorldActionCodec.Encode(initial.Accept()),
            ["complete"] = WorldActionCodec.Encode(accepted.Complete())
        };
        using JsonDocument vectors = Load();
        foreach (JsonElement item in vectors.RootElement.GetProperty("valid").EnumerateArray())
        {
            byte[] expected = Convert.FromHexString(item.GetProperty("hex").GetString()!);
            Assert.Equal(item.GetProperty("sha256").GetString(), ContentHash.Compute(expected).Value);
            Assert.Equal(expected, encoded[item.GetProperty("name").GetString()!]);
            Assert.Equal(expected, RoundTrip(item.GetProperty("kind").GetString()!, expected));
        }
    }

    [Fact]
    public void MalformedAndTruncatedFramesNeverAcquireQuestOrRewardMeaning()
    {
        using JsonDocument vectors = Load();
        foreach (JsonElement item in vectors.RootElement.GetProperty("invalid").EnumerateArray())
        {
            byte[] bytes = Convert.FromHexString(item.GetProperty("hex").GetString()!);
            Assert.Throws<FormatException>(() => RoundTrip(item.GetProperty("kind").GetString()!, bytes));
        }
        foreach (JsonElement item in vectors.RootElement.GetProperty("valid").EnumerateArray())
        {
            byte[] bytes = Convert.FromHexString(item.GetProperty("hex").GetString()!);
            for (int length = 0; length < bytes.Length; length++)
            {
                int cut = length;
                Assert.Throws<FormatException>(() => RoundTrip(item.GetProperty("kind").GetString()!, bytes[..cut]));
            }
        }
        Assert.Throws<FormatException>(() => QuestCodecV1.Decode(new byte[QuestCodecV1.MaximumSnapshotByteLength + 1]));
        Assert.Throws<FormatException>(() => QuestCodecV1.DecodeDefinition(new byte[QuestCodecV1.MaximumDefinitionByteLength + 1]));
    }

    [Fact]
    public void EarlierProfilesRefuseQuestActionsAndNeverDiscardComponents()
    {
        QuestFixture fixture = QuestFixture.Create();
        for (ushort profile = 1; profile < 4; profile++)
        {
            ushort version = profile;
            Assert.Throws<ArgumentException>(() => new WorldStepBatch(Id(2600), fixture.World.WorldId,
                fixture.World.BranchId, fixture.World.Revision, fixture.World.AdvancingSteps,
                WorldBatchKind.HeldInteraction, [], [fixture.Accept()], version));
        }
        WorldSnapshot world = fixture.AsWorld();
        Assert.Throws<ArgumentException>(() => WorldSnapshotCodecV3.Encode(world));
        Assert.Throws<ArgumentException>(() => WorldSnapshotCodecV2.Encode(world));
        Assert.Throws<ArgumentException>(() => WorldSnapshotCodecV1.Encode(world));
        Assert.Equal(world.ContentHash, WorldSnapshotCodec.Decode(WorldSnapshotCodec.Encode(world)).ContentHash);
    }

    private static JsonDocument Load() => JsonDocument.Parse(File.ReadAllBytes(Path.Combine(
        AppContext.BaseDirectory, "Quests", "quest-v1.json")));

    private static byte[] RoundTrip(string kind, byte[] bytes) => kind switch
    {
        "definition" => QuestCodecV1.EncodeDefinition(QuestCodecV1.DecodeDefinition(bytes)),
        "snapshot" => QuestCodecV1.Encode(QuestCodecV1.Decode(bytes)),
        "action" => WorldActionCodec.Encode(WorldActionCodec.Decode(bytes)),
        _ => throw new InvalidOperationException("Unknown test vector family.")
    };
}
