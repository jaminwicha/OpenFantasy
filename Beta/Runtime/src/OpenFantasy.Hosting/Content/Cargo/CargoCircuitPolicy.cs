using OpenFantasy.Core.Markets;

namespace OpenFantasy.Hosting.Content.Cargo;

/// <summary>
/// Newly authored market rules for the two ration counters of the cargo circuit. Both cite the retained rations
/// price (25 gil) through <see cref="MarketCircuitPolicies.Rations"/>; the targets, spreads, bounds and windows
/// are new versioned policy. They are distinct rules over distinct stores, so trading between them changes the
/// actual distribution of stock. Nothing here guarantees a profit: realised prices come from the counters'
/// actual stock at the moment of each quote.
/// </summary>
public static class CargoCircuitPolicy
{
    /// <summary>
    /// Brasshollow's declared opening shelf: eight rations as one typed authored source (not produced in this world
    /// and not relabelled from any other good), a twelve-unit store and an empty purse, since Marla only sells here.
    /// </summary>
    public static CargoMerchantProvision BrasshollowProvision { get; } =
        new(CargoCircuitSources.OriginMerchantId, openingUnits: 8, storageCapacity: 12, purseGil: 0);

    /// <summary>
    /// Gearford's declared opening state: no rations, an eight-unit store and an eighty-gil working purse admitted as
    /// new finite supply for this world. The legacy quest reserve is not used as a market treasury.
    /// </summary>
    public static CargoMerchantProvision GearfordProvision { get; } =
        new(CargoCircuitSources.DestinationMerchantId, openingUnits: 0, storageCapacity: 8, purseGil: 80);

    /// <summary>Advancing steps a request may wait for a merchant's answer.</summary>
    public const ulong InquiryLifetimeSteps = 600;

    /// <summary>Advancing steps an answered offer stays acceptable at either counter.</summary>
    public const ulong OfferWindowSteps = 1_200;

    /// <summary>Advancing steps one showing of a carried lot stays usable; also each rule's longest quote.</summary>
    public const ulong ShowingWindowSteps = 2_400;

    /// <summary>Brasshollow's general-store counter: four rations targeted, at most twelve held, two-gil spread.</summary>
    public static MarketPricePolicy BrasshollowRations() =>
        new(MarketCircuitPolicies.Rations, stockingTarget: 4, holdingLimit: 12, softening: 4, minimumUnitPrice: 5,
            maximumUnitPrice: 100, askMarkup: 2, bidMarkdown: 2, unitFee: 0, feePayeeAccountId: null, maximumLot: 2,
            quoteLifetimeSteps: ShowingWindowSteps);

    /// <summary>Gearford's toolwright counter: the same target with a narrower holding limit of eight.</summary>
    public static MarketPricePolicy GearfordRations() =>
        new(MarketCircuitPolicies.Rations, stockingTarget: 4, holdingLimit: 8, softening: 4, minimumUnitPrice: 5,
            maximumUnitPrice: 100, askMarkup: 2, bidMarkdown: 2, unitFee: 0, feePayeeAccountId: null, maximumLot: 2,
            quoteLifetimeSteps: ShowingWindowSteps);
}
