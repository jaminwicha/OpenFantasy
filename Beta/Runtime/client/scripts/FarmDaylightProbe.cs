using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Client;

/// <summary>Native lighting qualification using explicit-clock disposable fixtures, never a normal save.</summary>
internal sealed partial class FarmDaylightProbe(string savePath, string reportPath, string phase) : Node
{
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly List<string> _checks = [];
    private FarmGame? _game;

    public override async void _Ready()
    {
        try
        {
            if (phase != "reopen")
            {
                await Task.Run(CreateFixture);
            }
            else if (!File.Exists(savePath))
            {
                throw new InvalidOperationException("Daylight reopen requires the retained fixture save.");
            }
            _game = new FarmGame { SavePathOverride = savePath, StartPaused = true, ProductionProfile = false };
            AddChild(_game);
            if (DisplayServer.GetName() != "headless") GetWindow().GrabFocus();
            await WaitFor(() => _game.CurrentView is { } view && _game.Daylight.PresentedAt == view.Calendar
                && _game.IsLocallyPaused, "The fixture did not become ready and paused.");
            LocalWorldView initial = _game.CurrentView!;
            string initialView = JsonSerializer.Serialize(initial);
            Color initialColor = _game.Daylight.Color;
            await Task.Delay(500);
            Require(_game.IsLocallyPaused && JsonSerializer.Serialize(_game.CurrentView) == initialView
                && _game.Daylight.Color == initialColor, "Paused frames changed the clock, world or daylight.");
            _checks.Add("Paused native frames preserve the complete public world and daylight.");
            Require(_game.Daylight.GetCanvas() != _game.GetNode<CanvasLayer>("FarmHud").GetCanvas(),
                "The HUD shares the modulated world canvas.");
            Require(_game.Daylight.GetCanvas() != _game.GetNode<CanvasLayer>("FarmInventoryScreen").GetCanvas(),
                "The pack shares the modulated world canvas.");
            _checks.Add("HUD and pack use separate canvases from outdoor ambient light.");
            Color day = _game.Daylight.Palette.Sample(0.5f);
            Color night = _game.Daylight.Palette.Sample(0);
            Require(day == Colors.White && night.B > night.R && night.R >= 0.45f && night.A == 1,
                "The candidate daylight/nocturnal readability anchors changed; review the palette before qualifying it.");
            Require(_game.Daylight.Palette.Sample(1) == night, "Midnight palette endpoints disagree.");
            _checks.Add("Candidate palette retains full daylight, readable blue night and a continuous midnight seam.");
            string clockText = _game.GetNode<FarmHud>("FarmHud").GetNode<Label>("%Clock").Text;
            await Capture("initial");

            if (phase == "night")
            {
                await CheckNightInterfaces();
            }

            if (phase is "dusk" or "rollover")
            {
                await WaitFor(() => _game.IsWindowFocused, "The fixture window did not receive focus for input.");
                PressPause();
                await WaitFor(() => !_game.IsLocallyPaused, "Escape did not resume the paused fixture.");
                WorldInstant target = initial.Calendar.AddMilliseconds(10 * ExposurePacingV1.ExplorationMillisecondsPerStep);
                await WaitFor(() => _game.CurrentView!.Calendar >= target, "The ordinary running host did not advance.");
                PressPause();
                await Task.Delay(250);
                LocalWorldView stopped = _game.CurrentView!;
                Color stoppedColor = _game.Daylight.Color;
                string stoppedView = JsonSerializer.Serialize(stopped);
                Require(_game.IsLocallyPaused && _game.Daylight.PresentedAt == stopped.Calendar,
                    "Daylight did not follow the latest committed calendar before pause.");
                if (phase == "rollover")
                {
                    Require(stopped.Calendar.Days == initial.Calendar.Days + 1 && stoppedColor == initialColor,
                        "Actual midnight advancement did not preserve the continuous night tint.");
                }
                else
                {
                    Require(stoppedColor != initialColor, "Dusk light did not respond to actual host advancement.");
                }
                await Task.Delay(500);
                Require(_game.Daylight.Color == stoppedColor && JsonSerializer.Serialize(_game.CurrentView) == stoppedView,
                    "Daylight or world continued after the ordinary pause input.");
                _checks.Add(phase == "rollover" ? "Actual host steps cross midnight without a lighting flash or catch-up."
                    : "Actual host steps change dusk light; ordinary pause stops both clock and tint.");
                await Capture("advanced");
            }
            LocalWorldView final = _game.CurrentView!;
            WriteReport(new
            {
                Status = "passed", Phase = phase, Scope = "Explicit-calendar source-farm fixture; not an accelerated normal world",
                Runtime = RuntimeInformation.FrameworkDescription, DisplayDriver = DisplayServer.GetName(),
                InitialCalendar = initial.Calendar, InitialColor = Channels(initialColor), ClockText = clockText,
                FinalCalendar = final.Calendar, FinalColor = Channels(_game.Daylight.Color),
                FinalWorldView = JsonSerializer.Serialize(final), Checks = _checks,
                _game.IsLocallyPaused, _game.IsWindowFocused, ElapsedMilliseconds = _elapsed.ElapsedMilliseconds,
                NominalDayMilliseconds = WorldInstant.MillisecondsPerDay / ExposurePacingV1.ExplorationMillisecondsPerStep
                    * LocalWorldSession.StepInterval.TotalMilliseconds
            });
            _game.RequestClose();
        }
        catch (Exception error)
        {
            WriteReport(new { Status = "failed", Phase = phase, Error = error.ToString(), Checks = _checks,
                Calendar = _game?.CurrentView?.Calendar, Paused = _game?.IsLocallyPaused, Focused = _game?.IsWindowFocused });
            GD.PushError("Native daylight probe failed: " + error);
            GetTree().Quit(1);
        }
    }

