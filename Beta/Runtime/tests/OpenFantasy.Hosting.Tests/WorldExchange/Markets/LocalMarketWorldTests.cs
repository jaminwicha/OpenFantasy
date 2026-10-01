using Microsoft.Data.Sqlite;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Observations;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldExchange.Markets;

/// <summary>
/// Local markets as actual profile-33 world actions through the step processor and the SQLite authority:
/// rule-priced quotes over real stock, settlement through the existing paid transaction, stale-basis
/// refusal after a real arrival, local dated private knowledge with a player and an NPC observer, an
/// automatically selected coordinated cut, reopening, exact retry and journal tampering.
/// </summary>
public sealed class LocalMarketWorldTests
{
    [Fact]
    public void Rule_priced_purchase_and_resale_move_real_goods_and_funds_and_survive_retry_and_reopen()
    {
        var fixture = new LocalMarketWorldFixture(LocalMarketWorldFixture.Gentle);
        using var files = new StorageFiles();
        SqliteWorldAuthority authority = fixture.Create(files);
        try
        {
            var service = new MarketTradingService(authority);
            WorldSnapshot state = fixture.EarnReward(authority);
            MarketBinding market = fixture.WrenMarket;
            CurrencyLedger before = state.Progression!.Currency;
            ulong stock = market.CountUsableStock(state.Inventory, state.Production!.Lots);
            Assert.Equal(fixture.WrenGenesisStock, stock);
            ulong ask = market.Policy.Ask(stock, 1)!.Value;
            Assert.True(before.GetActorAccount(fixture.Player).Balance >= ask, "The earned reward must fund one unit.");

            // The merchant's own controller prepares the rule's price; the world recomputes it.
            CountedSourceLot lot = state.Inventory.Custody.GetLot(fixture.Paid.OrdinaryLot);
            TrustedCommandDelivery wren = LocalMarketWorldFixture.DeliveryOf(state, fixture.Wren);
            WorldActionCommand offer = service.PrepareOffer(wren, market.MarketId, fixture.Player, SpotQuoteSide.IssuerSells,
                lot.LotId, 1, 32, fixture.NextId())!;
            SpotQuote quote = ((IssueMarketQuoteIntent)offer.Intent).Quote;
            Assert.Equal(ask, quote.UnitPrice);
            Assert.Equal(WorldActionRejection.None, service.Submit(offer, wren, fixture.NextId()).Rejection);
            state = authority.ReadStateForHost();
            MarketQuoteRecord record = state.Coordination!.Markets!.FindQuote(quote.QuoteId)!;
            Assert.Equal((market.MarketId, stock, market.Policy.Version), (record.MarketId, record.StockBasis, record.PolicyVersion));
            Assert.Equal(before.Revision, state.Progression!.Currency.Revision); // Issuance moves no money.

            WorldActionCommand buy = fixture.Accept(state, fixture.Player, quote, 1);
            WorldActionReceipt bought = authority.SubmitPaidExchangeCommand(buy, LocalMarketWorldFixture.Delivery(buy), fixture.NextId());
            Assert.Equal(WorldActionRejection.None, bought.Rejection);
            state = authority.ReadStateForHost();
            CurrencyLedger afterBuy = state.Progression!.Currency;
            Assert.Equal(before.AdmittedSupply, afterBuy.AdmittedSupply);
            Assert.Equal(before.GetActorAccount(fixture.Player).Balance - ask, afterBuy.GetActorAccount(fixture.Player).Balance);
            Assert.Equal(before.GetActorAccount(fixture.Wren).Balance + ask, afterBuy.GetActorAccount(fixture.Wren).Balance);
            EntityId child = WorldPaidQuote.SplitChildOf(buy.CommandId);
            CountedSourceLot owned = state.Inventory.Custody.GetLot(child);
            Assert.Equal((state.GetResident(fixture.Player).InventoryCustodianId, 1UL, lot.Source.SourceId),
                (owned.CustodianId, owned.Quantity, owned.Source.SourceId));
            // The sale changed the merchant's usable stock once, and with it the eligible terms.
            Assert.Equal(stock - 1, market.CountUsableStock(state.Inventory, state.Production!.Lots));

            // The merchant buys the same unit back at its rule's bid for the new stock: less than was paid. It can
            // pin the player's lot only after the player itself shows exactly that lot to this market.
            ulong bid = market.Policy.Bid(stock - 1, 1)!.Value;
            Assert.True(bid < ask, "A closed buy and resale loses at least the declared spread.");
            Assert.Null(service.PrepareOffer(wren, market.MarketId, fixture.Player, SpotQuoteSide.IssuerBuys, child, 1, 32,
                fixture.NextId()));
            TrustedCommandDelivery player = LocalMarketWorldFixture.DeliveryOf(state, fixture.Player);
            WorldActionCommand show = service.PrepareDisclosure(player, fixture.Player, market.MarketId, child, 1, 32, fixture.NextId())!;
            Assert.Equal(WorldActionRejection.None, service.Submit(show, player, fixture.NextId()).Rejection);
            state = authority.ReadStateForHost();
            WorldActionCommand repurchase = service.PrepareOffer(LocalMarketWorldFixture.DeliveryOf(state, fixture.Wren),
                market.MarketId, fixture.Player, SpotQuoteSide.IssuerBuys, child, 1, 32, fixture.NextId())!;
            SpotQuote buyBack = ((IssueMarketQuoteIntent)repurchase.Intent).Quote;
            Assert.Equal(bid, buyBack.UnitPrice);
            Assert.Equal(WorldActionRejection.None,
                service.Submit(repurchase, LocalMarketWorldFixture.Delivery(repurchase), fixture.NextId()).Rejection);
            state = authority.ReadStateForHost();
            WorldActionCommand sell = fixture.Accept(state, fixture.Player, buyBack, 1);
            WorldActionReceipt sold = authority.SubmitPaidExchangeCommand(sell, LocalMarketWorldFixture.Delivery(sell), fixture.NextId());
            Assert.Equal(WorldActionRejection.None, sold.Rejection);
            state = authority.ReadStateForHost();
            CurrencyLedger afterSale = state.Progression!.Currency;
            Assert.Equal(before.AdmittedSupply, afterSale.AdmittedSupply);
            Assert.Equal(before.GetActorAccount(fixture.Player).Balance - ask + bid, afterSale.GetActorAccount(fixture.Player).Balance);
            Assert.Equal(stock, market.CountUsableStock(state.Inventory, state.Production!.Lots));
            Assert.Equal(market.MarketId, state.Coordination!.Markets!.FindQuote(buyBack.QuoteId)!.MarketId);

            // Exact retries return their original receipts; reading changes nothing.
            ContentHash head = state.ContentHash;
            Assert.Equal(bought.ResultId, authority.SubmitPaidExchangeCommand(buy, LocalMarketWorldFixture.Delivery(buy), fixture.NextId()).ResultId);
            Assert.Equal(sold.ResultId, authority.SubmitPaidExchangeCommand(sell, LocalMarketWorldFixture.Delivery(sell), fixture.NextId()).ResultId);
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);
            authority.Dispose();
            authority = fixture.Reopen(files);
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);
            Assert.True(authority.IsIdentityOccupiedForHost(quote.QuoteId));
            Assert.True(authority.IsIdentityOccupiedForHost(child));
            Assert.Equal(WorldReceiptCodecV1.Encode(sold), WorldReceiptCodecV1.Encode(
                authority.SubmitPaidExchangeCommand(sell, LocalMarketWorldFixture.Delivery(sell), fixture.NextId())));
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);
        }
        finally { authority.Dispose(); }
    }

    [Fact]
    public void A_real_arrival_makes_an_issued_market_quote_stale_until_it_is_withdrawn_and_requoted()
    {
        var fixture = new LocalMarketWorldFixture(LocalMarketWorldFixture.Gentle);
        using var files = new StorageFiles();
        SqliteWorldAuthority authority = fixture.Create(files);
        try
        {
            var service = new MarketTradingService(authority);
            WorldSnapshot state = fixture.EarnReward(authority);
            MarketBinding market = fixture.TraderMarket;
            TrustedCommandDelivery trader = LocalMarketWorldFixture.DeliveryOf(state, fixture.Trader);
            // An empty stall posts no ask: nothing is quoted from stock it does not have.
            Assert.Null(service.PrepareOffer(trader, market.MarketId, fixture.Player, SpotQuoteSide.IssuerSells,
                fixture.Paid.OrdinaryLot, 1, 16, fixture.NextId()));

            // Supply arrives only through Wren's real saved gift of one unit.
            EntityId first = fixture.Gift(authority, fixture.Wren, fixture.Trader, fixture.Paid.OrdinaryLot, 1);
            state = authority.ReadStateForHost();
            Assert.Equal(1UL, market.CountUsableStock(state.Inventory, state.Production!.Lots));
            WorldActionCommand offer = service.PrepareOffer(trader, market.MarketId, fixture.Player, SpotQuoteSide.IssuerSells,
                first, 1, 16, fixture.NextId())!;
            SpotQuote stale = ((IssueMarketQuoteIntent)offer.Intent).Quote;
            Assert.Equal(market.Policy.Ask(1, 1), stale.UnitPrice);
            Assert.Equal(WorldActionRejection.None, service.Submit(offer, trader, fixture.NextId()).Rejection);

            // Polling and reordered reads are pure: the head and the derived terms stay identical.
            state = authority.ReadStateForHost();
            ContentHash polled = state.ContentHash;
            MarketPostedTerms once = market.PostedTerms(market.CountUsableStock(state.Inventory, state.Production!.Lots));
            for (int read = 0; read < 3; read++)
            {
                WorldSnapshot again = authority.ReadStateForHost();
                Assert.Equal(polled, again.ContentHash);
                Assert.Equal(once, market.PostedTerms(market.CountUsableStock(again.Inventory, again.Production!.Lots)));
            }

            // A second real arrival changes the basis: the fixed offer is stale and is refused, never requoted.
            fixture.Gift(authority, fixture.Wren, fixture.Trader, fixture.Paid.OrdinaryLot, 1);
            state = authority.ReadStateForHost();
            Assert.Equal(2UL, market.CountUsableStock(state.Inventory, state.Production!.Lots));
            // The pinned lot itself is unchanged, so only the market basis makes this offer stale.
            Assert.Equal(stale.ExpectedLotRevision, state.Inventory.Custody.GetLot(first).Revision);
            Assert.True(state.Progression!.Currency.GetActorAccount(fixture.Player).Balance >= stale.UnitPrice,
                "The refusal below is not a funding refusal.");
            WorldActionCommand refused = fixture.Accept(state, fixture.Player, stale, 1);
            WorldActionReceipt conflict = authority.SubmitPaidExchangeCommand(refused, LocalMarketWorldFixture.Delivery(refused), fixture.NextId());
            Assert.Equal(WorldActionRejection.RevisionConflict, conflict.Rejection);
            WorldSnapshot after = authority.ReadStateForHost();
            AssertOwnersUnchanged(state, after);
            Assert.Equal(conflict.ResultId, authority.SubmitPaidExchangeCommand(refused,
                LocalMarketWorldFixture.Delivery(refused), fixture.NextId()).ResultId);

            // The issuer withdraws the stale offer; the rule then prices the larger stock lower.
            WorldActionCommand withdraw = fixture.Command(after, fixture.Trader, new WithdrawSpotQuoteIntent(stale.QuoteId));
            Assert.Equal(WorldActionRejection.None, authority.SubmitPaidExchangeCommand(withdraw, trader, fixture.NextId()).Rejection);
            state = authority.ReadStateForHost();
            Assert.Equal(WorldPaidQuoteOutcomeKind.Withdrawn, state.Coordination!.PaidExchange!.FindOutcome(stale.QuoteId)!.Kind);
            WorldActionCommand requote = service.PrepareOffer(trader, market.MarketId, fixture.Player, SpotQuoteSide.IssuerSells,
                first, 1, 16, fixture.NextId())!;
            SpotQuote fresh = ((IssueMarketQuoteIntent)requote.Intent).Quote;
            Assert.Equal(market.Policy.Ask(2, 1), fresh.UnitPrice);
            Assert.True(fresh.UnitPrice < stale.UnitPrice, "More usable stock lowers the ask.");
            Assert.Equal(WorldActionRejection.None, service.Submit(requote, trader, fixture.NextId()).Rejection);
            state = authority.ReadStateForHost();
            CurrencyLedger paid = state.Progression!.Currency;
            WorldActionCommand buy = fixture.Accept(state, fixture.Player, fresh, 1);
            Assert.Equal(WorldActionRejection.None,
                authority.SubmitPaidExchangeCommand(buy, LocalMarketWorldFixture.Delivery(buy), fixture.NextId()).Rejection);
            state = authority.ReadStateForHost();
            Assert.Equal(paid.GetActorAccount(fixture.Trader).Balance + fresh.UnitPrice,
                state.Progression!.Currency.GetActorAccount(fixture.Trader).Balance);
            Assert.Equal(paid.AdmittedSupply, state.Progression.Currency.AdmittedSupply);
            Assert.Equal(1UL, market.CountUsableStock(state.Inventory, state.Production!.Lots));
        }
        finally { authority.Dispose(); }
    }

    /// <summary>
    /// Usable stock is a settlement condition of a market quote, not a revision. An open quote refused while a
    /// real arrival changed the count settles at its exact terms once a real gift restores that count; after
    /// it is filled, the same count returning again never revives it, and the issuer revision advanced once.
    /// </summary>
    [Fact]
    public void An_open_market_quote_settles_again_when_gifts_restore_its_stock_basis_but_a_filled_one_never_revives()
    {
        var fixture = new LocalMarketWorldFixture(LocalMarketWorldFixture.Gentle);
        using var files = new StorageFiles();
        SqliteWorldAuthority authority = fixture.Create(files);
        try
        {
            var service = new MarketTradingService(authority);
            WorldSnapshot state = fixture.EarnReward(authority);
            MarketBinding market = fixture.TraderMarket;
            TrustedCommandDelivery trader = LocalMarketWorldFixture.DeliveryOf(state, fixture.Trader);
            EntityId first = fixture.Gift(authority, fixture.Wren, fixture.Trader, fixture.Paid.OrdinaryLot, 1);
            WorldActionCommand offer = service.PrepareOffer(trader, market.MarketId, fixture.Player, SpotQuoteSide.IssuerSells,
                first, 1, 32, fixture.NextId())!;
            SpotQuote quote = ((IssueMarketQuoteIntent)offer.Intent).Quote;
            Assert.Equal(WorldActionRejection.None, service.Submit(offer, trader, fixture.NextId()).Rejection);

            EntityId second = fixture.Gift(authority, fixture.Wren, fixture.Trader, fixture.Paid.OrdinaryLot, 1);
            state = authority.ReadStateForHost();
            Assert.Equal(2UL, market.CountUsableStock(state.Inventory, state.Production!.Lots));
            WorldActionCommand early = fixture.Accept(state, fixture.Player, quote, 1);
            Assert.Equal(WorldActionRejection.RevisionConflict,
                authority.SubmitPaidExchangeCommand(early, LocalMarketWorldFixture.Delivery(early), fixture.NextId()).Rejection);

            // The trader gives the arrival back: the open quote's exact basis returns and its fixed terms settle.
            fixture.Gift(authority, fixture.Trader, fixture.Wren, second, 1);
            state = authority.ReadStateForHost();
            Assert.Equal(1UL, market.CountUsableStock(state.Inventory, state.Production!.Lots));
            Assert.Equal(quote.ExpectedLotRevision, state.Inventory.Custody.GetLot(first).Revision);
            WorldActionCommand later = fixture.Accept(state, fixture.Player, quote, 1);
            Assert.Equal(WorldActionRejection.None,
                authority.SubmitPaidExchangeCommand(later, LocalMarketWorldFixture.Delivery(later), fixture.NextId()).Rejection);
            state = authority.ReadStateForHost();
            Assert.Equal(WorldPaidQuoteOutcomeKind.Filled, state.Coordination!.PaidExchange!.FindOutcome(quote.QuoteId)!.Kind);
            Assert.Equal(quote.MarketRevision + 1, state.Coordination.PaidExchange.GetIssuerRevision(fixture.Trader));

            // The same count returning once more cannot reopen the filled quote.
            fixture.Gift(authority, fixture.Wren, fixture.Trader, second, 1);
            state = authority.ReadStateForHost();
            Assert.Equal(1UL, market.CountUsableStock(state.Inventory, state.Production!.Lots));
            WorldActionCommand revived = fixture.Accept(state, fixture.Player, quote, 1);
            Assert.Equal(WorldActionRejection.Unavailable,
                authority.SubmitPaidExchangeCommand(revived, LocalMarketWorldFixture.Delivery(revived), fixture.NextId()).Rejection);
            AssertOwnersUnchanged(state, authority.ReadStateForHost());
        }
        finally { authority.Dispose(); }
    }

    [Fact]
    public void Learned_prices_are_local_dated_private_and_never_refresh_from_a_distance()
    {
        var fixture = new LocalMarketWorldFixture(LocalMarketWorldFixture.Steep);
        using var files = new StorageFiles();
        SqliteWorldAuthority authority = fixture.Create(files);
        try
        {
            var service = new MarketTradingService(authority);
            WorldSnapshot state = fixture.EarnReward(authority);
            MarketBinding market = fixture.WrenMarket;
            TrustedCommandDelivery player = LocalMarketWorldFixture.DeliveryOf(state, fixture.Player);
            TrustedCommandDelivery npc = LocalMarketWorldFixture.DeliveryOf(state, fixture.Trader);
            Assert.Empty(service.ReadKnowledge(fixture.Player, player));

            // A guessed market identity prepares nothing, and its raw command is refused without creating knowledge.
            var guess = new EntityId(0x4D4B_0BAD, 1);
            Assert.Null(service.PrepareObservation(player, fixture.Player, guess, fixture.NextId()));
            WorldActionCommand guessed = fixture.Command(authority.ReadStateForHost(), fixture.Player, new ObserveMarketIntent(guess, 0));
            Assert.Equal(WorldActionRejection.OutOfReach, service.Submit(guessed, player, fixture.NextId()).Rejection);
            Assert.Empty(service.ReadKnowledge(fixture.Player, player));

            // In person, the player and the NPC trader each learn the actual posted terms.
            Assert.Equal(SpatialInteraction.Reachable, authority.ReadStateForHost().Space.QueryInteraction(fixture.Player, fixture.Wren));
            WorldActionCommand look = service.PrepareObservation(player, fixture.Player, market.MarketId, fixture.NextId())!;
            Assert.Equal(WorldActionRejection.None, service.Submit(look, player, fixture.NextId()).Rejection);
            WorldActionCommand npcLook = service.PrepareObservation(npc, fixture.Trader, market.MarketId, fixture.NextId())!;
            Assert.Equal(WorldActionRejection.None, service.Submit(npcLook, npc, fixture.NextId()).Rejection);
            state = authority.ReadStateForHost();
            MarketPriceObservation learned = state.Coordination!.Markets!.FindObservation(fixture.Player, market.MarketId)!;
            ulong stock = market.CountUsableStock(state.Inventory, state.Production!.Lots);
            Assert.Equal((market.Policy.Ask(stock, 1), market.Policy.Bid(stock, 1), look.CommandId),
                (learned.AskUnitPrice, learned.BidUnitPrice, learned.CauseCommandId));
            Assert.Equal(market.Policy.Version, learned.PolicyVersion);
            Assert.NotNull(state.Coordination!.Markets!.FindObservation(fixture.Trader, market.MarketId));
            // Another actor's controller cannot read that memory.
            Assert.Throws<AuthorityDispatchException>(() => service.ReadKnowledge(fixture.Player, npc));
            Assert.Single(service.ReadKnowledge(fixture.Trader, npc));

            // The third human steps aside along its admitted exit; the player then actually walks out of range.
            SubunitPosition home = state.Space.GetActor(fixture.Player).Feet;
            state = fixture.StepTraderAside(authority);
            state = fixture.Walk(authority, fixture.Player, LocalMarketWorldFixture.DistantGoal(state, fixture.Player),
                value => value.Space.QueryInteraction(fixture.Player, fixture.Wren) != SpatialInteraction.Reachable);
            Assert.NotEqual(home, state.Space.GetActor(fixture.Player).Feet);

            // The market changes lawfully while the player is away: Wren gives one unit to the trader.
            fixture.Gift(authority, fixture.Wren, fixture.Trader, fixture.Paid.OrdinaryLot, 1);
            state = authority.ReadStateForHost();
            ulong changed = market.CountUsableStock(state.Inventory, state.Production!.Lots);
            Assert.Equal(stock - 1, changed);
            Assert.NotEqual(learned.AskUnitPrice, market.Policy.Ask(changed, 1));

            // Reading the old memory neither refreshes it nor reveals the distant change; it only ages.
            MarketKnowledgeEntry remembered = Assert.Single(service.ReadKnowledge(fixture.Player, player));
            Assert.Equal((learned.AskUnitPrice, learned.BidUnitPrice, learned.ObservedAdvancingStep),
                (remembered.AskUnitPrice, remembered.BidUnitPrice, remembered.ObservedAdvancingStep));
            Assert.True(remembered.AgeInSteps > 0);
            // From a distance, preparing a reading of the real market is indistinguishable from a guessed identity:
            // both give nothing and disclose no merchant revision, and preparation changes nothing.
            ContentHash away = authority.ReadStateForHost().ContentHash;
            Assert.Null(service.PrepareObservation(player, fixture.Player, market.MarketId, fixture.NextId()));
            Assert.Null(service.PrepareObservation(player, fixture.Player, guess, fixture.NextId()));
            Assert.Equal(away, authority.ReadStateForHost().ContentHash);
            // The raw command, even naming Wren's true current presence revision, refuses exactly like the guess and
            // cannot advance knowledge.
            state = authority.ReadStateForHost();
            WorldActionCommand distant = fixture.Command(state, fixture.Player,
                new ObserveMarketIntent(market.MarketId, state.Space.GetActor(fixture.Wren).Revision));
            Assert.Equal(WorldActionRejection.OutOfReach, service.Submit(distant, player, fixture.NextId()).Rejection);
            WorldActionCommand distantGuess = fixture.Command(authority.ReadStateForHost(), fixture.Player,
                new ObserveMarketIntent(guess, state.Space.GetActor(fixture.Wren).Revision));
            Assert.Equal(WorldActionRejection.OutOfReach, service.Submit(distantGuess, player, fixture.NextId()).Rejection);
            Assert.Equal(learned.ContentHash,
                authority.ReadStateForHost().Coordination!.Markets!.FindObservation(fixture.Player, market.MarketId)!.ContentHash);

            // Revisiting and observing again yields new dated evidence of the changed terms.
            state = fixture.Walk(authority, fixture.Player, home, value => value.Space.GetActor(fixture.Player).Feet == home);
            Assert.Equal(SpatialInteraction.Reachable, state.Space.QueryInteraction(fixture.Player, fixture.Wren));
            WorldActionCommand revisit = service.PrepareObservation(player, fixture.Player, market.MarketId, fixture.NextId())!;
            Assert.Equal(WorldActionRejection.None, service.Submit(revisit, player, fixture.NextId()).Rejection);
            MarketPriceObservation renewed = authority.ReadStateForHost().Coordination!.Markets!
                .FindObservation(fixture.Player, market.MarketId)!;
            Assert.Equal(market.Policy.Ask(changed, 1), renewed.AskUnitPrice);
            Assert.True(renewed.ObservedSequence > learned.ObservedSequence);
            // The NPC's earlier memory stayed exactly as it learned it.
            Assert.Equal(market.Policy.Ask(stock, 1), authority.ReadStateForHost().Coordination!.Markets!
                .FindObservation(fixture.Trader, market.MarketId)!.AskUnitPrice);
        }
        finally { authority.Dispose(); }
    }

    [Fact]
    public void Market_quotes_and_knowledge_cross_an_automatic_cut_then_settle_reopen_retry_and_refuse_tampering()
    {
        var fixture = new LocalMarketWorldFixture(stock => LocalMarketWorldFixture.Gentle(stock, 1_024),
            stock => LocalMarketWorldFixture.Gentle(stock, 1_024));
        using var files = new StorageFiles();
        SqliteWorldAuthority authority = fixture.Create(files);
        try
        {
            var service = new MarketTradingService(authority);
            WorldSnapshot state = fixture.EarnReward(authority);
            MarketBinding market = fixture.WrenMarket;
            TrustedCommandDelivery player = LocalMarketWorldFixture.DeliveryOf(state, fixture.Player);
            TrustedCommandDelivery wren = LocalMarketWorldFixture.DeliveryOf(state, fixture.Wren);
            WorldSnapshot beforeLook = state;
            WorldActionCommand look = service.PrepareObservation(player, fixture.Player, market.MarketId, fixture.NextId())!;
            Assert.Equal(WorldActionRejection.None, service.Submit(look, player, fixture.NextId()).Rejection);
            ulong lookSequence = authority.ReadStateForHost().Revision;
            WorldActionCommand offer = service.PrepareOffer(wren, market.MarketId, fixture.Player, SpotQuoteSide.IssuerSells,
                fixture.Paid.OrdinaryLot, 1, 1_000, fixture.NextId())!;
            SpotQuote quote = ((IssueMarketQuoteIntent)offer.Intent).Quote;
            Assert.Equal(WorldActionRejection.None, service.Submit(offer, wren, fixture.NextId()).Rejection);
            state = authority.ReadStateForHost();
            MarketPriceObservation learned = state.Coordination!.Markets!.FindObservation(fixture.Player, market.MarketId)!;
            // Wren also shows one unit of her own lot to the trader's market; that live showing must cross the cut.
            WorldActionCommand show = service.PrepareDisclosure(wren, fixture.Wren, fixture.TraderMarket.MarketId,
                fixture.Paid.OrdinaryLot, 1, 1_000, fixture.NextId())!;
            Assert.Equal(WorldActionRejection.None, service.Submit(show, wren, fixture.NextId()).Rejection);
            state = authority.ReadStateForHost();
            MarketLotDisclosure shown = state.Coordination!.Markets!.FindDisclosure(fixture.Wren, fixture.TraderMarket.MarketId)!;
            Assert.Equal((show.CommandId, fixture.Trader), (shown.CauseCommandId, shown.MerchantActorId));

            // Wren's ordinary bilateral quotes expire one by one until the host selects a coordinated cut.
            for (int index = 0; authority.InspectForHost().Journal.MaterialCareArchiveCount == 0; index++)
            {
                Assert.True(index < WorldPaidExchangeBook.MaximumOutcomes + 8, "The bounded outcome window must cut automatically.");
                state = ExpireOneBilateralQuote(authority, fixture, state);
            }
            state = authority.ReadStateForHost();
            WorldCoordinatedContinuation cut = state.Coordination!;
            Assert.NotNull(cut.Anchor!.Markets);
            Assert.Equal(quote.ContentHash, cut.PaidExchange!.FindOpen(quote.QuoteId)!.Quote.ContentHash);
            Assert.NotNull(cut.Markets!.FindQuote(quote.QuoteId));
            Assert.Equal(learned.ContentHash, cut.Markets!.FindObservation(fixture.Player, market.MarketId)!.ContentHash);
            Assert.Equal(shown.ContentHash, cut.Markets!.FindDisclosure(fixture.Wren, fixture.TraderMarket.MarketId)!.ContentHash);
            Assert.Equal(shown.ContentHash, cut.Anchor!.Markets!.Carried.FindDisclosure(fixture.Wren, fixture.TraderMarket.MarketId)!.ContentHash);
            ContentHash head = state.ContentHash;
            authority.Dispose();
            authority = fixture.Reopen(files);
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);

            // After reopening, the carried showing still lets the trader quote to buy exactly that lot.
            var reopened = new MarketTradingService(authority);
            state = authority.ReadStateForHost();
            TrustedCommandDelivery trader = LocalMarketWorldFixture.DeliveryOf(state, fixture.Trader);
            Assert.Single(reopened.ReadDisclosures(fixture.Trader, trader));
            WorldActionCommand traderBid = reopened.PrepareOffer(trader, fixture.TraderMarket.MarketId, fixture.Wren,
                SpotQuoteSide.IssuerBuys, fixture.Paid.OrdinaryLot, 1, 64, fixture.NextId())!;
            Assert.Equal(WorldActionRejection.None, reopened.Submit(traderBid, trader, fixture.NextId()).Rejection);

            // Live settlement after the cut, at the exact carried terms and unchanged stock basis.
            state = authority.ReadStateForHost();
            WorldActionCommand buy = fixture.Accept(state, fixture.Player, quote, 1);
            WorldActionReceipt settled = authority.SubmitPaidExchangeCommand(buy, LocalMarketWorldFixture.Delivery(buy), fixture.NextId());
            Assert.Equal(WorldActionRejection.None, settled.Rejection);
            state = authority.ReadStateForHost();
            Assert.Equal(WorldPaidQuoteOutcomeKind.Filled, state.Coordination!.PaidExchange!.FindOutcome(quote.QuoteId)!.Kind);
            Assert.Equal(learned.ContentHash, Assert.Single(authority.ReadMarketKnowledge(fixture.Player, player)).ContentHash);
            head = state.ContentHash;
            authority.Dispose();
            authority = fixture.Reopen(files);
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);
            Assert.Equal(WorldReceiptCodecV1.Encode(settled), WorldReceiptCodecV1.Encode(
                authority.SubmitPaidExchangeCommand(buy, LocalMarketWorldFixture.Delivery(buy), fixture.NextId())));
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);

            // A genuine earlier root with its own recomputed hash cannot replace the head: replay refuses it.
            using var forgedHead = new StorageFiles();
            authority.BackupForHost(forgedHead.Path);
            byte[] earlier = WorldSnapshotCodec.Encode(beforeLook);
            Mutate(forgedHead, "UPDATE world_head SET state=$state,hash=$hash WHERE singleton=1",
                ("$state", earlier), ("$hash", ContentHash.Compute(earlier).ToByteArray()));
            Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(forgedHead.Path));

            // A well-formed journal batch naming another market under the same identities also refuses.
            using var forgedJournal = new StorageFiles();
            authority.BackupForHost(forgedJournal.Path);
            byte[] mutated;
            using (var database = WorldDatabase.Open(forgedJournal.Path, create: false))
            {
                WorldStepBatch stored = WorldStepCodec.Decode(database.ReadJournal().Single(value => value.Sequence == lookSequence).AcceptedBatch);
                WorldActionCommand original = Assert.Single(stored.Actions);
                Assert.Equal(look.CommandId, original.CommandId);
                var changed = new WorldActionCommand(original.CommandId, original.PrincipalId, original.OriginSessionId,
                    original.ActorId, original.WorldId, original.BranchId, original.ExpectedActorRevision,
                    new ObserveMarketIntent(fixture.TraderMarket.MarketId, ((ObserveMarketIntent)original.Intent).ExpectedMerchantRevision));
                mutated = WorldStepCodec.Encode(new WorldStepBatch(stored.CauseId, stored.WorldId, stored.BranchId,
                    stored.ExpectedRevision, stored.ExpectedAdvancingSteps, stored.Kind, stored.Movement, [changed], stored.ProfileVersion));
            }
            Mutate(forgedJournal, "UPDATE world_commits SET accepted_batch=$batch WHERE sequence=$sequence",
                ("$batch", mutated), ("$sequence", WorldStorageBinary.Number(lookSequence)));
            Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(forgedJournal.Path));
        }
        finally { authority.Dispose(); }
    }

    [Fact]
    public void Market_submissions_refuse_unauthorized_forged_and_mispriced_terms_with_exact_scope()
    {
        var fixture = new LocalMarketWorldFixture(LocalMarketWorldFixture.Gentle);
        using var files = new StorageFiles();
        SqliteWorldAuthority authority = fixture.Create(files);
        try
        {
            var service = new MarketTradingService(authority);
            WorldSnapshot state = fixture.EarnReward(authority);
            MarketBinding market = fixture.WrenMarket;
            TrustedCommandDelivery wren = LocalMarketWorldFixture.DeliveryOf(state, fixture.Wren);
            TrustedCommandDelivery player = LocalMarketWorldFixture.DeliveryOf(state, fixture.Player);
            WorldActionCommand offer = service.PrepareOffer(wren, market.MarketId, fixture.Player, SpotQuoteSide.IssuerSells,
                fixture.Paid.OrdinaryLot, 1, 16, fixture.NextId())!;
            SpotQuote fair = ((IssueMarketQuoteIntent)offer.Intent).Quote;

            // Preparation refuses a caller that does not control the merchant; submission refuses a foreign delivery.
            Assert.Null(service.PrepareOffer(player, market.MarketId, fixture.Player, SpotQuoteSide.IssuerSells,
                fixture.Paid.OrdinaryLot, 1, 16, fixture.NextId()));
            Assert.Throws<AuthorityDispatchException>(() => service.Submit(offer, player, fixture.NextId()));

            // A merchant cannot charge other than its rule's price for the actual stock.
            WorldActionCommand mispriced = Reprice(offer, fair, fair.UnitPrice + 1);
            Assert.Equal(WorldActionRejection.RevisionConflict, service.Submit(mispriced, wren, fixture.NextId()).Rejection);
            WorldSnapshot after = authority.ReadStateForHost();
            AssertOwnersUnchanged(state, after);
            Assert.Equal(state.Coordination!.Markets!.ContentHash, after.Coordination!.Markets!.ContentHash);

            // Another actor cannot issue under Wren's market, even with its own true delivery.
            state = after;
            WorldActionCommand forged = fixture.Command(state, fixture.Trader, new IssueMarketQuoteIntent(market.MarketId, fair));
            Assert.Equal(WorldActionRejection.NotAuthorized, service.Submit(forged,
                LocalMarketWorldFixture.Delivery(forged), fixture.NextId()).Rejection);
            AssertOwnersUnchanged(state, authority.ReadStateForHost());

            // The bilateral profile-32 interpretation remains valid at an agreed price and records no market basis.
            state = authority.ReadStateForHost();
            EntityId bilateralId = fixture.NextId();
            var bilateral = new WorldActionCommand(bilateralId, wren.PrincipalId, wren.SessionId, fixture.Wren, state.WorldId,
                state.BranchId, state.Space.GetActor(fixture.Wren).Revision,
                new IssueSpotQuoteIntent(Rebind(fair, WorldPaidQuote.DeriveIdentity(bilateralId), 1, state)));
            Assert.Equal(WorldActionRejection.None, authority.SubmitPaidExchangeCommand(bilateral, wren, fixture.NextId()).Rejection);
            state = authority.ReadStateForHost();
            Assert.Null(state.Coordination!.Markets!.FindQuote(((IssueSpotQuoteIntent)bilateral.Intent).Quote.QuoteId));

            // Market intents are refused outside profile 33, and their bytes are canonical.
            WorldSnapshot paidOnly = fixture.Paid.Genesis;
            WorldActionCommand observe = fixture.Command(state, fixture.Player, new ObserveMarketIntent(market.MarketId, 0));
            Assert.Throws<ArgumentException>(() => new WorldStepBatch(fixture.NextId(), paidOnly.WorldId, paidOnly.BranchId, 0, 0,
                WorldBatchKind.AdvancingStep, [], [observe], WorldPaidExchangeAdmission.ProfileVersion));
            byte[] bytes = WorldActionCodec.Encode(observe);
            Assert.Equal(bytes, WorldActionCodec.Encode(WorldActionCodec.Decode(bytes)));
            Assert.ThrowsAny<FormatException>(() => WorldActionCodec.Decode([.. bytes, 0]));
            byte[] world = WorldSnapshotCodec.Encode(state);
            Assert.Equal(world, WorldSnapshotCodec.Encode(WorldSnapshotCodec.Decode(world)));
            Assert.Throws<FormatException>(() => WorldSnapshotCodec.Decode([.. world, 0]));
            Assert.Throws<ArgumentException>(() => WorldMarketAdmission.CreateFresh(state, [market]));
            Assert.Throws<ArgumentException>(() => WorldMarketAdmission.CreateFresh(fixture.Genesis, [market]));
        }
        finally { authority.Dispose(); }
    }

    private static WorldSnapshot ExpireOneBilateralQuote(SqliteWorldAuthority authority, LocalMarketWorldFixture fixture,
        WorldSnapshot state)
    {
        state = authority.ReadStateForHost();
        CountedSourceLot lot = state.Inventory.Custody.Lots.First(value => value.CustodianId == state.GetResident(fixture.Wren).InventoryCustodianId
            && value.Definition == fixture.Berries && value.ReservedBy is null && value.Protection == LotProtection.None);
        CurrencyLedger ledger = state.Progression!.Currency;
        TrustedCommandDelivery wren = LocalMarketWorldFixture.DeliveryOf(state, fixture.Wren);
        EntityId commandId = fixture.NextId();
        ulong issuedAt = state.AdvancingSteps + 1;
        var quote = new SpotQuote(WorldPaidQuote.DeriveIdentity(commandId), state.WorldId, state.BranchId, fixture.Wren,
            wren.PrincipalId, wren.SessionId, fixture.Player, SpotQuoteSide.IssuerSells, SpotLotKind.CountedSource, lot.LotId,
            lot.Definition, lot.Revision, lot.CustodianId, state.GetResident(fixture.Player).InventoryCustodianId,
            ledger.Currency.DefinitionId, ledger.Currency.Version, ledger.GetActorAccount(fixture.Wren).AccountId,
            ledger.GetActorAccount(fixture.Player).AccountId, 1, [], 1,
            state.Coordination!.PaidExchange!.GetIssuerRevision(fixture.Wren), issuedAt, issuedAt + 1);
        var issue = new WorldActionCommand(commandId, wren.PrincipalId, wren.SessionId, fixture.Wren, state.WorldId,
            state.BranchId, state.Space.GetActor(fixture.Wren).Revision, new IssueSpotQuoteIntent(quote));
        Assert.Equal(WorldActionRejection.None, authority.SubmitPaidExchangeCommand(issue, wren, fixture.NextId()).Rejection);
        state = authority.ReadStateForHost();
        authority.Commit(new WorldStepBatch(fixture.NextId(), state.WorldId, state.BranchId, state.Revision,
            state.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [], state.ProfileVersion));
        return authority.ReadStateForHost();
    }

    private static WorldActionCommand Reprice(WorldActionCommand command, SpotQuote quote, ulong unitPrice) =>
        new(command.CommandId, command.PrincipalId, command.OriginSessionId, command.ActorId, command.WorldId,
            command.BranchId, command.ExpectedActorRevision,
            new IssueMarketQuoteIntent(((IssueMarketQuoteIntent)command.Intent).MarketId, Copy(quote, quote.QuoteId, unitPrice)));

    private static SpotQuote Rebind(SpotQuote quote, EntityId quoteId, ulong unitPrice, WorldSnapshot state) =>
        new(quoteId, quote.WorldId, quote.BranchId, quote.IssuerActorId, quote.IssuerPrincipalId, quote.IssuerSessionId,
            quote.CounterpartyActorId, quote.Side, quote.LotKind, quote.LotId, quote.Definition,
            state.Inventory.Custody.GetLot(quote.LotId).Revision, quote.SellerCustodianId, quote.BuyerCustodianId,
            quote.CurrencyDefinitionId, quote.CurrencyVersion, quote.SellerAccountId, quote.BuyerAccountId, unitPrice, quote.Fees,
            quote.QuantityAllowance, quote.MarketRevision, state.AdvancingSteps + 1, state.AdvancingSteps + 16);

    private static SpotQuote Copy(SpotQuote quote, EntityId quoteId, ulong unitPrice) =>
        new(quoteId, quote.WorldId, quote.BranchId, quote.IssuerActorId, quote.IssuerPrincipalId, quote.IssuerSessionId,
            quote.CounterpartyActorId, quote.Side, quote.LotKind, quote.LotId, quote.Definition, quote.ExpectedLotRevision,
            quote.SellerCustodianId, quote.BuyerCustodianId, quote.CurrencyDefinitionId, quote.CurrencyVersion,
            quote.SellerAccountId, quote.BuyerAccountId, unitPrice, quote.Fees, quote.QuantityAllowance, quote.MarketRevision,
            quote.IssuedAtStep, quote.ExpiresAtStep);

    private static void AssertOwnersUnchanged(WorldSnapshot before, WorldSnapshot after)
    {
        Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, after.Production!.ContentHash);
        Assert.Equal(before.Progression!.ContentHash, after.Progression!.ContentHash);
        Assert.Equal(before.Coordination!.PaidExchange!.ContentHash, after.Coordination!.PaidExchange!.ContentHash);
        Assert.Equal(before.Coordination!.Markets!.ContentHash, after.Coordination!.Markets!.ContentHash);
        // Ordinary passive time still advances with the recorded refusal; it is not transaction leakage.
        Assert.Equal(before.Revision + 1, after.Revision);
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
    }

    /// <summary>Applies one deliberate edit to a disposable copy and proves it changed exactly one row.</summary>
    private static void Mutate(StorageFiles files, string sql, params (string Name, object Value)[] parameters)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = files.Path, Mode = SqliteOpenMode.ReadWrite, Pooling = false
        }.ToString());
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
        Assert.Equal(1, command.ExecuteNonQuery());
    }
}
