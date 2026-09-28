namespace OpenFantasy.Core.Production;

/// <summary>Explicit reserved-transport material archive bound to its actual closing owner and opaque stage proofs.</summary>
public static class ProductionCodecV7
{
    public const int MaximumSnapshotByteLength = ProductionCodecV1.MaximumSnapshotByteLength;
    public static byte[] Encode(ProductionSnapshot state) => ProductionMaterialCodec.Encode(state, 7);
    public static ProductionSnapshot Decode(ReadOnlySpan<byte> bytes, ProductionContinuationContext context,
        IReadOnlyCollection<OpenFantasy.Core.Gathering.GatherIssueEvidence> gatherEvidence,
        IReadOnlyCollection<OpenFantasy.Core.Construction.ConstructionCompletionEvidence> constructionEvidence,
        IReadOnlyCollection<OpenFantasy.Core.Geography.ReservoirTransportEvidence> transportEvidence)
    {
        ArgumentNullException.ThrowIfNull(transportEvidence);
        return ProductionMaterialCodec.Decode(bytes, context, 7, gatherEvidence, constructionEvidence, [], transportEvidence);
    }
}
