namespace OpenFantasy.Core.Production;

/// <summary>Explicit V6 ordered material interpretation, bound to its exact context and actual opaque evidence.</summary>
public static class ProductionCodecV6
{
    public const int MaximumSnapshotByteLength = ProductionCodecV1.MaximumSnapshotByteLength;

    public static byte[] Encode(ProductionSnapshot state) => ProductionMaterialCodec.Encode(state, 6);

    public static ProductionSnapshot Decode(ReadOnlySpan<byte> bytes, ProductionContinuationContext context,
        IReadOnlyCollection<OpenFantasy.Core.Gathering.GatherIssueEvidence> gatherEvidence,
        IReadOnlyCollection<OpenFantasy.Core.Construction.ConstructionCompletionEvidence> constructionEvidence,
        IReadOnlyCollection<OpenFantasy.Core.Geography.TerrainRepresentationEvidence> terrainEvidence) =>
        ProductionMaterialCodec.Decode(bytes, context, 6, gatherEvidence, constructionEvidence, terrainEvidence);
}
