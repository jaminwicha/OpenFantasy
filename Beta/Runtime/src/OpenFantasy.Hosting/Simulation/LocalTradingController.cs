using System.Collections.Frozen;
using System.Collections.Concurrent;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>
/// Trading actions selected at one cutoff, the actors whose action opportunity they occupy and the participants
/// of valid acceptances, who neither work nor walk in the settling step.
/// </summary>
internal sealed record LocalTradingSelection(IReadOnlyList<WorldActionCommand> Actions,
    IReadOnlySet<EntityId> ParticipatingActors, IReadOnlySet<EntityId> SettlingActors);

/// <summary>
/// Host scheduling of local trade under the session cutoff. It owns the transient inquiry queue (the player's and
/// configured host shoppers'), lets each configured host merchant answer under its own control and lets each
/// configured host shopper show, ask and accept under its own control. Every effect is an ordinary world command;
/// the world recomputes and authorizes all of it.
/// </summary>
/// <remarks>
/// Service policy: waiting inquiries are answered in admission order (first in, first out). A merchant answers
/// at most one inquiry per cutoff and none while its opportunity is already taken (by a valid acceptance of one of
/// its quotes, a saved-gift settlement or choice, or its own shopper action); a skipped inquiry keeps its place.
/// Host shoppers decide in actor-identity order, which only breaks ties between requests created at the same
/// cutoff. Settlement order inside a batch remains the world's canonical actor order; it decides determinism, not
/// fair service.
/// <para>
/// Reconsideration policy: a host shopper acts only while its market is available to it in person, which it can
/// perceive where it stands; it never reads the merchant's stock or funds. Each attempt shows at most once and
/// asks at most once under identities derived from the attempt. A new attempt begins when the shopper's own
/// holding or balance changed, or when <see cref="LocalShopperPreference.ReconsiderAfterSteps"/> advancing steps
/// passed since the last attempt began, and never while its inquiry waits or an offer to it is open. Attempts are
/// session memory: a restart forgets them, so the first attempt after reopening is a new decision.
/// </para>
/// </remarks>
internal sealed class LocalTradingController
{
    private readonly EntityId _playerActorId;
    private readonly ulong _inquiryLifetime;
    private readonly FrozenDictionary<EntityId, LocalMerchantPreference> _merchants;
    private readonly LocalShopperPreference[] _shoppers;
    private readonly List<Inquiry> _waiting = [];
    private readonly Dictionary<EntityId, Attempt> _attempts = [];
    private readonly List<PlannedAnswer> _answers = [];
    private readonly ConcurrentDictionary<EntityId, LocalTradeInquiryView> _inquiries = new();
    private ulong _sequence;

    internal LocalTradingController(WorldSnapshot state, EntityId playerActorId, LocalTradingConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _playerActorId = playerActorId;
        _inquiryLifetime = configuration.InquiryLifetimeSteps;
        var merchants = new SortedDictionary<EntityId, LocalMerchantPreference>();
        var shoppers = new SortedDictionary<EntityId, LocalShopperPreference>();
        WorldMarketBook? markets = state.Coordination?.Markets;
        foreach (LocalMerchantPreference merchant in configuration.Merchants)
        {
            if (markets is null || merchants.Count == WorldStepBatch.MaximumActorsPerBatch
                || !merchants.TryAdd(merchant.MerchantActorId, merchant) || !IsHostActor(state, merchant.MerchantActorId)
                || markets.Markets.All(value => value.MerchantActorId != merchant.MerchantActorId))
            {
                throw new AuthorityDispatchException("Merchant preferences require unique admitted host actors that run a market.");
            }
        }
        foreach (LocalShopperPreference shopper in configuration.Shoppers)
        {
            if (markets is null || shoppers.Count == WorldStepBatch.MaximumActorsPerBatch
                || !shoppers.TryAdd(shopper.ShopperActorId, shopper) || !IsHostActor(state, shopper.ShopperActorId)
                || markets.FindMarket(shopper.MarketId) is not { } market || market.MerchantActorId == shopper.ShopperActorId)
            {
                throw new AuthorityDispatchException("Shopper preferences require unique admitted host actors and another merchant's market.");
            }
        }
        _merchants = merchants.ToFrozenDictionary();
        _shoppers = shoppers.Values.ToArray();
    }

