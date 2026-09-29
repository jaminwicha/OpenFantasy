namespace OpenFantasy.Core.Production;

/// <summary>
/// Continued production whose custody is admitted with the complete world's ordered consent
/// evidence. The frame retains material rights; decoding alone does not admit a world or authenticate history.
/// </summary>
public static class ProductionCodecV11
{
    public const int MaximumSnapshotByteLength = ProductionCodecV3.MaximumSnapshotByteLength;
    internal const ulong Version = 11;

    public static byte[] Encode(ProductionSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Continuation is not { SupportsCompleteWorld: true, SupportsConsentedWorld: true } context)
        {
            throw new ArgumentException("Production V11 requires the consented complete-world interpretation.", nameof(state));
        }
        return ProductionCodecV3.EncodeFrame(state, context, Version);
    }

    public static ProductionSnapshot Decode(ReadOnlySpan<byte> bytes, ProductionContinuationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.SupportsCompleteWorld || !context.SupportsConsentedWorld)
        {
            throw new ArgumentException("Production V11 requires its exact consented world context.", nameof(context));
        }
        return ProductionCodecV3.DecodeFrame(bytes, context, Version);
    }
}
