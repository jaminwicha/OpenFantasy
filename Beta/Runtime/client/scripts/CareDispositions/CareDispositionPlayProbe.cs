using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>
/// Disposable native proof beginning at authenticated pending care. Inspection and input use the normal wheel;
/// the submission observer only records the command and receipt that the client actually sends.
/// </summary>
internal sealed partial class CareDispositionPlayProbe : Node
{
    private const double OpeningLimitSeconds = 30;
    private const double OverallLimitSeconds = 150;
    private readonly FarmGame _game;
    private readonly string _reportPath;
    private readonly string _mode;
    private readonly EntityId _patient;
    private readonly EntityId _helper;
    private readonly EntityId _request;
    private readonly bool _background;
    private readonly bool _startupNoFocus;
    private readonly MarketProbeDriver _driver;
    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private readonly List<FarmSubmission> _submissions = [];
    private readonly List<string> _checks = [];
    private readonly List<string> _images = [];
    private readonly List<object> _wheelBounds = [];
    private readonly List<object> _hudBounds = [];
    private bool _overflow;
    private bool _observedNativeFocus;
    private bool _observedHealthCue;
    private string _stage = "opening";
    private string? _initialHash;
    private string? _selectedCommandBytes;
    private double _openingSeconds;

    internal CareDispositionPlayProbe(FarmGame game, string reportPath, string mode,
        EntityId patient, EntityId helper, EntityId request, bool background, bool startupNoFocus, bool initiallyFocused)
    {
        _game = game;
        _reportPath = reportPath;
        _mode = mode;
        _patient = patient;
        _helper = helper;
        _request = request;
        _background = background;
        _startupNoFocus = startupNoFocus;
        _observedNativeFocus = initiallyFocused;
        _driver = new(this, game, mode.EndsWith("controller", StringComparison.Ordinal) ? "controller"
            : mode.EndsWith("pointer", StringComparison.Ordinal) ? "pointer" : "keyboard", () => _stage, OverallLimitSeconds);
    }

    internal static bool AcceptsMode(string mode) => mode is "cancel-keyboard" or "cancel-pointer" or "decline-controller"
        or "reopen-cancel-keyboard" or "reopen-cancel-pointer" or "reopen-decline-controller";

    private bool IsReopen => _mode.StartsWith("reopen-", StringComparison.Ordinal);
    private bool IsDecline => _mode.Contains("decline", StringComparison.Ordinal);
    private FarmSemanticAction Action => IsDecline ? FarmSemanticAction.DeclineCare : FarmSemanticAction.CancelCare;
    private LocalCareStatus ExpectedStatus => IsDecline ? LocalCareStatus.Declined : LocalCareStatus.Cancelled;
    private LocalCareClosureKind ExpectedClosure => IsDecline ? LocalCareClosureKind.HelperDeclined : LocalCareClosureKind.PatientCancelled;
    private LocalWorldView View => _game.CurrentView ?? throw new InvalidOperationException("No public view.");
    private FarmQuickWheel Wheel => _game.GetNode<FarmQuickWheel>("FarmQuickWheel");
    private FarmHud Hud => _game.GetNode<FarmHud>("FarmHud");
    private int PatientHp => View.EncounterActors.Single(actor => actor.ActorId == _patient).CurrentHp;

    public override void _Process(double delta)
    {
        if (DisplayServer.GetName() != "headless" && GetWindow().HasFocus()) _observedNativeFocus = true;
        if (_game.CurrentView is not null && _game.Canvas.GetNodeOrNull<Label>("Actors/Actor_" + _patient + "/HealthDelta")
            is { Visible: true }) _observedHealthCue = true;
    }

