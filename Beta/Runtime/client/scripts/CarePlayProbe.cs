using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Disposable native care journey: original combat, exact inspection and actual player input.</summary>
internal sealed partial class CarePlayProbe(FarmGame game, string reportPath, string mode) : Node
{
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly List<string> _checks = [];
    private readonly List<object> _observations = [];
    private readonly List<object> _inputs = [];
    private string _stage = "opening";
    private string? _initial;
    private bool _observedNativeFocus;
    private EntityId _wolf;
    private Dictionary<EntityId, SubunitPosition> _positions = [];
    private int _waitCount;

    public override void _Process(double delta)
    {
        if (DisplayServer.GetName() != "headless" && GetWindow().HasFocus()) _observedNativeFocus = true;
    }

    public override async void _Ready()
    {
        try
        {
            Require(mode is "wheel" or "keyboard" or "controller" or "pointer" or "reopen", "Unknown care probe mode.");
            await Until(() => game.CurrentView?.EncounterLifecycle is not null && game.CurrentOffers.Count > 0,
                "Care world did not open.", 30);
            game.InjectFixtureFocus(true);
            Require(game.IsLocallyPaused, "The disposable care world must open paused.");
            _initial = PublicHash();
            if (mode == "reopen")
            {
                using var prior = JsonDocument.Parse(File.ReadAllText(Output("fresh", "json")));
                Require(prior.RootElement.GetProperty("Status").GetString() == "passed"
                    && prior.RootElement.GetProperty("FinalHash").GetString() == _initial,
                    "Reopening did not retain the exact post-care public state.");
                await Delay(0.5);
                Require(PublicHash() == _initial && game.CurrentTarget is null && !game.IsWheelOpen,
                    "Paused recovery changed state or replayed an input draft.");
                _checks.Add("Separate process restores exact post-care state while paused, without replaying input.");
            }
            else
            {
                await ReachDefeat();
                await InspectCare();
                await AcceptCare();
            }
            await PauseAndSettle();
            Observe("final");
            await Capture(mode == "reopen" ? "reopen" : "treated");
            Deadline();
            Report("passed", null);
            game.RequestClose();
        }
        catch (Exception error)
        {
            Input.ActionRelease(FarmInputActions.WalkRight);
            Report("failed", error.ToString());
            GD.PushError("Native care probe failed: " + error);
            GetTree().Quit(1);
        }
    }

    private async Task ReachDefeat()
    {
        _stage = "entry";
        Require(View.Progression!.Hp == View.Progression.MaxHp && View.Care is null
            && View.EncounterLifecycle!.EncounterCount == 0, "Fresh care must start from living, unearned content.");
        _wolf = View.EncounterLifecycle!.Entries.Single(entry => entry.DisplayName.StartsWith("East", StringComparison.Ordinal)).TargetActorId;
        Observe("fresh");
        Tap(Key.Escape);
        for (int cycle = 0; game.CurrentTarget?.Id != _wolf; cycle++)
        {
            Require(cycle < 12, "Target cycling failed to reach the east wolf.");
            Tap(Key.Q);
            await Frame();
        }
        await Until(() => Offer(FarmSemanticAction.EnterEncounter)?.CanExecute == true, "No real entry offer.");
        Tap(Key.E);
        await Until(() => View.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Active && CanWait(),
            "The encounter did not reach a human choice.");
        _positions = View.Actors.ToDictionary(actor => actor.ActorId, actor => actor.Feet);
        int originalWolfHp = Wolf.CurrentHp;
        while (View.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome)
        {
            await Until(() => View.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Outcome || CanWait(),
                "Neither a lawful Wait nor genuine defeat arrived.", 15);
            if (View.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Outcome) break;
            Require(++_waitCount <= 12, "Original-content defeat exceeded twelve deliberate Wait choices.");
            _stage = "wait-" + _waitCount;
            ulong timing = View.Encounter!.TimingRevision;
            Tap(Key.G);
            await Until(() => !game.HasPendingAction && View.Encounter!.TimingRevision != timing
                && (View.EncounterActions!.RemainingDelay > 0 || View.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Outcome),
                "Wait did not incur actual debt or reach defeat.");
        }
        Require(View.Progression!.Hp == 0 && Wolf.CurrentHp == originalWolfHp && PositionsUnchanged(),
            "Only original wolf attacks may incapacitate the stationary player.");
        await Until(() => Offer(FarmSemanticAction.AcknowledgeCare)?.CanExecute == true, "Actual nearby care was not offered.");
        Require(!game.CurrentOffers.ContainsKey(FarmSemanticAction.ReleaseEncounter)
            && !game.CurrentOffers.ContainsKey(FarmSemanticAction.Strike)
            && !game.CurrentOffers.ContainsKey(FarmSemanticAction.Wait), "Defeat exposed a substitute physical action.");
        _checks.Add("Original combat and deliberate costed Wait cause actual incapacity, with all bodies retained.");
        Observe("defeat");
        await Capture("defeat");
    }

