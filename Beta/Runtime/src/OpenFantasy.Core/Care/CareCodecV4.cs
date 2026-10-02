namespace OpenFantasy.Core.Care;

/// <summary>
/// Explicit incapacitated-assistance catalogs, alongside unchanged legacy delivery definitions and aftermath records.
/// Inline and continued owners retain their distinct shapes; a continued owner requires its exact external context.
/// </summary>
public static class CareCodecV4
{
    public const int MaximumEncodedByteLength = CareCodecV2.MaximumEncodedByteLength;

    public static byte[] Encode(CareSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.Continuation is null ? CareCodecV2.EncodeVersion(state, 4) : CareCodecV3.EncodeVersion(state, 4);
    }

    public static CareSnapshot Decode(ReadOnlySpan<byte> bytes) => CareCodecV2.DecodeVersion(bytes, 4);

    public static CareSnapshot Decode(ReadOnlySpan<byte> bytes, CareContinuationContext context) =>
        CareCodecV3.DecodeVersion(bytes, context, 4);

    public static byte[] EncodeContinuation(CareContinuationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Definitions?.Any(value => value.DeliveryMode == RestorativeDeliveryMode.IncapacitatedAssistance) != true)
            throw new ArgumentException("Care V4 requires an explicit recovery catalog.");
        return context.ToCommitment();
    }
}
