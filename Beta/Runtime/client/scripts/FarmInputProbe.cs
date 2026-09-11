using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Godot;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Qualifies native event routing against the existing authoritative public view.</summary>
internal partial class FarmInputProbe : Node
{
    private readonly FarmGame _game;
    private readonly string _reportPath;
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly List<string> _checks = [];
    private object? _talkOfferAtPress;

    internal FarmInputProbe(FarmGame game, string reportPath)
    {
        _game = game;
        _reportPath = reportPath;
    }

    public override async void _Ready()
    {
        try
        {
            await Until(() => _game.CurrentView is not null, "The farm did not become ready.");
            await CheckPausedDirections();
            await CheckFocusReturn();
            await CheckStickDeadzone();
            await ApproachWren();
            await CheckDialogueCancellation();
            WriteReport("passed", null);
            _game.RequestClose();
        }
        catch (Exception error)
        {
            WriteReport("failed", error.Message);
            GD.PushError("Native input qualification failed: " + error);
            GetTree().Quit(1);
        }
    }

    private async Task CheckPausedDirections()
    {
        Require(_game.IsLocallyPaused, "The input probe must start paused.");
        KeyEvent(Key.A, true);
        KeyEvent(Key.D, true);
        KeyTap(Key.Escape);
        await Until(() => !_game.IsLocallyPaused, "Escape did not resume the world.");
        await Delay(0.1);
        KeyEvent(Key.A, false);
        await AssertStationary("Opposing held directions did not manufacture a neutral release.");
        KeyEvent(Key.D, false);
        await Delay(0.1);
        var before = Feet();
        KeyEvent(Key.D, true);
        await Until(() => Feet() != before, "Fresh keyboard movement did not reach the authority.");
        KeyEvent(Key.D, false);
        await Delay(0.1);
        _checks.Add("Fresh movement resumed after all held directions returned to neutral.");

        KeyEvent(Key.Escape, true);
        await Until(() => _game.IsLocallyPaused, "Escape did not pause ordinary gameplay.");
        KeyEvent(Key.Escape, true, echo: true);
        await Delay(0.15);
        Require(_game.IsLocallyPaused, "A repeated key press toggled pause again.");
        KeyEvent(Key.Escape, false);
        ButtonEvent(JoyButton.B, true);
        ButtonEvent(JoyButton.B, false);
        await Until(() => !_game.IsLocallyPaused, "Controller Cancel did not resume the top paused layer.");
        _checks.Add("Keyboard repeat did not duplicate pause; controller Cancel unwound pause.");
    }

    private async Task CheckFocusReturn()
    {
        GetWindow().EmitSignal(Window.SignalName.FocusExited);
        Require(!_game.IsWindowFocused, "The real focus-exit handler was not invoked.");
        KeyEvent(Key.D, true);
        // Deliver the buffered event while unfocused, without allowing a physics frame to run.
        Input.FlushBufferedEvents();
        GetWindow().EmitSignal(Window.SignalName.FocusEntered);
        Require(_game.IsWindowFocused, "The real focus-enter handler was not invoked.");
        await AssertStationary("Focus return did not replay a held movement key.");
        KeyEvent(Key.D, false);
        await Delay(0.1);
    }

    private async Task ApproachWren()
    {
        while (true)
        {
            LocalWorldView view = View();
            LocalActorView player = view.Actors.Single(actor => actor.ActorId == view.PlayerActorId);
            LocalActorView wren = view.Actors.Single(actor => actor.ActorId != view.PlayerActorId);
            long x = wren.Feet.X - player.Feet.X;
            long y = wren.Feet.Y - player.Feet.Y;
            ReleaseDirections();
            if ((Int128)x * x + (Int128)y * y <= 640 * 640)
            {
                await Delay(0.1);
                return;
            }
            string direction = Math.Abs(x) > Math.Abs(y)
                ? x < 0 ? "walk_left" : "walk_right"
                : y < 0 ? "walk_up" : "walk_down";
            Input.ActionPress(direction);
            await Frame();
        }
    }

    private async Task CheckStickDeadzone()
    {
        AxisEvent(JoyAxis.LeftX, 0.1f);
        await AssertStationary("Small stick drift stayed inside the circular deadzone.");
        AxisEvent(JoyAxis.LeftX, 0.2f);
        AxisEvent(JoyAxis.LeftY, 0.2f);
        var before = Feet();
        await Until(() => Feet() != before, "A diagonal outside the circular deadzone did not move.");
        AxisEvent(JoyAxis.LeftX, 0);
        AxisEvent(JoyAxis.LeftY, 0);
        await Delay(0.1);
        _checks.Add("A deliberate diagonal kept the existing circular-deadzone movement behavior.");
    }

