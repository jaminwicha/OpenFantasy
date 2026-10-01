using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;

namespace OpenFantasy.Client.Cargo;

/// <summary>
/// What a cargo frame actually rendered, for disposable probes: the own actor's settled node transform against its
/// projection, the terrain the regional layer last drew against the frame's observation, and framebuffer pixels at the
/// player's column, its sprite, visible water and empty padding. Window focus is sampled every frame and recorded as
/// observed state; the absence of focus requests is a separate source audit made by the native tool, not a counter.
/// </summary>
internal sealed class CargoRenderedEvidence(FarmGame game)
{
    /// <summary>Frames to wait for interpolation to settle before a sample fails.</summary>
    private const int SettleFrames = 30;
    private bool _observedNativeFocus;
    private bool? _lastFocus;
    private int _focusTransitions;
    private int _framesObserved;

    internal List<object> Samples { get; } = [];

    internal void ObserveFocus(Window window)
    {
        if (DisplayServer.GetName() == "headless") return;
        bool focused = window.HasFocus();
        _observedNativeFocus |= focused;
        if (_lastFocus is { } last && last != focused) _focusTransitions++;
        _lastFocus = focused;
        _framesObserved++;
    }

    /// <summary>Actual window state and observed focus counters; no focus-request count is claimed here.</summary>
    internal object Focus(Window window) => new
    {
        Unfocusable = window.Unfocusable, NativeFocused = window.HasFocus(), ObservedNativeFocus = _observedNativeFocus,
        ObservedFocusTransitions = _focusTransitions, FramesObserved = _framesObserved,
        StartupNoFocus = ProjectSettings.GetSetting("display/window/size/no_focus").AsBool()
    };

    /// <summary>
    /// One rendered sample after the actor settles: exact settled transform, the last drawn terrain shape, revisions and
    /// column choices, then framebuffer pixels when rendering. Every failure names its label.
    /// </summary>
    internal async Task Check(string label, Func<string, Task<Image?>> capture)
    {
        LocalWorldView view = game.CurrentView ?? throw new InvalidOperationException(label + ": no public world.");
        EntityId player = view.PlayerActorId;
        Vector2? transform = null, target = null;
        for (int frame = 0; frame < SettleFrames; frame++)
        {
            await game.ToSignal(game.GetTree(), SceneTree.SignalName.ProcessFrame);
            transform = game.Canvas.ActorTransform(player);
            target = game.Canvas.DrawnActor(player);
            if (transform is { } at && target is { } aim && at.DistanceTo(aim) < 0.01f) break;
        }
        CargoViewProjection projection = game.Canvas.Regional ?? throw new InvalidOperationException(label + ": no cargo projection.");
        SubunitPosition feet = game.Canvas.PresentedFeet(player) ?? throw new InvalidOperationException(label + ": no presented player.");
        CargoCanvasPoint point = projection.Project(feet);
        Require(transform is { } settled && Math.Abs(settled.X - point.X) < 0.01 && Math.Abs(settled.Y - point.Y) < 0.01,
            label + ": the settled actor transform is not at the projection of its presented feet.");
        CargoDrawnTerrain drawn = game.Canvas.DrawnTerrain ?? throw new InvalidOperationException(label + ": no terrain was drawn.");
        Require(drawn.Shape == CargoTerrainView.ShapeOf(projection.Observation)
            && drawn.Revisions.SequenceEqual(projection.Observation.Regions.Select(region => region.Revision)),
            label + ": the last drawn terrain is stale against the presented observation.");
        Require(drawn.ChoiceDigest == game.Canvas.ExpectedTerrainDigest(projection.Observation),
            label + ": a drawn column chose another tile than its observed ground.");
        object? pixels = await SamplePixels(label, projection, point, capture);
        Samples.Add(new { Label = label, Feet = new { feet.X, feet.Y, feet.Z }, Transform = new { transform!.Value.X, transform.Value.Y },
            Projected = new { point.X, point.Y }, Drawn = drawn, Pixels = pixels });
    }

    private async Task<object?> SamplePixels(string label, CargoViewProjection projection, CargoCanvasPoint point,
        Func<string, Task<Image?>> capture)
    {
        if (DisplayServer.GetName() == "headless") return null;
        using Image? image = await capture(label + "-sample");
        if (image is null) return null;
        Transform2D toViewport = game.Canvas.GetGlobalTransformWithCanvas();
        Color? Pixel(Vector2 canvas)
        {
            Vector2 at = toViewport * canvas;
            int x = (int)Math.Floor(at.X), y = (int)Math.Floor(at.Y);
            return x >= 0 && y >= 0 && x < image.GetWidth() && y < image.GetHeight() ? image.GetPixel(x, y) : null;
        }
        Color clear = ProjectSettings.GetSetting("rendering/environment/defaults/default_clear_color").AsColor();
        var column = new Vector2((float)point.X, (float)point.Y);
        CargoCanvasPoint corner = projection.CellCorner((long)Math.Floor(point.X / 32) + CargoViewProjection.OriginCellX,
            (long)Math.Floor(point.Y / 32) + CargoViewProjection.OriginCellY);
        Color ground = Pixel(new Vector2((float)corner.X + 3, (float)corner.Y + 3)) ?? throw new InvalidOperationException(label + ": the player's column is off screen.");
        Color sprite = Pixel(column + new Vector2(0, -20)) ?? throw new InvalidOperationException(label + ": the player's sprite is off screen.");
        Require(!Same(ground, clear), label + ": no terrain was rendered in the player's column.");
        Require(!Same(sprite, ground) && !Same(sprite, clear), label + ": the player's sprite was not rendered over its column.");
        object? water = null;
        foreach (LocalObservedWater wet in projection.Observation.Water)
        {
            CargoCanvasPoint cell = projection.CellCorner(wet.CellX, wet.CellY);
            if (Pixel(new Vector2((float)cell.X + 16, (float)cell.Y + 20)) is not { } shown) continue;
            // The daylight modulation scales every channel alike, so water keeps a bluer balance than the dry ground.
            Require(!Same(shown, clear) && !Same(shown, ground)
                && shown.B / Math.Max(shown.R, 0.01f) > ground.B / Math.Max(ground.R, 0.01f),
                label + ": an observed wet column was not rendered as water.");
            water = new { wet.CellX, wet.CellY, shown.R, shown.G, shown.B };
            break;
        }
        object? padding = null;
        CargoCanvasRect bounds = projection.Bounds;
        foreach (Vector2 outside in new[] { new Vector2((float)bounds.X - 24, (float)point.Y), new Vector2((float)bounds.Right + 24, (float)point.Y),
            new Vector2((float)point.X, (float)bounds.Y - 24), new Vector2((float)point.X, (float)bounds.Bottom + 24) })
        {
            if (Pixel(outside) is not { } empty || !game.CargoFieldViewportBounds.HasPoint(toViewport * outside)) continue;
            Require(Same(empty, clear), label + ": something was drawn outside the observed regions.");
            padding = new { outside.X, outside.Y };
            break;
        }
        return new { Ground = Text(ground), Sprite = Text(sprite), Clear = Text(clear), Water = water, Padding = padding };
    }

    private static bool Same(Color a, Color b) => Math.Abs(a.R - b.R) < 0.02f && Math.Abs(a.G - b.G) < 0.02f && Math.Abs(a.B - b.B) < 0.02f;
    private static string Text(Color color) => color.ToHtml(false);
    private static void Require(bool condition, string message) => MarketProbeDriver.Require(condition, message);
}
