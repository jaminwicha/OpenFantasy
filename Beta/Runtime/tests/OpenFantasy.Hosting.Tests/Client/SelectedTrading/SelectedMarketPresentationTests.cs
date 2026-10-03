using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Content.Trading;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.Simulation.SelectedQueries;

namespace OpenFantasy.Hosting.Tests.Client.SelectedTrading;

public sealed class SelectedMarketPresentationTests
{
    [Fact]
    public async Task Selected_trade_uses_its_own_wallet_stalls_and_exact_delivery_without_inventory_permission()
    {
        await using var fixture = await SelectedQueryTradingFixture.Open();
        await SelectedMarketFunding.EarnQuestReward(fixture);
        var player = await fixture.Acquire();
        var observation = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Source.Owner);
        var market = FarmMarketPresentation.From(observation, shared: true);
        Assert.Null(observation.ControlledView!.Inventory);
        Assert.Null(observation.ControlledView.Production);
        Assert.Null(observation.ControlledView.Recovery);
        Assert.Equal(fixture.Source.Owner, market.ActorId);
        Assert.Equal(observation.ControlledView.Trade!.Wallet!.Balance, market.CurrencyBalance);
        Assert.Equal(observation.Selection, market.Selection);
        var stall = Assert.Single(market.Markets!.Stalls, stall => stall.MarketId == fixture.Market.MarketId);
        var draft = new FarmMarketDraft();
        draft.Begin(market, stall.MarketId, LocalTradeDirection.Buy);
        var request = MarketActionOffers.PrepareRequest(market, stall, draft, null);
        Assert.NotNull(request.Inquiry);
        Assert.Null(request.Inquiry.OwnShownLotId);
        var pending = await player.SubmitSelectedTradeInquiryAsync(observation.Selection, request.Inquiry);
        pending.ValidateFor(observation, LocalSelectedQueryOperation.SubmitTradeInquiry);
        player.ResumeSelected(observation);
        await SessionFixture.Tick(fixture.Time);
        player.PauseSelected(observation.Selection);
        observation = player.ReadSelectedObservation();
        market = FarmMarketPresentation.From(observation, shared: true);
        stall = Assert.Single(market.Markets!.Stalls, stall => stall.MarketId == fixture.Market.MarketId);
        var quote = Assert.Single(market.PaidQuotes);
        Assert.True(draft.CaptureReview(market, stall, quote));
        var review = draft.Review;
        var scope = FarmActionScope.From(fixture.Source.Delivery(fixture.Source.Grantee), observation);
        var offers = new Dictionary<FarmSemanticAction, FarmActionOffer>();
        MarketActionOffers.Add(market, scope, draft, offers);
        Assert.Same(review, draft.Review);
        var confirmation = offers[FarmSemanticAction.ConfirmTrade];
        Assert.True(confirmation.CanExecute, confirmation.BlockingReason);
        var command = confirmation.ToCommand(confirmation.CommandId!.Value);
        Assert.Equal(fixture.Source.Owner, command.ActorId);
        Assert.Equal(observation.Selection.ControlWitness, command.ControlWitness);
        Assert.Equal(scope.PrincipalId, command.PrincipalId);
        Assert.Equal(market.ActorRevision, command.ExpectedActorRevision);
        var acceptance = Assert.IsType<AcceptSpotQuoteIntent>(command.Intent).Acceptance;
        Assert.Equal(quote.QuoteId, acceptance.QuoteId);
        Assert.Equal(quote.QuoteHash, acceptance.QuoteHash);
        Assert.Contains("other players may continue", confirmation.DetailText);
        Assert.DoesNotContain("pausing stops this clock", confirmation.DetailText);
    }

    [Fact]
    public async Task Returning_to_same_actor_discards_old_lot_quantity_and_review_generation()
    {
        await using var fixture = await SelectedQueryTradingFixture.Open();
        var player = await fixture.Acquire();
        var first = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Source.Owner);
        var presentation = FarmMarketPresentation.From(first);
        var stall = presentation.Markets!.Stalls.Single(stall => stall.MarketId == fixture.Market.MarketId);
        var draft = new FarmMarketDraft();
        draft.Begin(presentation, stall.MarketId, LocalTradeDirection.Sell);
        Assert.True(draft.SetQuantity(2, stall.MaximumUnitsPerOffer));
        var home = await player.SelectActorAsync(first.Selection, fixture.Source.Grantee);
        var second = await player.SelectActorAsync(home.Selection, fixture.Source.Owner);
        Assert.NotEqual(first.Selection.Generation, second.Selection.Generation);
        var secondPresentation = FarmMarketPresentation.From(second);
        var staleRequest = MarketActionOffers.PrepareRequest(secondPresentation,
            secondPresentation.Markets!.Stalls.Single(value => value.MarketId == stall.MarketId), draft, null);
        Assert.Null(staleRequest.Inquiry);
        Assert.Contains("shopper changed", staleRequest.BlockingReason);
        draft.ObserveScope(secondPresentation);
        Assert.Null(draft.MarketId);
        Assert.Null(draft.LotId);
        Assert.Null(draft.Review);
        Assert.False(draft.IsExplicit);
        Assert.Equal(LocalTradeDirection.Buy, draft.Direction);
        Assert.Equal(1UL, draft.Quantity);
    }

    [Fact]
    public async Task Withheld_trade_is_refused_and_private_history_is_not_inferred_from_trade()
    {
        await using var fixture = await SelectedQueryTradingFixture.Open();
        var player = await fixture.Acquire();
        var observation = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Source.Owner);
        var view = observation.ControlledView!;
        // These are presentation counterexamples only; they issue no authority command or query.
        LocalControlledActorView Disclose(WorldControlCapabilities capabilities, LocalControlledTradeView? trade) => new(
            view.WorldId, view.BranchId, view.ActorId, capabilities, view.Calendar, view.HasReadingHold,
            view.IsResolvingConsequences, view.IsRegional, view.Actors, view.Regional, null, null, null,
            null, null, null, trade, null, null);
        Assert.Throws<InvalidOperationException>(() => FarmMarketPresentation.From(observation with
        { ControlledView = Disclose(WorldControlCapabilities.PrivateHistory, null) }));
        var onlyTrade = FarmMarketPresentation.From(observation with
        { ControlledView = Disclose(WorldControlCapabilities.Trade, view.Trade) });
        Assert.False(onlyTrade.CanReadHistory);
        Assert.Equal(view.Trade!.Quotes, onlyTrade.PaidQuotes);
        Assert.True(FarmMarketPresentation.From(observation).CanReadHistory);
    }

    [Fact]
    public async Task Explicit_sell_disclosure_and_stop_showing_transfer_nothing_and_pin_the_lot()
    {
        await using var world = new NativeMarketTestWorld();
        await world.EarnReward();
        Assert.Equal(LocalTradeInquiryState.Offered, (await world.Ask(LocalTradeDirection.Buy, 2)).State);
        var draft = new FarmMarketDraft();
        var view = FarmMarketPresentation.From(world.View);
        draft.Begin(view, world.Stall.MarketId, LocalTradeDirection.Buy);
        Assert.True(draft.SetQuantity(2, world.StallView.MaximumUnitsPerOffer));
        Assert.True(draft.CaptureReview(view, world.StallView, Assert.Single(view.PaidQuotes)));
        Assert.Equal(WorldActionRejection.None, (await world.Submit(world.Offers(draft)[FarmSemanticAction.ConfirmTrade])).Rejection);
        view = FarmMarketPresentation.From(world.View);
        var stall = world.StallView;
        var lot = Assert.Single(stall.OwnLots);
        draft.SetDirection(LocalTradeDirection.Sell);
        Assert.Null(MarketActionOffers.PrepareRequest(view, stall, draft, null).Inquiry);
        Assert.True(draft.SelectLot(stall, lot.LotId));
        Assert.True(draft.SetQuantity(2, stall.MaximumUnitsPerOffer));
        var show = world.Offers(draft)[FarmSemanticAction.ShowTradeLot];
        var disclosure = Assert.IsType<DiscloseMarketLotIntent>(show.Intent);
        Assert.Equal(stall.MarketId, disclosure.MarketId);
        Assert.Equal(WorldActionRejection.None, (await world.Submit(show)).Rejection);
        Assert.Equal(2UL, world.Berries(world.Player));
        Assert.Equal(lot.LotId, world.StallView.OwnShowing!.LotId);
        var inquiry = MarketActionOffers.PrepareRequest(FarmMarketPresentation.From(world.View), world.StallView, draft, null).Inquiry;
        Assert.NotNull(inquiry);
        Assert.Equal(lot.LotId, inquiry.OwnShownLotId);
        var unshow = world.Offers(draft)[FarmSemanticAction.WithdrawTradeLot];
        Assert.IsType<WithdrawMarketLotDisclosureIntent>(unshow.Intent);
        Assert.Equal(WorldActionRejection.None, (await world.Submit(unshow)).Rejection);
        Assert.Equal(2UL, world.Berries(world.Player));
        Assert.Null(world.StallView.OwnShowing);
    }

    [Fact]
    public async Task Review_remains_immutable_when_personal_revision_changes_and_does_not_recapture()
    {
        await using var world = new NativeMarketTestWorld();
        await world.EarnReward();
        await world.WaitForWrenAtHome();
        Assert.Equal(LocalTradeInquiryState.Offered, (await world.Ask(LocalTradeDirection.Buy, 1)).State);
        var original = world.View;
        var draft = new FarmMarketDraft();
        draft.Begin(original, world.Stall.MarketId, LocalTradeDirection.Buy);
        Assert.True(draft.CaptureReview(original, world.StallView, Assert.Single(original.PaidQuotes)));
        var captured = draft.Review;
        var actor = original.Actors.Single(actor => actor.ActorId == original.PlayerActorId);
        var beforeOffer = world.Offers(draft)[FarmSemanticAction.ConfirmTrade];
        Assert.True(beforeOffer.CanExecute, beforeOffer.BlockingReason);
        world.Session.SetMovement(WorldMovementInput.DisplacementPerStep, 0);
        await world.Tick();
        world.Session.SetMovement(0, 0);
        var changed = world.View;
        var moved = changed.Actors.Single(value => value.ActorId == changed.PlayerActorId);
        Assert.NotEqual(actor.Feet, moved.Feet);
        Assert.NotEqual(actor.PresenceRevision, moved.PresenceRevision);
        Assert.Equal(captured!.Quote.ObservedIssuerRevision,
            Assert.Single(changed.PaidQuotes).ObservedIssuerRevision);
        var confirm = world.Offers(draft)[FarmSemanticAction.ConfirmTrade];
        Assert.Same(captured, draft.Review);
        Assert.False(confirm.CanExecute);
        Assert.Null(confirm.Intent);
        Assert.Equal(actor.PresenceRevision, confirm.Scope.ExpectedActorRevision);
        Assert.Contains("changed since review", confirm.BlockingReason);
    }
}
