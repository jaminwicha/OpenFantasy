using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>The participant-visible lifecycle of one paid quote. A due open quote already reads as expired.</summary>
public enum PaidQuoteStatus { Open = 1, Filled = 2, Superseded = 3, Withdrawn = 4, Expired = 5 }

/// <summary>
/// What one participant of a paid quote may know about it: identity, parties and side, the exact fixed goods,
/// storage, account, currency, price, fee and window terms, the content hash an acceptance restates, its
/// lifecycle and, after a fill, the agreed quantity and totals. It never carries the issuer's principal or
/// session, a market's stock basis or issuer fill counter, any acceptance or withdrawal delivery, the fill
/// that superseded it, or a world root. The issuer's presence revision is present only for the counterparty
/// of an open quote that can actually reach the issuer where both stand; a distant revision stays unknown.
/// A quote issued under a market rule names that market, read from the canonical market quote record of the same
/// root that holds the quote or its outcome; a bilateral paid quote names none. The record's stock basis stays private.
/// </summary>
public sealed class PaidQuoteView
{
    internal PaidQuoteView(SpotQuote quote, EntityId viewerActorId, PaidQuoteStatus status, ulong? closedAtSequence,
        SpotAcceptance? fill, ulong? observedIssuerRevision, EntityId? marketId = null)
    {
        QuoteId = quote.QuoteId;
        MarketId = marketId;
        IssuerActorId = quote.IssuerActorId;
        CounterpartyActorId = quote.CounterpartyActorId;
        ViewerIsIssuer = quote.IssuerActorId == viewerActorId;
        Side = quote.Side;
        Definition = quote.Definition;
        LotKind = quote.LotKind;
        LotId = quote.LotId;
        ExpectedLotRevision = quote.ExpectedLotRevision;
        SellerCustodianId = quote.SellerCustodianId;
        BuyerCustodianId = quote.BuyerCustodianId;
        CurrencyDefinitionId = quote.CurrencyDefinitionId;
        CurrencyVersion = quote.CurrencyVersion;
        SellerAccountId = quote.SellerAccountId;
        BuyerAccountId = quote.BuyerAccountId;
        UnitPrice = quote.UnitPrice;
        Fees = quote.Fees;
        UnitFees = quote.UnitFees;
        QuantityAllowance = quote.QuantityAllowance;
        IssuedAtStep = quote.IssuedAtStep;
        ExpiresAtStep = quote.ExpiresAtStep;
        QuoteHash = quote.ContentHash;
        Status = status;
        ClosedAtSequence = closedAtSequence;
        // Only the agreed amounts of a fill; its acceptance carries the accepting principal and session.
        FilledQuantity = fill?.Quantity;
        FilledPayment = fill?.Payment;
        FilledFees = fill?.Fees;
        ObservedIssuerRevision = observedIssuerRevision;
    }

    public EntityId QuoteId { get; }

    /// <summary>
    /// The market whose rule priced this quote, from the canonical market quote record; null for a quote issued
    /// outside any market. It identifies one stall even when one merchant keeps several stalls of the same goods.
    /// </summary>
    public EntityId? MarketId { get; }
    public EntityId IssuerActorId { get; }
    public EntityId CounterpartyActorId { get; }
    public bool ViewerIsIssuer { get; }
    public SpotQuoteSide Side { get; }
    public CountedItemDefinition Definition { get; }
    public SpotLotKind LotKind { get; }
    public EntityId LotId { get; }
    public ulong ExpectedLotRevision { get; }
    public EntityId SellerCustodianId { get; }
    public EntityId BuyerCustodianId { get; }
    public EntityId CurrencyDefinitionId { get; }
    public ContentHash CurrencyVersion { get; }
    public EntityId SellerAccountId { get; }
    public EntityId BuyerAccountId { get; }

    /// <summary>Whole currency units the buyer pays the seller per traded unit.</summary>
    public ulong UnitPrice { get; }
    public ReadOnlyCollection<SpotFee> Fees { get; }
    public ulong UnitFees { get; }
    public ulong QuantityAllowance { get; }

    /// <summary>Fixed-world advancing steps; the quote is acceptable while the world has advanced fewer than ExpiresAtStep.</summary>
    public ulong IssuedAtStep { get; }
    public ulong ExpiresAtStep { get; }

    /// <summary>The hash of the complete fixed quote that an acceptance must restate; nothing is requoted.</summary>
    public ContentHash QuoteHash { get; }
    public PaidQuoteStatus Status { get; }

    /// <summary>The world revision that published the terminal outcome, when closed.</summary>
    public ulong? ClosedAtSequence { get; }
    public ulong? FilledQuantity { get; }
    public ulong? FilledPayment { get; }
    public ulong? FilledFees { get; }

    /// <summary>The issuer's presence revision as this counterparty currently perceives it, or null when it cannot.</summary>
    public ulong? ObservedIssuerRevision { get; }
    public EntityId SellerActorId => Side == SpotQuoteSide.IssuerSells ? IssuerActorId : CounterpartyActorId;
    public EntityId BuyerActorId => Side == SpotQuoteSide.IssuerSells ? CounterpartyActorId : IssuerActorId;

    /// <summary>Total the buyer pays (price and fees) for one quantity of these fixed terms.</summary>
    public ulong TotalFor(ulong quantity) => checked(checked(UnitPrice + UnitFees) * quantity);

