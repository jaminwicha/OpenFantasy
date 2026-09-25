using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Disposable native input journey; observes public state and never edits combat outcomes.</summary>
internal sealed partial class EncounterPlayProbe(FarmGame game, string reportPath, string mode) : Node
{
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly List<string> _checks = [];
    private readonly List<object> _observations = [];
    private readonly List<object> _inputs = [];
    private string _stage = "opening";
    private string? _initial;
    private bool _observedNativeFocus;
    private EntityId _wolf;
    private Dictionary<EntityId, OpenFantasy.Core.Spatial.SubunitPosition> _positions = [];

    public override void _Process(double delta)
    {
        if (DisplayServer.GetName() != "headless" && GetWindow().HasFocus()) _observedNativeFocus = true;
    }

    public override async void _Ready()
    {
        try
        {
            await Until(() => game.CurrentView?.EncounterLifecycle is not null && game.CurrentOffers.Count > 0,
                "Encounter world did not open.", 30);
            game.InjectFixtureFocus(true);
            Require(game.IsLocallyPaused, "The disposable world must open paused.");
            _initial = PublicHash();
            if (mode != "fresh")
            {
                string priorMode = mode == "duel" ? "fresh" : "duel";
                using var prior = JsonDocument.Parse(File.ReadAllText(Output(priorMode, "json")));
                Require(prior.RootElement.GetProperty("Status").GetString() == "passed"
                    && prior.RootElement.GetProperty("FinalHash").GetString() == _initial,
                    "Exact public encounter state was not recovered before readiness.");
                _checks.Add("Separate process recovers exact paused condition, timing, actors and inventory.");
            }
            await Delay(0.4);
            Require(PublicHash() == _initial && game.CurrentTarget is null && !game.IsWheelOpen,
                "Paused opening changed state or restored a UI draft.");
            _wolf = View.EncounterLifecycle!.Entries.Single(entry => entry.DisplayName.StartsWith("East", StringComparison.Ordinal)).TargetActorId;
            _positions = View.Actors.ToDictionary(actor => actor.ActorId, actor => actor.Feet);
            Observe("opening");
            if (mode == "fresh") await EnterAndChoose();
            else if (mode == "duel") await FinishDuel();
            else CheckReleased();
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
            GD.PushError("Native encounter probe failed: " + error);
            GetTree().Quit(1);
        }
    }

    private async Task EnterAndChoose()
    {
        _stage = "entry";
        Tap(Key.Escape);
        await Select(_wolf);
        await Until(() => Offer(FarmSemanticAction.EnterEncounter)?.CanExecute == true, "Entry was not offered.");
        await Capture("entry");
        _positions = View.Actors.ToDictionary(actor => actor.ActorId, actor => actor.Feet);
        Tap(Key.E);
        await Until(() => View.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Active
            && !game.HasPendingAction && Offer(FarmSemanticAction.Strike)?.CanExecute == true, "Entry did not stage into a choice.");
        await Delay(0.25);
        // The launcher verifies every actor against the exact recorded pre-entry commit.
        // A live pre-submit view can precede a lawful exploration step by the background host.
        _positions = View.Actors.ToDictionary(actor => actor.ActorId, actor => actor.Feet);
        string held = PublicHash();
        Input.ActionPress(FarmInputActions.WalkRight);
        Tap(Key.F); // A farm shortcut must not run in a battle.
        await Delay(0.45);
        Input.ActionRelease(FarmInputActions.WalkRight);
        Require(PublicHash() == held && PositionsUnchanged(), "Waiting admitted movement, ordinary work or elapsed time.");
        _checks.Add("Entry preserves actual positions; required choice holds time and blocks movement/farm shortcuts.");
        await Capture("waiting");
        await OpenWheel(FarmSemanticAction.Wait, false);
        await Capture("wait-inspection");
        game.InjectFixtureFocus(false);
        await Until(() => !game.IsWheelOpen && !game.IsWindowFocused, "Focus loss did not cancel inspection.");
        game.InjectFixtureFocus(true);
        await Delay(0.25);
        Require(PublicHash() == held && !game.HasPendingAction, "Inspection/focus return committed a choice.");
        _checks.Add("Wait inspection and focus cancellation do not spend readiness or submit an action.");
        Tap(Key.I);
        await Until(() => game.IsInventoryOpen, "Pack did not open during required choice.");
        await Delay(0.2);
        Tap(Key.Escape);
        await Until(() => !game.IsInventoryOpen, "Pack did not close.");
        Require(PublicHash() == held && !game.IsLocallyPaused, "Pack inspection changed encounter time or pause ownership.");
        _stage = "first-strike";
        int hp = Wolf.CurrentHp;
        await OpenWheel(FarmSemanticAction.Strike, true);
        await Until(() => Wolf.CurrentHp < hp && !game.HasPendingAction, "Confirmed strike did not resolve real damage.");
        await PauseAndSettle();
        Require(PositionsUnchanged(), "Damage presentation moved authoritative bodies.");
        _checks.Add("Controller wheel confirmation queues and resolves a source-backed strike without moving bodies.");
        await Capture("damage");
    }

