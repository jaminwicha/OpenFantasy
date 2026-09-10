using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Worlds;

/// <summary>Retains exploration and gathering encodings beside the permanent transformation ledger.</summary>
public static class WorldSnapshotCodecV3
{
    public static byte[] Encode(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.ProfileVersion != 3 || world.Gathering is null || world.Production is null)
        {
            throw new ArgumentException("Production worlds require both work and transformation components.", nameof(world));
        }
        return EncodeProductionBasis(world);
    }

    internal static byte[] EncodeProductionBasis(WorldSnapshot world)
    {
        byte[] bytes = CanonicalEncodingV1.Frame("world.snapshot", CanonicalEncodingV1.Unsigned64(3),
            WorldSnapshotCodecV1.EncodeExplorationBasis(world), GatheringCodecV1.Encode(world.Gathering!),
            ProductionCodecV1.Encode(world.Production!));
        if (bytes.Length > WorldSnapshotCodec.MaximumStateByteLength)
        {
            throw new ArgumentException("The production world exceeds its admitted storage bound.", nameof(world));
        }
        return bytes;
    }

    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, WorldSnapshotCodec.MaximumStateByteLength, 5);
        reader.Tag("world.snapshot"u8);
        if (reader.Number() != 3)
        {
            throw new FormatException("Unsupported production world version.");
        }
        WorldSnapshot basis = WorldSnapshotCodecV1.Decode(reader.Field(WorldSnapshotCodec.MaximumStateByteLength));
        GatheringSnapshot gathering = GatheringCodecV1.Decode(reader.Field(WorldSnapshotCodec.MaximumStateByteLength));
        ProductionSnapshot production = ProductionCodecV1.Decode(reader.Field(WorldSnapshotCodec.MaximumStateByteLength));
        reader.Complete();
        try
        {
            var restored = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, basis.AdmissionVersion,
                basis.Calendar, basis.Revision, basis.AdvancingSteps, basis.Dialogues, gathering, production);
            if (!Encode(restored).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The production world is not canonically encoded.");
            }
            return restored;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw new FormatException("The production world violates its admission invariants.", error);
        }
    }
}
