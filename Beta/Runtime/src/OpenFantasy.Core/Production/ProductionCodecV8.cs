namespace OpenFantasy.Core.Production;

/// <summary>Explicit excavation work archive, requiring its actual closing owner and genuine shared-work proofs.</summary>
public static class ProductionCodecV8
{
    public const int MaximumSnapshotByteLength = ProductionCodecV1.MaximumSnapshotByteLength;
    public static byte[] Encode(ProductionSnapshot state) => ProductionMaterialCodec.Encode(state, 8);
    public static ProductionSnapshot Decode(ReadOnlySpan<byte> bytes, ProductionContinuationContext context,
        IReadOnlyCollection<OpenFantasy.Core.Terrain.ExcavationWorkEvidence> excavationEvidence)
    {
        ArgumentNullException.ThrowIfNull(excavationEvidence);
        return ProductionMaterialCodec.Decode(bytes, context, 8, [], [], [], [], excavationEvidence);
    }
}
