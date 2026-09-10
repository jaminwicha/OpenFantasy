namespace OpenFantasy.Core.Worlds;

public static class WorldStepCodec
{
    public const int MaximumBatchByteLength = WorldStepCodecV1.MaximumBatchByteLength;

    public static byte[] Encode(WorldStepBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        return batch.ProfileVersion switch
        {
            1 => WorldStepCodecV1.Encode(batch),
            2 => WorldStepCodecV2.Encode(batch),
            3 => WorldStepCodecV3.Encode(batch),
            _ => throw new ArgumentException("Unsupported world step profile.", nameof(batch))
        };
    }

    public static WorldStepBatch Decode(ReadOnlySpan<byte> bytes) =>
        WorldCodecVersion.Read(bytes, "world.step-batch"u8, MaximumBatchByteLength, 10) switch
        {
            1 => WorldStepCodecV1.Decode(bytes),
            2 => WorldStepCodecV2.Decode(bytes),
            3 => WorldStepCodecV3.Decode(bytes),
            _ => throw new FormatException("Unsupported world step version.")
        };
}
