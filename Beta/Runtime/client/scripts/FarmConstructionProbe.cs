using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Native named-input qualification of resumed work, a reading hold and installed material collision.</summary>
internal sealed partial class FarmConstructionProbe(FarmGame game, string reportPath, string mode) : Node
{
    private enum Stage
    {
        Opening, Recovering, Resuming, WatchingWork, Talking, Reading, ClosingDialogue,
        PartialWork, FinishingWork, RoutingEast, Colliding, RoutingAround, Settling, Paused, Done
    }

    private readonly Stopwatch _timeout = Stopwatch.StartNew();
    private readonly Stopwatch _stageTime = new();
    private readonly List<string> _checks = [];
    private readonly List<object> _inputs = [];
    private readonly List<object> _readiness = [];
    private readonly Dictionary<string, string> _observations = [];
    private Stage _stage;
    private (bool Paused, bool Focused)? _lastReadiness;
    private string? _movement;
    private string? _initial;
    private string? _held;
    private EntityId _builder;
    private SubunitPosition _builderOrigin;
    private long _lastFocusRequest;
    private int _focusRequests;
    private int _waypoint;
    private bool _routeSettling;
    private long _routeStopAt;
    private WorldInstant _collisionStarted;
    private long _collisionReadyMilliseconds;
    private long _lastFrameMilliseconds;
    private long _readingMilliseconds;
    private long _initialPausedMilliseconds;
    private long _finalPausedMilliseconds;
    private bool _releaseInitialConversation;

    public override void _Process(double delta)
    {
        if (_stage == Stage.Done) return;
        try
        {
            Require(!game.HasFailed, "The native farm reported failure.");
            Require(_timeout.Elapsed < TimeSpan.FromSeconds(120), "Construction probe timed out at " + _stage);
            if (game.CurrentView is not { } view) return;
            long now = _timeout.ElapsedMilliseconds;
            long elapsed = _lastFrameMilliseconds == 0 ? 0 : now - _lastFrameMilliseconds;
            _lastFrameMilliseconds = now;
            ObserveReadiness(view);
            switch (_stage)
            {
                case Stage.Opening: Opened(view); break;
                case Stage.Recovering: ObserveRecovery(view); break;
                case Stage.Resuming:
                    if (ReadyForInput)
                    {
                        if (mode == "fresh")
                        {
                            _releaseInitialConversation = true;
                            Press("talk");
                            Enter(Stage.ClosingDialogue);
                        }
                        else Enter(mode == "upgrade" ? Stage.WatchingWork : Stage.FinishingWork);
                    }
                    break;
                case Stage.WatchingWork: WatchWork(view); break;
                case Stage.Talking: ObserveTalk(view); break;
                case Stage.Reading: ObserveReading(view); break;
                case Stage.ClosingDialogue: ObserveClose(view); break;
                case Stage.PartialWork: ObservePartial(view); break;
                case Stage.FinishingWork: ObserveCompletion(view); break;
                case Stage.RoutingEast:
                    if (Route(view, [new(26880, 82944, 256), new(27520, 82944, 256), new(27520, 83072, 256)]))
                    {
                        _observations.Add("BeforeCollision", Snapshot(view));
                        _collisionStarted = view.Calendar;
                        Enter(Stage.Colliding);
                    }
                    break;
                case Stage.Colliding: ObserveCollision(view, elapsed); break;
                case Stage.RoutingAround:
                    if (Route(view, [new(27424, 82944, 256), new(27072, 82944, 256)]))
                    {
                        Require(Player(view).Feet.X < 27123 && Player(view).Feet.Y == 82944,
                            "The player did not walk around the north of the installed guard.");
                        _observations.Add("AfterWalkAround", Snapshot(view));
                        _checks.Add("The same player walked north and past the west side of the installed guard through ordinary movement.");
                        BeginPause();
                    }
                    break;
                case Stage.Settling: SettlePause(view); break;
                case Stage.Paused: ObservePause(view); break;
            }
        }
        catch (Exception error) { Fail(error); }
    }

    private bool ReadyForInput => !game.IsLocallyPaused && game.IsWindowFocused && !game.HasPendingAction;

