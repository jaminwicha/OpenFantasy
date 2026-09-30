using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Tests.Production;
using OpenFantasy.Core.Worlds;
using ProgressionFixture = OpenFantasy.Core.Tests.Progression.ProgressionFixture;

namespace OpenFantasy.Core.Tests.Markets;

/// <summary>
/// A trader, two merchant quote issuers and a guild fee account with actually owned raw stock, a real
/// crafting recipe and funded finite currency. Nothing is reset between steps of a journey.
/// </summary>
internal sealed class SpotExchangeFixture
{
    internal static EntityId Id(ulong value) => new(0, value);
    internal static readonly EntityId World = Id(1), Branch = Id(2);
    internal static readonly EntityId Trader = Id(10), MerchantA = Id(11), MerchantB = Id(12);
    internal static readonly TrustedCommandDelivery TraderDelivery = new(Id(20), Id(30));
    internal static readonly TrustedCommandDelivery MerchantADelivery = new(Id(21), Id(31));
    internal static readonly TrustedCommandDelivery MerchantBDelivery = new(Id(22), Id(32));
    internal static readonly EntityId Pack = Id(60), StallA = Id(61), StallB = Id(62);
    internal static readonly EntityId GuildAccount = Id(200), TraderAccount = Id(201), AAccount = Id(202), BAccount = Id(203);
    internal static readonly EntityId StallBerries = Id(74);
    internal const ulong Supply = 230;

    internal SpotExchangeFixture(ulong packCapacity = 64, ulong traderGil = 100, LotProtection stallProtection = LotProtection.None)
    {
        var custody = new CustodySnapshot(World, Branch, 0,
            [new(Pack, packCapacity, 0), new(StallA, 64, 0), new(StallB, 64, 0)],
            [new(Id(70), Pack, InstantCraftFixture.Berries, new SourceInterval(Id(71), 0, 6), 0),
             new(Id(72), Pack, InstantCraftFixture.Wood, new SourceInterval(Id(73), 0, 4), 0),
             new(StallBerries, StallA, InstantCraftFixture.Berries, new SourceInterval(Id(75), 0, 10), 0, stallProtection)]);
        Inventory = new CountedAuthorityState(custody, 20, [Trader, MerchantA, MerchantB],
            [new(TraderDelivery.PrincipalId, TraderDelivery.SessionId, Trader),
             new(MerchantADelivery.PrincipalId, MerchantADelivery.SessionId, MerchantA),
             new(MerchantBDelivery.PrincipalId, MerchantBDelivery.SessionId, MerchantB)],
            [new(Pack, Trader, CustodyDepositRule.OwnerOnly), new(StallA, MerchantA, CustodyDepositRule.OwnerOnly),
             new(StallB, MerchantB, CustodyDepositRule.OwnerOnly)],
            [new(Id(71), InstantCraftFixture.Berries, 6), new(Id(73), InstantCraftFixture.Wood, 4),
             new(Id(75), InstantCraftFixture.Berries, 10)]);
        Production = new ProductionSnapshot(World, Branch, 0, [InstantCraftFixture.Recipe()], [], []);
        Production.ValidateForAuthority(Inventory);
        LevelProgressionDefinition curve = ProgressionFixture.Curve();
        ActorStatProfileDefinition squire = ProgressionFixture.Squire(), unassigned = ProgressionFixture.Unassigned();
        var currency = new CurrencyLedger(World, Branch, 0, ProgressionFixture.Gil(), Supply,
            [new(GuildAccount, CurrencyAccountKind.Reserve, null, 0, 0),
             new(TraderAccount, CurrencyAccountKind.Actor, Trader, traderGil, 0),
             new(AAccount, CurrencyAccountKind.Actor, MerchantA, 50, 0),
             new(BAccount, CurrencyAccountKind.Actor, MerchantB, Supply - 50 - traderGil, 0)]);
        Progression = new ProgressionSnapshot(World, Branch, 0, [squire, unassigned], [curve],
            [new(Trader, 0, squire, curve, 1, 0, 60, 10), new(MerchantA, 0, unassigned, curve, 1, 0, 1, 0),
             new(MerchantB, 0, unassigned, curve, 1, 0, 1, 0)], currency);
        Book = SpotQuoteBook.Empty(World, Branch);
    }

    internal CountedAuthorityState Inventory { get; set; }
    internal ProductionSnapshot Production { get; set; }
    internal ProgressionSnapshot Progression { get; set; }
    internal SpotQuoteBook Book { get; set; }
    internal HashSet<EntityId> History { get; } = [];
    internal bool Occupied(EntityId identity) => History.Contains(identity);

