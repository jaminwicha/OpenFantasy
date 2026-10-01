using System.Diagnostics;
using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.NativeCargo;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.Retreat;

namespace OpenFantasy.Client;

/// <summary>
/// Disposable native retreat journey over an exported fixture. Through ordinary injected input on one named device
/// route it selects and engages the real wolf, opens the production encounter wheel once at the first ready boundary
/// and inspects the Retreat sector's destination, walk and recovery (the capture must be current, never retried),
/// cancels (nothing is sent), commits on a second opening, observes every walked sample until the world disengages
/// and then travels on. The keyboard route walks to Marla and buys one ration through the ordinary stall panel; the
/// other routes walk back over the retreated ground. The reopen mode only opens the saved world paused and reports.
/// </summary>
internal sealed partial class RetreatPlayProbe : Node
{
    private const double OpeningSeconds = 30;
    private const double ProbeSeconds = 300;
    private const int MaximumReplans = 6;
    private readonly FarmGame _game;
    private readonly string _reportPath;
    private readonly string _metadataPath;
    private readonly string _genesisPath;
    private readonly string _mode;
    private readonly MarketProbeDriver _driver;
    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private readonly List<object> _positions = [];
    private readonly List<string> _checks = [];
    private readonly List<string> _images = [];
    private readonly List<object> _walkInputs = [];
    private readonly List<object> _openings = [];
    private string _stage = "opening";
    private double _openingSeconds;
    private bool _observedNativeFocus;
    private object? _inspected;
    private object? _trade;
    private string? _notice;
    private Key? _held;

    internal RetreatPlayProbe(FarmGame game, string reportPath, string genesisPath, string metadataPath, string mode)
    {
        _game = game;
        _reportPath = reportPath;
        _genesisPath = genesisPath;
        _metadataPath = metadataPath;
        _mode = mode;
        _driver = new MarketProbeDriver(this, game, mode == "reopen" ? "keyboard" : mode, () => _stage, ProbeSeconds);
    }

    private LocalWorldView View => _game.CurrentView ?? throw new InvalidOperationException("No public world.");
    private FarmQuickWheel Wheel => _game.GetNode<FarmQuickWheel>("FarmQuickWheel");
    private FarmMarketPanel Panel => _game.GetNode<FarmMarketPanel>("FarmMarketPanel");
    private T Node<T>(string name) where T : Node => Panel.GetNode<T>("%" + name);
    private bool WheelOpen => Wheel.GetNode<Control>("%WheelRoot").IsVisibleInTree();
    private SubunitPosition Feet => View.Actors.Single(actor => actor.ActorId == View.PlayerActorId).Feet;
    private NativeCargoStall Marla => _game.CargoStalls.Single(stall => stall.MerchantSourceId == CargoCircuitSources.OriginMerchantId);
    private LocalMarketStallView MarlaStall => View.Markets!.Stalls.Single(value => value.MarketId == Marla.MarketId);
    private ulong CarriedRations => MarlaStall.OwnLots.Aggregate(0UL, (sum, lot) => sum + lot.Quantity);

    public override void _Process(double delta)
    {
        if (DisplayServer.GetName() != "headless" && GetWindow().HasFocus()) _observedNativeFocus = true;
    }

    public override async void _Ready()
    {
        try
        {
            await _driver.Until(() => _game.CurrentView?.Retreat is not null && _game.Canvas.Regional is not null,
                "The retreat world did not open.", OpeningSeconds);
            _openingSeconds = _watch.Elapsed.TotalSeconds;
            _game.InjectFixtureFocus(true);
            using var metadata = JsonDocument.Parse(File.ReadAllText(_metadataPath));
            Require(metadata.RootElement.GetProperty("Kind").GetString() == "native-retreat-fixture-v1"
                && metadata.RootElement.GetProperty("PlayerActorId").GetString() == View.PlayerActorId.ToString(),
                "The fixture metadata names another world or player.");
            string wolfText = metadata.RootElement.GetProperty("WolfActorId").GetString()!;
            EntityId wolf = View.Actors.Single(actor => actor.ActorId.ToString() == wolfText).ActorId;
            Require(_game.IsLocallyPaused && !View.IsInEncounter, "The retreat fixture must open paused in exploration.");
            if (_mode == "reopen")
            {
                _stage = "reopen";
                await Capture("reopen");
                _checks.Add("The saved world reopens paused in exploration with its retained retreat record and carried rations.");
            }
            else
            {
                await Exercise(wolf);
            }
            Require(_game.IsLocallyPaused && !_game.HasPendingAction && !WheelOpen && !_game.IsMarketOpen,
                "The journey did not end paused and closed.");
            Report("passed", null);
            _game.RequestClose();
        }
        catch (Exception error)
        {
            Release();
            Report("failed", error.ToString());
            GD.PushError("Native retreat probe failed: " + error);
            GetTree().Quit(1);
        }
    }

