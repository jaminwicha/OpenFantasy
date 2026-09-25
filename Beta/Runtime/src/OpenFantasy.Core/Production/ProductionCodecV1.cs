using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Production;

/// <summary>Strict inverse encoding of finite production admission, retirement history and live derived lots.</summary>
public static class ProductionCodecV1
{
    public const int MaximumSnapshotByteLength = 16 * 1024 * 1024;
    public const int MaximumRecipeByteLength = 128 * 1024;
    internal const int MaximumBatchByteLength = 32 * 1024;
    internal const int MaximumLotByteLength = 512;

    public static byte[] Encode(ProductionSnapshot state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.SupportsConsumption)
            throw new ArgumentException("Production V1 cannot encode a consumption-enabled owner.", nameof(state));
        return Frame("production.snapshot", Number(1), Id(state.WorldId), Id(state.BranchId), Number(state.Revision),
            Collection("production.recipes", state.Recipes.Select(EncodeRecipe)),
            Collection("production.batches", state.Batches.Select(EncodeBatch)),
            Collection("production.lots", state.Lots.Select(EncodeLot)));
    }

    public static ProductionSnapshot Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new CanonicalProductionReader(bytes, MaximumSnapshotByteLength, 8);
            reader.ExpectTag("production.snapshot"u8);
            reader.ExpectVersion(1);
            EntityId world = reader.ReadIdentity();
            EntityId branch = reader.ReadIdentity();
            ulong revision = reader.ReadUnsigned64();
            var recipes = new CanonicalProductionReader(reader.ReadField(5 * 1024 * 1024),
                5 * 1024 * 1024, ProductionSnapshot.MaximumRecipes + 1);
            recipes.ExpectTag("production.recipes"u8);
            List<CraftRecipeDefinition> recipeValues = [];
            while (recipes.RemainingFields != 0)
            {
                recipeValues.Add(DecodeRecipe(recipes.ReadField(MaximumRecipeByteLength)));
            }
            recipes.Complete();
            var batches = new CanonicalProductionReader(reader.ReadField(9 * 1024 * 1024),
                9 * 1024 * 1024, ProductionSnapshot.MaximumBatches + 1);
            batches.ExpectTag("production.batches"u8);
            List<ProductionBatch> batchValues = [];
            while (batches.RemainingFields != 0)
            {
                batchValues.Add(DecodeBatch(batches.ReadField(MaximumBatchByteLength)));
            }
            batches.Complete();
            var lots = new CanonicalProductionReader(reader.ReadField(3 * 1024 * 1024),
                3 * 1024 * 1024, ProductionSnapshot.MaximumLots + 1);
            lots.ExpectTag("production.lots"u8);
            List<TransformedLot> lotValues = [];
            while (lots.RemainingFields != 0)
            {
                lotValues.Add(DecodeLot(lots.ReadField(MaximumLotByteLength)));
            }
            lots.Complete();
            reader.Complete();
            var result = new ProductionSnapshot(world, branch, revision, recipeValues, batchValues, lotValues);
            RequireCanonical(bytes, Encode(result));
            return result;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw CanonicalProductionReader.Failure(ProductionFormatFailure.InvalidValue,
                "Production state violates its finite lineage or definition invariants: " + error.Message);
        }
    }

    public static byte[] EncodeRecipe(CraftRecipeDefinition recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return Frame("craft.recipe", Number(1), Id(recipe.DefinitionId), MaterialRecipeCodecV1.Encode(recipe.PhysicalRecipe),
            Collection("craft.bindings", recipe.Bindings.Select(binding => Frame("craft.item-physical", Number(1),
                Id(binding.Item.DefinitionId), binding.Item.Version.ToByteArray(), Id(binding.Physical.DefinitionId),
                binding.Physical.Version.ToByteArray()))),
            Collection("craft.allocations", recipe.Allocations.Select(value => Frame("craft.output-allocation", Number(1),
                Id(value.InputItem.DefinitionId), value.InputItem.Version.ToByteArray(), Number(value.OutputIndex),
                Number(value.MassPerOutputUnit.Value)))));
    }

    public static CraftRecipeDefinition DecodeRecipe(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new CanonicalProductionReader(bytes, MaximumRecipeByteLength, 6);
            reader.ExpectTag("craft.recipe"u8);
            reader.ExpectVersion(1);
            EntityId id = reader.ReadIdentity();
            MaterialRecipeDefinition physical = MaterialRecipeCodecV1.Decode(reader.ReadField(MaterialRecipeCodecV1.MaximumRecipeByteLength));
            var bindings = new CanonicalProductionReader(reader.ReadField(16 * 1024), 16 * 1024,
                MaterialCatalog.MaximumDefinitionCount + 1);
            bindings.ExpectTag("craft.bindings"u8);
            List<ItemPhysicalBinding> bindingValues = [];
            while (bindings.RemainingFields != 0)
            {
                var binding = new CanonicalProductionReader(bindings.ReadField(256), 256, 6);
                binding.ExpectTag("craft.item-physical"u8);
                binding.ExpectVersion(1);
                CountedItemDefinition item = Pin(ref binding);
                CountedItemDefinition material = Pin(ref binding);
                binding.Complete();
                MaterialDefinition exact = physical.Materials.FirstOrDefault(value => value.DefinitionId == material.DefinitionId
                    && value.Version == material.Version) ?? throw new ArgumentException("Unknown item physical calibration.");
                bindingValues.Add(new ItemPhysicalBinding(item, exact));
            }
            bindings.Complete();
            var allocations = new CanonicalProductionReader(reader.ReadField(48 * 1024), 48 * 1024,
                CraftRecipeDefinition.MaximumAllocations + 1);
            allocations.ExpectTag("craft.allocations"u8);
            List<OutputMaterialAllocation> allocationValues = [];
            while (allocations.RemainingFields != 0)
            {
                var allocation = new CanonicalProductionReader(allocations.ReadField(192), 192, 6);
                allocation.ExpectTag("craft.output-allocation"u8);
                allocation.ExpectVersion(1);
                CountedItemDefinition item = Pin(ref allocation);
                uint index = Index(allocation.ReadUnsigned64());
                MassMilligrams mass = new(allocation.ReadUnsigned64());
                allocation.Complete();
                allocationValues.Add(new OutputMaterialAllocation(item, index, mass));
            }
            allocations.Complete();
            reader.Complete();
            var result = new CraftRecipeDefinition(id, physical, bindingValues, allocationValues);
            RequireCanonical(bytes, EncodeRecipe(result));
            return result;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            throw CanonicalProductionReader.Failure(ProductionFormatFailure.InvalidValue,
                "Crafting definition violates its physical mapping: " + error.Message);
        }
    }

    internal static byte[] EncodeBatch(ProductionBatch batch) => Frame("production.batch", Number(1), Id(batch.BatchId),
        Id(batch.CommandId), Id(batch.ActorId), Id(batch.CustodianId), Id(batch.RecipeId), batch.RecipeVersion.ToByteArray(),
        Collection("production.retired", batch.Ingredients.Select(EncodeRetired)));

    internal static ProductionBatch DecodeBatch(ReadOnlySpan<byte> bytes)
    {
        var reader = new CanonicalProductionReader(bytes, MaximumBatchByteLength, 9);
        reader.ExpectTag("production.batch"u8);
        reader.ExpectVersion(1);
        EntityId id = reader.ReadIdentity();
        EntityId command = reader.ReadIdentity();
        EntityId actor = reader.ReadIdentity();
        EntityId custodian = reader.ReadIdentity();
        EntityId recipe = reader.ReadIdentity();
        ContentHash version = Hash(ref reader);
        var ingredients = new CanonicalProductionReader(reader.ReadField(24 * 1024), 24 * 1024,
            MaterialRecipeDefinition.MaximumTermCount + 1);
        ingredients.ExpectTag("production.retired"u8);
        List<RetiredIngredient> values = [];
        while (ingredients.RemainingFields != 0)
        {
            values.Add(DecodeRetired(ingredients.ReadField(384)));
        }
        ingredients.Complete();
        reader.Complete();
        return new ProductionBatch(id, command, actor, custodian, recipe, version, values);
    }

    internal static byte[] EncodeRetired(RetiredIngredient value) => Frame("production.ingredient", Number(1),
        Number((ulong)value.Kind), Id(value.LotId), Number(value.Revision), Id(value.Definition.DefinitionId),
        value.Definition.Version.ToByteArray(), value.Source is { } source ? EncodeSource(source) : EncodeOutput(value.Output!));

    internal static RetiredIngredient DecodeRetired(ReadOnlySpan<byte> bytes)
    {
        var reader = new CanonicalProductionReader(bytes, 384, 8);
        reader.ExpectTag("production.ingredient"u8);
        reader.ExpectVersion(1);
        ulong kind = reader.ReadUnsigned64();
        EntityId lot = reader.ReadIdentity();
        ulong revision = reader.ReadUnsigned64();
        CountedItemDefinition item = Pin(ref reader);
        ReadOnlySpan<byte> claim = reader.ReadField(160);
        reader.Complete();
        return kind switch
        {
            1 => new RetiredIngredient(lot, revision, item, DecodeSource(claim)),
            2 => new RetiredIngredient(lot, revision, item, DecodeOutput(claim)),
            _ => throw CanonicalProductionReader.Failure(ProductionFormatFailure.UnknownType, "Unknown ingredient provenance family.")
        };
    }

    internal static byte[] EncodeLot(TransformedLot lot) => Frame("production.lot", Number(1), Id(lot.LotId),
        Id(lot.CustodianId), Id(lot.Definition.DefinitionId), lot.Definition.Version.ToByteArray(), EncodeOutput(lot.Output),
        Number(lot.Revision), Number((ulong)lot.Protection), lot.ReservedBy is { } reserved ? Id(reserved) : []);

    internal static TransformedLot DecodeLot(ReadOnlySpan<byte> bytes)
    {
        var reader = new CanonicalProductionReader(bytes, MaximumLotByteLength, 10);
        reader.ExpectTag("production.lot"u8);
        reader.ExpectVersion(1);
        EntityId id = reader.ReadIdentity();
        EntityId custodian = reader.ReadIdentity();
        CountedItemDefinition item = Pin(ref reader);
        ProductionOutputInterval output = DecodeOutput(reader.ReadField(160));
        ulong revision = reader.ReadUnsigned64();
        ulong protection = reader.ReadUnsigned64();
        ReadOnlySpan<byte> reserved = reader.ReadField(16);
        reader.Complete();
        if (protection > 15 || reserved.Length is not 0 and not 16)
        {
            throw CanonicalProductionReader.Failure(ProductionFormatFailure.InvalidValue, "Invalid lot protection or reservation width.");
        }
        return new TransformedLot(id, custodian, item, output, revision, (LotProtection)protection,
            reserved.IsEmpty ? null : EntityId.FromBytes(reserved));
    }

    private static byte[] EncodeSource(SourceInterval source) => Frame("production.source-claim", Number(1),
        Id(source.SourceId), Number(source.Start), Number(source.End));

    private static SourceInterval DecodeSource(ReadOnlySpan<byte> bytes)
    {
        var reader = new CanonicalProductionReader(bytes, 160, 5);
        reader.ExpectTag("production.source-claim"u8);
        reader.ExpectVersion(1);
        var result = new SourceInterval(reader.ReadIdentity(), reader.ReadUnsigned64(), reader.ReadUnsigned64());
        reader.Complete();
        return result;
    }

    private static byte[] EncodeOutput(ProductionOutputInterval output) => Frame("production.output-claim", Number(1),
        Id(output.BatchId), Number(output.OutputIndex), Number(output.Start), Number(output.End));

    private static ProductionOutputInterval DecodeOutput(ReadOnlySpan<byte> bytes)
    {
        var reader = new CanonicalProductionReader(bytes, 160, 6);
        reader.ExpectTag("production.output-claim"u8);
        reader.ExpectVersion(1);
        var result = new ProductionOutputInterval(reader.ReadIdentity(), Index(reader.ReadUnsigned64()),
            reader.ReadUnsigned64(), reader.ReadUnsigned64());
        reader.Complete();
        return result;
    }

    private static uint Index(ulong value) => value < MaterialRecipeDefinition.MaximumTermCount ? (uint)value
        : throw CanonicalProductionReader.Failure(ProductionFormatFailure.InvalidValue, "Output index is outside the profile.");

    private static CountedItemDefinition Pin(ref CanonicalProductionReader reader) => new(reader.ReadIdentity(), Hash(ref reader));

    private static ContentHash Hash(ref CanonicalProductionReader reader)
    {
        ReadOnlySpan<byte> value = reader.ReadField(32);
        if (value.Length != 32)
        {
            throw CanonicalProductionReader.Failure(ProductionFormatFailure.InvalidValue, "A definition pin requires exactly 32 bytes.");
        }
        return new ContentHash(Convert.ToHexString(value).ToLowerInvariant());
    }

    private static void RequireCanonical(ReadOnlySpan<byte> original, byte[] encoded)
    {
        if (!original.SequenceEqual(encoded))
        {
            throw CanonicalProductionReader.Failure(ProductionFormatFailure.NonCanonical, "Production fields are not canonically ordered.");
        }
    }

    private static byte[] Collection(string tag, IEnumerable<byte[]> values) => Frame(tag,
        values.Select(value => (ReadOnlyMemory<byte>)value).ToArray());
    private static byte[] Id(EntityId id) => id.ToByteArray();
    private static byte[] Number(ulong value) => CanonicalEncodingV1.Unsigned64(value);
    private static byte[] Frame(string tag, params ReadOnlyMemory<byte>[] fields) => CanonicalEncodingV1.Frame(tag, fields);
}
