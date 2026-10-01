using OpenFantasy.Core;
using OpenFantasy.Core.Markets;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>One reviewed offer: the exact participant view the player inspected and the units it chose to accept.</summary>
internal sealed record FarmMarketReview(PaidQuoteView Quote, ulong Units)
{
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
    private (EntityId World, EntityId Branch, EntityId Actor)? _scope;

    internal EntityId? MarketId { get; private set; }
    internal LocalTradeDirection Direction { get; private set; } = LocalTradeDirection.Buy;
    internal ulong Quantity { get; private set; } = 1;
    internal EntityId? LotId { get; private set; }
    internal FarmMarketReview? Review { get; private set; }

    /// <summary>
    /// Starts a visit to one stall. Earlier unit and lot choices survive only for the same stall, world and player;
    /// a null direction keeps the current one. A reviewed offer never survives reopening the panel.
    /// </summary>
    internal void Begin(LocalWorldView view, EntityId marketId, LocalTradeDirection? direction)
    {
        ArgumentNullException.ThrowIfNull(view);
        var scope = (view.WorldId, view.BranchId, view.PlayerActorId);
        if (_scope != scope || MarketId != marketId)
        {
            _scope = scope;
            MarketId = marketId;
            Direction = LocalTradeDirection.Buy;
            Quantity = 1;
            LotId = null;
        }
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
    internal bool CaptureReview(LocalWorldView view, LocalMarketStallView stall, PaidQuoteView quote)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(stall);
        ArgumentNullException.ThrowIfNull(quote);
        Review = null;
        SpotQuoteSide side = Direction == LocalTradeDirection.Buy ? SpotQuoteSide.IssuerSells : SpotQuoteSide.IssuerBuys;
        if (_scope != (view.WorldId, view.BranchId, view.PlayerActorId) || stall.MarketId != MarketId
            || !MarketActionOffers.BelongsTo(stall, quote) || quote.Status != PaidQuoteStatus.Open
            || quote.ObservedIssuerRevision is null || quote.Side != side || Quantity > quote.QuantityAllowance
            || Direction == LocalTradeDirection.Sell && quote.LotId != LotId)
        {
            return false;
        }
        Review = new FarmMarketReview(quote, Quantity);
        return true;
    }

    /// <summary>Back, focus loss and disconnection: the reviewed offer must be explicitly reviewed again.</summary>
    internal void DiscardReview() => Review = null;

    private void ChangeDirection(LocalTradeDirection direction)
    {
        if (Direction == direction) return;
        Direction = direction;
        Quantity = 1;
        LotId = null;
    }
}
