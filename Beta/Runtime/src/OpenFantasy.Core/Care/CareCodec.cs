using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Care;

/// <summary>Selects the admitted care interpretation without upgrading legacy histories.</summary>
public static class CareCodec
{
    public const int MaximumEncodedByteLength = CareCodecV2.MaximumEncodedByteLength;

    public static byte[] Encode(CareSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Continuation is not null) return CareCodecV3.Encode(state);
        return state.SupportsOrdinary ? CareCodecV2.Encode(state) : CareCodecV1.Encode(state);
    }

    public static CareSnapshot Decode(ReadOnlySpan<byte> bytes) =>
        WorldCodecVersion.Read(bytes, "care.snapshot"u8, MaximumEncodedByteLength, 10) switch
        {
            1 => CareCodecV1.Decode(bytes),
            2 => CareCodecV2.Decode(bytes),
            3 => throw new FormatException("Continued care requires its exact continuation context."),
            _ => throw new FormatException("Unsupported care snapshot version.")
        };

    /// <summary>Continued bytes need their exact context; inline bytes refuse an extraneous one.</summary>
    public static CareSnapshot Decode(ReadOnlySpan<byte> bytes, CareContinuationContext? context)
    {
        if (context is null) return Decode(bytes);
        if (WorldCodecVersion.Read(bytes, "care.snapshot"u8, MaximumEncodedByteLength, 10) != 3)
            throw new FormatException("Only continued care accepts a continuation context.");
        return CareCodecV3.Decode(bytes, context);
    }
}
