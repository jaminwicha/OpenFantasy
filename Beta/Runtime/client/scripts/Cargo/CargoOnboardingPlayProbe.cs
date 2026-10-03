using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.NativeCargo;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;

namespace OpenFantasy.Client.Cargo;

/// <summary>
/// Disposable native onboarding over the ordinary production cargo opening: the same --cargo selection creates a fresh
/// save, then held arrow keys walk the player to Wren, E/G/G/E talk, accept and deliver her quest for its earned reward,
/// the wheel's mining action opens the bank divider through ordinary work, the released water is shown through the
/// regional projection, and the player walks on to Marla's stall. Nothing is granted, teleported or installed.
/// </summary>
internal sealed partial class CargoOnboardingPlayProbe : Node
{
    internal const double ProbeLimitSeconds = 300;
    private const double OpeningSeconds = 30;
    private const double ReplanSeconds = 2;
    private const int MaximumReplans = 12;
    private readonly FarmGame _game;
    private readonly string _reportPath;
    private readonly string _savePath;
    private readonly MarketProbeDriver _driver;
    private readonly CargoRenderedEvidence _rendered;
    private readonly List<string> _checks = [];
    private readonly List<object> _walkInputs = [];
    private readonly List<object> _observations = [];
    private readonly List<string> _images = [];
    private Key? _held;
    private string _stage = "opening";
    private EntityId _player;
    private bool _savedBeforeOpening;
    private readonly bool _reopening;

    internal CargoOnboardingPlayProbe(FarmGame game, string savePath, string reportPath, bool reopening = false)
    {
        _game = game;
        _savePath = savePath;
        _reportPath = reportPath;
        _savedBeforeOpening = File.Exists(savePath);
        _reopening = reopening;
        _driver = new MarketProbeDriver(this, game, "keyboard", () => _stage, ProbeLimitSeconds);
        _rendered = new CargoRenderedEvidence(game);
    }

    private LocalWorldView View => _game.CurrentView ?? throw new InvalidOperationException("No public world.");
    private SubunitPosition Feet => View.Actors.Single(actor => actor.ActorId == View.PlayerActorId).Feet;
    private FarmQuickWheel Wheel => _game.GetNode<FarmQuickWheel>("FarmQuickWheel");

    public override void _Process(double delta) => _rendered.ObserveFocus(GetWindow());

