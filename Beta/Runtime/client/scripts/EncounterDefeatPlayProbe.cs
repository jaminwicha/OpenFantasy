using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Disposable original-content defeat journey driven only through actual native input.</summary>
internal sealed partial class EncounterDefeatPlayProbe(FarmGame game, string reportPath, string mode) : Node
{
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly List<string> _checks = [];
    private readonly List<object> _observations = [];
    private readonly List<object> _inputs = [];
    private readonly List<object> _waitChoices = [];
    private string _stage = "opening";
    private string? _initial;
    private bool _observedNativeFocus;
    private EntityId _wolf;
    private Dictionary<EntityId, SubunitPosition> _positions = [];
    private string? _retained;
    private int _wolfHp;

    public override void _Process(double delta)
    {
        if (DisplayServer.GetName() != "headless" && GetWindow().HasFocus()) _observedNativeFocus = true;
    }

    public override async void _Ready()
    {
        try
        {
            Require(mode is "fresh" or "reopen", "Unknown defeat probe mode.");
            await Until(() => game.CurrentView?.EncounterLifecycle is not null && game.CurrentOffers.Count > 0,
                "Encounter world did not open.", 30);
            game.InjectFixtureFocus(true);
            Require(game.IsLocallyPaused, "The disposable save must open paused.");
            _initial = PublicHash();
            if (mode == "reopen")
            {
                using var prior = JsonDocument.Parse(File.ReadAllText(Output("fresh", "json")));
                Require(prior.RootElement.GetProperty("Status").GetString() == "passed"
                    && prior.RootElement.GetProperty("FinalHash").GetString() == _initial,
                    "Second process did not recover the exact public defeat state before readiness.");
                _checks.Add("Separate process recovers exact paused outcome, wounds, debt, time, residents and inventory.");
            }
            await Delay(0.4);
            Require(PublicHash() == _initial && game.CurrentTarget is null && !game.IsWheelOpen,
                "Paused opening changed authority or restored an input draft.");
            _wolf = View.EncounterLifecycle!.Entries.Single(entry => entry.DisplayName.StartsWith("East", StringComparison.Ordinal)).TargetActorId;
            Observe("opening");
            if (mode == "fresh") await WaitUntilDefeated();
            else
            {
                RememberRetainedState();
                Tap(Key.Escape);
            }
            await CheckOutcomeInputs();
            await PauseAndSettle();
            Observe("final");
            await Capture(mode);
            Deadline();
            Report("passed", null);
            game.RequestClose();
        }
        catch (Exception error)
        {
            Input.ActionRelease(FarmInputActions.WalkRight);
            Report("failed", error.ToString());
            GD.PushError("Native encounter defeat probe failed: " + error);
            GetTree().Quit(1);
        }
    }

