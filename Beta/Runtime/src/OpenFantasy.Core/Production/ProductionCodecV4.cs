namespace OpenFantasy.Core.Production;

/// <summary>Explicit V4 ordered material interpretation, bound to its exact context and actual opaque evidence.</summary>
public static class ProductionCodecV4
{
    public const int MaximumSnapshotByteLength = ProductionCodecV1.MaximumSnapshotByteLength;

    public static byte[] Encode(ProductionSnapshot state) => ProductionMaterialCodec.Encode(state, 4);

    public static ProductionSnapshot Decode(ReadOnlySpan<byte> bytes, ProductionContinuationContext context,
        IReadOnlyCollection<OpenFantasy.Core.Gathering.GatherIssueEvidence> gatherEvidence) =>
        ProductionMaterialCodec.Decode(bytes, context, 4, gatherEvidence, Array.Empty<OpenFantasy.Core.Construction.ConstructionCompletionEvidence>(), Array.Empty<OpenFantasy.Core.Geography.TerrainRepresentationEvidence>());
}
