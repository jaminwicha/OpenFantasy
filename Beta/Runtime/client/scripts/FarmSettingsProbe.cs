using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Godot;
using OpenFantasy.Client.Settings;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>
/// Native settings journey through the real key, controls and file: open with the settings key, change levels and
/// captions with the actual controls, confirm walking stays suppressed and the world keeps its own pause state,
/// close with Cancel, then confirm a second process restores the saved preferences before any interaction.
/// </summary>
internal sealed partial class FarmSettingsProbe(FarmGame game, string settingsFile, string reportPath, bool reopen) : Node
{
    private readonly Stopwatch _timeout = Stopwatch.StartNew();
    private readonly Stopwatch _stageTime = new();
    private readonly List<string> _checks = [];
    private int _stage;
    private bool _done;
    private string? _heldPosition;

    public override void _Process(double delta)
    {
        if (_done) return;
        try
        {
            Require(!game.HasFailed, "The native game reported a failure.");
            Require(_timeout.Elapsed < TimeSpan.FromSeconds(40), "The native settings probe timed out at stage " + _stage);
            if (game.CurrentView is not { } view) return;
            if (reopen) { Reopened(); return; }
            switch (_stage)
            {
                case 0:
                    Require(!game.IsSettingsOpen && !File.Exists(settingsFile), "The fixture did not start with closed, unsaved settings.");
                    Press(FarmInputActions.Settings);
                    _stage = 1;
                    break;
                case 1:
                    Require(game.IsSettingsOpen && game.SettingsPanel.IsOpen, "The settings key did not open the panel.");
                    Require(game.SettingsPanel.Level("Master").HasFocus(), "Opening settings did not focus the first control.");
                    Require(!game.IsLocallyPaused, "Opening settings paused the world.");
                    _heldPosition = PlayerPosition(view);
                    Input.ActionPress(FarmInputActions.WalkRight);
                    _stageTime.Restart();
                    _stage = 2;
                    break;
                case 2:
                    if (_stageTime.ElapsedMilliseconds < 400) break;
                    Input.ActionRelease(FarmInputActions.WalkRight);
                    Require(PlayerPosition(view) == _heldPosition, "Walking input moved the player while settings owned input.");
                    _checks.Add("Settings opened by key, focused their first control, suppressed walking and left the world unpaused.");
                    game.SettingsPanel.Level("Music").Value = 35;
                    game.SettingsPanel.Level("Effects").Value = 0;
                    game.SettingsPanel.CaptionsToggle.ButtonPressed = true;
                    RequireApplied(0.35, effectsMuted: true, captions: true);
                    _checks.Add("The actual controls set Music to 35%, muted Effects and enabled captions immediately.");
                    game.SettingsPanel.KeyButton("talk").EmitSignal(BaseButton.SignalName.Pressed);
                    Require(game.SettingsPanel.CapturingAction == "talk", "The talk row did not start choosing a key.");
                    PressKey(Key.T);
                    _stage = 4;
                    break;
                case 4:
                    Require(game.SettingsPanel.CapturingAction is null, "Choosing a key did not finish on the next key press.");
                    Require(game.IsSettingsOpen, "The chosen key leaked into gameplay routing and closed settings.");
                    game.SettingsPanel.KeyButton("gather").EmitSignal(BaseButton.SignalName.Pressed);
                    PressKey(Key.G);
                    _stage = 5;
                    break;
                case 5:
                    RequireKeys();
                    _checks.Add("Capture mode rebound Talk to T and moved Gather onto Give's G, swapping Give to F, without leaking either press.");
                    Press(FarmInputActions.Cancel);
                    _stage = 3;
                    break;
                case 3:
                    Require(!game.IsSettingsOpen && !game.SettingsPanel.IsOpen, "Cancel did not close settings.");
                    FarmSettings saved = new FarmSettingsStore(settingsFile).Load();
                    Require(saved == game.Settings, "Closing did not write the applied settings to the fixture file.");
                    _checks.Add("Cancel closed settings and wrote exactly the applied preferences to the fixture file.");
                    Finish();
                    break;
            }
        }
        catch (Exception error)
        {
            _done = true;
            Input.ActionRelease(FarmInputActions.WalkRight);
            WriteReport("failed", error.Message);
            GD.PushError("Native settings probe failed: " + error);
            GetTree().Quit(1);
        }
    }

