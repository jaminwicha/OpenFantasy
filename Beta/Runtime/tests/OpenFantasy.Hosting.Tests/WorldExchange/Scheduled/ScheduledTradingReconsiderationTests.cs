using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.WorldExchange.Markets;

namespace OpenFantasy.Hosting.Tests.WorldExchange.Scheduled;

/// <summary>
/// A configured host shopper keeps a bounded, deliberate desire instead of being silenced by one unfavorable
/// answer: it trades only in person, resumes when circumstances actually become favorable and never floods the
/// world with commands or retries a refused decision under a new identity.
/// </summary>
public sealed class ScheduledTradingReconsiderationTests
{
    /// <summary>
    /// The third human, funded only by a real sale at its own stall, waits beside Wren's worksite while Wren is still
    /// at the farm. It asks nothing while she is out of reach; once her own construction route actually brings her
    /// within reach it buys one unit with its own funds under its own control, and she then works again.
    /// </summary>
    [Fact]
    public async Task A_remote_host_buyer_asks_nothing_until_its_merchant_actually_arrives_then_buys()
    {
        using var world = new ScheduledTradingWorld();
        EntityId stocked = world.Fixture.Gift(world.Authority, world.Wren, world.Trader, world.Fixture.Paid.OrdinaryLot, 1);
        SpotQuote sale = world.BuyFromTraderDirectly(stocked, 1);
        ulong funds = world.Balance(world.Trader);
        Assert.Equal(sale.UnitPrice, funds);
        Assert.Equal(0UL, LocalMarketUnits(world, world.Trader));
        world.WalkBesideWork(world.Trader, awayFrom: world.State.Space.GetActor(world.Wren).Feet);
        Assert.NotEqual(SpatialInteraction.Reachable, world.State.Space.QueryInteraction(world.Trader, world.Wren));
        MarketBinding market = world.Fixture.WrenMarket;
        ulong supply = world.State.Progression!.Currency.AdmittedSupply;
        var trading = new LocalTradingConfiguration([new LocalMerchantPreference(world.Wren)],
            [new LocalShopperPreference(world.Trader, market.MarketId, desiredUnits: 1)]);
        var time = new ManualSessionTimeProvider();
        LocalWorldSession session = world.Open(time, trading);
        try
        {
            session.SetReady(true);
            ulong start = world.State.Revision;
            ulong? arrived = null;
            for (int tick = 0; world.State.Coordination!.PaidExchange!.Fills.All(value => value.Quote.Quote.CounterpartyActorId != world.Trader); tick++)
            {
                Assert.True(tick < 256, "Wren's own construction route must bring her within reach, and then the buyer must trade.");
                WorldSnapshot cutoff = world.State;
                if (arrived is null && cutoff.Space.QueryInteraction(world.Trader, world.Wren) == SpatialInteraction.Reachable
                    && cutoff.Space.QueryInteraction(world.Wren, world.Trader) == SpatialInteraction.Reachable)
                {
                    arrived = cutoff.Revision;
                }
                if (arrived is null)
                {
                    Assert.DoesNotContain(cutoff.Coordination!.PaidExchange!.Open, value => value.Quote.CounterpartyActorId == world.Trader);
                }
                await SessionFixture.Tick(time);
            }
            // While she was out of reach, nothing at all was asked of her: no actor command was selected.
            Assert.True(arrived > start, "The buyer started out of the merchant's reach.");
            Assert.All(world.RecordedBatches(start + 1, arrived!.Value), batch => Assert.Empty(batch.Actions));
            WorldPaidQuoteOutcome fill = world.State.Coordination!.PaidExchange!.Fills.Single(value =>
                value.Quote.Quote.CounterpartyActorId == world.Trader);
            SpotQuote bought = fill.Quote.Quote;
            Assert.Equal((world.Wren, SpotQuoteSide.IssuerSells, 1UL), (bought.IssuerActorId, bought.Side, fill.Settlement!.Acceptance.Quantity));
            WorldStepBatch settled = world.RecordedBatch(fill.WorldSequence);
            Assert.Equal((world.Trader, fill.Settlement.Acceptance.AcceptanceId), (Assert.Single(settled.Actions).ActorId,
                Assert.Single(settled.Actions).CommandId));
            Assert.DoesNotContain(settled.Movement, input => input.ActorId == world.Wren || input.ActorId == world.Trader);
            Assert.Equal(funds - bought.UnitPrice, world.Balance(world.Trader));
            Assert.Equal(1UL, LocalMarketUnits(world, world.Trader));
            Assert.Equal(supply, world.State.Progression!.Currency.AdmittedSupply);
            // Wren's own issuance answered the first cutoff at which both actually stood within reach.
            Assert.Equal(arrived + 1, fill.Quote.CreatedSequence);

            // Satisfied, the shopper selects nothing more; Wren completes her approach and returns to her funded work.
            ulong progress = world.Progress;
            ContentHash paid = world.State.Coordination!.PaidExchange!.ContentHash;
            for (int tick = 0; world.Progress == progress; tick++)
            {
                Assert.True(tick < 32, "After trading, the merchant reaches her worksite and works again.");
                await SessionFixture.Tick(time);
            }
            Assert.Equal(paid, world.State.Coordination!.PaidExchange!.ContentHash);
        }
        finally
        {
            session.SetReady(false);
            await session.StopAsync();
        }
    }

