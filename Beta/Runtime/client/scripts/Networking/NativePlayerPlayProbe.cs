using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client.Networking;

/// <summary>Actual FarmGame input over a remote connection; no authority, database, route advice or world mutation.</summary>
internal sealed partial class NativePlayerPlayProbe : Node
{
    private readonly FarmGame _game;
    private readonly string _report;
    private readonly MarketProbeDriver _driver;
    private readonly List<FarmSubmission> _submissions = [];
    private string _stage = "opening";
    private SubunitPosition? _origin;
    private SubunitPosition? _walked;
    private readonly List<object> _boundaries = [];
    private readonly bool _focusProbe;
    private bool _initialFocusGuard;
    private bool _observedNativeFocus;

    internal NativePlayerPlayProbe(FarmGame game, string report, bool focusProbe)
    {
        _game = game;
        _report = report;
        _focusProbe = focusProbe;
        _driver = new MarketProbeDriver(this, game, "keyboard", () => _stage, 60);
        game.ObserveSubmissions(value => _submissions.Add(value));
    }

    private LocalWorldView View => _game.CurrentView ?? throw new InvalidOperationException("No remote view.");
    private SubunitPosition Feet => View.Actors.Single(actor => actor.ActorId == View.PlayerActorId).Feet;

    public override void _Process(double delta)
    {
        if (DisplayServer.GetName() != "headless") _observedNativeFocus |= GetWindow().HasFocus();
    }

