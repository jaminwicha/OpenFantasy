using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Tests.Production;
using static OpenFantasy.Core.Tests.Markets.SpotExchangeFixture;

namespace OpenFantasy.Core.Tests.Markets;

public sealed class SpotExchangeTests
{
    [Fact]
    public void Buy_raw_then_sell_crafted_across_two_issuers_conserves_goods_money_and_provenance()
    {
        var market = new SpotExchangeFixture();
        SpotQuote offer = market.BerryOffer();
        market.Issue(offer, MerchantADelivery);
        Assert.Equal(SpotQuoteStatus.Open, market.Book.FindEntry(offer.QuoteId)!.Status);
        Assert.Equal(100UL, market.Balance(TraderAccount));

        SpotExchangePlan bought = market.Decide(Accept(offer, 3));
        market.Commit(bought);
        CountedSourceLot child = market.Inventory.Custody.GetLot(bought.Fill!.MovedLotId);
        Assert.Equal((Pack, 0UL, 3UL, Id(75)), (child.CustodianId, child.Source.Start, child.Source.End, child.Source.SourceId));
        Assert.Equal((3UL, 10UL), (market.Inventory.Custody.GetLot(StallBerries).Source.Start,
            market.Inventory.Custody.GetLot(StallBerries).Source.End));
        Assert.Equal((85UL, 62UL, 3UL, Supply), (market.Balance(TraderAccount), market.Balance(AAccount),
            market.Balance(GuildAccount), market.TotalBalance()));
        Assert.Equal(SpotQuoteStatus.Filled, market.Book.FindEntry(offer.QuoteId)!.Status);

        TransformedLot rations = market.CraftRations();
        SpotQuote bid = market.RationBid(rations);
        market.Issue(bid, MerchantBDelivery);
        SpotExchangePlan sold = market.Decide(Accept(bid, 2, 951));
        market.Commit(sold);
        TransformedLot delivered = market.Production.Lots.Single(value => value.LotId == rations.LotId);
        Assert.Equal((StallB, rations.Output, 1UL), (delivered.CustodianId, delivered.Output, delivered.Revision));
        Assert.Equal((103UL, 62UL, 62UL, 3UL, Supply), (market.Balance(TraderAccount), market.Balance(AAccount),
            market.Balance(BAccount), market.Balance(GuildAccount), market.TotalBalance()));
        market.Production.ValidateForAuthority(market.Inventory);
        market.Production.ValidateCustodyEvidence(market.Book.ProducedMovements());
        Assert.Throws<ArgumentException>(() => market.Production.ValidateCustodyEvidence([]));
        Assert.Equal(60, market.Progression.GetActor(Trader).CurrentHp);
        Assert.Equal(2, market.Book.Fills.Count);
        Assert.Equal(1UL, market.Book.GetIssuerRevision(MerchantA));
    }

    [Fact]
    public void Refusals_return_the_untouched_originals_for_every_owner()
    {
        var market = new SpotExchangeFixture();
        SpotQuote offer = market.BerryOffer();
        market.Issue(offer, MerchantADelivery);
        SpotAcceptance valid = Accept(offer, 3);
        var intruder = new TrustedCommandDelivery(Id(21), Id(31));
        var cases = new (SpotExchangePlan Plan, SpotExchangeRejection Expected)[]
        {
            (market.Decide(Accept(offer, 3, claims: intruder), intruder), SpotExchangeRejection.NotAuthorized),
            (market.Decide(valid, intruder), SpotExchangeRejection.NotAuthorized),
            (market.Decide(new SpotAcceptance(valid.AcceptanceId, valid.PrincipalId, valid.OriginSessionId, offer.QuoteId,
                offer.ContentHash, 3, 11, 3)), SpotExchangeRejection.NotAuthorized),
            (market.Decide(new SpotAcceptance(valid.AcceptanceId, valid.PrincipalId, valid.OriginSessionId, offer.QuoteId,
                market.BerryOffer(price: 3).ContentHash, 3, 9, 3)), SpotExchangeRejection.NotAuthorized),
            (market.Decide(Accept(offer, 6)), SpotExchangeRejection.NotAuthorized),
            (market.Decide(valid, step: 101), SpotExchangeRejection.Expired),
            (market.Decide(valid, step: 9), SpotExchangeRejection.Expired)
        };
        foreach (var (plan, expected) in cases)
        {
            Assert.Equal((SpotExchangeOutcome.Rejected, expected), (plan.Outcome, plan.Rejection));
            Assert.Same(market.Inventory, plan.CandidateInventory);
            Assert.Same(market.Production, plan.CandidateProduction);
            Assert.Same(market.Progression, plan.CandidateProgression);
            Assert.Same(market.Book, plan.CandidateBook);
            Assert.Throws<InvalidOperationException>(() => plan.ApplyTo(market.Inventory, market.Production,
                market.Progression, market.Book));
        }

        market.History.Add(valid.AcceptanceId);
        Assert.Equal(SpotExchangeRejection.IdentityConflict, market.Decide(valid).Rejection);
    }

