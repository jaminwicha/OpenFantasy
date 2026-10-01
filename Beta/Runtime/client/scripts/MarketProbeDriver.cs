using System.Diagnostics;
using Godot;

namespace OpenFantasy.Client;

/// <summary>
/// Drives one named injected device route through ordinary Godot input for the disposable market probes. Keyboard and
/// controller runs move focus with arrows or the D-pad along the panel's own focus neighbours and activate with Enter
/// or A; pointer runs hover, scroll and click. Dropdown rows use the same route inside the open popup. The driver never
/// grabs a destination's focus, selects a row, emits a signal or calls a semantic handler: the only focus it starts
/// from is the one the game itself set. Every injected event, focus move and dropdown row reached is traced.
/// </summary>
internal sealed class MarketProbeDriver(Node host, FarmGame game, string route, Func<string> stage, double limitSeconds)
{
    /// <summary>Upper bound on focus moves or popup rows for one destination; the panel chain is shorter.</summary>
    private const int NavigationLimit = 24;

    /// <summary>Vertical pointer step, in viewport pixels, while hovering down a popup to find one row.</summary>
    private const float HoverStep = 3;

    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private bool _mouseEntered;
    private Vector2 _pointer;

    internal string Route => route;
    internal List<object> Inputs { get; } = [];
    internal long ElapsedMilliseconds => _elapsed.ElapsedMilliseconds;

    /// <summary>Activates one enabled control: navigation then Enter / A, or a pointer click on it.</summary>
    internal async Task Activate(Button button)
    {
        await Until(() => button.IsVisibleInTree() && !button.Disabled, "Market control is unavailable: " + button.Name);
        await Press(button);
    }

    /// <summary>The same route activation on a visible disabled control; the caller proves nothing was sent.</summary>
    internal async Task Attempt(Button button)
    {
        await Frame();
        Require(button.IsVisibleInTree() && button.Disabled, "Expected a visible disabled control: " + button.Name);
        await Press(button);
    }

    /// <summary>Moves focus with arrows or the D-pad along the panel's own neighbours until the target holds it.</summary>
    internal async Task Reach(Control target)
    {
        Require(route != "pointer", "The pointer route reaches controls by clicking them.");
        Viewport viewport = host.GetViewport();
        for (int moves = 0; viewport.GuiGetFocusOwner() != target; moves++)
        {
            Control from = viewport.GuiGetFocusOwner() ?? throw new InvalidOperationException("No focus owner to navigate from.");
            Require(moves < NavigationLimit, $"Navigation did not reach {target.Name} from {from.Name}.");
            bool down = Distance(from, target, control => control.FocusNeighborBottom)
                <= Distance(from, target, control => control.FocusNeighborTop);
            if (route == "controller") Joy(down ? JoyButton.DpadDown : JoyButton.DpadUp);
            else Tap(down ? Key.Down : Key.Up);
            await Frame();
            Control? to = viewport.GuiGetFocusOwner();
            Inputs.Add(new { Stage = stage(), Route = route, Navigate = down ? "down" : "up", From = from.Name.ToString(),
                To = to?.Name.ToString() });
            Require(to is not null && to != from, $"Navigation from {from.Name} did not move focus.");
        }
    }

    /// <summary>Opens a dropdown and chooses one exact row through the route, tracing the row reached and chosen.</summary>
    internal async Task Choose(OptionButton control, int row)
    {
        Require(control.IsVisibleInTree() && row > 0 && row < control.ItemCount, "Dropdown choice unavailable: " + control.Name);
        PopupMenu popup = await OpenPopup(control);
        Vector2 point = await ReachRow(control, popup, row);
        if (route == "pointer") await ClickAt(point, control.Name + "-row");
        else if (route == "controller") Joy(JoyButton.A);
        else Tap(Key.Enter);
        await Until(() => !popup.Visible && control.Selected == row, "Dropdown input did not select the exact row.");
        Inputs.Add(new { Stage = stage(), Route = route, Dropdown = control.Name.ToString(), Selected = row,
            Text = control.GetItemText(row) });
        await Frame();
    }