    private async Task Exercise(EntityId wolf)
    {
        _stage = "resume";
        Pause();
        await _driver.Until(() => !_game.IsLocallyPaused, "Ordinary input did not resume world time.");

        _stage = "select-wolf";
        for (int presses = 0; _game.CurrentTarget?.Id != wolf; presses++)
        {
            Require(presses < 10, "Target cycling never selected the wolf.");
            if (_mode == "controller") _driver.Joy(JoyButton.RightShoulder);
            else _driver.Tap(Key.Q);
            await _driver.Delay(0.1);
        }
        _stage = "engage";
        if (_mode == "controller") _driver.Joy(JoyButton.A);
        else _driver.Tap(Key.E);
        // The first boundary at which the world waits for the player: the wheel is opened exactly once here.
        await _driver.Until(() => View.Retreat?.Offer is { CanQueue: true } && !_game.HasPendingAction,
            "No retreat was offered at the player's boundary.", 30);
        LocalRetreatOffer offer = View.Retreat!.Offer!;
        _checks.Add($"Engaging the real wolf offers a {offer.Samples}-step retreat: {RetreatOffers.Steps(offer)}.");

        _stage = "inspect";
        await InspectRetreat();
        await Capture("retreat-inspected");
        RequireInspectableRetreat();
        _checks.Add("The first wheel opening at the ready boundary showed the current Retreat offer; nothing read as changed.");
        _stage = "cancel";
        ulong timing = View.Encounter!.TimingRevision;
        if (_mode == "controller") _driver.Joy(JoyButton.B);
        else if (_mode == "pointer") await PointerPress(Wheel.GetNode<Button>("%CloseWheel"));
        else _driver.Tap(Key.V);
        await _driver.Until(() => !WheelOpen, "Cancelling did not close the wheel.");
        await _driver.Delay(0.3);
        // The opponent may queue its own choice meanwhile; the player's own boundary must still be waiting, unanswered.
        Require(!_game.HasPendingAction && View.Retreat!.Progress is null && View.Retreat.Last is null
            && View.Encounter is { PlayerDecisionQueued: false, PlayerIsDue: true } held && held.TimingRevision >= timing,
            "Cancelling the wheel sent or committed a choice.");
        _checks.Add("Cancelling the inspected Retreat sends nothing: the player's own boundary still waits, unanswered.");

        _stage = "commit";
        await InspectRetreat();
        LocalRetreatOffer committed = View.Retreat!.Offer!;
        RequireInspectableRetreat();
        if (_mode == "controller") _driver.Joy(JoyButton.A);
        else if (_mode == "pointer") await PointerPress(Wheel.GetNode<Button>("%ConfirmChoice"));
        else _driver.Tap(Key.Enter);
        _stage = "retreating";
        SubunitPosition last = Feet;
        _positions.Add(new { last.X, last.Y, last.Z, Seconds = _watch.Elapsed.TotalSeconds });
        bool captured = false;
        var deadline = Stopwatch.StartNew();
        while (View.Retreat?.Last?.Status != EncounterRetreatStatus.Disengaged)
        {
            Require(deadline.Elapsed.TotalSeconds < 60, "The committed retreat did not disengage.");
            Require(View.Retreat?.Last is not { Status: EncounterRetreatStatus.Interrupted or EncounterRetreatStatus.Cancelled },
                "The retreat stopped before disengaging.");
            await _driver.Frame();
            SubunitPosition now = Feet;
            if (now == last) continue;
            Require(Math.Abs(now.X - last.X) + Math.Abs(now.Y - last.Y) == EncounterRetreatRules.SampleSubunits,
                "A presented retreat step was not one ordinary sample.");
            last = now;
            _positions.Add(new { now.X, now.Y, now.Z, Seconds = _watch.Elapsed.TotalSeconds });
            if (!captured && View.Retreat?.Progress is not null)
            {
                captured = true;
                Require(View.EncounterActions is { CanWait: false, WaitBlockedReason: LocalEncounterActionBlockedReason.Retreating }
                    && View.Encounter?.PlayerDecisionQueued == true, "The held player was offered another choice while retreating.");
                _checks.Add("While the route is walked the player is shown as retreating and is offered no Wait or Strike.");
                await Capture("retreating");
            }
        }
        Require(_positions.Count - 1 == committed.Samples && Feet == committed.Destination && !View.IsInEncounter,
            "The disengaged position differs from the committed destination.");
        Label message = _game.GetNode<FarmHud>("FarmHud").GetNode<Label>("%Message");
        var announced = Stopwatch.StartNew();
        while (!message.Text.Contains("broke away", StringComparison.Ordinal) && announced.Elapsed.TotalSeconds < 2) await _driver.Frame();
        _notice = message.Text;
        Require(_notice.Contains("broke away", StringComparison.Ordinal), "The disengagement was not announced: " + _notice);
        await Capture("disengaged");
        _checks.Add($"The committed route was walked as {committed.Samples} presented 32-subunit steps and ended the encounter.");

        _stage = "travel";
        if (_mode == "keyboard")
        {
            await TravelAndBuy();
        }
        else
        {
            // Ordinary exploration resumes: walk back over the same ground to where the encounter began.
            await Walk([committed.Origin]);
            Require(Feet == committed.Origin && !View.IsInEncounter, "Ordinary travel did not return to the encounter's origin.");
            _checks.Add("Ordinary held-key travel continues after the retained readiness is paid by exploration.");
        }
        _stage = "pause";
        Pause();
        await _driver.Until(() => _game.IsLocallyPaused, "The journey could not pause.");
        await Capture("final");
    }

