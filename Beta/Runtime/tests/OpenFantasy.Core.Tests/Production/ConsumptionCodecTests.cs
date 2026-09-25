using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using static OpenFantasy.Core.Tests.Production.ConsumptionFixture;
using static OpenFantasy.Core.Tests.Production.InstantCraftFixture;

namespace OpenFantasy.Core.Tests.Production;

public sealed class ConsumptionCodecTests
{
    [Fact]
    public void Legacy_bytes_remain_exact_and_explicit_empty_consumption_has_a_distinct_canonical_format()
    {
        var initial = Create();
        using JsonDocument vectors = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "production-ledger-v1.json")));
        byte[] frozen = Convert.FromHexString(vectors.RootElement.GetProperty("empty").GetProperty("hex").GetString()!);
        Assert.Equal(frozen, ProductionCodecV1.Encode(initial.Production));
        Assert.Equal(frozen, ProductionCodecV1.Encode(ProductionCodecV1.Decode(frozen)));
        Assert.False(initial.Production.SupportsConsumption);
        Assert.Empty(initial.Production.ConsumedItems);
        var enabled = new ProductionSnapshot(Id(1), Id(2), 0, initial.Production.Recipes, [], [], []);
        byte[] bytes = ProductionCodecV2.Encode(enabled);
        byte[][] fields = Fields(frozen);
        fields[1] = Number(2);
        Assert.Equal(Frame([.. fields, WriteFrame("production.consumed")]), bytes);
        Assert.NotEqual(initial.Production.ContentHash, enabled.ContentHash);
        Assert.True(ProductionCodecV2.Decode(bytes).SupportsConsumption);
        Assert.Throws<ArgumentException>(() => ProductionCodecV1.Encode(enabled));
        Assert.Throws<ArgumentException>(() => ProductionCodecV2.Encode(initial.Production));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV1.Decode(bytes));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV2.Decode(frozen));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Complete_raw_and_produced_consumption_roundtrip_with_exact_participants_and_ingredient_fields(bool produced)
    {
        var state = produced ? ConsumedProduct() : ConsumedRaw();
        byte[] encoded = ProductionCodecV2.Encode(state.Production);
        ProductionSnapshot decoded = ProductionCodecV2.Decode(encoded);
        decoded.ValidateForAuthority(state.Inventory);
        Assert.Equal(encoded, ProductionCodecV2.Encode(decoded));
        Assert.Equal(state.Production.ContentHash, decoded.ContentHash);
        var expected = Assert.Single(state.Production.ConsumedItems);
        var actual = Assert.Single(decoded.ConsumedItems);
        Assert.Equal((expected.ConsumptionId, expected.CommandId, expected.ActorId, expected.RecipientActorId, expected.CustodianId),
            (actual.ConsumptionId, actual.CommandId, actual.ActorId, actual.RecipientActorId, actual.CustodianId));
        Assert.Equal((expected.Ingredient.Kind, expected.Ingredient.LotId, expected.Ingredient.Revision, expected.Ingredient.Definition),
            (actual.Ingredient.Kind, actual.Ingredient.LotId, actual.Ingredient.Revision, actual.Ingredient.Definition));
        Assert.Equal(expected.Ingredient.Source, actual.Ingredient.Source);
        Assert.Equal(expected.Ingredient.Output, actual.Ingredient.Output);
        byte[][] root = Fields(encoded);
        Assert.Equal(9, root.Length);
        byte[][] collection = Fields(root[8]);
        Assert.Equal("production.consumed", Encoding.UTF8.GetString(collection[0]));
        byte[] expectedClaim = WriteFrame("production.consumed-item", Number(1),
            expected.ConsumptionId.ToByteArray(), expected.CommandId.ToByteArray(), expected.ActorId.ToByteArray(),
            expected.RecipientActorId.ToByteArray(), expected.CustodianId.ToByteArray(), Ingredient(expected.Ingredient));
        Assert.Equal(expectedClaim, collection[1]);
    }

    [Fact]
    public void Canonical_claim_order_is_stable_and_decoder_rejects_reversed_or_duplicated_rows()
    {
        var raw = ConsumedRaw();
        ConsumedItemClaim first = Assert.Single(raw.Production.ConsumedItems);
        var second = Change(first, command: Id(9003),
            ingredient: new RetiredIngredient(Id(72), 0, Wood, new SourceInterval(Id(73), 0, 1)));
        var forward = new ProductionSnapshot(Id(1), Id(2), 2, raw.Production.Recipes, [], [], [first, second]);
        var reverse = new ProductionSnapshot(Id(1), Id(2), 2, raw.Production.Recipes.Reverse(), [], [], [second, first]);
        byte[] bytes = ProductionCodecV2.Encode(forward);
        Assert.Equal(bytes, ProductionCodecV2.Encode(reverse));
        byte[][] root = Fields(bytes);
        byte[][] rows = Fields(root[8]);
        root[8] = Frame([rows[0], rows[2], rows[1]]);
        var wrongOrder = Assert.Throws<ProductionFormatException>(() => ProductionCodecV2.Decode(Frame(root)));
        Assert.Equal(ProductionFormatFailure.NonCanonical, wrongOrder.Failure);
        root[8] = Frame([rows[0], rows[1], rows[1]]);
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV2.Decode(Frame(root)));
    }

    [Fact]
    public void Decoder_rejects_a_fresh_arbitrary_consumption_identity_substituted_into_an_otherwise_valid_archive()
    {
        ProductionSnapshot state = ConsumedProduct().Production;
        byte[] original = ProductionCodecV2.Encode(state);
        Assert.Equal(original, ProductionCodecV2.Encode(ProductionCodecV2.Decode(original)));
        EntityId unused = Id(999_999);
        Assert.False(state.ContainsIdentity(unused));
        byte[][] root = Fields(original);
        byte[][] collection = Fields(root[8]);
        byte[][] claim = Fields(collection[1]);
        claim[2] = unused.ToByteArray();
        root[8] = Frame([collection[0], Frame(claim)]);
        ProductionFormatException error = Assert.Throws<ProductionFormatException>(() => ProductionCodecV2.Decode(Frame(root)));
        Assert.Equal(ProductionFormatFailure.InvalidValue, error.Failure);
        Assert.Contains("deterministic consumption identity", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_claim_invalid_roles_forged_produced_origin_and_revision_do_not_decode()
    {
        byte[][] original = Fields(ProductionCodecV2.Encode(ConsumedProduct().Production));
        byte[][] collection = Fields(original[8]);
        byte[][] claim = Fields(collection[1]);
        byte[][] ingredient = Fields(claim[7]);
        void RejectWith(byte[] encodedClaim)
        {
            byte[][] root = original.ToArray();
            root[8] = Frame([collection[0], encodedClaim]);
            Assert.Throws<ProductionFormatException>(() => ProductionCodecV2.Decode(Frame(root)));
        }
        byte[][] missing = original.ToArray();
        missing[8] = WriteFrame("production.consumed");
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV2.Decode(Frame(missing)));
        foreach (int identityField in new[] { 2, 3 })
        {
            byte[][] alias = claim.ToArray();
            alias[identityField] = original[2]; // A consumption identity or command cannot be the world.
            RejectWith(Frame(alias));
        }
        byte[][] invalidRevision = ingredient.ToArray();
        invalidRevision[4] = Number(1); // The surviving lot has revision1 too.
        byte[][] changedClaim = claim.ToArray();
        changedClaim[7] = Frame(invalidRevision);
        RejectWith(Frame(changedClaim));
        byte[][] output = Fields(ingredient[7]);
        output[2] = Id(9999).ToByteArray();
        byte[][] unknownOrigin = ingredient.ToArray();
        unknownOrigin[7] = Frame(output);
        changedClaim[7] = Frame(unknownOrigin);
        RejectWith(Frame(changedClaim));
    }

    [Fact]
    public void Malformed_archive_lengths_versions_counts_and_trailing_data_fail_closed()
    {
        byte[] bytes = ProductionCodecV2.Encode(ConsumedRaw().Production);
        for (int length = 0; length < bytes.Length; length++)
        {
            byte[] truncated = bytes[..length];
            Assert.Throws<ProductionFormatException>(() => ProductionCodecV2.Decode(truncated));
        }
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV2.Decode([.. bytes, 0]));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV2.Decode(new byte[ProductionCodecV2.MaximumSnapshotByteLength + 1]));
        byte[][] fields = Fields(bytes);
        fields[1] = Number(3);
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV2.Decode(Frame(fields)));
        fields = Fields(bytes);
        byte[][] collection = Fields(fields[8]);
        byte[] excessive = collection[1];
        fields[8] = Frame([collection[0], .. Enumerable.Repeat(excessive, ProductionSnapshot.MaximumConsumedItems + 1)]);
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV2.Decode(Frame(fields)));
        fields[8] = Frame([collection[0], new byte[ProductionCodecV2.MaximumConsumedItemByteLength + 1]]);
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV2.Decode(Frame(fields)));
        fields = Fields(bytes);
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV2.Decode(Frame([.. fields, []])));
    }

    private static byte[] Ingredient(RetiredIngredient value)
    {
        byte[] provenance = value.Source is { } source
            ? WriteFrame("production.source-claim", Number(1),
                source.SourceId.ToByteArray(), Number(source.Start), Number(source.End))
            : WriteFrame("production.output-claim", Number(1),
                value.Output!.BatchId.ToByteArray(), Number(value.Output.OutputIndex),
                Number(value.Output.Start), Number(value.Output.End));
        return WriteFrame("production.ingredient", Number(1),
            Number((ulong)value.Kind), value.LotId.ToByteArray(), Number(value.Revision),
            value.Definition.DefinitionId.ToByteArray(), value.Definition.Version.ToByteArray(), provenance);
    }

    private static byte[][] Fields(byte[] bytes)
    {
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8)));
        byte[][] fields = new byte[count][];
        int offset = 12;
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
            offset += 4;
            fields[index] = bytes.AsSpan(offset, length).ToArray();
            offset += length;
        }
        Assert.Equal(bytes.Length, offset);
        return fields;
    }

    private static byte[] Frame(byte[][] fields) => WriteFrame(Encoding.UTF8.GetString(fields[0]),
        fields.Skip(1).Select(value => (ReadOnlyMemory<byte>)value).ToArray());

    private static byte[] Number(ulong value)
    {
        byte[] bytes = new byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return bytes;
    }

    // Independent OFC writer: ASCII magic, big-endian version/count and length-prefixed fields.
    private static byte[] WriteFrame(string tag, params ReadOnlyMemory<byte>[] fields)
    {
        using var stream = new MemoryStream();
        stream.Write("OFC\0"u8);
        Span<byte> number = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(number, 1);
        stream.Write(number);
        BinaryPrimitives.WriteUInt32BigEndian(number, checked((uint)fields.Length + 1));
        stream.Write(number);
        byte[] tagBytes = Encoding.UTF8.GetBytes(tag);
        BinaryPrimitives.WriteUInt32BigEndian(number, checked((uint)tagBytes.Length));
        stream.Write(number);
        stream.Write(tagBytes);
        foreach (ReadOnlyMemory<byte> field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(number, checked((uint)field.Length));
            stream.Write(number);
            stream.Write(field.Span);
        }
        return stream.ToArray();
    }
}