    public override async void _Ready()
    {
        try
        {
            _game.ObserveSubmissions(submission =>
            {
                if (_submissions.Count >= 2) _overflow = true;
                else _submissions.Add(submission);
            });
            await _driver.Until(() => _game.CurrentView?.SupportsCareDispositions == true && _game.Canvas.Regional is not null,
                "The profile 38 regional fixture did not open.", OpeningLimitSeconds);
            _openingSeconds = _watch.Elapsed.TotalSeconds;
            Require(_game.IsLocallyPaused && View.PlayerActorId == (IsDecline ? _helper : _patient),
                "The fixture did not open paused for the named human participant.");
            Require(View.Actors.Single(actor => actor.ActorId == _patient).ControllerKind == ResidentControllerKind.Player
                && View.Actors.Single(actor => actor.ActorId == _helper).ControllerKind == ResidentControllerKind.Player,
                "The fixture participants are not both admitted players.");
            Focus(true);
            _initialHash = ViewHash();
            AssertUntreatedOutcome();
            if (IsReopen) await Reopen();
            else await Decide();
            Require(_game.IsLocallyPaused && !_game.IsWheelOpen && !_game.HasPendingAction,
                "The route did not finish paused with no draft or pending action.");
            Require(!_overflow && _submissions.Count == (IsReopen ? 0 : 1)
                && _game.SubmissionObservationFailure is null, "The route contains an unaccounted submission or observer failure.");
            Require(!_observedHealthCue, "A disposition displayed a health restoration cue.");
            if (_background) Require(_startupNoFocus && GetWindow().Unfocusable && !_observedNativeFocus && !GetWindow().HasFocus(),
                "The background probe took native window focus.");
            Report("passed", null);
            _game.RequestClose();
        }
        catch (Exception error)
        {
            Report("failed", error.ToString());
            GD.PushError("Native care disposition probe failed: " + error);
            GetTree().Quit(1);
        }
    }

    private async Task Reopen()
    {
        Stage("reopen");
        AssertClosure();
        await _driver.Delay(0.5);
        Focus(false);
        await _driver.Delay(0.3);
        Focus(true);
        await _driver.Frame();
        Require(_game.IsLocallyPaused && ViewHash() == _initialHash && _submissions.Count == 0
            && _game.CurrentTarget is null, "Paused reopening or focus return changed the closure or restored a draft.");
        await CaptureHud("reopen");
        AssertClosureText();
        _checks.Add("Paused reopening preserves the observed closure and sends no command, including through logical focus loss/return.");
    }

    private async Task Decide()
    {
        Stage("pending-inspection");
        AssertPending();
        await CaptureHud("pending");
        await OpenWheel();
        await SelectSector();
        Require(Wheel.GetNode<Button>("%ConfirmChoice").Disabled, "Paused inspection allowed commitment.");
        await CaptureWheel("paused-inspection");
        await CloseWheel();
        AssertUnchangedPending();
        await OpenWheel();
        await SelectSector();
        Focus(false);
        await _driver.Until(() => !_game.IsWheelOpen && !_game.IsWindowFocused, "Focus loss retained the wheel draft.");
        await _driver.Delay(0.3);
        Focus(true);
        await _driver.Frame();
        Require(_game.IsLocallyPaused, "Logical focus return bypassed explicit pause.");
        AssertUnchangedPending();
        _checks.Add("Paused wheel inspection, explicit close and logical focus loss/return send no command and preserve pending care.");

        Stage("running-inspection");
        await TogglePause();
        await _driver.Until(() => !_game.IsLocallyPaused, "Explicit readiness did not unpause the participant.");
        await OpenWheel();
        await SelectSector();
        await CloseWheel();
        AssertUnchangedPending();
        await OpenWheel();
        await SelectSector();
        FarmActionOffer selected = CurrentSelection();
        string held = ViewHash();
        await CaptureWheel("selected");
        await _driver.Delay(0.6);
        Require(ViewHash() == held && SameChoice(selected, CurrentSelection()) && _submissions.Count == 0,
            "The inspected choice changed or committed before confirmation.");
        Require(!Wheel.GetNode<Button>("%ConfirmChoice").Disabled, "The ready selected choice is disabled.");
        _checks.Add("The exact ready care choice survives a rendered frame and a 0.6-second read without submitting.");

        Stage("confirm");
        if (_driver.Route == "pointer") await _driver.Click(Wheel.GetNode<Button>("%ConfirmChoice"));
        else if (_driver.Route == "controller") _driver.Joy(JoyButton.A);
        else _driver.Tap(Key.Enter);
        await _driver.Until(() => _submissions.Count != 0 || _overflow, "Native wheel confirmation submitted no command.", 10);
        await _driver.Frame();
        Require(!_overflow && _submissions.Count == 1, "Native confirmation submitted more than once.");
        FarmSubmission submission = _submissions[0];
        byte[] expected = WorldActionCodec.Encode(selected.ToCommand(submission.CommandId));
        _selectedCommandBytes = Convert.ToHexString(expected);
        Require(!submission.IsRetry && SameChoice(selected, submission.Offer)
            && expected.AsSpan().SequenceEqual(submission.CanonicalCommandBytes()),
            "The submitted command is not the exact pre-confirmation wheel choice.");
        await _driver.Until(() => submission.Receipt.IsCompleted, "The care decision received no receipt.", 30);
        Require(submission.Receipt.IsCompletedSuccessfully && submission.Receipt.Result.Status == CommandReceiptStatus.Applied,
            "The care decision did not receive an applied receipt.");
        await _driver.Until(() => View.Care?.Status == ExpectedStatus && !_game.HasPendingAction && !_game.IsWheelOpen,
            "The applied receipt did not publish its closed request.");
        AssertClosure();
        Stage("closure");
        await TogglePause();
        await _driver.Until(() => _game.IsLocallyPaused, "The closed request did not finish paused.");
        string closed = ViewHash();
        await _driver.Delay(0.5);
        Require(ViewHash() == closed && _submissions.Count == 1, "The closure changed or another command was submitted.");
        await CaptureHud("closed");
        AssertClosureText();
        _checks.Add("One exact canonical disposition command received an applied receipt; 0 HP and the unread encounter remain unchanged.");
    }