    public override async void _Ready()
    {
        try
        {
            await _driver.Until(() => _game.CurrentView?.Regional is not null && _game.Canvas.Regional is not null,
                "The remote native world did not open.", 30);
            if (_focusProbe)
            {
                _stage = "initial-focus";
                var calendar = View.Calendar;
                MarketProbeDriver.Require(DisplayServer.GetName() != "headless" && GetWindow().Unfocusable
                    && !GetWindow().HasFocus() && !_game.IsWindowFocused && !_game.IsLocallyPaused,
                    "The rendered startup must inherit an unfocused window without a local pause or focus injection.");
                await _driver.Delay(0.55);
                MarketProbeDriver.Require(View.Calendar == calendar && _game.ParticipantRunState == LocalWorldRunState.Paused,
                    "Initial focus did not withhold participant readiness.");
                _initialFocusGuard = true;
            }
            _game.InjectFixtureFocus(true);
            if (_focusProbe) _driver.Tap(Key.Escape);
            MarketProbeDriver.Require(_game.IsLocallyPaused && _game.CargoRouteAdvice is null && _game.CargoStalls.Count == 2,
                "The network fixture must begin paused with supplied presentation and no privileged route.");
            _origin = Feet;
            _stage = "walk";
            _driver.Tap(Key.Escape);
            await _driver.Until(() => !_game.IsLocallyPaused && _game.ParticipantRunState == LocalWorldRunState.Running,
                "Escape did not enable host-reported input.");
            // A small ordinary step proves that held native input reaches the host and comes back as an observation.
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Left, PhysicalKeycode = Key.Left, Pressed = true });
            Input.FlushBufferedEvents();
            try { await _driver.Until(() => Feet != _origin, "Native movement did not return from the host."); }
            finally
            {
                Input.ParseInputEvent(new InputEventKey { Keycode = Key.Left, PhysicalKeycode = Key.Left, Pressed = false });
                Input.FlushBufferedEvents();
            }
            _walked = Feet;
            _driver.Tap(Key.Escape);
            await _driver.Until(() => _game.IsLocallyPaused && _game.ParticipantRunState == LocalWorldRunState.Paused,
                "Escape did not pause host-reported participant input.");
            await _driver.Until(TalkMatchesView, "The displayed Talk offer did not catch up with settled movement.");
            _boundaries.Add(CaptureBoundary());
            if (_focusProbe) await CaptureFrame();
            // Resume, then invoke the actual currently displayed Talk offer only when it is available.
            _stage = "talk";
            _driver.Tap(Key.Escape);
            await _driver.Until(() => _game.ParticipantRunState == LocalWorldRunState.Running && TalkMatchesView(),
                "The retained starting companion is not reachable for Talk.");
            _boundaries.Add(CaptureBoundary());
            _driver.Tap(Key.E);
            await _driver.Until(() => View.Dialogue is not null && !_game.HasPendingAction, "Talk did not return its committed conversation.");
            _stage = "close-conversation";
            await _driver.Until(() => View.Dialogue is { } dialogue
                && _game.CurrentOffers.TryGetValue(FarmSemanticAction.CloseDialogue, out var close) && close.CanExecute
                && close.DialogueId == dialogue.DialogueId
                && close.Intent is CloseDialogueIntent intent && intent.DialogueId == dialogue.DialogueId
                && close.Scope.ExpectedActorRevision == View.Actors.Single(actor => actor.ActorId == View.PlayerActorId).PresenceRevision,
                "The committed conversation has no matching displayed Close offer.");
            _driver.Tap(Key.E);
            await _driver.Until(() => View.Dialogue is null && !_game.HasPendingAction, "Close did not return its committed result.");
            _driver.Tap(Key.Escape);
            await _driver.Until(() => _game.IsLocallyPaused && _game.ParticipantRunState == LocalWorldRunState.Paused,
                "Final host-reported input pause failed.");
            await _driver.Delay(0.25);
            MarketProbeDriver.Require(_submissions.Count == 2 && _submissions.All(value => value.Receipt.IsCompletedSuccessfully
                && value.Receipt.Result.Status == CommandReceiptStatus.Applied), "Expected exactly two native committed actions.");
            MarketProbeDriver.Require(!_focusProbe || !_observedNativeFocus, "The background fixture acquired native focus.");
            _stage = "complete";
            Write("passed", null);
            _game.RequestClose();
        }
        catch (Exception error)
        {
            Write("failed", error.ToString());
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private async Task CaptureFrame()
    {
        bool drawn = false;
        void OnDraw() => drawn = true;
        RenderingServer.FramePostDraw += OnDraw;
        try { await _driver.Until(() => drawn, "The network frame was not drawn."); }
        finally { RenderingServer.FramePostDraw -= OnDraw; }
        using Image image = GetViewport().GetTexture().GetImage();
        MarketProbeDriver.Require(image.SavePng(Path.ChangeExtension(_report, ".png")) == Error.Ok,
            "The network frame could not be saved.");
    }

    private bool TalkMatchesView()
    {
        LocalWorldView view = View;
        LocalActorView actor = view.Actors.Single(value => value.ActorId == view.PlayerActorId);
        return _game.CurrentOffers.TryGetValue(FarmSemanticAction.Speak, out var offer) && offer.CanExecute
            && offer.Scope.ExpectedActorRevision == actor.PresenceRevision
            && _game.Canvas.PresentedFeet(actor.ActorId) == actor.Feet
            && offer.Intent is SpeakToActorIntent intent && intent.TargetActorId == _game.CompanionActorId
            && view.Actors.Any(value => value.ActorId == intent.TargetActorId
                && value.PresenceRevision == intent.ExpectedTargetRevision);
    }

    private object CaptureBoundary() => new
    {
        Stage = _stage, State = _game.ParticipantRunState.ToString(),
        ActorRevision = View.Actors.Single(value => value.ActorId == View.PlayerActorId).PresenceRevision,
        OfferRevision = _game.CurrentOffers.GetValueOrDefault(FarmSemanticAction.Speak)?.Scope.ExpectedActorRevision,
        UnsupportedRegion = _game.IsRegionalViewUnsupported
    };

    private void Write(string status, string? failure)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_report))!);
        var result = new
        {
            Kind = "native-player-connection-v1", Status = status, Failure = failure, Stage = _stage,
            World = _game.CurrentView?.WorldId.ToString(), Actor = _game.CurrentView?.PlayerActorId.ToString(),
            Origin = _origin is { } origin ? new long[] { origin.X, origin.Y, origin.Z } : null,
            Walked = _walked is { } walked ? new long[] { walked.X, walked.Y, walked.Z } : null,
            FinalFeet = _game.CurrentView is null ? null : new long[] { Feet.X, Feet.Y, Feet.Z },
            Inputs = _driver.Inputs, ElapsedMilliseconds = _driver.ElapsedMilliseconds,
            Boundaries = _boundaries, FinalBoundary = _game.CurrentView is null ? null : CaptureBoundary(),
            Submissions = _submissions.Select(value => new
            {
                Command = Convert.ToHexString(WorldActionCodec.Encode(value.Command)),
                Receipt = value.Receipt.IsCompletedSuccessfully ? Convert.ToHexString(WorldReceiptCodecV1.Encode(value.Receipt.Result)) : null
            }).ToArray(),
            NativeFocused = GetWindow().HasFocus(), Unfocusable = GetWindow().Unfocusable,
            InitialFocusGuard = _initialFocusGuard, ObservedNativeFocus = _observedNativeFocus,
            Display = DisplayServer.GetName()
        };
        File.WriteAllText(_report, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }
}
