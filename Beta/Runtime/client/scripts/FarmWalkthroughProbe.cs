using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Godot;
using OpenFantasy.Client.Presentation.Audio;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Retained native integration probe: uses the real input adapter and public projection.</summary>
internal partial class FarmWalkthroughProbe : Node
{
    private readonly FarmGame _game;
    private readonly string _reportPath;
    private readonly bool _reopen;
    private readonly Stopwatch _timeout = Stopwatch.StartNew();
    private readonly Stopwatch _stageTime = new();
    private readonly Stopwatch _talkArrival = new();
    private readonly List<string> _checks = [];
    private string? _held;
    private string? _initial;
    private (ulong Player, ulong Target)? _talkRevisions;
    private object? _talkOfferAtPress;
    private int _stage;
    private bool _done;
    private SubunitPosition? _lastPlayerFeet;
    private double _playerPathSubunits;
    private double? _walkedSubunits;
    private Core.EntityId? _playerId;

    internal FarmWalkthroughProbe(FarmGame game, string reportPath, bool reopen)
    {
        _game = game;
        _reportPath = reportPath;
        _reopen = reopen;
    }

    public override void _Process(double delta)
    {
        if (_done) return;
        try
        {
            Require(!_game.HasFailed, "The native game reported a failure.");
            Require(_timeout.Elapsed < TimeSpan.FromSeconds(40), "The native walkthrough timed out.");
            if (_game.CurrentView is not { } view) return;
            _initial ??= Snapshot(view);
            TrackPlayerPath(view);
            if (_reopen)
            {
                using JsonDocument prior = JsonDocument.Parse(File.ReadAllText(_reportPath));
                Require(prior.RootElement.GetProperty("Final").GetString() == Snapshot(view), "Reopened view differs from the paused saved view.");
                _checks.Add("Exact paused public view restored after native process restart.");
                Finish(view);
                return;
            }
            switch (_stage)
            {
                case 0:
                    Require(view.PlayerInventory.Quantity == 8, "The declared finite initial wood was not eight.");
                    _checks.Add("Native host opened source-pinned farm with eight player wood.");
                    _stage = 1;
                    break;
                case 1:
                    Approach(view);
                    break;
                case 2:
                    if (view.Dialogue is not null && !_game.HasPendingAction)
                    {
                        _held = Snapshot(view);
                        _stageTime.Restart();
                        _stage = 3;
                    }
                    break;
                case 3:
                    Require(_held == Snapshot(view), "The addressed dialogue did not hold time and positions.");
                    if (_stageTime.ElapsedMilliseconds >= 500)
                    {
                        _checks.Add("Reading hold kept both characters and calendar unchanged for at least 500 ms.");
                        Capture("dialogue");
                        Press("give_wood");
                        _stage = 4;
                    }
                    break;
                case 4:
                    if (!_game.HasPendingAction && view.PlayerInventory.Quantity == 2)
                    {
                        Require(view.Dialogue is not null, "The handover unexpectedly dismissed dialogue.");
                        _checks.Add("Native give action committed six wood, leaving two, while the conversation stayed held.");
                        Press("talk");
                        _stage = 5;
                    }
                    break;
                case 5:
                    if (view.Dialogue is null && !_game.HasPendingAction)
                    {
                        _checks.Add("Native continue action closed the addressed dialogue.");
                        Press("pause_world");
                        _stageTime.Restart();
                        _stage = 6;
                    }
                    break;
                case 6:
                    if (_stageTime.ElapsedMilliseconds >= 250)
                    {
                        Capture("final");
                        Finish(view);
                    }
                    break;
            }
        }
        catch (Exception error)
        {
            _done = true;
            ReleaseMovement();
            WriteReport("failed", error.Message, _game.CurrentView);
            GD.PushError("Native farm walkthrough failed: " + error);
            GetTree().Quit(1);
        }
    }

