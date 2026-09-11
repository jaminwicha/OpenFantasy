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
    private FarmHud _hud = null!;
    private FarmInputRouter _input = new();
    private Task<FarmClientSession>? _opening;
    private FarmClientSession? _session;
    private Task<WorldActionReceipt>? _action;
    private Task? _closing;
    private IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> _presented =
        new Dictionary<FarmSemanticAction, FarmActionOffer>();
    private string _actionDescription = "";
    private bool _failed;
    private FarmTargetSelection? _targets;
    private FarmInventoryModel? _inventoryModel;
    private FarmInventoryScreen _inventory = null!;
    private FarmQuickWheel _wheel = null!;

    internal string? SavePathOverride { get; init; }
    internal bool StartPaused { get; init; }
    internal bool GatheringProfile { get; init; } = true;
    internal bool ProductionProfile { get; init; } = true;
    internal bool QuestProfile { get; init; }
    internal bool ConstructionProfile { get; init; }
    internal string? UpgradeSourcePathOverride { get; init; }
    internal LocalWorldView? CurrentView => _session?.World.ReadView();
    /// <summary>The offers presented this frame; HUD buttons and keys execute exactly these.</summary>
    internal IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> CurrentOffers => _presented;
    internal FarmTarget? CurrentTarget => _targets?.Selected;
    internal bool HasPendingAction => _action is not null;
    internal bool HasFailed => _failed;
    internal bool IsLocallyPaused => _input.Paused;
    internal bool IsWindowFocused => _input.Focused;
    internal bool IsInventoryOpen => _input.InventoryOpen;
    internal bool IsWheelOpen => _input.WheelOpen;
    internal FarmDaylight Daylight => _canvas.Daylight;
    internal void RequestClose() => BeginClose();

    public override void _Ready()
    {
        _input = new FarmInputRouter(StartPaused);
        AddChild(_canvas);
        Rect2 bounds = _canvas.LocalBounds;
        _camera.LimitLeft = (int)bounds.Position.X;
        _camera.LimitTop = (int)bounds.Position.Y;
        _camera.LimitRight = (int)bounds.End.X;
        _camera.LimitBottom = (int)bounds.End.Y;
        AddChild(_camera);
        _hud = GD.Load<PackedScene>("res://scenes/FarmHud.tscn").Instantiate<FarmHud>();
        AddChild(_hud);
        _hud.ActionRequested += Perform;
        _inventory = GD.Load<PackedScene>("res://scenes/FarmInventoryScreen.tscn").Instantiate<FarmInventoryScreen>();
        AddChild(_inventory);
        _inventory.CloseRequested += () => Perform(FarmSemanticAction.CloseInventory);
        _wheel = GD.Load<PackedScene>("res://scenes/FarmQuickWheel.tscn").Instantiate<FarmQuickWheel>();
        AddChild(_wheel);
        _wheel.ActionRequested += Perform;
        Input.Singleton.JoyConnectionChanged += OnControllerConnectionChanged;
        GetTree().AutoAcceptQuit = false;
        GetWindow().CloseRequested += BeginClose;
        GetWindow().FocusEntered += OnFocusEntered;
        GetWindow().FocusExited += OnFocusExited;
        _hud.Message("Opening the farm…");
        bool production = GatheringProfile && ProductionProfile;
        bool quests = production && QuestProfile;
        bool construction = quests && ConstructionProfile;
        string savePath = SavePathOverride ?? ProjectSettings.GlobalizePath(construction
            ? "user://worlds/wrens-farm-construction.sqlite" : quests
            ? "user://worlds/wrens-farm-quests.sqlite" : production
            ? "user://worlds/wrens-farm-production.sqlite" : GatheringProfile
                ? "user://worlds/wrens-farm-work.sqlite" : "user://worlds/wrens-farm.sqlite");
        string? upgradeSource = UpgradeSourcePathOverride ?? (SavePathOverride is null && production
            ? ProjectSettings.GlobalizePath(construction ? "user://worlds/wrens-farm-quests.sqlite"
                : quests ? "user://worlds/wrens-farm-production.sqlite" : "user://worlds/wrens-farm-work.sqlite") : null);
        string? gatheringSource = SavePathOverride is null && quests
            ? ProjectSettings.GlobalizePath("user://worlds/wrens-farm-work.sqlite") : null;
        string? productionSource = SavePathOverride is null && construction
            ? ProjectSettings.GlobalizePath("user://worlds/wrens-farm-production.sqlite") : null;
        _opening = FarmClientSession.OpenAsync(savePath, "aldermere-first-walk-v1", "Traveler", GatheringProfile,
            production, upgradeSource, quests, gatheringSource, construction, productionSource);
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
                _targets = new FarmTargetSelection(_session.WrenActorId);
                _inventoryModel = new FarmInventoryModel(_session.WoodDefinitionId, _session.BerryDefinitionId);
                if (ChangeReadiness(_input.WorldShouldRun)) _hud.Message("Find Wren by the rows.");
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
        Present(_session.World.ReadView());
    }

    private void Present(LocalWorldView view)
    {
        if (_session is null || _targets is null)
        {
            return;
        }
        _targets.Refresh(view);
        _canvas.Present(new FarmCanvasFrame(view.Calendar, view.Actors.Select(actor => new FarmActorFrame(
            actor.ActorId, actor.DisplayName, actor.Feet, actor.ActorId == view.PlayerActorId,
            actor.ActorId == _session.WrenActorId ? FarmSpriteKind.Wren : FarmSpriteKind.Squire)),
            view.GatheringNodes.Select(node => new FarmGatherNodeFrame(node.NodeId, node.DisplayName, node.Feet,
                node.RequiredTool, node.Visibility)),
            view.Construction.Where(project => project.IsCompleted).Select(project =>
                new FarmStructureFrame(project.ProjectId, project.DisplayName, project.SiteFeet))));
        Vector2 focus = _canvas.FocusPosition;
        if (view.Dialogue is { } conversation)
        {
            LocalActorView speaker = view.Actors.Single(value => value.ActorId == conversation.SpeakerActorId);
            focus = (focus + FarmCanvasCoordinates.Position(speaker.Feet)) / 2;
        }
        _camera.Position = focus + new Vector2(0, -26);
        // One coherent snapshot per frame: the HUD explains and the keys submit the same offers.
        _presented = _session.CreateOffers(view, _targets.Selected);
        _canvas.PresentTarget(_targets.Selected);
        _hud.Present(view, _session.WoodQuantity(view), _session.BerryQuantity(view), _action is not null,
            _input.Paused || !_input.Focused, _presented, _targets);
        _inventory.Present(view, _session.World.ReadRunState());
        if (_wheel.Model is { } wheel)
        {
            wheel.Refresh(target => _session.CreateOffers(view, LocatePinnedTarget(view, target)));
            _wheel.Present(view, _session.World.ReadRunState(), _input.Permits(Situation(), FarmSemanticAction.ConfirmWheel));
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_session is null || _failed || _closing is not null)
        {
            return;
        }
        try
        {
            FarmMovementIntent movement = _input.Movement(Situation(), ReadWalkingInput());
            _session.World.SetMovement(movement.DeltaX, movement.DeltaY);
        }
        catch (InvalidOperationException error)
        {
            Fail("The world has stopped. " + error.GetBaseException().Message);
        }
    }

    public override void _Input(InputEvent input)
    {
        // Release bookkeeping must run before a focused Control can consume the release.
        // New gameplay presses still respect the normal GUI-first unhandled-input path.
        if (input.IsPressed() || input.IsEcho()) return;
        FarmInputDecision decision = _input.Route(Situation(), Translate(input));
        if (decision.Consumed) GetViewport().SetInputAsHandled();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        FarmInputDecision decision = _input.Route(Situation(), Translate(input));
        if (decision.Action != FarmSemanticAction.None) Perform(decision.Action);
        if (decision.Consumed) GetViewport().SetInputAsHandled();
    }

    /// <summary>World facts the router consults; the projection read is a lock-free snapshot.</summary>
    private FarmInputSituation Situation() => new(
        SessionOpen: _session is not null,
        ReadingHold: _session?.World.ReadView().HasReadingHold ?? false,
        ActionPending: _action is not null);

    private static FarmDirectionalInput ReadWalkingInput()
    {
        Vector2 direction = Input.GetVector(FarmInputActions.WalkLeft, FarmInputActions.WalkRight,
            FarmInputActions.WalkUp, FarmInputActions.WalkDown);
        // Keep GetVector's circular deadzone. Individual strengths separately expose opposing
        // buttons, which can sum to zero without the player actually releasing either one.
        float Strength(string action) => direction == Vector2.Zero
            ? Input.GetActionStrength(action) : Input.GetActionRawStrength(action);
        return new FarmDirectionalInput(Strength(FarmInputActions.WalkLeft), Strength(FarmInputActions.WalkRight),
            Strength(FarmInputActions.WalkUp), Strength(FarmInputActions.WalkDown))
        {
            ResolvedIntent = FarmMovementIntent.FromAxes(direction.X, direction.Y)
        };
    }

    private static FarmInputSignal Translate(InputEvent input)
    {
        List<string> matched = [];
        foreach (string action in FarmInputActions.Routed)
        {
            if (!input.IsAction(action)) continue;
            matched.Add(action);
        }
        return new FarmInputSignal(matched, input.IsPressed(), input.IsEcho());
    }

    /// <summary>Single entry for HUD, device and contextual requests; the router owns the context check.</summary>
    private void Perform(FarmSemanticAction action)
    {
        if (!_input.Permits(Situation(), action)) return;
        _input.CaptureBoundary(Situation(), ReadWalkingInput());
        if (action is FarmSemanticAction.OpenWheel or FarmSemanticAction.CloseWheel)
        {
            ChangeWheel(action == FarmSemanticAction.OpenWheel);
            return;
        }
        if (action == FarmSemanticAction.ConfirmWheel)
        {
            FarmActionOffer? chosen = _wheel.Model?.Selected?.Offer;
            if (chosen?.CanExecute != true) return;
            ChangeWheel(false);
            Submit(chosen);
            return;
        }
        if (action is FarmSemanticAction.OpenInventory or FarmSemanticAction.CloseInventory)
        {
            ChangeInventory(action == FarmSemanticAction.OpenInventory);
            return;
        }
        if (action == FarmSemanticAction.TogglePause)
        {
            TogglePause();
            return;
        }
        if (action is FarmSemanticAction.NextTarget or FarmSemanticAction.ClearTarget)
        {
            if (_targets is not null && _session is not null)
            {
                if (action == FarmSemanticAction.NextTarget)
                {
                    _targets.Cycle();
                }
                else
                {
                    _targets.Clear();
                }
                // Refresh the displayed choice and its offers together, including inputs in one frame.
                Present(_session.World.ReadView());
            }
            return;
        }
        if (!_presented.TryGetValue(action, out FarmActionOffer? offer))
        {
            _hud.Message("That action is not available on this farm.");
            return;
        }
        Submit(offer);
    }

    /// <summary>Submits the presented offer as captured; nothing is reselected from a fresher view.</summary>
    private void Submit(FarmActionOffer offer)
    {
        // Context legality was decided by the router; this guard only prevents a second in-flight action.
        if (_session is null || _action is not null) return;
        try
        {
            _actionDescription = offer.CompletionText;
            _action = _session.SubmitOffer(offer);
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
        if (!_input.TogglePause()) return;
        if (ChangeReadiness(_input.WorldShouldRun))
            _hud.Message(_input.Paused ? "Paused. Press Esc to continue." : "Explore the farm and speak with Wren.");
    }

    private void ChangeInventory(bool open)
    {
        if (_session is null || _inventoryModel is null) return;
        _input.SetInventoryOpen(open);
        if (open)
        {
            _inventory.Open(_inventoryModel);
            _hud.SetCovered(true);
            try { _session.World.SetMovement(0, 0); }
            catch (InvalidOperationException error)
            {
                Fail("The world has stopped. " + error.GetBaseException().Message);
                return;
            }
            Present(_session.World.ReadView());
            _inventory.FocusSelection();
        }
        else
        {
            _hud.SetCovered(false);
            _inventory.Close();
        }
    }

    private void OnFocusEntered()
    {
        _input.CaptureBoundary(Situation(), ReadWalkingInput());
        _input.ReconcileHeldActions(FarmInputActions.Routed.Where(action => Input.IsActionPressed(action)));
        if (_input.SetFocused(true)) ChangeReadiness(_input.WorldShouldRun);
    }

    private void OnFocusExited()
    {
        if (_input.SetFocused(false))
        {
            _input.CaptureBoundary(Situation(), ReadWalkingInput());
            if (_input.WheelOpen) ChangeWheel(false);
            ChangeReadiness(_input.WorldShouldRun);
        }
    }

    private void ChangeWheel(bool open)
    {
        if (_session is null) return;
        _input.SetWheelOpen(open);
        if (open)
        {
            _wheel.Open(_presented);
            _hud.SetCovered(true);
            try { _session.World.SetMovement(0, 0); }
            catch (InvalidOperationException error) { Fail(error.GetBaseException().Message); return; }
            Present(_session.World.ReadView());
        }
        else
        {
            _hud.SetCovered(false);
            _wheel.Close();
        }
    }

    private void OnControllerConnectionChanged(long device, bool connected)
    {
        if (!connected && _input.WheelOpen)
        {
            _input.CaptureBoundary(Situation(), ReadWalkingInput());
            ChangeWheel(false);
        }
    }

    private static FarmTarget? LocatePinnedTarget(LocalWorldView view, FarmTarget? target)
    {
        if (target is null) return null;
        LocalActorView? person = view.Actors.FirstOrDefault(actor => actor.ActorId == target.Id);
        if (person is not null) return target with { Kind = FarmTargetKind.Person, Feet = person.Feet };
        LocalGatherNodeView? plant = view.GatheringNodes.FirstOrDefault(node => node.NodeId == target.Id);
        return target with { Kind = plant is null ? target.Kind : FarmTargetKind.Harvestable, Feet = plant?.Feet };
    }

    private bool ChangeReadiness(bool ready)
    {
        try { _session?.World.SetReady(ready); return true; }
        catch (InvalidOperationException error) { Fail("The world has stopped. " + error.GetBaseException().Message); return false; }
    }

    private void Fail(string message)
    {
        _failed = true;
        _input.MarkUnavailable();
        if (_input.InventoryOpen) ChangeInventory(false);
        if (_input.WheelOpen) ChangeWheel(false);
        try { _session?.World.SetReady(false); }
        catch (InvalidOperationException) { /* A failed host already stopped accepting input. */ }
        _hud.Message(message);
        GD.PushError(message);
    }

    private void BeginClose()
    {
        if (_closing is not null) return;
        _input.MarkUnavailable();
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
        Input.Singleton.JoyConnectionChanged -= OnControllerConnectionChanged;
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
