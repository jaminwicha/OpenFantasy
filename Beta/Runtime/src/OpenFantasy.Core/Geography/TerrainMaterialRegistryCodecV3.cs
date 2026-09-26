namespace OpenFantasy.Core.Geography;

/// <summary>Explicit consumption-capable archive; raw-only and recipe-only formats remain separate contracts.</summary>
public static class TerrainMaterialRegistryCodecV3
{
    public const int MaximumEncodedByteLength = TerrainMaterialRegistryCodecV2.MaximumEncodedByteLength;

    public static byte[] Encode(TerrainMaterialRegistry state) =>
        TerrainMaterialRegistryCodecV1.EncodeRegistry(state, withProduction: true, consumption: true);

    public static TerrainMaterialRegistry Decode(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken = default) =>
        TerrainMaterialRegistryCodecV1.DecodeRegistry(bytes, withProduction: true, cancellationToken, consumption: true);
}