    private async Task OpenWheel()
    {
        if (_driver.Route == "pointer") await _driver.Click(Hud.GetNode<Button>("%OpenWheel"));
        else if (_driver.Route == "controller") _driver.Joy(JoyButton.RightStick);
        else _driver.Tap(Key.V);
        await _driver.Until(() => _game.IsWheelOpen && Wheel.Model is not null, "The Actions wheel did not open.");
        await AwaitDraw("wheel layout");
        Require(Wheel.Model!.PageName == "Actions" && Wheel.Model.Selected is null, "Opening the wheel restored a selected draft.");
    }

    private async Task SelectSector()
    {
        int sector = IsDecline ? 2 : 3;
        Require(Wheel.Model!.Slots[sector]?.Action == Action, "The expected care sector is missing.");
        if (_driver.Route == "pointer") await _driver.Point(Wheel.GetNode<Control>("%Disc"), sector);
        else if (_driver.Route == "controller") _driver.Joy(JoyButton.DpadDown);
        else _driver.Tap(Key.Left);
        await _driver.Until(() => Wheel.Model?.Selected?.Action == Action, "Native input did not select the care sector.");
        _ = CurrentSelection();
    }

    private async Task CloseWheel()
    {
        if (_driver.Route == "pointer") await _driver.Click(Wheel.GetNode<Button>("%CloseWheel"));
        else if (_driver.Route == "controller") _driver.Joy(JoyButton.B);
        else _driver.Tap(Key.Escape);
        await _driver.Until(() => !_game.IsWheelOpen, "Closing inspection did not discard the wheel draft.");
    }

    private async Task TogglePause()
    {
        if (_driver.Route == "pointer") await _driver.Click(Hud.GetNode<Button>("%Pause"));
        else if (_driver.Route == "controller") _driver.Joy(JoyButton.Start);
        else _driver.Tap(Key.Escape);
        await _driver.Frame();
    }

    private FarmActionOffer CurrentSelection()
    {
        FarmActionOffer offer = Wheel.Model?.Selected?.Offer ?? throw new InvalidOperationException("No selected care offer.");
        WorldActionIntent? intent = IsDecline ? View.Care?.Decision?.Decline : View.Care?.Decision?.Cancellation;
        Require(offer.CanExecute && offer.Action == Action && offer.Intent == intent && intent is PendingCareDispositionIntent disposition
            && disposition.AcknowledgmentId == _request && offer.Scope.ActorId == View.PlayerActorId
            && offer.Scope.WorldId == View.WorldId && offer.Scope.BranchId == View.BranchId
            && offer.Scope.ExpectedActorRevision == View.Actors.Single(actor => actor.ActorId == View.PlayerActorId).PresenceRevision,
            "The wheel does not retain the current participant's exact request and revisions.");
        return offer;
    }