    private async Task InspectCare()
    {
        _stage = "inspection";
        string held = PublicHash();
        var offer = Offer(FarmSemanticAction.AcknowledgeCare)!;
        Require(offer.Intent is AcknowledgeCareIntent && offer.TargetName == "Wren"
            && !string.IsNullOrWhiteSpace(offer.DetailText), "Care must identify the helper and exact acknowledgment.");
        Input.ActionPress(FarmInputActions.WalkRight);
        Tap(Key.F);
        Tap(Key.C);
        Tap(Key.G);
        await Delay(0.25);
        Input.ActionRelease(FarmInputActions.WalkRight);
        await OpenCareWheel();
        Require(PublicHash() == held && !game.HasPendingAction && PositionsUnchanged(),
            "Inspection or blocked bodily actions changed the held world.");
        await Capture("care-inspection");
        Joy(JoyButton.B);
        await Until(() => !game.IsWheelOpen, "Care wheel cancel did not close.");
        Require(PublicHash() == held, "Cancelling care inspection committed an acknowledgment.");
        await OpenCareWheel();
        game.InjectFixtureFocus(false);
        await Until(() => !game.IsWheelOpen && !game.IsWindowFocused, "Focus loss retained a care draft.");
        await Delay(0.3);
        Require(PublicHash() == held && !game.HasPendingAction, "Focus loss selected care or advanced time.");
        game.InjectFixtureFocus(true);
        await Until(() => game.IsWindowFocused, "Fixture focus did not return.");
        _checks.Add("Care inspection, cancellation, blocked work/movement and focus loss preserve the exact held world.");
    }

    private async Task AcceptCare()
    {
        _stage = "acknowledge-" + mode;
        var before = View;
        var exact = (AcknowledgeCareIntent)Offer(FarmSemanticAction.AcknowledgeCare)!.Intent!;
        Observe("before-acknowledgment");
        if (mode == "wheel")
        {
            await OpenCareWheel();
            Joy(JoyButton.A);
        }
        else if (mode == "pointer")
        {
            await Click(await ReadyCareButton());
        }
        else if (mode == "controller")
        {
            Button button = await ReadyCareButton();
            button.GrabFocus();
            Require(GetViewport().GuiGetFocusOwner() == button, "Controller care must focus its actual enabled button.");
            Joy(JoyButton.A);
        }
        else
        {
            GetViewport().GuiGetFocusOwner()?.ReleaseFocus();
            Tap(Key.E);
        }
        await Until(() => !game.HasPendingAction && View.Progression!.Hp > 0 && View.Care is null,
            "One native acknowledgment did not lead to separate actual helper treatment.");
        Require(View.Encounter!.EncounterId == exact.EncounterId
            && View.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Active && PositionsUnchanged(),
            "Care changed episode identity or teleported a participant.");
        Observe("restored");
        // The exact zero-duration care transactions and helper debt are checked from their saved
        // boundaries; a later rendered sample may already include an ordinary tactical tick.
        Require(View.Calendar.CompareTo(before.Calendar) >= 0, "Care moved world time backwards.");
        await Until(() => Offer(FarmSemanticAction.Strike)?.CanExecute == true && !game.HasPendingAction,
            "Restoration did not return the player to a lawful action.", 15);
        int hp = Wolf.CurrentHp;
        GetViewport().GuiGetFocusOwner()?.ReleaseFocus();
        Tap(Key.E);
        await Until(() => !game.HasPendingAction && Wolf.CurrentHp < hp,
            "The restored player could not land a real subsequent attack.", 15);
        _checks.Add("One explicit native care choice is followed by helper treatment and a real resumed player attack.");
    }

    private async Task OpenCareWheel()
    {
        Tap(Key.V);
        await Until(() => game.IsWheelOpen, "Care wheel did not open.");
        var wheel = game.GetNode<FarmQuickWheel>("FarmQuickWheel");
        int sector = Enumerable.Range(0, 4).Single(index => wheel.Model!.Slots[index]?.Action == FarmSemanticAction.AcknowledgeCare);
        Joy(sector switch { 0 => JoyButton.DpadUp, 1 => JoyButton.DpadRight, 2 => JoyButton.DpadDown, _ => JoyButton.DpadLeft });
        await Until(() => wheel.Model?.Selected?.Action == FarmSemanticAction.AcknowledgeCare,
            "Wheel did not select exact care.");
        Require(wheel.Model!.Selected!.Offer.Intent == Offer(FarmSemanticAction.AcknowledgeCare)!.Intent,
            "Wheel substituted another acknowledgment.");
    }

