using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;

namespace OpenFantasy.Hosting.Tests.WorldExchange.Scheduled;

/// <summary>
/// Scheduled trading through actual <see cref="LocalWorldSession"/> cutoffs and SQLite authority: authenticated
/// player inquiries answered by configured host merchants under their own control, participant-safe quote
/// inspection, separate confirmation, work and movement exclusion during a fill, refusals with lawful
/// contrasts and exact receipts after reopening.
/// </summary>
public sealed class ScheduledTradingSessionTests
{
    [Fact]
    public async Task A_working_merchant_answers_player_inquiries_in_each_direction_and_only_confirmation_settles()
    {
        using var world = new ScheduledTradingWorld();
        world.WalkWrenToWork();
        world.WalkBesideWork(world.Player);
        Assert.Equal(SpatialInteraction.Reachable, world.State.Space.QueryInteraction(world.Player, world.Wren));
        MarketBinding market = world.Fixture.WrenMarket;
        var time = new ManualSessionTimeProvider();
        LocalWorldSession session = world.Open(time, ScheduledTradingWorld.Merchants(world.Wren));
        WorldActionCommand accept;
        WorldActionReceipt bought;
        PaidQuoteView offer;
        ContentHash head;
        try
        {
            session.SetReady(true);
            ulong progress = world.Progress;
            await SessionFixture.Tick(time);
            Assert.True(world.Progress > progress, "Without an inquiry the merchant does her ordinary funded work.");

            // The inquiry names only the market, commodity, direction and quantity; the merchant chooses its own lot.
            ulong stock = world.Stock(market);
            ulong playerFunds = world.Balance(world.Player);
            progress = world.Progress;
            LocalTradeInquiryView answered = await ScheduledTradingWorld.Ask(session, time,
                new LocalTradeInquiry(market.MarketId, world.Berries, LocalTradeDirection.Buy, 1));
            Assert.Equal(LocalTradeInquiryState.Offered, answered.State);
            Assert.Equal(progress, world.Progress);
            offer = Assert.Single(session.ReadView().PaidQuotes);
            Assert.Equal((answered.QuoteId!.Value, PaidQuoteStatus.Open, SpotQuoteSide.IssuerSells, world.Wren, false),
                (offer.QuoteId, offer.Status, offer.Side, offer.IssuerActorId, offer.ViewerIsIssuer));
            Assert.Equal(market.Policy.Ask(stock, 1), offer.UnitPrice);
            Assert.Equal(world.State.Space.GetActor(world.Wren).Revision, offer.ObservedIssuerRevision);
            ScheduledTradingWorld.AssertCarriesNoDelivery(offer, world.DeliveryOf(world.Wren));
            Assert.Equal(offer.QuoteHash, (await session.InspectPaidQuoteAsync(offer.QuoteId))!.QuoteHash);

            // An offer is only an offer: cutoffs never accept it, and the merchant returns to useful work.
            progress = world.Progress;
            for (int tick = 0; tick < 3; tick++)
            {
                await SessionFixture.Tick(time);
            }
            Assert.Equal(playerFunds, world.Balance(world.Player));
            Assert.Equal(PaidQuoteStatus.Open, Assert.Single(session.ReadView().PaidQuotes).Status);
            Assert.True(world.Progress > progress, "After answering, the merchant works again.");

            // The player's separate confirmation settles exactly the inspected terms. The issuer's opportunity is
            // reserved: no work, answer or walk of either participant enters the settling step.
            offer = Assert.Single(session.ReadView().PaidQuotes);
            WorldSnapshot before = world.State;
            progress = world.Progress;
            accept = world.PlayerAcceptance(offer, 1);
            bought = await ScheduledTradingWorld.Submit(session, time, accept);
            Assert.Equal(WorldActionRejection.None, bought.Rejection);
            WorldSnapshot after = world.State;
            Assert.Equal(progress, world.Progress);
            WorldStepBatch fill = world.RecordedBatch(after.Revision);
            Assert.Equal(accept.CommandId, Assert.Single(fill.Actions).CommandId);
            Assert.DoesNotContain(fill.Movement, input => input.ActorId == world.Wren || input.ActorId == world.Player);
            Assert.Equal(before.Space.GetActor(world.Wren).Feet, after.Space.GetActor(world.Wren).Feet);
            Assert.Equal(playerFunds - offer.UnitPrice, world.Balance(world.Player));
            Assert.Equal(before.Progression!.Currency.AdmittedSupply, after.Progression!.Currency.AdmittedSupply);
            PaidQuoteView filled = Assert.Single(session.ReadView().PaidQuotes);
            Assert.Equal((PaidQuoteStatus.Filled, (ulong?)1, (ulong?)offer.UnitPrice),
                (filled.Status, filled.FilledQuantity, filled.FilledPayment));
            ScheduledTradingWorld.AssertCarriesNoDelivery(filled, world.DeliveryOf(world.Wren));

            // Selling: an unshown own lot is invisible to the merchant, so it offers nothing.
            EntityId own = world.OwnedLotFrom(offer.LotId, accept.CommandId, world.Player);
            // The answer was the merchant's own recorded command in the step that issued the offer.
            Assert.Contains(world.RecordedBatch(IssuanceSequence(world, offer.QuoteId)).Actions,
                action => action.ActorId == world.Wren && action.Intent is IssueMarketQuoteIntent);
            var sell = new LocalTradeInquiry(market.MarketId, world.Berries, LocalTradeDirection.Sell, 1, own);
            Assert.Equal(LocalTradeInquiryState.Declined, (await ScheduledTradingWorld.Ask(session, time, sell)).State);
            TrustedCommandDelivery player = world.DeliveryOf(world.Player);
            WorldActionCommand show = world.Service.PrepareDisclosure(player, world.Player, market.MarketId, own, 1, 32,
                world.Fixture.NextId())!;
            Assert.Equal(WorldActionRejection.None, (await ScheduledTradingWorld.Submit(session, time, show)).Rejection);

            // Once shown, the merchant uses that showing and its own earned funds to bid.
            stock = world.Stock(market);
            ulong wrenFunds = world.Balance(world.Wren);
            playerFunds = world.Balance(world.Player);
            answered = await ScheduledTradingWorld.Ask(session, time, sell);
            Assert.Equal(LocalTradeInquiryState.Offered, answered.State);
            PaidQuoteView bid = session.ReadView().PaidQuotes.Single(value => value.QuoteId == answered.QuoteId);
            Assert.Equal((SpotQuoteSide.IssuerBuys, market.Policy.Bid(stock, 1)!.Value, own),
                (bid.Side, bid.UnitPrice, bid.LotId));
            Assert.Equal(WorldActionRejection.None,
                (await ScheduledTradingWorld.Submit(session, time, world.PlayerAcceptance(bid, 1))).Rejection);
            Assert.Equal(playerFunds + bid.UnitPrice, world.Balance(world.Player));
            Assert.Equal(wrenFunds - bid.UnitPrice, world.Balance(world.Wren));
            Assert.Equal(stock + 1, world.Stock(market));
            head = world.State.ContentHash;
        }
        finally
        {
            session.SetReady(false);
            await session.StopAsync();
        }

        // Reopening reproduces the head; an exact retry returns the original receipt without a new step.
        world.Reopen();
        Assert.Equal(head, world.State.ContentHash);
        var reopenedTime = new ManualSessionTimeProvider();
        LocalWorldSession reopened = world.Open(reopenedTime, null);
        try
        {
            Assert.Null(reopened.ReadTradeInquiry());
            Assert.Equal(WorldReceiptCodecV1.Encode(bought), WorldReceiptCodecV1.Encode(await reopened.SubmitAction(accept)));
            PaidQuoteView history = (await reopened.InspectPaidQuoteAsync(offer.QuoteId))!;
            Assert.Equal((PaidQuoteStatus.Filled, offer.QuoteHash), (history.Status, history.QuoteHash));
            Assert.Equal(head, world.State.ContentHash);
        }
        finally { await reopened.StopAsync(); }
    }

