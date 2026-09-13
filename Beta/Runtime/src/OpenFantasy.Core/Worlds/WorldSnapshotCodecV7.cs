using OpenFantasy.Core.Geography;

namespace OpenFantasy.Core.Worlds;

/// <summary>Retains a complete construction basis with one admitted immutable watershed network.</summary>
public static class WorldSnapshotCodecV7
{
    public static byte[] Encode(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.ProfileVersion != 7 || world.WatershedNetwork is null)
        {
            throw new ArgumentException("This codec requires the watershed network world profile.", nameof(world));
        }
        var basis = new WorldSnapshot(world.Inventory, world.Space, world.Residents, world.AdmissionVersion,
            world.Calendar, world.Revision, world.AdvancingSteps, world.Dialogues, world.Gathering,
            world.Production, world.Progression, world.Quests, world.Construction);
        byte[] bytes = CanonicalEncodingV1.Frame("world.snapshot", CanonicalEncodingV1.Unsigned64(7),
            WorldSnapshotCodecV5.Encode(basis), WatershedNetworkCodecV1.Encode(world.WatershedNetwork));
        if (bytes.Length > WorldSnapshotCodec.MaximumStateByteLength)
        {
            throw new ArgumentException("The watershed network world exceeds its retained storage bound.", nameof(world));
        }
        return bytes;
    }

    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, WorldSnapshotCodec.MaximumStateByteLength, 4);
        reader.Tag("world.snapshot"u8);
        if (reader.Number() != 7)
        {
            throw new FormatException("Unsupported watershed network world version.");
        }
        WorldSnapshot basis = WorldSnapshotCodecV5.Decode(reader.Field(WorldSnapshotCodec.MaximumStateByteLength));
        PreparedWatershedNetwork network = WatershedNetworkCodecV1.Decode(reader.Field(WatershedNetworkCodecV1.MaximumEncodedByteLength));
        reader.Complete();
        try
        {
            var restored = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, basis.AdmissionVersion,
                basis.Calendar, basis.Revision, basis.AdvancingSteps, basis.Dialogues, basis.Gathering,
                basis.Production, basis.Progression, basis.Quests, basis.Construction, watershedNetwork: network);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The watershed network world is not canonical.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The watershed network world violates its macro admission.", error);
        }
    }
}