    private async Task CheckDialogueCancellation()
    {
        await PressStationaryTalk();
        await Until(() => View().Dialogue is not null && !_game.HasPendingAction,
            "The ordinary Talk key did not open Wren's dialogue.");
        await Delay(0.1);
        var calendar = (View().Calendar.Days, View().Calendar.MillisecondsWithinDay);
        KeyEvent(Key.D, true);
        await AssertStationary("Addressed dialogue suppressed held movement.");
        Require((View().Calendar.Days, View().Calendar.MillisecondsWithinDay) == calendar,
            "The dialogue reading hold allowed world time to advance.");
        KeyEvent(Key.Escape, true);
        KeyEvent(Key.Escape, true, echo: true);
        await Until(() => View().Dialogue is null && !_game.HasPendingAction,
            "Escape did not dismiss the top dialogue layer.");
        Require(!_game.IsLocallyPaused, "The same Escape press also paused the world.");
        await AssertStationary("Dismissing dialogue did not release held movement into gameplay.");
        KeyEvent(Key.Escape, false);
        KeyEvent(Key.D, false);
        await Delay(0.1);

        await PressStationaryTalk();
        await Until(() => View().Dialogue is not null && !_game.HasPendingAction,
            "Dialogue did not reopen after a fresh Talk press.");
        ulong wood = View().PlayerInventory.Quantity;
        ButtonEvent(JoyButton.B, true);
        ButtonEvent(JoyButton.B, false);
        await Until(() => View().Dialogue is null && !_game.HasPendingAction,
            "Controller Cancel did not dismiss dialogue.");
        await Delay(0.1);
        Require(!_game.IsLocallyPaused && View().PlayerInventory.Quantity == wood,
            "Controller cancellation submitted a world action or paused the world.");
        _checks.Add("Escape and controller B close dialogue first without leaking the event into world actions.");
    }

    private async Task PressStationaryTalk()
    {
        (ulong Player, ulong Target)? previous = null;
        Stopwatch stable = Stopwatch.StartNew();
        await Until(() =>
        {
            LocalWorldView view = View();
            LocalActorView player = view.Actors.Single(actor => actor.ActorId == view.PlayerActorId);
            LocalActorView wren = view.Actors.Single(actor => actor.ActorId != view.PlayerActorId);
            var revisions = (player.PresenceRevision, wren.PresenceRevision);
            if (previous != revisions)
            {
                previous = revisions;
                stable.Restart();
            }
            return stable.ElapsedMilliseconds >= 150 && !_game.HasPendingAction
                && _game.CurrentOffers.TryGetValue(FarmSemanticAction.Speak, out FarmActionOffer? offer)
                && offer.CanExecute && offer.Scope.ExpectedActorRevision == player.PresenceRevision
                && offer.Intent is SpeakToActorIntent speak && speak.TargetActorId == wren.ActorId
                && speak.ExpectedTargetRevision == wren.PresenceRevision;
        }, "The released player and Wren did not settle for one deliberate Talk press.");
        FarmActionOffer ready = _game.CurrentOffers[FarmSemanticAction.Speak];
        _talkOfferAtPress = new { ready.Scope.ExpectedActorRevision,
            Target = ((SpeakToActorIntent)ready.Intent!).ExpectedTargetRevision };
        KeyTap(Key.E);
    }

    private async Task AssertStationary(string evidence)
    {
        await Delay(0.1);
        var before = Feet();
        await Delay(0.3);
        Require(Feet() == before, evidence + " Player position changed.");
        _checks.Add(evidence);
    }

    private LocalWorldView View() => _game.CurrentView
        ?? throw new InvalidOperationException("The native farm lost its public view.");

    private (long X, long Y) Feet()
    {
        LocalWorldView view = View();
        LocalActorView player = view.Actors.Single(actor => actor.ActorId == view.PlayerActorId);
        return (player.Feet.X, player.Feet.Y);
    }

    private async Task Until(Func<bool> condition, string failure)
    {
        Stopwatch wait = Stopwatch.StartNew();
        while (!condition())
        {
            Require(wait.Elapsed < TimeSpan.FromSeconds(8), failure);
            await Frame();
        }
    }

    private async Task Frame()
    {
        Require(!_game.HasFailed && _elapsed.Elapsed < TimeSpan.FromSeconds(40),
            "The native game failed or the input probe timed out.");
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Delay(double seconds)
    {
        Stopwatch wait = Stopwatch.StartNew();
        while (wait.Elapsed.TotalSeconds < seconds)
        {
            await Frame();
        }
    }

    private static void KeyTap(Key key)
    {
        KeyEvent(key, true);
        KeyEvent(key, false);
    }

    private static void KeyEvent(Key key, bool pressed, bool echo = false) =>
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = pressed, Echo = echo });

    private static void ButtonEvent(JoyButton button, bool pressed) =>
        Input.ParseInputEvent(new InputEventJoypadButton { Device = 0, ButtonIndex = button, Pressed = pressed });

    private static void AxisEvent(JoyAxis axis, float value) =>
        Input.ParseInputEvent(new InputEventJoypadMotion { Device = 0, Axis = axis, AxisValue = value });

    private static void ReleaseDirections()
    {
        foreach (string name in new[] { "walk_left", "walk_right", "walk_up", "walk_down" })
        {
            Input.ActionRelease(name);
        }
    }

    private void WriteReport(string status, string? failure)
    {
        ReleaseDirections();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_reportPath))!);
        File.WriteAllText(_reportPath, JsonSerializer.Serialize(new
        {
            Kind = "native-input-focus-v1", Status = status, Failure = failure,
            Runtime = RuntimeInformation.FrameworkDescription, DisplayDriver = DisplayServer.GetName(),
            _game.IsLocallyPaused, _game.IsWindowFocused, _game.HasPendingAction,
            FinalView = _game.CurrentView, TalkOfferAtPress = _talkOfferAtPress,
            Checks = _checks, ElapsedMilliseconds = _elapsed.ElapsedMilliseconds
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
