using OpenFantasy.Core.Content;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Observations;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Explicit fresh selection and structural admission of the local-market world (profile 33): the paid world
/// plus admitted market bindings, market quote records and actor price knowledge. It composes only an
/// unplayed paid genesis; it never upgrades a lived world or relabels a root of another interpretation,
/// creates no stock and mints no money. Structural admission authenticates no history.
/// </summary>
public static class WorldMarketAdmission
{
    public const ushort ProfileVersion = 33;

    /// <summary>
    /// Admits the authored markets into an unplayed profile-32 genesis. Merchants trade only with stock and
    /// funds the genesis already admitted to them or that they later acquire through real world operations.
    /// </summary>
    public static WorldSnapshot CreateFresh(WorldSnapshot paidGenesis, IEnumerable<MarketBinding> markets)
    {
        ArgumentNullException.ThrowIfNull(paidGenesis);
        ArgumentNullException.ThrowIfNull(markets);
        if (paidGenesis.ProfileVersion != WorldPaidExchangeAdmission.ProfileVersion || paidGenesis.Revision != 0
            || paidGenesis.AdvancingSteps != 0 || paidGenesis.Coordination is not { Anchor: null, PaidExchange: { } paid, Proposals: { } proposals } coordination
            || paid.Open.Count != 0 || paid.Outcomes.Count != 0 || paid.IssuerRevisions.Count != 0
            || proposals.Open.Count != 0 || proposals.Outcomes.Count != 0 || coordination.ConsentedMaterial!.Transfers.Count != 0)
        {
            throw new ArgumentException("Markets are admitted only into an unplayed fresh paid genesis.", nameof(paidGenesis));
        }
        var book = new WorldMarketBook(markets.OrderBy(value => value.MarketId), [], []);
        if (book.Markets.Count == 0)
        {
            throw new ArgumentException("A market world admits at least one market.", nameof(markets));
        }
        var admission = ContentHash.Compute(CanonicalEncodingV1.Frame("world.market-admission.v1",
            paidGenesis.ContentHash.ToByteArray(), book.ContentHash.ToByteArray()));
        WorldSnapshot basis = paidGenesis;
        var world = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle,
            care: basis.Care, resourceHistory: WorldResourceHistory.CreateFresh(admission, basis.Progression!),
            coordination: new WorldCoordinatedContinuation(coordination.Sources,
                consentedMaterial: new WorldConsentedMaterialHistory([]), proposals: new WorldMaterialProposalBook([], []),
                paidExchange: WorldPaidExchangeBook.Empty, markets: book));
        _ = WorldSnapshotCodec.Encode(world);
        return world;
    }

    /// <summary>
    /// Every binding names its admitted merchant with presence and the same home-place identity, the storage
    /// and account it owns and a fee payee that exists; every quote record names a quote of this segment
    /// whose fixed terms are exactly the rule's: its unit price at the recorded basis and allowance, its
    /// window and its market due and payee; every observation and disclosure names actual residents and lies
    /// within this world's time, and every disclosure names its seller's own storage and account. It does
    /// not prove that an observation, disclosure or quote was actually delivered.
    /// </summary>
    internal static void Validate(WorldSnapshot world, WorldMarketBook book, ulong boundary)
    {
        var progression = world.Progression!;
        WorldPaidExchangeBook quotes = world.Coordination!.PaidExchange!;
        bool OwnsStorage(EntityId actor, EntityId custodian) =>
            world.Inventory.Policies.Any(value => value.CustodianId == custodian && value.OwnerActorId == actor);
        bool OwnsAccount(EntityId actor, EntityId account) =>
            progression.Currency.Accounts.Any(value => value.AccountId == account && value.ActorId == actor);
        foreach (MarketBinding market in book.Markets)
        {
            if (world.Space.Actors.FirstOrDefault(value => value.ActorId == market.MerchantActorId) is not { } merchant
                || merchant.HomePlaceId != market.HomePlaceId
                || !OwnsStorage(market.MerchantActorId, market.CustodianId) || !OwnsAccount(market.MerchantActorId, market.AccountId)
                || market.Policy.FeePayeeAccountId is { } payee && progression.Currency.Accounts.All(value => value.AccountId != payee))
            {
                throw new ArgumentException("A market binds an admitted merchant's own storage, account and an existing fee payee.");
            }
        }
        foreach (MarketQuoteRecord record in book.Quotes)
        {
            // A record lives exactly as long as its quote is open or closed inside this segment.
            WorldPaidQuote? quote = quotes.FindOpen(record.QuoteId) ?? quotes.FindOutcome(record.QuoteId)?.Quote;
            MarketBinding market = book.FindMarket(record.MarketId)!;
            if (quote is null)
            {
                throw new ArgumentException("A market quote record names a quote of its own segment.");
            }
            SpotQuote terms = quote.Quote;
            bool sells = terms.Side == SpotQuoteSide.IssuerSells;
            ulong? derived = sells ? market.Policy.Ask(record.StockBasis, terms.QuantityAllowance)
                : market.Policy.Bid(record.StockBasis, terms.QuantityAllowance);
            if (terms.IssuerActorId != market.MerchantActorId || !terms.Definition.Equals(market.Commodity)
                || (sells ? terms.SellerCustodianId : terms.BuyerCustodianId) != market.CustodianId
                || (sells ? terms.SellerAccountId : terms.BuyerAccountId) != market.AccountId
                || derived != terms.UnitPrice || terms.ExpiresAtStep - terms.IssuedAtStep > market.Policy.QuoteLifetimeSteps
                || !WorldMarketResolver.FeeMatches(market.Policy, terms, sells ? terms.BuyerAccountId : terms.SellerAccountId))
            {
                throw new ArgumentException("A market quote's fixed terms equal its rule's price, allowance, window and due.");
            }
        }
        foreach (MarketPriceObservation observation in book.Knowledge)
        {
            if (world.Residents.All(value => value.ActorId != observation.ObserverActorId)
                || observation.ObservedSequence > world.Revision || observation.ObservedAdvancingStep > world.AdvancingSteps
                || observation.ObservedSequence <= boundary && world.Coordination!.Anchor is null)
            {
                throw new ArgumentException("Price knowledge belongs to a resident and lies within this world's time.");
            }
        }
        foreach (MarketLotDisclosure disclosure in book.Disclosures)
        {
            MarketBinding market = book.FindMarket(disclosure.MarketId)!;
            if (world.Residents.All(value => value.ActorId != disclosure.SellerActorId)
                || !OwnsStorage(disclosure.SellerActorId, disclosure.SellerCustodianId)
                || !OwnsAccount(disclosure.SellerActorId, disclosure.SellerAccountId)
                || disclosure.DisclosedSequence > world.Revision || disclosure.DisclosedAtStep > world.AdvancingSteps
                || disclosure.ExpiresAtStep - disclosure.DisclosedAtStep > market.Policy.QuoteLifetimeSteps
                || disclosure.DisclosedSequence <= boundary && world.Coordination!.Anchor is null)
            {
                throw new ArgumentException("A disclosure shows its resident seller's own lot within this world's time and window.");
            }
        }
    }
}