    /// <summary>
    /// The counterparty's own exact acceptance of these inspected terms: the quote hash, a quantity within the
    /// allowance, both money totals and the issuer revision it actually observed. Preparing it submits nothing;
    /// confirmation is the separate submission of this intent under the counterparty's own command identity.
    /// </summary>
    public AcceptSpotQuoteIntent PrepareAcceptance(EntityId acceptanceCommandId, TrustedCommandDelivery counterpartyDelivery,
        ulong quantity)
    {
        ArgumentNullException.ThrowIfNull(counterpartyDelivery);
        if (ViewerIsIssuer || Status != PaidQuoteStatus.Open || ObservedIssuerRevision is not { } issuerRevision)
        {
            throw new InvalidOperationException("Only the counterparty of an open quote whose issuer it can observe may accept it.");
        }
        if (quantity == 0 || quantity > QuantityAllowance)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "An acceptance names a quantity within the quote's allowance.");
        }
        var acceptance = new SpotAcceptance(acceptanceCommandId, counterpartyDelivery.PrincipalId, counterpartyDelivery.SessionId,
            QuoteId, QuoteHash, quantity, checked(UnitPrice * quantity), checked(UnitFees * quantity));
        return new AcceptSpotQuoteIntent(acceptance, issuerRevision);
    }
}

/// <summary>Projects participant quote views from a world root the caller is already authorized to read for that actor.</summary>
internal static class PaidQuoteProjection
{
    /// <summary>Every open quote and current-segment outcome the actor takes part in, as that participant may see it.</summary>
    internal static IEnumerable<PaidQuoteView> Project(WorldSnapshot state, EntityId viewerActorId)
    {
        if (state.Coordination?.PaidExchange is not { } book)
        {
            yield break;
        }
        foreach (WorldPaidQuote open in book.Open.Where(value => value.HasParticipant(viewerActorId)))
        {
            // A due quote is already unacceptable, before the next advancing boundary closes it.
            bool due = open.IsDue(state.AdvancingSteps);
            yield return new PaidQuoteView(open.Quote, viewerActorId, due ? PaidQuoteStatus.Expired : PaidQuoteStatus.Open,
                null, null, due ? null : ObservableIssuerRevision(state, open.Quote, viewerActorId),
                MarketOf(state, open.QuoteId));
        }
        foreach (WorldPaidQuoteOutcome outcome in book.Outcomes.Where(value => value.Quote.HasParticipant(viewerActorId)))
        {
            yield return Closed(state, outcome, viewerActorId);
        }
    }

    /// <summary>
    /// One quote by identity: the live owner first, then an archived outcome the actor took part in. For an archived
    /// outcome the caller supplies the recorded root that published it, so its market comes from that root's record.
    /// </summary>
    internal static PaidQuoteView? ProjectRecorded(WorldSnapshot state, WorldPaidQuoteOutcome? archived, EntityId quoteId,
        EntityId viewerActorId) =>
        Project(state, viewerActorId).FirstOrDefault(view => view.QuoteId == quoteId)
        ?? (archived is not null && archived.Quote.QuoteId == quoteId && archived.Quote.HasParticipant(viewerActorId)
            ? Closed(state, archived, viewerActorId) : null);

    /// <summary>The market named by the canonical quote record in this root, or null for a bilateral quote.</summary>
    internal static EntityId? MarketOf(WorldSnapshot state, EntityId quoteId) =>
        state.Coordination?.Markets?.FindQuote(quoteId)?.MarketId;

    private static PaidQuoteView Closed(WorldSnapshot state, WorldPaidQuoteOutcome outcome, EntityId viewerActorId) =>
        new(outcome.Quote.Quote, viewerActorId, outcome.Kind switch
        {
            WorldPaidQuoteOutcomeKind.Filled => PaidQuoteStatus.Filled,
            WorldPaidQuoteOutcomeKind.Superseded => PaidQuoteStatus.Superseded,
            WorldPaidQuoteOutcomeKind.Withdrawn => PaidQuoteStatus.Withdrawn,
            _ => PaidQuoteStatus.Expired
        }, outcome.WorldSequence, outcome.Settlement?.Acceptance, null, MarketOf(state, outcome.Quote.QuoteId));

    /// <summary>
    /// The issuer's current presence revision only when the viewing counterparty and the issuer are both present
    /// and can actually reach each other where they stand. Any other viewer or position learns nothing.
    /// </summary>
    internal static ulong? ObservableIssuerRevision(WorldSnapshot state, SpotQuote quote, EntityId viewerActorId)
    {
        if (quote.CounterpartyActorId != viewerActorId || !MutuallyReachable(state, viewerActorId, quote.IssuerActorId))
        {
            return null;
        }
        return state.Space.GetActor(quote.IssuerActorId).Revision;
    }

    internal static bool MutuallyReachable(WorldSnapshot state, EntityId actorId, EntityId otherActorId) =>
        actorId != otherActorId && state.Space.Actors.Any(value => value.ActorId == actorId)
        && state.Space.Actors.Any(value => value.ActorId == otherActorId)
        && state.Space.QueryInteraction(actorId, otherActorId) == SpatialInteraction.Reachable
        && state.Space.QueryInteraction(otherActorId, actorId) == SpatialInteraction.Reachable;
}
