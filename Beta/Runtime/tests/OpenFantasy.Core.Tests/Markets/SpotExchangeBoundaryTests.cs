using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using static OpenFantasy.Core.Tests.Markets.SpotExchangeFixture;

namespace OpenFantasy.Core.Tests.Markets;

public sealed class SpotExchangeBoundaryTests
{
    [Fact]
    public void Seller_can_receive_a_named_fee_and_unrelated_accounts_and_actors_do_not_change()
    {
        var market = new SpotExchangeFixture();
        var originalActors = market.Progression.Actors.ToArray();
        CurrencyAccount untouched = market.Progression.GetAccount(BAccount);
        var quote = Copy(market.BerryOffer(), fees: [new(AAccount, 1), new(GuildAccount, 1)]);
        market.Issue(quote, MerchantADelivery);
        market.Commit(market.Decide(Accept(quote, 3)));
        Assert.Equal((82UL, 65UL, 3UL, Supply), (market.Balance(TraderAccount), market.Balance(AAccount),
            market.Balance(GuildAccount), market.TotalBalance()));
        Assert.Equal(1UL, market.Progression.GetAccount(AAccount).Revision);
        Assert.Equal(untouched, market.Progression.GetAccount(BAccount));
        Assert.Equal(originalActors, market.Progression.Actors);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Reserved_stock_is_refused_in_both_material_owners(bool produced)
    {
        var market = new SpotExchangeFixture();
        var rations = market.CraftRations();
        var inventory = market.Inventory;
        var reservation = Id(5000);
        var raw = inventory.Custody.Lots.Select(lot => !produced && lot.LotId == StallBerries
            ? new CountedSourceLot(lot.LotId, lot.CustodianId, lot.Definition, lot.Source, lot.Revision, lot.Protection, reservation) : lot);
        market.Inventory = new(new CustodySnapshot(World, Branch, inventory.Custody.Revision, inventory.Custody.Storages, raw),
            inventory.AdmittedUnitCapacity, inventory.Actors, inventory.Controls, inventory.Policies, inventory.Sources, [reservation]);
        if (produced)
        {
            market.Production = new(World, Branch, market.Production.Revision, market.Production.Recipes, market.Production.Batches,
                market.Production.Lots.Select(lot => new TransformedLot(lot.LotId, lot.CustodianId, lot.Definition,
                    lot.Output, lot.Revision, lot.Protection, reservation)));
        }
        var quote = produced ? market.RationBid(rations) : market.BerryOffer();
        market.Issue(quote, produced ? MerchantBDelivery : MerchantADelivery);
        Refused(market, market.Decide(Accept(quote, 1)), SpotExchangeRejection.ProtectedStock);
    }

    [Fact]
    public void Quote_rechecks_lot_revision_definition_and_current_market_revision()
    {
        var market = new SpotExchangeFixture();
        var stale = Copy(market.BerryOffer(), expectedRevision: 1);
        market.Issue(stale, MerchantADelivery);
        Refused(market, market.Decide(Accept(stale, 1)), SpotExchangeRejection.RevisionConflict);
        var otherPin = Copy(market.BerryOffer(901), definition: new(Id(6001), ContentHash.Compute("other"u8)));
        market.Issue(otherPin, MerchantADelivery);
        Refused(market, market.Decide(Accept(otherPin, 1)), SpotExchangeRejection.StockUnavailable);
        var first = market.BerryOffer(902);
        var competitor = market.BerryOffer(903);
        market.Issue(first, MerchantADelivery);
        market.Issue(competitor, MerchantADelivery);
        market.Commit(market.Decide(Accept(first, 1)));
        Refused(market, market.Decide(Accept(competitor, 1, 951)), SpotExchangeRejection.RevisionConflict);
    }

    [Theory]
    [InlineData("currency")] [InlineData("account")] [InlineData("progression")]
    [InlineData("custody")] [InlineData("storage")] [InlineData("lot")]
    public void Revision_overflow_never_leaks_a_partial_goods_or_payment_change(string owner)
    {
        var market = new SpotExchangeFixture();
        var inventory = market.Inventory;
        var custody = inventory.Custody;
        market.Inventory = WithCustody(inventory, new(World, Branch, owner == "custody" ? ulong.MaxValue : custody.Revision,
            custody.Storages.Select(value => owner == "storage" && value.CustodianId == Pack
                ? value with { Revision = ulong.MaxValue } : value),
            custody.Lots.Select(value => owner == "lot" && value.LotId == StallBerries
                ? new CountedSourceLot(value.LotId, value.CustodianId, value.Definition, value.Source, ulong.MaxValue) : value)));
        var progression = market.Progression;
        var currency = progression.Currency;
        var ledger = new CurrencyLedger(World, Branch, owner == "currency" ? ulong.MaxValue : currency.Revision,
            currency.Currency, currency.AdmittedSupply, currency.Accounts.Select(value => owner == "account" && value.AccountId == TraderAccount
                ? new CurrencyAccount(value.AccountId, value.Kind, value.ActorId, value.Balance, ulong.MaxValue) : value));
        market.Progression = new(World, Branch, owner == "progression" ? ulong.MaxValue : progression.Revision,
            progression.Profiles, progression.Curves, progression.Actors, ledger);
        var quote = market.BerryOffer();
        market.Issue(quote, MerchantADelivery);
        Refused(market, market.Decide(Accept(quote, 1)), SpotExchangeRejection.Overflow);
    }

    [Fact]
    public void Issuance_checks_production_identity_and_pins_every_read_owner()
    {
        var market = new SpotExchangeFixture();
        var rations = market.CraftRations();
        var collision = Copy(market.BerryOffer(), quoteId: rations.LotId);
        var authority = new SpotExchangeAuthority();
        Assert.Equal(SpotExchangeRejection.IdentityConflict, authority.Issue(market.Inventory, market.Production,
            market.Progression, market.Book, collision, MerchantADelivery, 10, market.Occupied).Rejection);
        var quote = market.BerryOffer();
        Assert.Equal(SpotExchangeRejection.NotAuthorized, authority.Issue(market.Inventory, market.Production,
            market.Progression, market.Book, quote, TraderDelivery, 10, market.Occupied).Rejection);
        Assert.Throws<ArgumentNullException>(() => authority.Issue(market.Inventory, market.Production,
            market.Progression, market.Book, quote, null!, 10, market.Occupied));
        var plan = authority.Issue(market.Inventory, market.Production, market.Progression, market.Book,
            quote, MerchantADelivery, 10, market.Occupied);
        Assert.Throws<CustodyConflictException>(() => plan.ApplyTo(new SpotExchangeFixture(packCapacity: 63).Inventory,
            market.Production, market.Progression, market.Book));
        Assert.Throws<CustodyConflictException>(() => plan.ApplyTo(market.Inventory, new SpotExchangeFixture().Production,
            market.Progression, market.Book));
        Assert.Throws<CustodyConflictException>(() => plan.ApplyTo(market.Inventory, market.Production,
            new SpotExchangeFixture(traderGil: 99).Progression, market.Book));
        market.Issue(market.BerryOffer(904), MerchantADelivery);
        Assert.Throws<CustodyConflictException>(() => plan.ApplyTo(market.Inventory, market.Production,
            market.Progression, market.Book));
    }

    [Fact]
    public void Missing_delivery_fees_tampering_and_spawn_collision_cannot_settle()
    {
        var market = new SpotExchangeFixture();
        var quote = market.BerryOffer();
        market.Issue(quote, MerchantADelivery);
        var acceptance = Accept(quote, 2);
        Assert.Throws<ArgumentNullException>(() => new SpotExchangeAuthority().Accept(market.Inventory,
            market.Production, market.Progression, market.Book, acceptance, null!, 20, market.Occupied));
        var tampered = new SpotAcceptance(acceptance.AcceptanceId, acceptance.PrincipalId, acceptance.OriginSessionId,
            acceptance.QuoteId, acceptance.QuoteHash, acceptance.Quantity, acceptance.Payment, 0);
        Refused(market, market.Decide(tampered), SpotExchangeRejection.NotAuthorized);
        market.History.Add(EntityId.DeriveSpawned(acceptance.AcceptanceId, CustodyTransferPlanner.SplitChildOrdinal));
        Refused(market, market.Decide(acceptance), SpotExchangeRejection.IdentityConflict);
        var huge = new SpotAcceptance(Id(953), TraderDelivery.PrincipalId, TraderDelivery.SessionId,
            quote.QuoteId, quote.ContentHash, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        Refused(market, market.Decide(huge), SpotExchangeRejection.NotAuthorized);
    }

    internal static SpotQuote Copy(SpotQuote q, IEnumerable<SpotFee>? fees = null, ulong? expectedRevision = null,
        CountedItemDefinition? definition = null, EntityId? quoteId = null) => new(quoteId ?? q.QuoteId, q.WorldId,
        q.BranchId, q.IssuerActorId, q.IssuerPrincipalId, q.IssuerSessionId, q.CounterpartyActorId, q.Side, q.LotKind,
        q.LotId, definition ?? q.Definition, expectedRevision ?? q.ExpectedLotRevision, q.SellerCustodianId,
        q.BuyerCustodianId, q.CurrencyDefinitionId, q.CurrencyVersion, q.SellerAccountId, q.BuyerAccountId,
        q.UnitPrice, fees ?? q.Fees, q.QuantityAllowance, q.MarketRevision, q.IssuedAtStep, q.ExpiresAtStep);

    private static void Refused(SpotExchangeFixture market, SpotExchangePlan plan, SpotExchangeRejection expected)
    {
        Assert.Equal((SpotExchangeOutcome.Rejected, expected), (plan.Outcome, plan.Rejection));
        Assert.Same(market.Inventory, plan.CandidateInventory);
        Assert.Same(market.Production, plan.CandidateProduction);
        Assert.Same(market.Progression, plan.CandidateProgression);
        Assert.Same(market.Book, plan.CandidateBook);
        Assert.Null(plan.Evidence);
    }
}
