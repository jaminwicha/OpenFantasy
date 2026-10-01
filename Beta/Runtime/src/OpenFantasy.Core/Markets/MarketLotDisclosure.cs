using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Markets;

/// <summary>
/// A seller's explicit, bounded showing of one selected lot of its own to one market's merchant, recorded
/// from the seller's own admitted command made in person. It pins the market and its merchant, the exact lot
/// kind, identity, definition and revision, the units shown, the seller's own storage and the account the
/// seller chose to be paid into, and its window and cause. It is the only thing that lets that merchant
/// prepare or issue a buy quote pinning the lot. It is not a gift permission, a quote acceptance, a sale, a
/// reservation or a stock owner, and it discloses nothing else in the seller's inventory or wallet.
/// </summary>
public sealed class MarketLotDisclosure
{
    public const int MaximumByteLength = 512;

    public MarketLotDisclosure(EntityId sellerActorId, EntityId marketId, EntityId merchantActorId, SpotLotKind lotKind,
        EntityId lotId, CountedItemDefinition definition, ulong lotRevision, ulong quantity, EntityId sellerCustodianId,
        EntityId sellerAccountId, ulong disclosedSequence, ulong disclosedAtStep, ulong expiresAtStep,
        EntityId causeCommandId, uint causeActionIndex)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!Enum.IsDefined(lotKind))
        {
            throw new ArgumentOutOfRangeException(nameof(lotKind), "A disclosure names one known lot owner.");
        }
        EntityId[] roles = [sellerActorId, marketId, merchantActorId, lotId, sellerCustodianId, sellerAccountId, causeCommandId];
        if (roles.Distinct().Count() != roles.Length || quantity == 0 || quantity > SpotQuote.MaximumQuantity
            || disclosedSequence == 0 || disclosedAtStep == 0 || disclosedAtStep > disclosedSequence
            || expiresAtStep <= disclosedAtStep)
        {
            throw new ArgumentException("A disclosure shows another merchant a bounded part of one lot for a dated window.");
        }
        SellerActorId = sellerActorId;
        MarketId = marketId;
        MerchantActorId = merchantActorId;
        LotKind = lotKind;
        LotId = lotId;
        Definition = definition;
        LotRevision = lotRevision;
        Quantity = quantity;
        SellerCustodianId = sellerCustodianId;
        SellerAccountId = sellerAccountId;
        DisclosedSequence = disclosedSequence;
        DisclosedAtStep = disclosedAtStep;
        ExpiresAtStep = expiresAtStep;
        CauseCommandId = causeCommandId;
        CauseActionIndex = causeActionIndex;
        ContentHash = ContentHash.Compute(Encode());
    }

    public EntityId SellerActorId { get; }
    public EntityId MarketId { get; }

    /// <summary>The market's merchant at disclosure; only it may quote against the shown lot.</summary>
    public EntityId MerchantActorId { get; }
    public SpotLotKind LotKind { get; }
    public EntityId LotId { get; }
    public CountedItemDefinition Definition { get; }

    /// <summary>The lot revision shown; any later spending, split or movement makes the showing stale.</summary>
    public ulong LotRevision { get; }

    /// <summary>The most units a quote under this disclosure may name.</summary>
    public ulong Quantity { get; }
    public EntityId SellerCustodianId { get; }

    /// <summary>The seller's own account it chose to be paid into; nothing else about its wallet is shown.</summary>
    public EntityId SellerAccountId { get; }

    /// <summary>The world revision that published the disclosure.</summary>
    public ulong DisclosedSequence { get; }

    /// <summary>The fixed-world advancing step that published the disclosure.</summary>
    public ulong DisclosedAtStep { get; }

    /// <summary>The disclosure is usable while the world has advanced fewer steps than this.</summary>
    public ulong ExpiresAtStep { get; }

    /// <summary>The seller's own admitted command that showed the lot.</summary>
    public EntityId CauseCommandId { get; }
    public uint CauseActionIndex { get; }
    public ContentHash ContentHash { get; }

    /// <summary>Whether a command applied after <paramref name="advancingSteps"/> steps may still use it.</summary>
    public bool IsLiveAt(ulong advancingSteps) => advancingSteps < ExpiresAtStep;

    /// <summary>
    /// Whether this live showing authorizes the merchant's buy quote: the quote names this seller as its
    /// counterparty, this merchant as its issuer, the exact shown lot, revision, storage and account, at most
    /// the shown units, and closes no later than the showing itself.
    /// </summary>
    public bool Authorizes(SpotQuote quote, ulong advancingSteps)
    {
        ArgumentNullException.ThrowIfNull(quote);
        return IsLiveAt(advancingSteps) && quote.Side == SpotQuoteSide.IssuerBuys
            && quote.CounterpartyActorId == SellerActorId && quote.IssuerActorId == MerchantActorId
            && quote.LotKind == LotKind && quote.LotId == LotId && quote.Definition.Equals(Definition)
            && quote.ExpectedLotRevision == LotRevision && quote.SellerCustodianId == SellerCustodianId
            && quote.SellerAccountId == SellerAccountId && quote.QuantityAllowance <= Quantity
            && quote.ExpiresAtStep <= ExpiresAtStep;
    }

    public byte[] Encode() => CanonicalEncodingV1.Frame("market.lot-disclosure.v1", SellerActorId.ToByteArray(),
        MarketId.ToByteArray(), MerchantActorId.ToByteArray(), CanonicalEncodingV1.Unsigned64((ulong)LotKind),
        LotId.ToByteArray(), Definition.DefinitionId.ToByteArray(), Definition.Version.ToByteArray(),
        CanonicalEncodingV1.Unsigned64(LotRevision), CanonicalEncodingV1.Unsigned64(Quantity),
        SellerCustodianId.ToByteArray(), SellerAccountId.ToByteArray(), CanonicalEncodingV1.Unsigned64(DisclosedSequence),
        CanonicalEncodingV1.Unsigned64(DisclosedAtStep), CanonicalEncodingV1.Unsigned64(ExpiresAtStep),
        CauseCommandId.ToByteArray(), CanonicalEncodingV1.Unsigned64(CauseActionIndex));
}
