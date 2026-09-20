namespace OpenFantasy.Core.Worlds;

/// <summary>Dispatches known saved profiles without guessing missing components or changing legacy bytes.</summary>
public static class WorldSnapshotCodec
{
    public const int MaximumStateByteLength = WorldSnapshotCodecV1.MaximumStateByteLength;

    public static byte[] Encode(WorldSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.ProfileVersion switch
        {
            1 => WorldSnapshotCodecV1.Encode(state),
            2 => WorldSnapshotCodecV2.Encode(state),
            3 => WorldSnapshotCodecV3.Encode(state),
            16 => WorldSnapshotCodecV16.Encode(state),
            15 => WorldSnapshotCodecV15.Encode(state),
            14 => WorldSnapshotCodecV14.Encode(state),
            13 => WorldSnapshotCodecV13.Encode(state),
            12 => WorldSnapshotCodecV12.Encode(state),
            11 => WorldSnapshotCodecV11.Encode(state),
            10 => WorldSnapshotCodecV10.Encode(state),
            9 => WorldSnapshotCodecV9.Encode(state),
            8 => WorldSnapshotCodecV8.Encode(state),
            7 => WorldSnapshotCodecV7.Encode(state),
            6 => WorldSnapshotCodecV6.Encode(state),
            5 => WorldSnapshotCodecV5.Encode(state),
            4 => WorldSnapshotCodecV4.Encode(state),
            _ => throw new ArgumentException("Unsupported world profile.", nameof(state))
        };
    }

    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes) =>
        WorldCodecVersion.Read(bytes, "world.snapshot"u8, MaximumStateByteLength, 22) switch
        {
            1 => WorldSnapshotCodecV1.Decode(bytes),
            2 => WorldSnapshotCodecV2.Decode(bytes),
            3 => WorldSnapshotCodecV3.Decode(bytes),
            16 => WorldSnapshotCodecV16.Decode(bytes),
            15 => WorldSnapshotCodecV15.Decode(bytes),
            14 => WorldSnapshotCodecV14.Decode(bytes),
            13 => WorldSnapshotCodecV13.Decode(bytes),
            12 => WorldSnapshotCodecV12.Decode(bytes),
            11 => WorldSnapshotCodecV11.Decode(bytes),
            10 => WorldSnapshotCodecV10.Decode(bytes),
            9 => WorldSnapshotCodecV9.Decode(bytes),
            8 => WorldSnapshotCodecV8.Decode(bytes),
            7 => WorldSnapshotCodecV7.Decode(bytes),
            6 => WorldSnapshotCodecV6.Decode(bytes),
            5 => WorldSnapshotCodecV5.Decode(bytes),
            4 => WorldSnapshotCodecV4.Decode(bytes),
            _ => throw new FormatException("Unsupported saved world profile.")
        };
}