    /// <summary>
    /// The shopper shows its gifted unit to unfunded Wren, who cannot bid. Its unchanged desire then asks nothing more
    /// until its bounded interval has passed; meanwhile the player's in-person purchase funds Wren through an actual
    /// cutoff, and the reconsidered inquiry is answered with a funded bid that the shopper accepts.
    /// </summary>
    [Fact]
    public async Task A_declined_host_seller_reconsiders_after_its_interval_and_sells_once_the_merchant_is_funded()
    {
        using var world = new ScheduledTradingWorld();
        EntityId goods = world.Fixture.Gift(world.Authority, world.Wren, world.Trader, world.Fixture.Paid.OrdinaryLot, 1);
        world.WalkWrenToWork();
        world.WalkBesideWork(world.Trader);
        world.WalkBesideWork(world.Player);
        Assert.Equal((0UL, 0UL), (world.Balance(world.Wren), world.Balance(world.Trader)));
        MarketBinding market = world.Fixture.WrenMarket;
        const ulong interval = 6;
        var trading = new LocalTradingConfiguration([new LocalMerchantPreference(world.Wren)],
            [new LocalShopperPreference(world.Trader, market.MarketId, desiredUnits: 0, reconsiderAfterSteps: interval)]);
        ulong supply = world.State.Progression!.Currency.AdmittedSupply;
        var time = new ManualSessionTimeProvider();
        LocalWorldSession session = world.Open(time, trading);
        try
        {
            session.SetReady(true);
            ulong start = world.State.Revision;
            await SessionFixture.Tick(time);
            MarketLotDisclosure shown = world.State.Coordination!.Markets!.FindDisclosure(world.Trader, market.MarketId)!;
            Assert.Equal(goods, shown.LotId);
            await SessionFixture.Tick(time);
            Assert.DoesNotContain(world.State.Coordination!.PaidExchange!.Open, value => value.Quote.CounterpartyActorId == world.Trader);

            // The player's own inquiry and confirmation fund Wren; the shopper's unchanged desire stays quiet meanwhile.
            LocalTradeInquiryView bought = await ScheduledTradingWorld.Ask(session, time,
                new LocalTradeInquiry(market.MarketId, world.Berries, LocalTradeDirection.Buy, 1));
            Assert.Equal(LocalTradeInquiryState.Offered, bought.State);
            PaidQuoteView ask = session.ReadView().PaidQuotes.Single(value => value.QuoteId == bought.QuoteId);
            Assert.Equal(WorldActionRejection.None, (await ScheduledTradingWorld.Submit(session, time, world.PlayerAcceptance(ask, 1))).Rejection);
            Assert.Equal(ask.UnitPrice, world.Balance(world.Wren));

            for (int tick = 0; world.State.Coordination!.PaidExchange!.Fills.All(value => value.Quote.Quote.CounterpartyActorId != world.Trader); tick++)
            {
                Assert.True(tick < (int)interval + 4, "The reconsidered desire must trade within its bounded interval.");
                await SessionFixture.Tick(time);
            }
            WorldPaidQuoteOutcome sale = world.State.Coordination!.PaidExchange!.Fills.Single(value =>
                value.Quote.Quote.CounterpartyActorId == world.Trader);
            // The reconsidered attempt began exactly one interval after the first one, which began with the showing.
            Assert.Equal(shown.DisclosedAtStep + interval, sale.Quote.Quote.IssuedAtStep);
            Assert.Equal((SpotQuoteSide.IssuerBuys, goods), (sale.Quote.Quote.Side, sale.Quote.Quote.LotId));
            Assert.Equal(sale.Quote.Quote.UnitPrice, world.Balance(world.Trader));
            Assert.Equal(ask.UnitPrice - sale.Quote.Quote.UnitPrice, world.Balance(world.Wren));
            Assert.Equal(supply, world.State.Progression!.Currency.AdmittedSupply);

            // Bounded: the shopper published one showing and one acceptance, and Wren issued it exactly one quote.
            IReadOnlyList<WorldActionCommand> commands = world.RecordedBatches(start + 1, world.State.Revision)
                .SelectMany(batch => batch.Actions).ToList();
            Assert.Equal(2, commands.Count(value => value.ActorId == world.Trader));
            Assert.Single(commands, value => value.ActorId == world.Trader && value.Intent is DiscloseMarketLotIntent);
            Assert.Single(commands, value => value.Intent is IssueMarketQuoteIntent issue && issue.Quote.CounterpartyActorId == world.Trader);
        }
        finally
        {
            session.SetReady(false);
            await session.StopAsync();
        }
    }