    [Fact]
    public void Funds_capacity_and_protected_stock_are_revalidated_at_acceptance()
    {
        var poor = new SpotExchangeFixture(traderGil: 14);
        SpotQuote offer = poor.BerryOffer();
        poor.Issue(offer, MerchantADelivery);
        Assert.Equal(SpotExchangeRejection.InsufficientFunds, poor.Decide(Accept(offer, 3)).Rejection);
        Assert.Equal(SpotExchangeOutcome.Applied, poor.Decide(Accept(offer, 2)).Outcome);

        var full = new SpotExchangeFixture(packCapacity: 12);
        SpotQuote fullOffer = full.BerryOffer();
        full.Issue(fullOffer, MerchantADelivery);
        Assert.Equal(SpotExchangeRejection.InsufficientCapacity, full.Decide(Accept(fullOffer, 3)).Rejection);

        var favorite = new SpotExchangeFixture(stallProtection: LotProtection.Favorite);
        SpotQuote protectedOffer = favorite.BerryOffer();
        favorite.Issue(protectedOffer, MerchantADelivery);
        Assert.Equal(SpotExchangeRejection.ProtectedStock, favorite.Decide(Accept(protectedOffer, 3)).Rejection);
    }

    [Fact]
    public void Single_fill_closes_allowance_exact_retry_is_duplicate_and_reuse_conflicts()
    {
        var market = new SpotExchangeFixture();
        SpotQuote offer = market.BerryOffer();
        market.Issue(offer, MerchantADelivery);
        SpotAcceptance first = Accept(offer, 2);
        market.Commit(market.Decide(first));
        SpotQuoteBook settled = market.Book;

        SpotExchangePlan retry = market.Decide(first);
        Assert.Equal(SpotExchangeOutcome.Duplicate, retry.Outcome);
        Assert.Same(settled, retry.CandidateBook);
        Assert.Equal(first.AcceptanceId, retry.Fill!.AcceptanceId);
        Assert.Equal(SpotExchangeRejection.QuoteClosed, market.Decide(Accept(offer, 2, 952)).Rejection);
        Assert.Equal(SpotExchangeRejection.QuoteClosed, market.Decide(Accept(offer, 3)).Rejection);

        // The unused allowance is closed; a new quote must price the lot's advanced revision.
        SpotQuote stale = market.BerryOffer(quoteId: 903);
        Assert.Equal(SpotExchangeRejection.RevisionConflict, new SpotExchangeAuthority()
            .Issue(market.Inventory, market.Production, market.Progression, market.Book, new SpotQuote(stale.QuoteId, World, Branch, MerchantA,
                MerchantADelivery.PrincipalId, MerchantADelivery.SessionId, Trader, SpotQuoteSide.IssuerSells,
                SpotLotKind.CountedSource, StallBerries, InstantCraftFixture.Berries, stale.ExpectedLotRevision, StallA, Pack,
                stale.CurrencyDefinitionId, stale.CurrencyVersion, AAccount, TraderAccount, 4, [], 5, 0, 10, 100),
                MerchantADelivery, 10, market.Occupied).Rejection);
        market.Issue(stale, MerchantADelivery);
        Assert.Equal(SpotExchangeRejection.IdentityConflict, market.Decide(Accept(stale, 1, 950)).Rejection);
        Assert.Equal(SpotExchangeOutcome.Applied, market.Decide(Accept(stale, 1, 953)).Outcome);
    }

