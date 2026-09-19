using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Worlds;

/// <summary>Retains the construction world and exact admitted excavation owner within the existing world bound.</summary>
public static class WorldSnapshotCodecV10
{
    public static byte[] Encode(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.ProfileVersion != 10 || world.Excavation is null)
        {
            throw new ArgumentException("This codec requires the excavation world profile.", nameof(world));
        }
        var basis = new WorldSnapshot(world.Inventory, world.Space, world.Residents, world.AdmissionVersion,
            world.Calendar, world.Revision, world.AdvancingSteps, world.Dialogues, world.Gathering,
            world.Production, world.Progression, world.Quests, world.Construction);
        byte[] bytes = CanonicalEncodingV1.Frame("world.snapshot", CanonicalEncodingV1.Unsigned64(10),
            WorldSnapshotCodecV5.Encode(basis), ExcavationCodecV1.Encode(world.Excavation));
        if (bytes.Length > WorldSnapshotCodec.MaximumStateByteLength)
        {
            throw new ArgumentException("The excavation world exceeds its retained storage bound.", nameof(world));
        }
        return bytes;
    }

    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, WorldSnapshotCodec.MaximumStateByteLength, 4);
        reader.Tag("world.snapshot"u8);
        if (reader.Number() != 10)
        {
            throw new FormatException("Unsupported excavation world version.");
        }
        WorldSnapshot basis = WorldSnapshotCodecV5.Decode(reader.Field(WorldSnapshotCodec.MaximumStateByteLength));
        ExcavationSnapshot excavation = ExcavationCodecV1.Decode(reader.Field(ExcavationCodecV1.MaximumEncodedByteLength));
        reader.Complete();
        try
        {
            var restored = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, basis.AdmissionVersion,
                basis.Calendar, basis.Revision, basis.AdvancingSteps, basis.Dialogues, basis.Gathering,
                basis.Production, basis.Progression, basis.Quests, basis.Construction, excavation: excavation);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The excavation world is not canonical.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The excavation world violates its terrain admission.", error);
        }
    }
}
