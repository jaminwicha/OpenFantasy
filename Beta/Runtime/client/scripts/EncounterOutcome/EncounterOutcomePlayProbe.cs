using System.Diagnostics;
using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.NativeCargo;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.EncounterOutcome;

namespace OpenFantasy.Client;

/// <summary>
/// Disposable native outcome journey over an exported fixture that starts paused. Through ordinary injected input only:
/// <c>victory</c> walks north on the lane until the wounded chief enters under its own control, strikes with E until the
/// episode resolves, reads the outcome panel, continues with E, and after the world completes the encounter detours around
/// the camp and sells the carried ration at Kegga; <c>defeat</c> meets the production chief, strikes until incapacitated,
/// reads why ordinary action and care are unavailable, accepts with E and leaves through the terminal panel's Save and
/// leave button; <c>care</c> and <c>care-wheel</c> start from the same prepared fixture, whose genuine managed approach and
/// the player's own managed entry stop at the Active episode's first decision at 1 HP, so they do not exercise a native
/// care-specific approach or entry: they wait with G until the wolf's real bite, see Wren's admitted care and decline it,
/// <c>care</c> with the HUD's Accept defeat button (no E, which asks for care) and <c>care-wheel</c> through the Actions
/// wheel's right sector; <c>reopen</c> only opens the defeat save paused and reports.
/// Every world command is observed at the client's submission boundary: before each injected choice the presented offer
/// must be current for the view, the input must produce exactly one submission of that presented offer, and its exact
/// command bytes and receipt are reported for authentication against the saved journal. Each observed state is captured
/// first; then the post-draw bounds of the outcome panel and of the HUD (header, footer and every visible footer label and
/// button, target bar) are checked against the viewport, unclipped text and both combatants' rendered sprites.
/// </summary>
internal sealed partial class EncounterOutcomePlayProbe : Node
{
    private const double OpeningSeconds = 30;
    private const int MaximumReplans = 6;
    /// <summary>The bounded number of submissions one route may observe; every route needs far fewer.</summary>
    private const int MaximumSubmissions = 128;
    private readonly FarmGame _game;
    private readonly EncounterOutcomeOverlay _overlay;
    private readonly string _reportPath;
    private readonly string _metadataPath;
    private readonly string _genesisPath;
    private readonly string _mode;
    private readonly MarketProbeDriver _driver;
    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private readonly List<string> _checks = [];
    private readonly List<string> _images = [];
    private readonly List<object> _walkInputs = [];
    private readonly List<object> _panels = [];
    private readonly List<object> _hudBounds = [];
    private readonly List<object> _wheelBounds = [];
    private readonly List<(string Stage, FarmSubmission Submission, ulong Frame)> _submitted = [];
    private readonly List<object> _selections = [];
    private bool _submissionOverflow;
    private string _stage = "opening";
    private double _openingSeconds;
    private bool _observedNativeFocus;
    private int _strikes;
    private int _waits;
    private object? _engagement;
    private object? _outcome;
    private object? _completed;
    private object? _trade;
    private object? _decline;
    private Key? _held;

    private sealed record OfferFacts(FarmSemanticAction Action, string Label, string TargetName, EntityId? TargetId,
        FarmActionScope Scope, WorldActionIntent? Intent, EntityId? CommandId);

    internal EncounterOutcomePlayProbe(FarmGame game, EncounterOutcomeOverlay overlay, string reportPath, string genesisPath,
        string metadataPath, string mode)
    {
        _game = game;
        _overlay = overlay;
        _reportPath = reportPath;
        _genesisPath = genesisPath;
        _metadataPath = metadataPath;
        _mode = mode;
        _driver = new MarketProbeDriver(this, game, "keyboard", () => _stage, mode == "victory" ? 690 : 290);
    }

    private LocalWorldView View => _game.CurrentView ?? throw new InvalidOperationException("No public world.");
    private FarmQuickWheel Wheel => _game.GetNode<FarmQuickWheel>("FarmQuickWheel");
    private FarmMarketPanel Panel => _game.GetNode<FarmMarketPanel>("FarmMarketPanel");
    private FarmHud Hud => _game.GetNode<FarmHud>("FarmHud");
    private Label Status => Hud.GetNode<Label>("%EncounterStatus");
    private T Node<T>(string name) where T : Node => Panel.GetNode<T>("%" + name);
    private SubunitPosition Feet => View.Actors.Single(actor => actor.ActorId == View.PlayerActorId).Feet;
    private NativeCargoStall Kegga => _game.CargoStalls.Single(stall => stall.MerchantSourceId == CargoCircuitSources.DestinationMerchantId);
    private NativeCargoStall Marla => _game.CargoStalls.Single(stall => stall.MerchantSourceId == CargoCircuitSources.OriginMerchantId);
    private LocalMarketStallView StallOf(NativeCargoStall stall) => View.Markets!.Stalls.Single(value => value.MarketId == stall.MarketId);
    private ulong CarriedRations => StallOf(Marla).OwnLots.Aggregate(0UL, (sum, lot) => sum + lot.Quantity);
    private ulong EncounterCount => View.EncounterLifecycle!.EncounterCount;

    public override void _Process(double delta)
    {
        if (DisplayServer.GetName() != "headless" && GetWindow().HasFocus()) _observedNativeFocus = true;
    }

    public override async void _Ready()
    {
        try
        {
            Trace("Waiting for the world to open.");
            // Passive: the game's submission boundary reports each command; the probe never submits or retries one itself.
            _game.ObserveSubmissions(Observe);
            await _driver.Until(() => _game.CurrentView?.Retreat is not null && _game.Canvas.Regional is not null,
                "The outcome cargo world did not open.", OpeningSeconds);
            _openingSeconds = _watch.Elapsed.TotalSeconds;
            Trace("World opened.");
            _game.InjectFixtureFocus(true);
            using var metadata = JsonDocument.Parse(File.ReadAllText(_metadataPath));
            string fixture = _mode == "care-reopen" ? "care-accepted" : _mode == "reopen" ? "defeat" : _mode == "care-wheel" ? "care" : _mode;
            Require(metadata.RootElement.GetProperty("Kind").GetString() == "native-encounter-outcome-fixture-v1"
                && metadata.RootElement.GetProperty("Route").GetString() == fixture
                && metadata.RootElement.GetProperty("PlayerActorId").GetString() == View.PlayerActorId.ToString(),
                "The fixture metadata names another fixture, route, world or player.");
            EntityId opponent = View.Actors.Single(actor => actor.ActorId.ToString()
                == metadata.RootElement.GetProperty("OpponentActorId").GetString()).ActorId;
            Require(_game.IsLocallyPaused, "The outcome fixture must open paused.");
            switch (_mode)
            {
                case "care-accepted":
                    await AcceptCare(opponent);
                    break;
                case "care-reopen":
                    await Reopen(opponent);
                    break;
                case "reopen":
                    await Reopen(opponent);
                    break;
                case "victory":
                    await Victory(opponent);
                    break;
                case "defeat":
                    await Defeat(opponent);
                    break;
                default:
                    await DeclineCare(opponent, wheel: _mode == "care-wheel");
                    break;
            }
            RequireAccountedSubmissions();
            if (_mode == "defeat")
            {
                // Save and leave is the deliberate exit: the report is written first, then the panel's button closes the world.
                Report("passed", null);
                SetStage("leave");
                await _driver.Click(_overlay.LeaveButton);
                return;
            }
            Require(_game.IsLocallyPaused && !_game.HasPendingAction && !_game.IsMarketOpen, "The journey did not end paused and closed.");
            Report("passed", null);
            _game.RequestClose();
        }
        catch (Exception error)
        {
            Release();
            Report("failed", error.ToString());
            GD.PushError("Native outcome probe failed: " + error);
            GetTree().Quit(1);
        }
    }

