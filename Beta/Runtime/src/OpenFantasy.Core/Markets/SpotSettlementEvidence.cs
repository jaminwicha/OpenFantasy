using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Progression;

namespace OpenFantasy.Core.Markets;

/// <summary>
/// Evidence created by a funded, approved four-owner settlement. Hashes observe the payment owners;
/// they never replace their balances. No public constructor or decoder grants settlement evidence.
/// A durable world must still journal and replay the complete transaction under its own profile.
/// </summary>
public sealed class SpotSettlementEvidence
{
    internal SpotSettlementEvidence(CountedAuthorityState inventory, ProductionSnapshot production,
        ProgressionSnapshot progression, SpotQuoteBook book, SpotQuoteEntry entry, SpotAcceptance acceptance,
        TrustedCommandDelivery delivery, ulong step, CountedAuthorityState movedInventory,
        ProgressionSnapshot paidProgression, SpotQuoteBook filledBook, EntityId movedLotId)
    {
        Quote = entry.Quote;
        Acceptance = acceptance;
        IssuerDelivery = entry.IssuerDelivery;
        CounterpartyDelivery = delivery;
        Step = step;
        MovedLotId = movedLotId;
        BeforeInventoryHash = inventory.ContentHash;
        BeforeProductionHash = production.ContentHash;
        BeforeProgressionHash = progression.ContentHash;
        BeforeBookHash = book.ContentHash;
        AfterInventoryHash = movedInventory.ContentHash;
        AfterProgressionHash = paidProgression.ContentHash;
        AfterBookHash = filledBook.ContentHash;
        ContentHash = ContentHash.Compute(CanonicalEncodingV1.Frame("markets.spot.settlement-proof",
            CanonicalEncodingV1.Unsigned64(1), SpotExchangeCodecV1.Encode(Quote),
            SpotExchangeCodecV1.Encode(Acceptance), IssuerDelivery.PrincipalId.ToByteArray(),
            IssuerDelivery.SessionId.ToByteArray(), CounterpartyDelivery.PrincipalId.ToByteArray(),
            CounterpartyDelivery.SessionId.ToByteArray(), CanonicalEncodingV1.Unsigned64(Step),
            MovedLotId.ToByteArray(), BeforeInventoryHash.ToByteArray(), BeforeProductionHash.ToByteArray(),
            BeforeProgressionHash.ToByteArray(), BeforeBookHash.ToByteArray(), AfterInventoryHash.ToByteArray(),
            AfterProgressionHash.ToByteArray(), AfterBookHash.ToByteArray()));
    }

    public SpotQuote Quote { get; }
    public SpotAcceptance Acceptance { get; }
    public TrustedCommandDelivery IssuerDelivery { get; }
    public TrustedCommandDelivery CounterpartyDelivery { get; }
    public ulong Step { get; }
    public EntityId MovedLotId { get; }
    public ContentHash BeforeInventoryHash { get; }
    public ContentHash BeforeProductionHash { get; }
    public ContentHash BeforeProgressionHash { get; }
    public ContentHash BeforeBookHash { get; }
    public ContentHash AfterInventoryHash { get; }
    public ContentHash AfterProgressionHash { get; }
    public ContentHash AfterBookHash { get; }
    public ContentHash ContentHash { get; }

    internal SpotMaterialOutcome ReplayMaterial(CountedAuthorityState inventory, ContentHash productionHash,
        IReadOnlyCollection<TransformedLot> produced, Func<EntityId, bool> occupied)
    {
        bool Controller(TrustedCommandDelivery delivery, EntityId principal, EntityId session, EntityId actor) =>
            delivery.PrincipalId == principal && delivery.SessionId == session && inventory.CanControl(delivery, actor);
        if (inventory.ContentHash != BeforeInventoryHash || productionHash != BeforeProductionHash
            || Quote.WorldId != inventory.WorldId || Quote.BranchId != inventory.BranchId
            || !Acceptance.Binds(Quote) || Step < Quote.IssuedAtStep || Step > Quote.ExpiresAtStep
            || !Controller(IssuerDelivery, Quote.IssuerPrincipalId, Quote.IssuerSessionId, Quote.IssuerActorId)
            || !Controller(CounterpartyDelivery, Acceptance.PrincipalId, Acceptance.OriginSessionId, Quote.CounterpartyActorId)
            || !inventory.Policies.Any(value => value.CustodianId == Quote.SellerCustodianId && value.OwnerActorId == Quote.SellerActorId)
            || !inventory.Policies.Any(value => value.CustodianId == Quote.BuyerCustodianId && value.OwnerActorId == Quote.BuyerActorId))
        {
            throw new ArgumentException("Settlement evidence must match its exact preceding material owners and approvals.");
        }
        SpotMaterialOutcome result = new SpotMaterialTransferStep(inventory, produced, occupied).Apply(Quote, Acceptance);
        if (result.Rejection != SpotExchangeRejection.None || result.MovedLotId != MovedLotId
            || result.Inventory.ContentHash != AfterInventoryHash)
        {
            throw new ArgumentException("Settlement evidence must reproduce its exact paid material movement.");
        }
        return result;
    }
}