    private void CreateFixture()
    {
        int milliseconds = phase switch
        {
            "dawn" => 19_800_000,
            "day" => 43_200_000,
            "dusk" => 66_600_000,
            "night" => 82_800_000,
            "rollover" => 86_391_000,
            _ => throw new ArgumentException("Unknown daylight fixture phase.")
        };
        WorldSnapshot source = GatheringFarmAdmission.CreateEmbedded("aldermere-first-walk-v1", "Traveler");
        var fixture = new WorldSnapshot(source.Inventory, source.Space, source.Residents, source.AdmissionVersion,
            new WorldInstant(0, milliseconds), 0, 0, gathering: source.Gathering);
        using SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(savePath, fixture);
    }

    private async Task WaitFor(Func<bool> predicate, string failure)
    {
        var wait = Stopwatch.StartNew();
        while (!predicate())
        {
            Require(_game?.HasFailed != true && wait.ElapsedMilliseconds < 5_000, failure);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private async Task Capture(string stage)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(Path.ChangeExtension(reportPath, stage + ".png")) == Error.Ok,
            "The native daylight capture was not saved.");
    }

    private async Task CheckNightInterfaces()
    {
        await WaitFor(() => _game!.IsWindowFocused, "The night fixture did not receive focus.");
        PressPause();
        await WaitFor(() => !_game!.IsLocallyPaused, "Escape did not resume night exploration.");
        PressKey(Key.Q);
        await WaitFor(() => _game!.CurrentTarget is not null, "Night exploration did not select a field target.");
        PressPause();
        await WaitFor(() => _game!.IsLocallyPaused, "Escape did not pause the selected night target.");
        await Task.Delay(250);
        CanvasItem marker = _game!.Daylight.GetParent().GetNode<CanvasItem>("TargetMarker");
        Require(marker.Visible && marker.Material is CanvasItemMaterial
            { LightMode: CanvasItemMaterial.LightModeEnum.Unshaded }, "The selected marker is not an unshaded interface cue.");
        Color light = _game.Daylight.Color;
        string world = JsonSerializer.Serialize(_game.CurrentView);
        await Capture("target");
        PressKey(Key.I);
        await WaitFor(() => _game.IsInventoryOpen, "The night pack did not open through its mapped input.");
        await Capture("pack");
        PressKey(Key.I);
        await WaitFor(() => !_game.IsInventoryOpen, "The night pack did not close through its mapped input.");
        Require(_game.IsLocallyPaused && _game.Daylight.Color == light
            && JsonSerializer.Serialize(_game.CurrentView) == world,
            "Night pack inspection changed the paused world or its light.");
        _checks.Add("Real night target selection uses an unshaded marker; pack inspection preserves paused world and light.");
    }

    private static void PressPause() => PressKey(Key.Escape);

    private static void PressKey(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
    }

    private void WriteReport(object report) => File.WriteAllText(reportPath,
        JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");

    private static float[] Channels(Color color) => [color.R, color.G, color.B, color.A];

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
