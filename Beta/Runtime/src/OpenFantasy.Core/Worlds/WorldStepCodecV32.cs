namespace OpenFantasy.Core.Worlds;

/// <summary>The paid world's step frame: the shared layout under its own explicit profile number.</summary>
public static class WorldStepCodecV32
{
    public static byte[] Encode(WorldStepBatch batch) => WorldStepFrameCodec.Encode(batch, WorldPaidExchangeAdmission.ProfileVersion);
    public static WorldStepBatch Decode(ReadOnlySpan<byte> bytes) => WorldStepFrameCodec.Decode(bytes, WorldPaidExchangeAdmission.ProfileVersion);
}