    /// <summary>After disengaging, walk the authored lane to Marla and buy one ration through the ordinary stall panel.</summary>
    private async Task TravelAndBuy()
    {
        WorldSnapshot genesis = WorldSnapshotCodec.Decode(File.ReadAllBytes(_genesisPath));
        CargoCorridor corridor = NativeCargoWorld.Corridor(genesis);
        CellCoordinate standing = genesis.Space.GetActor(View.PlayerActorId).Feet.Cell;
        CellCoordinate farmStart = new(standing.X, standing.Y, standing.Z - 1);
        ulong carried = CarriedRations;
        ulong balance = View.Progression!.CurrencyBalance;
        Require(carried == 1 && !MarlaStall.IsAvailableInPerson, "The retreat must begin away from Marla carrying one ration.");
        // The wolf stays selected after the encounter; the ordinary clear-target key returns the wheel to exploration offers.
        _driver.Tap(Key.R);
        await _driver.Until(() => _game.CurrentTarget is null, "Clearing the selected wolf did not work.");
        await Walk([.. CargoCorridor.Lane([farmStart, new CellCoordinate(farmStart.X, corridor.OriginApproach.Y, farmStart.Z),
            corridor.OriginApproach]).Select(CargoCorridor.FeetAt)]);
        await _driver.Until(() => MarlaStall.IsAvailableInPerson, "Marla's stall is not available at her approach.");
        await Capture("marla");
        _checks.Add("Held-key travel carried the player from the retreat to Marla's stall in Brasshollow.");

        _stage = "buy";
        Require(_game.CurrentOffers[FarmSemanticAction.TradeBuy].TargetId == Marla.MerchantActorId
            && _game.CurrentOffers[FarmSemanticAction.TradeBuy].CanExecute, "The wheel's Buy does not address Marla, who is in reach.");
        await MarketProbeEvidence.OpenStall(_driver, _game, Wheel, FarmSemanticAction.TradeBuy);
        Require(_game.MarketDraft.MarketId == Marla.MarketId && _game.MarketDraft.IsExplicit && _game.MarketDraft.Quantity == 1,
            "The stall panel does not address one ration from Marla.");
        await _driver.Activate(Node<Button>("AskMerchant"));
        await _driver.Until(() => MarketActionOffers.OpenOffer(View, MarlaStall) is { Side: SpotQuoteSide.IssuerSells } offer
            && offer.QuantityAllowance == 1, "Marla did not answer the request.");
        PaidQuoteView quote = MarketActionOffers.OpenOffer(View, MarlaStall)!;
        await _driver.Activate(Node<Button>("ReviewOffer"));
        await _driver.Until(() => _game.MarketDraft.Review?.Quote.QuoteId == quote.QuoteId, "Review did not capture the exact offer.");
        await Capture("buy-review");
        await _driver.Activate(Node<Button>("ConfirmTrade"));
        await _driver.Until(() => View.PaidQuotes.FirstOrDefault(value => value.QuoteId == quote.QuoteId)?.Status == PaidQuoteStatus.Filled
            && !_game.HasPendingAction, "The confirmed offer was not filled.");
        await _driver.Until(() => CarriedRations == carried + 1, "The bought ration did not reach the player's pack.");
        Require(View.Progression!.CurrencyBalance == balance - quote.TotalFor(1), "The purchase did not pay exactly the quoted total.");
        _trade = new { QuoteId = quote.QuoteId.ToString(), Total = quote.TotalFor(1), BalanceBefore = balance,
            BalanceAfter = View.Progression.CurrencyBalance, RationsBefore = carried, RationsAfter = CarriedRations };
        await Capture("bought");
        _driver.Tap(Key.Escape);
        await _driver.Until(() => !_game.IsMarketOpen, "The stall panel did not close.");
        _checks.Add("At Marla's stall one ration was asked, reviewed and bought at the exact quoted total; the pack holds both lots.");
    }

