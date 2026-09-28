namespace OpenFantasy.Core.Geography;

/// <summary>Explicit V7 reservoir bytes requiring actual transport qualification and opaque material evidence.</summary>
public static class LiquidReservoirCodecV4
{
    public const int MaximumEncodedByteLength = LiquidReservoirCodecV1.MaximumEncodedByteLength;

    public static byte[] Encode(LiquidReservoirState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return LiquidReservoirCodecV1.EncodeReservoirs(state, state.Capacity.Production?.SupportsConsumption == true, continued: true, transport: true);
    }

    public static LiquidReservoirState Decode(ReadOnlySpan<byte> bytes, GeographyProductionDependencies dependencies,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        return LiquidReservoirCodecV1.DecodeReservoirs(bytes, cancellationToken, dependencies.Context.SupportsConsumption, dependencies, transport: true);
    }
}
