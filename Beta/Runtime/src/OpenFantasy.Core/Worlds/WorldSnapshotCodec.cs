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
            _ => throw new ArgumentException("Unsupported world profile.", nameof(state))
        };
    }

    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes) =>
        WorldCodecVersion.Read(bytes, "world.snapshot"u8, MaximumStateByteLength, 11) switch
        {
            1 => WorldSnapshotCodecV1.Decode(bytes),
            2 => WorldSnapshotCodecV2.Decode(bytes),
            _ => throw new FormatException("Unsupported saved world profile.")
        };
}
