using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.Worlds.Dispositions;

public sealed class CareDispositionActionCodecTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Exact_disposition_terms_roundtrip_only_in_the_new_action_and_world_profile(bool decline)
    {
        PendingCareDispositionIntent intent = decline ? new DeclinePendingCareIntent(Id(10), Id(11), 42)
            : new CancelPendingCareIntent(Id(10), Id(11), 42);
        var command = new WorldActionCommand(Id(1), Id(2), Id(3), Id(4), Id(5), Id(6), 8, intent);
        byte[] bytes = WorldActionCodec.Encode(command);
        WorldActionCommand restored = WorldActionCodec.Decode(bytes);
        Assert.Equal(intent, restored.Intent);
        Assert.Equal(bytes, WorldActionCodecV21.Encode(restored));
        Assert.Throws<ArgumentException>(() => WorldActionCodecV13.Encode(command));
        Assert.Throws<FormatException>(() => WorldActionCodecV13.Decode(bytes));
        Assert.Throws<FormatException>(() => WorldActionCodecV21.Decode(bytes.Concat(new byte[] { 0 }).ToArray()));
        Assert.Throws<FormatException>(() => WorldActionCodecV21.Decode(bytes[..^1]));

        var batch = new WorldStepBatch(Id(7), Id(5), Id(6), 9, 3, WorldBatchKind.Care, [], [command], 38);
        byte[] batchBytes = WorldStepCodec.Encode(batch);
        var decoded = WorldStepCodec.Decode(batchBytes);
        Assert.Equal(WorldBatchKind.Care, decoded.Kind);
        Assert.Equal(bytes, WorldActionCodec.Encode(Assert.Single(decoded.Actions)));
        Assert.Equal(batchBytes, WorldStepCodecV38.Encode(decoded));
        Assert.Throws<FormatException>(() => WorldStepCodecV37.Decode(batchBytes));
        foreach (ushort profile in new ushort[] { 25, 26, 27, 28, 30, 32, 33, 34, 35, 36, 37 })
            Assert.Throws<ArgumentException>(() => new WorldStepBatch(Id(7), Id(5), Id(6), 9, 3,
                WorldBatchKind.Care, [], [command], profile));
        Assert.Throws<ArgumentException>(() => new WorldStepBatch(Id(7), Id(5), Id(6), 9, 3,
            WorldBatchKind.AdvancingStep, [], [command], 38));
    }

    private static EntityId Id(ulong value) => new(0xda38000000000000, value);
}
