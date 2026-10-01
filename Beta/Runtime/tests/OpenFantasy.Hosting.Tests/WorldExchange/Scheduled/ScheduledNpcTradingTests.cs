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
/// A configured host shopper completes an ordinary trade with a configured host merchant through actual
/// session cutoffs: it shows its own gifted goods in person, asks, and accepts the merchant's funded bid under
/// its own control. The merchant's ordinary work yields only for the answer and the fill, then resumes; the
/// satisfied shopper selects nothing more. Participant history then survives a real archive cut and reopening.
/// </summary>
public sealed class ScheduledNpcTradingTests
{
    [Fact]
    public async Task A_host_shopper_sells_its_own_shown_goods_to_a_working_merchant_and_history_survives_a_cut()
    {
        using var world = new ScheduledTradingWorld();
        // Genuine setup routes: the player's earned purchase funds Wren; Wren's saved gift stocks the shopper.
        (SpotQuote purchase, EntityId _) = world.BuyOneFromWrenDirectly();
        EntityId goods = world.Fixture.Gift(world.Authority, world.Wren, world.Trader, world.Fixture.Paid.OrdinaryLot, 1);
        world.WalkWrenToWork();
        world.WalkBesideWork(world.Trader);
        Assert.Equal(SpatialInteraction.Reachable, world.State.Space.QueryInteraction(world.Trader, world.Wren));
        MarketBinding market = world.Fixture.WrenMarket;
        ulong supply = world.State.Progression!.Currency.AdmittedSupply;
        ulong wrenFunds = world.Balance(world.Wren);
        Assert.Equal(0UL, world.Balance(world.Trader));
        var trading = new LocalTradingConfiguration([new LocalMerchantPreference(world.Wren)],
            [new LocalShopperPreference(world.Trader, market.MarketId, desiredUnits: 0)]);
        var time = new ManualSessionTimeProvider();
        LocalWorldSession session = world.Open(time, trading);
        WorldPaidQuoteOutcome sale;
        try
        {
            session.SetReady(true);
            // First cutoff: the shopper shows its own lot in person; showing spends no merchant opportunity.
            ulong progress = world.Progress;
            await SessionFixture.Tick(time);
            MarketLotDisclosure shown = world.State.Coordination!.Markets!.FindDisclosure(world.Trader, market.MarketId)!;
            Assert.Equal((goods, 1UL), (shown.LotId, shown.Quantity));
            Assert.True(world.Progress > progress, "The merchant keeps working while a shopper only shows goods.");

            // Second cutoff: the shopper asks and the merchant answers from that showing and its own funds.
            ulong stock = world.Stock(market);
            progress = world.Progress;
            await SessionFixture.Tick(time);
            WorldPaidQuote bid = Assert.Single(world.State.Coordination!.PaidExchange!.Open,
                value => value.Quote.CounterpartyActorId == world.Trader);
            Assert.Equal((world.Wren, SpotQuoteSide.IssuerBuys, goods, market.Policy.Bid(stock, 1)!.Value),
                (bid.Quote.IssuerActorId, bid.Quote.Side, bid.Quote.LotId, bid.Quote.UnitPrice));
            Assert.Equal(progress, world.Progress);

            // Third cutoff: the shopper's own acceptance settles; neither party works or walks in that step.
            WorldSnapshot before = world.State;
            await SessionFixture.Tick(time);
            sale = world.State.Coordination!.PaidExchange!.FindOutcome(bid.QuoteId)!;
            Assert.Equal(WorldPaidQuoteOutcomeKind.Filled, sale.Kind);
            Assert.Equal(progress, world.Progress);
            WorldStepBatch fill = world.RecordedBatch(sale.WorldSequence);
            WorldActionCommand acceptance = Assert.Single(fill.Actions);
            Assert.Equal((world.Trader, sale.Settlement!.Acceptance.AcceptanceId), (acceptance.ActorId, acceptance.CommandId));
            Assert.DoesNotContain(fill.Movement, input => input.ActorId == world.Wren || input.ActorId == world.Trader);
            Assert.Equal(before.Space.GetActor(world.Trader).Feet, world.State.Space.GetActor(world.Trader).Feet);
            Assert.Equal(bid.Quote.UnitPrice, world.Balance(world.Trader));
            Assert.Equal(wrenFunds - bid.Quote.UnitPrice, world.Balance(world.Wren));
            Assert.Equal(supply, world.State.Progression!.Currency.AdmittedSupply);
            Assert.Equal(world.State.GetResident(world.Wren).InventoryCustodianId,
                world.State.Inventory.Custody.GetLot(goods).CustodianId);

            // Satisfied, the shopper selects nothing more; the merchant returns to its useful work.
            ContentHash paid = world.State.Coordination!.PaidExchange!.ContentHash;
            ContentHash markets = world.State.Coordination!.Markets!.ContentHash;
            progress = world.Progress;
            for (int tick = 0; tick < 3; tick++)
            {
                await SessionFixture.Tick(time);
            }
            Assert.Equal((paid, markets), (world.State.Coordination!.PaidExchange!.ContentHash,
                world.State.Coordination!.Markets!.ContentHash));
            Assert.True(world.Progress > progress, "The merchant resumes ordinary work after trading.");

            // The player is not a participant: the NPC trade is invisible to its inspection.
            Assert.Null(await session.InspectPaidQuoteAsync(bid.QuoteId));
            Assert.DoesNotContain(session.ReadView().PaidQuotes, value => value.QuoteId == bid.QuoteId);
        }
        finally
        {
            session.SetReady(false);
            await session.StopAsync();
        }

        // Real archive pressure moves both closed quotes out of the live segment.
        world.Reopen();
        world.CutArchive();
        Assert.Null(world.State.Coordination!.PaidExchange!.FindOutcome(purchase.QuoteId));
        ContentHash head = world.State.ContentHash;
        world.Reopen();
        Assert.Equal(head, world.State.ContentHash);
        var reopenedTime = new ManualSessionTimeProvider();
        LocalWorldSession reopened = world.Open(reopenedTime, null);
        try
        {
            // Restart keeps no inquiry; authenticated replay still reaches the player's own archived outcome.
            Assert.Null(reopened.ReadTradeInquiry());
            Assert.DoesNotContain(reopened.ReadView().PaidQuotes, value => value.QuoteId == purchase.QuoteId);
            PaidQuoteView archived = (await reopened.InspectPaidQuoteAsync(purchase.QuoteId))!;
            Assert.Equal((PaidQuoteStatus.Filled, purchase.ContentHash, (ulong?)1, (ulong?)purchase.UnitPrice, (ulong?)null),
                (archived.Status, archived.QuoteHash, archived.FilledQuantity, archived.FilledPayment, archived.ObservedIssuerRevision));
            ScheduledTradingWorld.AssertCarriesNoDelivery(archived, world.DeliveryOf(world.Wren));
            Assert.Throws<InvalidOperationException>(() => archived.PrepareAcceptance(world.Fixture.NextId(),
                world.DeliveryOf(world.Player), 1));
            Assert.Null(await reopened.InspectPaidQuoteAsync(sale.Quote.QuoteId));
            Assert.Null(await reopened.InspectPaidQuoteAsync(new EntityId(0x4D4B_0BAD, 9)));
            Assert.Equal(head, world.State.ContentHash);
        }
        finally { await reopened.StopAsync(); }
        // The other participants read the NPC trade through the same authenticated archive replay.
        world.Reopen();
        PaidQuoteView merchantView = world.Service.FindRecordedQuote(sale.Quote.QuoteId, world.Wren, world.DeliveryOf(world.Wren))!;
        Assert.Equal((PaidQuoteStatus.Filled, true), (merchantView.Status, merchantView.ViewerIsIssuer));
        ScheduledTradingWorld.AssertCarriesNoDelivery(merchantView, world.DeliveryOf(world.Trader));
        Assert.Throws<AuthorityDispatchException>(() => world.Service.FindRecordedQuote(sale.Quote.QuoteId, world.Wren,
            world.DeliveryOf(world.Trader)));
    }