    private void Approach(LocalWorldView view)
    {
        LocalActorView player = view.Actors.Single(actor => actor.ActorId == view.PlayerActorId);
        LocalActorView wren = view.Actors.Single(actor => actor.ActorId != view.PlayerActorId);
        long x = wren.Feet.X - player.Feet.X;
        long y = wren.Feet.Y - player.Feet.Y;
        ReleaseMovement();
        if ((Int128)x * x + (Int128)y * y <= 640 * 640)
        {
            // Movement release crosses the physics/host boundary asynchronously. Settle before
            // one deliberate press; a stale action remains a failure, never an automatic retry.
            var revisions = (player.PresenceRevision, wren.PresenceRevision);
            if (_talkRevisions != revisions)
            {
                _talkRevisions = revisions;
                _talkArrival.Restart();
                return;
            }
            if (_talkArrival.ElapsedMilliseconds < 150 || _game.HasPendingAction
                || !_game.CurrentOffers.TryGetValue(FarmSemanticAction.Speak, out FarmActionOffer? offer)
                || !offer.CanExecute || offer.Scope.ExpectedActorRevision != player.PresenceRevision
                || offer.Intent is not SpeakToActorIntent speak || speak.TargetActorId != wren.ActorId
                || speak.ExpectedTargetRevision != wren.PresenceRevision)
            {
                return;
            }
            _talkOfferAtPress = new { offer.Scope.ExpectedActorRevision,
                Target = speak.ExpectedTargetRevision };
            _checks.Add("Named movement input approached Wren through the real Godot adapter.");
            // The approach walks one axis then the other, so the committed path, not the straight line, sets strides.
            _walkedSubunits = _playerPathSubunits;
            Press("talk");
            _stage = 2;
        }
        else
        {
            _talkRevisions = null;
            _talkArrival.Reset();
            string direction = Math.Abs(x) > Math.Abs(y) ? x < 0 ? "walk_left" : "walk_right"
                : y < 0 ? "walk_up" : "walk_down";
            Input.ActionPress(direction);
        }
    }

