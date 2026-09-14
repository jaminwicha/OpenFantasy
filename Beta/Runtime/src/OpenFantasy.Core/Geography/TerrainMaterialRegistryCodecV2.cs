namespace OpenFantasy.Core.Geography;

/// <summary>Explicit production-backed archive; the raw-only v1 format remains a separate contract.</summary>
public static class TerrainMaterialRegistryCodecV2
{
    public const int MaximumEncodedByteLength = 23_100_000;

    public static byte[] Encode(TerrainMaterialRegistry state) => TerrainMaterialRegistryCodecV1.EncodeRegistry(state, withProduction: true);

    public static TerrainMaterialRegistry Decode(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken = default) =>
        TerrainMaterialRegistryCodecV1.DecodeRegistry(bytes, withProduction: true, cancellationToken);
}
