using OpenFantasy.Core.Geography;

namespace OpenFantasy.Core.Worlds;

/// <summary>Retains the complete construction basis and its admitted immutable macro constraints.</summary>
public static class WorldSnapshotCodecV6
{
    public static byte[] Encode(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.ProfileVersion != 6 || world.Watershed is null)
        {
            throw new ArgumentException("This codec requires the watershed world profile.", nameof(world));
        }
        var basis = new WorldSnapshot(world.Inventory, world.Space, world.Residents, world.AdmissionVersion,
            world.Calendar, world.Revision, world.AdvancingSteps, world.Dialogues, world.Gathering,
            world.Production, world.Progression, world.Quests, world.Construction);
        byte[] bytes = CanonicalEncodingV1.Frame("world.snapshot", CanonicalEncodingV1.Unsigned64(6),
            WorldSnapshotCodecV5.Encode(basis), WatershedCodecV1.Encode(world.Watershed));
        if (bytes.Length > WorldSnapshotCodec.MaximumStateByteLength)
        {
            throw new ArgumentException("The watershed world exceeds its retained storage bound.", nameof(world));
        }
        return bytes;
    }

    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, WorldSnapshotCodec.MaximumStateByteLength, 4);
        reader.Tag("world.snapshot"u8);
        if (reader.Number() != 6)
        {
            throw new FormatException("Unsupported watershed world version.");
        }
        WorldSnapshot basis = WorldSnapshotCodecV5.Decode(reader.Field(WorldSnapshotCodec.MaximumStateByteLength));
        PreparedWatershed watershed = WatershedCodecV1.Decode(reader.Field(WatershedCodecV1.MaximumEncodedByteLength));
        reader.Complete();
        try
        {
            var restored = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, basis.AdmissionVersion,
                basis.Calendar, basis.Revision, basis.AdvancingSteps, basis.Dialogues, basis.Gathering,
                basis.Production, basis.Progression, basis.Quests, basis.Construction, watershed);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The watershed world is not canonical.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The watershed world violates its macro admission.", error);
        }
    }
}
