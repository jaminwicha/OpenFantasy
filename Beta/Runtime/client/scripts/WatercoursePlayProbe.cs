using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using OpenFantasy.Client.Presentation;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Disposable real-input water/farm journey; never injects stock, positions or calendar.</summary>
internal sealed partial class WatercoursePlayProbe(FarmGame game, string reportPath, string mode) : Node
{
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly List<string> _checks = [];
    private readonly List<object> _inputs = [];
    private readonly List<object> _water = [];
    private string? _movement;
    private string _stage = "opening";
    private string? _initial;

    public override async void _Ready()
    {
        try
        {
            await Until(() => game.CurrentView?.Liquids is not null && game.CurrentOffers.Count > 0,
                "The watercourse did not open.", 30);
            game.InjectFixtureFocus(true);
            Require(game.IsLocallyPaused, "The disposable watercourse must open paused.");
            _initial = PublicHash();
            if (mode == "reopen")
            {
                using JsonDocument prior = JsonDocument.Parse(File.ReadAllText(Output("fresh", "json")));
                Require(prior.RootElement.GetProperty("Status").GetString() == "passed"
                    && prior.RootElement.GetProperty("FinalHash").GetString() == _initial,
                    "The exact prior public state including liquid placement did not recover.");
                _checks.Add("The separate process recovered the exact paused public world and cell placement.");
            }
            await Delay(0.6);
            Require(PublicHash() == _initial, "Paused opening advanced the world.");
            ObserveWater("opening");
            if (mode == "fresh")
            {
                Tap(Key.Escape);
                await Until(() => !game.IsLocallyPaused, "Resume did not apply.");
                await Journey();
                await PauseAndSettle();
            }
            await Capture(mode);
            ObserveWater("final");
            CheckJourneyDeadline();
            Report("passed", null);
            game.RequestClose();
        }
        catch (Exception error)
        {
            SetMovement(null);
            Report("failed", error.ToString());
            GD.PushError("Native watercourse probe failed: " + error);
            GetTree().Quit(1);
        }
    }

    private async Task Journey()
    {
        _stage = "bank-walk";
        await Walk(new SubunitPosition(Player.Feet.X, WatercourseFarmLayout.NorthBank.Y, 256));
        await Walk(WatercourseFarmLayout.NorthBank);
        await Frame();
        ObserveWater("first-bank");
        await Capture("first-bank");
        ulong upperBefore = UpperVolume();
        ulong lowerBefore = LowerVolume();
        Require(lowerBefore > 0 && upperBefore < 6_000_000_000UL,
            "The finite stock did not begin descending during ordinary walking.");
        _stage = "south-bank";
        await Walk(new SubunitPosition(WatercourseFarmLayout.HeadBank.X, WatercourseFarmLayout.NorthBank.Y, 256));
        await Walk(new SubunitPosition(WatercourseFarmLayout.HeadBank.X, 330 * 256 + 128, 256));
        await Walk(new SubunitPosition(110 * 256 + 128, 330 * 256 + 128, 256));
        await Capture("south-bank");
        Require(Player.Feet.Z == 256, "The dry front bank changed physical elevation.");
        await Walk(new SubunitPosition(WatercourseFarmLayout.HeadBank.X, Player.Feet.Y, 256));
        await Walk(new SubunitPosition(WatercourseFarmLayout.HeadBank.X, WatercourseFarmLayout.NorthBank.Y, 256));
        _checks.Add("The dry south-bank stance retains actualZ1 footing while the cutaway restores its foreground ground tile.");
        _stage = "gathering";
        await Walk(new SubunitPosition(101 * 256 + 128, WatercourseFarmLayout.NorthBank.Y, 256));
        await Walk(new SubunitPosition(101 * 256 + 128, 326 * 256 + 128, 256));
        for (int swing = 0; swing < 2; swing++)
        {
            await Until(() => game.CurrentOffers[FarmSemanticAction.Gather].CanExecute, "Gathering was unavailable.");
            ulong before = View.PlayerInventory.Quantity;
            Tap(Key.F);
            await Until(() => !game.HasPendingAction && View.PlayerInventory.Quantity > before,
                "Ordinary F did not gather source stock.");
        }
        _stage = "conversation";
        await ApproachWren();
        Tap(Key.E);
        await Until(() => View.Dialogue is not null && !game.HasPendingAction, "E did not open the conversation.");
        string held = PublicHash();
        await Delay(0.65);
        Require(View.HasReadingHold && PublicHash() == held,
            "Reading advanced the calendar, actors, liquid or another public field.");
        ObserveWater("reading");
        _checks.Add("Real conversation holds the complete public world, including actual liquid cells, for at least650ms.");
        for (int operation = 0; operation < 2; operation++)
        {
            await Until(() => game.CurrentOffers[FarmSemanticAction.Give].CanExecute,
                "Wren's captured quest operation was unavailable.");
            ulong before = View.Quests.Aggregate(0UL, (sum, quest) => sum + quest.ParticipationRevision);
            Tap(Key.G);
            await Until(() => !game.HasPendingAction
                && View.Quests.Aggregate(0UL, (sum, quest) => sum + quest.ParticipationRevision) > before,
                "G did not apply the quest operation.");
        }
        Require(View.Quests.Single().Status == ActorQuestStatus.Completed
            && View.Construction.Single().CompletedWork == 0,
            "The funded quest must finish before any construction under the reading hold.");
        Tap(Key.E);
        await Until(() => View.Dialogue is null && !game.HasPendingAction, "E did not release the conversation.");
        _stage = "funded-work";
        await Walk(new SubunitPosition(Player.Feet.X, WatercourseFarmLayout.NorthBank.Y, 256));
        await Walk(WatercourseFarmLayout.NorthBank);
        await Until(() => View.Construction.Single().IsCompleted, "Wren did not finish the funded guard.", 45);
        Require(View.Construction.Single().CompletedWork == 200,
            "The guard did not consume its complete admitted work.");
        Require(UpperVolume() < upperBefore && LowerVolume() > lowerBefore,
            "The saved finite source and lower pool did not evolve during work.");
        ObserveWater("completed-work");
        await Capture("completed-work");
        _checks.Add("Ordinary gathering and quest delivery fund the same200-work guard while conserved stock drains into the pool.");
        _stage = "pause-resume";
        await PauseAndSettle();
        string stopped = PublicHash();
        Tap(Key.Escape);
        await Until(() => !game.IsLocallyPaused && PublicHash() != stopped, "The paused world did not resume.");
        _checks.Add("The ordinary local pause holds exact state; explicit resume advances the same world.");
    }

