using OpenFantasy.Core.Gathering;

namespace OpenFantasy.Core.Worlds;

/// <summary>The gathering profile composes exact retained exploration bytes with its mandatory work state.</summary>
public static class WorldSnapshotCodecV2
{
    public static byte[] Encode(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.ProfileVersion != 2 || world.Gathering is null)
        {
            throw new ArgumentException("Gathering worlds require an explicit admitted work component.", nameof(world));
        }
        byte[] bytes = CanonicalEncodingV1.Frame("world.snapshot", CanonicalEncodingV1.Unsigned64(2),
            WorldSnapshotCodecV1.EncodeExplorationBasis(world), GatheringCodecV1.Encode(world.Gathering));
        if (bytes.Length > WorldSnapshotCodec.MaximumStateByteLength)
        {
            throw new ArgumentException("The gathering world exceeds its admitted storage bound.", nameof(world));
        }
        return bytes;
    }

    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, WorldSnapshotCodec.MaximumStateByteLength, 4);
        reader.Tag("world.snapshot"u8);
        if (reader.Number() != 2)
        {
            throw new FormatException("Unsupported gathering world version.");
        }
        WorldSnapshot basis = WorldSnapshotCodecV1.Decode(reader.Field(WorldSnapshotCodec.MaximumStateByteLength));
        GatheringSnapshot gathering = GatheringCodecV1.Decode(reader.Field(WorldSnapshotCodec.MaximumStateByteLength));
        reader.Complete();
        try
        {
            var restored = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, basis.AdmissionVersion,
                basis.Calendar, basis.Revision, basis.AdvancingSteps, basis.Dialogues, gathering);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The gathering world is not canonically encoded.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The gathering world violates its admission invariants.", error);
        }
    }
}
