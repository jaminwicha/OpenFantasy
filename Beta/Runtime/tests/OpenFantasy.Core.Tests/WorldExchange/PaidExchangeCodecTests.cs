using System.Buffers.Binary;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Tests.Markets;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.WorldExchange;

public sealed class PaidExchangeCodecTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Only_the_named_profile_selects_paid_actions_and_each_command_retains_exact_bytes(bool accept)
    {
        WorldPaidQuote quote = Quote(500);
        var terms = quote.Quote;
        var approval = new SpotAcceptance(Id(700), Id(20), Id(30), quote.QuoteId, terms.ContentHash, 1, 4, 1);
        WorldActionIntent intent = accept ? new AcceptSpotQuoteIntent(approval, 8) : new IssueSpotQuoteIntent(terms);
        var command = new WorldActionCommand(accept ? approval.AcceptanceId : quote.CreationCommandId,
            accept ? approval.PrincipalId : terms.IssuerPrincipalId,
            accept ? approval.OriginSessionId : terms.IssuerSessionId,
            accept ? terms.CounterpartyActorId : terms.IssuerActorId, terms.WorldId, terms.BranchId, 3, intent);
        byte[] encoded = WorldActionCodec.Encode(command);
        Assert.Equal(encoded, WorldActionCodec.Encode(WorldActionCodec.Decode(encoded)));
        Assert.Equal(encoded, WorldActionCodecV17.Encode(command));
        Assert.Throws<FormatException>(() => WorldActionCodecV16.Decode(encoded));
        foreach (ushort old in new ushort[] { 26, 27, 28, 29, 30, 31 })
        {
            Assert.Throws<ArgumentException>(() => new WorldStepBatch(Id(900), terms.WorldId, terms.BranchId,
                11, 10, WorldBatchKind.AdvancingStep, [], [command], old));
        }
        var batch = new WorldStepBatch(Id(900), terms.WorldId, terms.BranchId, 11, 10,
            WorldBatchKind.AdvancingStep, [], [command], 32);
        byte[] frame = WorldStepCodec.Encode(batch);
        Assert.Equal(frame, WorldStepCodecV32.Encode(WorldStepCodec.Decode(frame)));
        Assert.Throws<FormatException>(() => WorldStepCodecV30.Decode(frame));
        Assert.Throws<ArgumentException>(() => new WorldStepBatch(Id(900), terms.WorldId, terms.BranchId,
            11, 10, WorldBatchKind.HeldInteraction, [], [command], 32));
    }

    [Fact]
    public void Quote_book_has_an_independent_byte_vector_and_copies_its_inputs_and_outputs()
    {
        WorldPaidQuote quote = Quote(500);
        var input = new List<WorldPaidQuote> { quote };
        var book = new WorldPaidExchangeBook(input, [], []);
        input.Clear();
        byte[] expected = Frame("world.paid-exchange.v1"u8.ToArray(),
            Frame("world.paid-exchange.open"u8.ToArray(), quote.Encode()),
            Frame("world.paid-exchange.outcomes"u8.ToArray()), Frame("world.paid-exchange.issuers"u8.ToArray()));
        Assert.Equal(expected, WorldPaidExchangeCodec.Encode(book));
        var restored = WorldPaidExchangeCodec.Decode(expected);
        Assert.Equal(book.ContentHash, restored.ContentHash);
        expected[^1] ^= 1;
        Assert.Equal(book.ContentHash, restored.ContentHash);
        byte[] emitted = WorldPaidExchangeCodec.Encode(restored);
        emitted[0] ^= 1;
        Assert.Equal(book.ContentHash, restored.ContentHash);
        Assert.Single(book.Open);
    }

    [Fact]
    public void Malformed_frames_and_noncanonical_or_excessive_quotes_are_refused()
    {
        WorldPaidQuote[] ordered = new[] { Quote(500), Quote(501) }.OrderBy(value => value.QuoteId).ToArray();
        byte[] outcomes = Frame("world.paid-exchange.outcomes"u8.ToArray());
        byte[] issuers = Frame("world.paid-exchange.issuers"u8.ToArray());
        byte[] Book(byte[][] quotes) => Frame("world.paid-exchange.v1"u8.ToArray(),
            Frame(new[] { "world.paid-exchange.open"u8.ToArray() }.Concat(quotes).ToArray()), outcomes, issuers);
        byte[] valid = Book(ordered.Select(value => value.Encode()).ToArray());
        Assert.Equal(valid, WorldPaidExchangeCodec.Encode(WorldPaidExchangeCodec.Decode(valid)));
        Assert.Throws<FormatException>(() => WorldPaidExchangeCodec.Decode([.. valid, 0]));
        Assert.Throws<FormatException>(() => WorldPaidExchangeCodec.Decode(valid[..^1]));
        Assert.Throws<FormatException>(() => WorldPaidExchangeCodec.Decode(Book(ordered.Reverse().Select(value => value.Encode()).ToArray())));
        Assert.Throws<FormatException>(() => WorldPaidExchangeCodec.Decode(Book([ordered[0].Encode(), ordered[0].Encode()])));
        byte[][] excessive = Enumerable.Range(0, WorldPaidExchangeBook.MaximumOpen + 1)
            .Select(value => Quote((ulong)(700 + value))).OrderBy(value => value.QuoteId).Select(value => value.Encode()).ToArray();
        Assert.Throws<FormatException>(() => WorldPaidExchangeCodec.Decode(Book(excessive)));
        byte[] unknown = Frame("world.paid-exchange.v2"u8.ToArray(), Frame("world.paid-exchange.open"u8.ToArray()), outcomes, issuers);
        Assert.Throws<FormatException>(() => WorldPaidExchangeCodec.Decode(unknown));
    }

    [Fact]
    public void Single_fill_and_expiry_preserve_exact_terms_and_do_not_grant_another_fill()
    {
        WorldPaidQuote quote = Quote(500);
        var acceptance = new SpotAcceptance(Id(700), Id(20), Id(30), quote.QuoteId, quote.Quote.ContentHash, 1, 4, 1);
        var settlement = new WorldPaidSettlement(0, acceptance, WorldPaidQuote.SplitChildOf(acceptance.AcceptanceId), Id(75), 0, 0, 1);
        var outcome = new WorldPaidQuoteOutcome(quote, WorldPaidQuoteOutcomeKind.Filled, 12, settlement);
        var filled = new WorldPaidExchangeBook([quote], [], []).Fill(outcome);
        Assert.Empty(filled.Open);
        Assert.Equal(1UL, filled.GetIssuerRevision(quote.Quote.IssuerActorId));
        Assert.Throws<ArgumentException>(() => filled.Fill(outcome));
        var continued = WorldPaidExchangeCodec.Decode(WorldPaidExchangeCodec.Encode(filled)).ContinueAcrossCut();
        Assert.Empty(continued.Outcomes);
        Assert.Equal(filled.IssuerRevisions, continued.IssuerRevisions);
        var expiring = new WorldPaidExchangeBook([quote], [], []);
        Assert.Same(expiring, expiring.ExpireDue(quote.ExpiresAtAdvancingStep - 1, 100));
        var expired = expiring.ExpireDue(quote.ExpiresAtAdvancingStep, 101);
        Assert.Empty(expired.Open);
        Assert.Equal(WorldPaidQuoteOutcomeKind.Expired, Assert.Single(expired.Outcomes).Kind);
        Assert.Equal(quote.ContentHash, expired.Outcomes[0].Quote.ContentHash);
    }

    private static WorldPaidQuote Quote(ulong command)
    {
        var original = new SpotExchangeFixture().BerryOffer();
        var quote = new SpotQuote(WorldPaidQuote.DeriveIdentity(Id(command)), original.WorldId, original.BranchId,
            original.IssuerActorId, original.IssuerPrincipalId, original.IssuerSessionId, original.CounterpartyActorId,
            original.Side, original.LotKind, original.LotId, original.Definition, original.ExpectedLotRevision,
            original.SellerCustodianId, original.BuyerCustodianId, original.CurrencyDefinitionId, original.CurrencyVersion,
            original.SellerAccountId, original.BuyerAccountId, original.UnitPrice, original.Fees, original.QuantityAllowance,
            original.MarketRevision, original.IssuedAtStep, original.ExpiresAtStep);
        return new(Id(command), quote, 11);
    }

    private static EntityId Id(ulong value) => new(0, value);

    private static byte[] Frame(params byte[][] fields)
    {
        var bytes = new byte[12 + fields.Sum(value => 4 + value.Length)];
        "OFC\0"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), checked((uint)fields.Length));
        int offset = 12;
        foreach (byte[] value in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset), checked((uint)value.Length));
            offset += 4;
            value.CopyTo(bytes, offset);
            offset += value.Length;
        }
        return bytes;
    }
}
