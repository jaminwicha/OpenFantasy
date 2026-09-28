namespace OpenFantasy.Core.Geography;

/// <summary>Explicit continued terrain bytes requiring the exact qualified registry context and actual operation evidence.</summary>
public static class TerrainMaterialRegistryCodecV4
{
    public const int MaximumEncodedByteLength = TerrainMaterialRegistryCodecV2.MaximumEncodedByteLength;

    public static byte[] Encode(TerrainMaterialRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        return TerrainMaterialRegistryCodecV1.EncodeRegistry(registry, withProduction: true,
            consumption: registry.Production?.SupportsConsumption == true, continued: true);
    }

    public static TerrainMaterialRegistry Decode(ReadOnlySpan<byte> bytes, TerrainProductionDependencies dependencies,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        return TerrainMaterialRegistryCodecV1.DecodeRegistry(bytes, true, cancellationToken, dependencies.Context.SupportsConsumption, dependencies);
    }
}
