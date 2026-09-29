namespace OpenFantasy.Core.Worlds;

public static class WorldStepCodecV30
{
    public const int MaximumBatchByteLength = WorldStepFrameCodec.MaximumBatchByteLength;

    public static byte[] Encode(WorldStepBatch batch) => WorldStepFrameCodec.Encode(batch, 30);
    public static WorldStepBatch Decode(ReadOnlySpan<byte> bytes) => WorldStepFrameCodec.Decode(bytes, 30);
}
