using Godot;
using OpenFantasy.Client.Cargo;
using OpenFantasy.Client.Presentation;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting;

namespace OpenFantasy.Client;

public partial class FarmGame
{
    private LocalSelectedPlayerObservation? _presentedSelection;
    private FarmControlPanel _controlPanel = null!;
    private Task<LocalControlPermissionBoard>? _controlBoardRead;
    private Task? _controlOperation;
    private string? _controlNotice;
    private Task<LocalSelectedQueryResult<LocalTradeInquiryView>>? _selectedInquiry;

    private void ReadyActorSelection()
    {
        _controlPanel = GD.Load<PackedScene>("res://scenes/FarmControlPanel.tscn").Instantiate<FarmControlPanel>();
        AddChild(_controlPanel);
        _controlPanel.Enable(false);
        _controlPanel.OpenRequested += OpenControlPanel;
        _controlPanel.CloseRequested += CloseControlPanel;
        _controlPanel.RefreshRequested += RefreshControlPanel;
        _controlPanel.PermissionRequested += (board, intent) => StartControlOperation(() =>
            _session!.SelectedSession!.SubmitControlPermissionAsync(_session.PreparePermissionCommand(board, intent)));
        _controlPanel.AcquisitionRequested += acquisition => StartControlOperation(() =>
            _session!.SelectedSession!.AcquireControlAsync(acquisition));
        _controlPanel.ReleaseRequested += witness => StartControlOperation(() =>
            _session!.SelectedSession!.ReleaseControlAsync(witness));
        _controlPanel.SelectionRequested += (observation, actor) => StartControlOperation(() =>
            _session!.SelectedSession!.SelectActorAsync(observation.Selection, actor));
        _controlPanel.InquiryRequested += (observation, inquiry) =>
        {
            if (_selectedInquiry is not null) return;
            try { _selectedInquiry = _session!.SelectedSession!.SubmitSelectedTradeInquiryAsync(observation.Selection, inquiry); }
            catch (InvalidOperationException error) { _controlPanel.Notice(error.Message); }
        };
        _controlPanel.ConfirmationRequested += offer =>
        {
            if (_input.Paused || !_input.Focused || _action is not null)
            { _controlPanel.Notice("Resume this character before confirming a trade."); return; }
            CloseControlPanel();
            Submit(offer);
        };
    }

    private void StartControlOperation(Func<Task> operation)
    {
        if (_controlOperation is not null) return;
        try { _controlOperation = operation(); }
        catch (InvalidOperationException error) { _controlPanel.Notice(error.Message); }
    }

    private void OpenControlPanel()
    {
        if (_session?.SelectedSession is not { SupportsControlPermissions: true }) return;
        if (_input.WheelOpen) ChangeWheel(false);
        if (_input.InventoryOpen) ChangeInventory(false);
        if (_input.MarketOpen) CloseMarket();
        _input.CaptureBoundary(Situation(), ReadWalkingInput());
        _input.SetInventoryOpen(true);
        SendMovement(0, 0);
        _controlPanel.Open();
        RefreshControlPanel();
    }

    private void CloseControlPanel()
    {
        if (!_controlPanel.IsOpen) return;
        _controlPanel.Close();
        _input.SetInventoryOpen(false);
        _input.CaptureBoundary(Situation(), ReadWalkingInput());
    }

    private void RefreshControlPanel()
    {
        if (_controlBoardRead is not null || _session?.SelectedSession is not { SupportsControlPermissions: true } selected) return;
        _controlBoardRead = selected.ReadControlPermissionsAsync();
    }

