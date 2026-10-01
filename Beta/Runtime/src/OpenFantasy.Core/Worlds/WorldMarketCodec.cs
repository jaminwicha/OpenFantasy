using System.Text;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Observations;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Canonical frames of the market owner: bindings with their complete rule, quote records, dated
/// knowledge and lot disclosures, each list in its strict owner order. Decoding re-encodes and refuses any
/// other byte form.
/// </summary>
public static class WorldMarketCodec
{
    private const int MaximumBindingByteLength = 1_024;
    private const int MaximumListFields = WorldMarketBook.MaximumObservations + 2;

    public static byte[] Encode(WorldMarketBook book)
    {
        ArgumentNullException.ThrowIfNull(book);
        return CanonicalEncodingV1.Frame("world.market-book.v1", CanonicalEncodingV1.Unsigned64(1),
            List(book.Markets.Select(value => value.Encode())), List(book.Quotes.Select(value => value.Encode())),
            List(book.Knowledge.Select(value => value.Encode())), List(book.Disclosures.Select(value => value.Encode())));
    }

    public static WorldMarketBook Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, WorldMarketBook.MaximumByteLength, 6);
            reader.Tag("world.market-book.v1"u8);
            reader.Version();
            var markets = new List<MarketBinding>();
            var marketList = new WorldFrameReader(reader.Field(WorldMarketBook.MaximumByteLength), WorldMarketBook.MaximumByteLength, MaximumListFields);
            marketList.Tag("world.market-list.v1"u8);
            while (marketList.Remaining != 0)
            {
                markets.Add(DecodeBinding(marketList.Field(MaximumBindingByteLength)));
            }
            marketList.Complete();
            var records = new List<MarketQuoteRecord>();
            var recordList = new WorldFrameReader(reader.Field(WorldMarketBook.MaximumByteLength), WorldMarketBook.MaximumByteLength, MaximumListFields);
            recordList.Tag("world.market-list.v1"u8);
            while (recordList.Remaining != 0)
            {
                records.Add(DecodeRecord(recordList.Field(MarketQuoteRecord.MaximumByteLength)));
            }
            recordList.Complete();
            var knowledge = new List<MarketPriceObservation>();
            var knowledgeList = new WorldFrameReader(reader.Field(WorldMarketBook.MaximumByteLength), WorldMarketBook.MaximumByteLength, MaximumListFields);
            knowledgeList.Tag("world.market-list.v1"u8);
            while (knowledgeList.Remaining != 0)
            {
                knowledge.Add(DecodeObservation(knowledgeList.Field(MarketPriceObservation.MaximumByteLength)));
            }
            knowledgeList.Complete();
            var disclosures = new List<MarketLotDisclosure>();
            var disclosureList = new WorldFrameReader(reader.Field(WorldMarketBook.MaximumByteLength), WorldMarketBook.MaximumByteLength, MaximumListFields);
            disclosureList.Tag("world.market-list.v1"u8);
            while (disclosureList.Remaining != 0)
            {
                disclosures.Add(DecodeDisclosure(disclosureList.Field(MarketLotDisclosure.MaximumByteLength)));
            }
            disclosureList.Complete();
            reader.Complete();
            var book = new WorldMarketBook(markets, records, knowledge, disclosures);
            if (!Encode(book).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("The market owner is not canonical.");
            }
            return book;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException or InvalidOperationException)
        {
            throw new FormatException("The market owner violates its invariants.", error);
        }
    }

    private static byte[] List(IEnumerable<byte[]> items) =>
        CanonicalEncodingV1.Frame("world.market-list.v1", items.Select(value => (ReadOnlyMemory<byte>)value).ToArray());

    private static MarketBinding DecodeBinding(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumBindingByteLength, 9);
        reader.Tag("market.binding.v1"u8);
        EntityId market = reader.Identity(), merchant = reader.Identity(), custodian = reader.Identity(), account = reader.Identity();
        var commodity = new CountedItemDefinition(reader.Identity(), Hash(ref reader));
        EntityId place = reader.Identity();
        MarketPricePolicy policy = DecodePolicy(reader.Field(MaximumBindingByteLength));
        reader.Complete();
        return new MarketBinding(market, merchant, custodian, account, commodity, place, policy);
    }

    private static MarketPricePolicy DecodePolicy(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MaximumBindingByteLength, 13);
        reader.Tag("market.price-policy.v1"u8);
        var sourceReader = new WorldFrameReader(reader.Field(256), 256, 4);
        sourceReader.Tag("market.source-price.v1"u8);
        string key = Encoding.ASCII.GetString(sourceReader.Field(MarketSourcePrice.MaximumKeyLength));
        ulong sourcePrice = sourceReader.Number();
        ContentHash digest = Hash(ref sourceReader);
        sourceReader.Complete();
        ulong target = reader.Number(), holding = reader.Number(), softening = reader.Number();
        ulong minimum = reader.Number(), maximum = reader.Number(), markup = reader.Number(), markdown = reader.Number();
        ulong fee = reader.Number();
        ReadOnlySpan<byte> payee = reader.Field(16);
        EntityId? payeeId = payee.IsEmpty ? null : payee.Length == 16 ? EntityId.FromBytes(payee)
            : throw new FormatException("A fee payee is an identity or empty.");
        ulong lot = reader.Number(), lifetime = reader.Number();
        reader.Complete();
        return new MarketPricePolicy(new MarketSourcePrice(key, sourcePrice, digest), target, holding, softening, minimum,
            maximum, markup, markdown, fee, payeeId, lot, lifetime);
    }

    private static MarketQuoteRecord DecodeRecord(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MarketQuoteRecord.MaximumByteLength, 5);
        reader.Tag("world.market-quote-record.v1"u8);
        EntityId quote = reader.Identity(), market = reader.Identity();
        ContentHash policy = Hash(ref reader);
        ulong basis = reader.Number();
        reader.Complete();
        return new MarketQuoteRecord(quote, market, policy, basis);
    }

    private static MarketPriceObservation DecodeObservation(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MarketPriceObservation.MaximumByteLength, 14);
        reader.Tag("market.price-observation.v1"u8);
        EntityId observer = reader.Identity(), market = reader.Identity(), merchant = reader.Identity();
        var commodity = new CountedItemDefinition(reader.Identity(), Hash(ref reader));
        ContentHash policy = Hash(ref reader);
        ulong? ask = Optional(ref reader), bid = Optional(ref reader);
        ulong fee = reader.Number(), sequence = reader.Number(), step = reader.Number();
        EntityId cause = reader.Identity();
        ulong index = reader.Number();
        reader.Complete();
        return new MarketPriceObservation(observer, market, merchant, commodity, policy, ask, bid, fee, sequence, step,
            cause, checked((uint)index));
    }

    private static MarketLotDisclosure DecodeDisclosure(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, MarketLotDisclosure.MaximumByteLength, 17);
        reader.Tag("market.lot-disclosure.v1"u8);
        EntityId seller = reader.Identity(), market = reader.Identity(), merchant = reader.Identity();
        ulong kind = reader.Number();
        EntityId lot = reader.Identity();
        var definition = new CountedItemDefinition(reader.Identity(), Hash(ref reader));
        ulong revision = reader.Number(), quantity = reader.Number();
        EntityId custodian = reader.Identity(), account = reader.Identity();
        ulong sequence = reader.Number(), step = reader.Number(), expires = reader.Number();
        EntityId cause = reader.Identity();
        ulong index = reader.Number();
        reader.Complete();
        return new MarketLotDisclosure(seller, market, merchant, checked((SpotLotKind)(int)kind), lot, definition, revision,
            quantity, custodian, account, sequence, step, expires, cause, checked((uint)index));
    }

    private static ulong? Optional(ref WorldFrameReader reader)
    {
        ReadOnlySpan<byte> value = reader.Field(8);
        return value.IsEmpty ? null : value.Length == 8 ? System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(value)
            : throw new FormatException("An optional price is eight bytes or empty.");
    }

    private static ContentHash Hash(ref WorldFrameReader reader) =>
        new(Convert.ToHexString(reader.Exact(32)).ToLowerInvariant());
}
