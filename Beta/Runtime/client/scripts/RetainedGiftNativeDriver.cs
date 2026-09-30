using System.Diagnostics;
using Godot;

namespace OpenFantasy.Client;

/// <summary>
/// Bounded native input driver for disposable probes. It injects real input events only and never
/// calls the world. Each wait has its own limit, checked also when its predicate succeeds; the whole
/// journey keeps one fixed bound. Waits and inputs are recorded for the report.
/// </summary>
internal sealed class RetainedGiftNativeDriver
{
    private readonly Node _owner;
    private readonly FarmGame _game;
    private readonly double _journeySeconds;
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();

    internal RetainedGiftNativeDriver(Node owner, FarmGame game, double journeySeconds)
    {
        _owner = owner;
        _game = game;
        _journeySeconds = journeySeconds;
    }

    internal string Stage { get; set; } = "opening";
    internal List<object> Inputs { get; } = [];
    internal List<object> Waits { get; } = [];
    internal long ElapsedMilliseconds => _elapsed.ElapsedMilliseconds;

    internal void Tap(Key key)
    {
        Inputs.Add(new { Stage, Key = key.ToString() });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
        Input.FlushBufferedEvents();
    }

    /// <summary>Waits for a presented condition; a success observed after its limit still fails.</summary>
    internal async Task Until(Func<bool> predicate, string failure, double seconds = 10)
    {
        var watch = Stopwatch.StartNew();
        while (true)
        {
            bool satisfied = predicate();
            Deadline();
            double elapsed = watch.Elapsed.TotalSeconds;
            Require(elapsed < seconds, failure);
            if (satisfied)
            {
                Waits.Add(new { Stage, Condition = failure, Seconds = Math.Round(elapsed, 3), LimitSeconds = seconds });
                return;
            }
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
        await _owner.ToSignal(_owner.GetTree(), SceneTree.SignalName.ProcessFrame);
        Deadline();
    }

    internal void Deadline() => Require(!_game.HasFailed && _elapsed.Elapsed.TotalSeconds < _journeySeconds,
        $"Retained gift journey failed or exceeded its {_journeySeconds}-second bound at {Stage}");

    /// <summary>Saves the presented frame when a renderer exists; headless runs have no image evidence.</summary>
    internal async Task Capture(string path)
    {
        if (DisplayServer.GetName() == "headless") return;
        await _owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = _owner.GetViewport().GetTexture().GetImage();
        Require(image.SavePng(path) == Error.Ok, "Retained gift image could not be saved.");
    }

    internal static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
