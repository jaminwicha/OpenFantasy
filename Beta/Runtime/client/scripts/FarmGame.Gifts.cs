using OpenFantasy.Core;

namespace OpenFantasy.Client;

public partial class FarmGame
{
    private void ReviewGifts()
    {
        if (_session is null || !_input.InventoryOpen
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
        if (_session is null || !_input.InventoryOpen || !_input.Focused || _giftInspection is not null) return;
        try
        {
            _inspectingGiftId = proposalId;
            _giftInspection = _session.World.InspectMaterialProposalAsync(proposalId);
            _inventory.ShowGiftHistory(proposalId, null, "Reading recorded history…");
        }
        catch (Exception error)
        {
            _inventory.ShowGiftHistory(proposalId, null, error.Message);
        }
    }
}