    private async Task WaitUntilDefeated()
    {
        _stage = "entry";
        Require(View.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Exploration
            && View.EncounterLifecycle.EncounterCount == 0 && View.Progression!.Hp == View.Progression.MaxHp,
            "Fresh defeat qualification requires unchanged original living content.");
        Tap(Key.Escape);
        await Select(_wolf);
        await Until(() => Offer(FarmSemanticAction.EnterEncounter)?.CanExecute == true, "East confrontation was not offered.");
        Tap(Key.E);
        // This admitted source starts with one due automatic opponent. Its lawful queue changes
        // the public timing revision without resolving an action or earning world time. Begin
        // the exact-state hold assertion after that declared boundary, not a sampled earlier frame.
        await Until(() => View.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Active
            && View.Encounter!.TimingRevision == 1 && CanChooseWait(),
            "Entry did not stage into a synchronized required choice.");
        RememberRetainedState();
        _wolfHp = Wolf.CurrentHp;
        string waiting = PublicHash();
        await Delay(0.35);
        Require(PublicHash() == waiting, "Required human choice advanced time without input.");
        Observe("active");
        await Capture("waiting");

        string[] controls = ["wheel", "keyboard-g", "controller-x", "pointer-button"];
        int choice = 0;
        while (View.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome)
        {
            await Until(() => View.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Outcome || CanChooseWait(),
                "Neither a fresh legal Wait nor the actual outcome arrived.", 15);
            if (View.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Outcome) break;
            Require(choice < 12, "The original-content Wait journey exceeded twelve deliberate choices.");
            string control = controls[choice % controls.Length];
            _stage = "wait-" + (choice + 1) + "-" + control;
            var before = View;
            ulong timing = before.Encounter!.TimingRevision;
            ulong cost = before.EncounterActions!.WaitDelay;
            Require(cost > 0 && before.EncounterActions.RemainingDelay == 0, "Wait did not expose an actual positive readiness cost.");
            Observe("before-" + _stage);
            if (control == "wheel") await ConfirmWheelWait();
            else if (control == "keyboard-g") Tap(Key.G);
            else if (control == "controller-x") Joy(JoyButton.X);
            else await Click(game.GetNode<Button>("FarmHud/Root/Footer/Column/Actions/Wait"));
            // No retry: a single input must either incur observable debt or reach the genuine
            // terminal outcome. Exact queue/resolution/cancellation is checked from recorded rows.
            await Until(() => !game.HasPendingAction && View.Encounter!.TimingRevision != timing
                && (View.EncounterActions!.RemainingDelay > 0 || View.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Outcome),
                "One deliberate Wait did not resolve into debt or the actual outcome.");
            var after = View;
            Require(after.EncounterActions!.RemainingDelay <= cost && PositionsUnchanged()
                && RetainedHash() == _retained && Wolf.CurrentHp == _wolfHp,
                "Wait changed bodies, custody, opponents or charged more than its displayed delay.");
            _waitChoices.Add(new { Number = ++choice, Control = control, BeforeTimingRevision = timing,
                AfterTimingRevision = after.Encounter!.TimingRevision, DisplayedDelay = cost,
                ObservedRemainingDelay = after.EncounterActions.RemainingDelay,
                BeforeHp = before.Progression!.Hp, AfterHp = after.Progression!.Hp,
                BeforeCalendar = before.Calendar, AfterCalendar = after.Calendar,
                ReachedOutcome = after.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Outcome });
            if (choice == 1) await Capture("costed-wait");
        }
        Require(choice > 0 && View.Progression!.Hp == 0 && Wolf.CurrentHp == _wolfHp,
            "The original wolf must cause actual player incapacity while Wait causes no attack damage.");
        _checks.Add("Deliberate native Wait inputs incur readiness; advancing tactical time allows actual source-backed wolf attacks to incapacitate the player.");
        Observe("outcome");
        await Capture("outcome");
    }

