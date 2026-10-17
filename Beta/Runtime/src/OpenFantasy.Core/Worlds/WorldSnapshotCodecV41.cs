namespace OpenFantasy.Core.Worlds;

public static class WorldSnapshotCodecV41
{
    public static byte[] Encode(WorldSnapshot world) => WorldSnapshotCodecV35.EncodeComplete(world, WorldPartyWithdrawalAdmission.ProfileVersion);
    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes, WorldMaterialCareArchiveAnchor? anchor = null) =>
        WorldSnapshotCodecV35.DecodeComplete(bytes, anchor, WorldPartyWithdrawalAdmission.ProfileVersion);
}
