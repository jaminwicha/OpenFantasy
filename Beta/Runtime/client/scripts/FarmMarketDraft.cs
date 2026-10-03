using OpenFantasy.Core;
using OpenFantasy.Core.Markets;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>One reviewed offer: the exact participant view the player inspected and the units it chose to accept.</summary>
internal sealed record FarmMarketReview(PaidQuoteView Quote, ulong Units)
{
    internal FarmMarketScope? Scope { get; init; }
    internal ulong ActorRevision { get; init; }

    /// <summary>Whole currency units the buyer pays the seller for exactly these units.</summary>
    internal ulong Payment => checked(Quote.UnitPrice * Units);

    /// <summary>Market dues the buyer pays for exactly these units.</summary>
    internal ulong Fees => checked(Quote.UnitFees * Units);
}

/// <summary>
/// The player's explicit stall choices: which stall, buy or sell, how many units and, to sell, which exact own lot.
/// Changing any choice discards a reviewed offer, and so do focus loss and controller disconnection. Nothing here
/// asks, shows or confirms on its own; only an explicit review captures terms, and frame refresh never rewrites a
/// choice or swaps the reviewed offer for a newer one.
/// </summary>
internal sealed class FarmMarketDraft
{
    private FarmMarketScope? _scope;

    /// <summary>The stall the panel addresses: an explicit choice, or a stall only being looked at (see <see cref="IsExplicit"/>).</summary>
    internal EntityId? MarketId { get; private set; }

    /// <summary>
    /// Whether <see cref="MarketId"/> is the player's explicit choice: picked in the selector or on the field, or the
    /// stall in reach when its panel opened. A stall merely looked at while none is in reach is not a choice and is
    /// released when the panel closes, so it never pins the wheel to an arbitrary stall.
    /// </summary>
    internal bool IsExplicit { get; private set; }

    /// <summary>The explicit stall choice only; null when nothing has been chosen.</summary>
    internal EntityId? ChosenMarketId => IsExplicit ? MarketId : null;
    internal LocalTradeDirection Direction { get; private set; } = LocalTradeDirection.Buy;
    internal ulong Quantity { get; private set; } = 1;
    internal EntityId? LotId { get; private set; }
    internal FarmMarketReview? Review { get; private set; }

    /// <summary>
    /// Starts a visit to one stall. Earlier unit and lot choices survive only for the same stall, world and player;
    /// a null direction keeps the current one. A reviewed offer never survives reopening the panel.
    /// </summary>
    internal void Begin(LocalWorldView view, EntityId marketId, LocalTradeDirection? direction) =>
        Begin(FarmMarketPresentation.From(view), marketId, direction);

    internal void Begin(FarmMarketPresentation view, EntityId marketId, LocalTradeDirection? direction) =>
        Visit(view, marketId, direction, explicitChoice: true);

    /// <summary>
    /// Looks at one stall without choosing it, as when Offers opens while no stall is within reach. Nothing can be asked
    /// or committed there, and <see cref="ReleaseLook"/> forgets it again; an explicit choice is never downgraded.
    /// </summary>
    internal void Look(LocalWorldView view, EntityId marketId) => Look(FarmMarketPresentation.From(view), marketId);

    internal void Look(FarmMarketPresentation view, EntityId marketId)
    {
        if (IsExplicit) return;
        Visit(view, marketId, null, explicitChoice: false);
    }

    /// <summary>Forgets a stall that was only looked at; an explicit choice stays.</summary>
    internal void ReleaseLook()
    {
        if (IsExplicit) return;
        MarketId = null;
        Review = null;
    }

    private void Visit(FarmMarketPresentation view, EntityId marketId, LocalTradeDirection? direction, bool explicitChoice)
    {
        ArgumentNullException.ThrowIfNull(view);
        var scope = view.Scope;
        if (_scope != scope || MarketId != marketId)
        {
            _scope = scope;
            MarketId = marketId;
            Direction = LocalTradeDirection.Buy;
            Quantity = 1;
            LotId = null;
        }
        IsExplicit = explicitChoice;
        if (direction is { } chosen) ChangeDirection(chosen);
        Review = null;
    }

    internal bool SetDirection(LocalTradeDirection direction)
    {
        Review = null;
        if (!Enum.IsDefined(direction)) return false;
        ChangeDirection(direction);
        return true;
    }

    /// <summary>A unit count from one up to the stall's per-offer limit; any change discards the review.</summary>
    internal bool SetQuantity(ulong quantity, ulong maximum)
    {
        Review = null;
        if (quantity == 0 || quantity > maximum) return false;
        Quantity = quantity;
        return true;
    }

    /// <summary>Only a lot listed as the player's own usable stall commodity can be chosen to sell.</summary>
    internal bool SelectLot(LocalMarketStallView stall, EntityId lotId)
    {
        ArgumentNullException.ThrowIfNull(stall);
        Review = null;
        if (Direction != LocalTradeDirection.Sell || stall.MarketId != MarketId
            || stall.OwnLots.All(lot => lot.LotId != lotId))
        {
            LotId = null;
            return false;
        }
        LotId = lotId;
        return true;
    }

    internal void ClearLot()
    {
        LotId = null;
        Review = null;
    }

    /// <summary>
    /// The only act that captures terms to confirm. The quote must be an open offer from this stall's merchant to
    /// the player for this stall's commodity, on the chosen side, for the chosen lot when selling, covering the
    /// chosen units, and its issuer must be observable where the player stands. Anything else captures nothing.
    /// </summary>
    internal bool CaptureReview(LocalWorldView view, LocalMarketStallView stall, PaidQuoteView quote) =>
        CaptureReview(FarmMarketPresentation.From(view), stall, quote);

    internal bool CaptureReview(FarmMarketPresentation view, LocalMarketStallView stall, PaidQuoteView quote)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(stall);
        ArgumentNullException.ThrowIfNull(quote);
        Review = null;
        SpotQuoteSide side = Direction == LocalTradeDirection.Buy ? SpotQuoteSide.IssuerSells : SpotQuoteSide.IssuerBuys;
        if (_scope != view.Scope || stall.MarketId != MarketId
            || !MarketActionOffers.BelongsTo(stall, quote) || quote.CounterpartyActorId != view.ActorId || quote.Status != PaidQuoteStatus.Open
            || quote.ObservedIssuerRevision is null || quote.Side != side || Quantity > quote.QuantityAllowance
            || Direction == LocalTradeDirection.Sell && quote.LotId != LotId)
        {
            return false;
        }
        Review = new FarmMarketReview(quote, Quantity) { Scope = view.Scope, ActorRevision = view.ActorRevision };
        return true;
    }

    /// <summary>Back, focus loss and disconnection: the reviewed offer must be explicitly reviewed again.</summary>
    internal void DiscardReview() => Review = null;

    internal bool MatchesScope(FarmMarketPresentation view) => _scope == view.Scope;

    internal void ObserveScope(FarmMarketPresentation view)
    {
        if (_scope == view.Scope) return;
        _scope = view.Scope;
        MarketId = null;
        IsExplicit = false;
        Direction = LocalTradeDirection.Buy;
        Quantity = 1;
        LotId = null;
        Review = null;
    }

    private void ChangeDirection(LocalTradeDirection direction)
    {
        if (Direction == direction) return;
        Direction = direction;
        Quantity = 1;
        LotId = null;
    }
}
