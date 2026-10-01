using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Observations;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>
/// One actor's entry in its own bounded price memory: the market and commodity, the remembered single-unit
/// terms, when they were read and how many advancing steps old they are now. It carries no stock or balance.
/// </summary>
public sealed record MarketKnowledgeEntry(EntityId MarketId, EntityId MerchantActorId, CountedItemDefinition Commodity,
    ulong? AskUnitPrice, ulong? BidUnitPrice, ulong UnitFee, ulong ObservedAdvancingStep, ulong AgeInSteps);

/// <summary>
/// One live showing addressed to the caller's own market: exactly what its seller chose to show and for how
/// long. It is the merchant's only view of another actor's goods; nothing else in that inventory is listed.
/// </summary>
public sealed record MarketDisclosureEntry(EntityId SellerActorId, EntityId MarketId, SpotLotKind LotKind, EntityId LotId,
    CountedItemDefinition Definition, ulong LotRevision, ulong Quantity, ulong ExpiresAtStep);

/// <summary>
/// Application service shared by player and NPC controllers of the local-market world. It prepares a
/// merchant's rule-priced offer, an observer's in-person reading and a seller's in-person showing of one of
/// its own lots, and projects an actor's own price memory and the showings addressed to its own markets.
/// Every effect is a separate command through the trusted single-command submission; the world recomputes
/// and authorizes everything prepared here. Preparation returns one indistinguishable null for every
/// unknown, foreign, distant or unavailable subject, and never chooses another actor's lot or delivery.
/// </summary>
public sealed class MarketTradingService
{
    private readonly SqliteWorldAuthority _authority;

    public MarketTradingService(SqliteWorldAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
        _authority = authority;
    }

    /// <summary>
    /// The merchant's fixed offer at its market rule's price for the current usable stock. A sell offer pins
    /// one of the merchant's own usable lots of the commodity. A buy offer pins only the lot, revision,
    /// storage and payment account the counterparty itself showed to this market, within that showing's units
    /// and window. Returns null when the caller does not control the merchant, the rule posts no such quote,
    /// or the lot is not the merchant's own or not live-shown; those cases are indistinguishable.
    /// </summary>
    public WorldActionCommand? PrepareOffer(TrustedCommandDelivery merchantDelivery, EntityId marketId,
        EntityId counterpartyActorId, SpotQuoteSide side, EntityId lotId, ulong allowance, ulong lifetimeSteps,
        EntityId commandId)
    {
        ArgumentNullException.ThrowIfNull(merchantDelivery);
        WorldSnapshot state = _authority.ReadStateForHost();
        if (state.Coordination?.Markets is not { } book || book.FindMarket(marketId) is not { } market
            || !state.Inventory.CanControl(merchantDelivery, market.MerchantActorId)
            || counterpartyActorId == market.MerchantActorId || state.Residents.All(value => value.ActorId != counterpartyActorId)
            || lifetimeSteps == 0 || lifetimeSteps > market.Policy.QuoteLifetimeSteps)
        {
            return null;
        }
        ulong stock = market.CountUsableStock(state.Inventory, state.Production!.Lots);
        bool sells = side == SpotQuoteSide.IssuerSells;
        ulong? price = sells ? market.Policy.Ask(stock, allowance) : market.Policy.Bid(stock, allowance);
        ulong issuedAt = checked(state.AdvancingSteps + 1);
        ulong expiresAt = checked(issuedAt + lifetimeSteps);
        var ledger = state.Progression!.Currency;
        SpotQuote? quote = null;
        if (price is not { } unitPrice)
        {
            return null;
        }
        if (sells && OwnUsableLot(state, market, lotId) is { } own)
        {
            EntityId buyerAccount = ledger.GetActorAccount(counterpartyActorId).AccountId;
            if (market.Policy.FeePayeeAccountId == buyerAccount)
            {
                return null;
            }
            quote = Quote(state, market, merchantDelivery, commandId, counterpartyActorId, side, own.Kind, lotId,
                own.Revision, market.CustodianId, state.GetResident(counterpartyActorId).InventoryCustodianId,
                market.AccountId, buyerAccount, unitPrice, allowance, issuedAt, expiresAt);
        }
        else if (!sells && book.FindDisclosure(counterpartyActorId, marketId) is { } shown && shown.IsLiveAt(state.AdvancingSteps)
            && shown.LotId == lotId && allowance <= shown.Quantity && shown.ExpiresAtStep > issuedAt)
        {
            // Only what the seller itself showed is used; its other goods and accounts stay unread.
            if (market.Policy.FeePayeeAccountId == shown.SellerAccountId)
            {
                return null;
            }
            quote = Quote(state, market, merchantDelivery, commandId, counterpartyActorId, side, shown.LotKind, lotId,
                shown.LotRevision, shown.SellerCustodianId, market.CustodianId, shown.SellerAccountId, market.AccountId,
                unitPrice, allowance, issuedAt, Math.Min(expiresAt, shown.ExpiresAtStep));
        }
        return quote is null ? null : new WorldActionCommand(commandId, merchantDelivery.PrincipalId,
            merchantDelivery.SessionId, market.MerchantActorId, state.WorldId, state.BranchId,
            state.Space.GetActor(market.MerchantActorId).Revision, new IssueMarketQuoteIntent(marketId, quote));
    }

