namespace OpenFantasy.Core.Worlds;

/// <summary>The local-market world's step frame: the shared layout under its own explicit profile number.</summary>
public static class WorldStepCodecV33
{
    public static byte[] Encode(WorldStepBatch batch) => WorldStepFrameCodec.Encode(batch, WorldMarketAdmission.ProfileVersion);
    public static WorldStepBatch Decode(ReadOnlySpan<byte> bytes) => WorldStepFrameCodec.Decode(bytes, WorldMarketAdmission.ProfileVersion);
}