    public override async void _Ready()
    {
        try
        {
            Require(_savedBeforeOpening == _reopening, "The probe needs its declared fresh or retained save path.");
            await _driver.Until(() => _game.CurrentView?.Markets?.Stalls.Count == 2 && _game.Canvas.Regional is not null,
                "The production cargo world did not open.", OpeningSeconds);
            _game.InjectFixtureFocus(true);
            _player = View.PlayerActorId;
            if (_reopening)
            {
                Require(_game.AdventureProfile && _game.IsLocallyPaused, "Reopening requires the paused ordinary adventure.");
                await VerifyAdventureReopen();
                _driver.Deadline();
                Report("passed", null);
                _game.RequestClose();
                return;
            }
            Require(File.Exists(_savePath) && View.Progression!.CurrencyBalance == 0 && View.PaidQuotes.Count == 0
                && View.Quests.All(quest => quest.Status is null), "The production entry did not create a fresh cargo world.");
            Require(_game.IsLocallyPaused, "The onboarding run starts paused.");
            _checks.Add("The ordinary selection created a fresh save at its disposable path: no reward, quest or quote.");
            SubunitPosition origin = Feet;
            LocalObservedRegion farm = View.Regional!.RegionAt(Feet.Cell.X, Feet.Cell.Y)!;
            CargoCorridor corridor = CargoCorridor.CreateCircuit(farm.Bounds, NativeCargoWorld.Sources);
            await Capture("onboarding-start");
            _driver.Tap(Key.Escape);
            await _driver.Until(() => !_game.IsLocallyPaused, "Escape did not resume world time.");
            if (_game.AdventureProfile) await GatherAdventureSupplies();

            _stage = "walk-to-wren";
            LocalActorView wren = View.Actors.Single(actor => actor.ActorId == _game.CompanionActorId);
            await WalkBeside(wren.Feet);
            _stage = "wren-quest";
            await _driver.Until(() => _game.CurrentOffers.TryGetValue(FarmSemanticAction.Speak, out FarmActionOffer? speak)
                && speak.CanExecute, "Wren's talk offer is not available beside her.");
            _driver.Tap(Key.E);
            await _driver.Until(() => View.Dialogue is not null && !_game.HasPendingAction, "Talking to Wren opened no conversation.");
            _driver.Tap(Key.G);
            await _driver.Until(() => View.Quests.Any(quest => quest.Status == ActorQuestStatus.Accepted) && !_game.HasPendingAction,
                "G did not accept Wren's quest.");
            _driver.Tap(Key.G);
            await _driver.Until(() => View.Progression!.CurrencyBalance > 0 && !_game.HasPendingAction, "G did not deliver Wren's quest.");
            ulong reward = View.Progression!.CurrencyBalance;
            _driver.Tap(Key.E);
            await _driver.Until(() => View.Dialogue is null && !_game.HasPendingAction, "E did not close the conversation.");
            await Capture("wren-reward");
            _observations.Add(new { Label = "reward", Reward = reward });
            _checks.Add($"E, G, G, E talked to Wren, accepted and delivered her quest and closed: the earned reward is {reward}.");

            _stage = "walk-to-bank";
            // The authored lane's legs of at most eight cells keep each read-only route within the bounded search.
            await Walk("farm-to-bank", CargoCorridor.Lane([Cell(Feet),
                Cell(new SubunitPosition(Feet.X, BreachFarmLayout.MiningStance.Y, Feet.Z)), Cell(BreachFarmLayout.MiningStance)]));
            _stage = "mine-divider";
            await Mine();
            await Capture("water");
            if (_game.AdventureProfile)
            {
                await _driver.Until(() => View.Construction.Any(project => project.IsCompleted),
                    "Wren did not finish her funded construction while the player worked at the bank.", 40);
                _checks.Add("Wren remained an NPC and autonomously completed the funded original construction.");
            }

            _stage = "walk-to-marla";
            // Back along the north bank to the starting cell (the opened divider is now a hole), then the fixture's own lane.
            await Walk("bank-to-start", CargoCorridor.Lane([Cell(Feet), Cell(new SubunitPosition(origin.X, Feet.Y, Feet.Z)), Cell(origin)]));
            CellCoordinate start = Cell(origin);
            await Walk("start-to-brasshollow", CargoCorridor.Lane([start, new CellCoordinate(start.X, corridor.OriginApproach.Y, start.Z),
                corridor.OriginApproach]));
            NativeCargoStall marla = _game.CargoStalls.Single(stall => stall.MerchantSourceId == CargoCircuitSources.OriginMerchantId);
            await _driver.Until(() => View.Markets!.Stalls.Single(stall => stall.MarketId == marla.MarketId).IsAvailableInPerson,
                "Marla's stall is not available after the onboarding walk.");
            await _rendered.Check("marla", Capture);
            await Capture("marla");
            _checks.Add("Held arrow keys carried the rewarded player on to Marla's stall, now available in person.");

            _stage = "pause";
            _driver.Tap(Key.Escape);
            await _driver.Until(() => _game.IsLocallyPaused && !_game.HasPendingAction, "Escape did not pause the world.");
            _driver.Deadline();
            Report("passed", null);
            _game.RequestClose();
        }
        catch (Exception error)
        {
            Release();
            Report("failed", error.ToString());
            GD.PushError("Native cargo onboarding probe failed: " + error);
            GetTree().Quit(1);
        }
    }

