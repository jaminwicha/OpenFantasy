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
            36 => WorldStepCodecV36.Encode(batch),
            35 => WorldStepCodecV35.Encode(batch),
            34 => WorldStepCodecV34.Encode(batch),
            33 => WorldStepCodecV33.Encode(batch),
            32 => WorldStepCodecV32.Encode(batch),
            30 => WorldStepCodecV30.Encode(batch),
            28 => WorldStepCodecV28.Encode(batch),
            27 => WorldStepCodecV27.Encode(batch),
            26 => WorldStepCodecV26.Encode(batch),
            25 => WorldStepCodecV25.Encode(batch),
            24 => WorldStepCodecV24.Encode(batch),
            23 => WorldStepCodecV23.Encode(batch),
            22 => WorldStepCodecV22.Encode(batch),
            21 => WorldStepCodecV21.Encode(batch),
            20 => WorldStepCodecV20.Encode(batch),
            19 => WorldStepCodecV19.Encode(batch),
            18 => WorldStepCodecV18.Encode(batch),
            17 => WorldStepCodecV17.Encode(batch),
            16 => WorldStepCodecV16.Encode(batch),
            15 => WorldStepCodecV15.Encode(batch),
            14 => WorldStepCodecV14.Encode(batch),
            13 => WorldStepCodecV13.Encode(batch),
            12 => WorldStepCodecV12.Encode(batch),
            11 => WorldStepCodecV11.Encode(batch),
            10 => WorldStepCodecV10.Encode(batch),
            9 => WorldStepCodecV9.Encode(batch),
            8 => WorldStepCodecV8.Encode(batch),
            7 => WorldStepCodecV7.Encode(batch),
            6 => WorldStepCodecV6.Encode(batch),
            5 => WorldStepCodecV5.Encode(batch),
            4 => WorldStepCodecV4.Encode(batch),
            _ => throw new ArgumentException("Unsupported world step profile.", nameof(batch))
        };
    }

    public static WorldStepBatch Decode(ReadOnlySpan<byte> bytes) =>
        WorldCodecVersion.Read(bytes, "world.step-batch"u8, MaximumBatchByteLength, 11) switch
        {
            1 => WorldStepCodecV1.Decode(bytes),
            2 => WorldStepCodecV2.Decode(bytes),
            3 => WorldStepCodecV3.Decode(bytes),
            36 => WorldStepCodecV36.Decode(bytes),
            35 => WorldStepCodecV35.Decode(bytes),
            34 => WorldStepCodecV34.Decode(bytes),
            33 => WorldStepCodecV33.Decode(bytes),
            32 => WorldStepCodecV32.Decode(bytes),
            30 => WorldStepCodecV30.Decode(bytes),
            28 => WorldStepCodecV28.Decode(bytes),
            27 => WorldStepCodecV27.Decode(bytes),
            26 => WorldStepCodecV26.Decode(bytes),
            25 => WorldStepCodecV25.Decode(bytes),
            24 => WorldStepCodecV24.Decode(bytes),
            23 => WorldStepCodecV23.Decode(bytes),
            22 => WorldStepCodecV22.Decode(bytes),
            21 => WorldStepCodecV21.Decode(bytes),
            20 => WorldStepCodecV20.Decode(bytes),
            19 => WorldStepCodecV19.Decode(bytes),
            18 => WorldStepCodecV18.Decode(bytes),
            17 => WorldStepCodecV17.Decode(bytes),
            16 => WorldStepCodecV16.Decode(bytes),
            15 => WorldStepCodecV15.Decode(bytes),
            14 => WorldStepCodecV14.Decode(bytes),
            13 => WorldStepCodecV13.Decode(bytes),
            12 => WorldStepCodecV12.Decode(bytes),
            11 => WorldStepCodecV11.Decode(bytes),
            10 => WorldStepCodecV10.Decode(bytes),
            9 => WorldStepCodecV9.Decode(bytes),
            8 => WorldStepCodecV8.Decode(bytes),
            7 => WorldStepCodecV7.Decode(bytes),
            6 => WorldStepCodecV6.Decode(bytes),
            5 => WorldStepCodecV5.Decode(bytes),
            4 => WorldStepCodecV4.Decode(bytes),
            _ => throw new FormatException("Unsupported world step version.")
        };
}
