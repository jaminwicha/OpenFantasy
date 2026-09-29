using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Production;

/// <summary>
/// Ordered material bytes bound to the exact closing context and actual opaque gather and construction completion proofs. Decoding
/// validates structural rights, not a world journal, source issuance or the authenticity of a host.
/// The context is captured from actual closing owners and has no arbitrary-grant decoder.
/// </summary>
internal static class ProductionMaterialCodec
{
    internal const int MaximumSnapshotByteLength = ProductionCodecV1.MaximumSnapshotByteLength;
    private const int MaximumBatchCollectionByteLength = 9 * 1024 * 1024;
    private const int MaximumLotCollectionByteLength = 3 * 1024 * 1024;
    private const int MaximumConsumedCollectionByteLength = 164 * 1024;

    internal static byte[] Encode(ProductionSnapshot state, ulong version)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Continuation is not { SupportsMaterialOperations: true } context || state.MaterialHistory is null
            || version == 4 && (context.SupportsConstruction || context.SupportsTerrainRepresentation || context.SupportsReservedTransport || context.SupportsExcavation)
            || version == 5 && (!context.SupportsConstruction || context.SupportsTerrainRepresentation || context.SupportsReservedTransport || context.SupportsExcavation)
            || version == 6 && (!context.SupportsTerrainRepresentation || context.SupportsReservedTransport || context.SupportsExcavation)
            || version == 7 && (!context.SupportsReservedTransport || context.SupportsTerrainRepresentation || context.SupportsExcavation)
            || version == 8 && (!context.SupportsExcavation || context.SupportsTerrainRepresentation || context.SupportsReservedTransport)
            || context.SupportsConsentedTransfer != (version == ProductionCodecV10.Version)
            || version is not 4 and not 5 and not 6 and not 7 and not 8 and not ProductionCodecV10.Version)
            throw new ArgumentException("Production requires its exact explicitly continued capability.", nameof(state));
        return EncodeFields(context, version, state.Revision, state.Batches, state.Lots,
            state.ConsumedItems, state.MaterialHistory.Operations);
    }

    // Only the replay verifier uses this observer. The containing snapshot has already validated
    // every record, and replay has applied these earlier operations in order, checking rights,
    // identities and capacity. Hashing their current values must not recursively replay the prefix.
    internal static ContentHash HashReplayPrefix(ProductionContinuationContext context,
        IReadOnlyCollection<MaterialOperation> operations, IEnumerable<TransformedLot> liveLots)
    {
        if (!context.SupportsMaterialOperations || operations.Count > MaterialOperationHistory.MaximumOperations)
            throw new ArgumentException("A replay prefix requires bounded ordered material history.");
        var batches = ProductionCollections.Copy(operations.Select(operation => operation switch
        {
            MaterialCraftOperation craft => craft.Batch,
            MaterialConstructionOperation construction => construction.Evidence.Batch,
            _ => null
        }).OfType<ProductionBatch>(), ProductionSnapshot.MaximumBatches, value => value.BatchId);
        var claims = ProductionCollections.Copy(operations.OfType<MaterialConsumptionOperation>().Select(value => value.Claim),
            ProductionSnapshot.MaximumConsumedItems, value => value.ConsumptionId);
        var lots = ProductionCollections.Copy(liveLots, ProductionSnapshot.MaximumLots, value => value.LotId);
        ulong version = context.SupportsConsentedTransfer ? ProductionCodecV10.Version : context.SupportsExcavation ? 8UL : context.SupportsReservedTransport ? 7UL : context.SupportsTerrainRepresentation ? 6UL : context.SupportsConstruction ? 5UL : 4UL;
        return ContentHash.Compute(EncodeFields(context, version,
            checked(context.BaseProductionRevision + (ulong)operations.Count), batches, lots, claims, operations));
    }

    private static byte[] EncodeFields(ProductionContinuationContext context, ulong version, ulong revision,
        IReadOnlyCollection<ProductionBatch> batches, IReadOnlyCollection<TransformedLot> lots,
        IReadOnlyCollection<ConsumedItemClaim> claims, IReadOnlyCollection<MaterialOperation> operations)
    {
        byte[] bytes = CanonicalEncodingV1.Frame("production.snapshot", Number(version), context.Basis.WorldId.ToByteArray(),
            context.Basis.BranchId.ToByteArray(), Number(revision), context.ContentHash.ToByteArray(),
            Number(checked(context.BaseBatchCount + (ulong)batches.Count)),
            Number(checked(context.BaseConsumedItemCount + (ulong)claims.Count)),
            Number(checked(context.BaseMaterialOperationCount + (ulong)operations.Count)),
            Collection("production.batches", batches.Select(ProductionCodecV1.EncodeBatch),
                ProductionCodecV1.MaximumBatchByteLength, MaximumBatchCollectionByteLength),
            Collection("production.lots", lots.Select(ProductionCodecV1.EncodeLot),
                ProductionCodecV1.MaximumLotByteLength, MaximumLotCollectionByteLength),
            Collection("production.consumed", claims.Select(ProductionCodecV2.EncodeClaim),
                ProductionCodecV2.MaximumConsumedItemByteLength, MaximumConsumedCollectionByteLength),
            Collection("production.material-operations", operations.Select(MaterialOperationCodec.Encode),
                MaterialOperationCodec.MaximumByteLength, MaximumSnapshotByteLength));
        if (bytes.Length > MaximumSnapshotByteLength)
            throw CanonicalProductionReader.Failure(ProductionFormatFailure.LimitExceeded,
                "The continued production owner exceeds its encoded byte bound.");
        return bytes;
    }

    internal static ProductionSnapshot Decode(ReadOnlySpan<byte> bytes, ProductionContinuationContext context, ulong version,
        IReadOnlyCollection<OpenFantasy.Core.Gathering.GatherIssueEvidence> gatherEvidence,
        IReadOnlyCollection<OpenFantasy.Core.Construction.ConstructionCompletionEvidence> constructionEvidence,
        IReadOnlyCollection<OpenFantasy.Core.Geography.TerrainRepresentationEvidence> terrainEvidence,
        IReadOnlyCollection<OpenFantasy.Core.Geography.ReservoirTransportEvidence>? transportEvidence = null,
        IReadOnlyCollection<OpenFantasy.Core.Terrain.ExcavationWorkEvidence>? excavationEvidence = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(gatherEvidence);
        ArgumentNullException.ThrowIfNull(constructionEvidence);
        ArgumentNullException.ThrowIfNull(terrainEvidence);
        transportEvidence ??= [];
        excavationEvidence ??= [];
        try
        {
            if (!context.SupportsMaterialOperations || version == 4 && (context.SupportsConstruction || context.SupportsTerrainRepresentation || context.SupportsReservedTransport || context.SupportsExcavation)
                || version == 5 && (!context.SupportsConstruction || context.SupportsTerrainRepresentation || context.SupportsReservedTransport || context.SupportsExcavation)
                || version == 6 && (!context.SupportsTerrainRepresentation || context.SupportsReservedTransport || context.SupportsExcavation)
                || version == 7 && (!context.SupportsReservedTransport || context.SupportsTerrainRepresentation || context.SupportsExcavation)
                || version == 8 && (!context.SupportsExcavation || context.SupportsTerrainRepresentation || context.SupportsReservedTransport)
                || context.SupportsConsentedTransfer != (version == ProductionCodecV10.Version)
                || version is not 4 and not 5 and not 6 and not 7 and not 8 and not ProductionCodecV10.Version
                || gatherEvidence.Count > MaterialOperationHistory.MaximumOperations
                || constructionEvidence.Count > Construction.ConstructionSnapshot.MaximumProjects
                || terrainEvidence.Count > MaterialOperationHistory.MaximumOperations
                || transportEvidence.Count > MaterialOperationHistory.MaximumOperations
                || excavationEvidence.Count > MaterialOperationHistory.MaximumOperations)
                throw new ArgumentException("Material decoding requires its exact context and bounded actual evidence.");
            var proofs = Proofs(gatherEvidence, MaterialOperationHistory.MaximumOperations, value => value.ContentHash);
            var constructionProofs = Proofs(constructionEvidence, Construction.ConstructionSnapshot.MaximumProjects, value => value.ContentHash);
            var transportProofs = Proofs(transportEvidence, MaterialOperationHistory.MaximumOperations, value => value.ContentHash);
            var excavationProofs = Proofs(excavationEvidence, MaterialOperationHistory.MaximumOperations, value => value.ContentHash);
            var terrainProofs = Proofs(terrainEvidence, MaterialOperationHistory.MaximumOperations, value => value.ContentHash);
            var reader = new CanonicalProductionReader(bytes, MaximumSnapshotByteLength, 13);
            reader.ExpectTag("production.snapshot"u8);
            reader.ExpectVersion(version);
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
                ordered.Add(MaterialOperationCodec.Decode(operations.ReadField(MaterialOperationCodec.MaximumByteLength), batchMap, claimMap, proofs, constructionProofs, terrainProofs, transportProofs, excavationProofs));
            operations.Complete();
            reader.Complete();
            var result = new ProductionSnapshot(world, branch, revision, context.Recipes, batchValues, lotValues,
                context.SupportsConsumption ? claims : null, context, new MaterialOperationHistory(context, ordered));
            if (!context.SupportsConsumption && claims.Count != 0
                || result.TotalBatchCount != totalBatches || result.TotalConsumedItemCount != totalConsumed || result.TotalMaterialOperationCount != totalOperations)
                throw new ArgumentException("Continued production counters and consumption profile must match their exact context.");
            if (!bytes.SequenceEqual(Encode(result, version)))
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

    private static Dictionary<OpenFantasy.Core.Content.ContentHash, T> Proofs<T>(IReadOnlyCollection<T> evidence,
        int maximum, Func<T, OpenFantasy.Core.Content.ContentHash> hash) where T : class
    {
        T[] copy = evidence.Take(maximum + 1).ToArray();
        if (copy.Length > maximum || copy.Any(value => value is null))
            throw new ArgumentException("Opaque material evidence must be finite and non-null.");
        return copy.ToDictionary(hash);
    }

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
