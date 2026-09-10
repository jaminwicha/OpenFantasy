using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Real named-input proof of gathered crafting, copied quest upgrade, reward and process recovery.</summary>
internal sealed partial class FarmQuestProbe(FarmGame game, string reportPath, string mode) : Node
{
    private enum Stage
    {
        Opening, Recovering, Resuming, OutsideQuestContext, Harvesting, Crafting, Approaching, Talking,
        ReadingOffer, Accepting, Completing, Repeating, ClosingDialogue, Moving, Settling, Paused, Done
    }

    private readonly Stopwatch _timeout = Stopwatch.StartNew();
    private readonly Stopwatch _stageTime = new();
    private readonly List<string> _checks = [];
    private readonly List<object> _inputs = [];
    private readonly List<object> _readiness = [];
    private Stage _stage;
    private (bool Paused, bool Focused)? _lastReadiness;
    private string? _movement;
    private string? _initial;
    private string? _held;
    private string? _reading;
    private string? _beforeRepeat;
    private string? _outsideQuestBefore;
    private string? _outsideQuestAfter;
    private string? _outsideQuestInvariant;
    private bool _outsidePendingAfterInput;
    private bool _outsidePendingAfterObservation;
    private long _outsideQuestMilliseconds;
    private int _outsideQuestSamples;
    private EntityId _berry;
    private EntityId _wood;
    private EntityId _berryItem;
    private EntityId _wren;
    private int _swings;
    private bool _waitingForBerry;
    private ulong _expectedQuantity;
    private long _lastFocusRequest;
    private int _focusRequests;
    private SubunitPosition _movementOrigin;
    private WorldInstant _movementStartedAt;

    public override void _Process(double delta)
    {
        if (_stage == Stage.Done)
        {
            return;
        }
        try
        {
            Require(!game.HasFailed, "The native farm reported failure.");
            Require(_timeout.Elapsed < TimeSpan.FromSeconds(120), "Quest probe timed out at " + _stage);
            if (game.CurrentView is not { } view)
            {
                return;
            }
            ObserveReadiness(view);
            switch (_stage)
            {
                case Stage.Opening: Opened(view); break;
                case Stage.Recovering: ObserveRecovery(view); break;
                case Stage.Resuming: ResumeInput(view); break;
                case Stage.OutsideQuestContext: ObserveOutsideQuestContext(view); break;
                case Stage.Harvesting: Harvest(view); break;
                case Stage.Crafting: ObserveCraft(view); break;
                case Stage.Approaching: ApproachWren(view); break;
                case Stage.Talking: ObserveTalk(view); break;
                case Stage.ReadingOffer: ReadOffer(view); break;
                case Stage.Accepting: ObserveAcceptance(view); break;
                case Stage.Completing: ObserveCompletion(view); break;
                case Stage.Repeating: ObserveRepeat(view); break;
                case Stage.ClosingDialogue: ObserveClose(view); break;
                case Stage.Moving: ObserveMovement(view); break;
                case Stage.Settling: SettlePause(view); break;
                case Stage.Paused: ObservePause(view); break;
            }
        }
        catch (Exception error)
        {
            Fail(error);
        }
    }

    private bool ReadyForInput => !game.IsLocallyPaused && game.IsWindowFocused && !game.HasPendingAction;

    private void ObserveReadiness(LocalWorldView view)
    {
        var readiness = (game.IsLocallyPaused, game.IsWindowFocused);
        if (_lastReadiness != readiness)
        {
            _lastReadiness = readiness;
            _readiness.Add(new { WallMilliseconds = _timeout.ElapsedMilliseconds, game.IsLocallyPaused,
                game.IsWindowFocused, Day = view.Calendar.Days, Millisecond = view.Calendar.MillisecondsWithinDay });
        }
        if (game.IsLocallyPaused || !game.IsWindowFocused)
        {
            SetMovement(null);
        }
        if (!game.IsWindowFocused && !game.IsLocallyPaused && _focusRequests < 8
            && _timeout.ElapsedMilliseconds - _lastFocusRequest >= 2_000)
        {
            _lastFocusRequest = _timeout.ElapsedMilliseconds;
            _focusRequests++;
            GetWindow().GrabFocus();
        }
    }