    private async Task FinishDuel()
    {
        _stage = "duel";
        Require(View.IsInEncounter && Wolf.CurrentHp > 0 && View.EncounterLifecycle!.EncounterCount == 1,
            "The reopened stage is not the exact unfinished encounter.");
        Tap(Key.Escape);
        await Select(_wolf);
        int choices = 0;
        while (View.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome)
        {
            await Until(() => View.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Outcome
                || !game.HasPendingAction && Offer(FarmSemanticAction.Strike)?.CanExecute == true,
                "Neither a legal strike nor outcome arrived.", 15);
            if (View.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Outcome) break;
            Require(++choices <= 12, "The declared source duel exceeded its expected bounded action count.");
            ulong timing = View.Encounter!.TimingRevision;
            Tap(Key.E);
            await Until(() => View.Encounter!.TimingRevision != timing && !game.HasPendingAction,
                "A deliberate strike did not receive a new authoritative boundary.");
        }
        await Until(() => Offer(FarmSemanticAction.ReleaseEncounter)?.CanExecute == true,
            "The real source duel did not reach a releasable outcome.");
        Require(Wolf.IsIncapacitated && View.Progression!.Hp == 2 && PositionsUnchanged(),
            "The unchanged source duel must incapacitate the east wolf, retain player HP2 and preserve all bodies.");
        Observe("outcome");
        await Capture("outcome");
        string outcome = PublicHash();
        await Delay(0.5);
        Require(PublicHash() == outcome, "Outcome hold advanced time or restored resources.");
        _checks.Add("Real source duel reaches wolf HP0/player HP2; outcome holds without rewards, restoration or body removal.");
        _stage = "release";
        await Click(game.GetNode<Button>("FarmHud/Root/Footer/Column/Actions/ReleaseEncounter"));
        await Until(() => View.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Exploration && !game.HasPendingAction,
            "Outcome button did not release the encounter.");
        await PauseAndSettle();
        CheckReleased();
    }

    private void CheckReleased()
    {
        Require(View.EncounterLifecycle!.Phase == EncounterLifecyclePhase.Exploration
            && View.EncounterLifecycle.EncounterCount == 1 && View.Progression!.Hp == 2
            && View.Actors.Single(actor => actor.ActorId == _wolf).IsIncapacitated
            && !View.EncounterLifecycle.Entries.Single(entry => entry.TargetActorId == _wolf).CanEnter
            && View.Actors.Count == 4, "Release or restart reset condition, lost residents or revived the encounter.");
        _checks.Add("Released exploration retains player wounds and the incapacitated wolf; defeated encounter remains unavailable.");
    }

    private async Task OpenWheel(FarmSemanticAction action, bool confirm)
    {
        await Until(() => !game.HasPendingAction && Offer(action)?.CanExecute == true, "Wheel choice is not available.");
        Tap(Key.V);
        await Until(() => game.IsWheelOpen, "Wheel did not open.");
        var wheel = game.GetNode<FarmQuickWheel>("FarmQuickWheel");
        int sector = Enumerable.Range(0, 4).Single(index => wheel.Model!.Slots[index]?.Action == action);
        Joy(JoyButtonFor(sector));
        await Until(() => wheel.Model?.Selected?.Action == action, "Controller did not select intended wheel action.");
        if (confirm)
        {
            Joy(JoyButton.A);
            await Until(() => !game.IsWheelOpen, "Confirm did not close wheel.");
        }
    }

