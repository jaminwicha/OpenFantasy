using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>
/// Disposable native refusal journeys over closed real-authority fixtures, on one named injected route. Each opens a
/// paused save holding Wren's genuine offer, reviews it, proves Confirm is refused for exactly one reason while time runs
/// and every other condition holds, attempts confirmation anyway, then recovers lawfully: an expired offer by asking
/// again, an unfunded one by earning the quest reward and re-reviewing, and a moved issuer by re-reviewing after she
/// returns. Nothing is requoted or retargeted; the journal records only what the player actually confirmed.
/// </summary>
internal sealed partial class MarketBoundaryPlayProbe : Node
{
    private readonly FarmGame _game;
    private readonly string _reportPath;
    private readonly string _scenario;
    private readonly MarketProbeDriver _driver;
    private readonly List<string> _checks = [];
    private readonly List<object> _observations = [];
    private string _stage = "opening";
    private string? _initial;
    private EntityId? _quote;
    private EntityId? _recoveredQuote;
    private ulong? _reviewedIssuerRevision;
    private ulong? _attemptStep;
    private ulong? _recoveredIssuerRevision;
    private string? _refusal;
    private bool _observedNativeFocus;

    internal MarketBoundaryPlayProbe(FarmGame game, string reportPath, string scenario, string route)
    {
        _game = game;
        _reportPath = reportPath;
        _scenario = scenario;
        _driver = new MarketProbeDriver(this, game, route, () => _stage, 150);
    }

    private LocalWorldView View => _game.CurrentView ?? throw new InvalidOperationException("No public world.");
    private LocalMarketStallView Stall => View.Markets!.Stalls.Single();
    private FarmMarketPanel Panel => _game.GetNode<FarmMarketPanel>("FarmMarketPanel");
    private T Node<T>(string name) where T : Node => Panel.GetNode<T>("%" + name);

    public override void _Process(double delta)
    {
        if (DisplayServer.GetName() != "headless" && GetWindow().HasFocus()) _observedNativeFocus = true;
    }

    public override async void _Ready()
    {
        try
        {
            await _driver.Until(() => _game.CurrentView?.Markets?.Stalls.Count == 1
                && _game.CurrentOffers.ContainsKey(FarmSemanticAction.TradeBuy), "Market world did not open.", 30);
            _game.InjectFixtureFocus(true);
            PaidQuoteView offer = View.PaidQuotes.Single();
            Require(_game.IsLocallyPaused && Stall.IsAvailableInPerson && offer.Status == PaidQuoteStatus.Open
                && MarketActionOffers.BelongsTo(Stall, offer), "The boundary fixture must open paused beside Wren with her open offer.");
            _quote = offer.QuoteId;
            _initial = MarketProbeEvidence.PublicHash(View);
            Observe("opened");
            await Review(offer);
            switch (_scenario)
            {
                case "expired": await Expire(offer); break;
                case "unfunded": await Unfunded(offer); break;
                case "moved": await Moved(offer); break;
                default: throw new InvalidOperationException("Unknown boundary scenario " + _scenario);
            }
            await _driver.Activate(Node<Button>("PauseToRead"));
            await _driver.Until(() => _game.IsLocallyPaused && !_game.HasPendingAction, "The recovery did not settle paused.");
            await Capture("recovered");
            _driver.Tap(Key.Escape);
            await _driver.Until(() => !_game.IsMarketOpen, "The stall panel did not close.");
            Observe("final");
            _driver.Deadline();
            Report("passed", null);
            _game.RequestClose();
        }
        catch (Exception error)
        {
            Report("failed", error.ToString());
            GD.PushError("Native market boundary probe failed: " + error);
            GetTree().Quit(1);
        }
    }

    /// <summary>Opens the stall from the wheel, chooses the offer's units and reviews it while paused.</summary>
    private async Task Review(PaidQuoteView offer)
    {
        _stage = "review";
        await MarketProbeEvidence.OpenStall(_driver, _game, _game.GetNode<FarmQuickWheel>("FarmQuickWheel"), FarmSemanticAction.TradeBuy);
        while (_game.MarketDraft.Quantity < offer.QuantityAllowance) await _driver.Activate(Node<Button>("MarketMore"));
        await _driver.Activate(Node<Button>("ReviewOffer"));
        await _driver.Until(() => _game.MarketDraft.Review?.Quote.QuoteId == offer.QuoteId, "Review did not capture the fixture offer.");
        _reviewedIssuerRevision = _game.MarketDraft.Review!.Quote.ObservedIssuerRevision;
        Require(MarketProbeEvidence.PublicHash(View) == _initial, "Opening and reviewing changed the paused world.");
        await Capture("review");
        _checks.Add("The paused wheel opens the stall and the route reviews Wren's exact prepared offer without a command.");
    }