    private void Opened(LocalWorldView view)
    {
        Require(mode is "prepare" or "upgrade" or "reopen" or "fresh", "Unknown native quest mode.");
        Require(view.Crafting is not null && view.GatheringTools.Count == 2, "The retained production toolkit is missing.");
        LocalCraftRecipeView recipe = FarmCrafting.RationsRecipe(view)
            ?? throw new InvalidOperationException("The source ration recipe is missing.");
        _wood = recipe.Terms.Single(term => term.Role == RecipeMaterialRole.Input && term.Quantity == 1).DefinitionId;
        _berryItem = recipe.Terms.Single(term => term.Role == RecipeMaterialRole.Input && term.Quantity == 2).DefinitionId;
        _berry = view.GatheringNodes.Single(node => node.Kind == GatherNodeKind.Berry).NodeId;
        _wren = view.Actors.Single(actor => actor.ActorId != view.PlayerActorId).ActorId;
        _initial = Snapshot(view);
        if (mode == "fresh")
        {
            Require(game.IsLocallyPaused && view.Progression is { Level: 1, Experience: 0, CurrencyBalance: 0 }
                && view.Quests.Count == 0 && view.Construction.Count == 0 && view.Crafting!.Revision == 0
                && view.PlayerInventory.Quantity == 10 && Quantity(view, _wood) == 8,
                "Fresh construction preparation requires untouched progression, stock and initially undisclosed quests/projects.");
            _checks.Add("A fresh construction world started paused with initial stock and no premature reward or disclosed project.");
            Enter(Stage.Recovering);
            return;
        }
        if (mode == "prepare")
        {
            Require(view.Progression is null && view.Quests.Count == 0 && view.Crafting!.Revision == 0
                && view.PlayerInventory.Quantity == 10 && Quantity(view, _wood) == 8,
                "Preparation requires a fresh profile-three world with eight wood and two tools.");
            Enter(game.IsLocallyPaused ? Stage.Recovering : Stage.Harvesting);
            return;
        }
        Require(game.IsLocallyPaused && view.Progression is not null, "Quest recovery must start paused in profile four.");
        LocalProgressionView progression = view.Progression
            ?? throw new InvalidOperationException("The quest actor progression is missing.");
        string previousMode = mode == "upgrade" ? "prepare" : "upgrade";
        using JsonDocument previous = JsonDocument.Parse(File.ReadAllText(Output(previousMode, "json")));
        string before = previous.RootElement.GetProperty("Final").GetString()!;
        if (mode == "upgrade")
        {
            using JsonDocument old = JsonDocument.Parse(before);
            using JsonDocument current = JsonDocument.Parse(_initial);
            Require(old.RootElement.GetProperty("Retained").GetRawText() == current.RootElement.GetProperty("Retained").GetRawText(),
                "The copied upgrade changed the retained public world or production state.");
            Require(progression.Level == 1 && progression.Experience == 0
                && progression.CurrencyBalance == 0, "Upgrade awarded progression before the quest.");
            _checks.Add("Copied upgrade retained every recorded profile-three projection field without advancing time.");
        }
        else
        {
            Require(before == _initial, "The complete rewarded public view did not restore exactly in the third process.");
            VerifyReward(view);
            _checks.Add("Reward, completed quest, inventory, dialogue and all other public fields recovered exactly.");
        }
        Enter(Stage.Recovering);
    }

    private void ObserveRecovery(LocalWorldView view)
    {
        Require(game.IsLocallyPaused && Snapshot(view) == _initial, "Paused recovery changed before explicit resume.");
        if (_stageTime.ElapsedMilliseconds < 600)
        {
            return;
        }
        _checks.Add("Initial recovery stayed exactly unchanged for at least 600 ms while paused.");
        Press("pause_world");
        Enter(Stage.Resuming);
    }