    /// <summary>Opens a dropdown, reaches a row, then cancels through the route: Escape, controller B or a click outside.</summary>
    internal async Task CancelChoice(OptionButton control, int row, Control outside)
    {
        int before = control.Selected;
        PopupMenu popup = await OpenPopup(control);
        await ReachRow(control, popup, row);
        if (route == "pointer") await ClickAt(outside.GetGlobalRect().GetCenter(), outside.Name + "-outside");
        else if (route == "controller") Joy(JoyButton.B);
        else Tap(Key.Escape);
        await Until(() => !popup.Visible, "Cancelling did not close the dropdown.");
        await Frame();
        Require(control.Selected == before, "Cancelling the dropdown chose a row.");
        Inputs.Add(new { Stage = stage(), Route = route, Dropdown = control.Name.ToString(), Cancelled = row, Selected = control.Selected });
    }

    internal void Tap(Key key)
    {
        Inputs.Add(new { Stage = stage(), Route = route, Key = key.ToString() });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
        Input.FlushBufferedEvents();
    }

    internal void Joy(JoyButton button)
    {
        Inputs.Add(new { Stage = stage(), Route = route, Controller = button.ToString() });
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = true });
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = false });
        Input.FlushBufferedEvents();
    }

    /// <summary>A pointer click on a visible control, scrolling its panel with the wheel first when it is out of view.</summary>
    internal async Task Click(Control control, bool requireEnabled = true)
    {
        Require(control.IsVisibleInTree() && (!requireEnabled || control is not BaseButton { Disabled: true }),
            "Pointer control is unavailable: " + control.Name);
        await Reveal(control);
        Vector2 at = control.GetGlobalRect().GetCenter();
        Inputs.Add(new { Stage = stage(), Route = route, PointerControl = control.Name.ToString(), X = at.X, Y = at.Y });
        await PressAt(at);
    }

    /// <summary>Pointer motion over a control without pressing anything.</summary>
    internal async Task Hover(Control control)
    {
        await Reveal(control);
        Vector2 at = control.GetGlobalRect().GetCenter();
        Inputs.Add(new { Stage = stage(), Route = route, PointerHover = control.Name.ToString(), X = at.X, Y = at.Y });
        Move(at);
        await Frame();
    }

    /// <summary>Pointer motion to one sector direction of the wheel disc.</summary>
    internal async Task Point(Control disc, int sector)
    {
        Vector2 direction = new[] { Vector2.Up, Vector2.Right, Vector2.Down, Vector2.Left }[sector];
        Vector2 at = disc.GetGlobalRect().GetCenter() + direction * 116;
        Inputs.Add(new { Stage = stage(), Route = route, PointerSector = sector, X = at.X, Y = at.Y });
        Move(at);
        await Frame();
    }

    internal async Task Until(Func<bool> predicate, string failure, double seconds = 10)
    {
        var watch = Stopwatch.StartNew();
        while (true)
        {
            bool satisfied = predicate();
            Deadline();
            Require(watch.Elapsed.TotalSeconds < seconds, failure);
            if (satisfied) return;
            await Frame();
        }
    }

    internal async Task Delay(double seconds)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed.TotalSeconds < seconds) await Frame();
    }

    internal async Task Frame()
    {
        await host.ToSignal(host.GetTree(), SceneTree.SignalName.ProcessFrame);
        Deadline();
    }

    internal void Deadline() => Require(!game.HasFailed && _elapsed.Elapsed.TotalSeconds < limitSeconds,
        $"Market journey failed or exceeded its {limitSeconds:0}-second bound at {stage()}");

    internal static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private async Task Press(Button button)
    {
        void Trace(InputEvent input) => Inputs.Add(new { Stage = stage(), Route = route, Received = button.Name.ToString(),
            Event = input.AsText(), Pressed = input.IsPressed(), Accept = input.IsAction("ui_accept"), Focused = button.HasFocus() });
        button.GuiInput += Trace;
        try
        {
            if (route == "pointer") await Click(button, requireEnabled: false);
            else
            {
                await Reach(button);
                if (route == "controller") Joy(JoyButton.A);
                else Tap(Key.Enter);
            }
            await Frame();
            await Frame();
        }
        finally { button.GuiInput -= Trace; }
    }

    private async Task<PopupMenu> OpenPopup(OptionButton control)
    {
        PopupMenu popup = control.GetPopup();
        if (route == "pointer") await Click(control);
        else
        {
            await Reach(control);
            if (route == "controller") Joy(JoyButton.A);
            else Tap(Key.Enter);
        }
        await Until(() => popup.Visible, "Dropdown did not open for explicit input: " + control.Name);
        await Frame();
        return popup;
    }

    /// <summary>Moves the popup's highlighted row to the target: arrows or D-pad, or pointer hover down its rows.</summary>
    private async Task<Vector2> ReachRow(OptionButton control, PopupMenu popup, int row)
    {
        Vector2 point = Vector2.Zero;
        if (route == "pointer")
        {
            var area = new Rect2(popup.Position, popup.Size);
            for (float y = area.Position.Y + 1; popup.GetFocusedItem() != row; y += HoverStep)
            {
                Require(y < area.End.Y, "Pointer hover did not reach the dropdown row.");
                point = new Vector2(area.GetCenter().X, y);
                Move(point);
                await Frame();
            }
        }
        else
        {
            for (int moves = 0; popup.GetFocusedItem() != row; moves++)
            {
                Require(moves <= control.ItemCount, "Dropdown focus did not reach the row.");
                if (route == "controller") Joy(JoyButton.DpadDown);
                else Tap(Key.Down);
                await Frame();
            }
        }
        Inputs.Add(new { Stage = stage(), Route = route, Dropdown = control.Name.ToString(), RowReached = row,
            Text = control.GetItemText(row), X = point.X, Y = point.Y });
        return point;
    }

    /// <summary>Scrolls a control's panel with real wheel notches until the control is fully in view.</summary>
    private async Task Reveal(Control control)
    {
        ScrollContainer? scroll = null;
        for (Node? node = control.GetParent(); node is not null && scroll is null; node = node.GetParent()) scroll = node as ScrollContainer;
        for (int notch = 0; scroll is not null && !scroll.GetGlobalRect().Encloses(control.GetGlobalRect()); notch++)
        {
            Require(notch < 20, "Pointer scrolling did not reveal " + control.Name);
            bool down = control.GetGlobalRect().GetCenter().Y > scroll.GetGlobalRect().GetCenter().Y;
            Vector2 at = scroll.GetGlobalRect().GetCenter();
            Inputs.Add(new { Stage = stage(), Route = route, Wheel = down ? "down" : "up", Toward = control.Name.ToString() });
            EnterViewport();
            MouseButton wheel = down ? MouseButton.WheelDown : MouseButton.WheelUp;
            host.GetViewport().PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = wheel,
                Pressed = true, Factor = 1 }, true);
            host.GetViewport().PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = wheel,
                Pressed = false, Factor = 1 }, true);
            await Frame();
        }
    }

    private async Task ClickAt(Vector2 at, string label)
    {
        Inputs.Add(new { Stage = stage(), Route = route, PointerControl = label, X = at.X, Y = at.Y });
        await PressAt(at);
    }

    private async Task PressAt(Vector2 at)
    {
        Move(at);
        await Frame();
        host.GetViewport().PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left,
            Pressed = true, ButtonMask = MouseButtonMask.Left }, true);
        await Frame();
        host.GetViewport().PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left,
            Pressed = false }, true);
    }

    /// <summary>
    /// Pointer motion carries its actual relative movement and a matching velocity over one 60 Hz frame, as a moving
    /// device does; popups ignore zero-velocity motion as a window-manager artifact.
    /// </summary>
    private void Move(Vector2 at)
    {
        EnterViewport();
        Vector2 relative = at - _pointer;
        _pointer = at;
        host.GetViewport().PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at, Relative = relative,
            Velocity = relative * 60 }, true);
    }

    private void EnterViewport()
    {
        if (_mouseEntered) return;
        host.GetViewport().NotifyMouseEntered();
        _mouseEntered = true;
    }

    /// <summary>Moves along one neighbour direction of the panel's own focus chain to count the route length.</summary>
    private static int Distance(Control from, Control target, Func<Control, NodePath> neighbour)
    {
        Control current = from;
        for (int moves = 1; moves <= NavigationLimit; moves++)
        {
            NodePath path = neighbour(current);
            if (path.IsEmpty || current.GetNodeOrNull<Control>(path) is not { } next) return int.MaxValue;
            if (next == target) return moves;
            current = next;
        }
        return int.MaxValue;
    }
}