    private async Task Victory(EntityId chief)
    {
        Require(!View.IsInEncounter && EncounterCount == 0 && CarriedRations == 1, "The victory fixture starts on the road carrying one ration.");
        await Resume();
        await ApproachUntilEngaged();
        await StrikeUntilOutcome(chief);
        LocalEncounterOutcomeView outcome = View.EncounterOutcome!;
        Require(outcome is { Stage: LocalOutcomeStage.AwaitingYourReading, Standing: LocalOutcomeStanding.YourSideStands }
            && outcome.Acknowledgment is not null, "The resolved victory is not awaiting the player's reading.");
        await ReadPanel("victory-outcome", chief);
        SetStage("continue");
        FarmActionOffer read = _game.CurrentOffers[FarmSemanticAction.ReleaseEncounter];
        Require(read is { CanExecute: true, Intent: AcknowledgeEncounterOutcomeIntent } && read.Label == "Continue",
            "Continue is not the executable reading of this outcome.");
        await Submit(FarmSemanticAction.ReleaseEncounter, "key-E", () => _driver.Tap(Key.E));
        await _driver.Until(() => View.EncounterOutcome is { Stage: LocalOutcomeStage.Completed } && !View.IsInEncounter
            && !_game.HasPendingAction, "Continuing did not let the world complete the encounter.", 10);
        await _driver.Frame();
        Require(!_overlay.IsShowingOutcome, "The outcome panel stayed open after a completed victory.");
        _completed = new { View.EncounterOutcome!.CompletedAtSequence, Stage = View.EncounterOutcome.Stage.ToString(),
            EncounterCount, Hp = View.Progression!.Hp, Seconds = _watch.Elapsed.TotalSeconds };
        await CheckHud("victory-completed", chief);
        _checks.Add("Continue (E) read the victory; the world completed the encounter and returned the player to exploration.");

        SetStage("travel");
        WorldSnapshot genesis = WorldSnapshotCodec.Decode(File.ReadAllBytes(_genesisPath));
        CargoCorridor corridor = NativeCargoWorld.Corridor(genesis);
        await DetourAndSell(corridor, new CellCoordinate(Feet.Cell.X, Feet.Cell.Y, Feet.Cell.Z - 1));
        SetStage("pause");
        _driver.Tap(Key.Escape);
        await _driver.Until(() => _game.IsLocallyPaused, "The journey could not pause.");
        await Capture("final");
    }

    private async Task Defeat(EntityId chief)
    {
        Require(!View.IsInEncounter && EncounterCount == 0, "The defeat fixture starts on the road in exploration.");
        await Resume();
        await ApproachUntilEngaged();
        await StrikeUntilOutcome(chief);
        LocalEncounterOutcomeView outcome = View.EncounterOutcome!;
        Require(outcome is { Stage: LocalOutcomeStage.AwaitingYourReading, Standing: LocalOutcomeStanding.OpponentStands,
            Restriction: LocalOutcomeRestriction.Incapacitated } && View.Progression!.Hp == 0, "The defeat is not awaiting the player's reading.");
        Require(View.Care is null || View.Care.Offers.All(offer => !offer.CanAcknowledge), "The road defeat unexpectedly offers care.");
        await ReadPanel("defeat-outcome", chief);
        SetStage("accept");
        FarmActionOffer read = _game.CurrentOffers[FarmSemanticAction.ReleaseEncounter];
        Require(read is { CanExecute: true, Intent: AcknowledgeEncounterOutcomeIntent } && read.Label == "Accept defeat",
            "Accept defeat is not the executable reading of this defeat.");
        await Submit(FarmSemanticAction.ReleaseEncounter, "key-E", () => _driver.Tap(Key.E));
        await _driver.Until(() => View.EncounterOutcome is { IsTerminal: true } && !_game.HasPendingAction,
            "Accepting the defeat did not complete the encounter.", 10);
        await Terminal("terminal", chief);
    }