    private void Opened(LocalWorldView view)
    {
        Require(mode is "upgrade" or "fresh" or "finish" or "reopen", "Unknown native construction mode.");
        Require(game.IsLocallyPaused && (mode == "fresh" ? view.HasReadingHold && view.Dialogue is not null
                : !view.HasReadingHold && view.Dialogue is null),
            "Construction recovery requires the exact retained conversation state of its paused stage.");
        LocalConstructionView project = Project(view);
        Require(project.RequiredWork == 200 && project.SiteFeet == new SubunitPosition(27213, 83072, 256)
            && project.WorkFeet == new SubunitPosition(27264, 83328, 256), "Unexpected admitted construction calibration.");
        _builder = project.BuilderActorId;
        _builderOrigin = Builder(view).Feet;
        _initial = Snapshot(view);
        using JsonDocument source = JsonDocument.Parse(File.ReadAllText(Output("source", "json")));
        string sourceMode = source.RootElement.GetProperty("Mode").GetString()!;
        Require(sourceMode is "fresh" or "reopen", "Unknown source journey mode.");
        Require(mode != "fresh" || sourceMode == "fresh", "Fresh construction requires its actual fresh quest journey.");
        Require(mode != "upgrade" || sourceMode == "reopen", "Copied construction requires its actual retained quest journey.");
        string previousMode = mode switch
        {
            "upgrade" or "fresh" => "source", "finish" => sourceMode == "fresh" ? "fresh" : "upgrade", _ => "finish"
        };
        using JsonDocument previous = JsonDocument.Parse(File.ReadAllText(Output(previousMode, "json")));
        Require(previous.RootElement.GetProperty("Status").GetString() == "passed", "The prior native stage did not pass.");
        string before = previous.RootElement.GetProperty("Final").GetString()!;
        if (mode is "upgrade" or "fresh")
        {
            Require(FarmQuestProbe.Snapshot(view) == before,
                "Opening construction changed the retained quest, production or public world state.");
            Require(project.CompletedWork == 0 && !project.IsCompleted, "Construction performed work before explicit resume.");
            _checks.Add(mode == "upgrade" ? "Copied admission retained every prior public quest and production field and introduced exactly zero work."
                : "The actual fresh quest journey recovered exactly with its completed reward and zero construction work.");
        }
        else
        {
            Require(before == _initial, "The exact prior public construction state did not restore in this process.");
            Require(mode == "finish" ? project.CompletedWork is >= 40 and < 200 && !project.IsCompleted
                : project.CompletedWork == 200 && project.IsCompleted, "Unexpected resumed construction phase.");
            _checks.Add("The exact previous public world, work and quest state restored before any resume.");
        }
        Enter(Stage.Recovering);
    }

    private void ObserveRecovery(LocalWorldView view)
    {
        Require(game.IsLocallyPaused && Snapshot(view) == _initial, "Paused recovery changed before explicit resume.");
        if (_stageTime.ElapsedMilliseconds < 600) return;
        _initialPausedMilliseconds = _stageTime.ElapsedMilliseconds;
        _checks.Add("The recovered state remained exactly unchanged for at least 600 ms while paused.");
        if (mode == "reopen")
        {
            _held = _initial;
            Enter(Stage.Paused);
            return;
        }
        Press("pause_world");
        Enter(Stage.Resuming);
    }

    private void WatchWork(LocalWorldView view)
    {
        LocalConstructionView project = Project(view);
        Require(project.CompletedWork < 160, "The probe missed the live work interval before conversation.");
        if (!ReadyForInput) return;
        SubunitPosition player = Player(view).Feet;
        // Stand west of the material footprint; the player's body must not obstruct Wren's work.
        if (player.X < 26880) { SetMovement("walk_right"); return; }
        SetMovement(null);
        if (project.CompletedWork < 8) return;
        Require(Builder(view).Feet == project.WorkFeet && Builder(view).Feet != _builderOrigin,
            "Wren did not actually route from the retained location to the work position.");
        long dx = player.X - project.WorkFeet.X;
        long dy = player.Y - project.WorkFeet.Y;
        Require((Int128)dx * dx + (Int128)dy * dy <= 600L * 600,
            "The observing player is outside normal conversation range.");
        _observations.Add("BeforeTalk", Snapshot(view));
        _checks.Add("Wren moved from the retained source position and performed committed work at the admitted work site.");
        Press("talk");
        Enter(Stage.Talking);
    }

    private void ObserveTalk(LocalWorldView view)
    {
        if (game.HasPendingAction || view.Dialogue is null)
        {
            Require(_stageTime.ElapsedMilliseconds < 5_000, "Ordinary E did not open the working NPC's conversation.");
            return;
        }
        Require(view.HasReadingHold && view.Dialogue.SpeakerActorId == _builder,
            "The committed conversation did not hold the working builder.");
        _held = Snapshot(view);
        _observations.Add("ReadingStart", _held);
        Enter(Stage.Reading);
    }

    private void ObserveReading(LocalWorldView view)
    {
        Require(Snapshot(view) == _held && view.HasReadingHold,
            "Reading allowed work, movement, time or another public field to advance.");
        if (_stageTime.ElapsedMilliseconds < 600 || !ReadyForInput) return;
        _readingMilliseconds = _stageTime.ElapsedMilliseconds;
        _observations.Add("ReadingEnd", Snapshot(view));
        _checks.Add("Ordinary E held actual construction work and the entire public world for at least 600 ms.");
        Press("talk");
        Enter(Stage.ClosingDialogue);
    }

