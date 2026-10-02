using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Care;

/// <summary>Selects the admitted care interpretation without upgrading legacy histories.</summary>
public static class CareCodec
{
    public const int MaximumEncodedByteLength = CareCodecV2.MaximumEncodedByteLength;

    public static byte[] Encode(CareSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.SupportsDispositions) return CareCodecV5.Encode(state);
        if (state.Ordinary?.SupportsIncapacitatedAssistance == true) return CareCodecV4.Encode(state);
        if (state.Continuation is not null) return CareCodecV3.Encode(state);
        return state.SupportsOrdinary ? CareCodecV2.Encode(state) : CareCodecV1.Encode(state);
    }

    public static CareSnapshot Decode(ReadOnlySpan<byte> bytes) =>
        WorldCodecVersion.Read(bytes, "care.snapshot"u8, MaximumEncodedByteLength, 16) switch
        {
            1 => CareCodecV1.Decode(bytes),
            2 => CareCodecV2.Decode(bytes),
            3 => throw new FormatException("Continued care requires its exact continuation context."),
            4 => CareCodecV4.Decode(bytes),
            5 => CareCodecV5.Decode(bytes),
            _ => throw new FormatException("Unsupported care snapshot version.")
        };

    /// <summary>Continued bytes need their exact context; inline bytes refuse an extraneous one.</summary>
    public static CareSnapshot Decode(ReadOnlySpan<byte> bytes, CareContinuationContext? context)
    {
        if (context is null) return Decode(bytes);
        return WorldCodecVersion.Read(bytes, "care.snapshot"u8, MaximumEncodedByteLength, 16) switch
        {
            3 => CareCodecV3.Decode(bytes, context),
            4 => CareCodecV4.Decode(bytes, context),
            5 => CareCodecV5.Decode(bytes, context),
            _ => throw new FormatException("Only continued care accepts a continuation context.")
        };
    }

    internal static byte[] EncodeUseDefinition(RestorativeUseDefinition value) => CareCodecV2.EncodeUseDefinition(value, true);
    internal static byte[] EncodeUse(RestorativeUse value) => CareCodecV2.EncodeUse(value, true);
}
