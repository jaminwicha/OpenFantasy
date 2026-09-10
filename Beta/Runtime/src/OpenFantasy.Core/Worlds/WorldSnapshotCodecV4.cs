using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;

namespace OpenFantasy.Core.Worlds;

/// <summary>Retains exact production state alongside funded quest commitments and actual actor progression.</summary>
public static class WorldSnapshotCodecV4
{
    public static byte[] Encode(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.ProfileVersion != 4)
        {
            throw new ArgumentException("This codec requires the quest world profile.", nameof(world));
        }
        byte[] bytes = CanonicalEncodingV1.Frame("world.snapshot", CanonicalEncodingV1.Unsigned64(4),
            WorldSnapshotCodecV3.EncodeProductionBasis(world), ProgressionCodecV1.Encode(world.Progression!),
            QuestCodecV1.Encode(world.Quests!));
        if (bytes.Length > WorldSnapshotCodec.MaximumStateByteLength)
        {
            throw new ArgumentException("The quest world exceeds its admitted storage bound.", nameof(world));
        }
        return bytes;
    }

    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, WorldSnapshotCodec.MaximumStateByteLength, 5);
        reader.Tag("world.snapshot"u8);
        if (reader.Number() != 4)
        {
            throw new FormatException("Unsupported quest world version.");
        }
        WorldSnapshot basis = WorldSnapshotCodecV3.Decode(reader.Field(WorldSnapshotCodec.MaximumStateByteLength));
        ProgressionSnapshot progression = ProgressionCodecV1.Decode(reader.Field(ProgressionCodecV1.MaximumSnapshotByteLength));
        QuestSnapshot quests = QuestCodecV1.Decode(reader.Field(QuestCodecV1.MaximumSnapshotByteLength));
        reader.Complete();
        try
        {
            var restored = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, basis.AdmissionVersion,
                basis.Calendar, basis.Revision, basis.AdvancingSteps, basis.Dialogues, basis.Gathering, basis.Production,
                progression, quests);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The quest world is not canonically encoded.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The quest world violates its admission invariants.", error);
        }
    }
}
