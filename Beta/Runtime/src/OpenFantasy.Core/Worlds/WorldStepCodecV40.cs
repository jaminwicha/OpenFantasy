namespace OpenFantasy.Core.Worlds;

public static class WorldStepCodecV40
{
    public static byte[] Encode(WorldStepBatch batch) => WorldStepFrameCodec.Encode(batch, WorldDelegatedControlAdmission.ProfileVersion);
    public static WorldStepBatch Decode(ReadOnlySpan<byte> bytes)
    {
        var result = WorldStepFrameCodec.Decode(bytes, WorldDelegatedControlAdmission.ProfileVersion);
        if (!Encode(result).AsSpan().SequenceEqual(bytes)) throw new FormatException("The effective-control batch is not canonical.");
        return result;
    }
}
