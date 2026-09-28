namespace OpenFantasy.Core.Geography;

/// <summary>Reserved-transport material bytes qualified by the actual complete owner and required opaque delivery evidence.</summary>
public static class ReservoirTransportCodecV3
{
    public const int MaximumEncodedByteLength = ReservoirTransportCodecV1.MaximumEncodedByteLength;
    public static byte[] Encode(ReservoirTransportState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return ReservoirTransportCodecV1.EncodeTransport(state, state.Reservoirs.Capacity.Production?.SupportsConsumption == true, continued: true);
    }
    public static ReservoirTransportState Decode(ReadOnlySpan<byte> bytes, GeographyProductionDependencies dependencies,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        if (dependencies.TransportQualification is null || !dependencies.Context.SupportsReservedTransport)
            throw new ArgumentException("Reserved transport decoding requires its actual complete owner qualification.", nameof(dependencies));
        return ReservoirTransportCodecV1.DecodeTransport(bytes, cancellationToken, dependencies.Context.SupportsConsumption, dependencies);
    }
}