    private static void Press(string action)
    {
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true, Strength = 1 });
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false, Strength = 0 });
    }

    private static void ReleaseMovement()
    {
        foreach (string action in new[] { "walk_left", "walk_right", "walk_up", "walk_down" }) Input.ActionRelease(action);
    }

    private static string Snapshot(LocalWorldView view) => JsonSerializer.Serialize(new
    {
        World = view.WorldId.ToString(), Day = view.Calendar.Days, Millisecond = view.Calendar.MillisecondsWithinDay,
        Wood = view.PlayerInventory.Quantity, StorageRevision = view.PlayerInventory.Revision,
        Actors = view.Actors.Select(actor => new
        {
            Id = actor.ActorId.ToString(), actor.DisplayName, actor.Feet.X, actor.Feet.Y, actor.Feet.Z, actor.PresenceRevision
        }),
        Dialogue = view.Dialogue?.DialogueId.ToString()
    });

    private void Capture(string suffix)
    {
        if (DisplayServer.GetName() == "headless") return;
        using Image image = GetViewport().GetTexture().GetImage();
        string path = Path.ChangeExtension(_reportPath, "." + suffix + ".png");
        Require(image.SavePng(path) == Error.Ok, "Could not retain the rendered native screenshot.");
    }

    private void Finish(LocalWorldView view)
    {
        RequireSoundscape();
        WriteReport("passed", null, view);
        GD.Print("Native farm walkthrough passed: " + _reportPath);
        _done = true;
        _game.RequestClose();
    }

    private void WriteReport(string status, string? failure, LocalWorldView? view)
    {
        string output = _reopen ? Path.ChangeExtension(_reportPath, ".reopen.json") : _reportPath;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        string json = JsonSerializer.Serialize(new
        {
            Kind = "native-farm-walkthrough-v1", Stage = _reopen ? "reopen" : "create", Status = status,
            Runtime = RuntimeInformation.FrameworkDescription, DisplayDriver = DisplayServer.GetName(),
            Failure = failure, ProbeStage = _stage, _game.IsLocallyPaused, _game.IsWindowFocused,
            _game.HasPendingAction, HasReadingHold = view?.HasReadingHold,
            TalkOfferAtPress = _talkOfferAtPress,
            Initial = _initial, Final = view is null ? null : Snapshot(view), Checks = _checks,
            DurationMilliseconds = _timeout.ElapsedMilliseconds, Soundscape = SoundscapeReport()
        }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(output, json + "\n");
    }

    /// <summary>Sums the player's committed travel between consecutive observed views, as the cue deriver does.</summary>
    private void TrackPlayerPath(LocalWorldView view)
    {
        _playerId ??= view.PlayerActorId;
        SubunitPosition feet = view.Actors.Single(actor => actor.ActorId == view.PlayerActorId).Feet;
        if (_lastPlayerFeet is { } last)
        {
            double dx = feet.X - last.X, dy = feet.Y - last.Y;
            _playerPathSubunits += Math.Sqrt(dx * dx + dy * dy);
        }
        _lastPlayerFeet = feet;
    }

    /// <summary>
    /// Semantic sound obligations of this journey: committed walking derives footsteps, the conversation ducks, every
    /// derived cue still awaits a creator asset, and the paused reopen restores context without replaying a cue.
    /// </summary>
    private void RequireSoundscape()
    {
        SoundscapeDiagnostics sound = _game.SoundscapeDiagnostics;
        foreach (string bus in new[] { "Music", "Ambience", "Effects", "Voice" })
            Require(AudioServer.GetBusIndex(bus) >= 0, $"The native bus layout has no {bus} bus.");
        Require(sound.Visits == 1, $"Expected one listener visit, observed {sound.Visits}.");
        Require(sound.RecentContexts.Count > 0 && sound.RecentContexts[0].Context.Change == SoundscapeContextChange.Restoration,
            "The first sustained context was not a restoration.");
        long derived = Enum.GetValues<SoundCueKind>().Sum(sound.Derived);
        Require(sound.Total(AcousticPlayback.AwaitingCreatorAsset) == derived,
            "A derived cue claimed playback although no creator asset is mapped.");
        if (_reopen)
        {
            Require(derived == 0, $"The paused reopen replayed {derived} transient cues.");
            _checks.Add("Native reopen restored the sustained sound context and replayed no transient cue.");
            return;
        }
        long footsteps = sound.Derived(SoundCueKind.Footstep, _playerId!.Value);
        long strides = (long)Math.Floor((_walkedSubunits ?? 0) / SoundscapeCueDeriver.StrideSubunits);
        Require(strides >= 2, $"The fixture walk of {_walkedSubunits:0} subunits is too short to qualify footsteps.");
        // Frames normally observe one host step each; a delayed frame may merge strides but never adds any.
        // Visible residents who walk legitimately add their own footsteps; only the player's are compared here.
        Require(footsteps >= 1 && footsteps <= strides, $"The player walked {strides} strides but derived {footsteps} footsteps.");
        Require(sound.RecentContexts.Any(record => record.Context.Change == SoundscapeContextChange.Ducking && record.Context.IsDucked),
            "The native conversation did not duck music and ambience.");
        _checks.Add($"Native walking derived {footsteps} footstep cues over {strides} strides; the conversation ducked both beds.");
    }

    private object SoundscapeReport()
    {
        SoundscapeDiagnostics sound = _game.SoundscapeDiagnostics;
        return new
        {
            sound.Visits, WalkedSubunits = _walkedSubunits, PlayerPathSubunits = _playerPathSubunits,
            PlayerFootsteps = _playerId is { } player ? sound.Derived(SoundCueKind.Footstep, player) : 0,
            FootstepSources = sound.RecentCues.Where(record => record.Key.Kind == SoundCueKind.Footstep)
                .GroupBy(record => record.Key.SourceId.ToString()).ToDictionary(group => group.Key, group => group.Count()),
            Derived = Enum.GetValues<SoundCueKind>().ToDictionary(kind => kind.ToString(), sound.Derived),
            Playback = Enum.GetValues<AcousticPlayback>().ToDictionary(playback => playback.ToString(), sound.Total),
            Contexts = sound.RecentContexts.Select(record => new
            {
                Score = record.Context.Score.ToString(), Ambience = record.Context.Ambience.ToString(),
                record.Context.IsDucked, Change = record.Context.Change.ToString(), record.Context.FadeSeconds,
                ScoreStatus = record.ScoreStatus.ToString(), AmbienceStatus = record.AmbienceStatus.ToString(),
            }),
            Buses = new[] { "Music", "Ambience", "Effects", "Voice" }.ToDictionary(bus => bus, bus => AudioServer.GetBusIndex(bus)),
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
