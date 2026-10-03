using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Four separate gift actions from explicit public selections; quest delivery remains separate.</summary>
internal static class MaterialProposalActionOffers
{
    internal static bool IsGiftAction(FarmSemanticAction action) => action is FarmSemanticAction.ProposeMaterial
        or FarmSemanticAction.AcceptMaterial or FarmSemanticAction.DeclineMaterial or FarmSemanticAction.WithdrawMaterial;

    internal static void Add(LocalWorldView view, FarmActionScope scope, FarmGiftSelection? selection,
        IDictionary<FarmSemanticAction, FarmActionOffer> offers, EntityId? woodDefinitionId = null,
        EntityId? berryDefinitionId = null) =>
        Add(FarmInventoryPresentation.From(view), scope, selection, offers, woodDefinitionId, berryDefinitionId);

    internal static void Add(FarmInventoryPresentation view, FarmActionScope scope, FarmGiftSelection? selection,
        IDictionary<FarmSemanticAction, FarmActionOffer> offers, EntityId? woodDefinitionId = null,
        EntityId? berryDefinitionId = null)
    {
        if (view.MaterialProposals is null) return;
        string? contextBlocker = view.ContextBlocker
            ?? (selection is not null && !selection.MatchesScope(view)
                ? "Choose and prepare gifts for the current character selection." : null);
        offers[FarmSemanticAction.ProposeMaterial] = Create(view, scope, selection, contextBlocker,
            woodDefinitionId, berryDefinitionId);
        foreach (FarmSemanticAction action in new[] { FarmSemanticAction.AcceptMaterial,
            FarmSemanticAction.DeclineMaterial, FarmSemanticAction.WithdrawMaterial })
        {
            offers[action] = Reply(view, scope, selection?.SelectedOffer, action, contextBlocker,
                woodDefinitionId, berryDefinitionId);
        }
    }

    private static FarmActionOffer Create(FarmInventoryPresentation view, FarmActionScope scope, FarmGiftSelection? selection,
        string? contextBlocker, EntityId? woodDefinitionId, EntityId? berryDefinitionId)
    {
        ProposeMaterialIntent? prepared = selection?.PreparedGift;
        EntityId? recipient = prepared?.Terms.RecipientActorId ?? selection?.RecipientActorId;
        string partner = view.Name(recipient);
        string cost = prepared is null ? "Choose a stack and quantity"
            : $"{prepared.Terms.Quantity} {view.ItemName(prepared.Terms.Definition.DefinitionId, woodDefinitionId, berryDefinitionId)}";
        string? blocker = contextBlocker
            ?? (selection?.LotId is null || selection.LotKind is null ? "Choose one inventory stack in your pack."
            : recipient is null ? "Choose the named recipient."
            : prepared is null ? "Prepare the selected stack, recipient and quantity in your pack."
            : view.MaterialProposals!.Preparation?.CanStillPropose(prepared) != true
                ? "This exact prepared gift changed or expired. Inspect the selection and prepare it again." : null);
        string detail = "Creation reserves no stock. The recipient must separately accept this exact gift."
            + (prepared is null ? " Inspection spends nothing."
                : $" To {partner}. Available briefly while world time advances. Status: Prepared.");
        if (view.IsSelected) detail += " " + view.AvailabilityNote;
        return new(FarmSemanticAction.ProposeMaterial, scope, "Create gift", partner, recipient, cost,
            $"You offered {cost} to {partner}.", blocker is null ? prepared : null, blocker, detailText: detail);
    }

