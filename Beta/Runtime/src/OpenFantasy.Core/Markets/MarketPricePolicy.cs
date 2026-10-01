using System.Text;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Markets;

/// <summary>
/// One immutable source fact a market policy cites: the original item key, its source price in whole
/// currency units and the digest of the source file that states it. The runtime never edits it; new
/// pricing parameters are authored separately in <see cref="MarketPricePolicy"/>.
/// </summary>
public sealed record MarketSourcePrice
{
    public const int MaximumKeyLength = 64;

    public MarketSourcePrice(string itemKey, ulong unitPrice, ContentHash sourceDigest)
    {
        ArgumentNullException.ThrowIfNull(itemKey);
        ArgumentNullException.ThrowIfNull(sourceDigest);
        if (itemKey.Length is 0 or > MaximumKeyLength || itemKey.Any(character => character is < '!' or > '~')
            || unitPrice == 0 || unitPrice > MarketPricePolicy.MaximumUnitPrice)
        {
            throw new ArgumentException("A source price names a printable item key and a bounded positive price.");
        }
        ItemKey = itemKey;
        UnitPrice = unitPrice;
        SourceDigest = sourceDigest;
    }

    public string ItemKey { get; }
    public ulong UnitPrice { get; }
    public ContentHash SourceDigest { get; }

    public byte[] Encode() => CanonicalEncodingV1.Frame("market.source-price.v1", Encoding.ASCII.GetBytes(ItemKey),
        CanonicalEncodingV1.Unsigned64(UnitPrice), SourceDigest.ToByteArray());
}

/// <summary>
/// Versioned integer pricing rule of one local market. Demand is explicit: an authored stocking target
/// the merchant wants to hold and a holding limit above which it stops buying. The rule is a declared
/// initial demand policy, not a claim of simulated population consumption; later real orders or needs
/// enter through new explicit causes rather than by editing this value.
/// </summary>
/// <remarks>
/// The reference mid price at usable stock <c>s</c> is
/// <c>clamp(floor(source × (target + softening) ÷ (s + softening)), minimum, maximum)</c>. It equals the
/// source price at the stocking target and never increases as stock grows. A quote for <c>q</c> units is
/// priced at the state the whole allowance would produce: the ask at <c>mid(s − q) + askMarkup</c>, and the
/// merchant's all-in bid (the unit price it pays the seller plus the market due it pays as buyer) at
/// <c>mid(s + q) − bidMarkdown</c>, so the posted bid price is <c>mid(s + q) − bidMarkdown − unitFee</c>.
/// The merchant therefore receives at least its declared spread per unit over any closed buy/resell cycle
/// against unchanged other state, however the quantity is split or ordered. Because currency is
/// conserved, every coalition that excludes the merchant, including cycling traders together with the
/// due's payee, loses at least that spread. Price depends only on these integers and the stock count,
/// never on wall time, polling or enumeration order. Stock above <see cref="MaximumStock"/> is outside the
/// rule's admitted domain and posts no terms.
/// </remarks>
public sealed class MarketPricePolicy
{
    public const ulong MaximumUnitPrice = 1_000_000;
    public const ulong MaximumStock = 1_000_000;
    public const ulong MaximumLotLimit = 1_000;

    /// <summary>Equal to the complete-world quote lifetime bound; a market may only shorten it.</summary>
    public const ulong MaximumQuoteLifetimeSteps = 65_536;

