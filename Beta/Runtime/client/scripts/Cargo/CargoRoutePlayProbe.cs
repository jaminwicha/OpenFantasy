using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.NativeCargo;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client.Cargo;

/// <summary>
/// Disposable native cargo journey over an exported fixture with earned funds. Keyboard walking (held arrow keys
/// through the ordinary input map) carries the player from the farm to Marla in Brasshollow, along the whole authored
/// corridor to Kegga in Gearford and back again. Stall choice, buying, showing, selling and history inspection use the
/// production wheel and stall panel through the same injected keyboard route. Every walking sample checks that the
/// ground, the drawn player, the camera and the observed regions agree with one projection. A separate process
/// reopens paused and inspects both stalls' saved outcomes without advancing the world.
/// </summary>
internal sealed partial class CargoRoutePlayProbe : Node
{
    internal const ulong TradeUnits = 2;
    private const double ProbeLimitSeconds = 720;
    private const double OpeningSeconds = 30;

    /// <summary>A leg that makes no progress for this long is planned again from the actual feet.</summary>
    private const double ReplanSeconds = 2;
    private const int MaximumReplans = 12;
    private static readonly Key[] _walkKeys = [Key.Left, Key.Right, Key.Up, Key.Down];
    private readonly FarmGame _game;
    private readonly string _reportPath;
    private readonly string _genesisPath;
    private readonly string _metadataPath;
    private readonly bool _reopening;
    private readonly MarketProbeDriver _driver;
    private readonly List<string> _checks = [];
    private readonly List<object> _observations = [];
    private readonly List<object> _walkInputs = [];
    private readonly List<object> _walks = [];
    private readonly List<object> _agreement = [];
    private readonly List<string> _images = [];
    private readonly CargoRenderedEvidence _rendered;
    private object? _historyRead;
    private object? _stallChoice;
    private Key? _held;
    private string _stage = "opening";
    private string? _initial;
    private EntityId? _buyQuote;
    private EntityId? _sellQuote;
    private EntityId? _cargoLot;
    private bool _observedNativeFocus;
    private CargoCorridor _corridor = null!;
    private EntityId _player;
    private readonly Dictionary<string, ulong> _purses = [];

    internal CargoRoutePlayProbe(FarmGame game, string reportPath, string genesisPath, string metadataPath, string mode)
    {
        _game = game;
        _reportPath = reportPath;
        _genesisPath = genesisPath;
        _metadataPath = metadataPath;
        _reopening = mode == "reopen";
        _driver = new MarketProbeDriver(this, game, "keyboard", () => _stage, ProbeLimitSeconds);
        _rendered = new CargoRenderedEvidence(game);
    }

    private LocalWorldView View => _game.CurrentView ?? throw new InvalidOperationException("No public world.");
    private FarmQuickWheel Wheel => _game.GetNode<FarmQuickWheel>("FarmQuickWheel");
    private FarmMarketPanel Panel => _game.GetNode<FarmMarketPanel>("FarmMarketPanel");
    private T Node<T>(string name) where T : Node => Panel.GetNode<T>("%" + name);
    private NativeCargoStall Marla => _game.CargoStalls.Single(stall => stall.MerchantSourceId == CargoCircuitSources.OriginMerchantId);
    private NativeCargoStall Kegga => _game.CargoStalls.Single(stall => stall.MerchantSourceId == CargoCircuitSources.DestinationMerchantId);
    private LocalMarketStallView StallOf(NativeCargoStall stall) => View.Markets!.Stalls.Single(value => value.MarketId == stall.MarketId);
    private SubunitPosition Feet => View.Actors.Single(actor => actor.ActorId == View.PlayerActorId).Feet;

    public override void _Process(double delta)
    {
        if (DisplayServer.GetName() != "headless" && GetWindow().HasFocus()) _observedNativeFocus = true;
        _rendered.ObserveFocus(GetWindow());
    }

    public override async void _Ready()
    {
        try
        {
            await _driver.Until(() => _game.CurrentView?.Markets?.Stalls.Count == 2 && _game.Canvas.Regional is not null,
                "Cargo world did not open.", OpeningSeconds);
            _game.InjectFixtureFocus(true);
            WorldSnapshot genesis = WorldSnapshotCodec.Decode(File.ReadAllBytes(_genesisPath));
            _corridor = NativeCargoWorld.Corridor(genesis);
            _player = View.PlayerActorId;
            using (var metadata = JsonDocument.Parse(File.ReadAllText(_metadataPath)))
            {
                Require(metadata.RootElement.GetProperty("Kind").GetString() == "native-cargo-fixture-v1"
                    && metadata.RootElement.GetProperty("PlayerActorId").GetString() == _player.ToString(),
                    "The fixture metadata names another world or player.");
            }
            Require(_game.IsLocallyPaused && !View.IsInEncounter, "The cargo fixture must open paused in exploration.");
            _initial = MarketProbeEvidence.PublicHash(View);
            Observe("opened");
            if (_reopening) await Reopen();
            else await Exercise();
            Require(_game.IsLocallyPaused && !_game.HasPendingAction && !_game.IsMarketOpen, "The journey did not end paused and closed.");
            Observe("final");
            await Capture(_reopening ? "reopen-final" : "final");
            _driver.Deadline();
            Report("passed", null);
            _game.RequestClose();
        }
        catch (Exception error)
        {
            Release();
            Report("failed", error.ToString());
            GD.PushError("Native cargo probe failed: " + error);
            GetTree().Quit(1);
        }
    }

