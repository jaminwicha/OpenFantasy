using OpenFantasy.Client;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Content.Trading;

namespace OpenFantasy.Hosting.Tests.Client.Trading;

/// <summary>Explicit stall choices, exact review capture and the icon-led wheel entry over real participant views.</summary>
public sealed class MarketDraftTests
{
    [Fact]
    public async Task Any_changed_choice_discards_the_review_and_capture_refuses_mismatched_offers()
    {
        await using var world = new NativeMarketTestWorld();
        Assert.Equal(LocalTradeInquiryState.Offered, (await world.Ask(LocalTradeDirection.Buy, 2)).State);
        PaidQuoteView offer = Assert.Single(world.View.PaidQuotes);
        LocalMarketStallView stall = world.StallView;
        var draft = new FarmMarketDraft();
        draft.Begin(world.View, stall.MarketId, LocalTradeDirection.Buy);
        // One chosen unit within the offer's two-unit allowance is a legal partial acceptance of the exact terms.
        Assert.True(draft.CaptureReview(world.View, stall, offer));
        Assert.Equal(1UL, draft.Review!.Units);
        Assert.True(draft.SetQuantity(3, stall.MaximumUnitsPerOffer));
        Assert.False(draft.CaptureReview(world.View, stall, offer)); // more than the allowance is never reviewed
        Assert.True(draft.SetQuantity(2, stall.MaximumUnitsPerOffer));
        Assert.True(draft.CaptureReview(world.View, stall, offer));
        Assert.Equal((offer.QuoteId, 2UL), (draft.Review!.Quote.QuoteId, draft.Review.Units));

        Assert.True(draft.SetQuantity(1, stall.MaximumUnitsPerOffer));
        Assert.Null(draft.Review);
        Assert.False(draft.SetQuantity(stall.MaximumUnitsPerOffer + 1, stall.MaximumUnitsPerOffer));
        Assert.Equal(1UL, draft.Quantity);
        Assert.True(draft.SetQuantity(2, stall.MaximumUnitsPerOffer));
        Assert.True(draft.CaptureReview(world.View, stall, offer));
        Assert.True(draft.SetDirection(LocalTradeDirection.Sell));
        Assert.Null(draft.Review);
        Assert.Equal((1UL, (OpenFantasy.Core.EntityId?)null), (draft.Quantity, draft.LotId));
        Assert.False(draft.CaptureReview(world.View, stall, offer)); // a sell quote from Wren is not a buy review
        Assert.False(draft.SelectLot(stall, offer.LotId));             // Wren's lot is never the player's to show
        Assert.Null(draft.LotId);

        draft.SetDirection(LocalTradeDirection.Buy);
        draft.SetQuantity(2, stall.MaximumUnitsPerOffer);
        Assert.True(draft.CaptureReview(world.View, stall, offer));
        draft.DiscardReview();
        Assert.Null(draft.Review);
        // Reopening the panel keeps choices for the same stall but never a reviewed offer.
        Assert.True(draft.CaptureReview(world.View, stall, offer));
        draft.Begin(world.View, stall.MarketId, null);
        Assert.Null(draft.Review);
        Assert.Equal((LocalTradeDirection.Buy, 2UL), (draft.Direction, draft.Quantity));
    }

    [Fact]
    public async Task The_market_wheel_page_opens_the_panel_without_intents_and_commitments_need_explicit_choices()
    {
        await using var world = new NativeMarketTestWorld();
        IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> offers = world.Offers();
        var model = new FarmQuickWheelModel(offers);
        for (int page = 0; model.PageName != "Market"; page++)
        {
            Assert.True(page < model.PageCount);
            model.NextPage();
        }
        Assert.Equal(new FarmSemanticAction?[] { FarmSemanticAction.TradeBuy, FarmSemanticAction.TradeSell,
            FarmSemanticAction.TradeOffers, null }, model.Slots.Select(slot => slot?.Action).ToArray());
        Assert.Null(model.Selected);
        FarmActionOffer buy = offers[FarmSemanticAction.TradeBuy];
        Assert.True(buy.OpensPanel && buy.CanExecute && buy.Intent is null);
        Assert.Throws<InvalidOperationException>(() => buy.ToCommand(NativeMarketTestWorld.NewId()));
        // The session refuses synchronously, before any command or pending task exists.
        Assert.Throws<InvalidOperationException>(() => { _ = world.Client.SubmitOffer(buy); });
        // The player owns no berries yet, so selling is a known blocked choice with its reason.
        Assert.False(offers[FarmSemanticAction.TradeSell].CanExecute);
        Assert.Contains("no usable berries", offers[FarmSemanticAction.TradeSell].BlockingReason);
        Assert.True(offers[FarmSemanticAction.TradeOffers].CanExecute);
        // Without an opened draft there is no commitment to submit; with one, nothing is chosen for the player.
        Assert.False(offers.ContainsKey(FarmSemanticAction.ConfirmTrade));
        var draft = new FarmMarketDraft();
        draft.Begin(world.View, world.StallView.MarketId, LocalTradeDirection.Sell);
        IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> panel = world.Offers(draft);
        Assert.False(panel[FarmSemanticAction.ShowTradeLot].CanExecute);
        Assert.False(panel[FarmSemanticAction.ConfirmTrade].CanExecute);
        Assert.Contains("Review one offer", panel[FarmSemanticAction.ConfirmTrade].BlockingReason);
        FarmMarketRequest request = MarketActionOffers.PrepareRequest(world.View, world.StallView, draft, null);
        Assert.Null(request.Inquiry);
        Assert.Contains("Choose one exact lot", request.BlockingReason);
        draft.SetDirection(LocalTradeDirection.Buy);
        LocalTradeInquiry inquiry = MarketActionOffers.PrepareRequest(world.View, world.StallView, draft, null).Inquiry!;
        Assert.Equal((world.Stall.MarketId, LocalTradeDirection.Buy, 1UL, (OpenFantasy.Core.EntityId?)null),
            (inquiry.MarketId, inquiry.Direction, inquiry.Quantity, inquiry.OwnShownLotId));
        Assert.Equal(world.Stall.Commodity, inquiry.Commodity);
    }

    [Fact]
    public void Worlds_without_markets_keep_their_earlier_offers_and_wheel_pages()
    {
        WorldSnapshot gifts = WorldMaterialProposalAdmission.CreateFresh(OpenFantasy.Hosting.Content.RestorativeJourneyAdmission
            .CreateEmbedded(NativeMarketTestWorld.SeedText, NativeMarketTestWorld.PlayerName));
        Assert.Null(OpenFantasy.Hosting.Content.NativeTradingWorld.FindStall(gifts));
        var player = gifts.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        var view = new LocalWorldView(gifts, player);
        Assert.Null(view.Markets);
        var control = gifts.Inventory.Controls.Single(value => value.ActorId == player);
        var wren = gifts.Quests!.Definitions.Single().GiverActorId;
        var offers = new FarmActionOffers(new OpenFantasy.Core.Authority.TrustedCommandDelivery(control.PrincipalId, control.SessionId),
            wren, gifts.Inventory.Sources.First().Definition.DefinitionId, null).Create(view, null, null, new FarmMarketDraft());
        Assert.DoesNotContain(offers.Keys, action => MarketActionOffers.IsStallChoice(action)
            || action is FarmSemanticAction.ShowTradeLot or FarmSemanticAction.ConfirmTrade);
        var model = new FarmQuickWheelModel(offers);
        Assert.DoesNotContain("Market", Enumerable.Range(0, model.PageCount).Select(_ => { string name = model.PageName; model.NextPage(); return name; }));
    }
}