    private void ResumeInput(LocalWorldView view)
    {
        if (!ReadyForInput)
        {
            return;
        }
        if (mode == "reopen")
        {
            Require(view.HasReadingHold, "Reopened rewarded conversation lost its reading hold.");
            _reading = HoldSnapshot(view);
            _beforeRepeat = Snapshot(view);
            Press("give_wood");
            Enter(Stage.Repeating);
        }
        else if (mode == "fresh")
        {
            Require(view.Dialogue is null && !view.HasReadingHold, "The early G attempt requires no addressed conversation.");
            _outsideQuestBefore = Snapshot(view);
            _outsideQuestInvariant = QuestActionSnapshot(view);
            Enter(Stage.OutsideQuestContext);
            Press("give_wood");
            _outsidePendingAfterInput = game.HasPendingAction;
            Require(!_outsidePendingAfterInput, "Early G queued an action before the player spoke to Wren.");
        }
        else
        {
            Enter(mode == "prepare" ? Stage.Harvesting : Stage.Approaching);
        }
    }

    private void ObserveOutsideQuestContext(LocalWorldView view)
    {
        _outsideQuestSamples++;
        Require(!game.HasPendingAction && view.Dialogue is null && !view.HasReadingHold,
            "Early G created a pending action or changed the unaddressed conversation state.");
        Require(QuestActionSnapshot(view) == _outsideQuestInvariant,
            "Early G consumed player stock or changed quest/progression state before speaking to Wren.");
        if (_stageTime.ElapsedMilliseconds < 300 || _outsideQuestSamples < 2 || !ReadyForInput) return;
        _outsideQuestMilliseconds = _stageTime.ElapsedMilliseconds;
        _outsideQuestAfter = Snapshot(view);
        _outsidePendingAfterObservation = game.HasPendingAction;
        _checks.Add("Ordinary G before conversation queued no action and preserved all player stock, quest and progression fields for at least 300 ms.");
        Enter(Stage.Harvesting);
    }

    private void Harvest(LocalWorldView view)
    {
        if (_waitingForBerry)
        {
            if (game.HasPendingAction || view.PlayerInventory.Quantity != _expectedQuantity)
            {
                Require(_stageTime.ElapsedMilliseconds < 5_000, "The sickle input did not commit exactly one berry.");
                return;
            }
            _waitingForBerry = false;
            _swings++;
            Require(Quantity(view, _berryItem) == (ulong)_swings, "Harvested quantities are not actual berries.");
        }
        if (!ReadyForInput)
        {
            return;
        }
        if (_swings == 2)
        {
            Require(FarmCrafting.HasIngredients(view), "Gathered ingredients are unavailable for crafting.");
            _checks.Add("Two ordinary sickle inputs issued two actual berries from the admitted finite node.");
            SetMovement(null);
            Press("craft_rations");
            Enter(Stage.Crafting);
            return;
        }
        LocalGatherNodeView node = view.GatheringNodes.Single(value => value.NodeId == _berry);
        if (!Approach(Player(view).Feet, node.Feet, 300))
        {
            return;
        }
        if (view.PlayerGatherEffort < 400)
        {
            return;
        }
        Require(FarmClientSession.NearestGatherNode(view)?.NodeId == _berry, "The ordinary context selected a different resource.");
        _expectedQuantity = checked(view.PlayerInventory.Quantity + 1);
        _waitingForBerry = true;
        Press("gather");
        _stageTime.Restart();
    }