    [Fact]
    public void Stale_lot_controls_and_every_owner_basis_are_refused()
    {
        var market = new SpotExchangeFixture();
        SpotQuote offer = market.BerryOffer();
        market.Issue(offer, MerchantADelivery);
        SpotExchangePlan plan = market.Decide(Accept(offer, 3));
        Assert.True(plan.IsApplied);

        var other = new SpotExchangeFixture();
        other.Issue(other.BerryOffer(quoteId: 904), MerchantADelivery);
        // A content-identical owner is the same basis; any changed fact in any owner makes the plan stale.
        plan.ApplyTo(other.Inventory, market.Production, market.Progression, market.Book);
        Assert.Throws<CustodyConflictException>(() => plan.ApplyTo(new SpotExchangeFixture(packCapacity: 63).Inventory,
            market.Production, market.Progression, market.Book));
        Assert.Throws<CustodyConflictException>(() => plan.ApplyTo(market.Inventory, other.CraftAndReturn(), market.Progression, market.Book));
        Assert.Throws<CustodyConflictException>(() => plan.ApplyTo(market.Inventory, market.Production,
            new SpotExchangeFixture(traderGil: 99).Progression, market.Book));
        Assert.Throws<CustodyConflictException>(() => plan.ApplyTo(market.Inventory, market.Production, market.Progression, other.Book));

        // Rebinding the merchant's control invalidates the delivery recorded at issuance.
        var rebound = new CountedAuthorityState(market.Inventory.Custody, market.Inventory.AdmittedUnitCapacity,
            market.Inventory.Actors, market.Inventory.Controls.Select(value => value.ActorId == MerchantA
                ? value with { SessionId = Id(39) } : value), market.Inventory.Policies, market.Inventory.Sources);
        market.Inventory = rebound;
        Assert.Equal(SpotExchangeRejection.NotAuthorized, market.Decide(Accept(offer, 3)).Rejection);
    }

    [Fact]
    public void Ordered_material_history_refuses_an_unrecorded_paid_trade()
    {
        var market = new SpotExchangeFixture();
        TransformedLot rations = market.CraftRations();
        market.Production = market.Production.ContinueConsentedMaterial(market.Inventory);
        SpotQuote bid = market.RationBid(rations);
        market.Issue(bid, MerchantBDelivery);
        SpotExchangePlan refused = market.Decide(Accept(bid, 1));
        Assert.Equal(SpotExchangeRejection.UnsupportedHistory, refused.Rejection);
        Assert.Same(market.Production, refused.CandidateProduction);
    }

    [Fact]
    public void Codec_round_trips_and_refuses_malformed_frames()
    {
        var market = new SpotExchangeFixture();
        SpotQuote offer = market.BerryOffer();
        market.Issue(offer, MerchantADelivery);
        SpotAcceptance acceptance = Accept(offer, 3);
        market.Commit(market.Decide(acceptance));
        byte[] quoteBytes = SpotExchangeCodecV1.Encode(offer);
        Assert.Equal(offer.ContentHash, SpotExchangeCodecV1.DecodeQuote(quoteBytes).ContentHash);
        Assert.Equal(acceptance.ContentHash, SpotExchangeCodecV1.DecodeAcceptance(SpotExchangeCodecV1.Encode(acceptance)).ContentHash);
        byte[] bookBytes = SpotExchangeCodecV1.Encode(market.Book);
        Assert.Equal(bookBytes, SpotExchangeCodecV1.Encode(SpotExchangeCodecV1.DecodeBook(bookBytes)));
        Assert.Equal(AuthorityFormatFailure.TrailingData, Assert.Throws<AuthorityFormatException>(
            () => SpotExchangeCodecV1.DecodeQuote([.. quoteBytes, 0])).Failure);
        Assert.Equal(AuthorityFormatFailure.UnknownType, Assert.Throws<AuthorityFormatException>(
            () => SpotExchangeCodecV1.DecodeQuote(SpotExchangeCodecV1.Encode(acceptance))).Failure);
        Assert.Throws<AuthorityFormatException>(() => SpotExchangeCodecV1.DecodeBook(bookBytes.AsSpan(0, bookBytes.Length - 1)));
        Assert.Throws<ArgumentException>(() => new SpotQuote(Id(905), World, Branch, MerchantA, Id(21), Id(31), Trader,
            SpotQuoteSide.IssuerSells, SpotLotKind.CountedSource, StallBerries, InstantCraftFixture.Berries, 0, StallA, Pack,
            offer.CurrencyDefinitionId, offer.CurrencyVersion, AAccount, TraderAccount, ulong.MaxValue / 2,
            [new SpotFee(GuildAccount, ulong.MaxValue / 2)], 2, 0, 10, 100));
    }
}

internal static class SpotExchangeFixtureExtensions
{
    internal static ProductionSnapshot CraftAndReturn(this SpotExchangeFixture market)
    {
        market.CraftRations(6100);
        return market.Production;
    }
}