    /// <summary>
    /// Three ordinary work actions through the wheel's mining choice open the divider; the world then resolves the
    /// admitted breach and its committed water reaches the receiver. The regional view must draw both changes.
    /// </summary>
    private async Task Mine()
    {
        CargoDrawnTerrain before = _game.Canvas.DrawnTerrain ?? throw new InvalidOperationException("No terrain was drawn.");
        LocalExcavationView site = Assert(View.Worksite?.ExcavationTargets.SingleOrDefault(), "The cargo farm has no own work target.");
        Require(View.Regional!.RegionAt(site.Cell.X, site.Cell.Y)?.GroundAt(site.Cell.X, site.Cell.Y) is not LocalGroundKind.Open,
            "The intact divider is not observed as standing ground.");
        Require(View.Regional.Water.Any(water => water.CellX < site.Cell.X) && !View.Regional.Water.Any(water => water.CellX > site.Cell.X),
            "The genesis water is not observed in the upper channel alone.");
        for (int attempt = 0; _game.CurrentTarget?.Kind != FarmTargetKind.Excavation; attempt++)
        {
            Require(attempt < 8, "Q never selected the divider work target.");
            _driver.Tap(Key.Q);
            await _driver.Frame();
        }
        await _driver.Frame();
        CargoViewProjection projection = _game.Canvas.Regional!;
        CargoCanvasPoint expected = projection.Project(new SubunitPosition(site.Cell.X * 256 + 128, site.Cell.Y * 256 + 128,
            projection.Observation.FloorFeetZ));
        Require(_game.Canvas.TargetCue is { } cue && Math.Abs(cue.X - expected.X) < 0.01 && Math.Abs(cue.Y - expected.Y) < 0.01,
            "The work target cue is not on the divider's own column.");
        await Capture("bank");
        for (ulong work = 1; work <= site.RequiredWork; work++)
        {
            await _driver.Until(() => _game.CurrentOffers.TryGetValue(FarmSemanticAction.Excavate, out FarmActionOffer? offer)
                && offer.CanExecute && !_game.HasPendingAction, $"The mining action is not available for work {work}.");
            await ChooseOnWheel(FarmSemanticAction.Excavate);
            ulong done = work;
            await _driver.Until(() => !_game.HasPendingAction && (View.Worksite!.ExcavationTargets.Single().CompletedWork >= done
                || View.IsResolvingConsequences), $"Mining work {work} was not applied.");
        }
        await _driver.Until(() => !View.IsResolvingConsequences && View.Regional is { } observed
            && observed.RegionAt(site.Cell.X, site.Cell.Y)?.GroundAt(site.Cell.X, site.Cell.Y) == LocalGroundKind.Open
            && observed.Water.Any(water => water.CellX > site.Cell.X), "The opened divider and released water were not observed.", 20);
        await _driver.Frame();
        await _driver.Frame();
        CargoDrawnTerrain after = _game.Canvas.DrawnTerrain!;
        Require(after.Shape != before.Shape && !after.Revisions.SequenceEqual(before.Revisions) || after.WaterColumns != before.WaterColumns,
            "The drawn terrain did not change with the committed excavation and water.");
        await _rendered.Check("water", Capture);
        _observations.Add(new { Label = "mined", Before = before, After = after,
            Water = View.Regional!.Water.Select(water => new { water.CellX, water.CellY, water.VolumeMicrolitres, water.SurfaceSubunits }) });
        _checks.Add("Q cued the divider's own column; three wheel mining actions opened it and the regional view redrew the "
            + "opening and the released water in the receiver.");
    }

    private async Task ChooseOnWheel(FarmSemanticAction action)
    {
        _driver.Tap(Key.V);
        await _driver.Until(() => _game.IsWheelOpen, "Wheel did not open.");
        for (int page = 0; Wheel.Model!.Slots.All(slot => slot?.Action != action); page++)
        {
            Require(page < 4, "The wheel has no page holding " + action + ".");
            _driver.Tap(Key.Q);
            await _driver.Frame();
        }
        int index = Array.FindIndex(Wheel.Model.Slots.ToArray(), slot => slot?.Action == action);
        _driver.Tap(new[] { Key.Up, Key.Right, Key.Down, Key.Left }[index]);
        await _driver.Until(() => Wheel.Model?.Selected?.Action == action, action + " sector selection failed.");
        await _driver.Frame();
        _driver.Tap(Key.Enter);
        await _driver.Until(() => !_game.IsWheelOpen, "The wheel did not close after confirming " + action + ".");
    }

    /// <summary>Walks to a free column beside one standing position: the first cardinal neighbour the route reaches.</summary>
    private async Task WalkBeside(SubunitPosition other)
    {
        foreach ((long dx, long dy) in new[] { (0L, 1L), (0L, -1L), (1L, 0L), (-1L, 0L) })
        {
            SubunitPosition beside = other.Offset(dx * 256, dy * 256, 0);
            if (_game.CargoRouteAdvice!.Plan(beside).Waypoints is null && Feet != beside) continue;
            await Walk("farm-to-wren", [Cell(beside)]);
            return;
        }
        throw new InvalidOperationException("No reachable column beside Wren.");
    }

