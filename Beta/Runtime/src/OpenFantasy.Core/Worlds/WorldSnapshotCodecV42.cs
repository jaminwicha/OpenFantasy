namespace OpenFantasy.Core.Worlds;

/// <summary>Cognition root: exactly the profile-41 fields and frames, followed by the cognition section.</summary>
public static class WorldSnapshotCodecV42
{
    public static byte[] Encode(WorldSnapshot world) => WorldSnapshotCodecV35.EncodeComplete(world, WorldCognitionAdmission.ProfileVersion);
    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes, WorldMaterialCareArchiveAnchor? anchor = null) =>
        WorldSnapshotCodecV35.DecodeComplete(bytes, anchor, WorldCognitionAdmission.ProfileVersion);
}