    private static JoyButton JoyButtonFor(int sector) => sector switch
    { 0 => JoyButton.DpadUp, 1 => JoyButton.DpadRight, 2 => JoyButton.DpadDown, _ => JoyButton.DpadLeft };

    private async Task Select(EntityId id)
    {
        for (int count = 0; game.CurrentTarget?.Id != id; count++)
        {
            Require(count < 12, "Target cycling did not reach the intended visible wolf.");
            Tap(Key.Q);
            await Frame();
        }
        await Until(() => game.CurrentOffers.Values.Any(offer => offer.TargetId == id), "Presented target did not settle.");
    }

    private async Task PauseAndSettle()
    {
        if (!game.IsLocallyPaused) Tap(Key.Escape);
        await Until(() => game.IsLocallyPaused && !game.HasPendingAction, "Pause did not settle.");
        await Delay(0.25);
        string held = PublicHash();
        await Delay(0.25);
        Require(PublicHash() == held, "Paused world continued changing.");
    }

    private LocalWorldView View => game.CurrentView ?? throw new InvalidOperationException("No public view.");
    private LocalEncounterActorView Wolf => View.EncounterActors.Single(actor => actor.ActorId == _wolf);
    private FarmActionOffer? Offer(FarmSemanticAction action) => game.CurrentOffers.GetValueOrDefault(action);
    private bool PositionsUnchanged() => View.Actors.Count == _positions.Count
        && View.Actors.All(actor => _positions.TryGetValue(actor.ActorId, out var feet) && actor.Feet == feet);
    private string PublicHash() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(View))));
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
        Require(button.IsVisibleInTree() && !button.Disabled, "Intended pointer control is unavailable.");
        Vector2 at = button.GetGlobalRect().GetCenter();
        _inputs.Add(new { Stage = _stage, Control = button.Name.ToString(), X = at.X, Y = at.Y });
        // The rectangle is in viewport coordinates; use the same local-coordinate injection as
        // the retained wheel probe rather than treating it as OS-window coordinates.
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
        "Encounter journey failed or exceeded its 150-second bound at " + _stage);
    private async Task Capture(string stage)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var footer = game.GetNode<Control>("FarmHud/Root/Footer");
        if (footer.IsVisibleInTree())
        {
            var canvas = game.GetChildren().OfType<FarmCanvas>().Single();
            var actorIds = View.EncounterLifecycle!.Entries.Select(entry => entry.TargetActorId).Append(View.PlayerActorId);
            foreach (EntityId actorId in actorIds)
            {
                var actor = canvas.GetNode<Node2D>("Actors/Actor_" + actorId);
                Vector2 feet = actor.GetGlobalTransformWithCanvas().Origin;
                Require(feet.Y + 12 < footer.GetGlobalRect().Position.Y,
                    "The encounter footer obscures an observed participant's feet or ground ring at " + stage);
            }
        }
        using Image image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(Output(stage, "png")) == Error.Ok, "Encounter image could not be saved.");
    }
    private void Report(string status, string? error) => File.WriteAllText(Output(mode, status == "passed" ? "json" : "failed.json"),
        JsonSerializer.Serialize(new { Kind = "native-encounter-play-v1", Mode = mode, Status = status, Error = error,
            Stage = _stage, InitialHash = _initial, FinalHash = game.CurrentView is null ? null : PublicHash(),
            Checks = _checks, Inputs = _inputs, Observations = _observations, Display = DisplayServer.GetName(),
            FocusRequests = 0, Unfocusable = GetWindow().Unfocusable, NativeFocused = GetWindow().HasFocus(),
            ObservedNativeFocus = _observedNativeFocus,
            StartupNoFocus = ProjectSettings.GetSetting("display/window/size/no_focus").AsBool(),
            game.IsLocallyPaused, game.IsWindowFocused, DurationMilliseconds = _elapsed.ElapsedMilliseconds,
            Limit = "Controlled background focus and injected input; physical devices, production art, wider effects and full phase gates remain open."
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