    public MarketPricePolicy(MarketSourcePrice source, ulong stockingTarget, ulong holdingLimit, ulong softening,
        ulong minimumUnitPrice, ulong maximumUnitPrice, ulong askMarkup, ulong bidMarkdown, ulong unitFee,
        EntityId? feePayeeAccountId, ulong maximumLot, ulong quoteLifetimeSteps)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (stockingTarget == 0 || holdingLimit < stockingTarget || holdingLimit > MaximumStock
            || softening == 0 || softening > MaximumStock)
        {
            throw new ArgumentException("Demand requires a positive stocking target within a bounded holding limit.");
        }
        if (minimumUnitPrice == 0 || minimumUnitPrice > source.UnitPrice || source.UnitPrice > maximumUnitPrice
            || maximumUnitPrice > MaximumUnitPrice)
        {
            throw new ArgumentException("Price bounds must contain the cited source price.");
        }
        if (askMarkup > MaximumUnitPrice || bidMarkdown > MaximumUnitPrice || askMarkup + bidMarkdown == 0)
        {
            throw new ArgumentException("A market declares a bounded spread of at least one whole unit.");
        }
        if (unitFee > MaximumUnitPrice || (unitFee == 0) != (feePayeeAccountId is null))
        {
            throw new ArgumentException("A market fee names its payee exactly when it charges a bounded amount.");
        }
        if (maximumLot == 0 || maximumLot > MaximumLotLimit || quoteLifetimeSteps == 0
            || quoteLifetimeSteps > MaximumQuoteLifetimeSteps)
        {
            throw new ArgumentException("A market bounds each quote's allowance and lifetime.");
        }
        Source = source;
        StockingTarget = stockingTarget;
        HoldingLimit = holdingLimit;
        Softening = softening;
        MinimumUnitPrice = minimumUnitPrice;
        MaximumUnitPriceBound = maximumUnitPrice;
        AskMarkup = askMarkup;
        BidMarkdown = bidMarkdown;
        UnitFee = unitFee;
        FeePayeeAccountId = feePayeeAccountId;
        MaximumLot = maximumLot;
        QuoteLifetimeSteps = quoteLifetimeSteps;
        Version = ContentHash.Compute(Encode());
    }

    public MarketSourcePrice Source { get; }

    /// <summary>Units the merchant aims to hold; at this stock the mid price equals the source price.</summary>
    public ulong StockingTarget { get; }

    /// <summary>The merchant bids only while a fill would leave its usable stock at or below this count.</summary>
    public ulong HoldingLimit { get; }

    /// <summary>Stock elasticity: larger values flatten the curve around the target.</summary>
    public ulong Softening { get; }
    public ulong MinimumUnitPrice { get; }
    public ulong MaximumUnitPriceBound { get; }
    public ulong AskMarkup { get; }
    public ulong BidMarkdown { get; }

    /// <summary>Per-unit market due paid by the buyer of each fill to the authored payee; zero for none.</summary>
    public ulong UnitFee { get; }
    public EntityId? FeePayeeAccountId { get; }
    public ulong MaximumLot { get; }
    public ulong QuoteLifetimeSteps { get; }

    /// <summary>Content version of the complete rule, pinned by every market quote and learned observation.</summary>
    public ContentHash Version { get; }

    /// <summary>The reference mid price at one usable stock count; non-increasing in the stock.</summary>
    public ulong MidPrice(ulong stock)
    {
        UInt128 numerator = (UInt128)Source.UnitPrice * (StockingTarget + Softening);
        UInt128 mid = numerator / ((UInt128)stock + Softening);
        return mid < MinimumUnitPrice ? MinimumUnitPrice
            : mid > MaximumUnitPriceBound ? MaximumUnitPriceBound : (ulong)mid;
    }

    /// <summary>
    /// The merchant's whole-unit ask for selling <paramref name="quantity"/> units from <paramref name="stock"/>
    /// usable units, or null when the allowance is outside the rule, the stock cannot supply it or the stock
    /// lies outside the rule's admitted domain. The buyer also pays the per-unit market due.
    /// </summary>
    public ulong? Ask(ulong stock, ulong quantity)
    {
        if (quantity == 0 || quantity > MaximumLot || quantity > stock || stock > MaximumStock)
        {
            return null;
        }
        return checked(MidPrice(stock - quantity) + AskMarkup);
    }

    /// <summary>
    /// The unit price the merchant pays a seller for <paramref name="quantity"/> units into
    /// <paramref name="stock"/> usable units, or null when the fill would exceed the holding limit or no
    /// positive price remains. As the buyer the merchant also pays the market due, so its all-in cost per
    /// unit is <see cref="AllInBid"/>: never above the mid price less the declared markdown.
    /// </summary>
    public ulong? Bid(ulong stock, ulong quantity)
    {
        if (AllInBid(stock, quantity) is not { } allIn)
        {
            return null;
        }
        return allIn > UnitFee ? allIn - UnitFee : null;
    }

    /// <summary>
    /// The merchant's whole all-in cost per unit bought (seller price plus the market due it pays), or null
    /// when demand is exhausted or the stock lies outside the rule's admitted domain.
    /// </summary>
    public ulong? AllInBid(ulong stock, ulong quantity)
    {
        if (quantity == 0 || quantity > MaximumLot || stock > HoldingLimit || quantity > HoldingLimit - stock)
        {
            return null;
        }
        ulong mid = MidPrice(stock + quantity);
        return mid > BidMarkdown ? mid - BidMarkdown : null;
    }

    public byte[] Encode() => CanonicalEncodingV1.Frame("market.price-policy.v1", Source.Encode(),
        CanonicalEncodingV1.Unsigned64(StockingTarget), CanonicalEncodingV1.Unsigned64(HoldingLimit),
        CanonicalEncodingV1.Unsigned64(Softening), CanonicalEncodingV1.Unsigned64(MinimumUnitPrice),
        CanonicalEncodingV1.Unsigned64(MaximumUnitPriceBound), CanonicalEncodingV1.Unsigned64(AskMarkup),
        CanonicalEncodingV1.Unsigned64(BidMarkdown), CanonicalEncodingV1.Unsigned64(UnitFee),
        FeePayeeAccountId?.ToByteArray() ?? Array.Empty<byte>(), CanonicalEncodingV1.Unsigned64(MaximumLot),
        CanonicalEncodingV1.Unsigned64(QuoteLifetimeSteps));
}
