using OpenFantasy.Core.Geography;

namespace OpenFantasy.Core.Worlds;

/// <summary>Retains the construction world and its exclusive physical transport owner with exact shared-state pins.</summary>
public static class WorldSnapshotCodecV9
{
    public static byte[] Encode(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.ProfileVersion != 9 || world.Transport is null)
        {
            throw new ArgumentException("This codec requires the reservoir transport world profile.", nameof(world));
        }
        var basis = new WorldSnapshot(world.Inventory, world.Space, world.Residents, world.AdmissionVersion,
            world.Calendar, world.Revision, world.AdvancingSteps, world.Dialogues, world.Gathering,
            world.Production, world.Progression, world.Quests, world.Construction);
        byte[] bytes = CanonicalEncodingV1.Frame("world.snapshot", CanonicalEncodingV1.Unsigned64(9),
            WorldSnapshotCodecV5.Encode(basis), ReservoirTransportCodecV1.Encode(world.Transport));
        if (bytes.Length > WorldSnapshotCodec.MaximumStateByteLength)
        {
            throw new ArgumentException("The reservoir transport world exceeds its retained storage bound.", nameof(world));
        }
        return bytes;
    }

    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, WorldSnapshotCodec.MaximumStateByteLength, 4);
        reader.Tag("world.snapshot"u8);
        if (reader.Number() != 9)
        {
            throw new FormatException("Unsupported reservoir transport world version.");
        }
        WorldSnapshot basis = WorldSnapshotCodecV5.Decode(reader.Field(WorldSnapshotCodec.MaximumStateByteLength));
        ReservoirTransportState transport = ReservoirTransportCodecV1.Decode(reader.Field(ReservoirTransportCodecV1.MaximumEncodedByteLength));
        reader.Complete();
        try
        {
            var restored = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, basis.AdmissionVersion,
                basis.Calendar, basis.Revision, basis.AdvancingSteps, basis.Dialogues, basis.Gathering,
                basis.Production, basis.Progression, basis.Quests, basis.Construction, transport: transport);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The reservoir transport world is not canonical.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The reservoir transport world violates its physical-storage admission.", error);
        }
    }
}