    private void ObserveCraft(LocalWorldView view)
    {
        if (game.HasPendingAction || view.Crafting?.Revision != 1)
        {
            Require(_stageTime.ElapsedMilliseconds < 5_000, "The craft input did not commit one real production operation.");
            return;
        }
        Require(Quantity(view, _wood) == 7 && Quantity(view, _berryItem) == 0
            && view.PlayerInventory.Quantity == 12 && view.GatheringTools.Count == 2
            && FarmCrafting.Quantity(view, RecipeMaterialRole.Output) == 2
            && FarmCrafting.Quantity(view, RecipeMaterialRole.Waste) == 1, "The real ration craft lost its input/output accounting.");
        _checks.Add("One craft consumed two berries and one wood; seven wood, two rations, one offcut and both tools remain.");
        if (mode == "fresh") Enter(Stage.Approaching);
        else BeginPause();
    }

    private void ApproachWren(LocalWorldView view)
    {
        if (!ReadyForInput)
        {
            return;
        }
        if (Approach(Player(view).Feet, view.Actors.Single(actor => actor.ActorId == _wren).Feet, 600))
        {
            _checks.Add(mode == "fresh" ? "Ordinary movement approached Wren after gathering and crafting in the fresh world."
                : "Ordinary movement approached Wren after reopening the crafted world.");
            Press("talk");
            Enter(Stage.Talking);
        }
    }

    private void ObserveTalk(LocalWorldView view)
    {
        if (game.HasPendingAction || view.Dialogue?.SpeakerActorId != _wren)
        {
            Require(_stageTime.ElapsedMilliseconds < 5_000, "The ordinary talk input did not open Wren's dialogue.");
            return;
        }
        Require(view.HasReadingHold, "The addressed conversation did not hold time.");
        _reading = HoldSnapshot(view);
        Enter(Stage.ReadingOffer);
    }

    private void ReadOffer(LocalWorldView view)
    {
        Require(HoldSnapshot(view) == _reading, "Reading changed the calendar, effort, actors or world presentation.");
        if (_stageTime.ElapsedMilliseconds < 600 || !ReadyForInput)
        {
            return;
        }
        LocalQuestView quest = view.Quests.Single();
        Require(quest.Status is null && quest.CanAccept && quest.RequiredQuantity == 6
            && quest.EligibleQuantity == 7 && quest.RewardExperience == 420 && quest.RewardCurrency == 200,
            "The addressed source quest is not an exact funded six-wood offer.");
        _checks.Add("The actual addressed offer remained held for 600 ms and exposed its six-wood requirement and funded reward.");
        Press("give_wood");
        Enter(Stage.Accepting);
    }

    private void ObserveAcceptance(LocalWorldView view)
    {
        Require(HoldSnapshot(view) == _reading, "Quest acceptance advanced time or released the reading hold.");
        if (game.HasPendingAction || view.Quests.SingleOrDefault()?.Status != ActorQuestStatus.Accepted)
        {
            Require(_stageTime.ElapsedMilliseconds < 5_000, "G did not accept the addressed quest.");
            return;
        }
        Require(view.Quests.Single().ParticipationRevision == 1 && view.Quests.Single().CanComplete
            && Quantity(view, _wood) == 7 && view.Progression is { Experience: 0, CurrencyBalance: 0 },
            "Accepting the quest consumed material or awarded a premature reward.");
        if (!ReadyForInput)
        {
            return;
        }
        _checks.Add("First ordinary G input accepted the quest without consuming wood or granting a reward.");
        Press("give_wood");
        Enter(Stage.Completing);
    }

    private void ObserveCompletion(LocalWorldView view)
    {
        Require(HoldSnapshot(view) == _reading, "Quest completion advanced time or changed held world state.");
        if (game.HasPendingAction || view.Quests.SingleOrDefault()?.Status != ActorQuestStatus.Completed)
        {
            Require(_stageTime.ElapsedMilliseconds < 5_000, "Second G did not complete the accepted six-wood delivery.");
            return;
        }
        VerifyReward(view);
        _checks.Add("Second G delivered six current wood once: level 3, XP 38, HP 78/78, MP 14/14 and 200 gil while still held.");
        BeginPause();
    }

