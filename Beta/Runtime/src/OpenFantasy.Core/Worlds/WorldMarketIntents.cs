using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// One submission of the local-market interpretation (profile 33). Each is a separate command with its own
/// identity and receipt. Market quotes settle, expire and are withdrawn through the paid-exchange intents.
/// </summary>
public abstract record MarketIntent : WorldActionIntent;

/// <summary>
/// The merchant's fixed paid quote under its market rule. The world recomputes the rule's integer price
/// and fee from the actual usable stock and refuses any other terms; nothing is requoted for the caller.
/// </summary>
public sealed record IssueMarketQuoteIntent : MarketIntent
{
    public IssueMarketQuoteIntent(EntityId marketId, SpotQuote quote)
    {
        ArgumentNullException.ThrowIfNull(quote);
        MarketId = marketId;
        Quote = quote;
    }

    public EntityId MarketId { get; }
    public SpotQuote Quote { get; }
}

/// <summary>
/// The observer's own in-person reading of one market's posted terms. It names only the market and the
/// merchant presence revision the observer saw; the world derives the terms. A supplied price, a query or
/// a guessed market identity is never evidence.
/// </summary>
public sealed record ObserveMarketIntent(EntityId MarketId, ulong ExpectedMerchantRevision) : MarketIntent;

/// <summary>
/// The seller's own in-person showing of one selected lot to one market, so that market's merchant may quote
/// to buy it. It pins the exact lot, its definition and revision, the units shown, the seller's own account to
/// be paid into and the last step of the showing. It sells, reserves and moves nothing and consents to no
/// quote; the seller's later acceptance of a buy quote is the separate consent to sell.
/// </summary>
public sealed record DiscloseMarketLotIntent : MarketIntent
{
    public DiscloseMarketLotIntent(EntityId marketId, SpotLotKind lotKind, EntityId lotId, CountedItemDefinition definition,
        ulong expectedLotRevision, ulong quantity, EntityId sellerAccountId, ulong expiresAtStep)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!Enum.IsDefined(lotKind) || quantity == 0 || quantity > SpotQuote.MaximumQuantity)
        {
            throw new ArgumentException("A disclosure shows a bounded positive part of one known lot kind.");
        }
        MarketId = marketId;
        LotKind = lotKind;
        LotId = lotId;
        Definition = definition;
        ExpectedLotRevision = expectedLotRevision;
        Quantity = quantity;
        SellerAccountId = sellerAccountId;
        ExpiresAtStep = expiresAtStep;
    }

    public EntityId MarketId { get; }
    public SpotLotKind LotKind { get; }
    public EntityId LotId { get; }
    public CountedItemDefinition Definition { get; }
    public ulong ExpectedLotRevision { get; }
    public ulong Quantity { get; }
    public EntityId SellerAccountId { get; }
    public ulong ExpiresAtStep { get; }
}

/// <summary>The seller's own withdrawal of its current showing to one market; issued quotes keep their terms.</summary>
public sealed record WithdrawMarketLotDisclosureIntent(EntityId MarketId) : MarketIntent;
