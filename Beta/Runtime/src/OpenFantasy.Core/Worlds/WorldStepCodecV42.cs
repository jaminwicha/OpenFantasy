namespace OpenFantasy.Core.Worlds;

public static class WorldStepCodecV42
{
    public static byte[] Encode(WorldStepBatch batch) => WorldStepFrameCodec.Encode(batch, WorldCognitionAdmission.ProfileVersion);
    public static WorldStepBatch Decode(ReadOnlySpan<byte> bytes)
    {
        var result = WorldStepFrameCodec.Decode(bytes, WorldCognitionAdmission.ProfileVersion);
        if (!Encode(result).AsSpan().SequenceEqual(bytes)) throw new FormatException("The cognition batch is not canonical.");
        return result;
    }
}