    private void ObserveRepeat(LocalWorldView view)
    {
        Require(Snapshot(view) == _beforeRepeat && HoldSnapshot(view) == _reading,
            "Repeated G after recovery duplicated rewards, consumed material or advanced time.");
        if (_stageTime.ElapsedMilliseconds < 600 || !ReadyForInput)
        {
            return;
        }
        _checks.Add("Repeated ordinary G on the restored completed quest left every public field unchanged for 600 ms.");
        Press("talk");
        Enter(Stage.ClosingDialogue);
    }

    private void ObserveClose(LocalWorldView view)
    {
        if (game.HasPendingAction || view.Dialogue is not null)
        {
            Require(_stageTime.ElapsedMilliseconds < 5_000, "Ordinary E did not close the recovered conversation.");
            return;
        }
        Require(!view.HasReadingHold, "The conversation closed without releasing its reading hold.");
        _movementOrigin = Player(view).Feet;
        _movementStartedAt = view.Calendar;
        Enter(Stage.Moving);
    }

    private void ObserveMovement(LocalWorldView view)
    {
        if (!ReadyForInput)
        {
            return;
        }
        SubunitPosition player = Player(view).Feet;
        long dx = player.X - _movementOrigin.X;
        long dy = player.Y - _movementOrigin.Y;
        if ((Int128)dx * dx + (Int128)dy * dy >= 128L * 128 && view.Calendar > _movementStartedAt)
        {
            VerifyReward(view);
            _checks.Add("After E released the hold, ordinary movement travelled at least 128 subunits with advancing calendar and retained rewards.");
            BeginPause();
            return;
        }
        SubunitPosition wren = view.Actors.Single(actor => actor.ActorId == _wren).Feet;
        SetMovement(Direction(player.X - wren.X, player.Y - wren.Y));
        Require(_stageTime.ElapsedMilliseconds < 8_000, "The recovered player could not move after releasing the hold.");
    }

    private void VerifyReward(LocalWorldView view)
    {
        LocalProgressionView progression = view.Progression ?? throw new InvalidOperationException("Actor progression is missing.");
        LocalQuestView quest = view.Quests.Single();
        Require(quest.Status == ActorQuestStatus.Completed && quest.ParticipationRevision == 2
            && progression.Level == 3 && progression.Experience == 38 && progression.Hp == 78
            && progression.MaxHp == 78 && progression.Mp == 14 && progression.MaxMp == 14
            && progression.CurrencyBalance == 200 && progression.CurrencyUnit == "gil",
            "The committed reward is not the actual source leveling, restored vitals and conserved gil.");
        Require(Quantity(view, _wood) == 1 && Quantity(view, _berryItem) == 0
            && view.PlayerInventory.Quantity == 6 && view.GatheringTools.Count == 2
            && view.Crafting?.Revision == 1 && FarmCrafting.Quantity(view, RecipeMaterialRole.Output) == 2
            && FarmCrafting.Quantity(view, RecipeMaterialRole.Waste) == 1,
            "The delivery consumed unrelated tools, rations, offcuts or the final wood.");
    }

    private bool Approach(SubunitPosition player, SubunitPosition target, long range)
    {
        long x = target.X - player.X;
        long y = target.Y - player.Y;
        if ((Int128)x * x + (Int128)y * y <= (Int128)range * range)
        {
            SetMovement(null);
            return true;
        }
        SetMovement(Direction(x, y));
        return false;
    }

    private static string Direction(long x, long y) => Math.Abs(x) > Math.Abs(y)
        ? x < 0 ? "walk_left" : "walk_right" : y < 0 ? "walk_up" : "walk_down";

    private void SetMovement(string? action)
    {
        if (_movement == action)
        {
            return;
        }
        if (_movement is not null)
        {
            Input.ActionRelease(_movement);
        }
        _movement = action;
        if (action is not null)
        {
            Input.ActionPress(action);
        }
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
        if (_stageTime.ElapsedMilliseconds < 250 || game.HasPendingAction)
        {
            return;
        }
        _held = Snapshot(view);
        Enter(Stage.Paused);
    }

