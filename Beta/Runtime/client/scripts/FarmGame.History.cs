using Godot;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

public partial class FarmGame
{
    private FarmHistoryPanel _historyPanel = null!;
    private readonly FarmHistoryModel _history = new();
    internal bool IsHistoryOpen => _input.HistoryOpen;
    internal FarmHistoryModel History => _history;

    private void ReadyHistory()
    {
        _historyPanel = GD.Load<PackedScene>("res://scenes/FarmHistoryPanel.tscn").Instantiate<FarmHistoryPanel>();
        AddChild(_historyPanel);
        _historyPanel.CloseRequested += () => Perform(FarmSemanticAction.CloseHistory);
        _historyPanel.NavigationRequested += NavigateHistory;
        _historyPanel.SelectionRequested += (capture, kind, subject) =>
        {
            if (!_input.Permits(Situation(), FarmSemanticAction.InspectHistory)) return;
            _history.Update();
            _history.Select(capture, kind, subject);
            PresentHistoryPanel();
        };
    }

    private void OpenHistory()
    {
        if (_session is null || !_input.Permits(Situation(), FarmSemanticAction.OpenHistory)) return;
        if (_input.WheelOpen) ChangeWheel(false);
        try { SendMovement(0, 0); }
        catch (InvalidOperationException error) { _hud.Message(error.Message); return; }
        _history.Open(_session.World as IPrivateHistoryPlayerSession);
        _input.SetHistoryOpen(true);
        _historyPanel.Open(_history);
        _hud.SetCovered(true);
        PresentHistoryPanel();
        _historyPanel.FocusSelection();
    }

    private void CloseHistory()
    {
        _input.CaptureBoundary(Situation(), ReadWalkingInput());
        _history.Close();
        _input.SetHistoryOpen(false);
        _hud.SetCovered(false);
        _historyPanel.Close();
    }

    private void NavigateHistory(FarmSemanticAction action, FarmHistoryCapture capture,
        LocalPrivateHistoryKind? kind, EntityId? subject)
    {
        if (!_input.Permits(Situation(), action)) return;
        _history.Update();
        switch (action)
        {
            case FarmSemanticAction.RefreshHistory:
                _history.Refresh(capture);
                break;
            case FarmSemanticAction.NextHistory:
                _history.Next(capture);
                break;
            case FarmSemanticAction.InspectHistory when kind is { } selectedKind && subject is { } selectedSubject:
                _history.Inspect(capture, selectedKind, selectedSubject);
                break;
        }
        PresentHistoryPanel();
    }

    private void CompleteHistory()
    {
        // Completion observation is frame-driven; Update never schedules discovery or a retry.
        _history.Update();
        if (_input.HistoryOpen && !_history.IsOpen) CloseHistory();
    }

    private void PresentHistoryPanel()
    {
        _hud.SetHistoryAvailable(_input.Permits(Situation(), FarmSemanticAction.OpenHistory));
        if (_session is not null && _input.HistoryOpen)
            _historyPanel.Present(_history, _session.World.ReadRunState(), _session.IsRemote, _input.Paused);
    }
}
