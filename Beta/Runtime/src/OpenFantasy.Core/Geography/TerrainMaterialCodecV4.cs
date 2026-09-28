namespace OpenFantasy.Core.Geography;

/// <summary>Explicit continued terrain bytes requiring the exact qualified registry context and actual operation evidence.</summary>
public static class TerrainMaterialCodecV4
{
    public const int MaximumEncodedByteLength = TerrainMaterialCodecV2.MaximumEncodedByteLength;

    public static byte[] Encode(TerrainMaterialState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return TerrainMaterialCodecV1.EncodeState(state, withProduction: true,
            consumption: state.Production?.SupportsConsumption == true, continued: true);
    }

    public static TerrainMaterialState Decode(ReadOnlySpan<byte> bytes, TerrainProductionDependencies dependencies,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        return TerrainMaterialCodecV1.DecodeState(bytes, true, cancellationToken, dependencies.Context.SupportsConsumption, dependencies);
    }
}
