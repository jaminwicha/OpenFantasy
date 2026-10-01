using System.Buffers.Binary;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Tests.Markets;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Tests.WorldExchange;

/// <summary>
/// The paid-quote owner's lifecycle: a fill supersedes its issuer's other open quotes in the same owner,
/// the issuer's own withdrawal is a typed terminal cause, and outcomes keep one strict canonical order
/// that decoding enforces instead of normalizing.
/// </summary>
public sealed class PaidQuoteLifecycleTests
{
    private static readonly ulong IssuedStep = new SpotExchangeFixture().BerryOffer().IssuedAtStep;

    [Fact]
    public void A_fill_supersedes_every_sibling_with_exact_terms_and_one_revision()
    {
        WorldPaidQuote filled = Quote(500), first = Quote(501), second = Quote(502);
        WorldPaidQuote other = Quote(510, merchantB: true);
        var book = new WorldPaidExchangeBook(Ordered(filled, first, second, other), [], []);
        Assert.True(book.CanFill(filled));
        WorldPaidExchangeBook after = book.Fill(Fill(filled, 12, 0, 700));

        // Only the other issuer's quote stays open; the issuer advanced once for its one fill.
        Assert.Equal(other.QuoteId, Assert.Single(after.Open).QuoteId);
        Assert.Equal(1UL, after.GetIssuerRevision(filled.Quote.IssuerActorId));
        Assert.Equal(0UL, after.GetIssuerRevision(other.Quote.IssuerActorId));
        Assert.Equal(WorldPaidQuoteOutcomeKind.Filled, after.Outcomes[0].Kind);
        WorldPaidQuoteOutcome[] superseded = after.Outcomes.Skip(1).ToArray();
        Assert.Equal(new[] { first, second }.OrderBy(value => value.QuoteId).Select(value => value.ContentHash),
            superseded.Select(value => value.Quote.ContentHash));
        Assert.All(superseded, value =>
        {
            Assert.Equal(WorldPaidQuoteOutcomeKind.Superseded, value.Kind);
            Assert.Equal(12UL, value.WorldSequence);
            Assert.Equal(Id(700), value.Supersession!.AcceptanceId);
            Assert.Equal(filled.QuoteId, value.Supersession.FilledQuoteId);
            Assert.Null(value.Settlement);
            Assert.Null(value.CommandId);
        });
        // Supersession moves nothing: the only currency cause is the single fill.
        Assert.Single(after.Fills);
        Assert.Equal(after.ContentHash, WorldPaidExchangeCodec.Decode(WorldPaidExchangeCodec.Encode(after)).ContentHash);
        Assert.Throws<ArgumentException>(() => after.Fill(Fill(first, 13, 0, 701)));
    }

    [Fact]
    public void Withdrawal_is_a_typed_issuer_command_cause_with_exact_bytes_only_in_the_paid_profile()
    {
        WorldPaidQuote quote = Quote(500), kept = Quote(501);
        var book = new WorldPaidExchangeBook(Ordered(quote, kept), [], []);
        var delivery = SpotExchangeFixture.MerchantADelivery;
        var withdrawn = new WorldPaidQuoteOutcome(quote, 12, new WorldPaidWithdrawal(Id(800), delivery, 3));
        WorldPaidExchangeBook after = book.Withdraw(withdrawn);
        Assert.Equal(kept.QuoteId, Assert.Single(after.Open).QuoteId);
        WorldPaidQuoteOutcome outcome = Assert.Single(after.Outcomes);
        Assert.Equal((WorldPaidQuoteOutcomeKind.Withdrawn, Id(800), 3U), (outcome.Kind, outcome.CommandId!.Value, outcome.CommandOrdinal!.Value));
        Assert.Equal(delivery, outcome.Withdrawal!.Delivery);
        Assert.Empty(after.IssuerRevisions);
        Assert.Equal(WorldPaidExchangeCodec.Encode(after),
            WorldPaidExchangeCodec.Encode(WorldPaidExchangeCodec.Decode(WorldPaidExchangeCodec.Encode(after))));
        Assert.Throws<ArgumentException>(() => after.Withdraw(withdrawn));
        Assert.Throws<ArgumentException>(() => new WorldPaidQuoteOutcome(quote, WorldPaidQuoteOutcomeKind.Withdrawn, 12, null));
        Assert.Throws<ArgumentException>(() => new WorldPaidQuoteOutcome(quote, 12,
            new WorldPaidWithdrawal(quote.CreationCommandId, delivery, 0)));

        var command = new WorldActionCommand(Id(800), delivery.PrincipalId, delivery.SessionId, quote.Quote.IssuerActorId,
            quote.Quote.WorldId, quote.Quote.BranchId, 3, new WithdrawSpotQuoteIntent(quote.QuoteId));
        byte[] encoded = WorldActionCodec.Encode(command);
        Assert.Equal(encoded, WorldActionCodecV17.Encode(command));
        Assert.Equal(encoded, WorldActionCodec.Encode(WorldActionCodec.Decode(encoded)));
        Assert.ThrowsAny<FormatException>(() => WorldActionCodec.Decode([.. encoded, 0]));
        foreach (ushort old in new ushort[] { 26, 27, 28, 29, 30, 31 })
        {
            Assert.Throws<ArgumentException>(() => new WorldStepBatch(Id(900), command.WorldId, command.BranchId,
                11, 10, WorldBatchKind.AdvancingStep, [], [command], old));
        }
        _ = new WorldStepBatch(Id(900), command.WorldId, command.BranchId, 11, 10, WorldBatchKind.AdvancingStep, [], [command], 32);
    }

