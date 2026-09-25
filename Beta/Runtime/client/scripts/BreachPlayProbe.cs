using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using OpenFantasy.Client.Presentation;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Real-input partial-work/restart/breach journey; observes public state without granting work or water.</summary>
internal sealed partial class BreachPlayProbe(FarmGame game, string reportPath, string mode) : Node
{
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly List<string> _checks = [];
    private readonly List<object> _inputs = [];
    private readonly List<object> _observations = [];
    private string? _movement;
    private string _stage = "opening";
    private string? _initial;

    public override async void _Ready()
    {
        try
        {
            await Until(() => game.CurrentView is { Worksite: not null, Liquids: not null }
                && game.CurrentOffers.Count > 0, "The breach world did not open.", 30);
            game.InjectFixtureFocus(true);
            Require(game.IsLocallyPaused, "The disposable breach world must open paused.");
            _initial = PublicHash();
            if (mode != "fresh")
            {
                string priorMode = mode == "breach" ? "fresh" : "breach";
                using JsonDocument prior = JsonDocument.Parse(File.ReadAllText(Output(priorMode, "json")));
                Require(prior.RootElement.GetProperty("Status").GetString() == "passed"
                    && prior.RootElement.GetProperty("FinalHash").GetString() == _initial,
                    "The exact prior public state did not recover before readiness.");
                _checks.Add("Separate process recovered exact paused terrain, work, water, actors and inventory.");
            }
            await Delay(0.6);
            Require(PublicHash() == _initial && !game.IsWheelOpen && game.CurrentTarget is null,
                "Paused opening changed the world or restored transient input.");
            Observe("opening");
            if (mode != "reopen")
            {
                Tap(Key.Escape);
                await Until(() => !game.IsLocallyPaused, "Resume did not apply.");
                if (mode == "fresh") await StartMining();
                else await FinishMining();
                await PauseAndSettle();
            }
            await Frame();
            Observe("final");
            await Capture(mode);
            CheckJourneyDeadline();
            Report("passed", null);
            game.RequestClose();
        }
        catch (Exception error)
        {
            SetMovement(null);
            Report("failed", error.ToString());
            GD.PushError("Native breach probe failed: " + error);
            GetTree().Quit(1);
        }
    }

    private LocalExcavationView Site => View.Worksite!.ExcavationTargets.Single();
    private bool DividerSolid => View.Liquids!.Terrain.Single(region => region.Bounds.Contains(Site.Cell)).IsSolid(Site.Cell);
    private ulong ReceiverVolume => View.Liquids!.Cells.Where(cell => cell.Coordinate.X > BreachFarmLayout.DividerCell.X)
        .Aggregate(0UL, (sum, cell) => checked(sum + cell.VolumeMicrolitres));

    private async Task StartMining()
    {
        _stage = "bank-walk";
        await Walk(new SubunitPosition(Player.Feet.X, BreachFarmLayout.NorthBank.Y, 256));
        await Walk(BreachFarmLayout.MiningStance);
        await SelectDivider();
        await Frame();
        Require(Site.CompletedWork == 0 && DividerSolid && ReceiverVolume == 0,
            "Water crossed the solid divider before earned excavation.");
        Require(!View.Worksite!.HasPhysicalConstruction && !View.Worksite.SupportsAssistedTraversal
            && !game.CurrentOffers.ContainsKey(FarmSemanticAction.Traverse),
            "The dry bank advertised unsupported physical construction or climbing.");
        await Capture("divider");
        _stage = "cancel-mining";
        await InspectMining(false);
        await Capture("mining-wheel");
        game.InjectFixtureFocus(false);
        await Until(() => !game.IsWheelOpen && !game.IsWindowFocused, "Focus loss did not cancel the choice.");
        await Delay(0.2);
        Require(!game.HasPendingAction && Site.CompletedWork == 0 && DividerSolid && ReceiverVolume == 0,
            "A cancelled mining inspection changed the divider.");
        game.InjectFixtureFocus(true);
        await Delay(0.2);
        Require(Site.CompletedWork == 0 && !game.HasPendingAction, "Focus return submitted a cancelled action.");
        _checks.Add("Focus loss cancels inspection; return submits no mining command.");
        _stage = "partial-mining";
        ulong inventory = View.PlayerInventory.Quantity;
        await Mine(false);
        Require(Site.CompletedWork == 1 && DividerSolid && ReceiverVolume == 0
            && View.PlayerInventory.Quantity == inventory,
            "Partial work removed terrain, issued substrate or leaked water early.");
        _checks.Add("One confirmed wheel action saves partial labor while the divider remains solid and the receiver dry.");
    }

