using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldExchange.Markets;

/// <summary>
/// The buy side of a local market through the actual SQLite authority: a merchant may inspect and pin another
/// actor's lot only after that seller's own in-person showing of exactly that lot to its market. Preparation and
/// raw world commands, market and bilateral, refuse unknown, foreign and unshown lots alike. Showing is withdrawn,
/// expires and goes stale; a lawful showing funds a real resale whose market due goes to a third actor without
/// letting the trader and payee profit together; and structural admission refuses a rule whose due differs.
/// </summary>
public sealed class LocalMarketDisclosureTests
{
    [Fact]
    public void Buy_quotes_need_the_sellers_own_lot_showing_and_a_due_cannot_fund_a_cooperating_resale()
    {
        var fixture = new LocalMarketWorldFixture(LocalMarketWorldFixture.ConstantWithDue);
        using var files = new StorageFiles();
        SqliteWorldAuthority authority = fixture.Create(files);
        try
        {
            var service = new MarketTradingService(authority);
            WorldSnapshot state = fixture.EarnReward(authority);
            MarketBinding market = fixture.WrenMarket;
            TrustedCommandDelivery wren = LocalMarketWorldFixture.DeliveryOf(state, fixture.Wren);
            TrustedCommandDelivery player = LocalMarketWorldFixture.DeliveryOf(state, fixture.Player);
            TrustedCommandDelivery trader = LocalMarketWorldFixture.DeliveryOf(state, fixture.Trader);
            CurrencyLedger start = state.Progression!.Currency;

            // The player buys one unit at the constant ask and pays the three-unit due to the third human.
            WorldActionCommand offer = service.PrepareOffer(wren, market.MarketId, fixture.Player, SpotQuoteSide.IssuerSells,
                fixture.Paid.OrdinaryLot, 1, 32, fixture.NextId())!;
            SpotQuote sale = ((IssueMarketQuoteIntent)offer.Intent).Quote;
            Assert.Equal((6UL, 3UL), (sale.UnitPrice, sale.UnitFees));
            Assert.Equal(WorldActionRejection.None, service.Submit(offer, wren, fixture.NextId()).Rejection);
            AssertAdmissionRefusesAnotherDue(authority.ReadStateForHost(), market);
            state = authority.ReadStateForHost();
            WorldActionCommand buy = fixture.Accept(state, fixture.Player, sale, 1);
            Assert.Equal(WorldActionRejection.None,
                authority.SubmitPaidExchangeCommand(buy, LocalMarketWorldFixture.Delivery(buy), fixture.NextId()).Rejection);
            EntityId bought = WorldPaidQuote.SplitChildOf(buy.CommandId);
            state = authority.ReadStateForHost();
            CountedSourceLot owned = state.Inventory.Custody.GetLot(bought);
            Assert.Equal(state.GetResident(fixture.Player).InventoryCustodianId, owned.CustodianId);

            // Without a showing, neither preparation nor any raw buy command can inspect or pin the player's lot:
            // the real lot, a wrong revision and an unknown identity all refuse alike and change no owner.
            var unknown = new EntityId(0x4D4B_0BAD, 2);
            Assert.Null(service.PrepareOffer(wren, market.MarketId, fixture.Player, SpotQuoteSide.IssuerBuys, bought, 1, 16, fixture.NextId()));
            Assert.Null(service.PrepareOffer(wren, market.MarketId, fixture.Player, SpotQuoteSide.IssuerBuys, unknown, 1, 16, fixture.NextId()));
            Assert.Null(service.PrepareOffer(wren, market.MarketId, fixture.Player, SpotQuoteSide.IssuerSells, bought, 1, 16, fixture.NextId()));
            Assert.Empty(service.ReadDisclosures(fixture.Wren, wren));
            foreach ((EntityId lot, ulong revision) in new[] { (bought, owned.Revision), (bought, owned.Revision + 1), (unknown, 0UL) })
            {
                AssertBuyRefused(authority, fixture, market, lot, revision, bilateral: false);
                AssertBuyRefused(authority, fixture, market, lot, revision, bilateral: true);
            }

            // Another actor cannot show the player's lot, a foreign delivery cannot speak for the player, and the
            // trader's controller cannot prepare a showing for it.
            state = authority.ReadStateForHost();
            var foreignShowing = new DiscloseMarketLotIntent(market.MarketId, SpotLotKind.CountedSource, bought, owned.Definition,
                owned.Revision, 1, state.Progression!.Currency.GetActorAccount(fixture.Player).AccountId, state.AdvancingSteps + 16);
            WorldActionCommand forged = fixture.Command(state, fixture.Trader, foreignShowing);
            Assert.Equal(WorldActionRejection.NotAuthorized, service.Submit(forged, trader, fixture.NextId()).Rejection);
            AssertOwnersUnchanged(state, authority.ReadStateForHost());
            Assert.Throws<AuthorityDispatchException>(() => service.Submit(
                fixture.Command(authority.ReadStateForHost(), fixture.Player, foreignShowing), trader, fixture.NextId()));
            Assert.Null(service.PrepareDisclosure(trader, fixture.Player, market.MarketId, bought, 1, 16, fixture.NextId()));
            Assert.Empty(authority.ReadStateForHost().Coordination!.Markets!.Disclosures);

            // The player's own showing reaches exactly Wren's market and only Wren's controller sees it.
            Show(service, fixture, player, market, bought, 16);
            MarketDisclosureEntry entry = Assert.Single(service.ReadDisclosures(fixture.Wren, wren));
            Assert.Equal((fixture.Player, bought, owned.Revision, 1UL), (entry.SellerActorId, entry.LotId, entry.LotRevision, entry.Quantity));
            Assert.Empty(service.ReadDisclosures(fixture.Wren, trader));
            Assert.Empty(service.ReadDisclosures(fixture.Trader, trader));

            // Withdrawn, it authorizes nothing again.
            WorldActionCommand hide = service.PrepareDisclosureWithdrawal(player, fixture.Player, market.MarketId, fixture.NextId())!;
            Assert.Equal(WorldActionRejection.None, service.Submit(hide, player, fixture.NextId()).Rejection);
            Assert.Null(service.PrepareOffer(wren, market.MarketId, fixture.Player, SpotQuoteSide.IssuerBuys, bought, 1, 16, fixture.NextId()));
            AssertBuyRefused(authority, fixture, market, bought, owned.Revision, bilateral: false);
            Assert.Equal(WorldActionRejection.Unavailable, service.Submit(fixture.Command(authority.ReadStateForHost(), fixture.Player,
                new WithdrawMarketLotDisclosureIntent(market.MarketId)), player, fixture.NextId()).Rejection);

            // Expired after its one-step window, it authorizes nothing either.
            Show(service, fixture, player, market, bought, 1);
            state = authority.ReadStateForHost();
            authority.Commit(new WorldStepBatch(fixture.NextId(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
                WorldBatchKind.AdvancingStep, [], [], state.ProfileVersion));
            Assert.Null(service.PrepareOffer(wren, market.MarketId, fixture.Player, SpotQuoteSide.IssuerBuys, bought, 1, 16, fixture.NextId()));
            Assert.Empty(service.ReadDisclosures(fixture.Wren, wren));
            AssertBuyRefused(authority, fixture, market, bought, owned.Revision, bilateral: false);

            // A fresh showing lets Wren quote to buy the unit back; only the player's acceptance sells it.
            Show(service, fixture, player, market, bought, 16);
            state = authority.ReadStateForHost();
            WorldActionCommand resale = service.PrepareOffer(LocalMarketWorldFixture.DeliveryOf(state, fixture.Wren), market.MarketId,
                fixture.Player, SpotQuoteSide.IssuerBuys, bought, 1, 16, fixture.NextId())!;
            SpotQuote buyBack = ((IssueMarketQuoteIntent)resale.Intent).Quote;
            Assert.Equal((1UL, 3UL), (buyBack.UnitPrice, buyBack.UnitFees));
            Assert.Equal(4UL, market.Policy.AllInBid(market.CountUsableStock(state.Inventory, state.Production!.Lots), 1));
            Assert.Equal(WorldActionRejection.None, service.Submit(resale, wren, fixture.NextId()).Rejection);
            state = authority.ReadStateForHost();
            WorldActionCommand sell = fixture.Accept(state, fixture.Player, buyBack, 1);
            Assert.Equal(WorldActionRejection.None,
                authority.SubmitPaidExchangeCommand(sell, LocalMarketWorldFixture.Delivery(sell), fixture.NextId()).Rejection);

            // The closed cycle restores Wren's stock; the trader and the due's payee together lose the spread.
            state = authority.ReadStateForHost();
            CurrencyLedger end = state.Progression!.Currency;
            long Delta(EntityId actor) => (long)end.GetActorAccount(actor).Balance - (long)start.GetActorAccount(actor).Balance;
            Assert.Equal((-8L, 6L, 2L), (Delta(fixture.Player), Delta(fixture.Trader), Delta(fixture.Wren)));
            Assert.Equal(-(long)(market.Policy.AskMarkup + market.Policy.BidMarkdown), Delta(fixture.Player) + Delta(fixture.Trader));
            Assert.Equal(start.AdmittedSupply, end.AdmittedSupply);
            Assert.Equal(fixture.WrenGenesisStock, market.CountUsableStock(state.Inventory, state.Production!.Lots));
            Assert.Equal(market.CustodianId, state.Inventory.Custody.GetLot(bought).CustodianId);

            // The showing is now stale: the shown lot left the player, so its exact pins no longer issue.
            WorldActionCommand stale = service.PrepareOffer(wren, market.MarketId, fixture.Player, SpotQuoteSide.IssuerBuys,
                bought, 1, 16, fixture.NextId())!;
            Assert.Equal(WorldActionRejection.Unavailable, service.Submit(stale, wren, fixture.NextId()).Rejection);

            // Reopening keeps every owner, the showing and both receipts exact.
            ContentHash head = authority.ReadStateForHost().ContentHash;
            authority.Dispose();
            authority = fixture.Reopen(files);
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);
            Assert.Equal(WorldActionRejection.None,
                authority.SubmitPaidExchangeCommand(sell, LocalMarketWorldFixture.Delivery(sell), fixture.NextId()).Rejection);
            Assert.Equal(head, authority.ReadStateForHost().ContentHash);
        }
        finally { authority.Dispose(); }
    }