    /// <summary>
    /// Fixture preparation ends at the player's first decision boundary of the wolf encounter, after a genuine managed
    /// approach and the player's own managed entry at 1 HP beside Wren; every combat choice, the resolution, the reading
    /// that declines the offered care and the completion are native input from here.
    /// </summary>
    private async Task DeclineCare(EntityId wolf, bool wheel)
    {
        Require(View.IsInEncounter && EncounterCount == 1 && View.Progression!.Hp == 1 && View.EncounterOutcome is null,
            "The care fixture starts at 1 HP at the player's first boundary of the wolf encounter.");
        _engagement = new { EncounterCount, HelperDistance = HelperDistance(), Seconds = _watch.Elapsed.TotalSeconds };
        await Resume();
        SetStage("wait");
        var fight = Stopwatch.StartNew();
        while (View.EncounterOutcome is null)
        {
            Require(fight.Elapsed.TotalSeconds < 120 && _waits <= 12, "The wolf's real bite never resolved the encounter.");
            await _driver.Until(() => View.EncounterOutcome is not null || CanChoose(FarmSemanticAction.Wait),
                "Neither a lawful Wait nor the outcome arrived.", 30);
            if (View.EncounterOutcome is not null) break;
            ulong timing = View.Encounter!.TimingRevision;
            _waits++;
            await Submit(FarmSemanticAction.Wait, "key-G", () => _driver.Tap(Key.G));
            await _driver.Until(() => View.EncounterOutcome is not null
                || !_game.HasPendingAction && View.Encounter is { } encounter && encounter.TimingRevision != timing,
                "Wait did not reach the next boundary.", 30);
        }
        LocalEncounterOutcomeView outcome = View.EncounterOutcome!;
        await _driver.Until(() => View.Care?.Offers.Any(offer => offer.CanAcknowledge) == true
            && CanChoose(FarmSemanticAction.AcknowledgeCare), "Wren's admitted care was not offered: care "
            + (View.Care is null ? "view absent" : View.Care.Status + " " + string.Join(",", View.Care.Offers.Select(offer => offer.Rejection)))
            + $", helper distance {HelperDistance()} subunits.");
        Require(outcome is { Standing: LocalOutcomeStanding.OpponentStands, Restriction: LocalOutcomeRestriction.Incapacitated }
            && View.Progression!.Hp == 0, "The wolf's bite did not incapacitate the player.");
        string helper = View.Care!.Offers.First(offer => offer.CanAcknowledge).HelperName;
        string label = wheel ? "care-wheel-outcome" : "care-outcome";
        await ReadPanel(label, wolf);
        Require(_overlay.Panel.GetNode<Label>("%Body").Text.Contains(helper + " can help now", StringComparison.Ordinal)
            && _overlay.Panel.GetNode<Label>("%Body").Text.Contains("Accepting declines that care", StringComparison.Ordinal),
            "The panel does not name the available care and what accepting declines.");
        Require(_game.CurrentOffers[FarmSemanticAction.ReleaseEncounter] is { CanExecute: true, Label: "Accept defeat",
            Intent: AcknowledgeEncounterOutcomeIntent }, "The outcome acceptance is not offered beside care.");

        // The HUD keeps the two choices independent: E / A names care; the acceptance names itself and no key.
        Button accept = Hud.GetNode<Button>("Root/Footer/Column/Actions/ReleaseEncounter");
        Button care = Hud.GetNode<Button>("Root/Footer/Column/Actions/AcknowledgeCare");
        await _driver.Until(() => accept.IsVisibleInTree() && !accept.Disabled && care.IsVisibleInTree() && !care.Disabled,
            "The HUD does not present both care and acceptance.");
        Require(care.Text == "[E]" && accept.Text == "Accept defeat" && !accept.Text.Contains("[E]", StringComparison.Ordinal)
            && accept.TooltipText.EndsWith(EncounterOutcomeText.ReadingBesideCareRoutes, StringComparison.Ordinal),
            $"The HUD key hints are misleading: care '{care.Text}', acceptance '{accept.Text}'.");
        string hint = Hud.GetNode<Label>("Root/Footer/Column/Actions/Movement").Text;
        Require(hint.EndsWith("\nE / A: care · V: choices", StringComparison.Ordinal), "The footer hint does not name what each key does: " + hint);
        if (wheel) await DeclineThroughWheel(helper, accept);
        else await DeclineThroughHudButton(helper, accept, wolf);
        Require(View.Care is null || View.Care.Offers.All(offer => !offer.CanAcknowledge), "Care was still offered after completion.");
        // The presented offers follow the published view on the next frame; a care choice must not survive it.
        await _driver.Until(() => !_game.CurrentOffers.TryGetValue(FarmSemanticAction.AcknowledgeCare, out FarmActionOffer? offered)
            || !offered.CanExecute, "A care choice survived the completed encounter.", 3);
        Require(_submitted.All(row => row.Submission.Offer.Intent is not OpenFantasy.Core.Care.AcknowledgeCareIntent),
            "A care request was submitted although the route declined care.");
        _checks.Add(wheel
            ? $"{helper}'s admitted care was offered; the Actions wheel's Accept defeat sector declined it and the world completed the encounter."
            : $"{helper}'s admitted care was offered; the HUD's Accept defeat declined it and the world completed the encounter.");
        await Terminal(wheel ? "care-wheel-declined" : "care-declined", wolf);
        SetStage("pause");
        _driver.Tap(Key.Escape);
        await _driver.Until(() => _game.IsLocallyPaused, "The journey could not pause.");
        await Capture("final");
    }

    /// <summary>The HUD's acceptance, chosen by pointer; hovering first shows its one-line preview within the footer.</summary>
    private async Task DeclineThroughHudButton(string helper, Button accept, EntityId wolf)
    {
        SetStage("decline");
        string label = accept.Text;
        string tooltip = accept.TooltipText;
        await Submit(FarmSemanticAction.ReleaseEncounter, "hud-pointer", async () =>
        {
            await _driver.Hover(accept);
            await _driver.Delay(0.2);
            Require(Hud.GetNode<Label>("%ActionPreview").Text == "Accept defeat: Defeated · No cost",
                "The hovered acceptance does not preview in one compact line.");
            await CheckHud("care-hover", wolf);
            await _driver.Click(accept);
        });
        await WaitForTerminalCompletion();
        _decline = new { Helper = helper, Route = "hud-pointer", Button = label, Tooltip = tooltip,
            Terminal = View.EncounterOutcome!.IsTerminal, Seconds = _watch.Elapsed.TotalSeconds };
    }

    /// <summary>The Actions wheel shows care (left) and the acceptance (right) together; Right then Enter chooses acceptance.</summary>
    private async Task DeclineThroughWheel(string helper, Button accept)
    {
        SetStage("wheel-decline");
        string label = accept.Text;
        string? sector = null;
        await Submit(FarmSemanticAction.ReleaseEncounter, "wheel-keyboard", async () =>
        {
            _driver.Tap(Key.V);
            await _driver.Until(() => _game.IsWheelOpen && Wheel.Model is not null, "The Actions wheel did not open.");
            FarmQuickWheelModel model = Wheel.Model!;
            Require(model.Selected is null && model.PageName == "Actions", "Opening the wheel preselected a choice.");
            Require(model.Slots[1] is { Action: FarmSemanticAction.ReleaseEncounter } reading
                && reading.Offer.Intent is AcknowledgeEncounterOutcomeIntent
                && model.Slots[3] is { Action: FarmSemanticAction.AcknowledgeCare }
                && model.Slots[0] is null && model.Slots[2] is null, "The wheel does not offer care and acceptance side by side.");
            sector = FarmQuickWheelModel.SectorLabel(model.Slots[1]!);
            Require(sector == "Accept defeat" && FarmQuickWheelModel.SectorLabel(model.Slots[3]!) == "Care",
                "The wheel's sectors are not worded Accept defeat and Care.");
            await CheckWheel("wheel");
            _driver.Tap(Key.Right);
            await _driver.Until(() => Wheel.Model?.Selected?.Action == FarmSemanticAction.ReleaseEncounter,
                "Right did not select the acceptance sector.");
            Require(Wheel.Model!.Selected!.Offer is { CanExecute: true, Intent: AcknowledgeEncounterOutcomeIntent },
                "The selected sector is not the executable acceptance.");
            await CheckWheel("wheel-accept");
            _driver.Tap(Key.Enter);
        });
        Require(!_game.IsWheelOpen, "Confirming did not close the wheel.");
        await WaitForTerminalCompletion();
        _decline = new { Helper = helper, Route = "wheel-keyboard", Sector = sector, Button = label,
            Terminal = View.EncounterOutcome!.IsTerminal, Seconds = _watch.Elapsed.TotalSeconds };
    }

