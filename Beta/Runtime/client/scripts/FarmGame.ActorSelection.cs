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
    private ulong _controlPanelEpoch;
    private ulong _controlReadEpoch;
    private ulong _controlOperationEpoch;
    private ulong _selectedInquiryEpoch;
    private bool _controlRefreshRequested;

    // Passive probe observations of the same session and presented boundary used by ordinary input.
    internal LocalSelectedPlayerObservation? CurrentSelection => _session?.ReadSelection();
    internal LocalSelectedPlayerObservation? PresentedSelection => _presentedSelection;
    internal bool ControlOperationPending => _controlOperation is not null || _selectedInquiry is not null || _controlBoardRead is not null;
    internal string? LastControlOperationStatus { get; private set; }
    internal WorldActionCommand? LastControlPermissionCommand { get; private set; }
    internal Task<WorldActionReceipt>? LastControlPermissionReceipt { get; private set; }

    private void ReadyActorSelection()
    {
        _controlPanel = GD.Load<PackedScene>("res://scenes/FarmControlPanel.tscn").Instantiate<FarmControlPanel>();
        AddChild(_controlPanel);
        _controlPanel.Enable(false);
        _controlPanel.OpenRequested += OpenControlPanel;
        _controlPanel.CloseRequested += CloseControlPanel;
        _controlPanel.RefreshRequested += RefreshControlPanel;
        _controlPanel.PermissionRequested += (board, intent) => StartControlOperation(() =>
        {
            var command = _session!.PreparePermissionCommand(board, intent);
            var receipt = _session.SelectedSession!.SubmitControlPermissionAsync(command);
            LastControlPermissionCommand = command;
            LastControlPermissionReceipt = receipt;
            return receipt;
        });
        _controlPanel.AcquisitionRequested += acquisition => StartControlOperation(() =>
            _session!.SelectedSession!.AcquireControlAsync(acquisition));
        _controlPanel.ReleaseRequested += witness => StartControlOperation(() =>
            _session!.SelectedSession!.ReleaseControlAsync(witness));
        _controlPanel.SelectionRequested += (observation, actor) => StartControlOperation(() =>
            _session!.SelectedSession!.SelectActorAsync(observation.Selection, actor));
        _controlPanel.InquiryRequested += (observation, inquiry) =>
        {
            if (ControlOperationPending || !_controlPanel.IsOpen || !_input.Focused) return;
            try
            {
                _selectedInquiry = _session!.SelectedSession!.SubmitSelectedTradeInquiryAsync(observation.Selection, inquiry);
                _selectedInquiryEpoch = _controlPanelEpoch;
                _controlPanel.SetBusy(true);
            }
            catch (InvalidOperationException error) { _controlPanel.Notice(error.Message); }
        };
        _controlPanel.ConfirmationRequested += offer =>
        {
            if (!_controlPanel.IsOpen || ControlOperationPending) return;
            if (_input.Paused || !_input.Focused || _action is not null)
            { _controlPanel.Notice("Resume this character before confirming a trade."); return; }
            CloseControlPanel();
            Submit(offer);
        };
    }

    private void StartControlOperation(Func<Task> operation)
    {
        if (ControlOperationPending || !_controlPanel.IsOpen || !_input.Focused) return;
        try
        {
            _controlOperation = operation();
            _controlOperationEpoch = _controlPanelEpoch;
            LastControlOperationStatus = "Waiting for the control choice to be recorded.";
            _controlPanel.Notice(LastControlOperationStatus);
            _controlPanel.SetBusy(true);
        }
        catch (InvalidOperationException error)
        {
            LastControlOperationStatus = "Control choice refused: " + error.Message;
            _controlPanel.Notice(LastControlOperationStatus);
        }
    }

    private void OpenControlPanel()
    {
        if (_controlPanel.IsOpen || !_input.Focused
            || _session?.SelectedSession is not { SupportsControlPermissions: true }) return;
        if (_input.WheelOpen) ChangeWheel(false);
        if (_input.InventoryOpen) ChangeInventory(false);
        if (_input.MarketOpen) CloseMarket();
        if (_input.RecipesOpen) CloseRecipes();
        _input.CaptureBoundary(Situation(), ReadWalkingInput());
        _input.SetInventoryOpen(true);
        SendMovement(0, 0);
        _controlPanelEpoch++;
        _controlNotice = null;
        _controlPanel.Open();
        RefreshControlPanel();
    }

    private void CloseControlPanel()
    {
        if (!_controlPanel.IsOpen) return;
        _controlPanelEpoch++;
        _controlRefreshRequested = false;
        _controlNotice = null;
        _controlPanel.Close();
        _input.SetInventoryOpen(false);
        _input.CaptureBoundary(Situation(), ReadWalkingInput());
    }

    private void RefreshControlPanel()
    {
        if (!_controlPanel.IsOpen || _session?.SelectedSession is not { SupportsControlPermissions: true } selected) return;
        if (_controlBoardRead is not null || _controlOperation is not null || _selectedInquiry is not null)
        { _controlRefreshRequested = true; return; }
        _controlRefreshRequested = false;
        try
        {
            _controlBoardRead = selected.ReadControlPermissionsAsync();
            _controlReadEpoch = _controlPanelEpoch;
            _controlPanel.SetBusy(true);
        }
        catch (InvalidOperationException error) { _controlPanel.Notice("Permissions unavailable: " + error.Message); }
    }

    private void CompleteControlOperations()
    {
        _controlPanel.Enable(_session?.SelectedSession?.SupportsControlPermissions == true);
        if (_controlOperation is { IsCompleted: true } operation)
        {
            _controlOperation = null;
            LastControlOperationStatus = operation is Task<WorldActionReceipt> { IsCompletedSuccessfully: true } receipt
                ? receipt.Result.Rejection == WorldActionRejection.None ? "Control choice recorded."
                    : "Control choice refused: " + receipt.Result.Rejection
                : operation.IsCompletedSuccessfully ? "Control choice completed."
                    : operation.IsCanceled ? "Control choice was cancelled."
                    : "Control choice refused: " + operation.Exception?.GetBaseException().Message;
            if (_controlPanel.IsOpen && _controlOperationEpoch == _controlPanelEpoch)
            {
                _controlNotice = LastControlOperationStatus;
                _controlPanel.Notice(_controlNotice);
                _controlRefreshRequested = true;
            }
        }
        if (_selectedInquiry is { IsCompleted: true } inquiry)
        {
            _selectedInquiry = null;
            try
            {
                var result = inquiry.GetAwaiter().GetResult();
                result.ValidateFor(_session!.SelectedSession!.ReadSelectedObservation(), LocalSelectedQueryOperation.SubmitTradeInquiry);
                if (_controlPanel.IsOpen && _selectedInquiryEpoch == _controlPanelEpoch)
                    _controlPanel.Notice("Request is " + result.Value.State + ". Refresh after the merchant answers; nothing is purchased yet.");
            }
            catch (Exception error)
            {
                if (_controlPanel.IsOpen && _selectedInquiryEpoch == _controlPanelEpoch)
                    _controlPanel.Notice("Request unavailable: " + error.GetBaseException().Message);
            }
        }
        if (_controlBoardRead is { IsCompleted: true } read)
        {
            _controlBoardRead = null;
            // Observe failures even when a closed/reopened panel no longer owns this response.
            string? failure = read.Exception?.GetBaseException().Message;
            if (_controlPanel.IsOpen && _controlReadEpoch == _controlPanelEpoch && read.IsCompletedSuccessfully)
            {
                _controlPanel.Present(read.Result, _session!.ReadSelection(), _session);
                if (_controlNotice is { } notice) { _controlPanel.Notice(notice); _controlNotice = null; }
            }
            else if (_controlPanel.IsOpen && _controlReadEpoch == _controlPanelEpoch)
                _controlPanel.Notice("Permissions unavailable: " + (failure ?? "request cancelled"));
        }
        if (_controlRefreshRequested && !ControlOperationPending) RefreshControlPanel();
        _controlPanel.SetBusy(ControlOperationPending);
    }

    private void ObserveActorSelection(LocalSelectedPlayerObservation? observation)
    {
        if (observation is null) return;
        bool changed = _presentedSelection?.Selection != observation.Selection;
        _presentedSelection = observation;
        if (!changed) return;
        _gifts.Reset();
        _input.RequireControlResume(observation.ControlStatus.State == PlayerControlState.Pending);
        _input.CaptureBoundary(Situation(), ReadWalkingInput());
        _input.ReconcileHeldActions(FarmInputActions.Routed.Where(action => Input.IsActionPressed(action)));
        if (_controlPanel.IsOpen) CloseControlPanel();
        if (_input.InventoryOpen) ChangeInventory(false);
        if (_input.WheelOpen) ChangeWheel(false);
        if (_input.MarketOpen) CloseMarket();
        if (_input.RecipesOpen) CloseRecipes();
        _recipeLibrary.Clear();
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
        _presented = _session!.CreateOffers(observation, _targets.Selected, _gifts);
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
        LocalControlledProductionView? production = view.Production;
        LocalWorksiteView? worksite = production?.Worksite;
        var plants = production?.GatheringNodes.Where(node => regional?.Admits(node.Feet) != false)
            .Select(node => new FarmGatherNodeFrame(node.NodeId, node.DisplayName, node.Feet,
                node.RequiredTool, node.Visibility));
        var structures = view.Structures is null ? production?.Construction.Where(project => project.IsCompleted
                && (regional?.Admits(project.SiteFeet) ?? worksite?.HasPhysicalConstruction != true))
            .Select(project => new FarmStructureFrame(project.ProjectId, project.DisplayName, project.SiteFeet)) : null;
        var publicStructures = view.Structures?.Where(structure => regional?.Admits(structure.Origin) != false);
        _canvas.Present(regional is null
            ? new FarmCanvasFrame(view.Calendar, actors, plants, structures, worksite, publicStructures: publicStructures)
            : new FarmCanvasFrame(view.Calendar, actors, regional, plants, structures,
                worksite?.ExcavationTargets.Where(target =>
                    regional.Observation.RegionAt(target.Cell.X, target.Cell.Y) is not null), publicStructures));
        Vector2 focus = _canvas.FocusPosition;
        if (regional is not null)
        {
            Rect2 viewport = GetViewport().GetVisibleRect();
            Rect2 field = _hud.FieldViewportBounds;
            var framing = new CargoCameraFraming(regional.Bounds,
                new CargoCanvasRect(viewport.Position.X, viewport.Position.Y, viewport.Size.X, viewport.Size.Y),
                new CargoCanvasRect(field.Position.X, field.Position.Y, field.Size.X, field.Size.Y),
                new CargoCanvasPoint(_camera.Zoom.X, _camera.Zoom.Y));
            CargoCanvasRect limits = framing.Limits;
            _camera.LimitLeft = checked((int)Math.Floor(limits.X));
            _camera.LimitTop = checked((int)Math.Floor(limits.Y));
            _camera.LimitRight = checked((int)Math.Ceiling(limits.Right));
            _camera.LimitBottom = checked((int)Math.Ceiling(limits.Bottom));
            focus += new Vector2((float)framing.FollowOffset.X, (float)framing.FollowOffset.Y);
        }
        _camera.Position = focus;
        _canvas.PresentTarget(_targets.Selected);
        _hud.PresentControlled(observation, _action is not null, _input.Paused || !_input.Focused, _presented, _targets);
        PresentControlReturn(observation.ControlStatus);
        if (view.Inventory is not null)
            _inventory.Present(FarmInventoryPresentation.From(observation), _session.World.ReadRunState(), _session.IsRemote);
        if (_wheel.Model is { } wheel)
        {
            wheel.Refresh(target => _session.CreateOffers(observation, target, _gifts));
            _wheel.Present(observation.HomeView, _session.World.ReadRunState(),
                _input.Permits(Situation(), FarmSemanticAction.ConfirmWheel), _session.IsRemote);
        }
    }
}
