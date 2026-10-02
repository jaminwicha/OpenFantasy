namespace OpenFantasy.Core.Worlds;

/// <summary>Controller-aware root with a pinned general recovery law and Care V4.</summary>
public static class WorldSnapshotCodecV37
{
    public static byte[] Encode(WorldSnapshot world) => WorldSnapshotCodecV35.EncodeComplete(world, WorldRecoveryAdmission.ProfileVersion);
    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes, WorldMaterialCareArchiveAnchor? anchor = null) =>
        WorldSnapshotCodecV35.DecodeComplete(bytes, anchor, WorldRecoveryAdmission.ProfileVersion);
}
