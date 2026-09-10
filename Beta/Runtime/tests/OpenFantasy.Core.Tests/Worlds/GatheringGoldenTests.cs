using System.Text.Json;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;

namespace OpenFantasy.Core.Tests.Worlds;

public sealed class GatheringGoldenTests
{
    [Fact]
    public void Independent_request_and_step_match_exact_fields_bytes_and_hashes()
    {
        var action = new WorldActionCommand(Id(700), Id(20), Id(30), Id(10), Id(1), Id(2), 9,
            new GatherIntent(Id(501), 3, Id(901), 7));
        byte[] request = Bytes("action");
        Assert.Equal(request, WorldActionCodec.Encode(action));
        Assert.Equal(Hash("action"), action.ContentHash.Value);
        Assert.Equal(action.Intent, WorldActionCodec.Decode(request).Intent);
        Assert.Throws<FormatException>(() => WorldActionCodecV1.Decode(request));
        var batch = new WorldStepBatch(Id(999), Id(1), Id(2), 80, 77, WorldBatchKind.AdvancingStep,
            [new WorldMovementInput(new TrustedCommandDelivery(Id(20), Id(30)), Id(10), -32, 0)], [action], 2);
        byte[] steps = Bytes("batch");
        Assert.Equal(steps, WorldStepCodec.Encode(batch));
        Assert.Equal(Hash("batch"), ContentHash.Compute(steps).Value);
        Assert.Equal(steps, WorldStepCodec.Encode(WorldStepCodec.Decode(steps)));
        Assert.Throws<FormatException>(() => WorldStepCodecV1.Decode(steps));
    }

    [Fact]
    public void Independent_work_component_matches_negative_coordinates_pins_effort_and_cursor()
    {
        var wood = new CountedItemDefinition(Id(801), new ContentHash(new string('a', 64)));
        var axe = new CountedItemDefinition(Id(802), new ContentHash(new string('b', 64)));
        var expected = new GatheringSnapshot(Id(1), Id(2), 77,
            [new GatherNodeState(Id(501), new GatherNodeDefinition(Id(601), GatherNodeKind.Oak, wood),
                Id(701), 160, new SubunitPosition(-256, 768, 0), 3, 3, 2)],
            [new GatherToolDefinition(axe, GatherToolKind.Axe, 1)], [new GatherActorWork(Id(10), 1000)]);
        byte[] bytes = Bytes("snapshot");
        Assert.Equal(bytes, GatheringCodecV1.Encode(expected));
        Assert.Equal(Hash("snapshot"), expected.ContentHash.Value);
        GatheringSnapshot restored = GatheringCodecV1.Decode(bytes);
        Assert.Equal(bytes, GatheringCodecV1.Encode(restored));
        Assert.Equal(expected.Nodes[0].Feet, restored.Nodes[0].Feet);
        Assert.Equal(3UL, restored.Nodes[0].NextUnissuedUnit);
        Assert.Equal(1000U, restored.Actors[0].Effort);
    }

    [Fact]
    public void Every_truncated_independent_gathering_frame_is_rejected()
    {
        foreach (string kind in new[] { "action", "batch", "snapshot" })
        {
            byte[] original = Bytes(kind);
            for (int length = 0; length < original.Length; length++)
            {
                byte[] truncated = original[..length];
                Assert.Throws<FormatException>(() => Decode(kind, truncated));
            }
            Assert.Throws<FormatException>(() => Decode(kind, [.. original, 0]));
        }
    }

    private static void Decode(string kind, byte[] bytes)
    {
        if (kind == "action") WorldActionCodec.Decode(bytes);
        else if (kind == "batch") WorldStepCodec.Decode(bytes);
        else GatheringCodecV1.Decode(bytes);
    }

    private static JsonElement Vector(string name)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "gathering-v1.json")));
        return fixture.RootElement.GetProperty(name).Clone();
    }
    private static byte[] Bytes(string name) => Convert.FromHexString(Vector(name).GetProperty("hex").GetString()!);
    private static string Hash(string name) => Vector(name).GetProperty("sha256").GetString()!;
}