    private async Task WaitForTerminalCompletion()
    {
        Trace("Waiting for terminal completion.");
        await _driver.Until(() => View.EncounterOutcome is { IsTerminal: true } && !_game.HasPendingAction,
            "Declining care by accepting the defeat did not complete the encounter.", 10);
        Trace("Terminal completion observed.");
    }

    private async Task Reopen(EntityId chief)
    {
        SetStage("reopen");
        await _driver.Until(() => _overlay.IsShowingOutcome, "The reopened terminal world shows no outcome panel.");
        Require(View.EncounterOutcome is { IsTerminal: true, Stage: LocalOutcomeStage.Completed } && !View.IsInEncounter
            && View.Progression!.Hp == 0, "The reopened save is not the completed terminal defeat.");
        await CheckPanel("reopen", chief, terminal: true);
        _checks.Add("The saved terminal defeat reopens paused in a separate process with the same completed record.");
    }

    private async Task Resume()
    {
        SetStage("resume");
        _driver.Tap(Key.Escape);
        await _driver.Until(() => !_game.IsLocallyPaused, "Ordinary input did not resume world time.");
    }

    /// <summary>Held Up walks the lane until the chief begins the encounter under its own control within its reach.</summary>
    private async Task ApproachUntilEngaged()
    {
        SetStage("approach");
        var walking = Stopwatch.StartNew();
        while (!View.IsInEncounter)
        {
            Require(walking.Elapsed.TotalSeconds < 45, "Walking north never met the chief.");
            Hold(Key.Up);
            await _driver.Frame();
        }
        Release();
        Require(EncounterCount == 1, "The encounter is not the chief's first.");
        _engagement = new { EncounterCount, Feet = new { Feet.X, Feet.Y, Feet.Z }, Seconds = _watch.Elapsed.TotalSeconds };
        await Capture(_mode + "-engaged");
    }

    /// <summary>E at each of the player's own due boundaries (Q first if the Strike addresses another target).</summary>
    private async Task StrikeUntilOutcome(EntityId opponent)
    {
        SetStage("strike");
        var fight = Stopwatch.StartNew();
        while (View.EncounterOutcome is null)
        {
            Require(fight.Elapsed.TotalSeconds < 150 && _strikes <= 40, "The fight never resolved.");
            await _driver.Until(() => View.EncounterOutcome is not null || CanChoose(FarmSemanticAction.Strike),
                "Neither a Strike boundary nor the outcome arrived.", 30);
            if (View.EncounterOutcome is not null) break;
            for (int presses = 0; _game.CurrentOffers[FarmSemanticAction.Strike].TargetId != opponent; presses++)
            {
                Require(presses < 10, "Target cycling never addressed the opponent.");
                _driver.Tap(Key.Q);
                await _driver.Delay(0.1);
            }
            if (!CanChoose(FarmSemanticAction.Strike) || View.EncounterOutcome is not null) continue;
            ulong timing = View.Encounter!.TimingRevision;
            _strikes++;
            await Submit(FarmSemanticAction.Strike, "key-E", () => _driver.Tap(Key.E));
            await _driver.Until(() => View.EncounterOutcome is not null
                || !_game.HasPendingAction && View.Encounter is { } encounter && encounter.TimingRevision != timing,
                "The strike did not reach the next boundary.", 30);
        }
        await _driver.Until(() => !_game.HasPendingAction, "The resolved fight did not settle.");
    }

    /// <summary>Wren's centre distance from the player, for the report (her remedy reaches one cell, 256 subunits).</summary>
    private double HelperDistance()
    {
        SubunitPosition helper = View.Actors.Single(actor => actor.ActorId == _game.CompanionActorId).Feet;
        return helper.Z != Feet.Z ? double.MaxValue : Math.Sqrt(Math.Pow(helper.X - Feet.X, 2) + Math.Pow(helper.Y - Feet.Y, 2));
    }

    private bool CanChoose(FarmSemanticAction action) => !_game.HasPendingAction
        && _game.CurrentOffers.TryGetValue(action, out FarmActionOffer? offer) && offer.CanExecute;

    private void Observe(FarmSubmission submission)
    {
        // The seam must stay passive: an overflow is recorded and fails the route at its next check, never thrown here.
        if (_submitted.Count >= MaximumSubmissions) _submissionOverflow = true;
        else _submitted.Add((_stage, submission, Engine.GetProcessFrames()));
    }

    private Task Submit(FarmSemanticAction action, string route, Action inject) => Submit(action, route, () =>
    {
        inject();
        return Task.CompletedTask;
    });

    /// <summary>
    /// Proves the presented choice is current for the published view, performs the injected input and requires exactly one
    /// new submission whose offer is that presented choice (nothing reselected or retried), then its applied receipt.
    /// </summary>
    private async Task Submit(FarmSemanticAction action, string route, Func<Task> inject)
    {
        await _driver.Until(() => CurrentPresentation(action) is not null, $"The presented {action} is not current for the published view.", 5);
        FarmActionOffer presented = CurrentPresentation(action)!;
        OfferFacts expected = Facts(presented);
        ulong readyFrame = Engine.GetProcessFrames();
        var ready = new { Stage = _stage, Route = route, Action = action.ToString(), Frame = readyFrame, View.Calendar.MillisecondsWithinDay,
            ActorRevision = presented.Scope.ExpectedActorRevision, Timing = View.EncounterActions?.TimingRevision };
        int before = _submitted.Count;
        Trace($"Injecting {action}.");
        await inject();
        await _driver.Until(() => _submitted.Count > before || _submissionOverflow, $"The {action} input reached no submission.", 10);
        await _driver.Frame();
        Require(!_submissionOverflow && _submitted.Count == before + 1, $"The {action} input produced {_submitted.Count - before} submissions.");
        (string _, FarmSubmission submission, ulong submittedFrame) = _submitted[^1];
        // The pre-input presentation, completed with the identity the boundary actually generated, must be the exact
        // canonical command that was submitted: every nested fact, not a label or a diagnostic text.
        byte[] expectedCommand = WorldActionCodec.Encode(presented.ToCommand(submission.CommandId));
        Require(!submission.IsRetry && Facts(submission.Offer) == expected
            && expectedCommand.AsSpan().SequenceEqual(submission.CanonicalCommandBytes()), $"The submitted {action} is not the presented choice.");
        await _driver.Until(() => submission.Receipt.IsCompleted, $"The host did not answer the {action} submission.", 30);
        Require(submission.Receipt.IsCompletedSuccessfully && submission.Receipt.Result is { Status: CommandReceiptStatus.Applied },
            $"The {action} submission was not applied: " + (submission.Receipt.IsCompletedSuccessfully
                ? submission.Receipt.Result.Rejection.ToString() : submission.Receipt.Exception?.GetBaseException().Message));
        Trace($"Applied {action}.");
        _selections.Add(new { Ready = ready, CommandId = submission.CommandId.ToString(), SubmittedFrame = submittedFrame,
            ExpectedCommandBytes = Convert.ToHexString(expectedCommand), SameOfferObject = ReferenceEquals(submission.Offer, presented) });
    }

