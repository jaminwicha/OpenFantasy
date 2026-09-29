namespace OpenFantasy.Core.Production;

/// <summary>
/// Continued production owned by the coordinated complete world. Crafting and consumption keep their
/// revision meaning; raw custody, gathering and work remain with their actual world owners. Decoding
/// validates structure against the exact context, never source issuance or host authentication.
/// </summary>
public static class ProductionCodecV9
{
    public const int MaximumSnapshotByteLength = ProductionCodecV3.MaximumSnapshotByteLength;
    internal const ulong Version = 9;

    public static byte[] Encode(ProductionSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Continuation is not { SupportsCompleteWorld: true, SupportsConsentedWorld: false } context)
            throw new ArgumentException("Production V9 requires the coordinated complete-world owner.", nameof(state));
        return ProductionCodecV3.EncodeFrame(state, context, Version);
    }

    public static ProductionSnapshot Decode(ReadOnlySpan<byte> bytes, ProductionContinuationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.SupportsCompleteWorld || context.SupportsConsentedWorld)
            throw new ArgumentException("Production V9 requires the coordinated complete-world context.", nameof(context));
        return ProductionCodecV3.DecodeFrame(bytes, context, Version);
    }
}
