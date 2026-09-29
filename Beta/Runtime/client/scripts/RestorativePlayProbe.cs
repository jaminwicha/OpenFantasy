using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Native continuation from an earned injury and finite medicine, with exact paused recovery.</summary>
internal sealed partial class RestorativePlayProbe(FarmGame game, string reportPath, string metadataPath, string mode) : Node
{
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly List<string> _checks = [];
    private readonly List<object> _observations = [];
    private readonly List<object> _inputs = [];
    private string _stage = "opening";
    private string? _initial;
    private bool _observedNativeFocus;
    private Dictionary<EntityId, SubunitPosition> _positions = [];
    private LocalWorldView View => game.CurrentView ?? throw new InvalidOperationException("No public world.");
    private FarmQuickWheel Wheel => game.GetNode<FarmQuickWheel>("FarmQuickWheel");
    private Label HealthDelta => game.GetChildren().OfType<FarmCanvas>().Single()
        .GetNode<Label>("Actors/Actor_" + View.PlayerActorId + "/HealthDelta");
    private FarmSemanticAction Selection => mode == "revoke" ? FarmSemanticAction.RevokeCarePermission : FarmSemanticAction.UseRestorative;

    public override void _Process(double delta)
    {
        if (DisplayServer.GetName() != "headless" && GetWindow().HasFocus()) _observedNativeFocus = true;
    }

    public override async void _Ready()
    {
        try
        {
            using var metadata = JsonDocument.Parse(File.ReadAllText(metadataPath));
            var expected = metadata.RootElement;
            await Until(() => game.CurrentView?.Restorative is not null && game.CurrentOffers.Count > 0,
                "Restorative world did not open.", 30);
            game.InjectFixtureFocus(true);
            Require(game.IsLocallyPaused && View.Care is null && !View.IsInEncounter,
                "Ordinary native continuation must open paused in exploration without pending aftermath care.");
            _initial = PublicHash();
            _positions = View.Actors.ToDictionary(actor => actor.ActorId, actor => actor.Feet);
            Observe("opened");
            if (mode == "reopen")
            {
                using var prior = JsonDocument.Parse(File.ReadAllText(Output("active", "json")));
                Require(prior.RootElement.GetProperty("Status").GetString() == "passed"
                    && prior.RootElement.GetProperty("FinalHash").GetString() == _initial,
                    "Separate process did not restore the exact final public state.");
                await Delay(0.5);
                Require(PublicHash() == _initial && !game.IsWheelOpen && game.CurrentTarget is null && !HealthDelta.Visible,
                    "Paused reopening changed the world or replayed a draft/health cue.");
                _checks.Add("Separate process preserves exact paused state without replaying selection or health feedback.");
            }
            else
            {
                Require(View.Progression!.Hp == expected.GetProperty("beforeHp").GetInt32()
                    && View.Progression.Mp == expected.GetProperty("beforeMp").GetInt32(), "Earned fixture resources differ.");
                await Inspect();
                await Act(expected.GetProperty("expectedHp").GetInt32(), expected.GetProperty("expectedMp").GetInt32());
            }
            await PauseAndSettle();
            Require(PositionsUnchanged(), "Ordinary care moved or substituted an actor.");
            Observe("final");
            await Capture(mode == "reopen" ? "reopen" : "final");
            Deadline();
            Report("passed", null);
            game.RequestClose();
        }
        catch (Exception error)
        {
            Report("failed", error.ToString());
            GD.PushError("Native restorative probe failed: " + error);
            GetTree().Quit(1);
        }
    }

    private async Task Inspect()
    {
        _stage = "inspection";
        string held = PublicHash();
        await OpenSelection(controller: false);
        Require(Wheel.Model!.PageName == "Care" && Wheel.Model.PageCount == 2
            && Wheel.Model.Selected!.Offer.Action == Selection, "Care page lost its exact selected action.");
        Tap(Key.Enter);
        Joy(JoyButton.A);
        await Delay(0.25);
        Require(game.IsWheelOpen && !game.HasPendingAction && PublicHash() == held,
            "Paused wheel inspection or confirmation spent medicine, changed permission or advanced time.");
        Observe("inspection");
        await Capture("inspection");
        Joy(JoyButton.B);
        await Until(() => !game.IsWheelOpen, "Inspection cancel did not close the wheel.");
        await OpenSelection(controller: true);
        game.InjectFixtureFocus(false);
        await Until(() => !game.IsWheelOpen && !game.IsWindowFocused, "Focus loss retained a wheel selection.");
        await Delay(0.25);
        Require(PublicHash() == held && !game.HasPendingAction, "Focus loss changed the paused world.");
        game.InjectFixtureFocus(true);
        await Frame();
        _checks.Add("Page navigation, paused confirmation, cancellation and controlled focus loss commit no action.");
    }