    private static bool SameChoice(FarmActionOffer first, FarmActionOffer second) => first.Action == second.Action
        && first.Scope == second.Scope && first.Intent == second.Intent && first.TargetId == second.TargetId
        && first.Label == second.Label && first.CommandId == second.CommandId;

    private void AssertPending()
    {
        Require(View.Care is { Status: LocalCareStatus.Pending, Closure: null, Decision: not null } care
            && care.PendingAcknowledgmentId == _request && care.HelperActorId == _helper
            && care.Decision.PatientActorId == _patient, "The fixture does not expose its genuine pending human-helper request.");
        Require(IsDecline ? View.Care!.Decision!.Decline is not null && View.Care.Decision.Cancellation is null
            : View.Care!.Decision!.Cancellation is not null && View.Care.Decision.Decline is null && View.Care.Decision.Treatment is null,
            "The care choices disclose another participant's private decision.");
        AssertUntreatedOutcome();
    }

    private void AssertUnchangedPending()
    {
        AssertPending();
        Require(_submissions.Count == 0 && !_game.HasPendingAction && ViewHash() == _initialHash,
            "Inspection or readiness changed the pending view or submitted a command.");
    }

    private void AssertClosure()
    {
        Require(View.Care is { PendingAcknowledgmentId: null, Decision: null, Closure: { } closure } care
            && care.Status == ExpectedStatus && closure.AcknowledgmentId == _request && closure.PatientActorId == _patient
            && closure.Kind == ExpectedClosure, "The observed closure does not match the requested disposition.");
        Require(!_game.CurrentOffers.ContainsKey(FarmSemanticAction.CancelCare)
            && !_game.CurrentOffers.ContainsKey(FarmSemanticAction.DeclineCare)
            && !_game.CurrentOffers.ContainsKey(FarmSemanticAction.AdministerCare), "Closure retained a pending care choice.");
        AssertUntreatedOutcome();
    }

    private void AssertUntreatedOutcome() => Require(PatientHp == 0 && View.EncounterLifecycle?.Phase == EncounterLifecyclePhase.Outcome
        && View.EncounterOutcome is { IsTerminal: false } && !_observedHealthCue,
        "The care decision healed the patient, completed the encounter or displayed a health restoration cue.");

    private void AssertClosureText()
    {
        string expected = IsDecline ? "declined" : "cancelled";
        Require(TextControls(Hud).OfType<Label>().Any(label => label.Text.Contains(expected, StringComparison.OrdinalIgnoreCase)
            && label.Text.Contains("no treatment", StringComparison.OrdinalIgnoreCase)),
            "The HUD does not visibly distinguish this closure from treatment.");
    }

    private void Focus(bool focused)
    {
        _driver.Inputs.Add(new { Stage = _stage, LogicalFixtureFocus = focused });
        _game.InjectFixtureFocus(focused);
    }

    private string ViewHash() => Convert.ToHexString(SHA256.HashData(PlayerViewCodec.Encode(View)));

