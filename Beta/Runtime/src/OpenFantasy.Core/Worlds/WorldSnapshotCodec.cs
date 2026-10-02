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
            36 => WorldSnapshotCodecV36.Encode(state),
            35 => WorldSnapshotCodecV35.Encode(state),
            34 => WorldSnapshotCodecV34.Encode(state),
            33 => WorldSnapshotCodecV33.Encode(state),
            32 => WorldSnapshotCodecV32.Encode(state),
            30 => WorldSnapshotCodecV30.Encode(state),
            28 => WorldSnapshotCodecV28.Encode(state),
            27 => WorldSnapshotCodecV27.Encode(state),
            26 => WorldSnapshotCodecV26.Encode(state),
            25 => WorldSnapshotCodecV25.Encode(state),
            24 => WorldSnapshotCodecV24.Encode(state),
            23 => WorldSnapshotCodecV23.Encode(state),
            22 => WorldSnapshotCodecV22.Encode(state),
            21 => WorldSnapshotCodecV21.Encode(state),
            20 => WorldSnapshotCodecV20.Encode(state),
            19 => WorldSnapshotCodecV19.Encode(state),
            18 => WorldSnapshotCodecV18.Encode(state),
            17 => WorldSnapshotCodecV17.Encode(state),
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

    // The pre-screen admits the widest retained root (profile 36's 27 fields); each codec enforces its own exact bound.
    private const int MaximumRootFields = 27;

    public static WorldSnapshot Decode(ReadOnlySpan<byte> bytes, WorldResourceArchiveAnchor? archiveAnchor = null) =>
        WorldCodecVersion.Read(bytes, "world.snapshot"u8, MaximumStateByteLength, MaximumRootFields) switch
        {
            1 => WorldSnapshotCodecV1.Decode(bytes),
            2 => WorldSnapshotCodecV2.Decode(bytes),
            3 => WorldSnapshotCodecV3.Decode(bytes),
            36 => archiveAnchor is null ? WorldSnapshotCodecV36.Decode(bytes)
                : throw new FormatException("A controller world requires its joint archive context."),
            35 => archiveAnchor is null ? WorldSnapshotCodecV35.Decode(bytes)
                : throw new FormatException("The outcome-continuation world requires its complete archive context."),
            34 => archiveAnchor is null ? WorldSnapshotCodecV34.Decode(bytes)
                : throw new FormatException("The retreat-capable world requires its complete archive context."),
            33 => archiveAnchor is null ? WorldSnapshotCodecV33.Decode(bytes)
                : throw new FormatException("The local-market world requires its complete archive context."),
            32 => archiveAnchor is null ? WorldSnapshotCodecV32.Decode(bytes)
                : throw new FormatException("The paid world requires its complete archive context."),
            30 => archiveAnchor is null ? WorldSnapshotCodecV30.Decode(bytes)
                : throw new FormatException("The saved-proposal world requires its complete archive context."),
            28 => archiveAnchor is null ? WorldSnapshotCodecV28.Decode(bytes)
                : throw new FormatException("The consented world requires its complete archive context."),
            27 => archiveAnchor is null ? WorldSnapshotCodecV27.Decode(bytes)
                : throw new FormatException("The coordinated world does not accept a resource-only archive anchor."),
            26 => WorldSnapshotCodecV26.Decode(bytes, archiveAnchor),
            25 => WorldSnapshotCodecV25.Decode(bytes, archiveAnchor),
            24 => WorldSnapshotCodecV24.Decode(bytes),
            23 => WorldSnapshotCodecV23.Decode(bytes),
            22 => WorldSnapshotCodecV22.Decode(bytes),
            21 => WorldSnapshotCodecV21.Decode(bytes),
            20 => WorldSnapshotCodecV20.Decode(bytes),
            19 => WorldSnapshotCodecV19.Decode(bytes),
            18 => WorldSnapshotCodecV18.Decode(bytes),
            17 => WorldSnapshotCodecV17.Decode(bytes),
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

    /// <summary>
    /// Explicit joint decode context of the coordinated world. A null anchor restores only a world that
    /// has not yet cut; every other profile keeps its own unchanged entrypoint.
    /// </summary>
    public static WorldSnapshot DecodeCoordinated(ReadOnlySpan<byte> bytes, WorldMaterialCareArchiveAnchor? coordinatedAnchor) =>
        WorldCodecVersion.Read(bytes, "world.snapshot"u8, MaximumStateByteLength, MaximumRootFields) switch
        {
            27 => WorldSnapshotCodecV27.Decode(bytes, coordinatedAnchor),
            28 => WorldSnapshotCodecV28.Decode(bytes, coordinatedAnchor),
            30 => WorldSnapshotCodecV30.Decode(bytes, coordinatedAnchor),
            32 => WorldSnapshotCodecV32.Decode(bytes, coordinatedAnchor),
            33 => WorldSnapshotCodecV33.Decode(bytes, coordinatedAnchor),
            34 => WorldSnapshotCodecV34.Decode(bytes, coordinatedAnchor),
            36 => WorldSnapshotCodecV36.Decode(bytes, coordinatedAnchor),
            35 => WorldSnapshotCodecV35.Decode(bytes, coordinatedAnchor),
            _ => throw new FormatException("Only coordinated world profiles accept a joint decode context.")
        };
}
