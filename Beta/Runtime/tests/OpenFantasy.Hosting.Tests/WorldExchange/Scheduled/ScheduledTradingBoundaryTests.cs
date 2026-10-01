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
/// Trading at the session's other boundaries: actor commands that a reading hold cannot represent are refused
/// without faulting the session, while a nonbinding inquiry may wait over the hold under fixed-world time.
/// </summary>
public sealed class ScheduledTradingBoundaryTests
{
    [Fact]
    public async Task Paid_and_market_commands_during_a_reading_hold_are_refused_and_an_inquiry_waits_over_it()
    {
        using var world = new ScheduledTradingWorld();
        world.Fixture.Gift(world.Authority, world.Wren, world.Trader, world.Fixture.Paid.OrdinaryLot, 1);
        // Everyone stands still beside Wren's worksite, so retained commands pin presence revisions that stay current.
        world.WalkWrenToWork();
        world.WalkBesideWork(world.Trader);
        world.WalkBesideWork(world.Player);
        Assert.Equal(SpatialInteraction.Reachable, world.State.Space.QueryInteraction(world.Player, world.Wren));
        Assert.Equal(SpatialInteraction.Reachable, world.State.Space.QueryInteraction(world.Player, world.Trader));
        MarketBinding wrens = world.Fixture.WrenMarket;
        MarketBinding stall = world.Fixture.TraderMarket;
        var time = new ManualSessionTimeProvider();
        LocalWorldSession session = world.Open(time, ScheduledTradingWorld.Merchants(world.Wren, world.Trader));
        try
        {
            session.SetReady(true);
            LocalTradeInquiryView offered = await ScheduledTradingWorld.Ask(session, time,
                new LocalTradeInquiry(wrens.MarketId, world.Berries, LocalTradeDirection.Buy, 1));
            Assert.Equal(LocalTradeInquiryState.Offered, offered.State);
            PaidQuoteView offer = session.ReadView().PaidQuotes.Single(value => value.QuoteId == offered.QuoteId);

            // The player's own request opens Wren's dialogue through an actual cutoff.
            Assert.Equal(WorldActionRejection.None, (await ScheduledTradingWorld.Submit(session, time, world.Speak(world.Wren))).Rejection);
            WorldSnapshot held = world.State;
            Assert.True(held.HasReadingHold);

            // A paid confirmation and a market reading cannot be selected during the hold: each is refused before it
            // is queued, nothing is published, and the session keeps running.
            WorldActionCommand accept = world.PlayerAcceptance(offer, 1);
            WorldActionCommand observe = world.Service.PrepareObservation(world.DeliveryOf(world.Player), world.Player,
                wrens.MarketId, world.Fixture.NextId())!;
            foreach (WorldActionCommand command in new[] { accept, observe })
            {
                Task<WorldActionReceipt> refused = session.SubmitAction(command);
                await SessionFixture.Tick(time);
                await SessionFixture.Until(() => refused.IsCompleted);
                await Assert.ThrowsAsync<AuthorityDispatchException>(() => refused);
                Assert.False(session.Completion.IsCompleted);
                Assert.Equal(held.ContentHash, world.State.ContentHash);
            }

            // A nonbinding inquiry is a different contract: it may wait over the hold, where no time passes.
            LocalTradeInquiryView waiting = await session.SubmitTradeInquiryAsync(
                new LocalTradeInquiry(stall.MarketId, world.Berries, LocalTradeDirection.Buy, 1));
            Assert.Equal(LocalTradeInquiryState.Waiting, waiting.State);
            for (int tick = 0; tick < 3; tick++)
            {
                await SessionFixture.Tick(time);
            }
            Assert.Equal((held.ContentHash, LocalTradeInquiryState.Waiting, waiting.ExpiresAtStep),
                (world.State.ContentHash, session.ReadTradeInquiry()!.State, session.ReadTradeInquiry()!.ExpiresAtStep));

            // Closing the dialogue publishes no elapsed time; the next ready cutoff answers the waiting inquiry.
            Assert.Equal(WorldActionRejection.None, (await ScheduledTradingWorld.Submit(session, time, world.PlayerCommand(
                new CloseDialogueIntent(held.Dialogues.Single().DialogueId), world.Fixture.NextId()))).Rejection);
            Assert.Equal((held.Calendar, held.AdvancingSteps), (world.State.Calendar, world.State.AdvancingSteps));
            await SessionFixture.Tick(time);
            Assert.Equal(LocalTradeInquiryState.Offered, session.ReadTradeInquiry()!.State);
            Assert.Equal(world.Trader, session.ReadView().PaidQuotes.Single(value =>
                value.QuoteId == session.ReadTradeInquiry()!.QuoteId).IssuerActorId);

            // The refused commands were never published, so the very same requests are lawful at an unheld cutoff.
            Assert.Equal(WorldActionRejection.None, (await ScheduledTradingWorld.Submit(session, time, observe)).Rejection);
            Assert.Equal(WorldActionRejection.None, (await ScheduledTradingWorld.Submit(session, time, accept)).Rejection);
            Assert.Equal(PaidQuoteStatus.Filled, session.ReadView().PaidQuotes.Single(value => value.QuoteId == offer.QuoteId).Status);
        }
        finally
        {
            session.SetReady(false);
            await session.StopAsync();
        }
    }