    /// <summary>
    /// The observer's in-person reading of one market. Only a market whose merchant is present, mutually
    /// reachable, alive and able to act is prepared, pinning the merchant presence revision the observer can
    /// currently see; every other market, including a guessed identity, gives the same null and reveals no
    /// revision. It supplies no price, and a refused reading changes no knowledge.
    /// </summary>
    public WorldActionCommand? PrepareObservation(TrustedCommandDelivery observerDelivery, EntityId observerActorId,
        EntityId marketId, EntityId commandId)
    {
        ArgumentNullException.ThrowIfNull(observerDelivery);
        WorldSnapshot state = _authority.ReadStateForHost();
        if (!state.Inventory.CanControl(observerDelivery, observerActorId)
            || WorldMarketLocality.FindAvailable(state, observerActorId, marketId) is not { } market)
        {
            return null;
        }
        return new WorldActionCommand(commandId, observerDelivery.PrincipalId, observerDelivery.SessionId, observerActorId,
            state.WorldId, state.BranchId, state.Space.GetActor(observerActorId).Revision,
            new ObserveMarketIntent(marketId, state.Space.GetActor(market.MerchantActorId).Revision));
    }

    /// <summary>
    /// The seller's in-person showing of one of its own usable lots of a locally available market's commodity,
    /// to be paid into its own actor account. Returns null for a foreign or unusable lot or an unavailable
    /// market. Showing consents to no sale; the seller still accepts or ignores any later buy quote.
    /// </summary>
    public WorldActionCommand? PrepareDisclosure(TrustedCommandDelivery sellerDelivery, EntityId sellerActorId,
        EntityId marketId, EntityId lotId, ulong quantity, ulong lifetimeSteps, EntityId commandId)
    {
        ArgumentNullException.ThrowIfNull(sellerDelivery);
        WorldSnapshot state = _authority.ReadStateForHost();
        if (!state.Inventory.CanControl(sellerDelivery, sellerActorId)
            || WorldMarketLocality.FindAvailable(state, sellerActorId, marketId) is not { } market
            || quantity == 0 || lifetimeSteps == 0 || lifetimeSteps > market.Policy.QuoteLifetimeSteps
            || OwnLot(state, sellerActorId, lotId) is not { } lot || !lot.Usable || !lot.Definition.Equals(market.Commodity)
            || quantity > lot.Quantity)
        {
            return null;
        }
        ulong expiresAt = checked(state.AdvancingSteps + 1 + lifetimeSteps);
        return new WorldActionCommand(commandId, sellerDelivery.PrincipalId, sellerDelivery.SessionId, sellerActorId,
            state.WorldId, state.BranchId, state.Space.GetActor(sellerActorId).Revision,
            new DiscloseMarketLotIntent(marketId, lot.Kind, lotId, lot.Definition, lot.Revision, quantity,
                state.Progression!.Currency.GetActorAccount(sellerActorId).AccountId, expiresAt));
    }

    /// <summary>The seller's withdrawal of its own current showing to one market, or null without control.</summary>
    public WorldActionCommand? PrepareDisclosureWithdrawal(TrustedCommandDelivery sellerDelivery, EntityId sellerActorId,
        EntityId marketId, EntityId commandId)
    {
        ArgumentNullException.ThrowIfNull(sellerDelivery);
        WorldSnapshot state = _authority.ReadStateForHost();
        if (!state.Inventory.CanControl(sellerDelivery, sellerActorId)
            || state.Coordination?.Markets?.FindDisclosure(sellerActorId, marketId) is null)
        {
            return null;
        }
        return new WorldActionCommand(commandId, sellerDelivery.PrincipalId, sellerDelivery.SessionId, sellerActorId,
            state.WorldId, state.BranchId, state.Space.GetActor(sellerActorId).Revision,
            new WithdrawMarketLotDisclosureIntent(marketId));
    }

    public WorldActionReceipt Submit(WorldActionCommand command, TrustedCommandDelivery delivery, EntityId causeId) =>
        _authority.SubmitMarketCommand(command, delivery, causeId);

