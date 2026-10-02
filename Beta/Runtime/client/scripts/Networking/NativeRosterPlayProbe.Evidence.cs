using System.Text.Json;
using Godot;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking.ObservationViews;

namespace OpenFantasy.Client.Networking;

internal sealed partial class NativeRosterPlayProbe
{
    private async Task Capture()
    {
        MarketProbeDriver.Require(DisplayServer.GetName() != "headless", "The capture request requires the rendered roster process.");
        bool drawn = false;
        void OnDraw() => drawn = true;
        RenderingServer.FramePostDraw += OnDraw;
        try { await _driver.Until(() => drawn, "The roster frame did not render."); }
        finally { RenderingServer.FramePostDraw -= OnDraw; }
        using Image image = GetViewport().GetTexture().GetImage();
        MarketProbeDriver.Require(image.SavePng(Path.Combine(_directory, $"{_sequence:D4}.png")) == Error.Ok,
            "The rendered roster frame could not be saved.");
    }

    private void Respond(string status, string? failure)
    {
        Directory.CreateDirectory(_directory);
        WriteAtomic(Path.Combine(_directory, $"{_sequence:D4}.response.json"), new
        {
            Sequence = _sequence, Status = status, Failure = failure, Stage = _stage,
            View = _game.CurrentView is { } view ? Convert.ToBase64String(PlayerViewCodec.Encode(view)) : null,
            LocallyPaused = _game.IsLocallyPaused, RunState = _game.ParticipantRunState.ToString(),
            NativeFocused = GetWindow().HasFocus(), ObservedNativeFocus = _nativeFocus,
            InitialFocusGuard = _initialFocusGuard, Submissions = Submissions()
        });
    }

    private object[] Submissions() => _submissions.Select(value => (object)new
    {
        Command = Convert.ToHexString(value.CanonicalCommandBytes()), IsRetry = value.IsRetry,
        Receipt = value.Receipt.IsCompletedSuccessfully
            ? Convert.ToHexString(WorldReceiptCodecV1.Encode(value.Receipt.Result)) : null
    }).ToArray();

    private void WriteReport(string status, string? failure) => WriteAtomic(Path.Combine(_directory, "report.json"), new
    {
        Kind = "native-four-player-roster-v1", Status = status, Failure = failure, Stage = _stage,
        World = _game.CurrentView?.WorldId.ToString(), Actor = _game.CurrentView?.PlayerActorId.ToString(),
        Inputs = _driver.Inputs, Submissions = Submissions(), ElapsedMilliseconds = _driver.ElapsedMilliseconds,
        NativeFocused = GetWindow().HasFocus(), ObservedNativeFocus = _nativeFocus,
        InitialFocusGuard = _initialFocusGuard, Unfocusable = GetWindow().Unfocusable, Display = DisplayServer.GetName()
    });

    private static void WriteAtomic(string path, object value)
    {
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, overwrite: true);
    }
}
