using System.Buffers.Binary;
using OpenFantasy.Core.Worlds;
using static OpenFantasy.Core.Tests.Worlds.AdmissionFixture;
using static OpenFantasy.Core.Tests.Worlds.StepFixture;

namespace OpenFantasy.Core.Tests.Worlds;

public sealed class SnapshotCodecTests
{
    [Fact]
    public void RestoreRetainsMovedBodiesLanguageUtteranceHoldClockAndSourceIntervalsTogether()
    {
        var processor = new WorldStepProcessor();
        WorldSnapshot world = StepFixture.Create();
        world = processor.Decide(world, Batch(world, 1000, movement: [Move(world, 10)])).Candidate;
        world = processor.Decide(world, Batch(world, 1001,
            [Action(world, 1002, new SpeakToActorIntent(Id(11), 0))])).Candidate;
        world = processor.Decide(world, Batch(world, 1003,
            [Action(world, 1004, new GiveItemIntent(Id(11), 0, Id(70), 0, Id(60), 0, 4))])).Candidate;
        byte[] bytes = WorldSnapshotCodecV1.Encode(world);
        WorldSnapshot restored = WorldSnapshotCodecV1.Decode(bytes);
        Assert.Equal(bytes, WorldSnapshotCodecV1.Encode(restored));
        Assert.Equal(world.ContentHash, restored.ContentHash);
        Assert.Equal(32, restored.Space.GetActor(Id(10)).Feet.X);
        Assert.Equal(4UL, restored.Inventory.Custody.GetStoredQuantity(Id(61)));
        Assert.Equal(2UL, restored.AdvancingSteps);
        Assert.Equal(3UL, restored.Revision);
        Assert.Single(restored.Dialogues);
        Assert.Equal(world.GetResident(Id(11)).OpeningUtterance!.Text, restored.GetResident(Id(11)).OpeningUtterance!.Text);
    }

    [Fact]
    public void TruncatedTrailingAndOversizeWorldsAreRejected()
    {
        byte[] bytes = WorldSnapshotCodecV1.Encode(StepFixture.Create());
        foreach (int length in new[] { 0, 1, 11, 12, 128, bytes.Length - 1 })
        {
            Assert.Throws<FormatException>(() => WorldSnapshotCodecV1.Decode(bytes.AsSpan(0, length)));
        }
        Assert.Throws<FormatException>(() => WorldSnapshotCodecV1.Decode([.. bytes, 0]));
        Assert.Throws<FormatException>(() => WorldSnapshotCodecV1.Decode(new byte[WorldSnapshotCodecV1.MaximumStateByteLength + 1]));
    }

    [Fact]
    public void CanonicalBytesCannotRestoreImpossibleClockCountersOrCalendarRemainder()
    {
        byte[] bytes = WorldSnapshotCodecV1.Encode(StepFixture.Create());
        byte[][] fields = ReadFrame(bytes);
        byte[] impossibleSteps = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(impossibleSteps, 1);
        fields[9] = impossibleSteps;
        Assert.Throws<FormatException>(() => WorldSnapshotCodecV1.Decode(Frame(fields)));
        fields = ReadFrame(bytes);
        byte[] impossibleMilliseconds = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(impossibleMilliseconds, 86_400_000);
        fields[7] = impossibleMilliseconds;
        Assert.Throws<FormatException>(() => WorldSnapshotCodecV1.Decode(Frame(fields)));
    }

    [Fact]
    public void UnorderedOrDuplicatedResidentsAreRejectedInsteadOfSilentlyNormalized()
    {
        byte[][] fields = ReadFrame(WorldSnapshotCodecV1.Encode(StepFixture.Create()));
        byte[][] residents = ReadFrame(fields[4]);
        (residents[1], residents[2]) = (residents[2], residents[1]);
        fields[4] = Frame(residents);
        Assert.Throws<FormatException>(() => WorldSnapshotCodecV1.Decode(Frame(fields)));
        residents[1] = residents[2];
        fields[4] = Frame(residents);
        Assert.Throws<FormatException>(() => WorldSnapshotCodecV1.Decode(Frame(fields)));
    }

    private static byte[][] ReadFrame(byte[] frame)
    {
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(8)));
        int offset = 12;
        List<byte[]> fields = [];
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(frame.AsSpan(offset)));
            fields.Add(frame.AsSpan(offset + 4, length).ToArray());
            offset += 4 + length;
        }
        return fields.ToArray();
    }

    private static byte[] Frame(byte[][] fields)
    {
        byte[] bytes = new byte[12 + fields.Sum(field => 4 + field.Length)];
        "OFC\0"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), (uint)fields.Length);
        int offset = 12;
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset), (uint)field.Length);
            field.CopyTo(bytes, offset + 4);
            offset += field.Length + 4;
        }
        return bytes;
    }
}