    /// <summary>
    /// The stall answers the player's inquiry at the same cutoff in which the player's own request opens Wren's
    /// dialogue. The player resolves first in canonical order, so the world refuses the stall's issuance: the inquiry
    /// is declined, no quote exists and the stall's stock is untouched. A later explicit inquiry settles lawfully.
    /// </summary>
    [Fact]
    public async Task A_world_refused_merchant_issuance_declines_the_inquiry_and_a_later_inquiry_settles()
    {
        using var world = new ScheduledTradingWorld();
        world.Fixture.Gift(world.Authority, world.Wren, world.Trader, world.Fixture.Paid.OrdinaryLot, 1);
        Assert.True(world.Player < world.Trader, "The fixture authors the third human after the player in canonical order.");
        MarketBinding stall = world.Fixture.TraderMarket;
        var buy = new LocalTradeInquiry(stall.MarketId, world.Berries, LocalTradeDirection.Buy, 1);
        var time = new ManualSessionTimeProvider();
        LocalWorldSession session = world.Open(time, ScheduledTradingWorld.Merchants(world.Trader));
        try
        {
            session.SetReady(true);
            ulong stock = world.Stock(stall);
            Assert.Equal(LocalTradeInquiryState.Waiting, (await session.SubmitTradeInquiryAsync(buy)).State);
            Assert.Equal(WorldActionRejection.None, (await ScheduledTradingWorld.Submit(session, time, world.Speak(world.Wren))).Rejection);
            WorldSnapshot held = world.State;
            WorldActionCommand issuance = Assert.Single(world.RecordedBatch(held.Revision).Actions, value => value.ActorId == world.Trader);
            Assert.IsType<IssueMarketQuoteIntent>(issuance.Intent);
            Assert.Equal(WorldActionRejection.Unavailable, world.Authority.FindReceipt(issuance, world.DeliveryOf(world.Trader))!.Rejection);
            Assert.Equal((LocalTradeInquiryState.Declined, (EntityId?)null), (session.ReadTradeInquiry()!.State, session.ReadTradeInquiry()!.QuoteId));
            Assert.Empty(held.Coordination!.PaidExchange!.Open);
            Assert.Empty(session.ReadView().PaidQuotes);
            Assert.Equal(stock, world.Stock(stall));

            Assert.Equal(WorldActionRejection.None, (await ScheduledTradingWorld.Submit(session, time, world.PlayerCommand(
                new CloseDialogueIntent(held.Dialogues.Single().DialogueId), world.Fixture.NextId()))).Rejection);
            LocalTradeInquiryView offered = await ScheduledTradingWorld.Ask(session, time, buy);
            Assert.Equal(LocalTradeInquiryState.Offered, offered.State);
            PaidQuoteView ask = session.ReadView().PaidQuotes.Single(value => value.QuoteId == offered.QuoteId);
            Assert.Equal(WorldActionRejection.None, (await ScheduledTradingWorld.Submit(session, time, world.PlayerAcceptance(ask, 1))).Rejection);
            Assert.Equal(ask.UnitPrice, world.Balance(world.Trader));
        }
        finally
        {
            session.SetReady(false);
            await session.StopAsync();
        }
    }