    private async Task Act(int expectedHp, int expectedMp)
    {
        _stage = "act-" + mode;
        Tap(Key.Escape);
        await Until(() => !game.IsLocallyPaused && game.IsWindowFocused, "Exploration did not resume.");
        if (mode == "pointer")
        {
            var button = game.GetNode<Button>("FarmHud/Root/Footer/Column/Actions/UseRestorative");
            await Until(() => button.IsVisibleInTree() && !button.Disabled
                && game.CurrentOffers.GetValueOrDefault(Selection)?.CanExecute == true,
                "Personal medicine button was not ready.");
            await Click(button);
        }
        else if (mode is "keyboard" or "controller" or "revoke")
        {
            await OpenSelection(controller: mode == "controller");
            Require(Wheel.Model!.Selected!.Offer.CanExecute, "Captured care selection became unavailable before commitment.");
            if (mode == "revoke")
            {
                Require(Wheel.Model.Selected.Offer.Intent is RevokeCarePermissionIntent,
                    "Withdrawing patient permission must not create a helper command.");
                await Click(Wheel.GetNode<Button>("%ConfirmChoice"));
            }
            else if (mode == "controller") Joy(JoyButton.A);
            else Tap(Key.Enter);
        }
        else Require(mode == "helper", "Unsupported native restorative action mode.");

        if (mode == "revoke")
        {
            await Until(() => !game.HasPendingAction && View.Restorative!.PermissionOffers.All(offer => !offer.IsActive),
                "Patient withdrawal did not commit.");
            Require(View.Progression!.Hp == expectedHp && !HealthDelta.Visible, "Permission withdrawal restored health or replayed treatment.");
        }
        else
        {
            await Until(() => !game.HasPendingAction && View.Progression!.Hp == expectedHp,
                "Exact restorative action did not restore the earned patient.");
            await Until(() => HealthDelta.Visible && HealthDelta.Text == "+40", "Actual ordinary restoration did not reach its health cue.");
            Observe("restored");
            await Capture("restoration");
        }
        Require(View.Progression!.Mp == expectedMp && View.Care is null && !View.IsInEncounter,
            "Ordinary care altered magic or entered aftermath/encounter control.");
        _checks.Add(mode switch
        {
            "helper" => "Readiness permits a helper-owned policy action without a patient item command.",
            "revoke" => "Actual native control commits patient-owned withdrawal without medicine expenditure or restoration.",
            _ => "Actual native control commits one exact personal restorative use."
        });
    }

    private async Task OpenSelection(bool controller)
    {
        Tap(Key.V);
        await Until(() => game.IsWheelOpen && Wheel.Model is not null, "Quick wheel did not open.");
        Require(Wheel.Model!.PageName == "Actions" && Wheel.Model.Selected is null, "Opening silently preselected a care action.");
        if (controller) Joy(JoyButton.RightShoulder); else Tap(Key.Q);
        await Until(() => Wheel.Model?.PageName == "Care", "Care page navigation failed.");
        Require(Wheel.Model!.Selected is null, "Page change committed or retained a selection.");
        if (controller) Joy(mode == "revoke" ? JoyButton.DpadRight : JoyButton.DpadUp);
        else Tap(mode == "revoke" ? Key.Right : Key.Up);
        await Until(() => Wheel.Model?.Selected?.Action == Selection, "Care page did not select the expected sector.");
        Require(Wheel.Model!.Selected!.Offer.Intent == game.CurrentOffers[Selection].Intent,
            "Wheel substituted the exact selected action.");
    }

    private async Task PauseAndSettle()
    {
        if (game.IsWheelOpen) Tap(Key.Escape);
        if (!game.IsLocallyPaused) Tap(Key.Escape);
        await Until(() => game.IsLocallyPaused && !game.HasPendingAction, "Pause did not settle.");
        await Delay(0.25);
        string held = PublicHash();
        await Delay(0.25);
        Require(PublicHash() == held, "Paused restorative state continued changing.");
    }

    private bool PositionsUnchanged() => View.Actors.Count == _positions.Count
        && View.Actors.All(actor => _positions.TryGetValue(actor.ActorId, out var feet) && actor.Feet == feet);
    private string PublicHash() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(View))));
    private void Observe(string label) => _observations.Add(new { Label = label, View, game.IsLocallyPaused,
        game.IsWindowFocused, game.HasPendingAction, game.IsWheelOpen,
        HealthCue = new { HealthDelta.Text, HealthDelta.Visible },
        Selected = Wheel.Model?.Selected?.Action.ToString(), Page = Wheel.Model?.PageName });

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
        Require(button.IsVisibleInTree() && !button.Disabled, "Native pointer control is unavailable.");
        GetViewport().NotifyMouseEntered();
        Vector2 at = button.GetGlobalRect().GetCenter();
        _inputs.Add(new { Stage = _stage, PointerControl = button.Name.ToString(), X = at.X, Y = at.Y });
        GetViewport().PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at }, true);
        await Frame();
        GetViewport().PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        await Frame();
        GetViewport().PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, Pressed = false }, true);
    }
    private async Task Until(Func<bool> predicate, string failure, double seconds = 10)
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
        "Restorative continuation failed or exceeded its 150-second bound at " + _stage);
    private async Task Capture(string stage)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(Output(stage, "png")) == Error.Ok, "Restorative image could not be saved.");
    }
    private string Output(string stage, string extension) => Path.ChangeExtension(reportPath, null) + "." + stage + "." + extension;
    private void Report(string status, string? error) => File.WriteAllText(Output(mode == "reopen" ? "reopen" : "active",
        status == "passed" ? "json" : "failed.json"), JsonSerializer.Serialize(new
        {
            Kind = "native-restorative-v1", Mode = mode, Status = status, Error = error, Stage = _stage,
            InitialHash = _initial, FinalHash = game.CurrentView is null ? null : PublicHash(), Checks = _checks,
            Inputs = _inputs, Observations = _observations, Display = DisplayServer.GetName(), FocusRequests = 0,
            Unfocusable = GetWindow().Unfocusable, NativeFocused = GetWindow().HasFocus(), ObservedNativeFocus = _observedNativeFocus,
            StartupNoFocus = ProjectSettings.GetSetting("display/window/size/no_focus").AsBool(),
            game.IsLocallyPaused, game.IsWindowFocused, DurationMilliseconds = _elapsed.ElapsedMilliseconds,
            Limit = "Scripted managed preparation precedes the selected injected native action and paused recovery. This report does not prove its other supported action modes. Physical devices, OS focus, art and production gates remain open."
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