    /// <summary>
    /// The holder's own remembered prices, aged against the current fixed-world step. Only the holder's
    /// current controller may read them; this never refreshes an observation or reveals a remote change.
    /// </summary>
    public IReadOnlyList<MarketKnowledgeEntry> ReadKnowledge(EntityId actorId, TrustedCommandDelivery delivery)
    {
        IReadOnlyList<MarketPriceObservation> known = _authority.ReadMarketKnowledge(actorId, delivery);
        ulong now = _authority.ReadStateForHost().AdvancingSteps;
        return known.Select(value => new MarketKnowledgeEntry(value.MarketId, value.MerchantActorId, value.Commodity,
            value.AskUnitPrice, value.BidUnitPrice, value.UnitFee, value.ObservedAdvancingStep, value.AgeAt(now))).ToList();
    }

    /// <summary>
    /// The live showings addressed to markets of a merchant the caller currently controls, or an empty list
    /// for any other caller. Only what each seller chose to show is projected.
    /// </summary>
    public IReadOnlyList<MarketDisclosureEntry> ReadDisclosures(EntityId merchantActorId, TrustedCommandDelivery delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        WorldSnapshot state = _authority.ReadStateForHost();
        if (state.Coordination?.Markets is not { } book || !state.Inventory.CanControl(delivery, merchantActorId))
        {
            return [];
        }
        return book.DisclosuresTo(merchantActorId).Where(value => value.IsLiveAt(state.AdvancingSteps))
            .Select(value => new MarketDisclosureEntry(value.SellerActorId, value.MarketId, value.LotKind, value.LotId,
                value.Definition, value.LotRevision, value.Quantity, value.ExpiresAtStep)).ToList();
    }

    private static SpotQuote Quote(WorldSnapshot state, MarketBinding market, TrustedCommandDelivery merchantDelivery,
        EntityId commandId, EntityId counterpartyActorId, SpotQuoteSide side, SpotLotKind kind, EntityId lotId,
        ulong lotRevision, EntityId sellerCustodian, EntityId buyerCustodian, EntityId sellerAccount, EntityId buyerAccount,
        ulong unitPrice, ulong allowance, ulong issuedAt, ulong expiresAt)
    {
        var ledger = state.Progression!.Currency;
        SpotFee[] fees = market.Policy.FeePayeeAccountId is { } payee ? [new SpotFee(payee, market.Policy.UnitFee)] : [];
        return new SpotQuote(WorldPaidQuote.DeriveIdentity(commandId), state.WorldId, state.BranchId,
            market.MerchantActorId, merchantDelivery.PrincipalId, merchantDelivery.SessionId, counterpartyActorId, side,
            kind, lotId, market.Commodity, lotRevision, sellerCustodian, buyerCustodian,
            ledger.Currency.DefinitionId, ledger.Currency.Version, sellerAccount, buyerAccount,
            unitPrice, fees, allowance, state.Coordination!.PaidExchange!.GetIssuerRevision(market.MerchantActorId),
            issuedAt, expiresAt);
    }

    /// <summary>One of the merchant's own usable lots of the market commodity, or null for any other lot.</summary>
    private static (SpotLotKind Kind, ulong Revision)? OwnUsableLot(WorldSnapshot state, MarketBinding market, EntityId lotId) =>
        OwnLot(state, market.MerchantActorId, lotId) is { Usable: true } lot && lot.Custodian == market.CustodianId
            && lot.Definition.Equals(market.Commodity) ? (lot.Kind, lot.Revision) : null;

    /// <summary>A lot in a storage the actor owns, or null; another actor's lot is never read.</summary>
    private static (SpotLotKind Kind, CountedItemDefinition Definition, ulong Revision, ulong Quantity, EntityId Custodian, bool Usable)?
        OwnLot(WorldSnapshot state, EntityId actorId, EntityId lotId)
    {
        bool Owns(EntityId custodian) =>
            state.Inventory.Policies.Any(value => value.CustodianId == custodian && value.OwnerActorId == actorId);
        if (state.Inventory.Custody.Lots.FirstOrDefault(value => value.LotId == lotId && Owns(value.CustodianId)) is { } raw)
        {
            return (SpotLotKind.CountedSource, raw.Definition, raw.Revision, raw.Quantity, raw.CustodianId,
                raw.ReservedBy is null && raw.Protection == LotProtection.None);
        }
        if (state.Production!.Lots.FirstOrDefault(value => value.LotId == lotId && Owns(value.CustodianId)) is TransformedLot produced)
        {
            return (SpotLotKind.Produced, produced.Definition, produced.Revision, produced.Quantity, produced.CustodianId,
                produced.ReservedBy is null && produced.Protection == LotProtection.None);
        }
        return null;
    }
}