    private async Task CaptureHud(string label)
    {
        await Capture(label);
        List<object> controls = [];
        List<string> failures = [];
        Rect2 viewport = GetViewport().GetVisibleRect();
        foreach (string name in new[] { "Header", "Footer" })
        {
            Control panel = Hud.GetNode<Control>("Root/" + name);
            Measure(panel, viewport, controls, failures);
        }
        Rect2 header = Hud.GetNode<Control>("Root/Header").GetGlobalRect();
        Rect2 footer = Hud.GetNode<Control>("Root/Footer").GetGlobalRect();
        Control targetBar = Hud.GetNode<Control>("Root/TargetBar");
        Rect2 targetBounds = targetBar.GetGlobalRect();
        List<object> targetButtons = [];
        if (targetBar.IsVisibleInTree())
        {
            Measure(targetBar, viewport, controls, failures);
            if (!Hud.FieldViewportBounds.Encloses(targetBounds)) failures.Add("TargetBar outside the visible field");
            if (targetBounds.Intersects(header)) failures.Add("TargetBar overlaps Header");
            if (targetBounds.Intersects(footer)) failures.Add("TargetBar overlaps Footer");
            foreach (Button button in TextControls(targetBar).OfType<Button>())
            {
                StyleBox style = button.GetThemeStylebox("normal");
                Vector2 available = button.Size - new Vector2(style.GetContentMargin(Side.Left) + style.GetContentMargin(Side.Right),
                    style.GetContentMargin(Side.Top) + style.GetContentMargin(Side.Bottom));
                Font font = button.GetThemeFont("font");
                int fontSize = button.GetThemeFontSize("font_size");
                string[] lines = button.Text.Split('\n');
                float width = lines.Max(line => font.GetStringSize(line, HorizontalAlignment.Left, -1, fontSize).X);
                float height = font.GetHeight(fontSize) * lines.Length;
                bool complete = width <= available.X + 0.5f && height <= available.Y + 0.5f;
                if (!complete) failures.Add(button.Name + " target control text clipped");
                targetButtons.Add(new { Name = button.Name.ToString(), button.Text, TextWidth = width, TextHeight = height,
                    AvailableWidth = available.X, AvailableHeight = available.Y, Complete = complete });
            }
        }
        Rect2? patient = _game.Canvas.ActorSpriteViewportBounds(_patient);
        Rect2? helper = _game.Canvas.ActorSpriteViewportBounds(_helper);
        if (patient is not { } p || !Hud.ShowsFieldSprite(p)) failures.Add("Patient sprite is not visible in the field");
        if (helper is not { } h || !Hud.ShowsFieldSprite(h)) failures.Add("Helper sprite is not visible in the field");
        _hudBounds.Add(new { Label = label, Viewport = Rect(viewport), Header = Rect(header), Footer = Rect(footer),
            Field = Rect(Hud.FieldViewportBounds), TargetBar = targetBar.IsVisibleInTree() ? Rect(targetBounds) : null,
            TargetButtons = targetButtons, Controls = controls,
            PatientSprite = patient is { } a ? Rect(a) : null, HelperSprite = helper is { } b ? Rect(b) : null, Failures = failures });
        Require(failures.Count == 0, "HUD readability: " + string.Join("; ", failures));
    }

    private async Task CaptureWheel(string label)
    {
        await Capture(label);
        Rect2 viewport = GetViewport().GetVisibleRect();
        Control panel = Wheel.GetNode<Control>("WheelRoot/Panel");
        List<object> controls = [];
        List<string> failures = [];
        Measure(panel, viewport, controls, failures);
        _wheelBounds.Add(new { Label = label, Viewport = Rect(viewport), Panel = Rect(panel.GetGlobalRect()),
            SelectedAction = Wheel.Model?.Selected?.Action.ToString(), Controls = controls, Failures = failures });
        Require(failures.Count == 0, "Wheel readability: " + string.Join("; ", failures));
    }

    private static void Measure(Control panel, Rect2 viewport, List<object> controls, List<string> failures)
    {
        Rect2 bounds = panel.GetGlobalRect();
        if (!viewport.Encloses(bounds)) failures.Add(panel.Name + " outside viewport");
        foreach (Control control in TextControls(panel))
        {
            Rect2 rect = control.GetGlobalRect();
            int? lines = control is Label wrapped ? wrapped.GetLineCount() : null;
            int? visible = control is Label shown ? shown.GetVisibleLineCount() : null;
            float? width = control is Label { AutowrapMode: TextServer.AutowrapMode.Off } single
                ? single.Text.Split('\n').Max(line => single.GetThemeFont("font").GetStringSize(line,
                    HorizontalAlignment.Left, -1, single.GetThemeFontSize("font_size")).X) : null;
            bool complete = (lines is null || visible >= lines) && (width is null || width <= rect.Size.X + 0.5f);
            bool inside = bounds.Encloses(rect) && viewport.Encloses(rect);
            if (!inside || !complete) failures.Add(control.Name + " clipped");
            controls.Add(new { Name = control.Name.ToString(), Panel = panel.Name.ToString(), Rect = Rect(rect),
                Text = control is Label label ? label.Text : ((Button)control).Text, Lines = lines, VisibleLines = visible,
                TextWidth = width, Inside = inside, Complete = complete });
        }
    }