    private void CompleteControlOperations()
    {
        _controlPanel.Enable(_session?.SelectedSession?.SupportsControlPermissions == true);
        if (_controlOperation is { IsCompleted: true } operation)
        {
            _controlOperation = null;
            _controlNotice = operation is Task<WorldActionReceipt> { IsCompletedSuccessfully: true } receipt
                ? receipt.Result.Rejection == WorldActionRejection.None ? "Control choice recorded."
                    : "Control choice refused: " + receipt.Result.Rejection
                : operation.IsCompletedSuccessfully ? "Control choice completed."
                    : "Control choice refused: " + operation.Exception?.GetBaseException().Message;
            _controlPanel.Notice(_controlNotice);
            RefreshControlPanel();
        }
        if (_selectedInquiry is { IsCompleted: true } inquiry)
        {
            _selectedInquiry = null;
            try
            {
                var result = inquiry.GetAwaiter().GetResult();
                result.ValidateFor(_session!.SelectedSession!.ReadSelectedObservation(), LocalSelectedQueryOperation.SubmitTradeInquiry);
                _controlPanel.Notice("Request is " + result.Value.State + ". Refresh after the merchant answers; nothing is purchased yet.");
            }
            catch (Exception error) { _controlPanel.Notice("Request unavailable: " + error.GetBaseException().Message); }
        }
        if (_controlBoardRead is { IsCompleted: true } read)
        {
            _controlBoardRead = null;
            if (read.IsCompletedSuccessfully)
            {
                _controlPanel.Present(read.Result, _session!.ReadSelection(), _session);
                if (_controlNotice is { } notice) { _controlPanel.Notice(notice); _controlNotice = null; }
            }
            else _controlPanel.Notice("Permissions unavailable: " + read.Exception?.GetBaseException().Message);
        }
    }

    private void ObserveActorSelection(LocalSelectedPlayerObservation? observation)
    {
        if (observation is null) return;
        bool changed = _presentedSelection?.Selection != observation.Selection;
        _presentedSelection = observation;
        if (!changed) return;
        _input.RequireControlResume(observation.ControlStatus.State == PlayerControlState.Pending);
        _input.CaptureBoundary(Situation(), ReadWalkingInput());
        _input.ReconcileHeldActions(FarmInputActions.Routed.Where(action => Input.IsActionPressed(action)));
        if (_controlPanel.IsOpen) CloseControlPanel();
        if (_input.InventoryOpen) ChangeInventory(false);
        if (_input.WheelOpen) ChangeWheel(false);
        if (_input.MarketOpen) CloseMarket();
        _targets?.Clear();
        try { _session!.SelectedSession!.PauseSelected(observation.Selection); }
        catch (AuthorityDispatchException error) { _hud.Message(error.Message); return; }
        catch (SelectedInputUnavailableException error) { _hud.Message(error.Message); return; }
        _hud.Message("Character selected. Review your surroundings, then press Resume.");
    }

    private FarmInputSituation SelectedSituation(LocalSelectedPlayerObservation observation)
    {
        LocalControlledActorView? view = observation.ControlledView;
        LocalWorldView home = observation.HomeView;
        return new(SessionOpen: true, ReadingHold: home.HasReadingHold || view?.HasReadingHold == true,
            ActionPending: _action is not null || home.IsResolvingConsequences || view?.IsResolvingConsequences == true)
        {
            IsInEncounter = view is null ? home.IsInEncounter : view.Combat?.Encounter is not null,
            IsEncounterOutcome = view is null ? home.Encounter?.IsOutcome == true : view.Combat?.Encounter?.IsOutcome == true,
            HasCareAcknowledgment = _presented.ContainsKey(FarmSemanticAction.AcknowledgeCare),
            HasOutcomeReadingBesideCare = _presented.TryGetValue(FarmSemanticAction.ReleaseEncounter, out var reading)
                && reading.Intent is OpenFantasy.Core.Encounters.AcknowledgeEncounterOutcomeIntent,
            HasWheelPages = _wheel.Model?.PageCount > 1,
            WheelChoiceOpensPanel = _wheel.Model?.Selected?.Offer.OpensPanel == true,
            HasEncounterEntry = _presented.ContainsKey(FarmSemanticAction.EnterEncounter)
        };
    }

