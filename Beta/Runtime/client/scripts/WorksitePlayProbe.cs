using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Disposable native journey through real controls and saves; it never edits world state or actor positions.</summary>
internal sealed partial class WorksitePlayProbe(FarmGame game, string reportPath, string mode) : Node
{
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly List<string> _checks = [];
    private readonly List<object> _inputs = [];
    private string? _movement;
    private string _stage = "opening";
    private string? _initial;

    public override async void _Ready()
    {
        try
        {
            await Until(() => game.CurrentView?.Worksite is not null && game.CurrentOffers.Count > 0,
                "The worksite did not open.", 30);
            game.InjectFixtureFocus(true);
            Require(game.IsLocallyPaused, "The disposable worksite must open paused.");
            _initial = PublicHash();
            if (mode != "fresh")
            {
                string previous = mode switch { "build" => "fresh", "mine" => "build", "reopen" => "mine", _ => throw new InvalidOperationException("Unknown worksite stage.") };
                using JsonDocument report = JsonDocument.Parse(File.ReadAllText(Output(previous, "json")));
                Require(report.RootElement.GetProperty("Status").GetString() == "passed"
                    && report.RootElement.GetProperty("FinalHash").GetString() == _initial,
                    "The exact previous paused public state did not recover.");
                _checks.Add("Exact prior public world recovered before readiness, with no transient target or wheel restored.");
            }
            await Delay(0.6);
            Require(PublicHash() == _initial && !game.IsWheelOpen && game.CurrentTarget is null,
                "Paused opening changed the world or retained a transient selection.");
            if (mode != "reopen")
            {
                Tap(Key.Escape);
                await Until(() => !game.IsLocallyPaused, "Escape did not resume the world.");
                if (mode == "fresh") await FundAndBegin();
                else if (mode == "build") await BuildWalkAndStartMining();
                else await CompleteMining();
                await PauseAndSettle();
            }
            await Capture(mode);
            Report("passed", null);
            game.RequestClose();
        }
        catch (Exception error)
        {
            SetMovement(null);
            Report("failed", error.ToString());
            GD.PushError("Native worksite probe failed: " + error);
            GetTree().Quit(1);
        }
    }

    private async Task FundAndBegin()
    {
        _stage = "gathering";
        await Walk(new SubunitPosition(101 * 256 + 128, 326 * 256 + 128, 256));
        for (int swing = 0; swing < 4; swing++)
        {
            await Until(() => game.CurrentOffers[FarmSemanticAction.Gather].CanExecute, "Gathering did not become available.");
            ulong previous = View.PlayerInventory.Quantity;
            Tap(Key.F);
            await Until(() => !game.HasPendingAction && View.PlayerInventory.Quantity > previous, "F did not gather actual stock.");
        }
        _checks.Add("Four ordinary tool actions gathered finite source stock before either project was funded.");
        _stage = "funding";
        await ApproachWren();
        Tap(Key.E);
        await Until(() => View.Dialogue is not null && !game.HasPendingAction, "E did not open Wren's conversation.");
        for (int operation = 0; operation < 4; operation++)
        {
            FarmActionOffer offer = game.CurrentOffers[FarmSemanticAction.Give];
            Require(offer.CanExecute, "The addressed quest operation is unavailable: " + offer.BlockingReason);
            ulong before = View.Quests.Aggregate(0UL, (sum, quest) => sum + quest.ParticipationRevision);
            Tap(Key.G);
            await Until(() => !game.HasPendingAction && View.Quests.Aggregate(0UL,
                (sum, quest) => sum + quest.ParticipationRevision) > before, "G did not apply the captured quest operation.");
        }
        Require(View.Quests.Count == 2 && View.Quests.All(quest => quest.Status == ActorQuestStatus.Completed),
            "Both funded quests must complete while the reading hold prevents construction.");
        Require(View.Construction.All(project => project.CompletedWork == 0), "Reading performed construction.");
        Tap(Key.E);
        await Until(() => View.Dialogue is null && !game.HasPendingAction, "E did not end the conversation.");
        await Walk(WorksiteFarmLayout.PlayerParking);
        await Until(() => View.Construction.Sum(project => (long)project.CompletedWork) >= 25,
            "Wren did not route and start real work.", 30);
        Require(View.Construction.Any(project => !project.IsCompleted), "The partial construction interval was missed.");
        _checks.Add("Two addressed quests fund distinct projects; Wren routes and starts actual construction after dialogue closes.");
    }

