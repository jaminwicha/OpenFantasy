namespace OpenFantasy.Core.Worlds;

public static class WorldStepCodecV39
{
    public static byte[] Encode(WorldStepBatch batch) => WorldStepFrameCodec.Encode(batch, WorldControlPermissionAdmission.ProfileVersion);
    public static WorldStepBatch Decode(ReadOnlySpan<byte> bytes)
    {
        var result = WorldStepFrameCodec.Decode(bytes, WorldControlPermissionAdmission.ProfileVersion);
        if (!Encode(result).AsSpan().SequenceEqual(bytes)) throw new FormatException("The permission world batch is not canonical.");
        return result;
    }
}
