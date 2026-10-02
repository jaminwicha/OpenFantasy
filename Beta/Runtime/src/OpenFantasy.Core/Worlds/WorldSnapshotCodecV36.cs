namespace OpenFantasy.Core.Worlds;

/// <summary>Explicit controller-aware root. Older roots retain their exact version and encoding.</summary>
public static class WorldSnapshotCodecV36
{
    public static byte[] Encode(WorldSnapshot world) => WorldSnapshotCodecV35.EncodeComplete(world, WorldControllerAdmission.ProfileVersion);
    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes, WorldMaterialCareArchiveAnchor? anchor = null) =>
        WorldSnapshotCodecV35.DecodeComplete(bytes, anchor, WorldControllerAdmission.ProfileVersion);
}