    private static FarmActionOffer Reply(FarmInventoryPresentation view, FarmActionScope scope, MaterialProposalView? selected,
        FarmSemanticAction action, string? contextBlocker, EntityId? woodDefinitionId, EntityId? berryDefinitionId)
    {
        string label = action switch
        {
            FarmSemanticAction.AcceptMaterial => "Accept gift",
            FarmSemanticAction.DeclineMaterial => "Decline gift",
            _ => "Withdraw gift"
        };
        MaterialProposalView? current = selected is null ? null
            : view.MaterialProposals!.Offers.FirstOrDefault(offer => offer.ProposalId == selected.ProposalId);
        EntityId? partnerId = selected is null ? null
            : selected.ViewerIsSender ? selected.Terms.RecipientActorId : selected.Terms.SenderActorId;
        string partner = view.Name(partnerId);
        string quantity = selected is null ? "Select an offer"
            : $"{selected.Terms.Quantity} {view.ItemName(selected.Terms.Definition.DefinitionId, woodDefinitionId, berryDefinitionId)}";
        bool withdrawing = action == FarmSemanticAction.WithdrawMaterial;
        string? blocker = contextBlocker
            ?? (selected is null ? "Select one saved gift offer in your pack."
            : selected.Terms.WorldId != view.WorldId || selected.Terms.BranchId != view.BranchId
                || (selected.ViewerIsSender ? selected.Terms.SenderActorId : selected.Terms.RecipientActorId) != view.ActorId
                ? "This selected gift belongs to another participant or world."
            : current is null ? "This selected gift is no longer in the live list. Inspect its recorded outcome explicitly."
            : current.Status != MaterialProposalStatus.Open ? $"This gift is {current.Status.ToString().ToLowerInvariant()}."
            : selected.Status != MaterialProposalStatus.Open ? "This selected gift is already closed."
            : selected.ViewerIsSender != withdrawing ? withdrawing
                ? "Only your own outgoing gift can be withdrawn." : "Choose an incoming gift addressed to you."
            : (current with { SenderPresenceRevision = selected.SenderPresenceRevision }) != selected
                ? "This exact gift changed. Select the offer again."
            : action == FarmSemanticAction.AcceptMaterial && current.SenderPresenceRevision != selected.SenderPresenceRevision
                ? "The sender observation changed. Inspect and select the offer again."
            : action == FarmSemanticAction.AcceptMaterial && selected.SenderPresenceRevision is null
                ? "The sender is not present to be observed." : null);
        WorldActionIntent? intent = blocker is not null ? null : action switch
        {
            FarmSemanticAction.AcceptMaterial => selected!.PrepareAcceptance(),
            FarmSemanticAction.DeclineMaterial => new DeclineMaterialProposalIntent(selected!.ProposalId),
            _ => new WithdrawMaterialProposalIntent(selected!.ProposalId)
        };
        string detail = selected is null ? "Inspection and selection spend nothing."
            : $"{quantity} {(selected.ViewerIsSender ? "to" : "from")} {partner}; expires as world time advances. "
                + $"Status: {current?.Status.ToString() ?? selected.Status.ToString()}.";
        detail += " Open gifts reserve no stock. Acceptance transfers stock; it does not heal or grant care permission.";
        if (view.IsSelected) detail += " " + view.AvailabilityNote;
        string completion = action switch
        {
            FarmSemanticAction.AcceptMaterial => $"You accepted {quantity} from {partner}.",
            FarmSemanticAction.DeclineMaterial => $"You declined {partner}'s gift.",
            _ => $"You withdrew your gift to {partner}."
        };
        return new(action, scope, label, partner, partnerId,
            action == FarmSemanticAction.AcceptMaterial ? $"Receive {quantity}" : FarmActionOffers.NoCost,
            completion, intent, blocker, detailText: detail);
    }

    internal static string ActorName(LocalWorldView view, EntityId? actorId) =>
        FarmInventoryPresentation.From(view).Name(actorId);

    /// <summary>Only permitted public material descriptions are used.</summary>
    internal static string ItemName(LocalWorldView view, EntityId definitionId, EntityId? woodDefinitionId = null,
        EntityId? berryDefinitionId = null) =>
        FarmInventoryPresentation.From(view).ItemName(definitionId, woodDefinitionId, berryDefinitionId);
}
