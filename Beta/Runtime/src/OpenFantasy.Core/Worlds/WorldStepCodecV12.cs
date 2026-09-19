namespace OpenFantasy.Core.Worlds;

public static class WorldStepCodecV12
{
    public const int MaximumBatchByteLength = WorldStepFrameCodec.MaximumBatchByteLength;

    public static byte[] Encode(WorldStepBatch batch) => WorldStepFrameCodec.Encode(batch, 12);
    public static WorldStepBatch Decode(ReadOnlySpan<byte> bytes) => WorldStepFrameCodec.Decode(bytes, 12);
}