    private async Task CheckOutcomeInputs()
    {
        _stage = "defeated-input";
        await Until(() => !game.HasPendingAction && Offer(FarmSemanticAction.ReleaseEncounter) is { CanExecute: false },
            "Defeat must visibly block release rather than invent recovery.");
        Require(View.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Outcome && View.Progression!.Hp == 0
            && View.EncounterLifecycle.ReleaseActionHash is null && View.Actors.Count == 4
            && View.Actors.Single(actor => actor.ActorId == View.PlayerActorId).IsIncapacitated
            && !game.IsLocallyPaused && PositionsUnchanged(), "Actual defeat or all retained bodies were not observed.");
        var release = game.GetNode<Button>("FarmHud/Root/Footer/Column/Actions/ReleaseEncounter");
        Require(release.IsVisibleInTree() && release.Disabled
            && !string.IsNullOrWhiteSpace(Offer(FarmSemanticAction.ReleaseEncounter)!.BlockingReason),
            "Unavailable recovery must have a disabled control and explicit reason.");
        string held = PublicHash();
        Input.ActionPress(FarmInputActions.WalkRight);
        Tap(Key.E); // Unavailable release.
        Tap(Key.G); // No new Wait after outcome.
        Joy(JoyButton.X);
        Tap(Key.F); // No gathering.
        Tap(Key.C); // No crafting.
        await Delay(0.6);
        Input.ActionRelease(FarmInputActions.WalkRight);
        await Delay(0.25);
        Require(PublicHash() == held && !game.HasPendingAction && RetainedHash() == _retained && PositionsUnchanged(),
            "Outcome input advanced time, restored HP, spent inventory, moved or removed a retained actor.");
        _checks.Add("Unpaused player defeat rejects movement, ordinary work, Wait and release with exact public state retained; no recovery mechanism is claimed.");
        Tap(Key.I);
        await Until(() => game.IsInventoryOpen, "Defeated player's pack did not remain inspectable.");
        await Delay(0.2);
        Tap(Key.Escape);
        await Until(() => !game.IsInventoryOpen, "Defeated player's pack did not close.");
        Require(!game.IsLocallyPaused && PublicHash() == held, "Defeated pack inspection changed state or pause ownership.");
        Tap(Key.V);
        await Until(() => game.IsWheelOpen, "Defeated player's action wheel did not open.");
        var wheel = game.GetNode<FarmQuickWheel>("FarmQuickWheel");
        int sector = Enumerable.Range(0, 4).Single(index => wheel.Model!.Slots[index]?.Action == FarmSemanticAction.ReleaseEncounter);
        Joy(sector switch { 0 => JoyButton.DpadUp, 1 => JoyButton.DpadRight, 2 => JoyButton.DpadDown, _ => JoyButton.DpadLeft });
        await Until(() => wheel.Model?.Selected?.Action == FarmSemanticAction.ReleaseEncounter, "Blocked release was not inspectable.");
        Require(wheel.Model!.Selected!.Offer is { CanExecute: false, Intent: null }, "Disabled release inspection acquired an executable intent.");
        if (mode == "fresh") await Capture("defeat-inspection");
        Joy(JoyButton.B);
        await Until(() => !game.IsWheelOpen, "Controller cancel did not close defeat inspection.");
        Require(!game.IsLocallyPaused && !game.HasPendingAction && PublicHash() == held,
            "Defeated menu inspection or cancellation committed an action or reset the world.");
        _checks.Add("Defeated inventory and blocked-release wheel remain inspectable; close/cancel leaves exact authoritative state unchanged.");
    }

    private bool CanChooseWait()
    {
        var view = View;
        return !game.HasPendingAction && view.EncounterActions is { CanWait: true } actions
            && Offer(FarmSemanticAction.Wait) is { CanExecute: true, Intent: QueueEncounterWaitIntent intent }
            && intent.EncounterId == actions.EncounterId && intent.ExpectedTimingRevision == actions.TimingRevision;
    }

    private async Task ConfirmWheelWait()
    {
        Tap(Key.V);
        await Until(() => game.IsWheelOpen, "Wait wheel did not open.");
        var wheel = game.GetNode<FarmQuickWheel>("FarmQuickWheel");
        int sector = Enumerable.Range(0, 4).Single(index => wheel.Model!.Slots[index]?.Action == FarmSemanticAction.Wait);
        Joy(sector switch { 0 => JoyButton.DpadUp, 1 => JoyButton.DpadRight, 2 => JoyButton.DpadDown, _ => JoyButton.DpadLeft });
        await Until(() => wheel.Model?.Selected?.Action == FarmSemanticAction.Wait, "Wheel did not select Wait.");
        Joy(JoyButton.A);
        await Until(() => !game.IsWheelOpen, "Wait confirmation did not close the wheel.");
    }

    private async Task Select(EntityId id)
    {
        for (int count = 0; game.CurrentTarget?.Id != id; count++)
        {
            Require(count < 12, "Target cycling did not reach the east wolf.");
            Tap(Key.Q);
            await Frame();
        }
        await Until(() => game.CurrentOffers.Values.Any(offer => offer.TargetId == id), "Presented target did not settle.");
    }

    private void RememberRetainedState()
    {
        _positions = View.Actors.ToDictionary(actor => actor.ActorId, actor => actor.Feet);
        _retained = RetainedHash();
    }

    private async Task PauseAndSettle()
    {
        if (!game.IsLocallyPaused) Tap(Key.Escape);
        await Until(() => game.IsLocallyPaused && !game.HasPendingAction, "Pause did not settle.");
        await Delay(0.25);
        string held = PublicHash();
        await Delay(0.25);
        Require(PublicHash() == held, "Paused defeat continued changing.");
    }