    private void ObservePause(LocalWorldView view)
    {
        Require(game.IsLocallyPaused && Snapshot(view) == _held, "The final paused public state changed.");
        if (_stageTime.ElapsedMilliseconds >= 600)
        {
            _checks.Add("Final saved state stayed unchanged for at least 600 ms under the ordinary pause control.");
            Finish(view);
        }
    }

    private void Enter(Stage stage)
    {
        _stage = stage;
        _stageTime.Restart();
    }

    private static LocalActorView Player(LocalWorldView view) => view.Actors.Single(value => value.ActorId == view.PlayerActorId);
    private static ulong Quantity(LocalWorldView view, EntityId definition) => view.PlayerInventory.Lots
        .Where(lot => lot.DefinitionId == definition).Aggregate(0UL, (sum, lot) => checked(sum + lot.Quantity));

    private static object Actors(LocalWorldView view) => view.Actors.Select(actor => new
    {
        Id = actor.ActorId.ToString(), actor.DisplayName, actor.Feet.X, actor.Feet.Y, actor.Feet.Z,
        actor.PresenceRevision, actor.ControllerKind
    });

    private static object Nodes(LocalWorldView view) => view.GatheringNodes.Select(node => new
    {
        Id = node.NodeId.ToString(), node.DisplayName, node.Feet.X, node.Feet.Y, node.Feet.Z,
        node.Revision, node.Kind, node.RequiredTool, node.RemainingSwings, node.Visibility
    });

    private static object? Dialogue(LocalWorldView view) => view.Dialogue is not { } dialogue ? null : new
    {
        Id = dialogue.DialogueId.ToString(), Speaker = dialogue.SpeakerActorId.ToString(), dialogue.SpeakerName, dialogue.Text
    };

    private static string HoldSnapshot(LocalWorldView view) => JsonSerializer.Serialize(new
    {
        Day = view.Calendar.Days, Millisecond = view.Calendar.MillisecondsWithinDay,
        view.PlayerGatherEffort, view.HasReadingHold, Actors = Actors(view), Nodes = Nodes(view), Dialogue = Dialogue(view),
        CraftingRevision = view.Crafting?.Revision
    });

    private static string QuestActionSnapshot(LocalWorldView view)
    {
        using JsonDocument snapshot = JsonDocument.Parse(Snapshot(view));
        JsonElement root = snapshot.RootElement;
        JsonElement retained = root.GetProperty("Retained");
        return JsonSerializer.Serialize(new
        {
            Quantity = retained.GetProperty("Quantity"), Custodian = retained.GetProperty("Custodian"),
            StorageRevision = retained.GetProperty("StorageRevision"), Lots = retained.GetProperty("Lots"),
            Tools = retained.GetProperty("Tools"), CraftingRevision = retained.GetProperty("CraftingRevision"),
            Progression = root.GetProperty("Progression"), Quests = root.GetProperty("Quests")
        });
    }