    private static void Show(MarketTradingService service, LocalMarketWorldFixture fixture, TrustedCommandDelivery seller,
        MarketBinding market, EntityId lot, ulong lifetime)
    {
        WorldActionCommand show = service.PrepareDisclosure(seller, fixture.Player, market.MarketId, lot, 1, lifetime, fixture.NextId())!;
        Assert.Equal(WorldActionRejection.None, service.Submit(show, seller, fixture.NextId()).Rejection);
    }

    /// <summary>
    /// Wren's raw buy quote at the rule's actual price, pinning a lot in the player's storage, is refused as not
    /// authorized whatever that lot is; the market path and the ordinary bilateral path agree.
    /// </summary>
    private static void AssertBuyRefused(SqliteWorldAuthority authority, LocalMarketWorldFixture fixture, MarketBinding market,
        EntityId lotId, ulong revision, bool bilateral)
    {
        WorldSnapshot state = authority.ReadStateForHost();
        CurrencyLedger ledger = state.Progression!.Currency;
        TrustedCommandDelivery wren = LocalMarketWorldFixture.DeliveryOf(state, fixture.Wren);
        EntityId commandId = fixture.NextId();
        ulong issuedAt = state.AdvancingSteps + 1;
        ulong price = market.Policy.Bid(market.CountUsableStock(state.Inventory, state.Production!.Lots), 1)!.Value;
        var quote = new SpotQuote(WorldPaidQuote.DeriveIdentity(commandId), state.WorldId, state.BranchId, fixture.Wren,
            wren.PrincipalId, wren.SessionId, fixture.Player, SpotQuoteSide.IssuerBuys, SpotLotKind.CountedSource, lotId,
            fixture.Berries, revision, state.GetResident(fixture.Player).InventoryCustodianId, market.CustodianId,
            ledger.Currency.DefinitionId, ledger.Currency.Version, ledger.GetActorAccount(fixture.Player).AccountId,
            market.AccountId, price, [new SpotFee(market.Policy.FeePayeeAccountId!.Value, market.Policy.UnitFee)], 1,
            state.Coordination!.PaidExchange!.GetIssuerRevision(fixture.Wren), issuedAt, issuedAt + 16);
        var command = new WorldActionCommand(commandId, wren.PrincipalId, wren.SessionId, fixture.Wren, state.WorldId,
            state.BranchId, state.Space.GetActor(fixture.Wren).Revision,
            bilateral ? new IssueSpotQuoteIntent(quote) : new IssueMarketQuoteIntent(market.MarketId, quote));
        WorldActionReceipt receipt = bilateral ? authority.SubmitPaidExchangeCommand(command, wren, fixture.NextId())
            : authority.SubmitMarketCommand(command, wren, fixture.NextId());
        Assert.Equal(WorldActionRejection.NotAuthorized, receipt.Rejection);
        AssertOwnersUnchanged(state, authority.ReadStateForHost());
    }

