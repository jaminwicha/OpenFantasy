using OpenFantasy.Core;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

public partial class FarmGame
{
    private GiftHistoryRead? _giftInspection;

    private sealed record GiftHistoryRead(FarmInventoryPresentation Presentation, ulong PanelLifetime,
        ulong OfferSelectionVersion, EntityId ProposalId, Task<MaterialProposalView?>? Home,
        Task<LocalSelectedQueryResult<MaterialProposalView?>>? Selected)
    {
        internal bool IsCompleted => Home?.IsCompleted ?? Selected!.IsCompleted;
    }

    private void ReviewGifts()
    {
        if (_session is null || !_input.InventoryOpen || _inventory.CurrentPresentation is null
            || !_input.Permits(Situation(), FarmSemanticAction.CloseInventory)) return;
        _input.CaptureBoundary(Situation(), ReadWalkingInput());
        ChangeInventory(false);
        Present();
        if (!_input.Permits(Situation(), FarmSemanticAction.OpenWheel)) return;
        ChangeWheel(true);
        for (int page = 0; page < _wheel.Model!.PageCount && _wheel.Model.PageName != "Gifts"; page++)
            _wheel.NextPage();
    }

    private void InspectGiftHistory(EntityId proposalId)
    {
        if (_session is null || !_input.InventoryOpen || !_input.Focused || _giftInspection is not null
            || _inventory.CurrentPresentation is not { CanReadHistory: true } presentation
            || _gifts.SelectedOffer?.ProposalId != proposalId) return;
        try
        {
            _giftInspection = presentation.Selection is { } selection
                ? new(presentation, _inventory.PresentationLifetime, _inventory.GiftOfferSelectionVersion, proposalId,
                    null, _session.SelectedSession!.InspectSelectedMaterialProposalHistoryAsync(selection, proposalId))
                : new(presentation, _inventory.PresentationLifetime, _inventory.GiftOfferSelectionVersion, proposalId,
                    _session.World.InspectMaterialProposalAsync(proposalId), null);
            _inventory.ShowGiftHistory(proposalId, null, "Reading recorded history…");
        }
        catch (Exception error)
        {
            _inventory.ShowGiftHistory(proposalId, null, error.Message);
        }
    }

    private void CompleteGiftInspection()
    {
        if (_giftInspection is not { IsCompleted: true } inspection) return;
        _giftInspection = null;
        MaterialProposalView? result = null;
        string? failure = null;
        try
        {
            if (inspection.Selected is { } selected)
            {
                var response = selected.GetAwaiter().GetResult();
                response.ValidateFor(_session!.SelectedSession!.ReadSelectedObservation(),
                    LocalSelectedQueryOperation.InspectMaterialProposalHistory, inspection.ProposalId);
                result = response.Value;
            }
            else result = inspection.Home!.GetAwaiter().GetResult();
        }
        catch (Exception error) { failure = "History inspection unavailable: " + error.GetBaseException().Message; }
        // Admitted work still finishes and its failure is observed after a panel closes. Applying it
        // requires the exact selection and explicit offer inspection of this still-open presentation.
        if (_session is null || !_input.Focused || !_input.InventoryOpen || !_inventory.IsOpen
            || _inventory.PresentationLifetime != inspection.PanelLifetime
            || _inventory.GiftOfferSelectionVersion != inspection.OfferSelectionVersion
            || _gifts.SelectedOffer?.ProposalId != inspection.ProposalId
            || _inventory.CurrentPresentation is not { CanReadHistory: true } current
            || current.WorldId != inspection.Presentation.WorldId || current.BranchId != inspection.Presentation.BranchId
            || current.ActorId != inspection.Presentation.ActorId || current.Selection != inspection.Presentation.Selection) return;
        if (inspection.Selected is not null)
        {
            try { if (_session.ReadSelection()?.Selection != inspection.Presentation.Selection) return; }
            catch (InvalidOperationException) { return; }
        }
        _inventory.ShowGiftHistory(inspection.ProposalId, result, failure
            ?? (inspection.Selected is not null && result is null ? "No terminal recorded outcome is available for this selection." : null));
    }
}
