using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Godot;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Actual native selection, input isolation and one authoritative wheel action on a disposable farm.</summary>
internal sealed partial class FarmWheelProbe(FarmGame game, string reportPath, bool reopen) : Node
{
    private readonly List<string> _checks = [];
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private FarmQuickWheel _wheel = null!;
    private bool _pointerEntered;
    private readonly List<string> _focusEvents = [];

    public override async void _Ready()
    {
        try
        {
            GetWindow().FocusEntered += () => _focusEvents.Add($"{_elapsed.ElapsedMilliseconds}: entered");
            GetWindow().FocusExited += () => _focusEvents.Add($"{_elapsed.ElapsedMilliseconds}: exited");
            await Until(() => game.CurrentView is not null && game.CurrentOffers.Count > 0 && game.IsLocallyPaused,
                "The paused farm did not become ready.");
            if (DisplayServer.GetName() != "headless" && !GetWindow().Unfocusable)
            {
                // Allow the native application/window activation to settle, then request focus once.
                // During the actual journey, focus loss cancels normally and fails the check.
                await Delay(2);
                GetWindow().GrabFocus();
                await Delay(0.15);
                await Until(() => GetWindow().HasFocus() && game.IsWindowFocused,
                    "The rendered fixture did not receive focus after opening.");
            }
            _wheel = game.GetNode<FarmQuickWheel>("FarmQuickWheel");
            if (reopen)
            {
                Require(!game.IsWheelOpen && _wheel.Model is null, "A transient wheel survived process restart.");
                _checks.Add("Process reopening restores the paused world without restoring an action selection.");
            }
            else
            {
                await CheckPausedWheel();
                await CheckRunningWheel();
                await CheckCancellation();
            }
            Report("passed", null);
            game.RequestClose();
        }
        catch (Exception error)
        {
            Input.ActionRelease(FarmInputActions.WalkDown);
            Report("failed", error.ToString());
            GD.PushError("Native wheel probe failed: " + error);
            GetTree().Quit(1);
        }
    }

    private async Task CheckPausedWheel()
    {
        var initialTarget = game.CurrentTarget;
        string before = JsonSerializer.Serialize(View());
        Tap(Key.V);
        await Until(() => game.IsWheelOpen, "V did not open the wheel.");
        Require(_wheel.Model!.Selected is null, "Opening selected an action without a choice.");
        Control disc = _wheel.GetNode<Control>("%Disc");
        Vector2 center = disc.GetGlobalRect().GetCenter();
        PointAt(center);
        Input.ParseInputEvent(new InputEventJoypadMotion { Device = 0, Axis = JoyAxis.LeftX, AxisValue = 0.15f });
        await Delay(0.1);
        Require(_wheel.Model.Selected is null, "The neutral center or stick drift selected an action.");
        Input.ParseInputEvent(new InputEventJoypadMotion { Device = 0, Axis = JoyAxis.LeftX, AxisValue = 0 });
        Tap(Key.Enter);
        await Delay(0.1);
        Require(game.IsWindowFocused, "The native window lost focus during neutral inspection.");
        Require(game.IsWheelOpen && !game.HasPendingAction, "Neutral confirmation executed an action.");
        await Capture("neutral");
        Input.ParseInputEvent(new InputEventJoypadMotion { Device = 0, Axis = JoyAxis.LeftX, AxisValue = 0.9f });
        await Until(() => _wheel.Model?.SelectedIndex == 1, "A deliberate stick tilt did not select gathering.");
        Input.ParseInputEvent(new InputEventJoypadMotion { Device = 0, Axis = JoyAxis.LeftX, AxisValue = 0 });
        Vector2 hover = center + new Vector2(0, -115);
        PointAt(hover);
        await Until(() => _wheel.Model?.SelectedIndex == 0, "Pointer hover did not inspect Talk.");
        Tap(Key.Down);
        await Until(() => _wheel.Model?.SelectedIndex == 2, "Discrete selection did not choose crafting.");
        Require(_wheel.GetNode<Button>("%ConfirmChoice").Disabled
            && _wheel.GetNode<Label>("%ChoiceDetails").Text.Contains("Unavailable:", StringComparison.Ordinal),
            "Blocked crafting was not inspectable with its real reason.");
        await Capture("blocked");
        foreach (Key key in new[] { Key.E, Key.F, Key.C, Key.G, Key.I, Key.Q }) Tap(key);
        Tap(Key.Tab);
        await Delay(0.1);
        Require(_wheel.IsAncestorOf(GetViewport().GuiGetFocusOwner()), "Tab escaped the modal wheel.");
        KeyEvent(Key.D, true);
        await Delay(0.1);
        Button(JoyButton.B);
        await Until(() => !game.IsWheelOpen, "Controller B did not cancel the wheel.");
        Require(game.IsLocallyPaused && JsonSerializer.Serialize(View()) == before,
            "Inspection or cancellation changed the paused world or executed a field shortcut.");
        Require(game.CurrentTarget == initialTarget, "Wheel shortcuts changed the field target.");
        Tap(Key.Escape);
        await Until(() => !game.IsLocallyPaused, "Escape did not resume after closing the wheel.");
        var feet = PlayerFeet();
        await Delay(0.2);
        Require(PlayerFeet() == feet, "Held menu navigation leaked into walking after cancellation.");
        KeyEvent(Key.D, false);
        _checks.Add("Pointer/stick/discrete inspection, neutral dead zones, field-shortcut isolation, Tab containment and B cancellation preserve pause and require movement release.");
    }

