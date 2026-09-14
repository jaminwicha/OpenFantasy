namespace OpenFantasy.Core.Geography;

/// <summary>Explicit production-backed archive; the raw-only v1 format remains a separate contract.</summary>
public static class TerrainMaterialCodecV2
{
    public const int MaximumEncodedByteLength = 22_500_000;

    public static byte[] Encode(TerrainMaterialState state) => TerrainMaterialCodecV1.EncodeState(state, withProduction: true);

    public static TerrainMaterialState Decode(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken = default) =>
        TerrainMaterialCodecV1.DecodeState(bytes, withProduction: true, cancellationToken);
}
