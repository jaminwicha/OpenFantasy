using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Observations;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Decides profile-33 market submissions and the market condition of a market quote's acceptance. A market
/// quote is an ordinary paid quote whose terms the world derives from the market rule and actual usable
/// stock; its settlement, supersession, withdrawal and expiry stay with the paid owner. Every submission
/// first passes the shared command contract: current control, the caller's own presence revision and an
/// advancing unheld step with a ready actor. A market is a mobile actor-market: it trades wherever its
/// merchant stands, and in-person acts require the merchant's live presence and actual mutual reach.
/// </summary>
internal static class WorldMarketResolver
{
    internal static (WorldPaidExchangePlan Plan, WorldMarketBook Markets) Resolve(WorldSnapshot state, WorldStepBatch batch,
        CountedAuthorityState inventory, LocalSpatialSnapshot space, ProductionSnapshot production,
        ProgressionSnapshot progression, WorldPaidExchangeBook quotes, WorldMarketBook markets, bool held,
        WorldActionCommand action, uint actionIndex, ulong sequence, IReadOnlySet<EntityId> worked, Func<EntityId, bool> occupied)
    {
        (WorldPaidExchangePlan, WorldMarketBook) Refuse(WorldActionRejection reason) =>
            (new(inventory, production, progression, quotes, reason, [], null, null), markets);

        var delivery = new TrustedCommandDelivery(action.PrincipalId, action.OriginSessionId);
        if (action.WorldId != state.WorldId || action.BranchId != state.BranchId || !inventory.CanControl(delivery, action.ActorId))
        {
            return Refuse(WorldActionRejection.NotAuthorized);
        }
        ActorPresence? presence = space.Actors.FirstOrDefault(value => value.ActorId == action.ActorId);
        if (presence is null)
        {
            return Refuse(WorldActionRejection.Unavailable);
        }
        if (presence.Revision != action.ExpectedActorRevision)
        {
            return Refuse(WorldActionRejection.RevisionConflict);
        }
        if (batch.Kind != WorldBatchKind.AdvancingStep || held || !WorldActorAvailability.PermitsAction(state, action))
        {
            return Refuse(WorldActionRejection.Unavailable);
        }
        return action.Intent switch
        {
            IssueMarketQuoteIntent issue => Issue(state, batch, inventory, space, production, progression, quotes, markets,
                held, action, issue, actionIndex, sequence, worked, occupied, Refuse),
            ObserveMarketIntent observe => Observe(state, inventory, space, production, progression, quotes, markets,
                action, observe, actionIndex, sequence, Refuse),
            DiscloseMarketLotIntent disclose => Disclose(state, inventory, space, production, progression, quotes, markets,
                action, disclose, actionIndex, sequence, Refuse),
            WithdrawMarketLotDisclosureIntent withdraw => markets.FindDisclosure(action.ActorId, withdraw.MarketId) is { } shown
                ? (new(inventory, production, progression, quotes, WorldActionRejection.None, [], null, null), markets.Retract(shown))
                : Refuse(WorldActionRejection.Unavailable),
            _ => throw new ArgumentException("Only market submissions are decided here.", nameof(action))
        };
    }

    /// <summary>
    /// Whether an otherwise settleable acceptance of a market quote still matches the market state that priced
    /// it. A fixed offer is stale while the merchant's usable stock differs from its basis, its rule version
    /// changed or the merchant has no live presence. Stock is a condition, not a revision: an open quote can
    /// become settleable again when non-trade movements restore the same count, whereas a closed or filled
    /// quote never reopens. A stale offer is refused, never rewritten; its issuer may withdraw it and the
    /// fixed-world window closes it.
    /// </summary>
    internal static bool IsCurrent(MarketQuoteRecord record, WorldMarketBook markets, CountedAuthorityState inventory,
        ProductionSnapshot production, LocalSpatialSnapshot space)
    {
        MarketBinding? market = markets.FindMarket(record.MarketId);
        return market is not null && space.Actors.Any(value => value.ActorId == market.MerchantActorId)
            && market.Policy.Version == record.PolicyVersion
            && market.CountUsableStock(inventory, production.Lots) == record.StockBasis;
    }