    private LocalWorldView View => game.CurrentView ?? throw new InvalidOperationException("No public view.");
    private LocalEncounterActorView Wolf => View.EncounterActors.Single(actor => actor.ActorId == _wolf);
    private FarmActionOffer? Offer(FarmSemanticAction action) => game.CurrentOffers.GetValueOrDefault(action);
    private bool PositionsUnchanged() => View.Actors.Count == _positions.Count
        && View.Actors.All(actor => _positions.TryGetValue(actor.ActorId, out var feet) && actor.Feet == feet);
    private string PublicHash() => Hash(View);
    private string RetainedHash()
    {
        var view = View;
        var progression = view.Progression!;
        return Hash(new { view.PlayerInventory, view.Construction, view.Quests,
            progression.Mp, progression.Experience, progression.CurrencyBalance,
            Actors = view.Actors.Select(actor => new { actor.ActorId, actor.PresenceRevision, actor.Feet }) });
    }
    private static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private string Output(string stage, string extension) => Path.ChangeExtension(reportPath, "." + stage + "." + extension);
    private void Observe(string stage) => _observations.Add(new { Stage = stage, View });

    private void Tap(Key key)
    {
        _inputs.Add(new { Stage = _stage, Key = key.ToString(), Milliseconds = _elapsed.ElapsedMilliseconds });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
        Input.FlushBufferedEvents();
    }
    private void Joy(JoyButton button)
    {
        _inputs.Add(new { Stage = _stage, Button = button.ToString(), Milliseconds = _elapsed.ElapsedMilliseconds });
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = true });
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = false });
        Input.FlushBufferedEvents();
    }
    private async Task Click(Button button)
    {
        Require(button.IsVisibleInTree() && !button.Disabled, "Wait pointer control is unavailable.");
        Vector2 at = button.GetGlobalRect().GetCenter();
        _inputs.Add(new { Stage = _stage, Control = button.Name.ToString(), X = at.X, Y = at.Y });
        GetViewport().PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at }, true);
        await Frame();
        GetViewport().PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at,
            ButtonIndex = MouseButton.Left, Pressed = true }, true);
        await Frame();
        GetViewport().PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at,
            ButtonIndex = MouseButton.Left, Pressed = false }, true);
    }
    private async Task Until(Func<bool> condition, string failure, double seconds = 10)
    {
        var timer = Stopwatch.StartNew();
        while (true)
        {
            bool satisfied = condition();
            Deadline();
            Require(timer.Elapsed.TotalSeconds < seconds, failure);
            if (satisfied) return;
            await Frame();
        }
    }
    private async Task Delay(double seconds)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < seconds) await Frame();
    }
    private async Task Frame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Deadline();
    }
    private void Deadline() => Require(!game.HasFailed && _elapsed.Elapsed.TotalSeconds < 150,
        "Defeat journey failed or exceeded its 150-second bound at " + _stage);
    private async Task Capture(string stage)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(Output(stage, "png")) == Error.Ok, "Defeat image could not be saved.");
    }
    private void Report(string status, string? error) => File.WriteAllText(Output(mode, status == "passed" ? "json" : "failed.json"),
        JsonSerializer.Serialize(new { Kind = "native-encounter-defeat-v1", Mode = mode, Status = status, Error = error,
            Stage = _stage, InitialHash = _initial, FinalHash = game.CurrentView is null ? null : PublicHash(),
            Checks = _checks, Inputs = _inputs, WaitChoices = _waitChoices, Observations = _observations,
            Display = DisplayServer.GetName(), FocusRequests = 0, Unfocusable = GetWindow().Unfocusable,
            NativeFocused = GetWindow().HasFocus(), ObservedNativeFocus = _observedNativeFocus,
            StartupNoFocus = ProjectSettings.GetSetting("display/window/size/no_focus").AsBool(),
            game.IsLocallyPaused, game.IsWindowFocused, DurationMilliseconds = _elapsed.ElapsedMilliseconds,
            Limit = "Controlled background focus and injected input; actual incapacity without available release or invented recovery. Physical-device/art/full-phase acceptance remains open."
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