    [Fact]
    public void Every_independent_rearrangement_of_same_step_outcomes_is_refused()
    {
        WorldPaidExchangeBook canonical = CanonicalStep();
        Assert.Equal(new[]
        {
            WorldPaidQuoteOutcomeKind.Filled, WorldPaidQuoteOutcomeKind.Superseded, WorldPaidQuoteOutcomeKind.Superseded,
            WorldPaidQuoteOutcomeKind.Withdrawn, WorldPaidQuoteOutcomeKind.Expired, WorldPaidQuoteOutcomeKind.Expired
        }, canonical.Outcomes.Select(value => value.Kind));
        Assert.All(canonical.Outcomes, value => Assert.Equal(12UL, value.WorldSequence));
        Assert.True(canonical.Outcomes[1].Quote.QuoteId.CompareTo(canonical.Outcomes[2].Quote.QuoteId) < 0);
        Assert.True(canonical.Outcomes[4].Quote.QuoteId.CompareTo(canonical.Outcomes[5].Quote.QuoteId) < 0);
        byte[] bytes = WorldPaidExchangeCodec.Encode(canonical);
        Assert.Equal(bytes, WorldPaidExchangeCodec.Encode(WorldPaidExchangeCodec.Decode(bytes)));

        // An independent frame reader rearranges the supplied outcome bytes without the owner's encoder.
        byte[][] book = ReadFields(bytes);
        byte[][] outcomes = ReadFields(book[2]);
        int rearranged = 0;
        for (int first = 1; first < outcomes.Length; first++)
        {
            for (int second = first + 1; second < outcomes.Length; second++)
            {
                byte[][] swapped = (byte[][])outcomes.Clone();
                (swapped[first], swapped[second]) = (swapped[second], swapped[first]);
                byte[][] hostile = (byte[][])book.Clone();
                hostile[2] = WriteFields(swapped);
                Assert.Throws<FormatException>(() => WorldPaidExchangeCodec.Decode(WriteFields(hostile)));
                rearranged++;
            }
        }
        Assert.Equal(15, rearranged);
        // The same outcomes in a fully reversed order are refused as well, never normalized.
        byte[][] reversed = [outcomes[0], .. outcomes.Skip(1).Reverse()];
        byte[][] reversedBook = (byte[][])book.Clone();
        reversedBook[2] = WriteFields(reversed);
        Assert.Throws<FormatException>(() => WorldPaidExchangeCodec.Decode(WriteFields(reversedBook)));
    }

    [Fact]
    public void Supersession_needs_an_actual_fill_and_no_stale_sibling_may_stay_open()
    {
        WorldPaidQuote filled = Quote(500), sibling = Quote(501);
        WorldPaidQuoteOutcome fill = Fill(filled, 12, 0, 700);
        var revision = new KeyValuePair<EntityId, ulong>(filled.Quote.IssuerActorId, 1);
        // A stale sibling left open after its issuer's fill would hold capacity it can never use.
        var stale = Assert.Throws<ArgumentException>(() => new WorldPaidExchangeBook([sibling], [fill], [revision]));
        Assert.Contains("current market revision", stale.Message);
        // A fabricated cause, another step or another issuer's fill cannot supersede a quote.
        foreach (var forged in new[]
        {
            new WorldPaidQuoteOutcome(sibling, 12, new WorldPaidSupersession(Id(999), filled.QuoteId)),
            new WorldPaidQuoteOutcome(sibling, 13, new WorldPaidSupersession(Id(700), filled.QuoteId)),
            new WorldPaidQuoteOutcome(sibling, 12, new WorldPaidSupersession(Id(700), Quote(502).QuoteId))
        })
        {
            Assert.Throws<ArgumentException>(() => new WorldPaidExchangeBook([], [fill, forged], [revision]));
        }
        WorldPaidQuote foreign = Quote(510, merchantB: true);
        Assert.Throws<ArgumentException>(() => new WorldPaidExchangeBook([], [fill,
            new WorldPaidQuoteOutcome(foreign, 12, new WorldPaidSupersession(Id(700), filled.QuoteId))], [revision]));
        // Two primary outcomes cannot share one command position in either order.
        var withdrawal = new WorldPaidQuoteOutcome(sibling, 12, new WorldPaidWithdrawal(Id(800), SpotExchangeFixture.MerchantADelivery, 0));
        Assert.Throws<ArgumentException>(() => new WorldPaidExchangeBook([], [fill, withdrawal], [revision]));
        Assert.Throws<ArgumentException>(() => new WorldPaidExchangeBook([], [withdrawal, fill], [revision]));
    }

