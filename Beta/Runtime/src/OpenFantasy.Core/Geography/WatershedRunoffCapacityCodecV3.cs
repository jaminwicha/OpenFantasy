namespace OpenFantasy.Core.Geography;

/// <summary>Continued capacity bytes with exact external production context and current-segment opaque evidence.</summary>
public static class WatershedRunoffCapacityCodecV3
{
    public const int MaximumEncodedByteLength = WatershedRunoffCapacityCodecV1.MaximumEncodedByteLength;

    public static byte[] Encode(WatershedRunoffCapacityState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return WatershedRunoffCapacityCodecV1.EncodeCapacity(state, state.Production?.SupportsConsumption == true, continued: true);
    }

    public static WatershedRunoffCapacityState Decode(ReadOnlySpan<byte> bytes, GeographyProductionDependencies dependencies,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        return WatershedRunoffCapacityCodecV1.DecodeCapacity(bytes, cancellationToken, dependencies.Context.SupportsConsumption, dependencies);
    }
}
