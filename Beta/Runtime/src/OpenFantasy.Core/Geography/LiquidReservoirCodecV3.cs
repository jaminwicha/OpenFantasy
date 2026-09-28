namespace OpenFantasy.Core.Geography;

/// <summary>Continued reservoir bytes; the same external context and evidence pass through every nested production-bearing frame.</summary>
public static class LiquidReservoirCodecV3
{
    public const int MaximumEncodedByteLength = LiquidReservoirCodecV1.MaximumEncodedByteLength;

    public static byte[] Encode(LiquidReservoirState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return LiquidReservoirCodecV1.EncodeReservoirs(state, state.Capacity.Production?.SupportsConsumption == true, continued: true);
    }

    public static LiquidReservoirState Decode(ReadOnlySpan<byte> bytes, GeographyProductionDependencies dependencies,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        return LiquidReservoirCodecV1.DecodeReservoirs(bytes, cancellationToken, dependencies.Context.SupportsConsumption, dependencies);
    }
}
