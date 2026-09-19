using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>Complete direct snapshot with the explicitly admitted stepped terrain and construction locomotion rules.</summary>
public static class WorldSnapshotCodecV12
{
    public static byte[] Encode(WorldSnapshot world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.ProfileVersion != 12 || world.Excavation is null || world.Construction is null
            || world.Space.GroundSupport != GroundSupportMode.SteppedTerrainAndConstruction)
        {
            throw new ArgumentException("This codec requires the stepped terrain and construction world profile.", nameof(world));
        }
        return WorldSnapshotCodecV11.EncodeComplete(world, 12, WorldSpatialCodecV3.Encode(world.Space));
    }

    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes) => WorldSnapshotCodecV11.DecodeComplete(bytes, 12, 3);
}