    /// <summary>
    /// Structural admission promises a market quote's whole fixed terms. The same root with Wren's rule
    /// re-authored to a smaller due, every price unchanged and its quote record re-pinned to that rule, is
    /// refused, while the genuine root decodes.
    /// </summary>
    private static void AssertAdmissionRefusesAnotherDue(WorldSnapshot issued, MarketBinding market)
    {
        WorldMarketBook book = issued.Coordination!.Markets!;
        MarketPricePolicy rule = market.Policy;
        var lighter = new MarketPricePolicy(rule.Source, rule.StockingTarget, rule.HoldingLimit, rule.Softening,
            rule.MinimumUnitPrice, rule.MaximumUnitPriceBound, rule.AskMarkup, rule.BidMarkdown, rule.UnitFee - 1,
            rule.FeePayeeAccountId, rule.MaximumLot, rule.QuoteLifetimeSteps);
        var relabeled = new MarketBinding(market.MarketId, market.MerchantActorId, market.CustodianId, market.AccountId,
            market.Commodity, market.HomePlaceId, lighter);
        MarketQuoteRecord record = Assert.Single(book.Quotes);
        Assert.Equal(rule.Ask(record.StockBasis, 1), lighter.Ask(record.StockBasis, 1));
        var forged = new WorldMarketBook(book.Markets.Select(value => value.MarketId == market.MarketId ? relabeled : value),
            [new MarketQuoteRecord(record.QuoteId, record.MarketId, lighter.Version, record.StockBasis)], book.Knowledge,
            book.Disclosures);
        byte[] world = WorldSnapshotCodec.Encode(issued);
        byte[] original = WorldMarketCodec.Encode(book), replacement = WorldMarketCodec.Encode(forged);
        Assert.Equal(original.Length, replacement.Length);
        int at = IndexOf(world, original);
        byte[] spliced = [.. world[..at], .. replacement, .. world[(at + original.Length)..]];
        Assert.Equal(issued.ContentHash, WorldSnapshotCodec.Decode(world).ContentHash);
        Exception? refused = Record.Exception(() => WorldSnapshotCodec.Decode(spliced));
        Assert.True(refused is FormatException or ArgumentException, refused?.ToString() ?? "The forged due was admitted.");
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        int found = -1;
        for (int start = 0; start + needle.Length <= haystack.Length; start++)
        {
            if (haystack.AsSpan(start, needle.Length).SequenceEqual(needle))
            {
                Assert.Equal(-1, found);
                found = start;
            }
        }
        Assert.True(found >= 0, "The market owner frame must appear in the encoded root.");
        return found;
    }

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
}
