namespace OpenFantasy.Core.Geography;

/// <summary>Explicit consumption-capable archive retaining complete production disposition history.</summary>
public static class ReservoirTransportCodecV2
{
    public const int MaximumEncodedByteLength = ReservoirTransportCodecV1.MaximumEncodedByteLength;

    public static byte[] Encode(ReservoirTransportState state) => ReservoirTransportCodecV1.EncodeTransport(state, consumption: true);

    public static ReservoirTransportState Decode(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken = default) =>
        ReservoirTransportCodecV1.DecodeTransport(bytes, cancellationToken, consumption: true);
}