    private void SendMovement(long dx, long dy)
    {
        if (_session is null) return;
        if (_session.SelectedSession is { SupportsActorSelection: true } selected)
        {
            if (_presentedSelection is not { } observation) return;
            if (observation.Selection.ControlWitness is { } witness
                && (witness.Capabilities & WorldControlCapabilities.Traversal) == 0) return;
            try { selected.SetSelectedMovement(observation.Selection, dx, dy); }
            catch (SelectedInputUnavailableException error) { _hud.Message(error.Message); }
        }
        else _session.World.SetMovement(dx, dy);
    }

    private void PresentControlled(LocalSelectedPlayerObservation observation)
    {
        LocalControlledActorView view = observation.ControlledView!;
        _targets!.Refresh(view);
        _presented = _session!.CreateOffers(observation, _targets.Selected);
        if (view.IsRegional && view.Regional is null)
        {
            _hud.Message("The selected character's regional observation is not available yet.");
            return;
        }
        var regional = view.Regional is { } terrain ? new CargoViewProjection(terrain) : null;
        var actors = view.Actors.Where(actor => regional?.Admits(actor.Feet) != false).Select(actor =>
        {
            var stall = _session.CargoStalls.FirstOrDefault(value => value.MerchantActorId == actor.ActorId);
            FarmSpriteKind kind = actor.ActorId == _session.WrenActorId ? FarmSpriteKind.Wren
                : stall is not null ? FarmSpriteKind.Merchant
                : view.Combat?.Actors.Any(enemy => enemy.ActorId == actor.ActorId && enemy.Side != 0
                    && enemy.Side != view.Combat?.Actors.FirstOrDefault(own => own.ActorId == view.ActorId)?.Side) == true
                    ? FarmSpriteKind.Wolf : FarmSpriteKind.Squire;
            var frame = regional is null
                ? new FarmActorFrame(actor.ActorId, actor.DisplayName, actor.Feet, actor.ActorId == view.ActorId, kind,
                    multilevel: view.Production?.Worksite is not null)
                : new FarmActorFrame(actor.ActorId, actor.DisplayName, actor.Feet, actor.ActorId == view.ActorId, kind, regional);
            return frame with { AppearanceKey = stall?.MerchantSourceId, IsIncapacitated = actor.IsIncapacitated,
                IsSelected = _targets.Selected?.Id == actor.ActorId };
        }).ToArray();
        if (actors.Count(actor => actor.IsOwnActor) != 1)
        {
            _hud.Message("The selected character's current floor cannot be shown. Return home or wait for a supported observation.");
            return;
        }
        _canvas.Present(regional is null ? new FarmCanvasFrame(view.Calendar, actors, worksite: view.Production?.Worksite)
            : new FarmCanvasFrame(view.Calendar, actors, regional));
        if (regional is not null)
        {
            _camera.LimitLeft = checked((int)Math.Floor(regional.Bounds.X));
            _camera.LimitTop = checked((int)Math.Floor(regional.Bounds.Y));
            _camera.LimitRight = checked((int)Math.Ceiling(regional.Bounds.Right));
            _camera.LimitBottom = checked((int)Math.Ceiling(regional.Bounds.Bottom));
        }
        _camera.Position = _canvas.FocusPosition;
        _canvas.PresentTarget(_targets.Selected);
        _hud.PresentControlled(observation, _action is not null, _input.Paused || !_input.Focused, _presented, _targets);
        PresentControlReturn(observation.ControlStatus);
        if (_wheel.Model is { } wheel)
        {
            wheel.Refresh(target => _session.CreateOffers(observation, target));
            _wheel.Present(observation.HomeView, _session.World.ReadRunState(),
                _input.Permits(Situation(), FarmSemanticAction.ConfirmWheel), _session.IsRemote);
        }
    }
}
