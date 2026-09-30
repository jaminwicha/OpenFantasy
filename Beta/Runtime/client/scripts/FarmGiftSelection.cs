using OpenFantasy.Core;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Explicit physical stock and participant choices. Inspection never submits or refreshes consent.</summary>
internal sealed class FarmGiftSelection
{
    private (EntityId World, EntityId Branch, EntityId Actor)? _scope;

    internal EntityId? LotId { get; private set; }
    internal ConsentedMaterialLotKind? LotKind { get; private set; }
    internal EntityId? RecipientActorId { get; private set; }
    internal ulong Quantity { get; private set; } = 1;
    internal ProposeMaterialIntent? PreparedGift { get; private set; }
    internal MaterialProposalView? SelectedOffer { get; private set; }

    internal bool SelectLot(LocalWorldView view, ConsentedMaterialLotKind kind, EntityId id)
    {
        SelectScope(view);
        LotKind = kind;
        LotId = id;
        PreparedGift = null;
        return view.MaterialProposals?.Preparation?.Lots.Any(lot => lot.Kind == kind && lot.LotId == id) == true;
    }

    internal bool SelectRecipient(LocalWorldView view, EntityId id)
    {
        SelectScope(view);
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
    internal bool Prepare(LocalWorldView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        PreparedGift = _scope == (view.WorldId, view.BranchId, view.PlayerActorId)
            && LotKind is { } kind && LotId is { } lot && RecipientActorId is { } recipient
            ? view.MaterialProposals?.Preparation?.Prepare(kind, lot, recipient, Quantity) : null;
        return PreparedGift is not null;
    }

    /// <summary>Captures the complete row, including the sender observation used for acceptance.</summary>
    internal bool SelectOffer(LocalWorldView view, EntityId id)
    {
        ArgumentNullException.ThrowIfNull(view);
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

    private void SelectScope(LocalWorldView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var scope = (view.WorldId, view.BranchId, view.PlayerActorId);
        if (_scope == scope) return;
        ClearLot();
        ClearRecipient();
        ClearOffer();
        _scope = scope;
    }
}
