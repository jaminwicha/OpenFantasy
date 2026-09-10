using OpenFantasy.Core.Construction;

namespace OpenFantasy.Core.Worlds;

/// <summary>Retains the frozen quest basis and exact construction from which all placed geometry is reconstructed.</summary>
public static class WorldSnapshotCodecV5
{
    public static byte[] Encode(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.ProfileVersion != 5 || world.Construction is null)
        {
            throw new ArgumentException("This codec requires the construction world profile.", nameof(world));
        }
        // The outer frame owns every structure through the exact construction definition and
        // installed output binding. Only this deliberate basis serializer omits derived colliders.
        var basis = new WorldSnapshot(world.Inventory, world.Space.WithStructures([]), world.Residents,
            world.AdmissionVersion, world.Calendar, world.Revision, world.AdvancingSteps, world.Dialogues,
            world.Gathering, world.Production, world.Progression, world.Quests);
        byte[] bytes = CanonicalEncodingV1.Frame("world.snapshot", CanonicalEncodingV1.Unsigned64(5),
            WorldSnapshotCodecV4.Encode(basis), ConstructionCodecV1.Encode(world.Construction));
        if (bytes.Length > WorldSnapshotCodec.MaximumStateByteLength)
        {
            throw new ArgumentException("The construction world exceeds its retained storage bound.", nameof(world));
        }
        return bytes;
    }

    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, WorldSnapshotCodec.MaximumStateByteLength, 4);
        reader.Tag("world.snapshot"u8);
        if (reader.Number() != 5)
        {
            throw new FormatException("Unsupported construction world version.");
        }
        WorldSnapshot basis = WorldSnapshotCodecV4.Decode(reader.Field(WorldSnapshotCodec.MaximumStateByteLength));
        ConstructionSnapshot construction = ConstructionCodecV1.Decode(reader.Field(ConstructionCodecV1.MaximumSnapshotByteLength));
        reader.Complete();
        try
        {
            var space = basis.Space.WithStructures(construction.GetStructures());
            var restored = new WorldSnapshot(basis.Inventory, space, basis.Residents, basis.AdmissionVersion,
                basis.Calendar, basis.Revision, basis.AdvancingSteps, basis.Dialogues, basis.Gathering,
                basis.Production, basis.Progression, basis.Quests, construction);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The construction world is not canonical.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The construction world violates its physical admission.", error);
        }
    }
}