    private async Task CheckRunningWheel()
    {
        // Approach the real admitted oak through the existing movement input, then stop before choosing.
        await Until(() => game.IsWindowFocused, "The running farm has no focus.");
        LocalGatherNodeView oak = View().GatheringNodes.Single(node => node.Kind == OpenFantasy.Core.Gathering.GatherNodeKind.Oak);
        Stopwatch travel = Stopwatch.StartNew();
        while (!FarmClientSession.CanReachGatherNode(View(), oak))
        {
            Require(travel.Elapsed < TimeSpan.FromSeconds(5), "Could not approach the initial oak.");
            Input.ActionPress(FarmInputActions.WalkDown);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Input.ActionRelease(FarmInputActions.WalkDown);
        await Delay(0.2);
        await Until(() => game.CurrentOffers[FarmSemanticAction.Gather].CanExecute,
            "Normal effort recovery did not make the nearby oak workable.");
        Button(JoyButton.RightStick);
        await Until(() => game.IsWheelOpen, "Right-stick click did not open the wheel.");
        Button(JoyButton.DpadRight);
        await Until(() => _wheel.Model?.SelectedIndex == 1, "D-pad did not select gathering.");
        var selectedTarget = _wheel.Model!.Selected!.Offer.TargetId;
        var before = View();
        var feet = PlayerFeet();
        if (!game.BindWindowFocus)
        {
            GetWindow().EmitSignal(Window.SignalName.FocusExited);
            await Delay(0.1);
            Require(game.IsWindowFocused && game.IsWheelOpen,
                "An OS-window notification escaped the background fixture's focus boundary.");
            GetWindow().EmitSignal(Window.SignalName.FocusEntered);
            _checks.Add("Background rendering ignores native window notifications; explicit fixture focus still exercises cancellation separately.");
        }
        await Delay(0.4);
        Require(View().Calendar > before.Calendar && PlayerFeet() == feet && !game.HasPendingAction,
            "The wheel paused world time, moved the player or submitted on selection.");
        Require(_wheel.Model.Selected.Offer.TargetId == selectedTarget && _wheel.Model.Selected.Offer.CanExecute,
            "The pinned gathering target changed while the wheel was open.");
        await Capture("ready");
        uint swings = (uint)View().GatheringNodes.Single(node => node.NodeId == selectedTarget).RemainingSwings;
        ulong quantity = View().PlayerInventory.Quantity;
        Button(JoyButton.A);
        await Until(() => !game.IsWheelOpen && !game.HasPendingAction && View().PlayerInventory.Quantity == quantity + 1,
            "Explicit controller confirmation did not apply exactly one real gather.");
        Require(View().GatheringNodes.Single(node => node.NodeId == selectedTarget).RemainingSwings == swings - 1,
            "The wheel did not spend exactly one swing at the inspected target.");
        _checks.Add("World time continues without player movement; controller selection and confirmation harvest exactly one unit from the pinned plant.");
    }

    private async Task CheckCancellation()
    {
        ulong quantity = View().PlayerInventory.Quantity;
        Tap(Key.V);
        await Until(() => game.IsWheelOpen, "The wheel did not reopen.");
        Tap(Key.Right);
        Input.Singleton.EmitSignal(Input.SignalName.JoyConnectionChanged, 0L, false);
        await Until(() => !game.IsWheelOpen, "Controller disconnect did not cancel the wheel.");
        Tap(Key.V);
        await Until(() => game.IsWheelOpen, "The wheel did not open before focus loss.");
        game.InjectFixtureFocus(false);
        await Until(() => !game.IsWheelOpen && !game.IsWindowFocused, "Focus loss did not cancel the wheel.");
        await Delay(0.15);
        game.InjectFixtureFocus(true);
        await Until(() => game.IsWindowFocused, "Focus did not recover.");
        Tap(Key.Escape);
        await Until(() => game.IsLocallyPaused, "Could not pause the final world.");
        await Delay(0.2);
        Require(!game.HasPendingAction && View().PlayerInventory.Quantity == quantity,
            "Focus loss or disconnect executed an action.");
        await Capture("aftermath");
        _checks.Add("Disconnect and focus loss cancel without spending; the final paused world is ready for exact reopening.");
    }

    private LocalWorldView View() => game.CurrentView!;
    private OpenFantasy.Core.Spatial.SubunitPosition PlayerFeet() => View().Actors.Single(actor => actor.ActorId == View().PlayerActorId).Feet;
    private static void KeyEvent(Key key, bool pressed) => Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = pressed });
    private void PointAt(Vector2 position)
    {
        // Layout coordinates are viewport-local. Headless embedder dimensions need not match them.
        if (!_pointerEntered)
        {
            GetViewport().NotifyMouseEntered();
            _pointerEntered = true;
        }
        GetViewport().PushInput(new InputEventMouseMotion { Position = position, GlobalPosition = position }, true);
    }
    private static void Tap(Key key) { KeyEvent(key, true); KeyEvent(key, false); }
    private static void Button(JoyButton button)
    {
        Input.ParseInputEvent(new InputEventJoypadButton { Device = 0, ButtonIndex = button, Pressed = true });
        Input.ParseInputEvent(new InputEventJoypadButton { Device = 0, ButtonIndex = button, Pressed = false });
    }
    private async Task Until(Func<bool> condition, string error)
    {
        Stopwatch wait = Stopwatch.StartNew();
        while (!condition())
        {
            Require(!game.HasFailed && wait.Elapsed < TimeSpan.FromSeconds(5) && _elapsed.Elapsed < TimeSpan.FromSeconds(30), error);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
    private async Task Delay(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private async Task Capture(string name)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        foreach (Control control in Descendants(_wheel).OfType<Control>().Where(c => c.IsVisibleInTree()))
        {
            Rect2 bounds = control.GetGlobalRect();
            Require(GetViewport().GetVisibleRect().Encloses(bounds), "Wheel control is outside the viewport: " + control.Name);
        }
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(Path.ChangeExtension(reportPath, name + ".png")) == Error.Ok, "Could not save a native wheel frame.");
    }
    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            yield return child;
            foreach (Node descendant in Descendants(child)) yield return descendant;
        }
    }
    private void Report(string status, string? error) => File.WriteAllText(reportPath, JsonSerializer.Serialize(new
    {
        Status = status, Error = error, Runtime = RuntimeInformation.FrameworkDescription,
        DisplayDriver = DisplayServer.GetName(), Reopen = reopen, Checks = _checks,
        FocusEvents = _focusEvents,
        BackgroundRender = GetWindow().Unfocusable, NativeWindowFocused = GetWindow().HasFocus(),
        game.BindWindowFocus,
        FinalView = game.CurrentView, game.IsWheelOpen, game.IsLocallyPaused, game.IsWindowFocused,
        game.CurrentTarget, Offers = game.CurrentOffers.Values.Select(offer => new { offer.Action, offer.TargetId, offer.Label, offer.CanExecute, offer.BlockingReason }),
        FocusOwner = GetViewport().GuiGetFocusOwner()?.GetPath().ToString(),
        WheelSelection = _wheel?.Model?.SelectedIndex, WheelCanExecute = _wheel?.Model?.Selected?.Offer.CanExecute,
        ConfirmDisabled = _wheel?.GetNode<Button>("%ConfirmChoice").Disabled,
        PointerSeen = _wheel?.GetNode<FarmWheelDisc>("%Disc").LastPointerPosition?.ToString(),
        LastWheelButton = _wheel?.GetNode<FarmWheelDisc>("%Disc").LastButtonInput,
        AcceptBindings = InputMap.ActionGetEvents("ui_accept").Select(value => value.AsText()),
        PointerGlobal = GetViewport().GetMousePosition().ToString(),
        Navigation = Input.GetVector(FarmInputActions.WalkLeft, FarmInputActions.WalkRight, FarmInputActions.WalkUp, FarmInputActions.WalkDown).ToString(),
        game.HasPendingAction, ElapsedMilliseconds = _elapsed.ElapsedMilliseconds
    }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
}