    private async Task Exercise()
    {
        _purses["start"] = View.Progression!.CurrencyBalance;
        Require(_purses["start"] > 0 && View.PaidQuotes.Count == 0 && OwnRations(Marla) == 0,
            "The fixture must start with earned funds, no quotes and no carried rations.");
        Require(!StallOf(Marla).IsAvailableInPerson && !StallOf(Kegga).IsAvailableInPerson,
            "Neither distant stall may be available from the farm.");

        _stage = "farm";
        await Agree("farm-start");
        await _rendered.Check("farm-start", CaptureImage);
        await Capture("farm-start");
        _driver.Tap(Key.Escape);
        await _driver.Until(() => !_game.IsLocallyPaused, "Escape did not resume world time.");
        await CheckTargetCue();
        _checks.Add("The exported fixture opens paused at the farm with earned funds; Escape resumes ordinary time.");

        _stage = "farm-offers";
        // Offers at the farm, before visiting Marla: no stall is in reach, so nothing may be pinned as a choice.
        await OpenStall(FarmSemanticAction.TradeOffers);
        Require(!_game.MarketDraft.IsExplicit && _game.MarketDraft.ChosenMarketId is null
            && Node<OptionButton>("MarketStall").Selected == 0 && Node<Button>("AskMerchant").Disabled,
            "Opening Offers at the farm pinned an arbitrary stall as the player's choice.");
        await Capture("farm-offers");
        await CloseStall();
        Require(_game.MarketDraft.MarketId is null, "Closing the farm Offers panel kept a stall that was only looked at.");
        _checks.Add("Offers opened at the farm, before visiting Marla, looks at a stall without choosing it; closing forgets it.");

        _stage = "walk-to-brasshollow";
        // The same authored lane the managed journey walks: cardinal legs of at most eight cells for the bounded search.
        CellCoordinate start = new(Feet.Cell.X, Feet.Cell.Y, Feet.Cell.Z - 1);
        await Walk("farm-to-brasshollow", CargoCorridor.Lane([start, new CellCoordinate(start.X, _corridor.OriginApproach.Y, start.Z),
            _corridor.OriginApproach]));
        await _driver.Until(() => StallOf(Marla).IsAvailableInPerson, "Marla's stall is not available at her approach.");
        await Capture("brasshollow");

        _stage = "buy-marla";
        Require(_game.CurrentOffers[FarmSemanticAction.TradeBuy].TargetId == Marla.MerchantActorId
            && _game.CurrentOffers[FarmSemanticAction.TradeBuy].CanExecute,
            "After the farm Offers visit the wheel's Buy does not address Marla, who is in reach.");
        await OpenStall(FarmSemanticAction.TradeBuy);
        Require(_game.MarketDraft.MarketId == Marla.MarketId && _game.MarketDraft.IsExplicit
            && Node<Label>("MarketTitle").Text.Contains("'s stall · Brasshollow",
            StringComparison.Ordinal) && Node<Control>("StallRow").Visible, "The panel does not name Marla's Brasshollow stall.");
        await _driver.Activate(Node<Button>("MarketMore"));
        Require(_game.MarketDraft.Quantity == TradeUnits, "Quantity selection failed.");
        await _driver.Activate(Node<Button>("AskMerchant"));
        await _driver.Until(() => OpenOffer(Marla) is { Side: SpotQuoteSide.IssuerSells } offer && offer.QuantityAllowance == TradeUnits,
            "Marla did not answer the request.");
        PaidQuoteView buy = OpenOffer(Marla)!;
        Require(buy.MarketId == Marla.MarketId, "The offer is not associated with Marla's market by its canonical record.");
        _buyQuote = buy.QuoteId;
        await Confirm(buy);
        await _driver.Until(() => OwnRations(Marla) == TradeUnits, "The bought rations did not reach the player's pack.");
        _cargoLot = StallOf(Marla).OwnLots.Single().LotId;
        _purses["afterBuy"] = View.Progression!.CurrencyBalance;
        Require(_purses["afterBuy"] == _purses["start"] - buy.TotalFor(TradeUnits), "The purchase did not pay exactly the quoted total.");
        await Capture("brasshollow-bought");
        await CloseStall();
        _checks.Add("At Brasshollow the wheel opened Marla's stall by its market identity; two rations were asked, reviewed and bought at the exact quoted total.");
        await ReadHistoryWhileRendering();

        _stage = "walk-outbound";
        await Walk("brasshollow-to-gearford", _corridor.Outbound);
        await _driver.Until(() => StallOf(Kegga).IsAvailableInPerson, "Kegga's stall is not available at her approach.");
        Require(!StallOf(Marla).IsAvailableInPerson, "Marla's stall stayed available from Gearford.");
        await Capture("gearford");

        _stage = "choose-kegga";
        // Arriving at Kegga with Marla chosen: the wheel stays on Marla and explains the Offers → Stall route to Kegga.
        string keggaName = FarmMarketTerms.Merchant(View, StallOf(Kegga));
        FarmActionOffer pinnedBuy = _game.CurrentOffers[FarmSemanticAction.TradeBuy];
        Require(pinnedBuy.TargetId == Marla.MerchantActorId && !pinnedBuy.CanExecute
            && pinnedBuy.BlockingReason!.Contains(keggaName, StringComparison.Ordinal)
            && pinnedBuy.BlockingReason.Contains("Offers", StringComparison.Ordinal)
            && pinnedBuy.BlockingReason.Contains("under Stall", StringComparison.Ordinal),
            "At Gearford the pinned Marla blocker does not name Kegga and the Offers → Stall route.");
        _stallChoice = new { Gearford = pinnedBuy.BlockingReason, Pinned = Marla.MarketId.ToString() };
        await OpenStall(FarmSemanticAction.TradeOffers);
        Require(_game.MarketDraft.MarketId == Marla.MarketId && Node<Button>("AskMerchant").Disabled
            && Node<Label>("StallPlace").Text.Contains("not within reach", StringComparison.Ordinal),
            "The earlier explicit stall choice was retargeted instead of staying Marla's unavailable stall.");
        _checks.Add("Arriving at Gearford keeps the explicit Marla choice, shown as out of reach; nothing retargets to the nearer stall.");
        int keggaRow = Array.FindIndex(View.Markets!.Stalls.ToArray(), stall => stall.MarketId == Kegga.MarketId) + 1;
        await _driver.Choose(Node<OptionButton>("MarketStall"), keggaRow);
        await _driver.Until(() => _game.MarketDraft.MarketId == Kegga.MarketId
            && Node<Label>("MarketTitle").Text.Contains("'s stall · Gearford", StringComparison.Ordinal),
            "Choosing Kegga's stall in the selector did not address it.");
        await Capture("gearford-stall-choice");

        _stage = "sell-kegga";
        await _driver.Activate(Node<Button>("SellMode"));
        await _driver.Choose(Node<OptionButton>("MarketLot"), 1);
        Require(_game.MarketDraft.LotId == _cargoLot, "The carried lot was not chosen.");
        await _driver.Activate(Node<Button>("MarketMore"));
        await _driver.Activate(Node<Button>("ShowLot"));
        await _driver.Until(() => StallOf(Kegga).OwnShowing is { IsLive: true, MatchesOwnLot: true } shown && shown.Units == TradeUnits,
            "Showing the carried lot to Kegga did not apply.");
        await _driver.Activate(Node<Button>("AskMerchant"));
        await _driver.Until(() => OpenOffer(Kegga) is { Side: SpotQuoteSide.IssuerBuys } offer && offer.QuantityAllowance == TradeUnits,
            "Kegga did not answer the sale request.");
        PaidQuoteView sell = OpenOffer(Kegga)!;
        Require(sell.MarketId == Kegga.MarketId && sell.LotId == _cargoLot, "Kegga's offer names another market or lot.");
        _sellQuote = sell.QuoteId;
        await Confirm(sell);
        await _driver.Until(() => OwnRations(Kegga) == 0, "The sold rations stayed in the player's pack.");
        _purses["afterSell"] = View.Progression!.CurrencyBalance;
        Require(_purses["afterSell"] == _purses["afterBuy"] + sell.UnitPrice * TradeUnits, "The sale did not pay exactly the bid total.");
        await Capture("gearford-sold");

        _stage = "history-kegga";
        await InspectRecord(Kegga, sell.QuoteId, "sold 2 rations");
        Require(!HistoryRows(Kegga).Any(quote => quote.QuoteId == buy.QuoteId), "Marla's purchase is listed at Kegga's stall.");
        await Capture("gearford-history");
        await CloseStall();
        _checks.Add("Kegga was chosen explicitly; the carried lot was shown, asked, reviewed and sold, and its saved record read.");

        _stage = "walk-homeward";
        await Walk("gearford-to-brasshollow", [.. _corridor.Outbound.Reverse().Skip(1), _corridor.OriginApproach]);
        await _driver.Until(() => StallOf(Marla).IsAvailableInPerson, "Marla's stall is not available on return.");

        _stage = "history-marla";
        await OpenStall(FarmSemanticAction.TradeOffers);
        Require(_game.MarketDraft.MarketId == Kegga.MarketId, "The explicit Kegga choice was retargeted on return.");
        int marlaRow = Array.FindIndex(View.Markets!.Stalls.ToArray(), stall => stall.MarketId == Marla.MarketId) + 1;
        await _driver.Choose(Node<OptionButton>("MarketStall"), marlaRow);
        await _driver.Until(() => _game.MarketDraft.MarketId == Marla.MarketId, "Choosing Marla's stall on return failed.");
        await InspectRecord(Marla, buy.QuoteId, "bought 2 rations");
        Require(!HistoryRows(Marla).Any(quote => quote.QuoteId == sell.QuoteId), "Kegga's sale is listed at Marla's stall.");
        await Capture("return-history");
        await CloseStall();
        _checks.Add("Back in Brasshollow Marla's stall is chosen explicitly again and lists only its own saved purchase.");

        _stage = "pause";
        _driver.Tap(Key.Escape);
        await _driver.Until(() => _game.IsLocallyPaused && !_game.HasPendingAction, "Escape did not pause the world.");
        await _driver.Delay(0.3);
        string held = MarketProbeEvidence.PublicHash(View);
        await _driver.Delay(0.3);
        Require(MarketProbeEvidence.PublicHash(View) == held, "The paused world changed.");
    }

