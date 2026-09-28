namespace OpenFantasy.Core.Production;

/// <summary>Explicit V5 ordered material interpretation, bound to its exact context and actual opaque evidence.</summary>
public static class ProductionCodecV5
{
    public const int MaximumSnapshotByteLength = ProductionCodecV1.MaximumSnapshotByteLength;

    public static byte[] Encode(ProductionSnapshot state) => ProductionMaterialCodec.Encode(state, 5);

    public static ProductionSnapshot Decode(ReadOnlySpan<byte> bytes, ProductionContinuationContext context,
        IReadOnlyCollection<OpenFantasy.Core.Gathering.GatherIssueEvidence> gatherEvidence,
        IReadOnlyCollection<OpenFantasy.Core.Construction.ConstructionCompletionEvidence> constructionEvidence) =>
        ProductionMaterialCodec.Decode(bytes, context, 5, gatherEvidence, constructionEvidence, Array.Empty<OpenFantasy.Core.Geography.TerrainRepresentationEvidence>());
}