    /// <summary>The original player's current or last inquiry, published for frame-safe reading.</summary>
    internal LocalTradeInquiryView? PlayerInquiry => ReadInquiry(_playerActorId);
    internal LocalTradeInquiryView? ReadInquiry(EntityId actorId) => _inquiries.GetValueOrDefault(actorId);
    internal void ForgetInquiry(EntityId actorId) => _inquiries.TryRemove(actorId, out _);

    /// <summary>
    /// Admits one shopper's inquiry. A shopper has at most one waiting inquiry: repeating identical terms shares
    /// it and different terms are refused until it is answered, expires or is cancelled.
    /// </summary>
    internal LocalTradeInquiryView Admit(EntityId shopperActorId, EntityId requestId, LocalTradeInquiry request,
        ulong advancingSteps)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_waiting.FirstOrDefault(value => value.ShopperActorId == shopperActorId) is { } waiting)
        {
            return waiting.Request == request ? waiting.View
                : throw new AuthorityDispatchException("One trade inquiry is awaiting its answer.");
        }
        var inquiry = new Inquiry(checked(++_sequence), shopperActorId, requestId, request,
            checked(advancingSteps + _inquiryLifetime));
        _waiting.Add(inquiry);
        Publish(inquiry);
        return inquiry.View;
    }

    /// <summary>Withdraws a waiting inquiry before any cutoff answers it; an issued quote keeps its world lifecycle.</summary>
    internal LocalTradeInquiryView? Cancel(EntityId shopperActorId)
    {
        if (_waiting.FirstOrDefault(value => value.ShopperActorId == shopperActorId) is not { } waiting)
        {
            return ReadInquiry(shopperActorId);
        }
        Close(waiting, LocalTradeInquiryState.Cancelled);
        return waiting.View;
    }

    /// <summary>
    /// Selects this cutoff's trading actions. Expiry, unavailability and declines are host decisions made here;
    /// an issued answer becomes an offer only when its committed receipt accepts it (see <see cref="Observe"/>).
    /// </summary>
    internal LocalTradingSelection Select(WorldSnapshot state, Func<EntityId, bool> isOccupied,
        IReadOnlySet<EntityId> excludedActors, Func<WorldActionCommand, bool> settles)
    {
        ArgumentNullException.ThrowIfNull(state);
        _answers.Clear();
        var round = new Round(state, isOccupied, excludedActors);
        if (!AdmitsTrading(state))
        {
            // A held, resolving or non-market boundary publishes no answer; waiting inquiries keep their place.
            return round.ToSelection();
        }
        foreach (Inquiry due in _waiting.Where(value => state.AdvancingSteps >= value.ExpiresAtStep).ToList())
        {
            Close(due, LocalTradeInquiryState.Expired);
        }
        foreach (LocalShopperPreference shopper in _shoppers)
        {
            SelectShopper(round, shopper, settles);
        }
        foreach (Inquiry inquiry in _waiting.OrderBy(value => value.Sequence).ToList())
        {
            Answer(round, inquiry);
        }
        return round.ToSelection();
    }

    /// <summary>Applies committed receipts: an accepted issuance is an offer, a refused one a decline.</summary>
    internal void Observe(WorldStepDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        foreach (PlannedAnswer planned in _answers)
        {
            WorldActionReceipt? receipt = decision.Receipts.FirstOrDefault(value => value.CommandId == planned.CommandId);
            if (receipt is null || planned.Inquiry.State != LocalTradeInquiryState.Waiting)
            {
                // Not part of the committed batch: the inquiry keeps its place and its one answer identity.
                continue;
            }
            bool offered = receipt.Rejection == WorldActionRejection.None;
            Close(planned.Inquiry, offered ? LocalTradeInquiryState.Offered : LocalTradeInquiryState.Declined,
                offered ? planned.QuoteId : null);
        }
        _answers.Clear();
    }

    private void SelectShopper(Round round, LocalShopperPreference preference, Func<WorldActionCommand, bool> settles)
    {
        WorldSnapshot state = round.State;
        EntityId actor = preference.ShopperActorId;
        if (round.Participating.Contains(actor) || !WorldActorAvailability.CanAct(state, actor)
            || LocalCareController.HostControl(state, actor) is not { } control
            || state.Coordination!.Markets!.FindMarket(preference.MarketId) is not { } market)
        {
            return;
        }
        var delivery = new TrustedCommandDelivery(control.PrincipalId, control.SessionId);
        ulong holding = UsableUnits(state, actor, market.Commodity);
        ulong balance = state.Progression!.Currency.GetActorAccount(actor).Balance;
        if (OpenOffer(state, market, actor) is { } offer)
        {
            ConsiderOffer(round, preference, delivery, offer.Quote, holding, balance, settles);
            return;
        }
        // In person only: a distant, absent or unable merchant is asked nothing and costs no attempt.
        if (holding == preference.DesiredUnits || _waiting.Any(value => value.ShopperActorId == actor)
            || WorldMarketLocality.FindAvailable(state, actor, market.MarketId) is null)
        {
            return;
        }
        LocalTradeDirection direction = holding < preference.DesiredUnits ? LocalTradeDirection.Buy : LocalTradeDirection.Sell;
        Attempt attempt = CurrentAttempt(state, preference, direction, holding, balance);
        if (attempt.Asked)
        {
            return;
        }
        if (direction == LocalTradeDirection.Buy)
        {
            ulong quantity = Math.Min(Math.Min(preference.DesiredUnits - holding, preference.MaximumUnitsPerTrade),
                market.Policy.MaximumLot);
            Ask(state, actor, attempt, new LocalTradeInquiry(market.MarketId, market.Commodity, LocalTradeDirection.Buy, quantity));
        }
        else
        {
            OfferSurplus(round, preference, market, delivery, holding, attempt);
        }
    }

    /// <summary>
    /// The shopper's current attempt, or a new one when its own circumstances changed or its reconsideration
    /// interval since the last attempt began has passed. Nothing about the merchant is consulted.
    /// </summary>
    private Attempt CurrentAttempt(WorldSnapshot state, LocalShopperPreference preference, LocalTradeDirection direction,
        ulong holding, ulong balance)
    {
        EntityId circumstances = LocalTradingIdentity.Circumstances(state, preference.ShopperActorId, preference.MarketId,
            direction, holding, balance);
        if (!_attempts.TryGetValue(preference.ShopperActorId, out Attempt? attempt) || attempt.Circumstances != circumstances
            || state.AdvancingSteps - attempt.StartedAtStep >= preference.ReconsiderAfterSteps)
        {
            attempt = new Attempt(circumstances, state.AdvancingSteps);
            _attempts[preference.ShopperActorId] = attempt;
        }
        return attempt;
    }

    /// <summary>The attempt's one inquiry, admitted like any other and answered in admission order.</summary>
    private void Ask(WorldSnapshot state, EntityId actor, Attempt attempt, LocalTradeInquiry inquiry)
    {
        attempt.Asked = true;
        Admit(actor, LocalTradingIdentity.Request(state, actor, inquiry, attempt.Circumstances, attempt.StartedAtStep),
            inquiry, state.AdvancingSteps);
    }

    /// <summary>The host shopper accepts a wanted offer once, at its exact terms, or ignores it until it closes.</summary>
    private static void ConsiderOffer(Round round, LocalShopperPreference preference, TrustedCommandDelivery delivery,
        SpotQuote quote, ulong holding, ulong balance, Func<WorldActionCommand, bool> settles)
    {
        WorldSnapshot state = round.State;
        EntityId actor = preference.ShopperActorId;
        if (round.Participating.Contains(quote.IssuerActorId))
        {
            // The merchant's opportunity is already taken this cutoff; the decision waits for a free one.
            return;
        }
        ulong quantity = quote.QuantityAllowance;
        bool wanted = quote.Side == SpotQuoteSide.IssuerSells
            ? holding < preference.DesiredUnits && quantity <= preference.DesiredUnits - holding
                && balance >= checked(quote.PaymentFor(quantity) + quote.FeesFor(quantity))
            : holding > preference.DesiredUnits && quantity <= holding - preference.DesiredUnits;
        if (!wanted || PaidQuoteProjection.ObservableIssuerRevision(state, quote, actor) is not { } issuerRevision)
        {
            return;
        }
        EntityId acceptanceId = LocalTradingIdentity.Acceptance(state, actor, quote.QuoteId);
        EntityId splitChild = WorldPaidQuote.SplitChildOf(acceptanceId);
        if (round.Occupied(acceptanceId) || round.Occupied(splitChild))
        {
            return;
        }
        var acceptance = new SpotAcceptance(acceptanceId, delivery.PrincipalId, delivery.SessionId, quote.QuoteId,
            quote.ContentHash, quantity, quote.PaymentFor(quantity), quote.FeesFor(quantity));
        var command = new WorldActionCommand(acceptanceId, delivery.PrincipalId, delivery.SessionId, actor, state.WorldId,
            state.BranchId, state.Space.GetActor(actor).Revision, new AcceptSpotQuoteIntent(acceptance, issuerRevision));
        round.Take(command, [acceptanceId, splitChild]);
        if (settles(command))
        {
            // A valid acceptance also spends the merchant's opportunity: it neither works, answers nor walks.
            round.Participating.Add(quote.IssuerActorId);
            round.Settling.Add(actor);
            round.Settling.Add(quote.IssuerActorId);
        }
    }

    /// <summary>
    /// Asks to sell the shown surplus when a live showing of an own usable lot exists; otherwise shows one own usable
    /// lot in person first. One showing per attempt: a refused or colliding showing waits for the next attempt.
    /// </summary>
    private void OfferSurplus(Round round, LocalShopperPreference preference, MarketBinding market,
        TrustedCommandDelivery delivery, ulong holding, Attempt attempt)
    {
        WorldSnapshot state = round.State;
        EntityId actor = preference.ShopperActorId;
        ulong surplus = Math.Min(Math.Min(holding - preference.DesiredUnits, preference.MaximumUnitsPerTrade),
            market.Policy.MaximumLot);
        MarketLotDisclosure? shown = state.Coordination!.Markets!.FindDisclosure(actor, market.MarketId);
        if (shown is not null && shown.IsLiveAt(state.AdvancingSteps)
            && MarketTradingService.OwnLot(state, actor, shown.LotId) is { Usable: true } lot && lot.Revision == shown.LotRevision)
        {
            Ask(state, actor, attempt, new LocalTradeInquiry(market.MarketId, market.Commodity, LocalTradeDirection.Sell,
                Math.Min(surplus, Math.Min(shown.Quantity, lot.Quantity)), shown.LotId));
            return;
        }
        if (OwnSupply(state, state.GetResident(actor).InventoryCustodianId, market.Commodity, 1) is not { } own)
        {
            return;
        }
        ulong units = Math.Min(surplus, own.Quantity);
        ulong lifetime = Math.Min(preference.ShowingLifetimeSteps, market.Policy.QuoteLifetimeSteps);
        EntityId commandId = LocalTradingIdentity.Showing(state, actor, market.MarketId, attempt.Circumstances,
            attempt.StartedAtStep);
        if (round.Occupied(commandId)
            || MarketTradingService.PrepareDisclosure(state, delivery, actor, market.MarketId, own.LotId, units, lifetime,
                commandId) is not { } show)
        {
            return;
        }
        round.Take(show, [commandId]);
    }

    private void Answer(Round round, Inquiry inquiry)
    {
        WorldSnapshot state = round.State;
        // Locality first: a missing, distant, absent, unable or unconfigured market reveals nothing else.
        MarketBinding? market = WorldMarketLocality.FindAvailable(state, inquiry.ShopperActorId, inquiry.Request.MarketId);
        if (market is null || !_merchants.TryGetValue(market.MerchantActorId, out LocalMerchantPreference? preference)
            || LocalCareController.HostControl(state, market.MerchantActorId) is not { } control)
        {
            Close(inquiry, LocalTradeInquiryState.Unavailable);
            return;
        }
        if (round.Participating.Contains(market.MerchantActorId))
        {
            // The merchant is busy at this cutoff; the inquiry keeps its place in admission order.
            return;
        }
        WorldActionCommand? answer = PrepareAnswer(round, market, preference,
            new TrustedCommandDelivery(control.PrincipalId, control.SessionId), inquiry);
        if (answer is null)
        {
            Close(inquiry, LocalTradeInquiryState.Declined);
            return;
        }
        EntityId quoteId = WorldPaidQuote.DeriveIdentity(answer.CommandId);
        round.Take(answer, [answer.CommandId, quoteId]);
        _answers.Add(new PlannedAnswer(answer.CommandId, quoteId, inquiry));
    }

    /// <summary>
    /// The merchant's own rule-priced offer for one inquiry, or null when it offers none. It reads only its own
    /// stock, funds and open quotes and the live showing addressed to it; never the shopper's memory or bags.
    /// </summary>
    private static WorldActionCommand? PrepareAnswer(Round round, MarketBinding market, LocalMerchantPreference preference,
        TrustedCommandDelivery delivery, Inquiry inquiry)
    {
        WorldSnapshot state = round.State;
        LocalTradeInquiry request = inquiry.Request;
        EntityId shopper = inquiry.ShopperActorId;
        if (!market.Commodity.Equals(request.Commodity) || request.Quantity > market.Policy.MaximumLot)
        {
            return null;
        }
        // One standing offer per shopper: an open quote to it must be accepted, ignored or closed first.
        if (state.Coordination!.PaidExchange!.Open.Any(value => value.Quote.IssuerActorId == market.MerchantActorId
            && value.Quote.CounterpartyActorId == shopper && !value.IsDue(state.AdvancingSteps)))
        {
            return null;
        }
        // One answer identity per inquiry: a refused or colliding answer is never retried under a new identity.
        EntityId commandId = LocalTradingIdentity.Answer(state, market.MerchantActorId, shopper, inquiry.RequestId);
        if (round.Occupied(commandId) || round.Occupied(WorldPaidQuote.DeriveIdentity(commandId)))
        {
            return null;
        }
        ulong lifetime = Math.Min(preference.OfferLifetimeSteps, market.Policy.QuoteLifetimeSteps);
        try
        {
            if (request.Direction == LocalTradeDirection.Buy)
            {
                return OwnSupply(state, market.CustodianId, market.Commodity, request.Quantity) is { } lot
                    ? MarketTradingService.PrepareOffer(state, delivery, market.MarketId, shopper, SpotQuoteSide.IssuerSells,
                        lot.LotId, request.Quantity, lifetime, commandId)
                    : null;
            }
            return CanFund(state, market, request.Quantity)
                ? MarketTradingService.PrepareOffer(state, delivery, market.MarketId, shopper, SpotQuoteSide.IssuerBuys,
                    request.OwnShownLotId!.Value, request.Quantity, lifetime, commandId)
                : null;
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            // Terms the quote contract cannot represent are simply no offer.
            return null;
        }
    }

    /// <summary>The merchant pays the seller's bid and the market due from its own market account, never on credit.</summary>
    private static bool CanFund(WorldSnapshot state, MarketBinding market, ulong quantity)
    {
        ulong stock = market.CountUsableStock(state.Inventory, state.Production!.Lots);
        return market.Policy.Bid(stock, quantity) is { } bid
            && state.Progression!.Currency.GetAccount(market.AccountId).Balance
                >= checked(checked(bid + market.Policy.UnitFee) * quantity);
    }

    /// <summary>The first usable own lot of the commodity in one storage covering the quantity: raw, then produced, by identity.</summary>
    private static (EntityId LotId, ulong Revision, ulong Quantity)? OwnSupply(WorldSnapshot state, EntityId custodianId,
        CountedItemDefinition commodity, ulong quantity)
    {
        if (state.Inventory.Custody.Lots.Where(value => value.CustodianId == custodianId && value.Definition.Equals(commodity)
                && value.ReservedBy is null && value.Protection == LotProtection.None && value.Quantity >= quantity)
            .OrderBy(value => value.LotId).FirstOrDefault() is { } raw)
        {
            return (raw.LotId, raw.Revision, raw.Quantity);
        }
        return state.Production!.Lots.Where(value => value.CustodianId == custodianId && value.Definition.Equals(commodity)
                && value.ReservedBy is null && value.Protection == LotProtection.None && value.Quantity >= quantity)
            .OrderBy(value => value.LotId).Select(value => ((EntityId, ulong, ulong)?)(value.LotId, value.Revision, value.Quantity))
            .FirstOrDefault();
    }

    private static ulong UsableUnits(WorldSnapshot state, EntityId actor, CountedItemDefinition commodity)
    {
        EntityId bag = state.GetResident(actor).InventoryCustodianId;
        return state.Inventory.Custody.Lots.Where(value => value.CustodianId == bag && value.Definition.Equals(commodity)
                && value.ReservedBy is null && value.Protection == LotProtection.None).Select(value => value.Quantity)
            .Concat(state.Production!.Lots.Where(value => value.CustodianId == bag && value.Definition.Equals(commodity)
                && value.ReservedBy is null && value.Protection == LotProtection.None).Select(value => value.Quantity))
            .Aggregate(0UL, (total, quantity) => checked(total + quantity));
    }

    /// <summary>The lowest open, not yet due quote this market's merchant issued under it to the shopper.</summary>
    private static WorldPaidQuote? OpenOffer(WorldSnapshot state, MarketBinding market, EntityId shopper) =>
        state.Coordination!.PaidExchange!.Open.Where(value => value.Quote.IssuerActorId == market.MerchantActorId
                && value.Quote.CounterpartyActorId == shopper && !value.IsDue(state.AdvancingSteps)
                && state.Coordination.Markets!.FindQuote(value.QuoteId)?.MarketId == market.MarketId)
            .OrderBy(value => value.QuoteId).FirstOrDefault();

    private static bool AdmitsTrading(WorldSnapshot state) =>
        state.Coordination?.Markets is not null && state.Coordination.PaidExchange is not null
        && state.Progression is not null && state.Production is not null && !state.HasReadingHold
        && state.PendingExcavation is null && state.Care?.Pending is null && state.Pacing?.Encounter is null
        && (state.EncounterLifecycle is null || state.EncounterLifecycle.Phase == EncounterLifecyclePhase.Exploration);

    private static bool IsHostActor(WorldSnapshot state, EntityId actorId) =>
        state.Residents.Any(value => value.ActorId == actorId && value.ControllerKind == ResidentControllerKind.Host)
        && LocalCareController.HostControl(state, actorId) is not null;

    private void Close(Inquiry inquiry, LocalTradeInquiryState outcome, EntityId? quoteId = null)
    {
        _waiting.Remove(inquiry);
        inquiry.State = outcome;
        inquiry.QuoteId = quoteId;
        Publish(inquiry);
    }

    private void Publish(Inquiry inquiry)
    {
        _inquiries[inquiry.ShopperActorId] = inquiry.View;
    }

    private sealed class Inquiry(ulong sequence, EntityId shopperActorId, EntityId requestId, LocalTradeInquiry request,
        ulong expiresAtStep)
    {
        internal ulong Sequence { get; } = sequence;
        internal EntityId ShopperActorId { get; } = shopperActorId;
        internal EntityId RequestId { get; } = requestId;
        internal LocalTradeInquiry Request { get; } = request;
        internal ulong ExpiresAtStep { get; } = expiresAtStep;
        internal LocalTradeInquiryState State { get; set; } = LocalTradeInquiryState.Waiting;
        internal EntityId? QuoteId { get; set; }
        internal LocalTradeInquiryView View => new(Request, State, ExpiresAtStep, QuoteId);
    }

    private sealed record PlannedAnswer(EntityId CommandId, EntityId QuoteId, Inquiry Inquiry);

    /// <summary>One host shopper attempt: the circumstances it began under, its starting step and whether it has asked.</summary>
    private sealed class Attempt(EntityId circumstances, ulong startedAtStep)
    {
        internal EntityId Circumstances { get; } = circumstances;
        internal ulong StartedAtStep { get; } = startedAtStep;
        internal bool Asked { get; set; }
    }

    /// <summary>One cutoff's selection: the actions taken, occupied opportunities and reserved identities.</summary>
    private sealed class Round(WorldSnapshot state, Func<EntityId, bool> isOccupied, IReadOnlySet<EntityId> excludedActors)
    {
        private readonly List<WorldActionCommand> _actions = [];
        private readonly HashSet<EntityId> _identities = [];

        internal WorldSnapshot State { get; } = state;
        internal HashSet<EntityId> Participating { get; } = new(excludedActors);
        internal HashSet<EntityId> Settling { get; } = [];

        internal bool Occupied(EntityId identity) =>
            _identities.Contains(identity) || State.IdentityIndex.Contains(identity) || isOccupied(identity);

        internal void Take(WorldActionCommand command, IEnumerable<EntityId> created)
        {
            _actions.Add(command);
            Participating.Add(command.ActorId);
            _identities.UnionWith(created);
        }

        internal LocalTradingSelection ToSelection() =>
            new(_actions.AsReadOnly(), Participating.ToFrozenSet(), Settling.ToFrozenSet());
    }
}
