using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Care;

/// <summary>Selects the admitted care interpretation without upgrading legacy histories.</summary>
public static class CareCodec
{
    public const int MaximumEncodedByteLength = CareCodecV2.MaximumEncodedByteLength;

    public static byte[] Encode(CareSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.SupportsOrdinary ? CareCodecV2.Encode(state) : CareCodecV1.Encode(state);
    }

    public static CareSnapshot Decode(ReadOnlySpan<byte> bytes) =>
        WorldCodecVersion.Read(bytes, "care.snapshot"u8, MaximumEncodedByteLength, 10) switch
        {
            1 => CareCodecV1.Decode(bytes),
            2 => CareCodecV2.Decode(bytes),
            _ => throw new FormatException("Unsupported care snapshot version.")
        };
}