    private bool CanWait() => !game.HasPendingAction && View.EncounterActions is { CanWait: true } actions
        && Offer(FarmSemanticAction.Wait) is { CanExecute: true, Intent: QueueEncounterWaitIntent intent }
        && intent.ExpectedTimingRevision == actions.TimingRevision;
    private async Task PauseAndSettle()
    {
        if (!game.IsLocallyPaused) Tap(Key.Escape);
        await Until(() => game.IsLocallyPaused && !game.HasPendingAction, "Pause did not settle.");
        await Delay(0.25);
        string held = PublicHash();
        await Delay(0.25);
        Require(PublicHash() == held, "Paused care journey continued changing.");
    }
    private LocalWorldView View => game.CurrentView ?? throw new InvalidOperationException("No public view.");
    private LocalEncounterActorView Wolf => View.EncounterActors.Single(actor => actor.ActorId == _wolf);
    private FarmActionOffer? Offer(FarmSemanticAction action) => game.CurrentOffers.GetValueOrDefault(action);
    private bool PositionsUnchanged() => View.Actors.Count == _positions.Count
        && View.Actors.All(actor => _positions.TryGetValue(actor.ActorId, out var feet) && actor.Feet == feet);
    private string PublicHash() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(View))));
    private void Observe(string label) => _observations.Add(new { Label = label, View, game.IsLocallyPaused,
        game.IsWindowFocused, game.HasPendingAction, game.IsWheelOpen, Target = game.CurrentTarget,
        Offers = game.CurrentOffers.Values.Select(offer => new { offer.Action, offer.Label, offer.TargetId,
            offer.TargetName, offer.CostText, offer.DetailText, offer.BlockingReason }).ToArray() });
    private void Tap(Key key)
    {
        _inputs.Add(new { Stage = _stage, Key = key.ToString() });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
        Input.FlushBufferedEvents();
    }
    private void Joy(JoyButton button)
    {
        _inputs.Add(new { Stage = _stage, Controller = button.ToString() });
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = true });
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = false });
        Input.FlushBufferedEvents();
    }
    private async Task Click(Button button)
    {
        Require(button.IsVisibleInTree() && !button.Disabled, "Care pointer control is unavailable.");
        GetViewport().NotifyMouseEntered();
        Vector2 at = button.GetGlobalRect().GetCenter();
        _inputs.Add(new { Stage = _stage, PointerControl = button.Name.ToString(), X = at.X, Y = at.Y });
        GetViewport().PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at }, true);
        await Frame();
        GetViewport().PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at,
            ButtonIndex = MouseButton.Left, Pressed = true }, true);
        await Frame();
        GetViewport().PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at,
            ButtonIndex = MouseButton.Left, Pressed = false }, true);
    }
    private async Task<Button> ReadyCareButton()
    {
        Button button = game.GetNode<Button>("FarmHud/Root/Footer/Column/Actions/AcknowledgeCare");
        // Focus readiness changes synchronously; the HUD applies it on its next presentation frame.
        await Until(() => button.IsVisibleInTree() && !button.Disabled && game.IsWindowFocused
            && !game.IsLocallyPaused && !game.HasPendingAction
            && Offer(FarmSemanticAction.AcknowledgeCare)?.CanExecute == true,
            "Care control did not become available after focus returned.");
        return button;
    }
    private async Task Until(Func<bool> condition, string failure, double seconds = 10)
    {
        var watch = Stopwatch.StartNew();
        while (true)
        {
            bool satisfied = condition();
            Deadline();
            Require(watch.Elapsed.TotalSeconds < seconds, failure);
            if (satisfied) return;
            await Frame();
        }
    }
    private async Task Delay(double seconds)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed.TotalSeconds < seconds) await Frame();
    }
    private async Task Frame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Deadline();
    }
    private void Deadline() => Require(!game.HasFailed && _elapsed.Elapsed.TotalSeconds < 150,
        "Care journey failed or exceeded its 150-second bound at " + _stage);
    private async Task Capture(string stage)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(Output(stage, "png")) == Error.Ok, "Care image could not be saved.");
    }
    private string Output(string stage, string extension) => Path.ChangeExtension(reportPath, null) + "." + stage + "." + extension;
    private void Report(string status, string? error) => File.WriteAllText(Output(mode == "reopen" ? "reopen" : "fresh",
        status == "passed" ? "json" : "failed.json"), JsonSerializer.Serialize(new
        {
            Kind = "native-care-v1", Mode = mode, Status = status, Error = error, Stage = _stage,
            InitialHash = _initial, FinalHash = game.CurrentView is null ? null : PublicHash(),
            Checks = _checks, Inputs = _inputs, Observations = _observations, WaitChoices = _waitCount,
            Display = DisplayServer.GetName(), FocusRequests = 0, Unfocusable = GetWindow().Unfocusable,
            NativeFocused = GetWindow().HasFocus(), ObservedNativeFocus = _observedNativeFocus,
            StartupNoFocus = ProjectSettings.GetSetting("display/window/size/no_focus").AsBool(),
            game.IsLocallyPaused, game.IsWindowFocused, DurationMilliseconds = _elapsed.ElapsedMilliseconds,
            Limit = "Injected native controls and controlled background focus. Physical devices, OS focus, art and full-phase acceptance remain open."
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