    private async Task FinishMining()
    {
        _stage = "resume-mining";
        Require(Site.CompletedWork == 1 && DividerSolid && ReceiverVolume == 0
            && Player.Feet == BreachFarmLayout.MiningStance, "The saved partial-work stance was not retained.");
        await SelectDivider();
        var before = View.PlayerInventory.Lots.ToDictionary(lot => lot.LotId);
        await Mine(true);
        Require(Site.CompletedWork == 2 && DividerSolid && ReceiverVolume == 0,
            "The second unit of labor opened the divider early.");
        _stage = "breach";
        await Mine(false);
        Require(Site.IsComplete && !DividerSolid, "Final work did not remove the retained divider.");
        var after = View.PlayerInventory.Lots.ToDictionary(lot => lot.LotId);
        Require(before.All(pair => after.TryGetValue(pair.Key, out LocalItemLotView? lot) && lot == pair.Value)
            && after.Count == before.Count + 1 && after.Where(pair => !before.ContainsKey(pair.Key)).Single().Value.Quantity == 1,
            "Extraction must retain every prior lot and issue exactly one substrate unit.");
        await Until(() => View.Liquids!.Cells.Any(cell => cell.Coordinate.Z == -1
            && cell.Coordinate.X >= 118 && cell.VolumeMicrolitres > 0),
            "Conserved water did not reach the farther lower receiver through the earned opening.", 15);
        await Until(() => !game.CurrentOffers[FarmSemanticAction.Excavate].CanExecute,
            "A completed divider remained available for mining.");
        await Frame();
        Observe("flowing");
        await Capture("flowing");
        _checks.Add("Two deliberate actions finish saved labor; one substrate lot is issued and finite water reaches the lower receiver.");
        _stage = "pause-resume";
        await PauseAndSettle();
        string held = PublicHash();
        Tap(Key.Escape);
        await Until(() => !game.IsLocallyPaused && PublicHash() != held, "Resume did not advance the same breached world.");
    }

    private async Task SelectDivider()
    {
        Tap(Key.R);
        await Frame();
        for (int count = 0; game.CurrentTarget?.Id != Site.SiteId; count++)
        {
            Require(count < 8, "Target cycling did not find the actual owned divider.");
            Tap(Key.Q);
            await Frame();
        }
        await Until(() => game.CurrentOffers.TryGetValue(FarmSemanticAction.Excavate, out var offer)
            && offer.TargetId == Site.SiteId, "Presented mining did not catch up to the selected divider.");
    }

    private async Task InspectMining(bool controller)
    {
        await Until(() => !game.HasPendingAction
            && game.CurrentOffers.TryGetValue(FarmSemanticAction.Excavate, out FarmActionOffer? offer)
            && offer.CanExecute && offer.Scope.ExpectedActorRevision == Player.PresenceRevision
            && offer.Intent is ExcavateIntent mine && mine.SiteId == Site.SiteId
            && mine.ExpectedSiteRevision == Site.SiteRevision && mine.ExpectedTerrainRevision == Site.TerrainRevision,
            "The current mining offer did not become available.", 15);
        Tap(Key.V);
        await Until(() => game.IsWheelOpen, "V did not open contextual actions.");
        FarmQuickWheel wheel = game.GetChildren().OfType<FarmQuickWheel>().Single();
        Require(wheel.Model?.Selected is null && wheel.Model!.Slots.All(slot => slot?.Action != FarmSemanticAction.Traverse),
            "Opening the wheel selected an action or exposed unsupported climbing.");
        if (controller) Button(JoyButton.DpadRight); else Tap(Key.Right);
        await Until(() => wheel.Model?.Selected is { Action: FarmSemanticAction.Excavate, Offer.CanExecute: true },
            "Direction did not inspect the executable mining offer.");
    }

    private async Task Mine(bool controller)
    {
        ulong before = Site.CompletedWork;
        await InspectMining(controller);
        if (controller) Button(JoyButton.A); else Tap(Key.Enter);
        await Until(() => !game.IsWheelOpen && !game.HasPendingAction && Site.CompletedWork == before + 1,
            "One confirmed action did not earn exactly one unit of excavation work.", 15);
    }