    /// <summary>The presented offer for the action when it is executable and read from the current published view.</summary>
    private FarmActionOffer? CurrentPresentation(FarmSemanticAction action)
    {
        if (_game.HasPendingAction || !_game.CurrentOffers.TryGetValue(action, out FarmActionOffer? offer) || !offer.CanExecute
            || offer.Intent is null)
            return null;
        LocalWorldView view = View;
        if (offer.Scope.ActorId != view.PlayerActorId || offer.Scope.WorldId != view.WorldId || offer.Scope.BranchId != view.BranchId
            || offer.Scope.ExpectedActorRevision != view.Actors.Single(actor => actor.ActorId == view.PlayerActorId).PresenceRevision)
            return null;
        bool current = offer.Intent switch
        {
            QueueEncounterAbilityIntent ability => view.EncounterActions is { } actions && ability.EncounterId == actions.EncounterId
                && ability.ExpectedTimingRevision == actions.TimingRevision,
            QueueEncounterWaitIntent wait => view.EncounterActions is { } actions && wait.EncounterId == actions.EncounterId
                && wait.ExpectedTimingRevision == actions.TimingRevision,
            AcknowledgeEncounterOutcomeIntent reading => reading == view.EncounterOutcome?.Acknowledgment,
            _ => true
        };
        return current ? offer : null;
    }

    private static OfferFacts Facts(FarmActionOffer offer) => new(offer.Action, offer.Label, offer.TargetName, offer.TargetId,
        offer.Scope, offer.Intent, offer.CommandId);

    /// <summary>Every observed submission is one checked selection; nothing else reached the world-command boundary.</summary>
    private void RequireAccountedSubmissions()
    {
        Require(!_submissionOverflow && _submitted.Count == _selections.Count && _submitted.All(row => !row.Submission.IsRetry),
            $"Observed {_submitted.Count} submissions for {_selections.Count} checked selections.");
        Require(_game.SubmissionObservationFailure is null, "The submission observer failed: " + _game.SubmissionObservationFailure);
        Require(_mode is not ("reopen" or "care-reopen") || _submitted.Count == 0, "The paused reopen submitted a command.");
    }

    /// <summary>The unfinalized outcome panel: visible, readable and clear of the HUD and both combatants.</summary>
    private async Task ReadPanel(string label, EntityId opponent)
    {
        SetStage("read");
        await _driver.Until(() => _overlay.IsShowingOutcome, "The outcome panel did not appear.");
        // Pause for reading: the world holds at the outcome while the panel is read (Full Wait).
        var held = (View.Encounter?.TimingRevision, View.Markets?.AdvancingStep, Feet, View.Progression!.Hp);
        await _driver.Delay(0.6);
        Require((View.Encounter?.TimingRevision, View.Markets?.AdvancingStep, Feet, View.Progression!.Hp) == held
            && View.EncounterOutcome!.Stage == LocalOutcomeStage.AwaitingYourReading, "The world did not hold at the unread outcome.");
        _outcome = new { Title = _overlay.Panel.GetNode<Label>("%Title").Text, Body = _overlay.Panel.GetNode<Label>("%Body").Text,
            Status = Status.Text, StatusTooltip = Status.TooltipText, Result = View.EncounterOutcome!.Result.ToString(),
            Standing = View.EncounterOutcome.Standing.ToString(), Hp = View.Progression!.Hp, Seconds = _watch.Elapsed.TotalSeconds };
        await CheckPanel(label, opponent, terminal: false);
        _checks.Add($"The {label} panel stated the result and reason while the world held, clear of the HUD and combatants.");
    }

    private async Task Terminal(string label, EntityId opponent)
    {
        await _driver.Until(() => _overlay.IsShowingOutcome && _overlay.LeaveButton.IsVisibleInTree(), "The terminal panel did not appear.");
        string body = _overlay.Panel.GetNode<Label>("%Body").Text;
        Require(body.Contains(EncounterOutcomeText.CompletionPolicy, StringComparison.Ordinal) && !body.Contains("reach", StringComparison.Ordinal),
            "The terminal panel does not state the completion policy.");
        // The HUD keeps a compact status and hint; the panel and the status tooltip carry the full explanation.
        Require(Status.Text.Contains(EncounterOutcomeText.TerminalStatus(View.EncounterOutcome!), StringComparison.Ordinal)
            && !Status.Text.Contains(EncounterOutcomeText.CompletionPolicy, StringComparison.Ordinal)
            && Status.TooltipText.Contains(EncounterOutcomeText.CompletionPolicy, StringComparison.Ordinal)
            && Hud.GetNode<Label>("Root/Footer/Column/Actions/Movement").Text == EncounterOutcomeText.TerminalHint,
            "The terminal HUD status, its tooltip or the hint is not the compact form: " + Status.Text);
        _completed = new { View.EncounterOutcome!.CompletedAtSequence, Stage = View.EncounterOutcome.Stage.ToString(), EncounterCount,
            Hp = View.Progression!.Hp, Body = body, Status = Status.Text, Seconds = _watch.Elapsed.TotalSeconds };
        await CheckPanel(label, opponent, terminal: true);
        _checks.Add("The terminal panel explains incapacity and the completion policy and offers Save and leave.");
    }

    /// <summary>
    /// Captures the observed frame first, then checks the actual post-draw bounds of the panel against the viewport, HUD
    /// panels and both combatants' sprites, and of the whole HUD (<see cref="CheckHud"/>).
    /// </summary>
    private async Task CheckPanel(string label, EntityId opponent, bool terminal)
    {
        await Capture(label);
        await _driver.Frame();
        Rect2 viewport = GetViewport().GetVisibleRect();
        Rect2 panel = _overlay.Panel.GetGlobalRect();
        Label body = _overlay.Panel.GetNode<Label>("%Body");
        Rect2 field = Hud.FieldViewportBounds;
        Control targetBar = Hud.GetNode<Control>("Root/TargetBar");
        Rect2? player = _game.Canvas.ActorSpriteViewportBounds(View.PlayerActorId);
        Rect2? other = _game.Canvas.ActorSpriteViewportBounds(opponent);
        bool inField = field.Encloses(panel);
        bool clearOfTarget = !targetBar.IsVisibleInTree() || !targetBar.GetGlobalRect().Intersects(panel);
        bool clearOfPlayer = player is not { } p || !p.Intersects(panel);
        bool clearOfOpponent = other is not { } o || !o.Intersects(panel);
        bool readable = body.Text.Length > 0 && panel.Encloses(body.GetGlobalRect()) && body.GetVisibleLineCount() >= body.GetLineCount();
        bool leave = !terminal || panel.Encloses(_overlay.LeaveButton.GetGlobalRect()) && !_overlay.LeaveButton.Disabled;
        _panels.Add(new { Label = label, Panel = Rect(panel), Field = Rect(field), Viewport = Rect(viewport),
            TargetBar = targetBar.IsVisibleInTree() ? Rect(targetBar.GetGlobalRect()) : null,
            PlayerSprite = player is { } a ? Rect(a) : null, OpponentSprite = other is { } b ? Rect(b) : null,
            Body = Rect(body.GetGlobalRect()), Lines = body.GetLineCount(), VisibleLines = body.GetVisibleLineCount(),
            Leave = terminal ? Rect(_overlay.LeaveButton.GetGlobalRect()) : null,
            InViewport = viewport.Encloses(panel), InField = inField, ClearOfTargetBar = clearOfTarget, ClearOfPlayer = clearOfPlayer,
            ClearOfOpponent = clearOfOpponent, Readable = readable, LeaveShown = leave });
        string hud = MeasureHud(label, opponent);
        Require(viewport.Encloses(panel) && inField && clearOfTarget && clearOfPlayer && clearOfOpponent && readable && leave,
            $"The {label} panel is clipped or occludes the HUD or a combatant.");
        Require(player is not null && other is not null, "The combatants' sprites were not drawn while the panel was shown.");
        Require(hud.Length == 0, $"The {label} HUD is clipped or covers a combatant: {hud}");
    }

