using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;

namespace OpenFantasy.Core.Tests.Worlds;

public sealed class WorldCodecDispatchTests
{
    [Fact]
    public void Legacy_snapshot_dispatch_retains_exact_bytes_and_profile()
    {
        WorldSnapshot original = StepFixture.Create();
        byte[] frozen = WorldSnapshotCodecV1.Encode(original);
        WorldSnapshot restored = WorldSnapshotCodec.Decode(frozen);
        Assert.Equal((ushort)1, restored.ProfileVersion);
        Assert.Null(restored.Gathering);
        Assert.Equal(frozen, WorldSnapshotCodec.Encode(restored));
        Assert.Equal(original.ContentHash, restored.ContentHash);
    }

    [Fact]
    public void Legacy_commands_and_batches_keep_their_byte_and_receipt_contracts()
    {
        WorldSnapshot state = StepFixture.Create();
        WorldActionCommand action = StepFixture.Action(state, 700, new SpeakToActorIntent(Id(11), 0));
        Assert.Equal(WorldActionCodecV1.Encode(action), WorldActionCodec.Encode(action));
        Assert.Equal(action.ContentHash, WorldActionCodec.Decode(WorldActionCodec.Encode(action)).ContentHash);
        WorldStepBatch batch = StepFixture.Batch(state, 701, [action]);
        Assert.Equal(WorldStepCodecV1.Encode(batch), WorldStepCodec.Encode(batch));
        Assert.Equal((ushort)1, WorldStepCodec.Decode(WorldStepCodec.Encode(batch)).ProfileVersion);
    }

    [Fact]
    public void Profile_mismatch_never_reinterprets_a_saved_step()
    {
        WorldSnapshot legacy = StepFixture.Create();
        WorldStepBatch newer = new(Id(700), legacy.WorldId, legacy.BranchId, 0, 0,
            WorldBatchKind.AdvancingStep, [], [], profileVersion: 2);
        Assert.Throws<ArgumentException>(() => WorldStepCodecV1.Encode(newer));
        Assert.Throws<InvalidOperationException>(() => new WorldStepProcessor().Decide(legacy, newer));
        Assert.Equal((ushort)2, WorldStepCodec.Decode(WorldStepCodec.Encode(newer)).ProfileVersion);
    }

    [Fact]
    public void Unknown_versions_and_oversized_frames_fail_without_fallback()
    {
        byte[] unknown = WorldSnapshotCodecV1.Encode(StepFixture.Create());
        int tagLength = checked((int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(unknown.AsSpan(12)));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(unknown.AsSpan(20 + tagLength), 99);
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(unknown));
        Assert.Throws<FormatException>(() => WorldActionCodec.Decode(unknown));
        Assert.Throws<FormatException>(() => WorldStepCodec.Decode(unknown));
        Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode(new byte[WorldSnapshotCodec.MaximumStateByteLength + 1]));
    }
}