    private void Button(JoyButton button)
    {
        _inputs.Add(new { _stage, Button = button.ToString(), Milliseconds = _elapsed.ElapsedMilliseconds });
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = true });
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = false });
        Input.FlushBufferedEvents();
    }

    private void Observe(string stage)
    {
        LocalLiquidView liquids = View.Liquids!;
        ulong volume = liquids.Cells.Aggregate(0UL, (sum, cell) => checked(sum + cell.VolumeMicrolitres));
        FarmCanvas canvas = game.GetChildren().OfType<FarmCanvas>().Single();
        WatercourseView rendered = canvas.GetNode<WatercourseView>("Watercourse");
        string hint = game.GetChildren().OfType<FarmHud>().Single().GetNode<Label>("%ProjectStatus").Text;
        Require(!hint.Contains("Climb", StringComparison.Ordinal) && !hint.Contains("stairs", StringComparison.Ordinal)
            && !hint.Contains("worksite supplies", StringComparison.Ordinal),
            "The breach HUD described unsupported climbing, stairs or worksite funding.");
        Require(volume == 6_000_000_000UL && rendered.RenderedVolumeMicrolitres == volume
            && rendered.RenderedCellCount == liquids.Cells.Count,
            "Public and rendered liquid geometry must conserve all 6,000 litres.");
        Require(View.Actors.All(actor => !liquids.Cells.Any(cell => cell.Coordinate == actor.Feet.Cell)),
            "The dry-bank journey entered occupied water.");
        _observations.Add(new { Stage = stage, View.Calendar, Site, DividerSolid,
            TotalMicrolitres = volume, ReceiverMicrolitres = ReceiverVolume, Cells = liquids.Cells, Player.Feet });
    }

    private async Task PauseAndSettle()
    {
        SetMovement(null);
        Require(!game.IsLocallyPaused, "Unexpected local pause.");
        Tap(Key.Escape);
        await Until(() => game.IsLocallyPaused && !game.HasPendingAction, "Pause did not settle.");
        await Delay(0.25);
        string held = PublicHash();
        await Delay(0.65);
        Require(PublicHash() == held, "The paused public world changed.");
    }

    private async Task Walk(SubunitPosition destination)
    {
        var timer = Stopwatch.StartNew();
        while (true)
        {
            Require(!game.HasFailed && timer.Elapsed.TotalSeconds < 20,
                "Walking did not reach and settle at " + destination);
            SubunitPosition feet = Player.Feet;
            if (feet.X == destination.X && feet.Y == destination.Y)
            {
                SetMovement(null);
                // A movement sample already owned by the host may finish after key release.
                // Observe that result and correct through ordinary input; never snap the actor.
                await Delay(0.2);
                Require(timer.Elapsed.TotalSeconds < 20, "Walking exceeded its 20-second settling bound.");
                if (Player.Feet.X == destination.X && Player.Feet.Y == destination.Y)
                {
                    Require(Player.Feet.Z == destination.Z,
                        "The reached floor differs from the expected physical floor: " + Player.Feet);
                    return;
                }
                continue;
            }
            Require((destination.X - feet.X) % 32 == 0 && (destination.Y - feet.Y) % 32 == 0,
                "Movement left the admitted input lattice.");
            SetMovement(feet.X != destination.X ? feet.X < destination.X ? "walk_right" : "walk_left"
                : feet.Y < destination.Y ? "walk_down" : "walk_up");
            await Frame();
        }
    }

    private LocalWorldView View => game.CurrentView ?? throw new InvalidOperationException("Missing public view.");
    private LocalActorView Player => View.Actors.Single(actor => actor.ActorId == View.PlayerActorId);
    private string PublicHash() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(View))));
    private string Output(string stage, string extension) => Path.ChangeExtension(reportPath, "." + stage + "." + extension);

    private void SetMovement(string? action)
    {
        if (_movement == action) return;
        if (_movement is not null) Input.ActionRelease(_movement);
        _movement = action;
        if (action is not null) Input.ActionPress(action);
    }

    private void Tap(Key key)
    {
        _inputs.Add(new { _stage, Key = key.ToString(), Milliseconds = _elapsed.ElapsedMilliseconds });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
        Input.FlushBufferedEvents();
    }

    private async Task Until(Func<bool> condition, string failure, double seconds = 10)
    {
        var timer = Stopwatch.StartNew();
        while (true)
        {
            bool satisfied = condition();
            CheckJourneyDeadline();
            Require(timer.Elapsed.TotalSeconds < seconds, failure);
            if (satisfied) return;
            await Frame();
        }
    }

    private async Task Delay(double seconds)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < seconds) await Frame();
    }

    private async Task Frame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        CheckJourneyDeadline();
    }

    private void CheckJourneyDeadline() => Require(!game.HasFailed && _elapsed.Elapsed.TotalSeconds < 180,
        "Breach journey failed or exceeded its 180-second bound at " + _stage);

    private async Task Capture(string stage)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(Output(stage, "png")) == Error.Ok, "Breach image could not be saved.");
    }

    private void Report(string status, string? error)
    {
        File.WriteAllText(Output(mode, status == "passed" ? "json" : "failed.json"), JsonSerializer.Serialize(new
        {
            Kind = "native-terrain-breach-play-v1", Mode = mode, Status = status, Error = error, Stage = _stage,
            InitialHash = _initial, FinalHash = game.CurrentView is null ? null : PublicHash(),
            Checks = _checks, Inputs = _inputs, Observations = _observations, Display = DisplayServer.GetName(), FocusRequests = 0,
            game.IsWindowFocused, game.IsLocallyPaused, DurationMilliseconds = _elapsed.ElapsedMilliseconds,
            Actors = game.CurrentView?.Actors.Select(actor => new { Id = actor.ActorId.ToString(), actor.Feet }),
            Limitation = "Controlled background focus and injected native input; no human art or physical device acceptance."
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
