using OpenFantasy.Client;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Markets;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Content.Trading;

namespace OpenFantasy.Hosting.Tests.Client.Trading.Refinement;

/// <summary>Player-language stall terms and the stall/commodity identity a quote must share, over real views.</summary>
public sealed class MarketWordingAndMatchingTests
{
    private static readonly string[] ImplementationTerms = ["step", "segment", "revision", "multiplayer", "Status:"];

    [Fact]
    public void Goods_agree_with_their_count_and_mass_or_singular_names_stay_unchanged()
    {
        Assert.Equal("1 berry", FarmMarketTerms.Count(1, "berries"));
        Assert.Equal("4 berries", FarmMarketTerms.Count(4, "berries"));
        Assert.Equal("1 apple", FarmMarketTerms.Count(1, "apples"));
        Assert.Equal("1 wood", FarmMarketTerms.Count(1, "wood"));
        Assert.Equal("1 glass", FarmMarketTerms.Count(1, "glass"));
        Assert.Equal("0 berries", FarmMarketTerms.Count(0, "berries"));
    }

    [Fact]
    public async Task Offer_cards_state_price_total_time_and_shortfall_without_implementation_terms()
    {
        await using var world = new NativeMarketTestWorld();
        Assert.Equal(LocalTradeInquiryState.Offered, (await world.Ask(LocalTradeDirection.Buy, 1)).State);
        PaidQuoteView offer = Assert.Single(world.View.PaidQuotes);
        LocalWorldView view = world.View;
        string merchant = FarmMarketTerms.Merchant(view, world.StallView);
        string goods = FarmMarketTerms.Goods(view, world.StallView);
        string describe = FarmMarketTerms.Describe(view, offer, 1, merchant, goods);
        string restate = FarmMarketTerms.Restate(view, offer, 1, merchant, goods);
        string request = FarmMarketTerms.Request(world.Session.ReadTradeInquiry(), view.Markets!.AdvancingStep, true, merchant, goods);
        string outcome = FarmMarketTerms.Outcome(view, offer, merchant, goods);
        foreach (string text in new[] { describe, restate, request, outcome, FarmMarketTerms.Summary(view, offer, 1, goods) })
        {
            Assert.DoesNotContain(ImplementationTerms, term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain("1 berries", text);
        }
        // The fresh purse is the source zero: price, total, remaining time, purse and shortfall stay visible.
        Assert.Contains($"at {offer.UnitPrice} gil each", describe);
        Assert.Contains($"total {offer.TotalFor(1)} gil", describe);
        Assert.Contains("while time runs; pausing stops this clock", describe);
        Assert.Contains("Your purse: 0 gil", describe);
        Assert.Contains($"{offer.TotalFor(1)} gil short", describe);
        Assert.Contains("you have 0", FarmMarketTerms.Summary(view, offer, 1, goods));
        Assert.Contains("Buy 1 berry from", restate);
        Assert.Contains("Your purse holds 0 gil", restate);
        Assert.Contains("buy 1 berry", request);
        Assert.Contains("Diagnostics: advancing step", FarmMarketTerms.Diagnostics(view.Markets.AdvancingStep));
    }

    [Fact]
    public async Task A_quote_from_the_same_merchant_for_other_goods_is_never_the_stalls_offer_or_review()
    {
        await using var world = new NativeMarketTestWorld();
        Assert.Equal(LocalTradeInquiryState.Offered, (await world.Ask(LocalTradeDirection.Buy, 2)).State);
        PaidQuoteView berries = Assert.Single(world.View.PaidQuotes);
        LocalMarketStallView stall = world.StallView;
        SpotQuote actual = world.State.Coordination!.PaidExchange!.FindOpen(berries.QuoteId)!.Quote;
        CountedItemDefinition wood = world.State.Gathering!.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Oak)
            .Definition.YieldDefinition;
        Assert.NotEqual(stall.Commodity, wood);
        // The same issuer, counterparty, side and terms, differing only in the goods named.
        var otherGoods = new PaidQuoteView(new SpotQuote(actual.QuoteId, actual.WorldId, actual.BranchId, actual.IssuerActorId,
                actual.IssuerPrincipalId, actual.IssuerSessionId, actual.CounterpartyActorId, actual.Side, actual.LotKind, actual.LotId,
                wood, actual.ExpectedLotRevision, actual.SellerCustodianId, actual.BuyerCustodianId, actual.CurrencyDefinitionId,
                actual.CurrencyVersion, actual.SellerAccountId, actual.BuyerAccountId, actual.UnitPrice, actual.Fees,
                actual.QuantityAllowance, actual.MarketRevision, actual.IssuedAtStep, actual.ExpiresAtStep),
            world.Player, PaidQuoteStatus.Open, null, null, berries.ObservedIssuerRevision);
        Assert.True(MarketActionOffers.BelongsTo(stall, berries));
        Assert.False(MarketActionOffers.BelongsTo(stall, otherGoods));

        var draft = new FarmMarketDraft();
        draft.Begin(world.View, stall.MarketId, LocalTradeDirection.Buy);
        Assert.False(draft.CaptureReview(world.View, stall, otherGoods));
        Assert.Null(draft.Review);
        Assert.True(draft.CaptureReview(world.View, stall, berries));
        Assert.Equal(berries.QuoteId, MarketActionOffers.OpenOffer(world.View, stall)!.QuoteId);
    }
}