    /// <summary>
    /// Opens the encounter wheel and selects Retreat. The capture must be current on this one opening: a slot that
    /// reads as changed fails the journey instead of being closed and inspected again.
    /// </summary>
    private async Task InspectRetreat()
    {
        if (_mode == "controller") _driver.Joy(JoyButton.RightStick);
        else _driver.Tap(Key.V);
        await _driver.Until(() => WheelOpen && Wheel.Model is not null, "The encounter wheel did not open.");
        FarmActionOffer? slot = Wheel.Model!.Slots[0]?.Offer;
        _openings.Add(new { Stage = _stage, Timing = View.Encounter?.TimingRevision, Slot = slot?.TargetName,
            SlotBlocker = slot?.BlockingReason, CanExecute = slot?.CanExecute });
        Require(Wheel.Model.Slots[0]?.Action == FarmSemanticAction.Retreat, "Retreat is not the wheel's upper sector.");
        Require(slot is { CanExecute: true } && slot.Intent is QueueEncounterRetreatIntent,
            "The first opening captured a stale or unavailable Retreat: " + slot?.BlockingReason);
        if (_mode == "controller") _driver.Joy(JoyButton.DpadUp);
        else if (_mode == "pointer")
        {
            // Hover selection follows pointer motion between sectors: move through the lower sector into the upper one.
            await _driver.Point(Wheel.GetNode<Control>("%Disc"), 2);
            await _driver.Point(Wheel.GetNode<Control>("%Disc"), 0);
        }
        else _driver.Tap(Key.Up);
        await _driver.Until(() => Wheel.Model?.Selected?.Action == FarmSemanticAction.Retreat, "The Retreat sector was not selected.");
        Label nameLabel = Wheel.GetNode<Label>("%ChoiceName");
        Label detailLabel = Wheel.GetNode<Label>("%ChoiceDetails");
        bool Explains() => nameLabel.Text == "Retreat" && detailLabel.Text.Contains("To cell", StringComparison.Ordinal)
            && detailLabel.Text.Contains("s walk", StringComparison.Ordinal) && detailLabel.Text.Contains("recovery", StringComparison.Ordinal);
        var shown = Stopwatch.StartNew();
        while (!Explains() && shown.Elapsed.TotalSeconds < 3) await _driver.Frame();
        _inspected = new { Stage = _stage, Name = nameLabel.Text, Details = detailLabel.Text,
            Tooltip = Wheel.GetNode<Button>("%ConfirmChoice").TooltipText };
        Require(Explains(), $"The Retreat inspection does not explain destination, walk and recovery: '{nameLabel.Text}' / '{detailLabel.Text}'.");
        Require(Wheel.Model!.Selected!.Offer.CanExecute, "The inspected Retreat changed under inspection.");
        // Check the actual rendered interval. Checking only before FramePostDraw can authenticate a stale screenshot.
        ulong boundary = View.Encounter!.TimingRevision;
        var reading = Stopwatch.StartNew();
        do
        {
            await _driver.Frame();
            if (DisplayServer.GetName() != "headless")
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            RequireInspectableRetreat();
            Require(View.Encounter!.TimingRevision == boundary, "The inspected decision boundary changed while reading.");
        } while (reading.Elapsed.TotalSeconds < 0.6);
    }