    private async Task BuildWalkAndStartMining()
    {
        _stage = "building";
        Require(View.Construction.Any(project => !project.IsCompleted), "Expected unfinished saved construction.");
        await Until(() => View.Construction.Count == 2 && View.Construction.All(project => project.IsCompleted),
            "The resumed funded projects did not finish.", 90);
        Require(View.Worksite!.Members.Count == 14, "Completed geometry did not reach the public view.");
        await Frame();
        FarmCanvas canvas = game.GetChildren().OfType<FarmCanvas>().Single();
        Require(canvas.GetNode<Node2D>("Actors").GetChildren().Count(node => node.Name.ToString().StartsWith("Member_", StringComparison.Ordinal)) == 14,
            "The actual committed members did not become render nodes.");
        Require(canvas.Project(WorksiteFarmLayout.FixedDeck).Y
            == canvas.Project(WorksiteFarmLayout.FixedDeck with { Z = 256 }).Y - 72,
            "Upper-floor projection did not preserve the exact elevation offset.");
        await Walk(WorksiteFarmLayout.StairApproach);
        _stage = "stairs";
        await Walk(new SubunitPosition(WorksiteFarmLayout.FixedDeck.X, WorksiteFarmLayout.StairApproach.Y, 832));
        await Walk(WorksiteFarmLayout.FixedDeck);
        await Walk(WorksiteFarmLayout.RemovableDeck);
        await Capture("upper-deck");
        await Walk(WorksiteFarmLayout.FixedDeck);
        await Walk(new SubunitPosition(WorksiteFarmLayout.FixedDeck.X, WorksiteFarmLayout.StairApproach.Y, 832));
        await Walk(WorksiteFarmLayout.StairApproach);
        _checks.Add("Ordinary walking climbed the built stairs, crossed both upper decks and descended, without assisted teleportation.");
        _stage = "lower-stance";
        const long northernRoute = 323 * 256 + 128;
        await Walk(new SubunitPosition(WorksiteFarmLayout.StairApproach.X, northernRoute, 256));
        await Walk(new SubunitPosition(WorksiteFarmLayout.PitApproach.X, northernRoute, 256));
        await Walk(WorksiteFarmLayout.PitApproach);
        await SelectFace();
        Require(!game.CurrentOffers[FarmSemanticAction.Excavate].CanExecute,
            "Mining on the wrong elevation was presented as available.");
        await WheelAction(FarmSemanticAction.Traverse, 2, controller: true);
        await Until(() => Player.Feet == WorksiteFarmLayout.ExcavationStance && !game.HasPendingAction,
            "The confirmed descent did not reach the real lower stance.");
        await Capture("lower-stance");
        await CheckFocusedCancellation();
        _stage = "partial-mining";
        await WheelAction(FarmSemanticAction.Excavate, 1, controller: false);
        await Until(() => View.Worksite!.ExcavationTargets.Single().CompletedWork == 1 && !game.HasPendingAction,
            "The first excavation action did not preserve partial work.");
        Require(!View.Worksite!.ExcavationTargets.Single().IsComplete, "First mining action removed the entire cell early.");
        _checks.Add("A controller wheel descent and keyboard wheel mining action consumed actual effort; partial mining remains saved.");
    }

