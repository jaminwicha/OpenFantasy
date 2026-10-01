using System.Collections.ObjectModel;
using OpenFantasy.Core;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>
/// Explicit opt-in that one host actor answers inquiries addressed to the markets it runs, under its own
/// control, from its own stock, funds and the showings addressed to it. It grants no stock, money or consent.
/// </summary>
public sealed class LocalMerchantPreference
{
    public LocalMerchantPreference(EntityId merchantActorId, ulong offerLifetimeSteps = 32)
    {
        if (offerLifetimeSteps == 0 || offerLifetimeSteps > WorldPaidQuote.MaximumLifetimeSteps)
        {
            throw new ArgumentException("A merchant preference needs a bounded positive offer lifetime.");
        }
        MerchantActorId = merchantActorId;
        OfferLifetimeSteps = offerLifetimeSteps;
    }

    public EntityId MerchantActorId { get; }

    /// <summary>Advancing steps an answered offer stays open, further capped by the market rule and any showing.</summary>
    public ulong OfferLifetimeSteps { get; }
}

/// <summary>
/// A configured host shopper's bounded authored stock target for one market's commodity: it asks to buy while
/// it holds fewer usable units than <see cref="DesiredUnits"/>, and shows and offers its own surplus above it.
/// It is a simple desire, not demand simulation, route planning or a wage contract; it trades only in person.
/// </summary>
public sealed class LocalShopperPreference
{
    public LocalShopperPreference(EntityId shopperActorId, EntityId marketId, ulong desiredUnits,
        ulong maximumUnitsPerTrade = 1, ulong showingLifetimeSteps = 32, ulong reconsiderAfterSteps = 32)
    {
        if (maximumUnitsPerTrade == 0 || maximumUnitsPerTrade > SpotQuote.MaximumQuantity
            || desiredUnits > SpotQuote.MaximumQuantity
            || showingLifetimeSteps == 0 || showingLifetimeSteps > WorldPaidQuote.MaximumLifetimeSteps
            || reconsiderAfterSteps == 0 || reconsiderAfterSteps > WorldPaidQuote.MaximumLifetimeSteps)
        {
            throw new ArgumentException("A shopper preference needs a bounded target, lot size, showing lifetime and reconsideration interval.");
        }
        ShopperActorId = shopperActorId;
        MarketId = marketId;
        DesiredUnits = desiredUnits;
        MaximumUnitsPerTrade = maximumUnitsPerTrade;
        ShowingLifetimeSteps = showingLifetimeSteps;
        ReconsiderAfterSteps = reconsiderAfterSteps;
    }

    public EntityId ShopperActorId { get; }
    public EntityId MarketId { get; }
    public ulong DesiredUnits { get; }
    public ulong MaximumUnitsPerTrade { get; }
    public ulong ShowingLifetimeSteps { get; }

    /// <summary>
    /// Advancing steps after one attempt (a showing and an inquiry) began before the same unchanged desire may start
    /// another. A change of the shopper's own holding or balance starts a new attempt at once.
    /// </summary>
    public ulong ReconsiderAfterSteps { get; }
}

/// <summary>Immutable Host advice copied by a session; it is resupplied explicitly on reopening, never saved.</summary>
public sealed class LocalTradingConfiguration
{
    /// <summary>Advancing steps an inquiry may wait for its answer unless the configuration names another bound.</summary>
    public const ulong DefaultInquiryLifetimeSteps = 32;

    public LocalTradingConfiguration(IEnumerable<LocalMerchantPreference> merchants,
        IEnumerable<LocalShopperPreference>? shoppers = null, ulong inquiryLifetimeSteps = DefaultInquiryLifetimeSteps)
    {
        ArgumentNullException.ThrowIfNull(merchants);
        if (inquiryLifetimeSteps == 0 || inquiryLifetimeSteps > WorldPaidQuote.MaximumLifetimeSteps)
        {
            throw new ArgumentException("Trading needs a bounded positive inquiry lifetime.");
        }
        Merchants = Array.AsReadOnly(merchants.Select(value => value ?? throw new ArgumentNullException(nameof(merchants))).ToArray());
        Shoppers = Array.AsReadOnly((shoppers ?? []).Select(value => value ?? throw new ArgumentNullException(nameof(shoppers))).ToArray());
        InquiryLifetimeSteps = inquiryLifetimeSteps;
    }

    public static LocalTradingConfiguration None { get; } = new([]);
    public ReadOnlyCollection<LocalMerchantPreference> Merchants { get; }
    public ReadOnlyCollection<LocalShopperPreference> Shoppers { get; }

    /// <summary>Fixed-world advancing steps every inquiry of the session, the player's included, may wait unanswered.</summary>
    public ulong InquiryLifetimeSteps { get; }
}