    private async Task Reopen()
    {
        using var prior = JsonDocument.Parse(File.ReadAllText(Output("active", "json")));
        JsonElement active = prior.RootElement;
        Require(active.GetProperty("Status").GetString() == "passed" && active.GetProperty("FinalHash").GetString() == _initial,
            "Separate process did not restore the exact final public state.");
        _buyQuote = EntityId.Parse(active.GetProperty("BuyQuoteId").GetString());
        _sellQuote = EntityId.Parse(active.GetProperty("SellQuoteId").GetString());
        JsonElement feet = active.GetProperty("FinalFeet");
        Require(Feet == new SubunitPosition(feet.GetProperty("X").GetInt64(), feet.GetProperty("Y").GetInt64(), feet.GetProperty("Z").GetInt64()),
            "The reopened player stands somewhere else.");
        Require(_game.CurrentInquiry is null && _game.MarketDraft.Review is null && _game.MarketDraft.MarketId is null,
            "A transient request, review or stall choice was restored.");
        await Agree("reopened");
        await _rendered.Check("reopened", CaptureImage);
        _stage = "reopen";
        await OpenStall(FarmSemanticAction.TradeOffers);
        Require(_game.MarketDraft.MarketId == Marla.MarketId, "The reopened panel did not open on the stall in reach.");
        await InspectRecord(Marla, _buyQuote.Value, "bought 2 rations");
        int keggaRow = Array.FindIndex(View.Markets!.Stalls.ToArray(), stall => stall.MarketId == Kegga.MarketId) + 1;
        await _driver.Choose(Node<OptionButton>("MarketStall"), keggaRow);
        await _driver.Until(() => _game.MarketDraft.MarketId == Kegga.MarketId, "Choosing Kegga's stall after reopening failed.");
        await InspectRecord(Kegga, _sellQuote.Value, "sold 2 rations");
        await Capture("reopen");
        await CloseStall();
        await _driver.Delay(0.4);
        Require(MarketProbeEvidence.PublicHash(View) == _initial && _game.IsLocallyPaused, "Paused reopening and inspection changed the public world.");
        _checks.Add("A separate paused process restores the exact position and both stalls' saved outcomes without advancing the world.");
    }