    private async Task CompleteMining()
    {
        _stage = "terminal-mining";
        Require(View.Worksite!.ExcavationTargets.Single().CompletedWork == 1, "Expected actual saved partial mining.");
        await SelectFace();
        long[] before = View.Worksite.Members.Select(member => member.Origin.Z).ToArray();
        for (int attempt = 0; !View.Worksite!.ExcavationTargets.Single().IsComplete; attempt++)
        {
            Require(attempt < 3, "Mining repeated beyond the remaining admitted work.");
            ulong work = View.Worksite.ExcavationTargets.Single().CompletedWork;
            await WheelAction(FarmSemanticAction.Excavate, 1, controller: attempt % 2 == 0);
            await Until(() => !game.HasPendingAction && !View.IsResolvingConsequences
                && View.Worksite!.ExcavationTargets.Single().CompletedWork > work,
                "The captured mining action did not finish its actual consequences.", 30);
        }
        long[] after = View.Worksite!.Members.Select(member => member.Origin.Z).ToArray();
        Require(before.Zip(after).Count(pair => pair.First - pair.Second == 256) == 2
            && before.Zip(after).All(pair => pair.First == pair.Second || pair.First - pair.Second == 256),
            "Only the removable assembly's two members must settle by one metre.");
        await Capture("settled");
        Require(!game.CurrentOffers[FarmSemanticAction.Excavate].CanExecute, "A finished excavation was offered for duplicate extraction.");
        await WheelAction(FarmSemanticAction.Traverse, 2, controller: false);
        await Until(() => Player.Feet == WorksiteFarmLayout.PitApproach && !game.HasPendingAction,
            "The confirmed ascent did not return to the real upper ground.");
        _checks.Add("Resumed mining removes one retained cell and visibly settles only its supported deck; completed extraction cannot repeat.");
        _checks.Add("Ordinary assisted climb returns from the mined stance using the same actual footing and clearance rules.");
    }

    private async Task CheckFocusedCancellation()
    {
        await Until(() => game.CurrentOffers[FarmSemanticAction.Excavate].CanExecute,
            "Mining did not become ready before cancellation proof.");
        ulong before = View.Worksite!.ExcavationTargets.Single().CompletedWork;
        Tap(Key.V);
        await Until(() => game.IsWheelOpen, "V did not open the mining wheel.");
        Tap(Key.Right);
        await Frame();
        game.InjectFixtureFocus(false);
        await Until(() => !game.IsWheelOpen && !game.IsWindowFocused, "Focus loss did not cancel the mining choice.");
        await Delay(0.15);
        Require(!game.HasPendingAction && View.Worksite!.ExcavationTargets.Single().CompletedWork == before,
            "Losing focus committed the selected mining action.");
        game.InjectFixtureFocus(true);
        await Delay(0.15);
        Require(!game.HasPendingAction && View.Worksite!.ExcavationTargets.Single().CompletedWork == before,
            "Focus return replayed the cancelled mining action.");
        _checks.Add("Injected focus loss cancels an inspected mining choice; focus return submits nothing.");
    }

    private async Task WheelAction(FarmSemanticAction action, int sector, bool controller)
    {
        await Until(() => !game.HasPendingAction && game.CurrentOffers.TryGetValue(action, out FarmActionOffer? offer)
            && offer.CanExecute, "The requested wheel action did not become available: " + action, 15);
        Tap(Key.V);
        await Until(() => game.IsWheelOpen, "V did not open contextual actions.");
        FarmQuickWheel wheel = game.GetChildren().OfType<FarmQuickWheel>().Single();
        Require(wheel.Model?.Selected is null, "Opening the wheel selected a default action.");
        if (controller) Button(sector == 1 ? JoyButton.DpadRight : JoyButton.DpadDown);
        else Tap(sector == 1 ? Key.Right : Key.Down);
        await Until(() => wheel.Model?.Selected?.Action == action, "Direction did not select the intended contextual action.");
        if (controller) Button(JoyButton.A); else Tap(Key.Enter);
        await Until(() => !game.IsWheelOpen, "Confirmation did not close the wheel and submit its captured action.");
    }

    private async Task SelectFace()
    {
        Tap(Key.R);
        await Frame();
        for (int count = 0; game.CurrentTarget?.Kind != FarmTargetKind.Excavation; count++)
        {
            Require(count < 8, "Target cycling did not reach the public worksite face.");
            Tap(Key.Q);
            await Frame();
        }
    }

