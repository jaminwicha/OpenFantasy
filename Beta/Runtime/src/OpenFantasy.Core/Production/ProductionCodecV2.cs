using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Production;

/// <summary>
/// Complete production admission with explicit consumed material claims. V1 recipe, batch, ingredient
/// and live-lot frames retain their meanings; the root version and consumed collection are explicit.
/// </summary>
public static class ProductionCodecV2
{
    public const int MaximumSnapshotByteLength = ProductionCodecV1.MaximumSnapshotByteLength;
    public const int MaximumConsumedItemByteLength = 640;
    private const int MaximumConsumedCollectionByteLength = 164 * 1024;
    private const int MaximumRecipeCollectionByteLength = 5 * 1024 * 1024;
    private const int MaximumBatchCollectionByteLength = 9 * 1024 * 1024;
    private const int MaximumLotCollectionByteLength = 3 * 1024 * 1024;

    public static byte[] Encode(ProductionSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.SupportsConsumption)
            throw new ArgumentException("Production V2 requires an explicitly consumption-enabled owner.", nameof(state));
        byte[] bytes = CanonicalEncodingV1.Frame("production.snapshot", Number(2), state.WorldId.ToByteArray(),
            state.BranchId.ToByteArray(), Number(state.Revision),
            Collection("production.recipes", state.Recipes.Select(ProductionCodecV1.EncodeRecipe),
                ProductionCodecV1.MaximumRecipeByteLength, MaximumRecipeCollectionByteLength),
            Collection("production.batches", state.Batches.Select(ProductionCodecV1.EncodeBatch),
                ProductionCodecV1.MaximumBatchByteLength, MaximumBatchCollectionByteLength),
            Collection("production.lots", state.Lots.Select(ProductionCodecV1.EncodeLot),
                ProductionCodecV1.MaximumLotByteLength, MaximumLotCollectionByteLength),
            Collection("production.consumed", state.ConsumedItems.Select(EncodeClaim),
                MaximumConsumedItemByteLength, MaximumConsumedCollectionByteLength));
        if (bytes.Length > MaximumSnapshotByteLength)
            throw CanonicalProductionReader.Failure(ProductionFormatFailure.LimitExceeded,
                "The production owner exceeds its encoded byte bound.");
        return bytes;
    }

    public static ProductionSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new CanonicalProductionReader(bytes, MaximumSnapshotByteLength, 9);
            reader.ExpectTag("production.snapshot"u8);
            reader.ExpectVersion(2);
            EntityId world = reader.ReadIdentity();
            EntityId branch = reader.ReadIdentity();
            ulong revision = reader.ReadUnsigned64();
            var recipes = new CanonicalProductionReader(reader.ReadField(MaximumRecipeCollectionByteLength),
                MaximumRecipeCollectionByteLength, ProductionSnapshot.MaximumRecipes + 1);
            recipes.ExpectTag("production.recipes"u8);
            List<CraftRecipeDefinition> recipeValues = [];
            while (recipes.RemainingFields != 0)
                recipeValues.Add(ProductionCodecV1.DecodeRecipe(recipes.ReadField(ProductionCodecV1.MaximumRecipeByteLength)));
            recipes.Complete();
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
                claims.Add(DecodeClaim(consumed.ReadField(MaximumConsumedItemByteLength)));
            consumed.Complete();
            reader.Complete();
            var result = new ProductionSnapshot(world, branch, revision, recipeValues, batchValues, lotValues, claims);
            if (!bytes.SequenceEqual(Encode(result)))
                throw CanonicalProductionReader.Failure(ProductionFormatFailure.NonCanonical,
                    "Production fields and consumed claims must be canonically ordered.");
            return result;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw CanonicalProductionReader.Failure(ProductionFormatFailure.InvalidValue,
                "Production state violates its consumption or lineage invariants: " + error.Message);
        }
    }

    private static byte[] EncodeClaim(ConsumedItemClaim claim) => CanonicalEncodingV1.Frame("production.consumed-item",
        Number(1), claim.ConsumptionId.ToByteArray(), claim.CommandId.ToByteArray(), claim.ActorId.ToByteArray(),
        claim.RecipientActorId.ToByteArray(), claim.CustodianId.ToByteArray(), ProductionCodecV1.EncodeRetired(claim.Ingredient));

    private static ConsumedItemClaim DecodeClaim(ReadOnlySpan<byte> bytes)
    {
        var reader = new CanonicalProductionReader(bytes, MaximumConsumedItemByteLength, 8);
        reader.ExpectTag("production.consumed-item"u8);
        reader.ExpectVersion(1);
        EntityId id = reader.ReadIdentity();
        EntityId command = reader.ReadIdentity();
        EntityId actor = reader.ReadIdentity();
        EntityId recipient = reader.ReadIdentity();
        EntityId custodian = reader.ReadIdentity();
        RetiredIngredient ingredient = ProductionCodecV1.DecodeRetired(reader.ReadField(384));
        reader.Complete();
        return new(id, command, actor, recipient, custodian, ingredient);
    }

    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
    private static byte[] Collection(string tag, IEnumerable<byte[]> values, int maximumItemLength, int maximumCollectionLength)
    {
        List<ReadOnlyMemory<byte>> fields = [];
        foreach (byte[] value in values)
        {
            if (value.Length > maximumItemLength)
                throw CanonicalProductionReader.Failure(ProductionFormatFailure.LimitExceeded,
                    "A production entry exceeds its encoded byte bound.");
            fields.Add(value);
        }
        byte[] bytes = CanonicalEncodingV1.Frame(tag, fields.ToArray());
        if (bytes.Length > maximumCollectionLength)
            throw CanonicalProductionReader.Failure(ProductionFormatFailure.LimitExceeded,
                "A production collection exceeds its encoded byte bound.");
        return bytes;
    }
}
