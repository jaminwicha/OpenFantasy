using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Native input proof spanning a gathered save, preserved-copy upgrade and crafted continuation.</summary>
internal sealed partial class FarmProductionProbe(FarmGame game, string reportPath, string mode) : Node
{
    private enum Stage { Opening, Recovering, Harvesting, Crafting, Settling, Held, Done }
    private readonly Stopwatch _timeout = Stopwatch.StartNew();
    private readonly Stopwatch _stageTime = new();
    private readonly List<string> _checks = [];
    private Stage _stage;
    private EntityId _berry;
    private string? _initial;
    private string? _held;
    private ulong _expectedQuantity;
    private int _swings;
    private bool _waiting;
    private long _lastFocusRequest;
    private int _focusRequests;
    private ulong _craftStartingRevision;

    public override void _Process(double delta)
    {
        if (_stage == Stage.Done)
        {
            return;
        }
        try
        {
            Require(!game.HasFailed, "The native farm reported failure.");
            Require(_timeout.Elapsed < TimeSpan.FromSeconds(90), "Production probe timed out at " + _stage);
            if (game.CurrentView is not { } view)
            {
                return;
            }
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
                case Stage.Recovering:
                    Require(Snapshot(view) == _initial, "Paused opening changed the retained public view.");
                    if (_stageTime.ElapsedMilliseconds < 600)
                    {
                        return;
                    }
                    _checks.Add("Restored world stayed unchanged for600ms before explicit resume.");
                    Press("pause_world");
                    _stage = mode == "upgrade" ? Stage.Crafting : Stage.Harvesting;
                    if (_stage == Stage.Crafting)
                    {
                        StartCraft(view);
                    }
                    break;
                case Stage.Harvesting:
                    if (Harvest(view))
                    {
                        _checks.Add("Two real sickle actions gathered two finite berries through the ordinary input adapter.");
                        if (mode == "prepare")
                        {
                            BeginPause();
                        }
                        else
                        {
                            _stage = Stage.Crafting;
                            StartCraft(view);
                        }
                    }
                    break;
                case Stage.Crafting:
                    if (game.HasPendingAction)
                    {
                        return;
                    }
                    if (view.Crafting?.Revision == _craftStartingRevision && _stageTime.ElapsedMilliseconds < 2_000)
                    {
                        // Named input may be delivered after this frame; observe its durable revision without resending it.
                        return;
                    }
                    ulong operations = mode == "upgrade" ? 1UL : 2UL;
                    Require(FarmCrafting.Quantity(view, RecipeMaterialRole.Output) == operations * 2,
                        "Crafting did not retain exactly two rations per operation.");
                    Require(FarmCrafting.Quantity(view, RecipeMaterialRole.Waste) == operations,
                        "Crafting lost its recoverable offcut output.");
                    Require(view.Crafting?.Revision == operations, "The native action did not commit exactly once.");
                    Require(view.GatheringTools.Count == 2, "Crafting consumed a tool.");
                    Require(view.PlayerInventory.Quantity == _expectedQuantity, "Crafting changed counted total unexpectedly.");
                    _checks.Add("Named craft input consumed2berries+1wood and retained2rations+1offcut with tools untouched.");
                    BeginPause();
                    break;
                case Stage.Settling:
                    if (_stageTime.ElapsedMilliseconds < 250)
                    {
                        return;
                    }
                    _held = Snapshot(view);
                    _stageTime.Restart();
                    _stage = Stage.Held;
                    break;
                case Stage.Held:
                    Require(_held == Snapshot(view), "Paused crafting state changed.");
                    if (_stageTime.ElapsedMilliseconds >= 600)
                    {
                        Finish(view);
                    }
                    break;
            }
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.ChangeExtension(reportPath, "." + mode + ".failed.json"), JsonSerializer.Serialize(new
            {
                Status = "failed", Mode = mode, Stage = _stage.ToString(), Error = error.ToString(), FocusRequests = _focusRequests,
                game.IsLocallyPaused, game.IsWindowFocused,
                Current = game.CurrentView is { } current ? Snapshot(current) : null
            }, new JsonSerializerOptions { WriteIndented = true }));
            _stage = Stage.Done;
            ReleaseMovement();
            GD.PushError("Native production probe failed: " + error);
            GetTree().Quit(1);
        }
    }

    private void Opened(LocalWorldView view)
    {
        _initial = Snapshot(view);
        _berry = view.GatheringNodes.Single(node => node.Kind == GatherNodeKind.Berry).NodeId;
        Require(view.GatheringTools.Count == 2, "The finite source toolkit was not retained.");
        if (mode == "prepare")
        {
            Require(view.Crafting is null && view.PlayerInventory.Quantity == 10, "Preparation needs the original gathered profile.");
            _stage = Stage.Harvesting;
            return;
        }
        Require(view.Crafting is not null, "The production profile was not opened.");
        string priorMode = mode == "upgrade" ? "prepare" : "upgrade";
        using JsonDocument prior = JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(reportPath, "." + priorMode + ".json")));
        string before = prior.RootElement.GetProperty("Final").GetString()!;
        if (mode == "upgrade")
        {
            using JsonDocument previous = JsonDocument.Parse(before);
            using JsonDocument current = JsonDocument.Parse(_initial);
            Require(previous.RootElement.GetProperty("Retained").GetRawText() == current.RootElement.GetProperty("Retained").GetRawText(),
                "The upgrade lost saved presence, calendar, work, source nodes or actual inventory.");
            _checks.Add("The preserved-copy upgrade retained every recorded projection field without advancing time.");
        }
        else
        {
            Require(before == _initial, "The crafted projection did not restore exactly in a third process.");
            _checks.Add("Exact crafted public state recovered in a third native process.");
        }
        _stageTime.Restart();
        _stage = Stage.Recovering;
    }

    private bool Harvest(LocalWorldView view)
    {
        if (_waiting)
        {
            if (game.HasPendingAction)
            {
                return false;
            }
            Require(view.PlayerInventory.Quantity == _expectedQuantity, "The sickle action did not yield exactly one berry.");
            _waiting = false;
            _swings++;
        }
        if (_swings == 2)
        {
            return true;
        }
        LocalGatherNodeView node = view.GatheringNodes.Single(value => value.NodeId == _berry);
        var feet = view.Actors.Single(actor => actor.ActorId == view.PlayerActorId).Feet;
        long x = node.Feet.X - feet.X;
        long y = node.Feet.Y - feet.Y;
        ReleaseMovement();
        if ((Int128)x * x + (Int128)y * y > 300L * 300)
        {
            Input.ActionPress(Math.Abs(x) > Math.Abs(y) ? x < 0 ? "walk_left" : "walk_right" : y < 0 ? "walk_up" : "walk_down");
            return false;
        }
        if (view.PlayerGatherEffort < 400 || game.HasPendingAction)
        {
            return false;
        }
        Require(FarmClientSession.NearestGatherNode(view)?.NodeId == _berry, "Context selected a different resource.");
        _expectedQuantity = checked(view.PlayerInventory.Quantity + 1);
        _waiting = true;
        Press("gather");
        return false;
    }

    private void StartCraft(LocalWorldView view)
    {
        Require(FarmCrafting.HasIngredients(view), "The retained gathered ingredients are unavailable.");
        _expectedQuantity = view.PlayerInventory.Quantity;
        _craftStartingRevision = view.Crafting!.Revision;
        _stageTime.Restart();
        Press("craft_rations");
    }

    private void BeginPause()
    {
        ReleaseMovement();
        Press("pause_world");
        _stageTime.Restart();
        _stage = Stage.Settling;
    }

    private static string Snapshot(LocalWorldView view) => JsonSerializer.Serialize(new
    {
        Retained = new
        {
            World = view.WorldId.ToString(), Day = view.Calendar.Days, Millisecond = view.Calendar.MillisecondsWithinDay,
            view.PlayerGatherEffort, view.HasReadingHold, Quantity = view.PlayerInventory.Quantity,
            StorageRevision = view.PlayerInventory.Revision,
            Lots = view.PlayerInventory.Lots.Select(lot => new { Id = lot.LotId.ToString(), Definition = lot.DefinitionId.ToString(), lot.Revision, lot.Quantity, lot.Provenance }),
            Actors = view.Actors.Select(actor => new { Id = actor.ActorId.ToString(), actor.Feet.X, actor.Feet.Y, actor.Feet.Z, actor.PresenceRevision }),
            Nodes = view.GatheringNodes.Select(node => new { Id = node.NodeId.ToString(), node.Revision, node.RemainingSwings, node.Visibility })
        },
        CraftingRevision = view.Crafting?.Revision
    });

    private void Finish(LocalWorldView view)
    {
        if (DisplayServer.GetName() != "headless")
        {
            using Image image = GetViewport().GetTexture().GetImage();
            Require(image.SavePng(Path.ChangeExtension(reportPath, "." + mode + ".png")) == Error.Ok, "Native capture failed.");
        }
        string output = Path.ChangeExtension(reportPath, "." + mode + ".json");
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            Kind = "native-farm-production-v1", Mode = mode, Status = "passed", Runtime = RuntimeInformation.FrameworkDescription,
            DisplayDriver = DisplayServer.GetName(), Initial = _initial, Final = Snapshot(view), Checks = _checks,
            FocusRequests = _focusRequests, DurationMilliseconds = _timeout.ElapsedMilliseconds
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        _stage = Stage.Done;
        GD.Print("Native production passed: " + output);
        game.RequestClose();
    }

    private static void Press(string action)
    {
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = true, Strength = 1 });
        Input.ParseInputEvent(new InputEventAction { Action = action, Pressed = false });
    }

    private static void ReleaseMovement()
    {
        foreach (string action in new[] { "walk_left", "walk_right", "walk_up", "walk_down" })
        {
            Input.ActionRelease(action);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