    internal static string Snapshot(LocalWorldView view) => JsonSerializer.Serialize(new
    {
        Retained = new
        {
            World = view.WorldId.ToString(), Branch = view.BranchId.ToString(), Player = view.PlayerActorId.ToString(),
            Day = view.Calendar.Days, Millisecond = view.Calendar.MillisecondsWithinDay,
            view.PlayerGatherEffort, view.HasReadingHold, Quantity = view.PlayerInventory.Quantity,
            Custodian = view.PlayerInventory.CustodianId.ToString(), StorageRevision = view.PlayerInventory.Revision,
            Lots = view.PlayerInventory.Lots.Select(lot => new { Id = lot.LotId.ToString(), Definition = lot.DefinitionId.ToString(),
                lot.Revision, lot.Quantity, lot.Provenance, lot.AvailableForDelivery }),
            Actors = Actors(view), Nodes = Nodes(view), Dialogue = Dialogue(view),
            Tools = view.GatheringTools.Select(tool => new { Id = tool.LotId.ToString(), tool.Revision, tool.Kind, tool.Power }),
            CraftingRevision = view.Crafting?.Revision,
            Recipes = view.Crafting?.Recipes.Select(recipe => new { Id = recipe.RecipeId.ToString(), Version = recipe.Version.Value,
                recipe.Key, Terms = recipe.Terms.Select(term => new { Definition = term.DefinitionId.ToString(), term.Quantity, term.Role }) })
        },
        view.Progression,
        Quests = view.Quests.Select(quest => new { Id = quest.DefinitionId.ToString(), Version = quest.Version.Value,
            quest.Title, quest.Description, Giver = quest.GiverActorId.ToString(), Item = quest.ItemDefinitionId.ToString(),
            quest.RequiredQuantity, quest.EligibleQuantity, quest.Status, quest.ParticipationRevision, quest.MinimumLevel,
            quest.RewardExperience, quest.RewardCurrency, quest.CurrencyUnit, quest.CanAccept, quest.CanComplete })
    });

    private string Output(string stage, string extension) => Path.ChangeExtension(reportPath, "." + stage + "." + extension);

    private void Finish(LocalWorldView view)
    {
        if (mode == "fresh")
        {
            Require(view.HasReadingHold && view.Construction is [{ CompletedWork: 0, IsCompleted: false }],
                "The completed fresh quest must retain its real reading hold and exactly zero construction work.");
            _checks.Add("Fresh-world crafting, acceptance and delivery completed with a real held conversation and no premature construction.");
        }
        if (DisplayServer.GetName() != "headless")
        {
            using Image image = GetViewport().GetTexture().GetImage();
            Require(image.SavePng(Output(mode, "png")) == Error.Ok, "Native quest capture failed.");
        }
        string report = JsonSerializer.Serialize(new
        {
            Kind = "native-farm-quests-v1", Mode = mode, Status = "passed", Runtime = RuntimeInformation.FrameworkDescription,
            DisplayDriver = DisplayServer.GetName(), Initial = _initial, Final = Snapshot(view),
            WoodDefinition = _wood.ToString(), BerryDefinition = _berryItem.ToString(), Checks = _checks,
            Inputs = _inputs, Readiness = _readiness, FocusRequests = _focusRequests,
            game.IsLocallyPaused, game.IsWindowFocused, DurationMilliseconds = _timeout.ElapsedMilliseconds
        }, new JsonSerializerOptions { WriteIndented = true });
        if (mode == "fresh")
        {
            var expanded = System.Text.Json.Nodes.JsonNode.Parse(report)!;
            expanded["OutsideQuestAction"] = JsonSerializer.SerializeToNode(new
            {
                Before = _outsideQuestBefore, After = _outsideQuestAfter, ObservationMilliseconds = _outsideQuestMilliseconds,
                Samples = _outsideQuestSamples, HasPendingActionAfterInput = _outsidePendingAfterInput,
                HasPendingActionAfterObservation = _outsidePendingAfterObservation
            });
            report = expanded.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
        File.WriteAllText(Output(mode, "json"), report + "\n");
        _stage = Stage.Done;
        GD.Print("Native quests passed: " + Output(mode, "json"));
        game.RequestClose();
    }

    private void Fail(Exception error)
    {
        SetMovement(null);
        File.WriteAllText(Output(mode, "failed.json"), JsonSerializer.Serialize(new
        {
            Status = "failed", Mode = mode, Stage = _stage.ToString(), Error = error.ToString(),
            Inputs = _inputs, Readiness = _readiness, FocusRequests = _focusRequests,
            game.IsLocallyPaused, game.IsWindowFocused, Current = game.CurrentView is { } view ? Snapshot(view) : null
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        _stage = Stage.Done;
        GD.PushError("Native quest probe failed: " + error);
        GetTree().Quit(1);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