    /// <summary>
    /// With a one-step inquiry lifetime, the player's valid confirmation of an earlier offer reserves Wren at the only
    /// cutoff that could answer a second inquiry. That inquiry expires against the world's next advancing step with no
    /// quote; a fresh inquiry is then answered. A paused inquiry neither progresses nor expires, and stopping discards
    /// it: the reopened session holds none and no merchant ever answers it.
    /// </summary>
    [Fact]
    public async Task An_inquiry_expires_in_world_time_while_its_merchant_is_reserved_and_stop_discards_a_waiting_one()
    {
        using var world = new ScheduledTradingWorld();
        world.WalkWrenToWork();
        world.WalkBesideWork(world.Player);
        MarketBinding market = world.Fixture.WrenMarket;
        var buy = new LocalTradeInquiry(market.MarketId, world.Berries, LocalTradeDirection.Buy, 1);
        var trading = new LocalTradingConfiguration([new LocalMerchantPreference(world.Wren)], inquiryLifetimeSteps: 1);
        var time = new ManualSessionTimeProvider();
        LocalWorldSession session = world.Open(time, trading);
        ContentHash head;
        try
        {
            session.SetReady(true);
            LocalTradeInquiryView first = await ScheduledTradingWorld.Ask(session, time, buy);
            Assert.Equal(LocalTradeInquiryState.Offered, first.State);
            PaidQuoteView offer = session.ReadView().PaidQuotes.Single(value => value.QuoteId == first.QuoteId);

            LocalTradeInquiryView second = await session.SubmitTradeInquiryAsync(buy);
            Assert.Equal(world.State.AdvancingSteps + 1, second.ExpiresAtStep);
            Task<WorldActionReceipt> confirmed = session.SubmitAction(world.PlayerAcceptance(offer, 1));
            await SessionFixture.Tick(time);
            await SessionFixture.Until(() => confirmed.IsCompleted);
            Assert.Equal(WorldActionRejection.None, (await confirmed).Rejection);
            Assert.Equal(LocalTradeInquiryState.Waiting, session.ReadTradeInquiry()!.State);
            await SessionFixture.Tick(time);
            Assert.Equal((LocalTradeInquiryState.Expired, (EntityId?)null), (session.ReadTradeInquiry()!.State, session.ReadTradeInquiry()!.QuoteId));
            Assert.Single(session.ReadView().PaidQuotes);

            Assert.Equal(LocalTradeInquiryState.Offered, (await ScheduledTradingWorld.Ask(session, time, buy)).State);
            Assert.Equal(2, session.ReadView().PaidQuotes.Count);

            // Paused, a new inquiry neither progresses nor expires; stopping then discards it.
            session.SetReady(false);
            LocalTradeInquiryView paused = await session.SubmitTradeInquiryAsync(
                new LocalTradeInquiry(world.Fixture.TraderMarket.MarketId, world.Berries, LocalTradeDirection.Buy, 1));
            ulong revision = world.State.Revision;
            await SessionFixture.Tick(time, TimeSpan.FromSeconds(10));
            Assert.Equal((revision, LocalTradeInquiryState.Waiting, paused.ExpiresAtStep),
                (world.State.Revision, session.ReadTradeInquiry()!.State, session.ReadTradeInquiry()!.ExpiresAtStep));
            head = world.State.ContentHash;
        }
        finally
        {
            session.SetReady(false);
            await session.StopAsync();
        }
        world.Reopen();
        Assert.Equal(head, world.State.ContentHash);
        var reopenedTime = new ManualSessionTimeProvider();
        LocalWorldSession reopened = world.Open(reopenedTime, trading);
        try
        {
            Assert.Null(reopened.ReadTradeInquiry());
            reopened.SetReady(true);
            await SessionFixture.Tick(reopenedTime);
            await SessionFixture.Tick(reopenedTime);
            Assert.DoesNotContain(world.State.Coordination!.PaidExchange!.Open, value => value.Quote.IssuerActorId == world.Trader);
            Assert.Null(reopened.ReadTradeInquiry());
        }
        finally
        {
            reopened.SetReady(false);
            await reopened.StopAsync();
        }
    }