    /// <summary>Captures the observed frame first, then checks the actual post-draw HUD bounds (no outcome panel shown).</summary>
    private async Task CheckHud(string label, EntityId opponent)
    {
        await Capture(label);
        await _driver.Frame();
        string failures = MeasureHud(label, opponent);
        Require(failures.Length == 0, $"The {label} HUD is clipped or covers a combatant: {failures}");
    }

    private async Task CheckWheel(string label)
    {
        await Capture(label);
        await _driver.Frame();
        Rect2 viewport = GetViewport().GetVisibleRect();
        Rect2 panel = Wheel.GetNode<Control>("WheelRoot/Panel").GetGlobalRect();
        List<object> controls = [];
        List<string> failures = [];
        if (!viewport.Encloses(panel)) failures.Add("Wheel panel outside the viewport");
        foreach (Control control in TextControls(Wheel.GetNode<Control>("WheelRoot/Panel")))
        {
            Rect2 rect = control.GetGlobalRect();
            int? lines = control is Label wrapped ? wrapped.GetLineCount() : null;
            int? visible = control is Label shown ? shown.GetVisibleLineCount() : null;
            bool complete = lines is null || visible >= lines;
            if (!viewport.Encloses(rect) || !panel.Encloses(rect) || !complete) failures.Add(control.Name + " clipped");
            controls.Add(new { Name = control.Name.ToString(), Rect = Rect(rect), Lines = lines, VisibleLines = visible });
        }
        _wheelBounds.Add(new { Label = label, Viewport = Rect(viewport), Panel = Rect(panel), Controls = controls, Failures = failures });
        Require(failures.Count == 0, string.Join("; ", failures));
    }

    /// <summary>
    /// Records the HUD's actual post-draw bounds: header, footer and target bar inside the viewport; every visible footer
    /// and header label and button inside its panel and the viewport; autowrapped labels showing every line and the
    /// non-wrapping hint's widest line within its rect; and the player's (and a drawn opponent's) sprite clear of the
    /// header, footer and target bar. Returns the failures, empty when all hold.
    /// </summary>
    private string MeasureHud(string label, EntityId opponent)
    {
        Rect2 viewport = GetViewport().GetVisibleRect();
        Control header = Hud.GetNode<Control>("Root/Header");
        Control footer = Hud.GetNode<Control>("Root/Footer");
        Control targetBar = Hud.GetNode<Control>("Root/TargetBar");
        List<string> failures = [];
        List<object> controls = [];
        foreach ((Control panel, string name) in new[] { (header, "Header"), (footer, "Footer") })
        {
            Rect2 bounds = panel.GetGlobalRect();
            if (!viewport.Encloses(bounds)) failures.Add(name + " outside the viewport");
            foreach (Control control in TextControls(panel))
            {
                Rect2 rect = control.GetGlobalRect();
                bool inside = viewport.Encloses(rect) && bounds.Encloses(rect);
                int? lines = control is Label { AutowrapMode: not TextServer.AutowrapMode.Off } wrapped ? wrapped.GetLineCount() : null;
                int? visible = control is Label { AutowrapMode: not TextServer.AutowrapMode.Off } shown ? shown.GetVisibleLineCount() : null;
                float? width = control is Label { AutowrapMode: TextServer.AutowrapMode.Off } single ? WidestLine(single) : null;
                bool complete = (lines is null || visible >= lines) && (width is null || width <= rect.Size.X + 0.5f);
                if (!inside) failures.Add(control.Name + " outside its panel or the viewport");
                if (!complete) failures.Add(control.Name + " text clipped");
                controls.Add(new { Name = control.Name.ToString(), Panel = name, Rect = Rect(rect), Text = control is Label l ? l.Text
                    : ((Button)control).Text, Lines = lines, VisibleLines = visible, TextWidth = width, Inside = inside, Complete = complete });
            }
        }
        bool targetShown = targetBar.IsVisibleInTree();
        if (targetShown && !viewport.Encloses(targetBar.GetGlobalRect())) failures.Add("TargetBar outside the viewport");
        Rect2? player = _game.Canvas.ActorSpriteViewportBounds(View.PlayerActorId);
        Rect2? other = _game.Canvas.ActorSpriteViewportBounds(opponent);
        if (player is not { } drawn) failures.Add("player sprite not drawn");
        else if (!Hud.ShowsFieldSprite(drawn)) failures.Add("player sprite covered by the HUD or target bar");
        if (other is { } opponentSprite && !Hud.ShowsFieldSprite(opponentSprite)) failures.Add("opponent sprite covered by the HUD or target bar");
        _hudBounds.Add(new { Label = label, Viewport = Rect(viewport), Header = Rect(header.GetGlobalRect()), Footer = Rect(footer.GetGlobalRect()),
            TargetBar = targetShown ? Rect(targetBar.GetGlobalRect()) : null, Field = Rect(Hud.FieldViewportBounds),
            PlayerSprite = player is { } a ? Rect(a) : null, OpponentSprite = other is { } b ? Rect(b) : null,
            Controls = controls, Failures = failures });
        return string.Join("; ", failures);
    }