    private void Reopened()
    {
        RequireApplied(0.35, effectsMuted: true, captions: true);
        RequireKeys();
        Require(!game.IsSettingsOpen, "Reopening opened settings by itself.");
        _checks.Add("A second process restored Music 35%, muted Effects, captions and the rebound keys before any interaction.");
        Finish();
    }

    private void RequireApplied(double music, bool effectsMuted, bool captions)
    {
        Require(Math.Abs(game.Settings.Sound.Music - music) < 1e-9, "Music level was not applied: " + game.Settings.Sound.Music);
        int musicBus = AudioServer.GetBusIndex("Music");
        Require(Math.Abs(AudioServer.GetBusVolumeDb(musicBus) - Mathf.LinearToDb((float)music)) < 0.01f, "The Music bus volume did not follow the setting.");
        Require(AudioServer.IsBusMute(AudioServer.GetBusIndex("Effects")) == effectsMuted, "The Effects bus mute did not follow the setting.");
        Require(game.Settings.Captions == captions, "The captions preference was not applied.");
    }

    private void RequireKeys()
    {
        Require(Bound(Key.T, FarmInputActions.Interact) && !Bound(Key.E, FarmInputActions.Interact), "Talk is not on T alone.");
        Require(Bound(Key.G, FarmInputActions.Gather) && !Bound(Key.F, FarmInputActions.Gather), "Gather is not on G alone.");
        Require(Bound(Key.F, FarmInputActions.Give) && !Bound(Key.G, FarmInputActions.Give), "Give was not swapped onto F.");
        Require(Bound(Key.Left, FarmInputActions.WalkLeft) && Bound(Key.A, FarmInputActions.WalkLeft), "Walking keys changed unexpectedly.");
        Require(game.Settings.Keys["talk"] == (long)Key.T, "The saved bindings do not record Talk on T.");
        Require(FarmKeyHints.Current is { Interact: "T", Gather: "G", Give: "F" },
            "On-screen key hints do not follow the rebound keys: " + FarmKeyHints.Current);
    }

    private static bool Bound(Key key, string action) =>
        InputMap.ActionHasEvent(action, new InputEventKey { PhysicalKeycode = key });

    private static void PressKey(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
    }

    private static string PlayerPosition(LocalWorldView view) =>
        view.Actors.Single(actor => actor.ActorId == view.PlayerActorId).Feet.ToString();

    private void Finish()
    {
        WriteReport("passed", null);
        GD.Print("Native settings probe passed: " + reportPath);
        _done = true;
        game.RequestClose();
    }

    private void WriteReport(string status, string? failure)
    {
        string output = reopen ? Path.ChangeExtension(reportPath, ".reopen.json") : reportPath;
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            Kind = "native-settings-v1", Stage = reopen ? "reopen" : "change", Status = status, Failure = failure,
            Runtime = RuntimeInformation.FrameworkDescription, DisplayDriver = DisplayServer.GetName(), ProbeStage = _stage,
            Settings = game.Settings.ToEntries(), SettingsFileExists = File.Exists(settingsFile),
            Buses = FarmSettings.Buses.ToDictionary(bus => bus, bus => new
            {
                VolumeDb = AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex(bus)),
                Muted = AudioServer.IsBusMute(AudioServer.GetBusIndex(bus)),
            }),
            Checks = _checks, DurationMilliseconds = _timeout.ElapsedMilliseconds,
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    private static void Press(string action)
    {
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true, Strength = 1 });
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
