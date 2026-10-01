using System.Diagnostics;
using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.NativeCargo;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.Retreat;
using OpenFantasy.Hosting.Simulation.RoadThreat;

namespace OpenFantasy.Client;

/// <summary>
/// Disposable native road-threat journey over an exported fixture that starts paused on the lane south of the camp,
/// carrying one bought ration. Through ordinary injected input it reads the warning shown before contact, walks north
/// until the Bandit Chief enters under its own control, (keyboard) strikes once and takes the chief's real hit, opens the
/// production wheel at a ready boundary, inspects and cancels Retreat (nothing is sent), commits it on a second opening,
/// observes every walked sample to disengagement and checks that no automatic reentry follows. The keyboard route then
/// walks a paid detour around the camp, travels to Kegga and sells the carried lot through the stall panel; controller
/// and pointer routes walk back down the lane. The reopen mode only opens the saved world paused and reports.
/// </summary>
internal sealed partial class RoadThreatPlayProbe : Node
{
    private const double OpeningSeconds = 30;
    private const int MaximumReplans = 6;
    private readonly FarmGame _game;
    private readonly string _reportPath;
    private readonly string _metadataPath;
    private readonly string _genesisPath;
    private readonly string _mode;
    private readonly MarketProbeDriver _driver;
    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private readonly List<object> _approach = [];
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
    private object? _hit;
    private object? _engagement;
    private object? _holdOff;
    private object? _unobserved;
    private object? _warningObservation;
    private object? _purchase;
    private ulong? _reward;
    private string? _warning;
    private string? _warningDetail;
    private string? _notice;
    private Key? _held;

    internal RoadThreatPlayProbe(FarmGame game, string reportPath, string genesisPath, string metadataPath, string mode)
    {
        _game = game;
        _reportPath = reportPath;
        _genesisPath = genesisPath;
        _metadataPath = metadataPath;
        _mode = mode;
        _driver = new MarketProbeDriver(this, game, mode == "reopen" ? "keyboard" : mode, () => _stage, mode == "keyboard" ? 690 : 290);
    }

    private LocalWorldView View => _game.CurrentView ?? throw new InvalidOperationException("No public world.");
    private FarmQuickWheel Wheel => _game.GetNode<FarmQuickWheel>("FarmQuickWheel");
    private FarmMarketPanel Panel => _game.GetNode<FarmMarketPanel>("FarmMarketPanel");
    private Label Status => _game.GetNode<FarmHud>("FarmHud").GetNode<Label>("%EncounterStatus");
    private T Node<T>(string name) where T : Node => Panel.GetNode<T>("%" + name);
    private bool WheelOpen => Wheel.GetNode<Control>("%WheelRoot").IsVisibleInTree();
    private SubunitPosition Feet => View.Actors.Single(actor => actor.ActorId == View.PlayerActorId).Feet;
    private NativeCargoStall Kegga => _game.CargoStalls.Single(stall => stall.MerchantSourceId == CargoCircuitSources.DestinationMerchantId);
    private NativeCargoStall Marla => _game.CargoStalls.Single(stall => stall.MerchantSourceId == CargoCircuitSources.OriginMerchantId);
    private LocalMarketStallView StallOf(NativeCargoStall stall) => View.Markets!.Stalls.Single(value => value.MarketId == stall.MarketId);
    private ulong CarriedRations => StallOf(Marla).OwnLots.Aggregate(0UL, (sum, lot) => sum + lot.Quantity);
    private ulong EncounterCount => View.EncounterLifecycle!.EncounterCount;
    private LocalRoadThreatView Threat => View.RoadThreats?.Single() ?? throw new InvalidOperationException("No observed road threat.");

    public override void _Process(double delta)
    {
        if (DisplayServer.GetName() != "headless" && GetWindow().HasFocus()) _observedNativeFocus = true;
    }

