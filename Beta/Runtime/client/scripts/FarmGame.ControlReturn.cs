using Godot;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

public partial class FarmGame
{
    private PlayerControlStatus? _observedControlReturn;
    private PlayerControlStatus _presentedControl = PlayerControlStatus.Available;

    internal PlayerControlStatus ParticipantControl => _session?.World.ReadControlStatus()
        ?? PlayerControlStatus.Available;

    /// <summary>Consume the participant boundary without converting observation or regained focus into consent.</summary>
    private void ObserveControlReturn(PlayerControlStatus control)
    {
        if (control.State == PlayerControlState.Available) return;
        if (_observedControlReturn is { } previous && previous.State == control.State
            && previous.PortId == control.PortId && previous.Generation == control.Generation) return;

        _observedControlReturn = control;
        _input.RequireControlResume(control.State == PlayerControlState.Pending);
        _input.CaptureBoundary(Situation(), ReadWalkingInput());
        _input.ReconcileHeldActions(FarmInputActions.Routed.Where(action => Input.IsActionPressed(action)));
        if (_input.InventoryOpen) ChangeInventory(false);
        if (_input.WheelOpen) ChangeWheel(false);
        if (_input.MarketOpen) CloseMarket();
        if (_input.RecipesOpen) CloseRecipes();
        if (_input.CareOpen) CloseCare();
        if (_input.HistoryOpen) CloseHistory();
        ChangeReadiness(false);
    }

    private void PresentControlReturn(PlayerControlStatus control)
    {
        _presentedControl = control;
        bool pending = control.State == PlayerControlState.Pending;
        _hud.SetControlPending(pending);
        if (pending)
            _hud.Message("Finishing your character's committed action. Control will return when it is safe.");
        else if (control.State == PlayerControlState.Restored && _input.Paused)
            _hud.Message("Control returned. Review your surroundings, then press Resume when you are ready.");
    }

    /// <summary>Only the explicit pause control can acknowledge the captured restored observation.</summary>
    private bool ResumeReturnedControl()
    {
        if (_session is null) return false;
        PlayerControlStatus control = _presentedControl;
        if (control.State == PlayerControlState.Pending) return true;
        if (control.State != PlayerControlState.Restored) return false;
        if (!_input.Focused) return true;
        try
        {
            _session.World.Resume(control);
            _input.CaptureBoundary(Situation(), ReadWalkingInput());
            _input.TogglePause();
            _hud.Message("Control resumed.");
        }
        catch (InvalidOperationException error)
        {
            _hud.Message("Control could not resume. " + error.GetBaseException().Message);
        }
        return true;
    }
}