    /// <summary>
    /// At the cutoff where the shopper accepts Wren's bid, the player's own request opens Wren's dialogue first in
    /// canonical order, so the world refuses the acceptance. That decision is never retried: not on later cutoffs and
    /// not after a real restart. Once the refused quote closes, a new attempt is a new decision with new identities,
    /// and it trades.
    /// </summary>
    [Fact]
    public async Task A_refused_host_acceptance_is_never_retried_and_a_new_decision_trades_after_a_restart()
    {
        using var world = new ScheduledTradingWorld();
        world.BuyOneFromWrenDirectly();
        EntityId goods = world.Fixture.Gift(world.Authority, world.Wren, world.Trader, world.Fixture.Paid.OrdinaryLot, 1);
        world.WalkWrenToWork();
        world.WalkBesideWork(world.Trader);
        world.WalkBesideWork(world.Player);
        Assert.True(world.Player < world.Trader, "The fixture authors the third human after the player in canonical order.");
        MarketBinding market = world.Fixture.WrenMarket;
        var trading = new LocalTradingConfiguration([new LocalMerchantPreference(world.Wren, offerLifetimeSteps: 4)],
            [new LocalShopperPreference(world.Trader, market.MarketId, desiredUnits: 0, reconsiderAfterSteps: 4)]);
        ulong start = world.State.Revision;
        var time = new ManualSessionTimeProvider();
        LocalWorldSession session = world.Open(time, trading);
        WorldActionCommand refused;
        WorldPaidQuote first;
        try
        {
            session.SetReady(true);
            await SessionFixture.Tick(time);
            await SessionFixture.Tick(time);
            first = Assert.Single(world.State.Coordination!.PaidExchange!.Open, value => value.Quote.CounterpartyActorId == world.Trader);
            WorldSnapshot before = world.State;
            Assert.Equal(WorldActionRejection.None, (await ScheduledTradingWorld.Submit(session, time, world.Speak(world.Wren))).Rejection);
            WorldSnapshot held = world.State;
            Assert.True(held.HasReadingHold);
            refused = Assert.Single(world.RecordedBatch(held.Revision).Actions, value => value.ActorId == world.Trader);
            Assert.Equal(first.QuoteId, Assert.IsType<AcceptSpotQuoteIntent>(refused.Intent).Acceptance.QuoteId);
            Assert.Equal(WorldActionRejection.Unavailable, world.Authority.FindReceipt(refused, world.DeliveryOf(world.Trader))!.Rejection);
            Assert.NotNull(held.Coordination!.PaidExchange!.FindOpen(first.QuoteId));
            Assert.Equal((before.Inventory.ContentHash, before.Progression!.ContentHash),
                (held.Inventory.ContentHash, held.Progression!.ContentHash));
            Assert.Equal(WorldActionRejection.None, (await ScheduledTradingWorld.Submit(session, time, world.PlayerCommand(
                new CloseDialogueIntent(held.Dialogues.Single().DialogueId), world.Fixture.NextId()))).Rejection);
        }
        finally
        {
            session.SetReady(false);
            await session.StopAsync();
        }

        // A real restart forgets every attempt; the occupied acceptance still keeps the refused decision decided.
        world.Reopen();
        var reopenedTime = new ManualSessionTimeProvider();
        LocalWorldSession reopened = world.Open(reopenedTime, trading);
        try
        {
            reopened.SetReady(true);
            for (int tick = 0; world.State.Coordination!.PaidExchange!.Fills.All(value => value.Quote.Quote.CounterpartyActorId != world.Trader); tick++)
            {
                Assert.True(tick < 16, "After the refused quote closes, a new decision must trade within a few cutoffs.");
                await SessionFixture.Tick(reopenedTime);
            }
            WorldPaidQuoteOutcome sale = world.State.Coordination!.PaidExchange!.Fills.Single(value =>
                value.Quote.Quote.CounterpartyActorId == world.Trader);
            Assert.NotEqual(first.QuoteId, sale.Quote.QuoteId);
            Assert.Equal(WorldPaidQuoteOutcomeKind.Expired, world.State.Coordination!.PaidExchange!.FindOutcome(first.QuoteId)!.Kind);
            Assert.Equal(world.State.GetResident(world.Wren).InventoryCustodianId, world.State.Inventory.Custody.GetLot(goods).CustodianId);
            List<WorldActionCommand> acceptances = world.RecordedBatches(start + 1, world.State.Revision).SelectMany(batch => batch.Actions)
                .Where(value => value.ActorId == world.Trader && value.Intent is AcceptSpotQuoteIntent).ToList();
            Assert.Equal(new[] { refused.CommandId, sale.Settlement!.Acceptance.AcceptanceId }, acceptances.Select(value => value.CommandId));
        }
        finally
        {
            reopened.SetReady(false);
            await reopened.StopAsync();
        }
    }

    internal static ulong LocalMarketUnits(ScheduledTradingWorld world, EntityId actor) =>
        LocalMarketWorldFixture.UsableUnits(world.State, world.State.GetResident(actor).InventoryCustodianId, world.Berries);
}
