using OpenFantasy.Core;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>What the shopper wants to do with the market's merchant: buy from it or sell to it.</summary>
public enum LocalTradeDirection { Buy = 1, Sell = 2 }

/// <summary>
/// A shopper's nonbinding request that a market's merchant offer terms. It names only what the shopper itself
/// knows and owns: the market, its commodity, the direction, the units wanted and, when selling, the shopper's
/// own lot that it has already shown to that market. It never names a merchant lot, delivery or price. It is
/// application input, not a world fact: it reserves, promises and publishes nothing, and no merchant is bound.
/// </summary>
public sealed record LocalTradeInquiry
{
    public LocalTradeInquiry(EntityId marketId, CountedItemDefinition commodity, LocalTradeDirection direction,
        ulong quantity, EntityId? ownShownLotId = null)
    {
        ArgumentNullException.ThrowIfNull(commodity);
        if (!Enum.IsDefined(direction) || quantity == 0 || quantity > SpotQuote.MaximumQuantity
            || (direction == LocalTradeDirection.Sell) != ownShownLotId.HasValue)
        {
            throw new ArgumentException("An inquiry names one direction, a bounded positive quantity and, only to sell, its own shown lot.");
        }
        MarketId = marketId;
        Commodity = commodity;
        Direction = direction;
        Quantity = quantity;
        OwnShownLotId = ownShownLotId;
    }

    public EntityId MarketId { get; }
    public CountedItemDefinition Commodity { get; }
    public LocalTradeDirection Direction { get; }
    public ulong Quantity { get; }
    public EntityId? OwnShownLotId { get; }
}

/// <summary>
/// Where one inquiry stands. Waiting is the only nonterminal state. Unavailable is one uniform answer for a
/// missing, distant, absent, unable or unconfigured market; Declined means a locally available merchant offered
/// no terms. Offered names the quote the merchant actually issued; accepting or ignoring it is a separate choice.
/// </summary>
public enum LocalTradeInquiryState { Waiting = 1, Offered = 2, Declined = 3, Unavailable = 4, Expired = 5, Cancelled = 6 }

/// <summary>
/// The shopper's own view of its current inquiry. <see cref="ExpiresAtStep"/> is fixed-world time: a paused or
/// held session publishes no advancing step, so its inquiry neither progresses nor expires.
/// </summary>
public sealed record LocalTradeInquiryView(LocalTradeInquiry Inquiry, LocalTradeInquiryState State, ulong ExpiresAtStep,
    EntityId? QuoteId);