    private async Task Walk(string label, IReadOnlyList<CellCoordinate> waypoints)
    {
        int replans = 0;
        foreach (CellCoordinate waypoint in waypoints)
        {
            SubunitPosition target = CargoCorridor.FeetAt(waypoint);
            while (Feet != target)
            {
                var (path, status) = _game.CargoRouteAdvice!.Plan(target);
                Require(path is { Count: > 0 }, $"{label}: no ground route to {waypoint} ({status}).");
                bool reached = true;
                foreach (SubunitPosition point in path!)
                {
                    var stuck = System.Diagnostics.Stopwatch.StartNew();
                    SubunitPosition last = Feet;
                    while (Feet != point && reached)
                    {
                        long dx = point.X - Feet.X;
                        long dy = point.Y - Feet.Y;
                        Hold(dx != 0 ? dx > 0 ? Key.Right : Key.Left : dy > 0 ? Key.Down : Key.Up);
                        await _driver.Frame();
                        if (Feet != last) { last = Feet; stuck.Restart(); }
                        reached = stuck.Elapsed.TotalSeconds < ReplanSeconds;
                    }
                    if (!reached) break;
                }
                if (!reached)
                {
                    Release();
                    Require(++replans <= MaximumReplans, $"{label}: no progress towards {waypoint}.");
                    await _driver.Delay(0.3);
                }
            }
        }
        Release();
        await _driver.Until(() => !_game.HasPendingAction, label + " did not settle.");
        await _rendered.Check(label, Capture);
        _observations.Add(new { Label = label, Feet = new { Feet.X, Feet.Y, Feet.Z }, Replans = replans });
    }

    private static CellCoordinate Cell(SubunitPosition feet) => new(feet.Cell.X, feet.Cell.Y, feet.Cell.Z - 1);

    private void Hold(Key key)
    {
        if (_held == key) return;
        Release();
        _held = key;
        _walkInputs.Add(new { Stage = _stage, Route = "keyboard", Hold = key.ToString() });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
        Input.FlushBufferedEvents();
    }

    private void Release()
    {
        if (_held is not { } key) return;
        _held = null;
        _walkInputs.Add(new { Stage = _stage, Route = "keyboard", Release = key.ToString() });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
        Input.FlushBufferedEvents();
    }

    private async Task<Image?> Capture(string stage)
    {
        if (DisplayServer.GetName() == "headless") return null;
        _driver.Deadline();
        await AwaitDraw().WaitAsync(TimeSpan.FromSeconds(5));
        _driver.Deadline();
        Image image = GetViewport().GetTexture().GetImage();
        string path = Path.ChangeExtension(_reportPath, null) + "." + stage + ".png";
        Require(image.SavePng(path) == Error.Ok, "Onboarding image could not be saved.");
        _images.Add(path);
        return image;
    }

    private async Task AwaitDraw() => await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

    private void Report(string status, string? error)
    {
        File.WriteAllText(Path.ChangeExtension(_reportPath, null) + (status == "passed" ? ".json" : ".failed.json"),
            JsonSerializer.Serialize(new
            {
                Kind = _game.AdventureProfile ? "native-adventure-onboarding-v1" : "native-cargo-onboarding-v1",
                Mode = _reopening ? "reopen" : "onboarding", Route = "keyboard", Status = status, Error = error,
                Stage = _stage, Save = _savePath, Entry = _game.AdventureProfile ? "--adventure" : "--cargo",
                SeedText = _game.AdventureProfile ? AdventureWorldAdmission.SeedText : NativeCargoWorld.SeedText,
                PlayerActorId = _player.ToString(), FinalFeet = _game.CurrentView is null ? null : new { Feet.X, Feet.Y, Feet.Z },
                FinalBalance = _game.CurrentView?.Progression?.CurrencyBalance, Checks = _checks, Inputs = _driver.Inputs,
                WalkInputs = _walkInputs, Observations = _observations, Rendered = _rendered.Samples, Images = _images,
                Focus = _rendered.Focus(GetWindow()), _game.IsLocallyPaused, DurationMilliseconds = _driver.ElapsedMilliseconds
            }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    private static T Assert<T>(T? value, string message) where T : class => value ?? throw new InvalidOperationException(message);
    private static void Require(bool condition, string message) => MarketProbeDriver.Require(condition, message);
}
