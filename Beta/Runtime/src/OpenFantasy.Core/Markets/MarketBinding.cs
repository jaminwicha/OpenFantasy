using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Markets;

/// <summary>
/// One named mobile actor-market: the actual admitted merchant actor, the storage and account it already
/// owns, one pinned commodity definition, the merchant's admitted home-place identity and its versioned
/// price rule. The market trades wherever its merchant actually stands; every trade and reading requires
/// the merchant's live presence and actual mutual reach, never a place identity. The binding owns no stock
/// or money; inventory, production and progression keep those facts.
/// </summary>
public sealed class MarketBinding
{
    public MarketBinding(EntityId marketId, EntityId merchantActorId, EntityId custodianId, EntityId accountId,
        CountedItemDefinition commodity, EntityId homePlaceId, MarketPricePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(commodity);
        ArgumentNullException.ThrowIfNull(policy);
        EntityId[] roles = [marketId, merchantActorId, custodianId, accountId, commodity.DefinitionId, homePlaceId];
        if (roles.Distinct().Count() != roles.Length || policy.FeePayeeAccountId == accountId)
        {
            throw new ArgumentException("A market names distinct roles, and its fee payee is not the merchant's own account.");
        }
        MarketId = marketId;
        MerchantActorId = merchantActorId;
        CustodianId = custodianId;
        AccountId = accountId;
        Commodity = commodity;
        HomePlaceId = homePlaceId;
        Policy = policy;
        ContentHash = ContentHash.Compute(Encode());
    }

    public EntityId MarketId { get; }
    public EntityId MerchantActorId { get; }
    public EntityId CustodianId { get; }
    public EntityId AccountId { get; }
    public CountedItemDefinition Commodity { get; }

    /// <summary>
    /// The merchant's admitted home-place identity, an origin pin checked at admission. Presence keeps it
    /// when the merchant walks, so it is neither the merchant's current location nor a stall boundary; a
    /// fixed venue would need its own admitted geometry.
    /// </summary>
    public EntityId HomePlaceId { get; }
    public MarketPricePolicy Policy { get; }
    public ContentHash ContentHash { get; }

    /// <summary>
    /// The usable stock this market prices from: unreserved, unprotected raw and produced lots of the exact
    /// commodity pin in the merchant's storage. It is a checked derived witness over the existing owners,
    /// never a second quantity ledger. The count saturates one unit above the rule's admitted stock bound,
    /// where the rule posts no terms, so no admitted holding can overflow it.
    /// </summary>
    public ulong CountUsableStock(CountedAuthorityState inventory, IEnumerable<TransformedLot> produced)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(produced);
        const ulong beyondDomain = MarketPricePolicy.MaximumStock + 1;
        ulong total = 0;
        void Count(ulong quantity) => total = quantity >= beyondDomain - total ? beyondDomain : total + quantity;
        foreach (CountedSourceLot lot in inventory.Custody.Lots)
        {
            if (lot.CustodianId == CustodianId && lot.Definition.Equals(Commodity) && lot.ReservedBy is null
                && lot.Protection == LotProtection.None)
            {
                Count(lot.Quantity);
            }
        }
        foreach (TransformedLot lot in produced)
        {
            if (lot.CustodianId == CustodianId && lot.Definition.Equals(Commodity) && lot.ReservedBy is null
                && lot.Protection == LotProtection.None)
            {
                Count(lot.Quantity);
            }
        }
        return total;
    }

    /// <summary>The merchant's posted single-unit terms at one usable stock count.</summary>
    public MarketPostedTerms PostedTerms(ulong usableStock) =>
        new(Policy.Ask(usableStock, 1), Policy.Bid(usableStock, 1), Policy.UnitFee);

    public byte[] Encode() => CanonicalEncodingV1.Frame("market.binding.v1", MarketId.ToByteArray(),
        MerchantActorId.ToByteArray(), CustodianId.ToByteArray(), AccountId.ToByteArray(),
        Commodity.DefinitionId.ToByteArray(), Commodity.Version.ToByteArray(), HomePlaceId.ToByteArray(), Policy.Encode());
}

/// <summary>
/// The posted single-unit terms of one market state: the merchant's ask (absent with no usable stock), its
/// bid (absent at the holding limit or without a positive bid) and the per-unit market due of each fill.
/// </summary>
public sealed record MarketPostedTerms(ulong? AskUnitPrice, ulong? BidUnitPrice, ulong UnitFee);