    private async Task Capture(string label)
    {
        await AwaitDraw(label);
        if (DisplayServer.GetName() == "headless") return;
        string imagePath = Path.Combine(Path.GetDirectoryName(_reportPath)!, Path.GetFileNameWithoutExtension(_reportPath) + "." + label + ".png");
        Require(GetViewport().GetTexture().GetImage().SavePng(imagePath) == Error.Ok, "Could not save " + label);
        _images.Add(imagePath);
    }

    /// <summary>Container geometry is usable only after the opened controls have actually been drawn.</summary>
    private async Task AwaitDraw(string label)
    {
        await _driver.Frame();
        if (DisplayServer.GetName() == "headless") return;
        bool drawn = false;
        void Drawn() => drawn = true;
        RenderingServer.FramePostDraw += Drawn;
        try { await _driver.Until(() => drawn, "No rendered frame for " + label, 10); }
        finally { RenderingServer.FramePostDraw -= Drawn; }
    }

    private static IEnumerable<Control> TextControls(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is not Control control || !control.IsVisibleInTree()) continue;
            if (control is Label or Button) yield return control;
            foreach (Control nested in TextControls(control)) yield return nested;
        }
    }

    private void Report(string status, string? failure)
    {
        File.WriteAllText(_reportPath, JsonSerializer.Serialize(new
        {
            Kind = "native-care-disposition-v1", Mode = _mode, Status = status, Failure = failure,
            OpeningSeconds = _openingSeconds, ProbeMilliseconds = _watch.ElapsedMilliseconds,
            Paused = _game.IsLocallyPaused, PatientActorId = _patient.ToString(), HelperActorId = _helper.ToString(),
            RequestId = _request.ToString(), ControlledActorId = _game.CurrentView?.PlayerActorId.ToString(),
            FinalCareStatus = _game.CurrentView?.Care?.Status.ToString(), InitialViewHash = _initialHash,
            FinalViewHash = _game.CurrentView is null ? null : ViewHash(), SelectedCommandBytes = _selectedCommandBytes,
            Submissions = _submissions.Select(value => new { CommandId = value.CommandId.ToString(),
                CommandBytes = Convert.ToHexString(value.CanonicalCommandBytes()),
                ReceiptBytes = value.Receipt.IsCompletedSuccessfully ? Convert.ToHexString(WorldReceiptCodecV1.Encode(value.Receipt.Result)) : null,
                value.IsRetry, IntentType = value.Command.Intent.GetType().Name }),
            Inputs = _driver.Inputs, Images = _images, Checks = _checks, HudBounds = _hudBounds, WheelBounds = _wheelBounds,
            FocusRequests = 0, ObservedNativeFocus = _observedNativeFocus, NativeFocused = GetWindow().HasFocus(),
            Unfocusable = GetWindow().Unfocusable, StartupNoFocus = _startupNoFocus, Display = DisplayServer.GetName(),
            LogicalFocus = _game.IsWindowFocused, ObservedHealthCue = _observedHealthCue, SubmissionOverflow = _overflow,
            FinalPatientHp = _game.CurrentView?.EncounterActors.SingleOrDefault(actor => actor.ActorId == _patient)?.CurrentHp,
            FinalEncounterPhase = _game.CurrentView?.EncounterLifecycle?.Phase.ToString(), Stage = _stage
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void Stage(string stage)
    {
        _stage = stage;
        GD.Print($"Care disposition {_mode} [{stage}] {_watch.Elapsed.TotalSeconds:F3}s");
    }

    private static object Rect(Rect2 bounds) => new { X = bounds.Position.X, Y = bounds.Position.Y, Width = bounds.Size.X, Height = bounds.Size.Y };
    private static void Require(bool condition, string message) => MarketProbeDriver.Require(condition, message);
}