    private void ObserveClose(LocalWorldView view)
    {
        if (game.HasPendingAction || view.Dialogue is not null)
        {
            Require(_stageTime.ElapsedMilliseconds < 5_000, "Ordinary E did not release the work conversation.");
            return;
        }
        Require(!view.HasReadingHold, "Closed dialogue retained the reading hold.");
        Enter(_releaseInitialConversation ? Stage.WatchingWork : Stage.PartialWork);
        _releaseInitialConversation = false;
    }

    private void ObservePartial(LocalWorldView view)
    {
        Require(Project(view).CompletedWork < 200, "The partial-save stage missed the unfinished job.");
        if (!ReadyForInput || Project(view).CompletedWork < 60) return;
        _checks.Add("After E closed the conversation, the same unfinished project resumed before an ordinary pause.");
        BeginPause();
    }

    private void ObserveCompletion(LocalWorldView view)
    {
        if (!Project(view).IsCompleted) return;
        Require(Project(view).CompletedWork == 200, "The installed structure has an incorrect work count.");
        if (!ReadyForInput) return;
        _observations.Add("Completed", Snapshot(view));
        _checks.Add("The retained partial job resumed to exactly 200 work and became an installed structure.");
        _waypoint = 0;
        Enter(Stage.RoutingEast);
    }

    private bool Route(LocalWorldView view, SubunitPosition[] waypoints)
    {
        if (!ReadyForInput) return false;
        if (_routeSettling)
        {
            if (_timeout.ElapsedMilliseconds - _routeStopAt < 200) return false;
            _routeSettling = false;
            if (Player(view).Feet == waypoints[_waypoint]) _waypoint++;
        }
        if (_waypoint == waypoints.Length) return true;
        SubunitPosition current = Player(view).Feet;
        SubunitPosition target = waypoints[_waypoint];
        if (current == target)
        {
            SetMovement(null);
            _routeSettling = true;
            _routeStopAt = _timeout.ElapsedMilliseconds;
            return false;
        }
        // All waypoints preserve the source's 32-subunit movement lattice.
        Require((target.X - current.X) % 32 == 0 && (target.Y - current.Y) % 32 == 0,
            "The retained player is not aligned with the actual fixed movement input.");
        SetMovement(current.Y != target.Y ? current.Y < target.Y ? "walk_down" : "walk_up"
            : current.X < target.X ? "walk_right" : "walk_left");
        return false;
    }

    private void ObserveCollision(LocalWorldView view, long elapsed)
    {
        if (!ReadyForInput) return;
        Require(Project(view).IsCompleted && Player(view).Feet.Y == 83072,
            "Collision attempt left the admitted guard approach.");
        Require(Player(view).Feet.X >= 27424, "The player's body crossed the completed guard.");
        if (_movement == "walk_left") _collisionReadyMilliseconds += elapsed;
        SetMovement("walk_left");
        if (_collisionReadyMilliseconds < 900) return;
        Require(Player(view).Feet.X == 27424 && view.Calendar > _collisionStarted,
            "The guard did not stop ordinary west movement at its actual body-clearance boundary while time advanced.");
        Require(view.Actors.Where(actor => actor.ActorId != view.PlayerActorId)
            .All(actor => Math.Abs(actor.Feet.Y - 83072) >= 140 || actor.Feet.X <= 27212 || actor.Feet.X >= 27604),
            "Another actor could explain the measured construction collision.");
        _observations.Add("BlockedByGuard", Snapshot(view));
        _checks.Add("At least 900 ms of focused west input stopped at X27424 against the installed material, with advancing calendar.");
        SetMovement(null);
        _waypoint = 0;
        _routeSettling = false;
        Enter(Stage.RoutingAround);
    }

    private void BeginPause()
    {
        SetMovement(null);
        Require(!game.IsLocallyPaused, "The probe tried to invert an existing pause.");
        Press("pause_world");
        Enter(Stage.Settling);
    }

    private void SettlePause(LocalWorldView view)
    {
        Require(game.IsLocallyPaused, "The normal pause input was not applied.");
        if (_stageTime.ElapsedMilliseconds < 250 || game.HasPendingAction) return;
        Require(mode is not ("upgrade" or "fresh") || Project(view).CompletedWork is >= 40 and < 200,
            "The retained partial state is outside its required work interval.");
        _held = Snapshot(view);
        Enter(Stage.Paused);
    }

    private void ObservePause(LocalWorldView view)
    {
        Require(game.IsLocallyPaused && Snapshot(view) == _held, "The final paused public construction state changed.");
        if (_stageTime.ElapsedMilliseconds < 600) return;
        _finalPausedMilliseconds = _stageTime.ElapsedMilliseconds;
        _checks.Add("The final saved state remained exactly unchanged for at least 600 ms under the normal pause control.");
        Finish(view);
    }