    [Fact]
    public void Never_pruned_issuer_revisions_stay_within_the_admitted_actor_roster()
    {
        // Issuers are present world actors; both rosters stay below the issuer bound, so no revision is reset.
        Assert.InRange(LocalSpatialSnapshot.MaximumActors, 1, WorldPaidExchangeBook.MaximumIssuers);
        Assert.InRange(WorldStepBatch.MaximumActorsPerBatch, 1, WorldPaidExchangeBook.MaximumIssuers);
        // A fill closes at most every open quote, and the cut reservation keeps that many outcome slots.
        Assert.InRange(WorldPaidExchangeBook.MaximumOpen, 1, WorldPaidExchangeBook.MaximumOutcomes - 1);
    }

    private static WorldPaidExchangeBook CanonicalStep()
    {
        WorldPaidQuote filled = Quote(500), siblingA = Quote(501), siblingB = Quote(502);
        WorldPaidQuote dueA = Quote(510, merchantB: true, lifetime: 1), withdrawn = Quote(511, merchantB: true),
            dueB = Quote(512, merchantB: true, lifetime: 1);
        var book = new WorldPaidExchangeBook(Ordered(filled, siblingA, siblingB, dueA, withdrawn, dueB), [], []);
        // One step: the counterparty's acceptance at position 0, the other issuer's withdrawal at position 1,
        // then the time boundary that expires two due quotes.
        book = book.Fill(Fill(filled, 12, 0, 700));
        book = book.Withdraw(new WorldPaidQuoteOutcome(withdrawn, 12,
            new WorldPaidWithdrawal(Id(800), SpotExchangeFixture.MerchantBDelivery, 1)));
        return book.ExpireDue(IssuedStep + 1, 12);
    }

    private static WorldPaidQuoteOutcome Fill(WorldPaidQuote quote, ulong sequence, uint index, ulong acceptanceId)
    {
        var acceptance = new SpotAcceptance(Id(acceptanceId), Id(20), Id(30), quote.QuoteId, quote.Quote.ContentHash, 1, 4, 1);
        return new WorldPaidQuoteOutcome(quote, WorldPaidQuoteOutcomeKind.Filled, sequence,
            new WorldPaidSettlement(index, acceptance, WorldPaidQuote.SplitChildOf(acceptance.AcceptanceId), Id(75), 0, 0, 1));
    }

    private static IEnumerable<WorldPaidQuote> Ordered(params WorldPaidQuote[] quotes) => quotes.OrderBy(value => value.QuoteId);

    private static WorldPaidQuote Quote(ulong command, bool merchantB = false, ulong lifetime = 64)
    {
        SpotQuote original = new SpotExchangeFixture().BerryOffer();
        var delivery = merchantB ? SpotExchangeFixture.MerchantBDelivery : SpotExchangeFixture.MerchantADelivery;
        var quote = new SpotQuote(WorldPaidQuote.DeriveIdentity(Id(command)), original.WorldId, original.BranchId,
            merchantB ? SpotExchangeFixture.MerchantB : original.IssuerActorId, delivery.PrincipalId, delivery.SessionId,
            original.CounterpartyActorId, original.Side, original.LotKind, original.LotId, original.Definition,
            original.ExpectedLotRevision, merchantB ? SpotExchangeFixture.StallB : original.SellerCustodianId,
            original.BuyerCustodianId, original.CurrencyDefinitionId, original.CurrencyVersion,
            merchantB ? SpotExchangeFixture.BAccount : original.SellerAccountId, original.BuyerAccountId,
            original.UnitPrice, original.Fees, original.QuantityAllowance, 0, original.IssuedAtStep,
            original.IssuedAtStep + lifetime);
        return new(Id(command), quote, 11);
    }

    private static EntityId Id(ulong value) => new(0, value);

    // An independent frame reader and writer deliberately bypass the owner's codec for hostile bytes.
    private static byte[][] ReadFields(byte[] bytes)
    {
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8, 4)));
        var result = new byte[count][];
        int offset = 12;
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4)));
            offset += 4;
            result[index] = bytes.AsSpan(offset, length).ToArray();
            offset += length;
        }
        Assert.Equal(bytes.Length, offset);
        return result;
    }

    private static byte[] WriteFields(byte[][] fields)
    {
        var bytes = new byte[12 + fields.Sum(value => 4 + value.Length)];
        "OFC\0"u8.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), checked((uint)fields.Length));
        int offset = 12;
        foreach (byte[] field in fields)
        {
            BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset), checked((uint)field.Length));
            offset += 4;
            field.CopyTo(bytes, offset);
            offset += field.Length;
        }
        return bytes;
    }
}
