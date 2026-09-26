namespace OpenFantasy.Core.Geography;

/// <summary>Explicit consumption-capable archive retaining complete production disposition history.</summary>
public static class LiquidReservoirCodecV2
{
    public const int MaximumEncodedByteLength = LiquidReservoirCodecV1.MaximumEncodedByteLength;

    public static byte[] Encode(LiquidReservoirState state) => LiquidReservoirCodecV1.EncodeReservoirs(state, consumption: true);

    public static LiquidReservoirState Decode(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken = default) =>
        LiquidReservoirCodecV1.DecodeReservoirs(bytes, cancellationToken, consumption: true);
}
