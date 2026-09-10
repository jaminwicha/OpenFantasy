using Godot;
using OpenFantasy.Client.Presentation;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Godot presentation and input adapter; the retained host owns every gameplay transition.</summary>
public partial class FarmGame : Node2D
{
    private readonly FarmCanvas _canvas = new();
    private readonly Camera2D _camera = new() { Zoom = new Vector2(2, 2) };
    private readonly FarmHud _hud = new();
    private Task<FarmClientSession>? _opening;
    private FarmClientSession? _session;
    private Task<WorldActionReceipt>? _action;
    private Task? _closing;
    private string _actionDescription = "";
    private bool _paused;
    private bool _failed;
    private bool _focused = true;

    internal string? SavePathOverride { get; init; }
    internal bool StartPaused { get; init; }
    internal bool GatheringProfile { get; init; } = true;
    internal LocalWorldView? CurrentView => _session?.World.ReadView();
    internal bool HasPendingAction => _action is not null;
    internal bool HasFailed => _failed;
    internal bool IsLocallyPaused => _paused;
    internal bool IsWindowFocused => _focused;
    internal void RequestClose() => BeginClose();

    public override void _Ready()
    {
        AddChild(_canvas);
        Rect2 bounds = _canvas.LocalBounds;
        _camera.LimitLeft = (int)bounds.Position.X;
        _camera.LimitTop = (int)bounds.Position.Y;
        _camera.LimitRight = (int)bounds.End.X;
        _camera.LimitBottom = (int)bounds.End.Y;
        AddChild(_camera);
        AddChild(_hud);
        _hud.TalkRequested += Speak;
        _hud.GiveRequested += Give;
        _hud.GatherRequested += Gather;
        _hud.CloseRequested += CloseDialogue;
        _hud.PauseRequested += TogglePause;
        GetTree().AutoAcceptQuit = false;
        GetWindow().CloseRequested += BeginClose;
        GetWindow().FocusEntered += OnFocusEntered;
        GetWindow().FocusExited += OnFocusExited;
        _hud.Message("Opening the farm…");
        _paused = StartPaused;
        string savePath = SavePathOverride ?? ProjectSettings.GlobalizePath(GatheringProfile
            ? "user://worlds/wrens-farm-work.sqlite" : "user://worlds/wrens-farm.sqlite");
        _opening = FarmClientSession.OpenAsync(savePath, "aldermere-first-walk-v1", "Traveler", GatheringProfile);
    }

