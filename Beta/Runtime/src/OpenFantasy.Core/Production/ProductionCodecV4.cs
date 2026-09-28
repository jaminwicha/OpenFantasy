namespace OpenFantasy.Core.Production;

/// <summary>
/// Ordered material bytes bound to the exact closing context and actual opaque gather proofs. Decoding
/// validates structural rights, not a world journal, source issuance or the authenticity of a host.
/// The context is captured from actual closing owners and has no arbitrary-grant decoder.
/// </summary>
public static class ProductionCodecV4
{
    public const int MaximumSnapshotByteLength = ProductionCodecV1.MaximumSnapshotByteLength;
    private const int MaximumBatchCollectionByteLength = 9 * 1024 * 1024;
    private const int MaximumLotCollectionByteLength = 3 * 1024 * 1024;
    private const int MaximumConsumedCollectionByteLength = 164 * 1024;

    public static byte[] Encode(ProductionSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Continuation is not { SupportsMaterialOperations: true } context || state.MaterialHistory is null)
            throw new ArgumentException("Production V4 requires an explicitly continued owner.", nameof(state));
        byte[] bytes = CanonicalEncodingV1.Frame("production.snapshot", Number(4), state.WorldId.ToByteArray(),
            state.BranchId.ToByteArray(), Number(state.Revision), context.ContentHash.ToByteArray(),
            Number(state.TotalBatchCount), Number(state.TotalConsumedItemCount), Number(state.TotalMaterialOperationCount),
            Collection("production.batches", state.Batches.Select(ProductionCodecV1.EncodeBatch),
                ProductionCodecV1.MaximumBatchByteLength, MaximumBatchCollectionByteLength),
            Collection("production.lots", state.Lots.Select(ProductionCodecV1.EncodeLot),
                ProductionCodecV1.MaximumLotByteLength, MaximumLotCollectionByteLength),
            Collection("production.consumed", state.ConsumedItems.Select(ProductionCodecV2.EncodeClaim),
                ProductionCodecV2.MaximumConsumedItemByteLength, MaximumConsumedCollectionByteLength),
            Collection("production.material-operations", state.MaterialHistory.Operations.Select(MaterialOperationCodec.Encode),
                MaterialOperationCodec.MaximumByteLength, MaximumSnapshotByteLength));
        if (bytes.Length > MaximumSnapshotByteLength)
            throw CanonicalProductionReader.Failure(ProductionFormatFailure.LimitExceeded,
                "The continued production owner exceeds its encoded byte bound.");
        return bytes;
    }

    public static ProductionSnapshot Decode(ReadOnlySpan<byte> bytes, ProductionContinuationContext context,
        IReadOnlyCollection<OpenFantasy.Core.Gathering.GatherIssueEvidence> gatherEvidence)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(gatherEvidence);
        try
        {
            if (!context.SupportsMaterialOperations || gatherEvidence.Count > MaterialOperationHistory.MaximumOperations)
                throw new ArgumentException("Material decoding requires an exact V4 context and bounded actual issuing evidence.");
            var proofs = gatherEvidence.ToDictionary(value => value.ContentHash);
            var reader = new CanonicalProductionReader(bytes, MaximumSnapshotByteLength, 13);
            reader.ExpectTag("production.snapshot"u8);
            reader.ExpectVersion(4);
            EntityId world = reader.ReadIdentity();
            EntityId branch = reader.ReadIdentity();
            ulong revision = reader.ReadUnsigned64();
            if (!reader.ReadField(32).SequenceEqual(context.ContentHash.ToByteArray()))
                throw new ArgumentException("The supplied closing context does not match this production segment.");
            ulong totalBatches = reader.ReadUnsigned64();
            ulong totalConsumed = reader.ReadUnsigned64();
            ulong totalOperations = reader.ReadUnsigned64();
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
            var operations = new CanonicalProductionReader(reader.ReadField(MaximumSnapshotByteLength),
                MaximumSnapshotByteLength, MaterialOperationHistory.MaximumOperations + 1);
            operations.ExpectTag("production.material-operations"u8);
            var batchMap = batchValues.ToDictionary(value => value.BatchId);
            var claimMap = claims.ToDictionary(value => value.ConsumptionId);
            var ordered = new List<MaterialOperation>();
            while (operations.RemainingFields != 0)
                ordered.Add(MaterialOperationCodec.Decode(operations.ReadField(MaterialOperationCodec.MaximumByteLength), batchMap, claimMap, proofs));
            operations.Complete();
            reader.Complete();
            var result = new ProductionSnapshot(world, branch, revision, context.Recipes, batchValues, lotValues,
                context.SupportsConsumption ? claims : null, context, new MaterialOperationHistory(context, ordered));
            if (!context.SupportsConsumption && claims.Count != 0
                || result.TotalBatchCount != totalBatches || result.TotalConsumedItemCount != totalConsumed || result.TotalMaterialOperationCount != totalOperations)
                throw new ArgumentException("Continued production counters and consumption profile must match their exact context.");
            if (!bytes.SequenceEqual(Encode(result)))
                throw CanonicalProductionReader.Failure(ProductionFormatFailure.NonCanonical,
                    "Continued production fields must be canonically ordered.");
            return result;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw CanonicalProductionReader.Failure(ProductionFormatFailure.InvalidValue,
                "Continued production violates its closing context or lineage invariants: " + error.Message);
        }
        catch (FormatException error) when (error is not ProductionFormatException)
        {
            throw CanonicalProductionReader.Failure(ProductionFormatFailure.InvalidValue,
                "A typed material command violates its canonical format: " + error.Message);
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
