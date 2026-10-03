using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Markets;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Bounded canonical frames of the world's paid-exchange owner. Quote and acceptance terms reuse the
/// spot-exchange contract frames. Decoding validates shape and local invariants only; it authenticates
/// neither participant, replays no payment and publishes nothing. Supplied outcomes must already follow
/// the owner's canonical order; a permutation is refused, never silently normalized.
/// </summary>
public static class WorldPaidExchangeCodec
{
    private const int MaximumListByteLength = WorldPaidExchangeBook.MaximumByteLength;

    public static byte[] Encode(WorldPaidExchangeBook book)
    {
        ArgumentNullException.ThrowIfNull(book);
        byte[] bytes = CanonicalEncodingV1.Frame("world.paid-exchange.v1",
            CanonicalEncodingV1.Frame("world.paid-exchange.open",
                book.Open.Select(value => (ReadOnlyMemory<byte>)Bounded(value.Encode(), WorldPaidQuote.MaximumByteLength)).ToArray()),
            CanonicalEncodingV1.Frame("world.paid-exchange.outcomes",
                book.Outcomes.Select(value => (ReadOnlyMemory<byte>)Bounded(value.Encode(),
                    WorldPaidQuoteOutcome.MaximumByteLength)).ToArray()),
            CanonicalEncodingV1.Frame("world.paid-exchange.issuers",
                book.IssuerRevisions.Select(value => (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("world.paid-exchange.issuer",
                    value.Key.ToByteArray(), CanonicalEncodingV1.Unsigned64(value.Value))).ToArray()));
        return Bounded(bytes, WorldPaidExchangeBook.MaximumByteLength);
    }

    public static WorldPaidExchangeBook Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            var reader = new WorldFrameReader(bytes, WorldPaidExchangeBook.MaximumByteLength, 4);
            reader.Tag("world.paid-exchange.v1"u8);
            ReadOnlySpan<byte> openBytes = reader.Field(MaximumListByteLength);
            ReadOnlySpan<byte> outcomeBytes = reader.Field(MaximumListByteLength);
            ReadOnlySpan<byte> issuerBytes = reader.Field(MaximumListByteLength);
            reader.Complete();

            var openReader = new WorldFrameReader(openBytes, MaximumListByteLength, WorldPaidExchangeBook.MaximumOpen + 1);
            openReader.Tag("world.paid-exchange.open"u8);
            List<WorldPaidQuote> open = [];
            while (openReader.Remaining != 0)
            {
                open.Add(DecodeQuote(openReader.Field(WorldPaidQuote.MaximumByteLength)));
            }
            openReader.Complete();

            var outcomeReader = new WorldFrameReader(outcomeBytes, MaximumListByteLength, WorldPaidExchangeBook.MaximumOutcomes + 1);
            outcomeReader.Tag("world.paid-exchange.outcomes"u8);
            List<WorldPaidQuoteOutcome> outcomes = [];
            while (outcomeReader.Remaining != 0)
            {
                outcomes.Add(DecodeOutcome(outcomeReader.Field(WorldPaidQuoteOutcome.MaximumByteLength)));
            }
            outcomeReader.Complete();

            var issuerReader = new WorldFrameReader(issuerBytes, MaximumListByteLength, WorldPaidExchangeBook.MaximumIssuers + 1);
            issuerReader.Tag("world.paid-exchange.issuers"u8);
            List<KeyValuePair<EntityId, ulong>> issuers = [];
            while (issuerReader.Remaining != 0)
            {
                var issuer = new WorldFrameReader(issuerReader.Field(128), 128, 3);
                issuer.Tag("world.paid-exchange.issuer"u8);
                issuers.Add(new(issuer.Identity(), issuer.Number()));
                issuer.Complete();
            }
            issuerReader.Complete();

            var book = new WorldPaidExchangeBook(open, outcomes, issuers);
            if (!Encode(book).AsSpan().SequenceEqual(bytes))
            {
                throw new FormatException("Paid quotes are not canonical.");
            }
            return book;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException
            or AuthorityFormatException)
        {
            throw new FormatException("Paid quotes violate their bounded owner contract.", error);
        }
    }

    private static WorldPaidQuote DecodeQuote(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, WorldPaidQuote.MaximumByteLength, 5);
        bool witnessed = Version(ref reader, "world.paid-quote.v1"u8, "world.paid-quote.v2"u8);
        SpotQuote quote = SpotExchangeCodecV1.DecodeQuote(reader.Field(SpotExchangeCodecV1.MaximumQuoteByteLength));
        EntityId creation = reader.Identity();
        ulong sequence = reader.Number();
        WorldControlWitness? witness = witnessed ? WorldControlWitnessCodecV1.Decode(reader.Field(WorldControlWitnessCodecV1.MaximumByteLength)) : null;
        reader.Complete();
        return new WorldPaidQuote(creation, quote, sequence, witness);
    }

    private static WorldPaidQuoteOutcome DecodeOutcome(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, WorldPaidQuoteOutcome.MaximumByteLength, 5);
        reader.Tag("world.paid-quote-outcome.v2"u8);
        WorldPaidQuote quote = DecodeQuote(reader.Field(WorldPaidQuote.MaximumByteLength));
        ulong kind = reader.Number();
        ulong sequence = reader.Number();
        ReadOnlySpan<byte> cause = reader.Field(WorldPaidSettlement.MaximumByteLength);
        reader.Complete();
        // Each kind reads exactly its own cause frame; an expiry has none.
        return kind switch
        {
            (ulong)WorldPaidQuoteOutcomeKind.Filled => new WorldPaidQuoteOutcome(quote, WorldPaidQuoteOutcomeKind.Filled,
                sequence, DecodeSettlement(cause)),
            (ulong)WorldPaidQuoteOutcomeKind.Expired when cause.IsEmpty => new WorldPaidQuoteOutcome(quote,
                WorldPaidQuoteOutcomeKind.Expired, sequence, null),
            (ulong)WorldPaidQuoteOutcomeKind.Superseded => new WorldPaidQuoteOutcome(quote, sequence, DecodeSupersession(cause)),
            (ulong)WorldPaidQuoteOutcomeKind.Withdrawn => new WorldPaidQuoteOutcome(quote, sequence, DecodeWithdrawal(cause)),
            _ => throw new FormatException("Unsupported quote outcome or terminal cause.")
        };
    }

    private static WorldPaidSupersession DecodeSupersession(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, WorldPaidSupersession.MaximumByteLength, 3);
        reader.Tag("world.paid-supersession.v1"u8);
        EntityId acceptance = reader.Identity();
        EntityId filledQuote = reader.Identity();
        reader.Complete();
        return new WorldPaidSupersession(acceptance, filledQuote);
    }

    private static WorldPaidWithdrawal DecodeWithdrawal(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, WorldPaidWithdrawal.MaximumAuthorizedByteLength, 6);
        bool witnessed = Version(ref reader, "world.paid-withdrawal.v1"u8, "world.paid-withdrawal.v2"u8);
        EntityId command = reader.Identity();
        EntityId principal = reader.Identity();
        EntityId session = reader.Identity();
        uint index = checked((uint)reader.Number());
        WorldControlWitness? witness = witnessed ? WorldControlWitnessCodecV1.Decode(reader.Field(WorldControlWitnessCodecV1.MaximumByteLength)) : null;
        reader.Complete();
        return new WorldPaidWithdrawal(command, new TrustedCommandDelivery(principal, session), index, witness);
    }

    private static WorldPaidSettlement DecodeSettlement(ReadOnlySpan<byte> bytes)
    {
        var reader = new WorldFrameReader(bytes, WorldPaidSettlement.MaximumByteLength, 9);
        bool witnessed = Version(ref reader, "world.paid-settlement.v1"u8, "world.paid-settlement.v2"u8);
        uint index = checked((uint)reader.Number());
        SpotAcceptance acceptance = SpotExchangeCodecV1.DecodeAcceptance(
            reader.Field(SpotExchangeCodecV1.MaximumAcceptanceByteLength));
        EntityId movedLot = reader.Identity();
        EntityId rightsOwner = reader.Identity();
        uint outputIndex = checked((uint)reader.Number());
        ulong start = reader.Number();
        ulong end = reader.Number();
        WorldControlWitness? witness = witnessed ? WorldControlWitnessCodecV1.Decode(reader.Field(WorldControlWitnessCodecV1.MaximumByteLength)) : null;
        reader.Complete();
        return new WorldPaidSettlement(index, acceptance, movedLot, rightsOwner, outputIndex, start, end, witness);
    }

    private static bool Version(ref WorldFrameReader reader, ReadOnlySpan<byte> oldTag, ReadOnlySpan<byte> witnessedTag)
    {
        ReadOnlySpan<byte> tag = reader.Field(witnessedTag.Length);
        if (tag.SequenceEqual(oldTag)) return false;
        if (tag.SequenceEqual(witnessedTag)) return true;
        throw new FormatException("Unsupported paid exchange provenance version.");
    }

    private static byte[] Bounded(byte[] bytes, int maximum) => bytes.Length <= maximum ? bytes
        : throw new ArgumentException("A paid-exchange frame exceeds its bound.");
}