    /// <summary>Merchant A offers stall berries at 4 gil each plus a 1 gil guild fee per unit.</summary>
    internal SpotQuote BerryOffer(ulong quoteId = 900, ulong allowance = 5, ulong expiresAt = 100, ulong price = 4) =>
        new(Id(quoteId), World, Branch, MerchantA, MerchantADelivery.PrincipalId, MerchantADelivery.SessionId, Trader,
            SpotQuoteSide.IssuerSells, SpotLotKind.CountedSource, StallBerries, InstantCraftFixture.Berries,
            Inventory.Custody.GetLot(StallBerries).Revision, StallA, Pack, Progression.Currency.Currency.DefinitionId,
            Progression.Currency.Currency.Version, AAccount, TraderAccount, price, [new SpotFee(GuildAccount, 1)],
            allowance, Book.GetIssuerRevision(MerchantA), 10, expiresAt);

    /// <summary>Merchant B bids for the trader's crafted rations at 9 gil each, without fees.</summary>
    internal SpotQuote RationBid(TransformedLot rations, ulong quoteId = 901, ulong allowance = 2) =>
        new(Id(quoteId), World, Branch, MerchantB, MerchantBDelivery.PrincipalId, MerchantBDelivery.SessionId, Trader,
            SpotQuoteSide.IssuerBuys, SpotLotKind.Produced, rations.LotId, rations.Definition, rations.Revision, Pack,
            StallB, Progression.Currency.Currency.DefinitionId, Progression.Currency.Currency.Version, TraderAccount,
            BAccount, 9, [], allowance, Book.GetIssuerRevision(MerchantB), 10, 50);

    internal void Issue(SpotQuote quote, TrustedCommandDelivery delivery, ulong step = 10)
    {
        SpotQuoteIssuePlan plan = new SpotExchangeAuthority().Issue(Inventory, Production, Progression, Book, quote, delivery, step, Occupied);
        Assert.Equal(SpotExchangeRejection.None, plan.Rejection);
        Book = plan.ApplyTo(Inventory, Production, Progression, Book);
    }

    internal static SpotAcceptance Accept(SpotQuote quote, ulong quantity, ulong acceptanceId = 950,
        TrustedCommandDelivery? claims = null) => new(Id(acceptanceId), (claims ?? TraderDelivery).PrincipalId,
        (claims ?? TraderDelivery).SessionId, quote.QuoteId, quote.ContentHash, quantity, quote.PaymentFor(quantity),
        quote.FeesFor(quantity));

    internal SpotExchangePlan Decide(SpotAcceptance acceptance, TrustedCommandDelivery? delivery = null, ulong step = 20) =>
        new SpotExchangeAuthority().Accept(Inventory, Production, Progression, Book, acceptance,
            delivery ?? TraderDelivery, step, Occupied);

    internal void Commit(SpotExchangePlan plan)
    {
        Assert.Equal(SpotExchangeOutcome.Applied, plan.Outcome);
        (Inventory, Production, Progression, Book) = plan.ApplyTo(Inventory, Production, Progression, Book);
    }

    /// <summary>The trader crafts rations from their own berries and wood through the real planner.</summary>
    internal TransformedLot CraftRations(ulong command = 6000)
    {
        var request = new CraftRequest(Production.Recipes[0].DefinitionId, Production.Recipes[0].Version, Production.Revision,
            Pack, Inventory.Custody.GetStorage(Pack).Revision,
            [new(IngredientLotKind.Source, Id(70), Inventory.Custody.GetLot(Id(70)).Revision, 2),
             new(IngredientLotKind.Source, Id(72), Inventory.Custody.GetLot(Id(72)).Revision, 1)]);
        CraftPlan craft = new InstantCraftPlanner().Plan(Inventory, Production, TraderDelivery, Trader, Id(command), request, Occupied);
        Assert.Equal(WorldActionRejection.None, craft.Rejection);
        (Inventory, Production) = (craft.CandidateInventory, craft.CandidateProduction);
        History.Add(Id(command));
        return Production.Lots.Single(value => value.Definition == InstantCraftFixture.Rations);
    }

    internal static CountedAuthorityState WithCustody(CountedAuthorityState inventory, CustodySnapshot custody) =>
        new(custody, inventory.AdmittedUnitCapacity, inventory.Actors, inventory.Controls, inventory.Policies,
            inventory.Sources, inventory.ReservationIds);

    internal ulong Balance(EntityId account) => Progression.GetAccount(account).Balance;

    internal ulong TotalBalance() => Progression.Currency.Accounts.Aggregate(0UL, (total, value) => total + value.Balance);
}
