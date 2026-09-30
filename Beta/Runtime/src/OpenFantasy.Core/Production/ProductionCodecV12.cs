using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Markets;

namespace OpenFantasy.Core.Production;

/// <summary>
/// Ordered paid material history with explicit genuine settlement proofs supplied by the caller.
/// Decoding verifies structure; material authority replays goods. Neither admits a durable world.
/// </summary>
public static class ProductionCodecV12
{
    public const int MaximumSnapshotByteLength = ProductionCodecV1.MaximumSnapshotByteLength;
    internal const ulong Version = 12;

    public static byte[] Encode(ProductionSnapshot state) => ProductionMaterialCodec.Encode(state, Version);

    public static ProductionSnapshot Decode(ReadOnlySpan<byte> bytes, ProductionContinuationContext context,
        IReadOnlyCollection<SpotSettlementEvidence> settlements,
        IReadOnlyCollection<GatherIssueEvidence>? gathering = null) =>
        ProductionMaterialCodec.Decode(bytes, context, Version, gathering ?? [], [], [],
            settlementEvidence: settlements);
}
