namespace OpenFantasy.Core.Worlds;

/// <summary>Explicit pending-care dispositions over the complete recovery root.</summary>
public static class WorldSnapshotCodecV38
{
    public static byte[] Encode(WorldSnapshot world) => WorldSnapshotCodecV35.EncodeComplete(world, WorldCareDispositionAdmission.ProfileVersion);
    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes, WorldMaterialCareArchiveAnchor? anchor = null) =>
        WorldSnapshotCodecV35.DecodeComplete(bytes, anchor, WorldCareDispositionAdmission.ProfileVersion);
}
