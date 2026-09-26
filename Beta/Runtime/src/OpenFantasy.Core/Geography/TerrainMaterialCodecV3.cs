namespace OpenFantasy.Core.Geography;

/// <summary>Explicit consumption-capable archive; raw-only and recipe-only formats remain separate contracts.</summary>
public static class TerrainMaterialCodecV3
{
    public const int MaximumEncodedByteLength = TerrainMaterialCodecV2.MaximumEncodedByteLength;

    public static byte[] Encode(TerrainMaterialState state) =>
        TerrainMaterialCodecV1.EncodeState(state, withProduction: true, consumption: true);

    public static TerrainMaterialState Decode(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken = default) =>
        TerrainMaterialCodecV1.DecodeState(bytes, withProduction: true, cancellationToken, consumption: true);
}