    private ulong UpperVolume() => View.Liquids!.Cells.Where(cell => cell.Coordinate.X < 116)
        .Aggregate(0UL, (sum, cell) => checked(sum + cell.VolumeMicrolitres));
    private ulong LowerVolume() => View.Liquids!.Cells.Where(cell => cell.Coordinate.Z < 0)
        .Aggregate(0UL, (sum, cell) => checked(sum + cell.VolumeMicrolitres));

    private void ObserveWater(string stage)
    {
        LocalLiquidView liquids = View.Liquids!;
        ulong total = liquids.Cells.Aggregate(0UL, (sum, cell) => checked(sum + cell.VolumeMicrolitres));
        WatercourseView rendered = game.GetChildren().OfType<FarmCanvas>().Single().GetNode<WatercourseView>("Watercourse");
        Require(total == 6_000_000_000UL && rendered.RenderedVolumeMicrolitres == total
            && rendered.RenderedCellCount == liquids.Cells.Count,
            "The public and rendered geometry must retain all6,000litres without refill.");
        _water.Add(new { Stage = stage, View.Calendar, TotalMicrolitres = total,
            UpperMicrolitres = UpperVolume(), LowerMicrolitres = LowerVolume(), Cells = liquids.Cells });
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

    private async Task ApproachWren()
    {
        (ulong Player, ulong Target)? previous = null;
        var stable = Stopwatch.StartNew();
        await Until(() =>
        {
            LocalWorldView view = View;
            LocalActorView player = view.Actors.Single(actor => actor.ActorId == view.PlayerActorId);
            LocalActorView wren = view.Actors.Single(actor => actor.ControllerKind == ResidentControllerKind.Host);
            long dx = wren.Feet.X - player.Feet.X, dy = wren.Feet.Y - player.Feet.Y;
            if (dx * dx + dy * dy > 384L * 384)
            {
                SetMovement(Math.Abs(dx) > Math.Abs(dy) ? dx > 0 ? "walk_right" : "walk_left" : dy > 0 ? "walk_down" : "walk_up");
                previous = null;
                stable.Restart();
                return false;
            }
            SetMovement(null);
            var revisions = (player.PresenceRevision, wren.PresenceRevision);
            if (previous != revisions)
            {
                previous = revisions;
                stable.Restart();
            }
            return stable.ElapsedMilliseconds >= 150 && !game.HasPendingAction
                && game.CurrentOffers.TryGetValue(FarmSemanticAction.Speak, out FarmActionOffer? offer)
                && offer.CanExecute && offer.Scope.ExpectedActorRevision == player.PresenceRevision
                && offer.Intent is SpeakToActorIntent speak && speak.TargetActorId == wren.ActorId
                && speak.ExpectedTargetRevision == wren.PresenceRevision;
        }, "The player did not approach Wren.", 20);
        _checks.Add("Released movement and both presented Talk revisions settled before one deliberate E press.");
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
        "Watercourse journey failed or exceeded its 180-second bound at " + _stage);

    private async Task Capture(string stage)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(Output(stage, "png")) == Error.Ok, "Watercourse image could not be saved.");
    }

    private void Report(string status, string? error)
    {
        File.WriteAllText(Output(mode, status == "passed" ? "json" : "failed.json"), JsonSerializer.Serialize(new
        {
            Kind = "native-watercourse-play-v1", Mode = mode, Status = status, Error = error, Stage = _stage,
            InitialHash = _initial, FinalHash = game.CurrentView is null ? null : PublicHash(),
            Checks = _checks, Inputs = _inputs, Water = _water, Display = DisplayServer.GetName(), FocusRequests = 0,
            game.IsWindowFocused, game.IsLocallyPaused, DurationMilliseconds = _elapsed.ElapsedMilliseconds,
            Actors = game.CurrentView?.Actors.Select(actor => new { Id = actor.ActorId.ToString(), actor.Feet }),
            Construction = game.CurrentView?.Construction,
            Limitation = "Controlled background focus and injected native input; no human art or physical device acceptance."
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