    private async Task Expire(PaidQuoteView offer)
    {
        _stage = "refuse";
        Require(offer.ExpiresAtStep - View.Markets!.AdvancingStep <= 40 && View.Progression!.CurrencyBalance >= offer.TotalFor(offer.QuantityAllowance),
            "The near-expiry fixture is not funded or not near expiry.");
        await Resume();
        await _driver.Until(() => Status(offer.QuoteId) == PaidQuoteStatus.Expired, "The offer did not reach its authored expiry.");
        await Refuse("expired", offer, funded: true);
        _stage = "recover";
        await _driver.Activate(Node<Button>("BackFromReview"));
        await _driver.Activate(Node<Button>("AskMerchant"));
        await _driver.Until(() => MarketActionOffers.OpenOffer(View, Stall) is { } fresh && fresh.QuoteId != offer.QuoteId,
            "Asking again after expiry did not receive Wren's new offer.");
        Require(Status(offer.QuoteId) == PaidQuoteStatus.Expired, "The expired offer was renewed or reopened.");
        await ConfirmCurrent();
        _checks.Add("After expiry the player asks again, and only Wren's new answer is reviewed and confirmed; the old terms stay expired.");
    }

    private async Task Unfunded(PaidQuoteView offer)
    {
        _stage = "refuse";
        await Resume();
        await _driver.Until(() => Blocker()?.Contains("You need", StringComparison.Ordinal) == true, "Confirm was not refused for funds.");
        Require(View.Progression!.CurrencyBalance < offer.TotalFor(offer.QuantityAllowance), "The purse could pay.");
        await Refuse("You need", offer, funded: false);
        _stage = "earn";
        await _driver.Activate(Node<Button>("BackFromReview"));
        _driver.Tap(Key.Escape);
        await _driver.Until(() => !_game.IsMarketOpen, "The stall did not close for the conversation.");
        await World(Key.E, () => View.Dialogue is not null, "Wren's conversation did not open.");
        await World(Key.G, () => View.Quests.Any(quest => quest.Status is not null), "The task was not accepted.");
        await World(Key.G, () => View.Progression!.CurrencyBalance >= offer.TotalFor(offer.QuantityAllowance), "The reward was not paid.");
        await World(Key.E, () => View.Dialogue is null, "The conversation did not close.");
        _checks.Add("The player earns the ordinary quest reward natively: talk, accept, deliver and continue.");
        await Returned(offer);
        _stage = "recover";
        await MarketProbeEvidence.OpenStall(_driver, _game, _game.GetNode<FarmQuickWheel>("FarmQuickWheel"), FarmSemanticAction.TradeBuy);
        await ConfirmCurrent(offer.QuoteId);
        _checks.Add("With earned money the same still-open offer is reviewed again and confirmed; nothing was requoted.");
    }

    private async Task Moved(PaidQuoteView offer)
    {
        _stage = "refuse";
        Require(View.Progression!.CurrencyBalance >= offer.TotalFor(offer.QuantityAllowance), "The moved-issuer fixture is not funded.");
        await Resume();
        await _driver.Until(() => Blocker() is { } reason && (reason.Contains("moved since you reviewed", StringComparison.Ordinal)
            || reason.Contains("within reach", StringComparison.Ordinal)), "Confirm was not refused after Wren moved.", 30);
        await Refuse(Blocker()!.Contains("moved since", StringComparison.Ordinal) ? "moved since" : "within reach", offer, funded: true);
        await Returned(offer);
        _stage = "recover";
        await _driver.Activate(Node<Button>("BackFromReview"));
        await ConfirmCurrent(offer.QuoteId);
        Require(_recoveredIssuerRevision != _reviewedIssuerRevision, "The re-review did not capture Wren's new presence.");
        _checks.Add("After Wren returns, an explicit re-review captures her new presence and the same offer is confirmed.");
    }

    /// <summary>
    /// With time running, the reviewed Confirm is disabled for the intended reason only. The route's activation on it
    /// sends nothing and moves no goods or money.
    /// </summary>
    private async Task Refuse(string reason, PaidQuoteView offer, bool funded)
    {
        Button confirm = Node<Button>("ConfirmTrade");
        await _driver.Until(() => confirm.Disabled && Node<Label>("ConfirmText").Text.Contains(reason, StringComparison.Ordinal),
            "The confirmation card does not explain the refusal: " + reason);
        Require(!_game.IsLocallyPaused && !_game.HasPendingAction && _game.IsWindowFocused,
            "Time must run with no pending action and focus held, so only the intended reason blocks.");
        Require(funded == View.Progression!.CurrencyBalance >= offer.TotalFor(offer.QuantityAllowance), "Funding differs from the scenario.");
        ulong purse = View.Progression.CurrencyBalance;
        ulong berries = OwnBerries();
        _refusal = Node<Label>("ConfirmText").Text;
        _attemptStep = View.Markets!.AdvancingStep;
        Observe("before-attempt");
        await Capture("refused");
        await _driver.Attempt(confirm);
        await _driver.Delay(0.3);
        Require(!_game.HasPendingAction && Status(offer.QuoteId) != PaidQuoteStatus.Filled
            && View.Progression!.CurrencyBalance == purse && OwnBerries() == berries,
            "An attempted refused confirmation sent a command or moved goods or money.");
        Observe("after-attempt");
        _checks.Add($"Confirm is refused ({reason}) with time running; the route's activation on it sends nothing and keeps goods and money.");
    }

