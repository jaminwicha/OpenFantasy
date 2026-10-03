using OpenFantasy.Core;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Explicit physical stock and participant choices. Inspection never submits or refreshes consent.</summary>
internal sealed class FarmGiftSelection
{
    private FarmInventoryScope? _scope;

    internal EntityId? LotId { get; private set; }
    internal ConsentedMaterialLotKind? LotKind { get; private set; }
    internal EntityId? RecipientActorId { get; private set; }
    internal ulong Quantity { get; private set; } = 1;
    internal ProposeMaterialIntent? PreparedGift { get; private set; }
    internal MaterialProposalView? SelectedOffer { get; private set; }

    internal bool SelectLot(LocalWorldView view, ConsentedMaterialLotKind kind, EntityId id) => SelectLot(FarmInventoryPresentation.From(view), kind, id);

    internal bool SelectLot(FarmInventoryPresentation view, ConsentedMaterialLotKind kind, EntityId id)
    {
        ObserveScope(view);
        LotKind = kind;
        LotId = id;
        PreparedGift = null;
        return view.MaterialProposals?.Preparation?.Lots.Any(lot => lot.Kind == kind && lot.LotId == id) == true;
    }

    internal bool SelectRecipient(LocalWorldView view, EntityId id) => SelectRecipient(FarmInventoryPresentation.From(view), id);

    internal bool SelectRecipient(FarmInventoryPresentation view, EntityId id)
    {
        ObserveScope(view);
        RecipientActorId = id;
        PreparedGift = null;
        return view.MaterialProposals?.Preparation?.RecipientActorIds.Contains(id) == true;
    }

    internal bool SetQuantity(ulong quantity)
    {
        PreparedGift = null;
        if (quantity == 0) return false;
        Quantity = quantity;
        return true;
    }

    /// <summary>Only this explicit preparation captures a new expiry; frame refresh must not call it.</summary>
    internal bool Prepare(LocalWorldView view) => Prepare(FarmInventoryPresentation.From(view));

    internal bool Prepare(FarmInventoryPresentation view)
    {
        ArgumentNullException.ThrowIfNull(view);
        ObserveScope(view);
        PreparedGift = _scope == view.Scope
            && LotKind is { } kind && LotId is { } lot && RecipientActorId is { } recipient
            ? view.MaterialProposals?.Preparation?.Prepare(kind, lot, recipient, Quantity) : null;
        return PreparedGift is not null;
    }

    /// <summary>Captures the complete row, including the sender observation used for acceptance.</summary>
    internal bool SelectOffer(LocalWorldView view, EntityId id) => SelectOffer(FarmInventoryPresentation.From(view), id);

    internal bool SelectOffer(FarmInventoryPresentation view, EntityId id)
    {
        ArgumentNullException.ThrowIfNull(view);
        ObserveScope(view);
        SelectedOffer = view.MaterialProposals?.Offers.FirstOrDefault(offer => offer.ProposalId == id);
        return SelectedOffer is not null;
    }

    internal void ClearLot()
    {
        LotId = null;
        LotKind = null;
        PreparedGift = null;
    }

    internal void ClearRecipient()
    {
        RecipientActorId = null;
        PreparedGift = null;
    }

    internal void ClearOffer() => SelectedOffer = null;

    internal bool MatchesScope(FarmInventoryPresentation view) => _scope == view.Scope;

    internal void Reset()
    {
        ClearLot();
        ClearRecipient();
        ClearOffer();
        Quantity = 1;
        _scope = null;
    }

    internal void ObserveScope(FarmInventoryPresentation view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (_scope == view.Scope) return;
        Reset();
        _scope = view.Scope;
    }
}
