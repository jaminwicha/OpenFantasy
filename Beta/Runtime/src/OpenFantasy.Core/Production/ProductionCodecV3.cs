namespace OpenFantasy.Core.Production;

/// <summary>
/// Continued production bytes bound to an exact externally supplied closing context. Decoding
/// validates structural rights, not a world journal, source issuance or the authenticity of a host.
/// The context is captured from actual closing owners and has no arbitrary-grant decoder.
/// </summary>
public static class ProductionCodecV3
{
    public const int MaximumSnapshotByteLength = ProductionCodecV1.MaximumSnapshotByteLength;
    private const int MaximumBatchCollectionByteLength = 9 * 1024 * 1024;
    private const int MaximumLotCollectionByteLength = 3 * 1024 * 1024;
    private const int MaximumConsumedCollectionByteLength = 164 * 1024;

    public static byte[] Encode(ProductionSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Continuation is not { } context || context.SupportsMaterialOperations || context.SupportsCompleteWorld)
            throw new ArgumentException("Production V3 requires an explicitly continued owner.", nameof(state));
        return EncodeFrame(state, context, 3);
    }

    /// <summary>Shared continued frame; each public codec selects its own interpretation and version.</summary>
    internal static byte[] EncodeFrame(ProductionSnapshot state, ProductionContinuationContext context, ulong version)
    {
        byte[] bytes = CanonicalEncodingV1.Frame("production.snapshot", Number(version), state.WorldId.ToByteArray(),
            state.BranchId.ToByteArray(), Number(state.Revision), context.ContentHash.ToByteArray(),
            Number(state.TotalBatchCount), Number(state.TotalConsumedItemCount),
            Collection("production.batches", state.Batches.Select(ProductionCodecV1.EncodeBatch),
                ProductionCodecV1.MaximumBatchByteLength, MaximumBatchCollectionByteLength),
            Collection("production.lots", state.Lots.Select(ProductionCodecV1.EncodeLot),
                ProductionCodecV1.MaximumLotByteLength, MaximumLotCollectionByteLength),
            Collection("production.consumed", state.ConsumedItems.Select(ProductionCodecV2.EncodeClaim),
                ProductionCodecV2.MaximumConsumedItemByteLength, MaximumConsumedCollectionByteLength));
        if (bytes.Length > MaximumSnapshotByteLength)
            throw CanonicalProductionReader.Failure(ProductionFormatFailure.LimitExceeded,
                "The continued production owner exceeds its encoded byte bound.");
        return bytes;
    }

    public static ProductionSnapshot Decode(ReadOnlySpan<byte> bytes, ProductionContinuationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return DecodeFrame(bytes, context, 3);
    }

    internal static ProductionSnapshot DecodeFrame(ReadOnlySpan<byte> bytes, ProductionContinuationContext context, ulong version)
    {
        try
        {
            var reader = new CanonicalProductionReader(bytes, MaximumSnapshotByteLength, 11);
            reader.ExpectTag("production.snapshot"u8);
            reader.ExpectVersion(version);
            EntityId world = reader.ReadIdentity();
            EntityId branch = reader.ReadIdentity();
            ulong revision = reader.ReadUnsigned64();
            if (!reader.ReadField(32).SequenceEqual(context.ContentHash.ToByteArray()))
                throw new ArgumentException("The supplied closing context does not match this production segment.");
            ulong totalBatches = reader.ReadUnsigned64();
            ulong totalConsumed = reader.ReadUnsigned64();
            var batches = new CanonicalProductionReader(reader.ReadField(MaximumBatchCollectionByteLength),
                MaximumBatchCollectionByteLength, ProductionSnapshot.MaximumBatches + 1);
            batches.ExpectTag("production.batches"u8);
            List<ProductionBatch> batchValues = [];
            while (batches.RemainingFields != 0)
                batchValues.Add(ProductionCodecV1.DecodeBatch(batches.ReadField(ProductionCodecV1.MaximumBatchByteLength)));
            batches.Complete();
            var lots = new CanonicalProductionReader(reader.ReadField(MaximumLotCollectionByteLength),
                MaximumLotCollectionByteLength, ProductionSnapshot.MaximumLots + 1);
            lots.ExpectTag("production.lots"u8);
            List<TransformedLot> lotValues = [];
            while (lots.RemainingFields != 0)
                lotValues.Add(ProductionCodecV1.DecodeLot(lots.ReadField(ProductionCodecV1.MaximumLotByteLength)));
            lots.Complete();
            var consumed = new CanonicalProductionReader(reader.ReadField(MaximumConsumedCollectionByteLength),
                MaximumConsumedCollectionByteLength, ProductionSnapshot.MaximumConsumedItems + 1);
            consumed.ExpectTag("production.consumed"u8);
            List<ConsumedItemClaim> claims = [];
            while (consumed.RemainingFields != 0)
                claims.Add(ProductionCodecV2.DecodeClaim(consumed.ReadField(ProductionCodecV2.MaximumConsumedItemByteLength)));
            consumed.Complete();
            reader.Complete();
            var result = new ProductionSnapshot(world, branch, revision, context.Recipes, batchValues, lotValues,
                context.SupportsConsumption ? claims : null, context);
            if (!context.SupportsConsumption && claims.Count != 0
                || result.TotalBatchCount != totalBatches || result.TotalConsumedItemCount != totalConsumed)
                throw new ArgumentException("Continued production counters and consumption profile must match their exact context.");
            if (!bytes.SequenceEqual(version == ProductionCodecV9.Version ? ProductionCodecV9.Encode(result) : Encode(result)))
                throw CanonicalProductionReader.Failure(ProductionFormatFailure.NonCanonical,
                    "Continued production fields must be canonically ordered.");
            return result;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw CanonicalProductionReader.Failure(ProductionFormatFailure.InvalidValue,
                "Continued production violates its closing context or lineage invariants: " + error.Message);
        }
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);

    private static byte[] Collection(string tag, IEnumerable<byte[]> values, int maximumItemLength, int maximumLength)
    {
        List<ReadOnlyMemory<byte>> fields = [];
        long length = 16 + System.Text.Encoding.UTF8.GetByteCount(tag);
        foreach (byte[] value in values)
        {
            length = checked(length + 4 + value.Length);
            if (value.Length > maximumItemLength || length > maximumLength)
                throw CanonicalProductionReader.Failure(ProductionFormatFailure.LimitExceeded,
                    "A continued production collection exceeds its byte bound.");
            fields.Add(value);
        }
        return CanonicalEncodingV1.Frame(tag, fields.ToArray());
    }
}