    private async Task ApproachWren()
    {
        await Until(() =>
        {
            SubunitPosition target = View.Actors.Single(actor => actor.ControllerKind == OpenFantasy.Core.Worlds.ResidentControllerKind.Host).Feet;
            long dx = target.X - Player.Feet.X, dy = target.Y - Player.Feet.Y;
            if (dx * dx + dy * dy <= 384L * 384) { SetMovement(null); return true; }
            SetMovement(Math.Abs(dx) > Math.Abs(dy) ? dx > 0 ? "walk_right" : "walk_left" : dy > 0 ? "walk_down" : "walk_up");
            return false;
        }, "The player did not approach Wren.", 20);
        await Delay(0.2);
    }

    private async Task Walk(SubunitPosition destination)
    {
        await Until(() =>
        {
            SubunitPosition feet = Player.Feet;
            if (feet.X == destination.X && feet.Y == destination.Y) { SetMovement(null); return true; }
            Require((destination.X - feet.X) % 32 == 0 && (destination.Y - feet.Y) % 32 == 0,
                "Movement left the admitted input lattice.");
            SetMovement(feet.X != destination.X ? feet.X < destination.X ? "walk_right" : "walk_left"
                : feet.Y < destination.Y ? "walk_down" : "walk_up");
            return false;
        }, "Walking did not reach " + destination, 20);
        await Delay(0.2);
        Require(Player.Feet == destination, "The reached floor differs from the expected physical floor: " + Player.Feet);
    }

    private async Task PauseAndSettle()
    {
        SetMovement(null);
        Require(!game.IsLocallyPaused, "The journey unexpectedly paused.");
        Tap(Key.Escape);
        await Until(() => game.IsLocallyPaused && !game.HasPendingAction, "Pause did not settle.");
        await Delay(0.25);
        string held = PublicHash();
        await Delay(0.6);
        Require(PublicHash() == held, "The paused public world changed.");
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

    private void Button(JoyButton button)
    {
        _inputs.Add(new { _stage, Button = button.ToString(), Milliseconds = _elapsed.ElapsedMilliseconds });
        Input.ParseInputEvent(new InputEventJoypadButton { Device = 0, ButtonIndex = button, Pressed = true });
        Input.ParseInputEvent(new InputEventJoypadButton { Device = 0, ButtonIndex = button, Pressed = false });
        Input.FlushBufferedEvents();
    }

    private async Task Until(Func<bool> condition, string failure, double seconds = 10)
    {
        var timer = Stopwatch.StartNew();
        while (!condition())
        {
            Require(!game.HasFailed && _elapsed.Elapsed.TotalSeconds < 180, "Worksite journey failed or exceeded its 180-second bound at " + _stage);
            Require(timer.Elapsed.TotalSeconds < seconds, failure);
            await Frame();
        }
    }

    private async Task Delay(double seconds)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < seconds) await Frame();
    }

    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    private async Task Capture(string stage)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(Output(stage, "png")) == Error.Ok, "Worksite image could not be saved.");
    }

    private void Report(string status, string? error)
    {
        File.WriteAllText(Output(mode, status == "passed" ? "json" : "failed.json"), JsonSerializer.Serialize(new
        {
            Kind = "native-worksite-play-v1", Mode = mode, Status = status, Error = error, Stage = _stage,
            InitialHash = _initial, FinalHash = game.CurrentView is null ? null : PublicHash(),
            Checks = _checks, Inputs = _inputs, Display = DisplayServer.GetName(), FocusRequests = 0,
            game.IsWindowFocused, game.IsLocallyPaused, DurationMilliseconds = _elapsed.ElapsedMilliseconds,
            Actors = game.CurrentView?.Actors.Select(actor => new { Id = actor.ActorId.ToString(), actor.Feet }),
            Construction = game.CurrentView?.Construction, Excavation = game.CurrentView?.Worksite?.ExcavationTargets,
            Limitation = "Injected native controls and controlled background focus; not physical device or human art acceptance."
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