    private void ObserveReadiness(LocalWorldView view)
    {
        var readiness = (game.IsLocallyPaused, game.IsWindowFocused);
        if (_lastReadiness != readiness)
        {
            _lastReadiness = readiness;
            _readiness.Add(new { WallMilliseconds = _timeout.ElapsedMilliseconds, game.IsLocallyPaused,
                game.IsWindowFocused, Day = view.Calendar.Days, Millisecond = view.Calendar.MillisecondsWithinDay });
        }
        if (game.IsLocallyPaused || !game.IsWindowFocused) SetMovement(null);
        if (!game.IsWindowFocused && !game.IsLocallyPaused && _focusRequests < 8
            && _timeout.ElapsedMilliseconds - _lastFocusRequest >= 2_000)
        {
            _lastFocusRequest = _timeout.ElapsedMilliseconds;
            _focusRequests++;
            GetWindow().GrabFocus();
        }
    }

    private void SetMovement(string? action)
    {
        if (_movement == action) return;
        if (_movement is not null) Input.ActionRelease(_movement);
        _movement = action;
        if (action is not null) Input.ActionPress(action);
        RecordInput(action ?? "stop_movement");
    }

    private void Press(string action)
    {
        RecordInput(action);
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true, Strength = 1 });
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false, Strength = 0 });
    }

    private void RecordInput(string action) => _inputs.Add(new { Action = action, Stage = _stage.ToString(),
        WallMilliseconds = _timeout.ElapsedMilliseconds, game.IsLocallyPaused, game.IsWindowFocused });
    private void Enter(Stage stage) { _stage = stage; _stageTime.Restart(); }
    private static LocalActorView Player(LocalWorldView view) => view.Actors.Single(actor => actor.ActorId == view.PlayerActorId);
    private LocalActorView Builder(LocalWorldView view) => view.Actors.Single(actor => actor.ActorId == _builder);
    private static LocalConstructionView Project(LocalWorldView view) => view.Construction.Single();

    private static string Snapshot(LocalWorldView view) => JsonSerializer.Serialize(new
    {
        Prior = JsonSerializer.Deserialize<JsonElement>(FarmQuestProbe.Snapshot(view)),
        Construction = view.Construction.Select(project => new
        {
            Id = project.ProjectId.ToString(), Builder = project.BuilderActorId.ToString(), project.DisplayName,
            Site = new { project.SiteFeet.X, project.SiteFeet.Y, project.SiteFeet.Z },
            Work = new { project.WorkFeet.X, project.WorkFeet.Y, project.WorkFeet.Z },
            project.CompletedWork, project.RequiredWork, project.IsCompleted
        })
    });

    private string Output(string stage, string extension) => Path.ChangeExtension(reportPath, "." + stage + "." + extension);

    private void Finish(LocalWorldView view)
    {
        if (DisplayServer.GetName() != "headless")
        {
            using Image image = GetViewport().GetTexture().GetImage();
            Require(image.SavePng(Output(mode, "png")) == Error.Ok, "Native construction capture failed.");
        }
        File.WriteAllText(Output(mode, "json"), JsonSerializer.Serialize(new
        {
            Kind = "native-farm-construction-v1", Mode = mode, Status = "passed", Runtime = RuntimeInformation.FrameworkDescription,
            DisplayDriver = DisplayServer.GetName(), Initial = _initial, Final = Snapshot(view), Observations = _observations,
            Checks = _checks, Inputs = _inputs, Readiness = _readiness, FocusRequests = _focusRequests,
            InitialPausedMilliseconds = _initialPausedMilliseconds, FinalPausedMilliseconds = _finalPausedMilliseconds,
            ReadingMilliseconds = _readingMilliseconds, CollisionReadyMilliseconds = _collisionReadyMilliseconds,
            game.IsLocallyPaused, game.IsWindowFocused, DurationMilliseconds = _timeout.ElapsedMilliseconds
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        _stage = Stage.Done;
        GD.Print("Native construction passed: " + Output(mode, "json"));
        game.RequestClose();
    }

    private void Fail(Exception error)
    {
        SetMovement(null);
        File.WriteAllText(Output(mode, "failed.json"), JsonSerializer.Serialize(new
        {
            Status = "failed", Mode = mode, Stage = _stage.ToString(), Error = error.ToString(),
            Inputs = _inputs, Readiness = _readiness, Observations = _observations, FocusRequests = _focusRequests,
            game.IsLocallyPaused, game.IsWindowFocused, Current = game.CurrentView is { } view ? Snapshot(view) : null
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        _stage = Stage.Done;
        GD.PushError("Native construction probe failed: " + error);
        GetTree().Quit(1);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