    private static (WorldPaidExchangePlan, WorldMarketBook) Issue(WorldSnapshot state, WorldStepBatch batch,
        CountedAuthorityState inventory, LocalSpatialSnapshot space, ProductionSnapshot production,
        ProgressionSnapshot progression, WorldPaidExchangeBook quotes, WorldMarketBook markets, bool held,
        WorldActionCommand action, IssueMarketQuoteIntent issue, uint actionIndex, ulong sequence,
        IReadOnlySet<EntityId> worked, Func<EntityId, bool> occupied,
        Func<WorldActionRejection, (WorldPaidExchangePlan, WorldMarketBook)> refuse)
    {
        // Only the market's own merchant issues under it; a missing market gives the same refusal.
        MarketBinding? market = markets.FindMarket(issue.MarketId);
        SpotQuote quote = issue.Quote;
        if (market is null || market.MerchantActorId != action.ActorId || quote.IssuerActorId != action.ActorId)
        {
            return refuse(WorldActionRejection.NotAuthorized);
        }
        bool sells = quote.Side == SpotQuoteSide.IssuerSells;
        EntityId custodian = sells ? quote.SellerCustodianId : quote.BuyerCustodianId;
        EntityId account = sells ? quote.SellerAccountId : quote.BuyerAccountId;
        EntityId counterpartAccount = sells ? quote.BuyerAccountId : quote.SellerAccountId;
        MarketPricePolicy policy = market.Policy;
        if (custodian != market.CustodianId || account != market.AccountId || !quote.Definition.Equals(market.Commodity)
            || quote.ExpiresAtStep - quote.IssuedAtStep > policy.QuoteLifetimeSteps || quote.QuantityAllowance > policy.MaximumLot)
        {
            return refuse(WorldActionRejection.Unavailable);
        }
        // A buy quote pins the counterparty's private lot. Before anything about that lot is read, the seller
        // must have shown exactly that lot to this market; an unknown, foreign or unshown lot refuses alike.
        if (!sells && !markets.DisclosesTo(quote, market.MarketId, state.AdvancingSteps))
        {
            return refuse(WorldActionRejection.NotAuthorized);
        }
        if (sells)
        {
            // A sell quote inspects only the merchant's own usable lot of the commodity; any other lot refuses alike.
            if (FindLot(inventory, production, quote.LotKind, quote.LotId) is not { } own || !own.Usable
                || own.Custodian != market.CustodianId || !own.Definition.Equals(market.Commodity))
            {
                return refuse(WorldActionRejection.Unavailable);
            }
        }
        ulong stock = market.CountUsableStock(inventory, production.Lots);
        ulong? derived = sells ? policy.Ask(stock, quote.QuantityAllowance) : policy.Bid(stock, quote.QuantityAllowance);
        if (derived is null)
        {
            // No usable stock to sell, no remaining demand to buy, or stock beyond the rule's domain.
            return refuse(WorldActionRejection.Unavailable);
        }
        if (quote.UnitPrice != derived || !FeeMatches(policy, quote, counterpartAccount))
        {
            return refuse(WorldActionRejection.RevisionConflict);
        }
        // Every remaining issuance rule is the shared paid contract under this same command.
        WorldActionCommand paid = new(action.CommandId, action.PrincipalId, action.OriginSessionId, action.ActorId,
            action.WorldId, action.BranchId, action.ExpectedActorRevision, new IssueSpotQuoteIntent(quote));
        WorldPaidExchangePlan plan = WorldPaidExchangeResolver.Resolve(state, batch, inventory, space, production,
            progression, quotes, held, paid, actionIndex, sequence, worked, occupied,
            candidate => markets.DisclosesTo(candidate, market.MarketId, state.AdvancingSteps));
        if (plan.Rejection != WorldActionRejection.None)
        {
            return (plan, markets);
        }
        return (plan, markets.RecordQuote(new MarketQuoteRecord(quote.QuoteId, market.MarketId, policy.Version, stock)));
    }

    /// <summary>The market due is exactly the rule's, paid to its authored payee and never to the counterparty.</summary>
    internal static bool FeeMatches(MarketPricePolicy policy, SpotQuote quote, EntityId counterpartAccount) =>
        policy.FeePayeeAccountId is { } payee
            ? quote.Fees.Count == 1 && quote.Fees[0].PayeeAccountId == payee && quote.Fees[0].UnitAmount == policy.UnitFee
                && payee != counterpartAccount
            : quote.Fees.Count == 0;

    private static (WorldPaidExchangePlan, WorldMarketBook) Observe(WorldSnapshot state, CountedAuthorityState inventory,
        LocalSpatialSnapshot space, ProductionSnapshot production, ProgressionSnapshot progression,
        WorldPaidExchangeBook quotes, WorldMarketBook markets, WorldActionCommand action, ObserveMarketIntent observe,
        uint actionIndex, ulong sequence, Func<WorldActionRejection, (WorldPaidExchangePlan, WorldMarketBook)> refuse)
    {
        // A missing market and a market out of reach refuse alike, so a guessed identity discloses nothing.
        MarketBinding? market = markets.FindMarket(observe.MarketId);
        if (market is null || !MutuallyReachable(space, action.ActorId, market))
        {
            return refuse(WorldActionRejection.OutOfReach);
        }
        ActorPresence merchant = space.GetActor(market.MerchantActorId);
        if (merchant.Revision != observe.ExpectedMerchantRevision)
        {
            return refuse(WorldActionRejection.RevisionConflict);
        }
        // A trading merchant is alive and able to show its goods where it actually stands.
        if (!IsTrading(state, progression, market))
        {
            return refuse(WorldActionRejection.Unavailable);
        }
        MarketPriceObservation? prior = markets.FindObservation(action.ActorId, market.MarketId);
        if (prior is not null && prior.ObservedSequence >= sequence)
        {
            return refuse(WorldActionRejection.Unavailable);
        }
        MarketPostedTerms terms = market.PostedTerms(market.CountUsableStock(inventory, production.Lots));
        var observation = new MarketPriceObservation(action.ActorId, market.MarketId, market.MerchantActorId,
            market.Commodity, market.Policy.Version, terms.AskUnitPrice, terms.BidUnitPrice, terms.UnitFee, sequence,
            checked(state.AdvancingSteps + 1), action.CommandId, actionIndex);
        // Observation is a perceptual act: it moves nothing, reserves nothing and spends no work opportunity.
        return (new(inventory, production, progression, quotes, WorldActionRejection.None, [], null, null),
            markets.Learn(observation));
    }

