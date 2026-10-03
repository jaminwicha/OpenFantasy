using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking.ObservationViews;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client.Networking;

/// <summary>Disposable coordinator mailbox. It observes views and injects native input; it cannot access authority.</summary>
internal sealed partial class NativeRosterPlayProbe : Node
{
    private readonly FarmGame _game;
    private readonly string _directory;
    private readonly MarketProbeDriver _driver;
    private readonly List<FarmSubmission> _submissions = [];
    private string _stage = "opening";
    private int _sequence;
    private bool _nativeFocus;
    private bool _initialFocusGuard;

    internal NativeRosterPlayProbe(FarmGame game, string directory)
    {
        _game = game;
        _directory = Path.GetFullPath(directory);
        _driver = new MarketProbeDriver(this, game, "roster-keyboard-controller", () => _stage, 240);
        game.ObserveSubmissions(value => _submissions.Add(value));
    }

    private LocalWorldView View => _game.CurrentView ?? throw new InvalidOperationException("No native participant view.");

    public override void _Process(double delta)
    {
        if (DisplayServer.GetName() != "headless") _nativeFocus |= GetWindow().HasFocus();
    }

    public override async void _Ready()
    {
        try
        {
            Directory.CreateDirectory(_directory);
            await _driver.Until(() => _game.CurrentView?.Regional is not null && _game.Canvas.Regional is not null
                && _game.ParticipantRunState == LocalWorldRunState.Paused, "Roster client did not open paused.", 30);
            MarketProbeDriver.Require(_game.IsLocallyPaused && !_game.HasPendingAction && !_game.IsWheelOpen,
                "Every opening and reconnect starts paused without a restored UI draft.");
            _initialFocusGuard = DisplayServer.GetName() == "headless" || GetWindow().Unfocusable && !GetWindow().HasFocus();
            MarketProbeDriver.Require(_initialFocusGuard, "Rendered roster client acquired desktop focus at startup.");
            _game.InjectFixtureFocus(true);
            Respond("ready", null);
            for (_sequence = 1; _sequence <= 512; _sequence++)
            {
                string path = Path.Combine(_directory, $"{_sequence:D4}.request.json");
                await _driver.Until(() => File.Exists(path), "Roster coordinator did not supply its next bounded request.", 45);
                MarketProbeDriver.Require(new FileInfo(path).Length <= 4096, "Roster request exceeds its finite bound.");
                using JsonDocument request = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement value = request.RootElement;
                MarketProbeDriver.Require(value.GetProperty("Sequence").GetInt32() == _sequence, "Mailbox request order differs.");
                _stage = value.GetProperty("Verb").GetString() ?? throw new InvalidDataException("Missing mailbox verb.");
                bool closing = await Execute(value);
                _driver.Deadline();
                MarketProbeDriver.Require(!_nativeFocus, "Disposable roster client acquired native desktop focus.");
                Respond("passed", null);
                if (closing)
                {
                    WriteReport("passed", null);
                    _game.RequestClose();
                    return;
                }
            }
            throw new InvalidOperationException("Roster mailbox exceeded its finite request budget.");
        }
        catch (Exception error)
        {
            ReleaseDirections();
            Respond("failed", error.ToString());
            WriteReport("failed", error.ToString());
            GD.PushError(error.ToString());
            GetTree().Quit(1);
        }
    }

    private async Task<bool> Execute(JsonElement request)
    {
        var requestElapsed = System.Diagnostics.Stopwatch.StartNew();
        switch (_stage)
        {
            case "observe": await _driver.Frame(); break;
            case "pause": await Pause(true); break;
            case "resume": await Pause(false); break;
            case "walk": await Walk(request.GetProperty("Direction").GetString()!); break;
            case "enter": await Enter(EntityId.Parse(request.GetProperty("Target").GetString()!)); break;
            case "wait": await ChooseWait(); break;
            case "strike": await ChooseWheel(FarmSemanticAction.Strike); break;
            case "read-outcome": await ChooseWheel(FarmSemanticAction.ReleaseEncounter); break;
            case "capture": await Capture(); break;
            case "capture-control": await CaptureControl(); break;
            case "control-open": await OpenSelectionPanel(); break;
            case "control-refresh": await RefreshSelectionPanel(); break;
            case "control-close": await CloseSelectionPanel(); break;
            case "control-offer": await OfferControl(RequestActor(request), request.GetProperty("Direction").GetString()!); break;
            case "control-accept": await PermissionControl("accept", RequestActor(request)); break;
            case "control-revoke": await PermissionControl("revoke", RequestActor(request)); break;
            case "control-acquire": await AssignmentControl("acquire", RequestActor(request)); break;
            case "control-select": await SelectControl(RequestActor(request)); break;
            case "control-home": await SelectControl(Selected.Selection.HomeActorId); break;
            case "control-release": await AssignmentControl("release", null); break;
            case "selected-walk": await SelectedWalk(request.GetProperty("Direction").GetString()!); break;
            case "selected-enter": await SelectedEnter(RequestActor(request)); break;
            case "selected-strike": await SelectedWheel(FarmSemanticAction.Strike); break;
            case "selected-wait": await SelectedWheel(FarmSemanticAction.Wait); break;
            case "capture-selection": await CaptureSelection(); break;
            case "try-pending-resume": await TryPendingResume(); break;
            case "focus-return": await FocusReturn(); break;
            case "hold-right": SetRight(true); await _driver.Delay(0.15); break;
            case "release-right": SetRight(false); await _driver.Delay(0.15); break;
            case "close": await Pause(true); return true;
            default: throw new InvalidDataException("Unsupported roster mailbox verb.");
        }
        if (_stage.StartsWith("control-", StringComparison.Ordinal) || _stage.StartsWith("selected-", StringComparison.Ordinal)
            || _stage == "capture-selection")
            MarketProbeDriver.Require(requestElapsed.Elapsed < TimeSpan.FromSeconds(20), "The selected native request exceeded twenty seconds.");
        return false;
    }

