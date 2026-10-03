namespace OpenFantasy.Core.Worlds;

/// <summary>Explicit named co-player permission lifecycle over the complete recovery root.</summary>
public static class WorldSnapshotCodecV39
{
    public static byte[] Encode(WorldSnapshot world) => WorldSnapshotCodecV35.EncodeComplete(world, WorldControlPermissionAdmission.ProfileVersion);
    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes, WorldMaterialCareArchiveAnchor? anchor = null) =>
        WorldSnapshotCodecV35.DecodeComplete(bytes, anchor, WorldControlPermissionAdmission.ProfileVersion);
}