    /// <summary>
    /// The player holds an open offer from Wren when the host shopper actually fills its own offer from her. The fill
    /// supersedes the player's offer: the player's live view and background inspection read it as superseded with no
    /// fill amounts and nothing about the other shopper; the player's confirmation is refused, nothing is requoted,
    /// and the transaction's owners conserve money and goods.
    /// </summary>
    [Fact]
    public async Task An_offer_superseded_by_another_shoppers_fill_reads_superseded_and_its_confirmation_is_refused()
    {
        using var world = new ScheduledTradingWorld();
        world.BuyOneFromWrenDirectly();
        EntityId goods = world.Fixture.Gift(world.Authority, world.Wren, world.Trader, world.Fixture.Paid.OrdinaryLot, 1);
        world.WalkWrenToWork();
        world.WalkBesideWork(world.Trader);
        world.WalkBesideWork(world.Player);
        MarketBinding market = world.Fixture.WrenMarket;
        var trading = new LocalTradingConfiguration([new LocalMerchantPreference(world.Wren)],
            [new LocalShopperPreference(world.Trader, market.MarketId, desiredUnits: 0)]);
        ulong supply = world.State.Progression!.Currency.AdmittedSupply;
        ulong total = world.Balance(world.Wren) + world.Balance(world.Trader) + world.Balance(world.Player);
        var time = new ManualSessionTimeProvider();
        LocalWorldSession session = world.Open(time, trading);
        try
        {
            session.SetReady(true);
            LocalTradeInquiryView offered = await ScheduledTradingWorld.Ask(session, time,
                new LocalTradeInquiry(market.MarketId, world.Berries, LocalTradeDirection.Buy, 1));
            Assert.Equal(LocalTradeInquiryState.Offered, offered.State);
            PaidQuoteView mine = session.ReadView().PaidQuotes.Single(value => value.QuoteId == offered.QuoteId);
            for (int tick = 0; world.State.Coordination!.PaidExchange!.Fills.All(value => value.Quote.Quote.CounterpartyActorId != world.Trader); tick++)
            {
                Assert.True(tick < 6, "The host shopper must show, ask and settle within a few cutoffs.");
                await SessionFixture.Tick(time);
            }
            WorldPaidQuoteOutcome theirs = world.State.Coordination!.PaidExchange!.Fills.Single(value =>
                value.Quote.Quote.CounterpartyActorId == world.Trader);

            PaidQuoteView superseded = session.ReadView().PaidQuotes.Single(value => value.QuoteId == mine.QuoteId);
            Assert.Equal((PaidQuoteStatus.Superseded, (ulong?)null, (ulong?)null, (ulong?)null, (ulong?)null),
                (superseded.Status, superseded.FilledQuantity, superseded.FilledPayment, superseded.FilledFees, superseded.ObservedIssuerRevision));
            Assert.Equal(theirs.WorldSequence, superseded.ClosedAtSequence);
            ScheduledTradingWorld.AssertCarriesNoDelivery(superseded, world.DeliveryOf(world.Wren), world.DeliveryOf(world.Trader));
            Assert.DoesNotContain(typeof(PaidQuoteView).GetProperties().Select(property => property.GetValue(superseded)),
                value => Equals(value, theirs.Settlement!.Acceptance.AcceptanceId) || Equals(value, world.Trader));
            Assert.Equal(PaidQuoteStatus.Superseded, (await session.InspectPaidQuoteAsync(mine.QuoteId))!.Status);
            Assert.DoesNotContain(session.ReadView().PaidQuotes, value => value.QuoteId == theirs.Quote.QuoteId);
            Assert.Null(await session.InspectPaidQuoteAsync(theirs.Quote.QuoteId));
            PaidQuoteView sellerView = Assert.Single(world.Service.ReadQuotes(world.Trader, world.DeliveryOf(world.Trader)));
            Assert.Equal((theirs.Quote.QuoteId, PaidQuoteStatus.Filled), (sellerView.QuoteId, sellerView.Status));

            // A prior confirmation prepared from the open view is refused; nothing moves and nothing is requoted.
            WorldSnapshot before = world.State;
            Assert.Equal(WorldActionRejection.Unavailable,
                (await ScheduledTradingWorld.Submit(session, time, world.PlayerAcceptance(mine, 1))).Rejection);
            Assert.Equal((before.Inventory.ContentHash, before.Production!.ContentHash),
                (world.State.Inventory.ContentHash, world.State.Production!.ContentHash));
            Assert.Equal(before.Coordination!.Markets!.Quotes.Count, world.State.Coordination!.Markets!.Quotes.Count);
            Assert.Equal(supply, world.State.Progression!.Currency.AdmittedSupply);
            Assert.Equal(total, world.Balance(world.Wren) + world.Balance(world.Trader) + world.Balance(world.Player));
            Assert.Equal(world.State.GetResident(world.Wren).InventoryCustodianId, world.State.Inventory.Custody.GetLot(goods).CustodianId);
        }
        finally
        {
            session.SetReady(false);
            await session.StopAsync();
        }
    }
}