    private void RequireInspectableRetreat()
    {
        Button confirm = Wheel.GetNode<Button>("%ConfirmChoice");
        Require(WheelOpen && Wheel.Model?.Selected?.Offer is { CanExecute: true } && !confirm.Disabled,
            "The rendered Retreat is stale or cannot be confirmed.");
        Rect2 viewport = GetViewport().GetVisibleRect();
        foreach (string path in new[] { "%ChoiceName", "%ChoiceDetails", "%ConfirmChoice", "%CloseWheel" })
        {
            Rect2 bounds = Wheel.GetNode<Control>(path).GetGlobalRect();
            Require(viewport.Encloses(bounds), "The rendered retreat control is clipped: " + path);
        }
    }

    /// <summary>Releases held input and observes subsequent world cutoffs before declaring the return complete.</summary>
    private async Task Walk(IReadOnlyList<SubunitPosition> waypoints)
    {
        await WalkRoute(waypoints);
        for (int correction = 0; ; correction++)
        {
            // HasPendingAction covers submitted actions, not the held movement sampled by the host. Let the
            // physics adapter publish the release, then observe two cutoffs to drain a previously sampled move.
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await _driver.Frame();
            ulong releasedAt = View.Markets!.AdvancingStep;
            await _driver.Until(() => View.Markets!.AdvancingStep >= checked(releasedAt + 2)
                && !_game.HasPendingAction, "Released travel input did not settle.", 5);
            if (Feet == waypoints[^1]) return;
            Require(correction < MaximumReplans, "Released travel repeatedly overshot its destination.");
            _walkInputs.Add(new { Stage = _stage, CorrectReleasedArrival = waypoints[^1].ToString() });
            await WalkRoute([waypoints[^1]]);
        }
    }

    /// <summary>Held arrow keys along read-only ground advice from the actual feet; the world validates every step.</summary>
    private async Task WalkRoute(IReadOnlyList<SubunitPosition> waypoints)
    {
        await _driver.Until(() => !_game.HasPendingAction && !View.IsInEncounter, "The world did not settle.");
        int replans = 0;
        foreach (SubunitPosition target in waypoints)
        {
            while (Feet != target)
            {
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
                            // A slow frame may present two published steps at once; each is still one ordinary sample.
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

    /// <summary>Moves the pointer onto the button (dismissing any hover tooltip of the inspected sector), then clicks it.</summary>
    private async Task PointerPress(Button button)
    {
        await _driver.Hover(button);
        await _driver.Delay(0.2);
        await _driver.Click(button);
    }

    private void Pause()
    {
        if (_mode == "controller") _driver.Joy(JoyButton.Start);
        else _driver.Tap(Key.Escape);
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
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string path = Path.Combine(Path.GetDirectoryName(_reportPath)!, Path.GetFileNameWithoutExtension(_reportPath) + "." + label + ".png");
        Require(GetViewport().GetTexture().GetImage().SavePng(path) == Error.Ok, "Could not save the rendered frame " + label);
        _images.Add(path);
    }

    private void Report(string status, string? failure)
    {
        LocalWorldView? view = _game.CurrentView;
        LocalRetreatRecord? record = view?.Retreat?.Last;
        SubunitPosition? feet = view?.Actors.SingleOrDefault(actor => actor.ActorId == view.PlayerActorId)?.Feet;
        ulong? rations = view?.Markets is not null && _game.CargoStalls.Count != 0 ? CarriedRations : null;
        File.WriteAllText(_reportPath, JsonSerializer.Serialize(new
        {
            Kind = "native-retreat-v2", Mode = _mode, Status = status, Failure = failure, Entry = "--cargo-retreat-probe",
            OpeningSeconds = Math.Round(_openingSeconds, 3), ProbeMilliseconds = _watch.ElapsedMilliseconds,
            Paused = _game.IsLocallyPaused, WindowFocusObserved = _observedNativeFocus, LogicalFocus = _game.IsWindowFocused,
            Inputs = _driver.Inputs, WalkInputs = _walkInputs, Inspected = _inspected, Openings = _openings, Positions = _positions,
            Checks = _checks, Images = _images, Notice = _notice, Trade = _trade,
            Record = record is null ? null : new { Status = record.Status.ToString(), Reason = record.Reason.ToString(),
                record.SamplesTaken, record.RouteSamples, record.ReadinessCharged, record.WorldSequence,
                Final = new { record.Final.X, record.Final.Y, record.Final.Z }, ActorId = record.ActorId.ToString(),
                CommandId = record.CommandId.ToString() },
            FinalFeet = feet is { } at ? new { at.X, at.Y, at.Z } : null,
            FinalBalance = view?.Progression?.CurrencyBalance,
            CarriedRations = rations
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void Require(bool condition, string message) => MarketProbeDriver.Require(condition, message);
}
