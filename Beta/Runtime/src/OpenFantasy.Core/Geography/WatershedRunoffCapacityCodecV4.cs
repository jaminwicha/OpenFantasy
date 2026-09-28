namespace OpenFantasy.Core.Geography;

/// <summary>Explicit V7 capacity bytes requiring actual transport qualification and opaque material evidence.</summary>
public static class WatershedRunoffCapacityCodecV4
{
    public const int MaximumEncodedByteLength = WatershedRunoffCapacityCodecV1.MaximumEncodedByteLength;

    public static byte[] Encode(WatershedRunoffCapacityState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return WatershedRunoffCapacityCodecV1.EncodeCapacity(state, state.Production?.SupportsConsumption == true, continued: true, transport: true);
    }

    public static WatershedRunoffCapacityState Decode(ReadOnlySpan<byte> bytes, GeographyProductionDependencies dependencies,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        return WatershedRunoffCapacityCodecV1.DecodeCapacity(bytes, cancellationToken, dependencies.Context.SupportsConsumption, dependencies, transport: true);
    }
}