    /// <summary>Waits through ordinary running time until Wren finishes her work and stands within reach again.</summary>
    private async Task Returned(PaidQuoteView offer)
    {
        _stage = "wait-" + _scenario;
        ulong? seen = null;
        int stable = 0;
        await _driver.Until(() =>
        {
            ulong? revision = View.Actors.FirstOrDefault(actor => actor.ActorId == offer.IssuerActorId)?.PresenceRevision;
            stable = revision == seen ? stable + 1 : 0;
            seen = revision;
            return View.Construction.All(project => project.IsCompleted) && Stall.IsAvailableInPerson && stable > 30;
        }, "Wren did not finish her work and return within reach.", 90);
        Require(Status(offer.QuoteId) == PaidQuoteStatus.Open, "The prepared offer closed before Wren returned.");
        Observe("returned");
    }

    /// <summary>Reviews the stall's current open offer (optionally a named one) and confirms it with time running.</summary>
    private async Task ConfirmCurrent(EntityId? expected = null)
    {
        if (_game.IsLocallyPaused) await Resume();
        await _driver.Activate(Node<Button>("ReviewOffer"));
        await _driver.Until(() => _game.MarketDraft.Review is not null, "The current offer was not reviewed.");
        FarmMarketReview review = _game.MarketDraft.Review!;
        Require(expected is null || review.Quote.QuoteId == expected, "Review captured a different offer.");
        _recoveredQuote = review.Quote.QuoteId;
        _recoveredIssuerRevision = review.Quote.ObservedIssuerRevision;
        ulong purse = View.Progression!.CurrencyBalance;
        ulong berries = OwnBerries();
        await _driver.Activate(Node<Button>("ConfirmTrade"));
        await _driver.Until(() => !_game.HasPendingAction && Status(review.Quote.QuoteId) == PaidQuoteStatus.Filled,
            "The recovered confirmation did not settle.");
        Require(OwnBerries() == berries + review.Units && View.Progression!.CurrencyBalance == purse - review.Quote.TotalFor(review.Units),
            "The recovered purchase did not move exactly the reviewed goods and money.");
        Observe("recovered");
    }

    private async Task Resume()
    {
        await _driver.Activate(Node<Button>("PauseToRead"));
        await _driver.Until(() => !_game.IsLocallyPaused, "Time did not resume.");
    }

    /// <summary>One ordinary world key with time running, then its settled effect.</summary>
    private async Task World(Key key, Func<bool> effect, string failure)
    {
        if (_game.IsLocallyPaused) _driver.Tap(Key.Escape);
        await _driver.Until(() => !_game.IsLocallyPaused && !_game.HasPendingAction, "World time is not running for " + key);
        _driver.Tap(key);
        await _driver.Until(() => !_game.HasPendingAction && effect(), failure);
        Observe("world-" + key);
    }

    private string? Blocker() => _game.CurrentOffers.GetValueOrDefault(FarmSemanticAction.ConfirmTrade)?.BlockingReason;
    private PaidQuoteStatus? Status(EntityId id) => View.PaidQuotes.FirstOrDefault(quote => quote.QuoteId == id)?.Status;
    private ulong OwnBerries() => Stall.OwnLots.Aggregate(0UL, (sum, lot) => sum + lot.Quantity);
    private void Observe(string label) => _observations.Add(MarketProbeEvidence.Observe(label, _game, View, GetViewport()));

    private async Task Capture(string stage)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using Image image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(Path.ChangeExtension(_reportPath, null) + "." + stage + ".png") == Error.Ok, "Boundary image could not be saved.");
    }

    private void Report(string status, string? error) => File.WriteAllText(Path.ChangeExtension(_reportPath, null)
        + (status == "passed" ? ".json" : ".failed.json"), JsonSerializer.Serialize(new
        {
            Kind = "native-trading-boundary-v1", Scenario = _scenario, Route = _driver.Route, Status = status, Error = error, Stage = _stage,
            InitialHash = _initial, FinalHash = _game.CurrentView is null ? null : MarketProbeEvidence.PublicHash(View),
            QuoteId = _quote?.ToString(), RecoveredQuoteId = _recoveredQuote?.ToString(), ReviewedIssuerRevision = _reviewedIssuerRevision,
            RecoveredIssuerRevision = _recoveredIssuerRevision, AttemptStep = _attemptStep, Refusal = _refusal,
            Checks = _checks, Inputs = _driver.Inputs, Observations = _observations, Display = DisplayServer.GetName(), FocusRequests = 0,
            Unfocusable = GetWindow().Unfocusable, NativeFocused = GetWindow().HasFocus(), ObservedNativeFocus = _observedNativeFocus,
            StartupNoFocus = ProjectSettings.GetSetting("display/window/size/no_focus").AsBool(),
            _game.IsLocallyPaused, DurationMilliseconds = _driver.ElapsedMilliseconds,
            Limit = "Preparation is a closed real-authority fixture; review, refusal, attempted confirmation and recovery are native "
                + "on one injected route. No physical device, OS focus or multiplayer Wait is exercised."
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");

    private static void Require(bool condition, string message) => MarketProbeDriver.Require(condition, message);
}