    private async Task Pause(bool paused)
    {
        if (_game.CurrentSelection is not null) { await SelectedPause(paused); return; }
        if (_game.IsLocallyPaused != paused) _driver.Tap(Key.Escape);
        await _driver.Until(() => _game.IsLocallyPaused == paused && (paused
            ? _game.ParticipantRunState == LocalWorldRunState.Paused
            : _game.ParticipantRunState is LocalWorldRunState.Running or LocalWorldRunState.Waiting),
            "Native pause/readiness did not settle.");
    }

    private async Task Walk(string direction)
    {
        MarketProbeDriver.Require(!View.IsInEncounter, "Walking request arrived during an encounter.");
        Key key = direction switch { "left" => Key.Left, "right" => Key.Right,
            "up" => Key.Up, "down" => Key.Down, _ => throw new InvalidDataException("Unknown native walking direction.") };
        await Pause(false);
        var origin = View.Actors.Single(value => value.ActorId == View.PlayerActorId).Feet;
        _driver.Inputs.Add(new { Stage = _stage, Direction = direction, Origin = origin });
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        Input.FlushBufferedEvents();
        try
        {
            await _driver.Until(() => View.Actors.Single(value => value.ActorId == View.PlayerActorId).Feet != origin,
                "Native movement did not produce an observed step.");
        }
        finally
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
            Input.FlushBufferedEvents();
        }
        await Pause(true);
        await _driver.Delay(0.3);
    }

    private async Task Enter(EntityId target)
    {
        await Pause(false);
        for (int count = 0; _game.CurrentTarget?.Id != target; count++)
        {
            MarketProbeDriver.Require(count < 24, "Native target cycling did not reach the selected wolf.");
            _driver.Tap(Key.Q);
            await _driver.Frame();
        }
        await _driver.Until(() => !_game.HasPendingAction
            && _game.CurrentOffers.GetValueOrDefault(FarmSemanticAction.EnterEncounter) is { CanExecute: true } offer
            && offer.TargetId == target && offer.Scope.ExpectedActorRevision
                == View.Actors.Single(actor => actor.ActorId == View.PlayerActorId).PresenceRevision,
            "The actual assembled party has no matching presented entry offer.");
        int before = _submissions.Count;
        _driver.Tap(Key.E);
        await Applied(before);
        await _driver.Until(() => View.Encounter is not null && View.Encounter.PlayerIsDue,
            "Native entry did not reach the required-choice boundary.");
    }

    private Task ChooseWait() => ChooseWheel(FarmSemanticAction.Wait);

    private async Task ChooseWheel(FarmSemanticAction action)
    {
        await Pause(false);
        await _driver.Until(() => !_game.HasPendingAction
            && _game.CurrentOffers.GetValueOrDefault(action) is { CanExecute: true } offer && MatchesView(offer),
            "A matching native " + action + " offer is unavailable.");
        int before = _submissions.Count;
        _driver.Tap(Key.V);
        await _driver.Until(() => _game.IsWheelOpen, "Native wheel did not open.");
        var wheel = _game.GetNode<FarmQuickWheel>("FarmQuickWheel");
        int sector = Enumerable.Range(0, 4).Single(index => wheel.Model!.Slots[index]?.Action == action);
        _driver.Joy(new[] { JoyButton.DpadUp, JoyButton.DpadRight, JoyButton.DpadDown, JoyButton.DpadLeft }[sector]);
        await _driver.Until(() => wheel.Model?.Selected?.Action == action, "The requested action was not selected through the wheel.");
        MarketProbeDriver.Require(wheel.Model!.Selected!.Offer.CanExecute && MatchesView(wheel.Model.Selected.Offer),
            "The captured wheel terms no longer match the observed encounter boundary; no action was submitted or retried.");
        _driver.Joy(JoyButton.A);
        await Applied(before);
    }

    private bool MatchesView(FarmActionOffer offer)
    {
        if (offer.Scope.ExpectedActorRevision != View.Actors.Single(actor => actor.ActorId == View.PlayerActorId).PresenceRevision)
            return false;
        return offer.Intent switch
        {
            QueueEncounterWaitIntent wait => View.EncounterActions is { } actions
                && wait.EncounterId == actions.EncounterId && wait.ExpectedTimingRevision == actions.TimingRevision,
            QueueEncounterAbilityIntent ability => View.EncounterActions is { } actions
                && ability.EncounterId == actions.EncounterId && ability.ExpectedTimingRevision == actions.TimingRevision,
            AcknowledgeEncounterOutcomeIntent reading => reading == View.EncounterOutcome?.Acknowledgment,
            _ => false
        };
    }

    private async Task Applied(int previous)
    {
        await _driver.Until(() => _submissions.Count == previous + 1 && _submissions[^1].Receipt.IsCompleted
            && !_game.HasPendingAction, "The single native action did not return a receipt.");
        FarmSubmission submission = _submissions[^1];
        MarketProbeDriver.Require(!submission.IsRetry && submission.Receipt.IsCompletedSuccessfully
            && submission.Receipt.Result.Status == CommandReceiptStatus.Applied,
            "Native action failed without retry: " + (submission.Receipt.IsCompletedSuccessfully
                ? submission.Receipt.Result.Rejection.ToString() : submission.Receipt.Exception?.ToString()));
    }

    private static void ReleaseDirections()
    {
        foreach (string action in new[] { FarmInputActions.WalkLeft, FarmInputActions.WalkRight,
            FarmInputActions.WalkUp, FarmInputActions.WalkDown }) Input.ActionRelease(action);
    }
}
