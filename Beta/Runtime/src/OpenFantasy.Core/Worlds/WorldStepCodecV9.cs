namespace OpenFantasy.Core.Worlds;

public static class WorldStepCodecV9
{
    public const int MaximumBatchByteLength = WorldStepFrameCodec.MaximumBatchByteLength;

    public static byte[] Encode(WorldStepBatch batch) => WorldStepFrameCodec.Encode(batch, 9);
    public static WorldStepBatch Decode(ReadOnlySpan<byte> bytes) => WorldStepFrameCodec.Decode(bytes, 9);
}
