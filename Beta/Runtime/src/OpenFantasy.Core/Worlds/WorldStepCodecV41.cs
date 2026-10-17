namespace OpenFantasy.Core.Worlds;

public static class WorldStepCodecV41
{
    public static byte[] Encode(WorldStepBatch batch) => WorldStepFrameCodec.Encode(batch, WorldPartyWithdrawalAdmission.ProfileVersion);
    public static WorldStepBatch Decode(ReadOnlySpan<byte> bytes)
    {
        var result = WorldStepFrameCodec.Decode(bytes, WorldPartyWithdrawalAdmission.ProfileVersion);
        if (!Encode(result).AsSpan().SequenceEqual(bytes)) throw new FormatException("The collective-withdrawal batch is not canonical.");
        return result;
    }
}