    /// <summary>
    /// The seller shows one of its own usable lots of the market's commodity to the merchant in person. It
    /// reveals that lot, the units shown and the chosen payment account to that merchant only, for a window
    /// no longer than the market's quote lifetime. It moves, reserves and pays nothing and spends no work.
    /// </summary>
    private static (WorldPaidExchangePlan, WorldMarketBook) Disclose(WorldSnapshot state, CountedAuthorityState inventory,
        LocalSpatialSnapshot space, ProductionSnapshot production, ProgressionSnapshot progression,
        WorldPaidExchangeBook quotes, WorldMarketBook markets, WorldActionCommand action, DiscloseMarketLotIntent disclose,
        uint actionIndex, ulong sequence, Func<WorldActionRejection, (WorldPaidExchangePlan, WorldMarketBook)> refuse)
    {
        MarketBinding? market = markets.FindMarket(disclose.MarketId);
        if (market is null || !MutuallyReachable(space, action.ActorId, market))
        {
            return refuse(WorldActionRejection.OutOfReach);
        }
        if (!IsTrading(state, progression, market))
        {
            return refuse(WorldActionRejection.Unavailable);
        }
        // Only the caller's own lot in its own storage, paid into its own account, can be shown.
        (EntityId Custodian, CountedItemDefinition Definition, ulong Revision, ulong Quantity, bool Usable)? lot =
            FindLot(inventory, production, disclose.LotKind, disclose.LotId);
        bool owned = lot is { } found
            && inventory.Policies.Any(value => value.CustodianId == found.Custodian && value.OwnerActorId == action.ActorId)
            && progression.Currency.Accounts.Any(value => value.AccountId == disclose.SellerAccountId && value.ActorId == action.ActorId);
        if (!owned)
        {
            return refuse(WorldActionRejection.NotAuthorized);
        }
        var (custodian, definition, revision, quantity, usable) = lot!.Value;
        ulong publishedStep = checked(state.AdvancingSteps + 1);
        if (!definition.Equals(disclose.Definition) || !definition.Equals(market.Commodity) || !usable
            || disclose.Quantity > quantity || disclose.ExpiresAtStep <= publishedStep
            || disclose.ExpiresAtStep - publishedStep > market.Policy.QuoteLifetimeSteps)
        {
            return refuse(WorldActionRejection.Unavailable);
        }
        if (revision != disclose.ExpectedLotRevision)
        {
            return refuse(WorldActionRejection.RevisionConflict);
        }
        MarketLotDisclosure? prior = markets.FindDisclosure(action.ActorId, market.MarketId);
        if (prior is not null && prior.DisclosedSequence >= sequence)
        {
            return refuse(WorldActionRejection.Unavailable);
        }
        var shown = new MarketLotDisclosure(action.ActorId, market.MarketId, market.MerchantActorId, disclose.LotKind,
            disclose.LotId, definition, revision, disclose.Quantity, custodian, disclose.SellerAccountId, sequence,
            publishedStep, disclose.ExpiresAtStep, action.CommandId, actionIndex);
        return (new(inventory, production, progression, quotes, WorldActionRejection.None, [], null, null),
            markets.Disclose(shown));
    }

    private static bool MutuallyReachable(LocalSpatialSnapshot space, EntityId actorId, MarketBinding market) =>
        WorldMarketLocality.IsReachable(space, actorId, market);

    private static bool IsTrading(WorldSnapshot state, ProgressionSnapshot progression, MarketBinding market) =>
        WorldMarketLocality.IsTrading(state, progression, market);

    private static (EntityId Custodian, CountedItemDefinition Definition, ulong Revision, ulong Quantity, bool Usable)? FindLot(
        CountedAuthorityState inventory, ProductionSnapshot production, SpotLotKind kind, EntityId lotId)
    {
        if (kind == SpotLotKind.Produced)
        {
            return production.Lots.FirstOrDefault(value => value.LotId == lotId) is { } produced
                ? (produced.CustodianId, produced.Definition, produced.Revision, produced.Quantity,
                    produced.ReservedBy is null && produced.Protection == LotProtection.None)
                : null;
        }
        return inventory.Custody.Lots.FirstOrDefault(value => value.LotId == lotId) is { } raw
            ? (raw.CustodianId, raw.Definition, raw.Revision, raw.Quantity, raw.ReservedBy is null && raw.Protection == LotProtection.None)
            : null;
    }
}