    /// <summary>
    /// The player and a host shopper both ask one merchant. Answers follow admission order; while the
    /// merchant's opportunity is reserved by the player's valid acceptance the shopper's inquiry keeps its
    /// place, and both bids are paid from the merchant's finite earned funds.
    /// </summary>
    [Fact]
    public async Task Inquiries_to_one_merchant_are_answered_in_admission_order_and_a_busy_merchant_keeps_their_place()
    {
        using var world = new ScheduledTradingWorld();
        // Genuine setup: the player buys two units (funding Wren) and gives one to the third human.
        (SpotQuote _, EntityId bought) = world.BuyFromWrenDirectly(2);
        world.Fixture.Gift(world.Authority, world.Player, world.Trader, bought, 1);
        world.WalkWrenToWork();
        world.WalkBesideWork(world.Trader);
        world.WalkBesideWork(world.Player);
        MarketBinding market = world.Fixture.WrenMarket;
        TrustedCommandDelivery player = world.DeliveryOf(world.Player);
        WorldActionCommand show = world.Service.PrepareDisclosure(player, world.Player, market.MarketId, bought, 1, 64,
            world.Fixture.NextId())!;
        Assert.Equal(WorldActionRejection.None, world.Service.Submit(show, player, world.Fixture.NextId()).Rejection);
        ulong funds = world.Balance(world.Wren);
        var trading = new LocalTradingConfiguration([new LocalMerchantPreference(world.Wren)],
            [new LocalShopperPreference(world.Trader, market.MarketId, desiredUnits: 0)]);
        var time = new ManualSessionTimeProvider();
        LocalWorldSession session = world.Open(time, trading);
        try
        {
            session.SetReady(true);
            await SessionFixture.Tick(time);
            Assert.NotNull(world.State.Coordination!.Markets!.FindDisclosure(world.Trader, market.MarketId));

            // The player's inquiry is admitted before the shopper's, which the next cutoff creates.
            Assert.Equal(LocalTradeInquiryState.Waiting, (await session.SubmitTradeInquiryAsync(
                new LocalTradeInquiry(market.MarketId, world.Berries, LocalTradeDirection.Sell, 1, bought))).State);
            await SessionFixture.Tick(time);
            LocalTradeInquiryView first = session.ReadTradeInquiry()!;
            Assert.Equal(LocalTradeInquiryState.Offered, first.State);
            Assert.DoesNotContain(world.State.Coordination!.PaidExchange!.Open,
                value => value.Quote.CounterpartyActorId == world.Trader);

            // The player's valid acceptance reserves the merchant: the shopper's answer waits, in place.
            PaidQuoteView offer = session.ReadView().PaidQuotes.Single(value => value.QuoteId == first.QuoteId);
            Assert.Equal(WorldActionRejection.None,
                (await ScheduledTradingWorld.Submit(session, time, world.PlayerAcceptance(offer, 1))).Rejection);
            Assert.DoesNotContain(world.State.Coordination!.PaidExchange!.Open,
                value => value.Quote.CounterpartyActorId == world.Trader);

            // At the next free cutoff the merchant answers the shopper, then the shopper settles.
            ulong progress = world.Progress;
            await SessionFixture.Tick(time);
            WorldPaidQuote second = Assert.Single(world.State.Coordination!.PaidExchange!.Open,
                value => value.Quote.CounterpartyActorId == world.Trader);
            Assert.Equal(progress, world.Progress);
            await SessionFixture.Tick(time);
            Assert.Equal(WorldPaidQuoteOutcomeKind.Filled,
                world.State.Coordination!.PaidExchange!.FindOutcome(second.QuoteId)!.Kind);
            Assert.Equal(funds - offer.UnitPrice - second.Quote.UnitPrice, world.Balance(world.Wren));
            Assert.Equal(second.Quote.UnitPrice, world.Balance(world.Trader));
            await SessionFixture.Tick(time);
            Assert.True(world.Progress > progress, "With both inquiries answered the merchant works again.");
        }
        finally
        {
            session.SetReady(false);
            await session.StopAsync();
        }
    }
}
