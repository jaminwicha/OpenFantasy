namespace OpenFantasy.Core.Geography;

/// <summary>Explicit consumption-capable archive retaining complete production disposition history.</summary>
public static class WatershedRunoffCapacityCodecV2
{
    public const int MaximumEncodedByteLength = WatershedRunoffCapacityCodecV1.MaximumEncodedByteLength;

    public static byte[] Encode(WatershedRunoffCapacityState state) => WatershedRunoffCapacityCodecV1.EncodeCapacity(state, consumption: true);

    public static WatershedRunoffCapacityState Decode(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken = default) =>
        WatershedRunoffCapacityCodecV1.DecodeCapacity(bytes, cancellationToken, consumption: true);
}
