using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Actual named-input gathering and cross-process continuation through the public farm view.</summary>
internal sealed partial class FarmGatheringProbe(FarmGame game, string reportPath, bool reopen) : Node
{
    private enum Stage { Opening, Oak, PauseSettling, PauseHeld, Berry, FinalSettling, Recovering, Regrowing, Done }
    private readonly Stopwatch _timeout = Stopwatch.StartNew();
    private readonly Stopwatch _stageTime = new();
    private readonly List<string> _checks = [];
    private readonly List<object> _readinessChanges = [];
    private (bool Paused, bool Focused)? _lastReadiness;
    private Stage _stage;
    private EntityId _oakId;
    private EntityId _berryId;
    private EntityId _woodDefinition;
    private string? _initial;
    private string? _held;
    private ulong _expectedQuantity;
    private bool _waitingForYield;
    private int _swings;
    private long _lastFocusRequest;
    private int _focusRequests;

    public override void _Process(double delta)
    {
        if (_stage == Stage.Done) return;
        try
        {
            Require(!game.HasFailed, "The native farm reported failure.");
            Require(_timeout.Elapsed < TimeSpan.FromSeconds(110), "The native gathering probe timed out at " + _stage);
            if (game.CurrentView is not { } view) return;
            var readiness = (game.IsLocallyPaused, game.IsWindowFocused);
            if (_lastReadiness != readiness)
            {
                _lastReadiness = readiness;
                _readinessChanges.Add(new { WallMilliseconds = _timeout.ElapsedMilliseconds,
                    game.IsLocallyPaused, game.IsWindowFocused, Calendar = view.Calendar.MillisecondsWithinDay });
            }
            // Rendered automation requires a foreground game, like a human player. Request actual
            // window focus after an interruption without bypassing production readiness or its clock.
            if (!game.IsWindowFocused && !game.IsLocallyPaused && _focusRequests < 8
                && _timeout.ElapsedMilliseconds - _lastFocusRequest >= 2_000)
            {
                _lastFocusRequest = _timeout.ElapsedMilliseconds;
                _focusRequests++;
                GetWindow().GrabFocus();
            }
            switch (_stage)
            {
                case Stage.Opening:
                    Opened(view);
                    break;
                case Stage.Oak:
                    if (Harvest(view, _oakId, 5))
                    {
                        _checks.Add("Five real oak swings issued exactly five wood and depleted the node.");
                        Press("pause_world");
                        _stageTime.Restart();
                        _stage = Stage.PauseSettling;
                    }
                    break;
                case Stage.PauseSettling:
                    if (_stageTime.ElapsedMilliseconds < 200) return;
                    Capture("oak-stump");
                    _held = Snapshot(view);
                    _stageTime.Restart();
                    _stage = Stage.PauseHeld;
                    break;
                case Stage.PauseHeld:
                    Require(Snapshot(view) == _held, "Pause changed clock, effort, plants, actors or inventory.");
                    if (_stageTime.ElapsedMilliseconds < 600) return;
                    _checks.Add("Pause froze depleted resource, calendar, effort and all public positions for600ms.");
                    Press("pause_world");
                    _swings = 0;
                    _stage = Stage.Berry;
                    break;
                case Stage.Berry:
                    if (Harvest(view, _berryId, 4))
                    {
                        Require(view.PlayerInventory.Quantity == 19, "Nine harvests did not preserve the ten starting units.");
                        Require(view.PlayerInventory.Lots.Where(lot => lot.DefinitionId == _woodDefinition)
                            .Aggregate(0UL, (sum, lot) => sum + lot.Quantity) == 13, "Oak did not issue the admitted wood definition.");
                        _checks.Add("Four real sickle swings issued four berries; tool lots remain owned.");
                        Press("pause_world");
                        _stageTime.Restart();
                        _stage = Stage.FinalSettling;
                    }
                    break;
                case Stage.FinalSettling:
                    if (_stageTime.ElapsedMilliseconds >= 250) Finish(view);
                    break;
                case Stage.Recovering:
                    Require(_initial == Snapshot(view), "Paused recovery advanced before resuming.");
                    if (_stageTime.ElapsedMilliseconds < 600) return;
                    _checks.Add("Recovered paused work stayed unchanged for600ms before explicit resume.");
                    Press("pause_world");
                    _stage = Stage.Regrowing;
                    break;
                case Stage.Regrowing:
                    LocalGatherNodeView? oak = view.GatheringNodes.FirstOrDefault(node => node.NodeId == _oakId);
                    if (oak?.Visibility == GatherNodeVisibility.Ready && oak.RemainingSwings == 5)
                    {
                        Require(view.PlayerInventory.Quantity == 19, "Regrowth minted inventory without a gathering action.");
                        _checks.Add("Oak regrew after its remaining committed advancing steps across restart, without issuing new inventory.");
                        Press("pause_world");
                        _stageTime.Restart();
                        _stage = Stage.FinalSettling;
                    }
                    break;
            }
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.ChangeExtension(reportPath, "." + (reopen ? "reopen" : "create") + ".failed.json"),
                JsonSerializer.Serialize(new { Status = "failed", Stage = _stage.ToString(), Error = error.ToString(),
                    game.IsLocallyPaused, game.IsWindowFocused, Readiness = _readinessChanges, FocusRequests = _focusRequests,
                    Current = game.CurrentView is { } current ? Snapshot(current) : null },
                    new JsonSerializerOptions { WriteIndented = true }));
            _stage = Stage.Done;
            ReleaseMovement();
            GD.PushError("Native gathering probe failed: " + error);
            GetTree().Quit(1);
        }
    }

    private void Opened(LocalWorldView view)
    {
        _initial = Snapshot(view);
        Require(view.PlayerGatherEffort.HasValue, "A gathering work profile was not admitted.");
        Require(view.GatheringTools.Count == 2, "The explicitly supplied finite toolkit was not present.");
        if (reopen)
        {
            using JsonDocument prior = JsonDocument.Parse(File.ReadAllText(reportPath));
            Require(prior.RootElement.GetProperty("Final").GetString() == _initial, "Saved gathering projection changed across native processes.");
            _oakId = EntityId.Parse(prior.RootElement.GetProperty("OakId").GetString()!);
            _berryId = EntityId.Parse(prior.RootElement.GetProperty("BerryId").GetString()!);
            _woodDefinition = EntityId.Parse(prior.RootElement.GetProperty("WoodDefinitionId").GetString()!);
            _checks.Add("Exact saved work, plants, custody and public actor view restored in a second native process.");
            _stageTime.Restart();
            _stage = Stage.Recovering;
            return;
        }
        Require(view.PlayerInventory.Quantity == 10, "Initial supply must be eight wood plus two finite tools.");
        _woodDefinition = view.PlayerInventory.Lots.Single(lot => lot.Quantity == 8).DefinitionId;
        _oakId = view.GatheringNodes.Single(node => node.Kind == GatherNodeKind.Oak).NodeId;
        _berryId = view.GatheringNodes.Single(node => node.Kind == GatherNodeKind.Berry).NodeId;
        _checks.Add("Original source-pinned oak and berries plus explicitly supplied tools opened in the native game.");
        _stage = Stage.Oak;
    }

    private bool Harvest(LocalWorldView view, EntityId nodeId, int totalSwings)
    {
        if (_waitingForYield)
        {
            if (game.HasPendingAction) return false;
            Require(view.PlayerInventory.Quantity == _expectedQuantity, "A native gathering action did not issue exactly one unit.");
            _waitingForYield = false;
            _swings++;
        }
        if (_swings == totalSwings) return true;
        LocalGatherNodeView node = view.GatheringNodes.Single(value => value.NodeId == nodeId);
        var feet = view.Actors.Single(actor => actor.ActorId == view.PlayerActorId).Feet;
        long x = node.Feet.X - feet.X;
        long y = node.Feet.Y - feet.Y;
        ReleaseMovement();
        if ((Int128)x * x + (Int128)y * y > 300L * 300)
        {
            Input.ActionPress(Math.Abs(x) > Math.Abs(y) ? x < 0 ? "walk_left" : "walk_right" : y < 0 ? "walk_up" : "walk_down");
            return false;
        }
        if (view.PlayerGatherEffort < 400 || game.HasPendingAction) return false;
        Require(FarmClientSession.NearestGatherNode(view)?.NodeId == nodeId, "Context input selected an unintended resource.");
        _expectedQuantity = checked(view.PlayerInventory.Quantity + 1);
        _waitingForYield = true;
        Press("gather");
        return false;
    }

    private static string Snapshot(LocalWorldView view) => JsonSerializer.Serialize(new
    {
        World = view.WorldId.ToString(), Day = view.Calendar.Days, Millisecond = view.Calendar.MillisecondsWithinDay,
        view.PlayerGatherEffort, view.HasReadingHold, Quantity = view.PlayerInventory.Quantity,
        StorageRevision = view.PlayerInventory.Revision,
        Lots = view.PlayerInventory.Lots.Select(lot => new { Id = lot.LotId.ToString(), Definition = lot.DefinitionId.ToString(), lot.Revision, lot.Quantity }),
        Actors = view.Actors.Select(actor => new { Id = actor.ActorId.ToString(), actor.Feet.X, actor.Feet.Y, actor.Feet.Z, actor.PresenceRevision }),
        Nodes = view.GatheringNodes.Select(node => new { Id = node.NodeId.ToString(), node.Revision, node.RemainingSwings, node.Visibility })
    });

    private void Finish(LocalWorldView view)
    {
        Capture(reopen ? "regrown" : "gathered");
        string output = reopen ? Path.ChangeExtension(reportPath, ".reopen.json") : reportPath;
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            Kind = "native-farm-gathering-v1", Stage = reopen ? "reopen" : "create", Status = "passed",
            Runtime = RuntimeInformation.FrameworkDescription, DisplayDriver = DisplayServer.GetName(),
            Initial = _initial, Final = Snapshot(view), OakId = _oakId.ToString(), BerryId = _berryId.ToString(),
            WoodDefinitionId = _woodDefinition.ToString(), Checks = _checks, Readiness = _readinessChanges,
            FocusRequests = _focusRequests,
            DurationMilliseconds = _timeout.ElapsedMilliseconds
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        _stage = Stage.Done;
        GD.Print("Native gathering passed: " + output);
        game.RequestClose();
    }

    private void Capture(string suffix)
    {
        if (DisplayServer.GetName() == "headless") return;
        using Image image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(Path.ChangeExtension(reportPath, "." + suffix + ".png")) == Error.Ok, "Rendered capture failed.");
    }

    private static void Press(string action)
    {
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true, Strength = 1 });
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
    }

    private static void ReleaseMovement()
    {
        foreach (string action in new[] { "walk_left", "walk_right", "walk_up", "walk_down" }) Input.ActionRelease(action);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
