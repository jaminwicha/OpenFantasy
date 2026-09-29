namespace OpenFantasy.Core.Production;

/// <summary>
/// Explicit V10 ordered material interpretation with bilateral consented transfers of raw and
/// produced stock, bound to its exact context. Decoding checks canonical structure and context;
/// ValidateForAuthority must re-decide every recorded approval against replayed owners and compare
/// the complete material result. Neither operation authenticates a host or admits a world.
/// </summary>
public static class ProductionCodecV10
{
    public const int MaximumSnapshotByteLength = ProductionCodecV1.MaximumSnapshotByteLength;
    internal const ulong Version = 10;

    public static byte[] Encode(ProductionSnapshot state) => ProductionMaterialCodec.Encode(state, Version);

    public static ProductionSnapshot Decode(ReadOnlySpan<byte> bytes, ProductionContinuationContext context,
        IReadOnlyCollection<OpenFantasy.Core.Gathering.GatherIssueEvidence> gatherEvidence) =>
        ProductionMaterialCodec.Decode(bytes, context, Version, gatherEvidence,
            Array.Empty<OpenFantasy.Core.Construction.ConstructionCompletionEvidence>(),
            Array.Empty<OpenFantasy.Core.Geography.TerrainRepresentationEvidence>());
}
