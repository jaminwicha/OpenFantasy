namespace OpenFantasy.Core.Worlds;

public static class WorldStepCodecV10
{
    public const int MaximumBatchByteLength = WorldStepFrameCodec.MaximumBatchByteLength;

    public static byte[] Encode(WorldStepBatch batch) => WorldStepFrameCodec.Encode(batch, 10);
    public static WorldStepBatch Decode(ReadOnlySpan<byte> bytes) => WorldStepFrameCodec.Decode(bytes, 10);
}