    [Fact]
    public async Task An_in_person_stall_answers_only_what_it_can_see_and_fund_and_never_requotes_a_stale_offer()
    {
        using var world = new ScheduledTradingWorld();
        (SpotQuote _, EntityId own) = world.BuyOneFromWrenDirectly();
        // A quote between two other actors, which the player must not be able to inspect.
        TrustedCommandDelivery wren = world.DeliveryOf(world.Wren);
        WorldActionCommand foreign = world.Service.PrepareOffer(wren, world.Fixture.WrenMarket.MarketId, world.Trader,
            SpotQuoteSide.IssuerSells, world.Fixture.Paid.OrdinaryLot, 1, 32, world.Fixture.NextId())!;
        Assert.Equal(WorldActionRejection.None, world.Service.Submit(foreign, wren, world.Fixture.NextId()).Rejection);
        EntityId foreignQuote = ((IssueMarketQuoteIntent)foreign.Intent).Quote.QuoteId;
        EntityId stocked = world.Fixture.Gift(world.Authority, world.Wren, world.Trader, world.Fixture.Paid.OrdinaryLot, 1);
        world.WalkWrenToWork();
        Assert.NotEqual(SpatialInteraction.Reachable, world.State.Space.QueryInteraction(world.Player, world.Wren));
        MarketBinding stall = world.Fixture.TraderMarket;
        Assert.Equal(0UL, world.Balance(world.Trader));
        var time = new ManualSessionTimeProvider();
        LocalWorldSession session = world.Open(time, new LocalTradingConfiguration(
            [new LocalMerchantPreference(world.Wren), new LocalMerchantPreference(world.Trader, offerLifetimeSteps: 6)]));
        try
        {
            // Paused: nothing is answered and no time passes; cancellation then removes the inquiry for good.
            var buy = new LocalTradeInquiry(stall.MarketId, world.Berries, LocalTradeDirection.Buy, 1);
            ulong revision = world.State.Revision;
            Assert.Equal(LocalTradeInquiryState.Waiting, (await session.SubmitTradeInquiryAsync(buy)).State);
            Assert.Equal(LocalTradeInquiryState.Waiting, (await session.SubmitTradeInquiryAsync(buy)).State);
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => session.SubmitTradeInquiryAsync(
                new LocalTradeInquiry(stall.MarketId, world.Berries, LocalTradeDirection.Buy, 2)));
            await SessionFixture.Tick(time);
            Assert.Equal((revision, LocalTradeInquiryState.Waiting), (world.State.Revision, session.ReadTradeInquiry()!.State));
            Assert.Equal(LocalTradeInquiryState.Cancelled, (await session.CancelTradeInquiryAsync())!.State);
            session.SetReady(true);
            await SessionFixture.Tick(time);
            Assert.Equal(revision + 1, world.State.Revision);
            Assert.DoesNotContain(session.ReadView().PaidQuotes, value => value.IssuerActorId == world.Trader);
            Assert.Equal(LocalTradeInquiryState.Cancelled, session.ReadTradeInquiry()!.State);

            // A distant real market and a guessed identity give the same answer and reveal nothing.
            LocalTradeInquiryView distant = await ScheduledTradingWorld.Ask(session, time,
                new LocalTradeInquiry(world.Fixture.WrenMarket.MarketId, world.Berries, LocalTradeDirection.Buy, 1));
            LocalTradeInquiryView guessed = await ScheduledTradingWorld.Ask(session, time,
                new LocalTradeInquiry(new EntityId(0x4D4B_0BAD, 7), world.Berries, LocalTradeDirection.Buy, 1));
            Assert.Equal((LocalTradeInquiryState.Unavailable, (EntityId?)null), (distant.State, distant.QuoteId));
            Assert.Equal((distant.State, distant.QuoteId), (guessed.State, guessed.QuoteId));

            // Unknown and foreign quotes are indistinguishable to the player.
            Assert.Null(await session.InspectPaidQuoteAsync(foreignQuote));
            Assert.Null(await session.InspectPaidQuoteAsync(new EntityId(0x4D4B_0BAD, 8)));
            Assert.Throws<AuthorityDispatchException>(() => world.Service.ReadQuotes(world.Player, world.DeliveryOf(world.Trader)));

            // A sell inquiry naming another actor's lot, or an unshown lot, gets no offer; showing one's own lot to
            // an unfunded stall still yields no bid: the merchant never buys on credit or from spawned money.
            Assert.Equal(LocalTradeInquiryState.Declined, (await ScheduledTradingWorld.Ask(session, time,
                new LocalTradeInquiry(stall.MarketId, world.Berries, LocalTradeDirection.Sell, 1, stocked))).State);
            TrustedCommandDelivery player = world.DeliveryOf(world.Player);
            WorldActionCommand show = world.Service.PrepareDisclosure(player, world.Player, stall.MarketId, own, 1, 32,
                world.Fixture.NextId())!;
            Assert.Equal(WorldActionRejection.None, (await ScheduledTradingWorld.Submit(session, time, show)).Rejection);
            var sell = new LocalTradeInquiry(stall.MarketId, world.Berries, LocalTradeDirection.Sell, 1, own);
            Assert.Equal(LocalTradeInquiryState.Declined, (await ScheduledTradingWorld.Ask(session, time, sell)).State);
            Assert.DoesNotContain(session.ReadView().PaidQuotes, value => value.IssuerActorId == world.Trader);

            // In person the stall sells its gifted unit; that sale is its only funding.
            LocalTradeInquiryView sold = await ScheduledTradingWorld.Ask(session, time, buy);
            Assert.Equal(LocalTradeInquiryState.Offered, sold.State);
            PaidQuoteView ask = session.ReadView().PaidQuotes.Single(value => value.QuoteId == sold.QuoteId);
            Assert.NotNull(ask.ObservedIssuerRevision);
            WorldActionCommand purchase = world.PlayerAcceptance(ask, 1);
            Assert.Equal(WorldActionRejection.None, (await ScheduledTradingWorld.Submit(session, time, purchase)).Rejection);
            Assert.Equal(ask.UnitPrice, world.Balance(world.Trader));
            EntityId second = world.OwnedLotFrom(ask.LotId, purchase.CommandId, world.Player);

            // Now funded, the stall bids on the shown lot. A real arrival then makes that fixed offer stale.
            LocalTradeInquiryView offered = await ScheduledTradingWorld.Ask(session, time, sell);
            Assert.Equal(LocalTradeInquiryState.Offered, offered.State);
            PaidQuoteView stale = session.ReadView().PaidQuotes.Single(value => value.QuoteId == offered.QuoteId);
            world.Fixture.Gift(world.Authority, world.Player, world.Trader, second, 1);
            WorldSnapshot before = world.State;
            // Only the market basis changed: the pinned lot and the issuer presence the player observed are exact.
            Assert.Equal(stale.ObservedIssuerRevision, before.Space.GetActor(world.Trader).Revision);
            Assert.Equal(stale.ExpectedLotRevision, before.Inventory.Custody.GetLot(own).Revision);
            WorldActionReceipt refused = await ScheduledTradingWorld.Submit(session, time, world.PlayerAcceptance(stale, 1));
            Assert.Equal(WorldActionRejection.RevisionConflict, refused.Rejection);
            WorldSnapshot after = world.State;
            Assert.Equal((before.Inventory.ContentHash, before.Progression!.ContentHash, before.Coordination!.Markets!.ContentHash),
                (after.Inventory.ContentHash, after.Progression!.ContentHash, after.Coordination!.Markets!.ContentHash));

            // Nothing is requoted: the inquiry keeps its one answer and the stale offer stands until it expires.
            Assert.Equal(LocalTradeInquiryState.Declined, (await ScheduledTradingWorld.Ask(session, time, sell)).State);
            for (int tick = 0; session.ReadView().PaidQuotes.Single(value => value.QuoteId == stale.QuoteId).Status
                == PaidQuoteStatus.Open; tick++)
            {
                Assert.True(tick < 8, "The stall's bounded offer lifetime must close the stale quote.");
                await SessionFixture.Tick(time);
            }
            Assert.Single(session.ReadView().PaidQuotes, value => value.IssuerActorId == world.Trader
                && value.Side == SpotQuoteSide.IssuerBuys);

            // After expiry a new explicit inquiry is priced at the changed stock and settles lawfully.
            ulong stock = world.Stock(stall);
            LocalTradeInquiryView renewed = await ScheduledTradingWorld.Ask(session, time, sell);
            Assert.Equal(LocalTradeInquiryState.Offered, renewed.State);
            PaidQuoteView fresh = session.ReadView().PaidQuotes.Single(value => value.QuoteId == renewed.QuoteId);
            Assert.Equal(stall.Policy.Bid(stock, 1), fresh.UnitPrice);
            Assert.Equal(WorldActionRejection.None,
                (await ScheduledTradingWorld.Submit(session, time, world.PlayerAcceptance(fresh, 1))).Rejection);
            Assert.Equal(ask.UnitPrice - fresh.UnitPrice, world.Balance(world.Trader));
        }
        finally
        {
            session.SetReady(false);
            await session.StopAsync();
        }
    }

    /// <summary>The world revision that recorded a quote's issuance, found through the live paid book.</summary>
    private static ulong IssuanceSequence(ScheduledTradingWorld world, EntityId quoteId)
    {
        WorldPaidExchangeBook book = world.State.Coordination!.PaidExchange!;
        return (book.FindOpen(quoteId) ?? book.FindOutcome(quoteId)!.Quote).CreatedSequence;
    }
}
