using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>
/// Authored first-circuit market rules. Each cites an immutable source price from the retained legacy item
/// table (rations 25 gil, shared by the Marla and Kegga candidates; berries 5 gil) by item key, price and the
/// source file's digest; the stocking targets, spreads, bounds and quote limits are new authored policy. No
/// source record, merchant identity, shop list or price is edited, and binding a rule admits no stock or money.
/// </summary>
public static class MarketCircuitPolicies
{
    /// <summary>SHA-256 of source-support/data/items.json in the retained legacy package.</summary>
    public static ContentHash LegacyItemsDigest { get; } =
        new("ac0944b98fbfc10474ae1d1309bd7ad6b74c511fb2da9c314ab41f6c8a655609");

    public static MarketSourcePrice Berries { get; } = new("berries", 5, LegacyItemsDigest);
    public static MarketSourcePrice Rations { get; } = new("rations", 25, LegacyItemsDigest);

    /// <summary>
    /// A small orchard stall: it wants eight units on hand, stops buying at sixteen, quotes at most four units
    /// per fixed offer for at most sixty-four advancing steps and keeps one gil on each side of the mid price.
    /// </summary>
    public static MarketPricePolicy BerryStall(ulong unitFee = 0, EntityId? feePayeeAccountId = null) =>
        new(Berries, stockingTarget: 8, holdingLimit: 16, softening: 4, minimumUnitPrice: 1, maximumUnitPrice: 20,
            askMarkup: 1, bidMarkdown: 1, unitFee: unitFee, feePayeeAccountId: feePayeeAccountId, maximumLot: 4,
            quoteLifetimeSteps: 64);

    /// <summary>A provisioner's ration counter: four units targeted, at most eight held, two-gil spread on each side.</summary>
    public static MarketPricePolicy RationCounter() =>
        new(Rations, stockingTarget: 4, holdingLimit: 8, softening: 4, minimumUnitPrice: 5, maximumUnitPrice: 100,
            askMarkup: 2, bidMarkdown: 2, unitFee: 0, feePayeeAccountId: null, maximumLot: 2, quoteLifetimeSteps: 64);

    /// <summary>
    /// Binds a rule to an admitted merchant of an unplayed world: its primary storage, its own actor account
    /// and its admitted home place. The merchant keeps exactly the stock and money the world already gives it.
    /// </summary>
    public static MarketBinding BindResident(WorldSnapshot world, EntityId marketId, EntityId merchantActorId,
        CountedItemDefinition commodity, MarketPricePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(world);
        WorldResident merchant = world.GetResident(merchantActorId);
        return new MarketBinding(marketId, merchantActorId, merchant.InventoryCustodianId,
            world.Progression!.Currency.GetActorAccount(merchantActorId).AccountId, commodity,
            world.Space.GetActor(merchantActorId).HomePlaceId, policy);
    }
}