    public override void _Process(double delta)
    {
        if (_closing is { IsCompleted: true })
        {
            if (_closing.IsFaulted)
            {
                GD.PushError(_closing.Exception!.GetBaseException().Message);
            }
            GetTree().Quit(_closing.IsFaulted ? 1 : 0);
            return;
        }
        if (_closing is not null)
        {
            return;
        }
        if (_opening is { IsCompleted: true })
        {
            Task<FarmClientSession> opening = _opening;
            _opening = null;
            if (opening.IsCompletedSuccessfully)
            {
                _session = opening.Result;
                if (ChangeReadiness(_focused && !_paused)) _hud.Message("Find Wren by the rows.");
            }
            else
            {
                Fail("The farm could not be opened. " + opening.Exception!.GetBaseException().Message);
            }
        }
        if (_session is null || _failed || _closing is not null)
        {
            return;
        }
        if (_session.World.Completion.IsFaulted)
        {
            Fail("The world has stopped. Your save has been retained. " + _session.World.Completion.Exception!.GetBaseException().Message);
            return;
        }
        if (_action is { IsCompleted: true })
        {
            Task<WorldActionReceipt> action = _action;
            _action = null;
            _hud.Message(action.IsCompletedSuccessfully ? Describe(action.Result)
                : "That action was not completed. " + action.Exception?.GetBaseException().Message);
        }
        LocalWorldView view = _session.World.ReadView();
        _canvas.Present(new FarmCanvasFrame(view.Calendar, view.Actors.Select(actor => new FarmActorFrame(
            actor.ActorId, actor.DisplayName, actor.Feet, actor.ActorId == view.PlayerActorId,
            actor.ActorId == _session.WrenActorId ? FarmSpriteKind.Wren : FarmSpriteKind.Squire)),
            view.GatheringNodes.Select(node => new FarmGatherNodeFrame(node.NodeId, node.DisplayName, node.Feet,
                node.RequiredTool, node.Visibility))));
        _camera.Position = _canvas.FocusPosition + new Vector2(0, -26);
        _hud.Present(view, _session.WoodQuantity(view), _session.BerryQuantity(view), _action is not null, _paused || !_focused);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_session is null || _failed || _closing is not null)
        {
            return;
        }
        try
        {
            if (_paused || !_focused || _session.World.ReadView().HasReadingHold)
            {
                _session.World.SetMovement(0, 0);
                return;
            }
            Vector2 input = Input.GetVector("walk_left", "walk_right", "walk_up", "walk_down");
            long dx = 0;
            long dy = 0;
            if (Math.Abs(input.X) > Math.Abs(input.Y)) dx = Math.Sign(input.X) * WorldMovementInput.DisplacementPerStep;
            else if (input.Y != 0) dy = Math.Sign(input.Y) * WorldMovementInput.DisplacementPerStep;
            _session.World.SetMovement(dx, dy);
        }
        catch (InvalidOperationException error)
        {
            Fail("The world has stopped. " + error.GetBaseException().Message);
        }
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input.IsActionPressed("pause_world")) TogglePause();
        else if (input.IsActionPressed("talk"))
        {
            if (_session?.World.ReadView().Dialogue is not null) CloseDialogue();
            else Speak();
        }
        else if (input.IsActionPressed("give_wood")) Give();
        else if (input.IsActionPressed("gather")) Gather();
        else return;
        GetViewport().SetInputAsHandled();
    }

    private void Speak() => Submit(session => session.Speak(session.World.ReadView()), "You spoke with Wren.");
    private void Give() => Submit(session => session.GiveWood(session.World.ReadView()), "You handed Wren wood from your pack.");
    private void Gather() => Submit(session => session.Gather(session.World.ReadView()), "You gathered supplies.");
    private void CloseDialogue() => Submit(session => session.CloseDialogue(session.World.ReadView()), "The conversation ended.");

    private void Submit(Func<FarmClientSession, Task<WorldActionReceipt>> submit, string description)
    {
        if (_session is null || _action is not null || _paused || !_focused || _failed || _closing is not null) return;
        try
        {
            _actionDescription = description;
            _action = submit(_session);
        }
        catch (Exception error)
        {
            _hud.Message(error.Message);
        }
    }

    private string Describe(WorldActionReceipt receipt) => receipt.Status == CommandReceiptStatus.Applied
        ? _actionDescription : receipt.Rejection switch
        {
            WorldActionRejection.OutOfReach => "Move closer to interact.",
            WorldActionRejection.RevisionConflict => "The moment changed; try again.",
            WorldActionRejection.UnableToCommunicate => "You cannot understand one another right now.",
            _ => "That action is unavailable right now."
        };

    private void TogglePause()
    {
        if (_failed || _closing is not null) return;
        _paused = !_paused;
        if (ChangeReadiness(!_paused && _focused))
            _hud.Message(_paused ? "Paused. Press Esc to continue." : "Explore the farm and speak with Wren.");
    }

    private void OnFocusEntered()
    {
        _focused = true;
        if (!_failed && _closing is null) ChangeReadiness(!_paused);
    }

    private void OnFocusExited()
    {
        _focused = false;
        if (!_failed && _closing is null) ChangeReadiness(false);
    }

    private bool ChangeReadiness(bool ready)
    {
        try { _session?.World.SetReady(ready); return true; }
        catch (InvalidOperationException error) { Fail("The world has stopped. " + error.GetBaseException().Message); return false; }
    }

    private void Fail(string message)
    {
        _failed = true;
        try { _session?.World.SetReady(false); }
        catch (InvalidOperationException) { /* A failed host already stopped accepting input. */ }
        _hud.Message(message);
        GD.PushError(message);
    }

    private void BeginClose()
    {
        if (_closing is not null) return;
        _hud.Message("Closing the farm…");
        _closing = CloseAsync();
    }

    private async Task CloseAsync()
    {
        FarmClientSession? session = _session;
        if (session is null && _opening is { } opening)
        {
            try { session = await opening.ConfigureAwait(false); }
            catch { return; }
        }
        if (session is not null) await session.DisposeAsync().ConfigureAwait(false);
    }

    public override void _ExitTree()
    {
        GetWindow().CloseRequested -= BeginClose;
        GetWindow().FocusEntered -= OnFocusEntered;
        GetWindow().FocusExited -= OnFocusExited;
        _closing ??= CloseAsync();
        _ = ObserveShutdownAsync(_closing);
    }

    private static async Task ObserveShutdownAsync(Task closing)
    {
        try { await closing.ConfigureAwait(false); }
        catch (Exception error) { System.Diagnostics.Trace.TraceError("World shutdown failed: " + error); }
    }
}