    public override async void _Ready()
    {
        try
        {
            await _driver.Until(() => _game.CurrentView?.Retreat is not null && _game.Canvas.Regional is not null,
                "The threatened cargo world did not open.", OpeningSeconds);
            _openingSeconds = _watch.Elapsed.TotalSeconds;
            _game.InjectFixtureFocus(true);
            using var metadata = JsonDocument.Parse(File.ReadAllText(_metadataPath));
            // The keyboard journey (and its reopening) starts from the fresh farm fixture; the device routes from the road.
            string kind = _mode is "keyboard" or "reopen" ? "native-road-threat-farm-fixture-v1" : "native-road-threat-fixture-v1";
            Require(metadata.RootElement.GetProperty("Kind").GetString() == kind
                && metadata.RootElement.GetProperty("PlayerActorId").GetString() == View.PlayerActorId.ToString(),
                "The fixture metadata names another fixture, world or player.");
            string chiefText = metadata.RootElement.GetProperty("ChiefActorId").GetString()!;
            EntityId chief = View.Actors.Single(actor => actor.ActorId.ToString() == chiefText).ActorId;
            Require(_game.IsLocallyPaused && !View.IsInEncounter, "The road-threat fixture must open paused in exploration.");
            if (_mode == "reopen")
            {
                _stage = "reopen";
                await Capture("reopen");
                _checks.Add("The saved world reopens paused in exploration with its retained encounter and retreat records.");
            }
            else
            {
                await Exercise(chief);
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
            GD.PushError("Native road-threat probe failed: " + error);
            GetTree().Quit(1);
        }
    }

    private async Task Exercise(EntityId chief)
    {
        if (_mode == "keyboard")
        {
            await EarnBuyAndTravel();
        }
        else
        {
            _stage = "resume";
            Pause();
            await _driver.Until(() => !_game.IsLocallyPaused, "Ordinary input did not resume world time.");
        }

        _stage = "unobserved";
        // Seven cells south of the camp the chief is beyond the three-cell observation bound: nothing about it is shown.
        SubunitPosition last = Feet;
        Require(last.Cell.X == 346 && last.Cell.Y == 318 && EncounterCount == 0 && !View.IsInEncounter,
            "The road segment must begin at road cell (346,318) before any encounter.");
        await _driver.Frame();
        Require(View.RoadThreats is { Count: 0 } && !Status.Text.Contains("Danger", StringComparison.Ordinal),
            "An unobserved chief must publish no warning, level, HP or distance.");
        _unobserved = new { last.X, last.Y, last.Z, Rows = View.RoadThreats!.Count, Status = Status.Text };
        _checks.Add("Seven cells south of the camp the chief is unobserved: no warning or detail is published.");

        _stage = "warning";
        // Walking north, the warning appears once the chief is observed (same floor, clear sightline, three cells),
        // before its 400-subunit reach: the player stops there safely and reads it.
        _approach.Add(new { last.X, last.Y, last.Z, Seconds = _watch.Elapsed.TotalSeconds });
        var sighting = Stopwatch.StartNew();
        while (!IsWatching(chief))
        {
            Require(sighting.Elapsed.TotalSeconds < 30 && !View.IsInEncounter, "The chief was never observed before contact.");
            Hold(Key.Up);
            await _driver.Frame();
            last = Approached(last);
        }
        Release();
        await _driver.Delay(0.4);
        last = Approached(last);
        LocalRoadThreatView watched = Threat;
        Require(watched.ActorId == chief && watched.State == LocalRoadThreatState.Watching && EncounterCount == 0 && !View.IsInEncounter
            && watched.DistanceSubunits > watched.ReachSubunits && watched.DistanceSubunits <= LocalRoadThreatView.ObservationRangeSubunits,
            "The chief must be observed beyond its reach before any encounter.");
        await _driver.Until(() => Status.Text.Contains("Danger nearby", StringComparison.Ordinal)
            && Status.Text.Contains("Bandit Chief", StringComparison.Ordinal), "No warning was shown before contact.");
        _warning = Status.Text;
        _warningDetail = Status.TooltipText;
        Require(_warningDetail.Contains("level 7", StringComparison.Ordinal) && _warningDetail.Contains("cells", StringComparison.Ordinal)
            && _warningDetail.Contains("attacks you", StringComparison.Ordinal), "The warning's detail does not explain the danger.");
        await Capture("warning");
        Vector2? drawn = _game.Canvas.DrawnActor(chief);
        Require(drawn is not null, "The observed chief is not drawn before contact.");
        Vector2 onScreen = _game.Canvas.GetGlobalTransformWithCanvas() * drawn!.Value;
        Rect2 chiefSprite = _game.Canvas.ActorSpriteViewportBounds(chief)
            ?? throw new InvalidOperationException("The chief has no rendered sprite bounds.");
        Rect2 playerSprite = _game.Canvas.ActorSpriteViewportBounds(View.PlayerActorId)
            ?? throw new InvalidOperationException("The player has no rendered sprite bounds.");
        FarmHud hud = _game.GetNode<FarmHud>("FarmHud");
        bool chiefVisible = hud.ShowsFieldSprite(chiefSprite);
        bool playerVisible = hud.ShowsFieldSprite(playerSprite);
        Require(chiefVisible && playerVisible, "The complete chief and player sprites must be visible before contact, clear of the HUD.");
        _warningObservation = new { watched.DistanceSubunits, watched.ReachSubunits, Feet = new { last.X, last.Y, last.Z },
            ChiefDrawn = true, ChiefViewport = new { onScreen.X, onScreen.Y }, ChiefInField = _game.CargoFieldViewportBounds.HasPoint(onScreen),
            ChiefSprite = SpriteRect(chiefSprite), PlayerSprite = SpriteRect(playerSprite),
            Field = SpriteRect(hud.FieldViewportBounds), ChiefSpriteVisible = chiefVisible, PlayerSpriteVisible = playerVisible,
            Seconds = _watch.Elapsed.TotalSeconds };
        _checks.Add("Walking north, the chief came into view three cells away: the HUD warned before contact, with level, HP, "
            + "reach and distance on inspection, and both complete sprites cleared the HUD.");

        _stage = "approach";
        var walking = Stopwatch.StartNew();
        while (!View.IsInEncounter)
        {
            Require(walking.Elapsed.TotalSeconds < 30, "Walking north never met the chief.");
            Hold(Key.Up);
            await _driver.Frame();
            last = Approached(last);
        }
        Release();
        LocalRoadThreatView engaged = Threat;
        Require(engaged.State == LocalRoadThreatState.Engaged && EncounterCount == 1, "The encounter is not the chief's.");
        _engagement = new { EncounterCount, Distance = engaged.DistanceSubunits, Reach = engaged.ReachSubunits,
            Feet = new { last.X, last.Y, last.Z }, Seconds = _watch.Elapsed.TotalSeconds };
        Require(engaged.DistanceSubunits <= engaged.ReachSubunits, "The chief engaged from beyond its reach.");
        await Capture("engaged");
        _checks.Add("Walking the lane north, the chief began the encounter itself once the player was within its reach.");

        if (_mode == "keyboard")
        {
            _stage = "strike";
            await _driver.Until(() => View.Retreat?.Offer is { CanQueue: true } && !_game.HasPendingAction,
                "The player's first boundary was not offered.", 30);
            int before = View.Progression!.Hp;
            for (int presses = 0; _game.CurrentOffers[FarmSemanticAction.Strike].TargetId != chief; presses++)
            {
                Require(presses < 10, "Target cycling never addressed the chief.");
                _driver.Tap(Key.Q);
                await _driver.Delay(0.1);
            }
            // The frame's offers follow the published view; wait for this boundary's executable Strike on the chief.
            await _driver.Until(() => _game.CurrentOffers[FarmSemanticAction.Strike] is { CanExecute: true } ready
                && ready.TargetId == chief && !_game.HasPendingAction, "Strike never addressed the chief at the player's boundary.", 10);
            _driver.Tap(Key.E);
            await _driver.Until(() => View.Progression!.Hp < before && View.Retreat?.Offer is { CanQueue: true } && !_game.HasPendingAction,
                "The chief's attack did not land before the next boundary.", 30);
            _hit = new { Before = before, After = View.Progression!.Hp, ChiefHp = engaged.CurrentHp,
                ChiefHpAfter = Threat.CurrentHp };
            await Capture("hit");
            _checks.Add($"Striking the level-7 chief cost the Squire {before - View.Progression.Hp} HP from its real attack.");
        }

        _stage = "boundary";
        await _driver.Until(() => View.Retreat?.Offer is { CanQueue: true } && !_game.HasPendingAction,
            "No retreat was offered at the player's boundary.", 30);
        LocalRetreatOffer offer = View.Retreat!.Offer!;
        _checks.Add($"The chief's encounter offers a {offer.Samples}-step retreat: {RetreatOffers.Steps(offer)}.");

        _stage = "inspect";
        await InspectRetreat();
        await Capture("retreat-inspected");
        RequireInspectableRetreat();
        _stage = "cancel";
        ulong timing = View.Encounter!.TimingRevision;
        if (_mode == "controller") _driver.Joy(JoyButton.B);
        else if (_mode == "pointer") await PointerPress(Wheel.GetNode<Button>("%CloseWheel"));
        else _driver.Tap(Key.V);
        await _driver.Until(() => !WheelOpen, "Cancelling did not close the wheel.");
        await _driver.Delay(0.3);
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
        last = Feet;
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
        _checks.Add($"The committed route was walked as {committed.Samples} presented 32-subunit steps and ended the chief's encounter.");

        _stage = "hold-off";
        // No automatic reentry: the chief keeps its place and the world runs on without a second confrontation.
        ulong step = View.Markets!.AdvancingStep;
        await _driver.Until(() => View.Markets!.AdvancingStep >= step + 20, "Exploration did not resume after disengaging.", 10);
        Require(EncounterCount == 1 && !View.IsInEncounter
            && (View.RoadThreats ?? []).All(threat => threat.State == LocalRoadThreatState.Watching),
            "The chief re-entered automatically after the retreat.");
        _holdOff = new { FromStep = step, ToStep = View.Markets.AdvancingStep, EncounterCount };
        _checks.Add("Twenty exploration steps after disengaging, the chief has not re-entered: the retreat ended contact beyond its reach.");

        _stage = "travel";
        WorldSnapshot genesis = WorldSnapshotCodec.Decode(File.ReadAllBytes(_genesisPath));
        CargoCorridor corridor = NativeCargoWorld.Corridor(genesis);
        CellCoordinate here = new(Feet.Cell.X, Feet.Cell.Y, Feet.Cell.Z - 1);
        if (_mode == "keyboard")
        {
            await DetourAndSell(corridor, here);
        }
        else
        {
            // Back down the lane, away from the camp: ordinary travel resumes once exploration pays the retained debt.
            CellCoordinate south = new(346, 318, here.Z);
            await Walk([.. CargoCorridor.Lane([here, new CellCoordinate(south.X, here.Y, here.Z), south]).Select(CargoCorridor.FeetAt)]);
            Require(EncounterCount == 1 && !View.IsInEncounter, "Walking away from the camp met the chief again.");
            _checks.Add("Held-key travel back down the lane continues after the retained readiness is paid, without another encounter.");
        }
        _stage = "pause";
        Pause();
        await _driver.Until(() => _game.IsLocallyPaused, "The journey could not pause.");
        await Capture("final");
    }

    /// <summary>
    /// The keyboard route's own preparation from the fresh farm fixture: E/G/G/E with Wren for her earned reward, held
    /// arrow keys to Marla, the stall panel's ask/review/confirm for one ration, then the authored outbound lane to the
    /// road cell (346,318). Nothing is granted, bought by the host or walked by managed preparation.
    /// </summary>
    private async Task EarnBuyAndTravel()
    {
        _stage = "farm";
        Require(View.Progression!.CurrencyBalance == 0 && View.PaidQuotes.Count == 0 && CarriedRations == 0 && EncounterCount == 0
            && View.Quests.All(quest => quest.Status is null), "The farm fixture must start fresh: no reward, quote, ration or quest.");
        await Capture("farm-start");
        Pause();
        await _driver.Until(() => !_game.IsLocallyPaused, "Ordinary input did not resume world time.");
        SubunitPosition origin = Feet;
        CargoCorridor corridor = NativeCargoWorld.Corridor(WorldSnapshotCodec.Decode(File.ReadAllBytes(_genesisPath)));

        _stage = "walk-to-wren";
        LocalActorView wren = View.Actors.Single(actor => actor.ActorId == _game.CompanionActorId);
        SubunitPosition? beside = null;
        foreach ((long dx, long dy) in new[] { (0L, 1L), (0L, -1L), (1L, 0L), (-1L, 0L) })
        {
            SubunitPosition candidate = wren.Feet.Offset(dx * 256, dy * 256, 0);
            if (Feet != candidate && _game.CargoRouteAdvice!.Plan(candidate).Waypoints is null) continue;
            beside = candidate;
            break;
        }
        Require(beside is not null, "No reachable column beside Wren.");
        await Walk([beside!.Value]);

        _stage = "wren";
        await _driver.Until(() => _game.CurrentOffers.TryGetValue(FarmSemanticAction.Speak, out FarmActionOffer? speak) && speak.CanExecute,
            "Wren's talk offer is not available beside her.");
        _driver.Tap(Key.E);
        await _driver.Until(() => View.Dialogue is not null && !_game.HasPendingAction, "Talking to Wren opened no conversation.");
        _driver.Tap(Key.G);
        await _driver.Until(() => View.Quests.Any(quest => quest.Status == ActorQuestStatus.Accepted) && !_game.HasPendingAction,
            "G did not accept Wren's quest.");
        _driver.Tap(Key.G);
        await _driver.Until(() => View.Progression!.CurrencyBalance > 0 && !_game.HasPendingAction, "G did not deliver Wren's quest.");
        _reward = View.Progression!.CurrencyBalance;
        _driver.Tap(Key.E);
        await _driver.Until(() => View.Dialogue is null && !_game.HasPendingAction, "E did not close the conversation.");
        await Capture("wren-reward");
        _checks.Add($"E, G, G, E talked to Wren, accepted and delivered her quest and closed: the earned reward is {_reward}.");

        _stage = "walk-to-marla";
        CellCoordinate start = Cell(origin);
        await Walk([origin]);
        await Walk([.. CargoCorridor.Lane([start, new CellCoordinate(start.X, corridor.OriginApproach.Y, start.Z), corridor.OriginApproach])
            .Select(CargoCorridor.FeetAt)]);
        await _driver.Until(() => StallOf(Marla).IsAvailableInPerson, "Marla's stall is not available at her approach.");
        await Capture("brasshollow");

        _stage = "buy";
        ulong funds = View.Progression!.CurrencyBalance;
        FarmActionOffer buyOffer = _game.CurrentOffers[FarmSemanticAction.TradeBuy];
        Require(buyOffer.TargetId == Marla.MerchantActorId && buyOffer.CanExecute, "The wheel's Buy does not address Marla in reach.");
        await MarketProbeEvidence.OpenStall(_driver, _game, Wheel, FarmSemanticAction.TradeBuy);
        Require(_game.MarketDraft.MarketId == Marla.MarketId && _game.MarketDraft.Quantity == 1,
            "The stall panel does not address one ration at Marla's stall.");
        await _driver.Activate(Node<Button>("AskMerchant"));
        await _driver.Until(() => MarketActionOffers.OpenOffer(View, StallOf(Marla)) is { Side: SpotQuoteSide.IssuerSells } offer
            && offer.QuantityAllowance == 1, "Marla did not answer the request.");
        PaidQuoteView quote = MarketActionOffers.OpenOffer(View, StallOf(Marla))!;
        Require(quote.MarketId == Marla.MarketId, "Marla's offer names another market.");
        await _driver.Activate(Node<Button>("ReviewOffer"));
        await _driver.Until(() => _game.MarketDraft.Review?.Quote.QuoteId == quote.QuoteId, "Review did not capture the exact offer.");
        await Capture("buy-review");
        await _driver.Activate(Node<Button>("ConfirmTrade"));
        await _driver.Until(() => View.PaidQuotes.FirstOrDefault(value => value.QuoteId == quote.QuoteId)?.Status == PaidQuoteStatus.Filled
            && !_game.HasPendingAction, "The confirmed purchase was not filled.");
        await _driver.Until(() => CarriedRations == 1, "The bought ration did not reach the player's pack.");
        EntityId lot = StallOf(Marla).OwnLots.Single().LotId;
        Require(View.Progression!.CurrencyBalance == funds - quote.TotalFor(1), "The purchase did not pay exactly the quoted total.");
        _purchase = new { QuoteId = quote.QuoteId.ToString(), LotId = lot.ToString(), Units = 1UL, Total = quote.TotalFor(1),
            BalanceBefore = funds, BalanceAfter = View.Progression.CurrencyBalance };
        await Capture("marla-bought");
        _driver.Tap(Key.Escape);
        await _driver.Until(() => !_game.IsMarketOpen, "The stall panel did not close.");
        _checks.Add("At Marla's stall one ration was asked, reviewed and bought through the panel at the exact quoted total.");

        _stage = "outbound";
        CellCoordinate road = new(346, 318, corridor.Corner.Z);
        await Walk([.. CargoCorridor.Lane([corridor.OriginApproach, corridor.Corner, road]).Select(CargoCorridor.FeetAt)]);
        Require(EncounterCount == 0 && !View.IsInEncounter && CarriedRations == 1, "The outbound lane must reach the road carrying the ration.");
        await Capture("road");
        _checks.Add("Held arrow keys carried the bought ration along the outbound lane to the road cell seven cells south of the camp.");
    }

    /// <summary>Whether the published view currently carries exactly the observed, out-of-reach chief.</summary>
    private bool IsWatching(EntityId chief) => View.RoadThreats is { Count: 1 } rows && rows[0].ActorId == chief
        && rows[0].State == LocalRoadThreatState.Watching;

    /// <summary>Records one more northward approach sample when the feet moved; anything else fails.</summary>
    private SubunitPosition Approached(SubunitPosition last)
    {
        SubunitPosition now = Feet;
        if (now == last) return last;
        Require(now.X == last.X && last.Y - now.Y > 0 && (last.Y - now.Y) % WorldMovementInput.DisplacementPerStep == 0,
            "Approach moved other than north by ordinary samples.");
        _approach.Add(new { now.X, now.Y, now.Z, Seconds = _watch.Elapsed.TotalSeconds });
        return now;
    }

    private static CellCoordinate Cell(SubunitPosition feet) => new(feet.Cell.X, feet.Cell.Y, feet.Cell.Z - 1);

    /// <summary>A paid detour west of the camp outside the chief's reach, the lane to Gearford and the sale at Kegga.</summary>
    private async Task DetourAndSell(CargoCorridor corridor, CellCoordinate here)
    {
        ulong carried = CarriedRations;
        ulong balance = View.Progression!.CurrencyBalance;
        Require(carried == 1, "The journey must reach the far market carrying the bought ration.");
        _driver.Tap(Key.R);
        await _driver.Until(() => _game.CurrentTarget is null, "Clearing the selected chief did not work.");
        _stage = "detour";
        CellCoordinate rejoin = new(346, 303, here.Z);
        await Walk([.. CargoCorridor.Lane([here, new CellCoordinate(343, here.Y, here.Z), new CellCoordinate(343, rejoin.Y, here.Z), rejoin])
            .Select(CargoCorridor.FeetAt)]);
        Require(EncounterCount == 1 && !View.IsInEncounter, "The detour came within the chief's reach.");
        await Capture("detoured");
        _checks.Add("A detour three cells west of the camp passed the chief outside its reach and rejoined the lane north of it.");

        _stage = "to-gearford";
        await Walk([.. CargoCorridor.Lane([rejoin, corridor.DestinationApproach]).Select(CargoCorridor.FeetAt)]);
        await _driver.Until(() => StallOf(Kegga).IsAvailableInPerson, "Kegga's stall is not available at her approach.");
        Require(EncounterCount == 1, "The road to Gearford met another encounter.");
        await Capture("gearford");

        _stage = "sell";
        await MarketProbeEvidence.OpenStall(_driver, _game, Wheel, FarmSemanticAction.TradeOffers);
        int keggaRow = Array.FindIndex(View.Markets!.Stalls.ToArray(), stall => stall.MarketId == Kegga.MarketId) + 1;
        await _driver.Choose(Node<OptionButton>("MarketStall"), keggaRow);
        await _driver.Until(() => _game.MarketDraft.MarketId == Kegga.MarketId, "Choosing Kegga's stall did not address it.");
        await _driver.Activate(Node<Button>("SellMode"));
        await _driver.Choose(Node<OptionButton>("MarketLot"), 1);
        EntityId lot = StallOf(Kegga).OwnLots.Single().LotId;
        Require(_game.MarketDraft.LotId == lot, "The carried lot was not chosen.");
        await _driver.Activate(Node<Button>("ShowLot"));
        await _driver.Until(() => StallOf(Kegga).OwnShowing is { IsLive: true, MatchesOwnLot: true } shown && shown.Units == 1,
            "Showing the carried lot to Kegga did not apply.");
        await _driver.Activate(Node<Button>("AskMerchant"));
        await _driver.Until(() => MarketActionOffers.OpenOffer(View, StallOf(Kegga)) is { Side: SpotQuoteSide.IssuerBuys } bid
            && bid.QuantityAllowance == 1, "Kegga did not answer the sale request.");
        PaidQuoteView quote = MarketActionOffers.OpenOffer(View, StallOf(Kegga))!;
        Require(quote.MarketId == Kegga.MarketId && quote.LotId == lot, "Kegga's offer names another market or lot.");
        await _driver.Activate(Node<Button>("ReviewOffer"));
        await _driver.Until(() => _game.MarketDraft.Review?.Quote.QuoteId == quote.QuoteId, "Review did not capture the exact offer.");
        await Capture("sell-review");
        await _driver.Activate(Node<Button>("ConfirmTrade"));
        await _driver.Until(() => View.PaidQuotes.FirstOrDefault(value => value.QuoteId == quote.QuoteId)?.Status == PaidQuoteStatus.Filled
            && !_game.HasPendingAction, "The confirmed sale was not filled.");
        await _driver.Until(() => CarriedRations == 0, "The sold ration stayed in the player's pack.");
        Require(View.Progression!.CurrencyBalance == balance + quote.TotalFor(1), "The sale did not pay exactly the bid total.");
        _trade = new { QuoteId = quote.QuoteId.ToString(), LotId = lot.ToString(), Total = quote.TotalFor(1), BalanceBefore = balance,
            BalanceAfter = View.Progression.CurrencyBalance, RationsBefore = carried, RationsAfter = CarriedRations };
        await Capture("sold");
        _driver.Tap(Key.Escape);
        await _driver.Until(() => !_game.IsMarketOpen, "The stall panel did not close.");
        _checks.Add("At Kegga's stall the carried lot was shown, asked, reviewed and sold at the exact bid total.");
    }

    /// <summary>Opens the encounter wheel and selects Retreat; a stale capture fails instead of being retried.</summary>
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
            "The opening captured a stale or unavailable Retreat: " + slot?.BlockingReason);
        if (_mode == "controller") _driver.Joy(JoyButton.DpadUp);
        else if (_mode == "pointer")
        {
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
        // Check the actual rendered interval, not only the pre-draw model.
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
            Require(viewport.Encloses(Wheel.GetNode<Control>(path).GetGlobalRect()), "The rendered retreat control is clipped: " + path);
        }
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
            Kind = "native-road-threat-v1", Mode = _mode, Status = status, Failure = failure, Entry = "--cargo-threat-probe",
            OpeningSeconds = Math.Round(_openingSeconds, 3), ProbeMilliseconds = _watch.ElapsedMilliseconds,
            Paused = _game.IsLocallyPaused, WindowFocusObserved = _observedNativeFocus, LogicalFocus = _game.IsWindowFocused,
            Start = _mode is "keyboard" or "reopen" ? "farm" : "road", Reward = _reward, Purchase = _purchase,
            Unobserved = _unobserved, WarningObservation = _warningObservation,
            Inputs = _driver.Inputs, WalkInputs = _walkInputs, Warning = _warning, WarningDetail = _warningDetail,
            Approach = _approach, Engagement = _engagement, Hit = _hit, Inspected = _inspected, Openings = _openings,
            Positions = _positions, HoldOff = _holdOff, Checks = _checks, Images = _images, Notice = _notice, Trade = _trade,
            Record = record is null ? null : new { Status = record.Status.ToString(), Reason = record.Reason.ToString(),
                record.SamplesTaken, record.RouteSamples, record.ReadinessCharged, record.WorldSequence,
                Final = new { record.Final.X, record.Final.Y, record.Final.Z }, ActorId = record.ActorId.ToString(),
                CommandId = record.CommandId.ToString() },
            EncounterCount = view?.EncounterLifecycle?.EncounterCount,
            FinalFeet = feet is { } at ? new { at.X, at.Y, at.Z } : null,
            FinalHp = view?.Progression?.Hp,
            FinalBalance = view?.Progression?.CurrencyBalance,
            CarriedRations = rations
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static object SpriteRect(Rect2 bounds) => new
    {
        X = bounds.Position.X, Y = bounds.Position.Y, Width = bounds.Size.X, Height = bounds.Size.Y
    };

    private static void Require(bool condition, string message) => MarketProbeDriver.Require(condition, message);
}