    private static IEnumerable<Control> TextControls(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is not Control control || !control.IsVisibleInTree()) continue;
            if (control is Label or Button) yield return control;
            foreach (Control nested in TextControls(control)) yield return nested;
        }
    }

    private static float WidestLine(Label label)
    {
        Font font = label.GetThemeFont("font");
        int size = label.GetThemeFontSize("font_size");
        return label.Text.Split('\n').Max(line => font.GetStringSize(line, HorizontalAlignment.Left, -1, size).X);
    }

    /// <summary>A paid detour west of the camp outside its reach, the lane to Gearford and the sale at Kegga.</summary>
    private async Task DetourAndSell(CargoCorridor corridor, CellCoordinate here)
    {
        ulong carried = CarriedRations;
        ulong balance = View.Progression!.CurrencyBalance;
        Require(carried == 1, "The journey must reach the far market carrying the bought ration.");
        if (_game.CurrentTarget is not null)
        {
            _driver.Tap(Key.R);
            await _driver.Until(() => _game.CurrentTarget is null, "Clearing the selected chief did not work.");
        }
        SetStage("detour");
        CellCoordinate rejoin = new(346, 303, here.Z);
        await Walk([.. CargoCorridor.Lane([here, new CellCoordinate(343, here.Y, here.Z), new CellCoordinate(343, rejoin.Y, here.Z), rejoin])
            .Select(CargoCorridor.FeetAt)]);
        Require(EncounterCount == 1 && !View.IsInEncounter, "The detour began another encounter.");
        SetStage("to-gearford");
        await Walk([.. CargoCorridor.Lane([rejoin, corridor.DestinationApproach]).Select(CargoCorridor.FeetAt)]);
        await _driver.Until(() => StallOf(Kegga).IsAvailableInPerson, "Kegga's stall is not available at her approach.");
        await Capture("gearford");

        SetStage("sell");
        await MarketProbeEvidence.OpenStall(_driver, _game, Wheel, FarmSemanticAction.TradeOffers);
        int keggaRow = Array.FindIndex(View.Markets!.Stalls.ToArray(), stall => stall.MarketId == Kegga.MarketId) + 1;
        await _driver.Choose(Node<OptionButton>("MarketStall"), keggaRow);
        await _driver.Until(() => _game.MarketDraft.MarketId == Kegga.MarketId, "Choosing Kegga's stall did not address it.");
        await _driver.Activate(Node<Button>("SellMode"));
        await _driver.Choose(Node<OptionButton>("MarketLot"), 1);
        EntityId lot = StallOf(Kegga).OwnLots.Single().LotId;
        Require(_game.MarketDraft.LotId == lot, "The carried lot was not chosen.");
        await ActivatePanel(FarmSemanticAction.ShowTradeLot, Node<Button>("ShowLot"));
        await _driver.Until(() => StallOf(Kegga).OwnShowing is { IsLive: true, MatchesOwnLot: true } shown && shown.Units == 1,
            "Showing the carried lot to Kegga did not apply.");
        await _driver.Activate(Node<Button>("AskMerchant"));
        await _driver.Until(() => MarketActionOffers.OpenOffer(View, StallOf(Kegga)) is { Side: SpotQuoteSide.IssuerBuys } bid
            && bid.QuantityAllowance == 1, "Kegga did not answer the sale request.");
        PaidQuoteView quote = MarketActionOffers.OpenOffer(View, StallOf(Kegga))!;
        await _driver.Activate(Node<Button>("ReviewOffer"));
        await _driver.Until(() => _game.MarketDraft.Review?.Quote.QuoteId == quote.QuoteId, "Review did not capture the exact offer.");
        await ActivatePanel(FarmSemanticAction.ConfirmTrade, Node<Button>("ConfirmTrade"));
        await _driver.Until(() => View.PaidQuotes.FirstOrDefault(value => value.QuoteId == quote.QuoteId)?.Status == PaidQuoteStatus.Filled
            && !_game.HasPendingAction, "The confirmed sale was not filled.");
        await _driver.Until(() => CarriedRations == 0, "The sold ration stayed in the player's pack.");
        Require(View.Progression!.CurrencyBalance == balance + quote.TotalFor(1), "The sale did not pay exactly the bid total.");
        _trade = new { QuoteId = quote.QuoteId.ToString(), LotId = lot.ToString(), Total = quote.TotalFor(1), BalanceBefore = balance,
            BalanceAfter = View.Progression.CurrencyBalance, RationsBefore = carried, RationsAfter = CarriedRations };
        await Capture("sold");
        _driver.Tap(Key.Escape);
        await _driver.Until(() => !_game.IsMarketOpen, "The stall panel did not close.");
        _checks.Add("After the completed victory, held keys walked the detour to Gearford and the carried ration was sold at Kegga.");
    }

    /// <summary>
    /// A stall commitment through the keyboard route: focus first reaches the panel's button, then the presented offer is
    /// proven current and Enter submits it before the next presentation. The panel re-derives time-bounded terms, such as
    /// a showing's expiry step, every frame while world time runs, so readiness is proven at the activating press.
    /// </summary>
    private async Task ActivatePanel(FarmSemanticAction action, Button button)
    {
        await _driver.Until(() => button.IsVisibleInTree() && !button.Disabled, "Market control is unavailable: " + button.Name);
        await _driver.Reach(button);
        await Submit(action, "panel-keyboard", () => _driver.Tap(Key.Enter));
        await _driver.Frame();
    }

    /// <summary>Releases held input and observes subsequent world cutoffs before declaring the arrival complete.</summary>
    private async Task Walk(IReadOnlyList<SubunitPosition> waypoints)
    {
        await WalkRoute(waypoints);
        for (int correction = 0; ; correction++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await _driver.Frame();
            ulong releasedAt = View.Markets!.AdvancingStep;
            await _driver.Until(() => View.Markets!.AdvancingStep >= checked(releasedAt + 2) && !_game.HasPendingAction,
                "Released travel input did not settle.", 5);
            if (Feet == waypoints[^1]) return;
            Require(correction < MaximumReplans, "Released travel repeatedly overshot its destination.");
            _walkInputs.Add(new { Stage = _stage, CorrectReleasedArrival = waypoints[^1].ToString() });
            await WalkRoute([waypoints[^1]]);
        }
    }

    /// <summary>Held arrow keys along read-only ground advice from the actual feet; the world validates every step.</summary>
    private async Task WalkRoute(IReadOnlyList<SubunitPosition> waypoints)
    {
        await _driver.Until(() => !_game.HasPendingAction && !View.IsInEncounter, "The world did not settle.", 30);
        int replans = 0;
        foreach (SubunitPosition target in waypoints)
        {
            while (Feet != target)
            {
                Require(!View.IsInEncounter, "Travel was interrupted by an encounter.");
                var (path, status) = _game.CargoRouteAdvice!.Plan(target);
                Require(path is { Count: > 0 }, $"No ground route for the continuation ({status}).");
                bool reached = true;
                foreach (SubunitPosition point in path!)
                {
                    var stuck = Stopwatch.StartNew();
                    SubunitPosition last = Feet;
                    while (Feet != point && reached)
                    {
                        long dx = point.X - Feet.X;
                        long dy = point.Y - Feet.Y;
                        Hold(dx != 0 ? dx > 0 ? Key.Right : Key.Left : dy > 0 ? Key.Down : Key.Up);
                        await _driver.Frame();
                        if (Feet != last)
                        {
                            long moved = Math.Abs(Feet.X - last.X) + Math.Abs(Feet.Y - last.Y);
                            Require((Feet.X == last.X || Feet.Y == last.Y) && moved % WorldMovementInput.DisplacementPerStep == 0
                                && moved <= 4 * WorldMovementInput.DisplacementPerStep, "Travel moved other than by ordinary samples.");
                            last = Feet;
                            stuck.Restart();
                        }
                        reached = stuck.Elapsed.TotalSeconds < 4;
                    }
                    if (!reached) break;
                }
                if (!reached)
                {
                    Release();
                    Require(++replans <= MaximumReplans, "Held-key travel made no progress after replanning.");
                    _walkInputs.Add(new { Stage = _stage, Replan = target.ToString() });
                    await _driver.Delay(0.3);
                }
            }
        }
        Release();
        await _driver.Until(() => !_game.HasPendingAction, "Travel did not settle.");
    }

    private void Hold(Key key)
    {
        if (_held == key) return;
        Release();
        _held = key;
        _walkInputs.Add(new { Stage = _stage, Hold = key.ToString() });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
        Input.FlushBufferedEvents();
    }

    private void Release()
    {
        if (_held is not { } key) return;
        _held = null;
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
        Input.FlushBufferedEvents();
    }

    private async Task Capture(string label)
    {
        if (DisplayServer.GetName() == "headless") return;
        Trace($"Waiting for draw: {label}; drawable={DisplayServer.WindowCanDraw()}.");
        bool drawn = false;
        void DrawCompleted() => drawn = true;
        RenderingServer.FramePostDraw += DrawCompleted;
        try
        {
            // A missing rendered frame must fail inside the existing route bound, not leave an unbounded signal wait.
            await _driver.Until(() => drawn, $"No post-draw frame arrived for {label}.", 10);
        }
        finally { RenderingServer.FramePostDraw -= DrawCompleted; }
        string path = Path.Combine(Path.GetDirectoryName(_reportPath)!, Path.GetFileNameWithoutExtension(_reportPath) + "." + label + ".png");
        Require(GetViewport().GetTexture().GetImage().SavePng(path) == Error.Ok, "Could not save the rendered frame " + label);
        _images.Add(path);
        Trace($"Captured {label}.");
    }

    /// <summary>
    /// One observed submission as evidence: the selected offer's action, label, target, scope and intent, the generated
    /// command identity and canonical bytes, and the receipt's canonical bytes and fields (or the dispatch failure).
    /// </summary>
    private static object SubmissionEvidence(string stage, FarmSubmission submission)
    {
        FarmActionOffer offer = submission.Offer;
        FarmActionScope scope = offer.Scope;
        Task<WorldActionReceipt> answer = submission.Receipt;
        WorldActionReceipt? receipt = answer.IsCompletedSuccessfully ? answer.Result : null;
        return new
        {
            Stage = stage, Action = offer.Action.ToString(), offer.Label, offer.TargetName, TargetId = offer.TargetId?.ToString(),
            Scope = new { PrincipalId = scope.PrincipalId.ToString(), SessionId = scope.SessionId.ToString(), ActorId = scope.ActorId.ToString(),
                WorldId = scope.WorldId.ToString(), BranchId = scope.BranchId.ToString(), scope.ExpectedActorRevision },
            IntentType = offer.Intent?.GetType().Name, IntentText = offer.Intent?.ToString(), OfferCommandId = offer.CommandId?.ToString(),
            CommandId = submission.CommandId.ToString(), CommandBytes = Convert.ToHexString(submission.CanonicalCommandBytes()),
            submission.IsRetry,
            Receipt = receipt is null ? null : new { Bytes = Convert.ToHexString(WorldReceiptCodecV1.Encode(receipt)),
                CommandId = receipt.CommandId.ToString(), Status = receipt.Status.ToString(), Rejection = receipt.Rejection.ToString(),
                receipt.CommitSequence, ResultId = receipt.ResultId.ToString() },
            Answer = answer.IsCompletedSuccessfully ? "receipt" : answer.IsFaulted ? "failed: " + answer.Exception!.GetBaseException().Message
                : answer.IsCanceled ? "cancelled" : "pending"
        };
    }

    private void Report(string status, string? failure)
    {
        LocalWorldView? view = _game.CurrentView;
        LocalEncounterOutcomeView? outcome = view?.EncounterOutcome;
        SubunitPosition? feet = view?.Actors.SingleOrDefault(actor => actor.ActorId == view.PlayerActorId)?.Feet;
        ulong? rations = view?.Markets is not null && _game.CargoStalls.Count != 0 ? CarriedRations : null;
        File.WriteAllText(_reportPath, JsonSerializer.Serialize(new
        {
            Kind = "native-encounter-outcome-v1", Mode = _mode, Status = status, Failure = failure, Entry = "--cargo-outcomes-probe",
            OpeningSeconds = Math.Round(_openingSeconds, 3), ProbeMilliseconds = _watch.ElapsedMilliseconds,
            Paused = _game.IsLocallyPaused, WindowFocusObserved = _observedNativeFocus, LogicalFocus = _game.IsWindowFocused,
            Inputs = _driver.Inputs, WalkInputs = _walkInputs, Strikes = _strikes, Waits = _waits, Engagement = _engagement,
            Outcome = _outcome, Completed = _completed, Decline = _decline, Trade = _trade, Panels = _panels, HudBounds = _hudBounds,
            WheelBounds = _wheelBounds, AcceptedCare = _acceptedCare,
            Selections = _selections, Submissions = _submitted.Select(row => SubmissionEvidence(row.Stage, row.Submission)).ToArray(),
            SubmissionLimit = MaximumSubmissions, SubmissionOverflow = _submissionOverflow, Checks = _checks,
            Images = _images, SaveAndLeave = _mode == "defeat",
            FinalStage = outcome?.Stage.ToString(), FinalTerminal = outcome?.IsTerminal, CompletedAtSequence = outcome?.CompletedAtSequence,
            FinalResult = outcome?.Result.ToString(), FinalStandingSide = outcome?.StandingSide,
            EncounterCount = view?.EncounterLifecycle?.EncounterCount,
            FinalFeet = feet is { } at ? new { at.X, at.Y, at.Z } : null,
            FinalHp = view?.Progression?.Hp, FinalBalance = view?.Progression?.CurrencyBalance, CarriedRations = rations
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private void SetStage(string stage)
    {
        _stage = stage;
        Trace("Stage entered.");
    }

    private void Trace(string detail) => GD.Print($"Outcome probe {_mode} [{_stage}] {_watch.Elapsed.TotalSeconds:F3}s: {detail}");

    private static object Rect(Rect2 bounds) => new { X = bounds.Position.X, Y = bounds.Position.Y, Width = bounds.Size.X, Height = bounds.Size.Y };

    private static void Require(bool condition, string message) => MarketProbeDriver.Require(condition, message);
}