    /// <summary>
    /// Walks authored waypoints by holding one arrow key at a time. Each leg asks the read-only route advice for the
    /// ordinary ground path from the actual feet; the held key follows it and the world validates every step. Region
    /// transitions are read from actual feet, and every walking sample checks the projection agreement.
    /// </summary>
    private async Task Walk(string label, IReadOnlyList<CellCoordinate> waypoints)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        ulong startStep = View.Markets!.AdvancingStep;
        int region = _corridor.RegionIndexOf(Feet.Cell);
        List<int> regions = [region];
        int samples = 0;
        int replans = 0;
        foreach (CellCoordinate waypoint in waypoints)
        {
            SubunitPosition target = CargoCorridor.FeetAt(waypoint);
            while (Feet != target)
            {
                // Plan from the actual feet; a leg that stalls (another body stepped into it) is planned again, boundedly.
                var (path, status) = _game.CargoRouteAdvice!.Plan(target);
                Require(path is { Count: > 0 }, $"{label}: no ground route to {waypoint} ({status}).");
                bool reached = true;
                foreach (SubunitPosition point in path!)
                {
                    var stuck = System.Diagnostics.Stopwatch.StartNew();
                    SubunitPosition last = Feet;
                    while (Feet != point && reached)
                    {
                        SubunitPosition feet = Feet;
                        long dx = point.X - feet.X;
                        long dy = point.Y - feet.Y;
                        Hold(dx != 0 ? dx > 0 ? Key.Right : Key.Left : dy > 0 ? Key.Down : Key.Up);
                        await _driver.Frame();
                        if (Feet != last)
                        {
                            last = Feet;
                            stuck.Restart();
                            int next = _corridor.RegionIndexOf(last.Cell);
                            Require(next >= 0 || label == "farm-to-brasshollow", $"{label}: the player left the admitted corridor.");
                            if (next != region)
                            {
                                regions.Add(next);
                                region = next;
                                await Agree($"{label}-region-{next}");
                                if (regions.Count == 2 && label == "brasshollow-to-gearford") await Capture("corridor-boundary");
                            }
                            else if (++samples % 24 == 0) await Agree(label);
                        }
                        reached = stuck.Elapsed.TotalSeconds < ReplanSeconds;
                    }
                    if (!reached) break;
                }
                if (!reached)
                {
                    Release();
                    Require(++replans <= MaximumReplans, $"{label}: walking made no progress towards {waypoint} after {replans} replans.");
                    _walkInputs.Add(new { Stage = _stage, Route = "keyboard", Replan = waypoint.ToString(), Step = View.Markets?.AdvancingStep });
                    await _driver.Delay(0.3);
                }
            }
        }
        Release();
        await _driver.Until(() => !_game.HasPendingAction, label + " did not settle.");
        await Agree(label + "-arrived");
        await _rendered.Check(label + "-arrived", CaptureImage);
        _walks.Add(new { Label = label, Waypoints = waypoints.Count, Regions = regions, Transitions = regions.Count - 1, Replans = replans,
            AdvancingSteps = View.Markets!.AdvancingStep - startStep, Seconds = Math.Round(watch.Elapsed.TotalSeconds, 3),
            End = new { Feet.X, Feet.Y, Feet.Z } });
        _checks.Add($"{label}: held arrow keys walked {regions.Count - 1} region transitions with agreeing ground, actors and camera.");
    }

    private void Hold(Key key)
    {
        if (_held == key) return;
        Release();
        _held = key;
        _walkInputs.Add(new { Stage = _stage, Route = "keyboard", Hold = key.ToString(), Step = View.Markets?.AdvancingStep });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
        Input.FlushBufferedEvents();
    }

    private void Release()
    {
        if (_held is not { } key) return;
        _held = null;
        _walkInputs.Add(new { Stage = _stage, Route = "keyboard", Release = key.ToString(), Step = _game.CurrentView?.Markets?.AdvancingStep });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
        Input.FlushBufferedEvents();
    }

    /// <summary>
    /// Samples agreement on a settled camera frame. A HUD text change resizes the field panels at the next layout pass,
    /// and the game frames the camera from those bounds when it next presents; waiting (at most a few frames) for that
    /// presentation keeps the unchanged exact assertions below from sampling between layout and presentation.
    /// </summary>
    private async Task Agree(string label)
    {
        for (int frame = 0; frame < 6 && !CameraMatchesField(); frame++) await _driver.Frame();
        CheckAgreement(label);
    }

    private bool CameraMatchesField()
    {
        if (_game.Canvas.Regional is not { } projection) return false;
        (_, Rect2 limits) = _game.CameraFrame;
        Rect2 viewport = GetViewport().GetVisibleRect();
        Rect2 field = _game.CargoFieldViewportBounds;
        var framing = new CargoCameraFraming(projection.Bounds,
            new CargoCanvasRect(viewport.Position.X, viewport.Position.Y, viewport.Size.X, viewport.Size.Y),
            new CargoCanvasRect(field.Position.X, field.Position.Y, field.Size.X, field.Size.Y), new CargoCanvasPoint(2, 2));
        return Math.Abs(limits.Position.X - framing.Limits.X) < 1 && Math.Abs(limits.Position.Y - framing.Limits.Y) < 1
            && Math.Abs(limits.End.X - framing.Limits.Right) < 1 && Math.Abs(limits.End.Y - framing.Limits.Bottom) < 1;
    }

    /// <summary>
    /// One agreement sample: the presented projection admits the player's authority feet, the player is drawn exactly
    /// at its projection, the observed ground (not the farm grid) is drawn and holds the player's column on intact
    /// ground, the camera limits are the observed extent, and the camera frames the player. Every visible merchant is
    /// drawn at its own projection; a merchant outside the observation is not drawn at all.
    /// </summary>
    private void CheckAgreement(string label)
    {
        CargoViewProjection projection = _game.Canvas.Regional ?? throw new InvalidOperationException("No cargo projection.");
        // The frame's own feet: the live view may already hold a newer step than the frame drawn this instant.
        SubunitPosition feet = _game.Canvas.PresentedFeet(_player) ?? throw new InvalidOperationException(label + ": no presented player.");
        SubunitPosition live = Feet;
        Require(Math.Abs(live.X - feet.X) + Math.Abs(live.Y - feet.Y) <= 4 * WorldMovementInput.DisplacementPerStep && live.Z == feet.Z,
            label + ": the presented frame is stale against the authority position.");
        Require(projection.Admits(feet), label + ": the projection does not admit the player's feet.");
        CargoCanvasPoint point = projection.Project(feet);
        Vector2 drawn = _game.Canvas.DrawnActor(_player) ?? throw new InvalidOperationException(label + ": the player is not drawn.");
        Require(Math.Abs(drawn.X - point.X) < 0.01 && Math.Abs(drawn.Y - point.Y) < 0.01, label + ": the player is drawn elsewhere.");
        // While walking, the node's actual transform eases from the previous step towards that target: it never trails by
        // more than the two 32-subunit steps (8 canvas pixels) one interpolation can span. Settled samples are exact.
        Vector2 transform = _game.Canvas.ActorTransform(_player) ?? throw new InvalidOperationException(label + ": no actor node.");
        Require(transform.DistanceTo(drawn) <= 2 * WorldMovementInput.DisplacementPerStep / CargoViewProjection.SubunitsPerPixel + 0.01f,
            label + ": the drawn actor transform trails its projection by more than one interpolation.");
        Require(_game.Canvas.DrawsObservedGround, label + ": the observed ground is not drawn.");
        var ground = projection.Observation.RegionAt(feet.Cell.X, feet.Cell.Y)?.GroundAt(feet.Cell.X, feet.Cell.Y);
        Require(ground is not null and not Hosting.Simulation.CargoPresentation.LocalGroundKind.Open, label + ": the player's column has no observed ground.");
        (Vector2 center, Rect2 limits) = _game.CameraFrame;
        Rect2 viewport = GetViewport().GetVisibleRect();
        Rect2 field = _game.CargoFieldViewportBounds;
        var framing = new CargoCameraFraming(projection.Bounds,
            new CargoCanvasRect(viewport.Position.X, viewport.Position.Y, viewport.Size.X, viewport.Size.Y),
            new CargoCanvasRect(field.Position.X, field.Position.Y, field.Size.X, field.Size.Y), new CargoCanvasPoint(2, 2));
        Require(Math.Abs(limits.Position.X - framing.Limits.X) < 1 && Math.Abs(limits.Position.Y - framing.Limits.Y) < 1
            && Math.Abs(limits.End.X - framing.Limits.Right) < 1 && Math.Abs(limits.End.Y - framing.Limits.Bottom) < 1,
            label + ": camera limits differ from the observed extent plus empty HUD padding.");
        Vector2 half = GetViewport().GetVisibleRect().Size / 2 / 2;
        Require(Math.Abs(center.X - point.X) <= half.X && Math.Abs(center.Y - point.Y) <= half.Y, label + ": the camera does not frame the player.");
        Vector2 playerViewport = _game.Canvas.GetGlobalTransformWithCanvas() * drawn;
        Require(field.HasPoint(playerViewport) && playerViewport.Y - 80 >= field.Position.Y,
            label + ": the field HUD hides the player's feet or upper sprite.");
        List<object> merchants = [];
        foreach (NativeCargoStall stall in _game.CargoStalls)
        {
            SubunitPosition at = _game.Canvas.PresentedFeet(stall.MerchantActorId)
                ?? View.Actors.Single(actor => actor.ActorId == stall.MerchantActorId).Feet;
            Vector2? shown = _game.Canvas.DrawnActor(stall.MerchantActorId);
            if (projection.Admits(at))
            {
                CargoCanvasPoint expected = projection.Project(at);
                Require(shown is { } value && Math.Abs(value.X - expected.X) < 0.01 && Math.Abs(value.Y - expected.Y) < 0.01,
                    label + ": a merchant is drawn away from its projection.");
            }
            else Require(shown is null, label + ": a merchant outside the observation is drawn.");
            merchants.Add(new { stall.MerchantSourceId, Drawn = shown is not null });
        }
        _agreement.Add(new { Label = label, Step = View.Markets?.AdvancingStep, Feet = new { feet.X, feet.Y, feet.Z },
            Canvas = new { point.X, point.Y }, Regions = projection.Observation.Regions.Select(region =>
                new { region.Bounds.Minimum.X, region.Bounds.Minimum.Y, region.Revision }),
            Camera = new { center.X, center.Y }, PlayerViewport = new { playerViewport.X, playerViewport.Y },
            FieldViewport = new { field.Position, field.Size }, Merchants = merchants });
    }

    /// <summary>
    /// A real saved-history read while time runs: one complete verified replay of the journal in the session's
    /// background history slot, under the authority's writer gate. Frames must keep rendering the published regional
    /// view throughout, while the world itself cannot advance until the read releases its slot.
    /// </summary>
    private async Task ReadHistoryWhileRendering()
    {
        _stage = "history-read";
        Require(!_game.IsLocallyPaused && !_game.IsMarketOpen, "The history read runs while time runs and no panel is open.");
        ulong step = View.Markets!.AdvancingStep;
        long framesBefore = Engine.GetFramesDrawn();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var gap = System.Diagnostics.Stopwatch.StartNew();
        double longestFrame = 0;
        int frames = 0;
        HashSet<ulong> stepsDuring = [step];
        Task<LocalHistoryVerification> read = _game.VerifyRecordedHistory();
        while (!read.IsCompleted)
        {
            await _driver.Frame();
            longestFrame = Math.Max(longestFrame, gap.Elapsed.TotalSeconds);
            gap.Restart();
            frames++;
            Require(_game.Canvas.Regional is not null && !_game.IsRegionalViewUnsupported, "The regional frame stopped presenting.");
            // Read the step before the task state: a step published after the slot's release follows the read's completion.
            ulong observed = View.Markets!.AdvancingStep;
            if (!read.IsCompleted) stepsDuring.Add(observed);
            Require(watch.Elapsed.TotalSeconds < 120, "The saved-history read did not finish within 120 seconds.");
        }
        // A cutoff already committing when the read was requested may publish once before the read takes the slot;
        // a running world would otherwise publish about twenty steps each second of the replay.
        bool advancedDuring = stepsDuring.Count > 2;
        LocalHistoryVerification verified = await read;
        double seconds = watch.Elapsed.TotalSeconds;
        Require(verified.ReproducesHead, "The verified replay did not reproduce the live head.");
        Require(frames >= 5 && longestFrame < 1.0, $"Frames stalled during the history read ({frames} frames, longest {longestFrame:0.000}s).");
        Require(!advancedDuring, "The world advanced while the history slot held the writer side.");
        _historyRead = new { Revision = verified.Revision, Seconds = Math.Round(seconds, 3), Frames = frames,
            FramesDrawn = Engine.GetFramesDrawn() - framesBefore, LongestFrameSeconds = Math.Round(longestFrame, 4),
            StepBefore = step, StepsDuring = stepsDuring.Order().ToArray(), StepAfter = View.Markets!.AdvancingStep };
        _checks.Add($"A complete verified replay to commit {verified.Revision} ran {seconds:0.00}s in the background history slot "
            + $"while {frames} frames rendered (longest {longestFrame:0.000}s) and the world held its step.");
    }

    private async Task<Image?> CaptureImage(string stage)
    {
        if (DisplayServer.GetName() == "headless") return null;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Image image = GetViewport().GetTexture().GetImage();
        string path = Output(stage, "png");
        Require(image.SavePng(path) == Error.Ok, "Cargo image could not be saved.");
        _images.Add(path);
        return image;
    }

    /// <summary>Q selects a target; its cue is drawn exactly at the target's projected feet, and R clears it.</summary>
    private async Task CheckTargetCue()
    {
        _driver.Tap(Key.Q);
        await _driver.Until(() => _game.CurrentTarget?.Feet is not null, "Q selected no target at the farm.");
        await _driver.Frame();
        FarmTarget target = _game.CurrentTarget!;
        CargoViewProjection projection = _game.Canvas.Regional!;
        if (projection.Admits(target.Feet!.Value))
        {
            CargoCanvasPoint expected = projection.Project(target.Feet.Value);
            Require(_game.Canvas.TargetCue is { } cue && Math.Abs(cue.X - expected.X) < 0.01 && Math.Abs(cue.Y - expected.Y) < 0.01,
                "The target cue is not at the target's projection.");
        }
        else Require(_game.Canvas.TargetCue is null, "A target outside the observation shows a cue.");
        _observations.Add(new { Label = "target-cue", Target = target.Name, Cue = _game.Canvas.TargetCue?.ToString() });
        _driver.Tap(Key.R);
        await _driver.Until(() => _game.CurrentTarget is null, "R did not clear the target.");
        _checks.Add("The target cue is drawn at the selected target's projected feet and clears without a command.");
    }

    private async Task OpenStall(FarmSemanticAction choice) => await MarketProbeEvidence.OpenStall(_driver, _game, Wheel, choice);

    private async Task Confirm(PaidQuoteView offer)
    {
        await _driver.Activate(Node<Button>("ReviewOffer"));
        await _driver.Until(() => _game.MarketDraft.Review?.Quote.QuoteId == offer.QuoteId, "Review did not capture the exact offer.");
        await _driver.Activate(Node<Button>("ConfirmTrade"));
        await _driver.Until(() => View.PaidQuotes.FirstOrDefault(quote => quote.QuoteId == offer.QuoteId)?.Status == PaidQuoteStatus.Filled
            && !_game.HasPendingAction, "The confirmed offer was not filled.");
        Observe("filled-" + offer.QuoteId);
    }

    private async Task CloseStall()
    {
        _driver.Tap(Key.Escape);
        await _driver.Until(() => !_game.IsMarketOpen, "The stall panel did not close.");
    }

    private PaidQuoteView[] HistoryRows(NativeCargoStall stall) => View.PaidQuotes
        .Where(quote => MarketActionOffers.BelongsTo(StallOf(stall), quote))
        .OrderByDescending(quote => quote.IssuedAtStep).ThenBy(quote => quote.QuoteId).ToArray();

    private async Task InspectRecord(NativeCargoStall stall, EntityId quoteId, string expected)
    {
        int index = Array.FindIndex(HistoryRows(stall), quote => quote.QuoteId == quoteId);
        Require(index >= 0, "The outcome is absent from its stall's list.");
        await _driver.Choose(Node<OptionButton>("MarketHistory"), index + 1);
        await _driver.Activate(Node<Button>("CheckRecord"));
        await _driver.Until(() => Node<Label>("HistoryDetails").Text.Contains("Saved record:", StringComparison.Ordinal)
            && Node<Label>("HistoryDetails").Text.Contains(expected, StringComparison.Ordinal), "Saved-record inspection did not complete.");
        _observations.Add(new { Label = "record-" + stall.MerchantSourceId, Quote = quoteId.ToString(), Text = Node<Label>("HistoryDetails").Text });
    }

    private PaidQuoteView? OpenOffer(NativeCargoStall stall) => MarketActionOffers.OpenOffer(View, StallOf(stall));
    private ulong OwnRations(NativeCargoStall stall) => StallOf(stall).OwnLots.Aggregate(0UL, (sum, lot) => sum + lot.Quantity);

    private void Observe(string label) => _observations.Add(MarketProbeEvidence.Observe(label, _game, View, GetViewport()));

    private async Task Capture(string stage)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
        string path = Output(stage, "png");
        Require(image.SavePng(path) == Error.Ok, "Cargo image could not be saved.");
        _images.Add(path);
    }

    private string Output(string stage, string extension) => Path.ChangeExtension(_reportPath, null) + "." + stage + "." + extension;

    private void Report(string status, string? error)
    {
        SubunitPosition? feet = _game.CurrentView is null ? null : Feet;
        File.WriteAllText(Output(_reopening ? "reopen" : "active", status == "passed" ? "json" : "failed.json"), JsonSerializer.Serialize(new
        {
            Kind = "native-cargo-v1", Mode = _reopening ? "reopen" : "keyboard", Route = "keyboard", Status = status, Error = error,
            Stage = _stage, Genesis = _genesisPath, InitialHash = _initial,
            FinalHash = _game.CurrentView is null ? null : MarketProbeEvidence.PublicHash(View),
            PlayerActorId = _player.ToString(), MarlaMarketId = _game.CargoStalls.Count == 2 ? Marla.MarketId.ToString() : null,
            KeggaMarketId = _game.CargoStalls.Count == 2 ? Kegga.MarketId.ToString() : null,
            BuyQuoteId = _buyQuote?.ToString(), SellQuoteId = _sellQuote?.ToString(), CargoLotId = _cargoLot?.ToString(), TradeUnits,
            Purses = _purses, FinalFeet = feet is { } value ? new { value.X, value.Y, value.Z } : null,
            Walks = _walks, Agreement = _agreement, Checks = _checks, Inputs = _driver.Inputs, WalkInputs = _walkInputs,
            Observations = _observations, Images = _images, Display = DisplayServer.GetName(),
            Focus = _rendered.Focus(GetWindow()), Rendered = _rendered.Samples, HistoryRead = _historyRead, StallChoice = _stallChoice,
            Unfocusable = GetWindow().Unfocusable, NativeFocused = GetWindow().HasFocus(), ObservedNativeFocus = _observedNativeFocus,
            StartupNoFocus = ProjectSettings.GetSetting("display/window/size/no_focus").AsBool(),
            _game.IsLocallyPaused, _game.IsWindowFocused, DurationMilliseconds = _driver.ElapsedMilliseconds,
            Limit = "Walking holds injected arrow keys through the ordinary input map; read-only route advice only chooses which key. "
                + "Stall choice, trades and history use the production wheel and panel with injected keyboard events. Focus is "
                + "injected through the fixture hook. Physical devices, OS focus, controller/pointer cargo routes, art acceptance, "
                + "streaming, threat/escort and production gates remain open."
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    private static void Require(bool condition, string message) => MarketProbeDriver.Require(condition, message);
}
