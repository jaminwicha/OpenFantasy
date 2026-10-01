using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Markets;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>
/// Disposable native market journey over an earned fixture: the requested device route opens the stall from the
/// wheel, asks, reads while paused, reviews, discards on a changed choice and on focus loss, confirms one exact
/// purchase, receives a genuine decline, cancels and then chooses a lot row, shows and sells one lot, inspects a saved
/// record and closes. Focus moves only through the route's own navigation events. A separate process reopens paused
/// and inspects the saved outcomes without advancing the world.
/// </summary>
internal sealed partial class MarketTradingPlayProbe : Node
{
    private const ulong BuyUnits = 4;
    private const ulong SellUnits = 1;
    private const string ReopenPrefix = "reopen-";
    private readonly FarmGame _game;
    private readonly string _reportPath;
    private readonly string _genesisPath;
    private readonly string _mode;
    /// <summary>A paused reopening names the device route it uses, such as "reopen-controller".</summary>
    private readonly bool _reopening;
    private readonly string _route;
    private readonly MarketProbeDriver _driver;
    private readonly List<string> _checks = [];
    private readonly List<object> _observations = [];
    private string _stage = "opening";
    private string? _initial;
    private EntityId? _buyQuote;
    private EntityId? _sellQuote;
    private EntityId? _soldLot;
    private bool _observedNativeFocus;

    internal MarketTradingPlayProbe(FarmGame game, string reportPath, string genesisPath, string mode)
    {
        _game = game;
        _reportPath = reportPath;
        _genesisPath = genesisPath;
        _mode = mode;
        _reopening = mode.StartsWith(ReopenPrefix, StringComparison.Ordinal);
        _route = _reopening ? mode[ReopenPrefix.Length..] : mode;
        _driver = new MarketProbeDriver(this, game, _route, () => _stage, 150);
    }

    private LocalWorldView View => _game.CurrentView ?? throw new InvalidOperationException("No public world.");
    private FarmQuickWheel Wheel => _game.GetNode<FarmQuickWheel>("FarmQuickWheel");
    private FarmMarketPanel Panel => _game.GetNode<FarmMarketPanel>("FarmMarketPanel");
    private LocalMarketStallView Stall => View.Markets!.Stalls.Single();
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
            Require(_game.IsLocallyPaused && !View.IsInEncounter && Stall.IsAvailableInPerson,
                "Market fixture must open paused in exploration beside Wren's stall.");
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
            Report("failed", error.ToString());
            GD.PushError("Native market probe failed: " + error);
            GetTree().Quit(1);
        }
    }

    private async Task Exercise()
    {
        ulong purse = View.Progression!.CurrencyBalance;
        ulong berries = OwnBerries();
        Require(View.PaidQuotes.Count == 0 && Stall.OwnShowing is null && berries == 0,
            "The fixture must start without quotes, showings or own berries.");

        _stage = "open-" + _mode;
        await MarketProbeEvidence.OpenStall(_driver, _game, Wheel, FarmSemanticAction.TradeBuy);
        Require(MarketProbeEvidence.PublicHash(View) == _initial && _game.MarketDraft.Direction == LocalTradeDirection.Buy
            && _game.MarketDraft.Review is null, "Opening the stall from a paused wheel changed the world or preselected terms.");
        _checks.Add("A paused wheel choice opens the stall for inspection without a command or preselected terms.");

        _stage = "ask-buy-" + _mode;
        await _driver.Activate(Node<Button>("PauseToRead"));
        await _driver.Until(() => !_game.IsLocallyPaused, "The stall's explicit pause control did not resume time.");
        for (ulong unit = 1; unit < BuyUnits; unit++) await _driver.Activate(Node<Button>("MarketMore"));
        Require(_game.MarketDraft.Quantity == BuyUnits, "Quantity selection failed.");
        await _driver.Activate(Node<Button>("AskMerchant"));
        await _driver.Until(() => OpenOffer() is { Side: SpotQuoteSide.IssuerSells } offer && offer.QuantityAllowance == BuyUnits,
            "Wren did not answer the request while the open panel kept time running.");
        PaidQuoteView buy = OpenOffer()!;
        _buyQuote = buy.QuoteId;
        await _driver.Activate(Node<Button>("PauseToRead"));
        await Settle("Reading pause");
        ulong readStep = View.Markets!.AdvancingStep;
        await _driver.Delay(0.4);
        Require(View.Markets!.AdvancingStep == readStep && OpenOffer()?.QuoteId == buy.QuoteId,
            "A reading pause aged or replaced the offer.");
        string inspector = Node<Label>("OfferDetails").Text;
        Require(inspector.Contains($"{buy.UnitPrice} ", StringComparison.Ordinal)
            && inspector.Contains("while time runs", StringComparison.Ordinal) && !inspector.Contains("step", StringComparison.Ordinal),
            "The inspector does not show the exact price and remaining time in player terms.");
        await Capture("buy-offer");
        _checks.Add("The panel left time running so Wren answered; an explicit reading pause then held the exact offer and its window.");

        _stage = "review-buy-" + _mode;
        string held = MarketProbeEvidence.PublicHash(View);
        await _driver.Activate(Node<Button>("ReviewOffer"));
        await _driver.Until(() => _game.MarketDraft.Review?.Quote.QuoteId == buy.QuoteId, "Review did not capture the exact offer.");
        Require(Node<Button>("ConfirmTrade").Disabled && Node<Label>("ConfirmText").Text.Contains("Resume world time", StringComparison.Ordinal),
            "Confirmation was available while reading paused.");
        await Capture("buy-review");
        await _driver.Activate(Node<Button>("BackFromReview"));
        Require(_game.MarketDraft.Review is null && !Node<Control>("ConfirmBox").Visible, "Back kept the reviewed offer.");
        await _driver.Activate(Node<Button>("ReviewOffer"));
        Require(_game.MarketDraft.Review?.Quote.QuoteId == buy.QuoteId, "Explicit re-review after Back failed.");
        await _driver.Activate(Node<Button>("MarketLess"));
        Require(_game.MarketDraft.Review is null && !Node<Control>("ConfirmBox").Visible, "A changed quantity kept the reviewed offer.");
        await _driver.Activate(Node<Button>("MarketMore"));
        await _driver.Activate(Node<Button>("ReviewOffer"));
        Require(_game.MarketDraft.Review?.Quote.QuoteId == buy.QuoteId, "Re-review after an explicit change failed.");
        _game.InjectFixtureFocus(false);
        await _driver.Until(() => !_game.IsWindowFocused && _game.MarketDraft.Review is null, "Focus loss kept the reviewed offer armed.");
        _game.InjectFixtureFocus(true);
        await _driver.Delay(0.25);
        Require(MarketProbeEvidence.PublicHash(View) == held && !_game.HasPendingAction && _game.IsMarketOpen,
            "Review, changed selection or focus loss sent a command.");
        _checks.Add("A changed quantity and controlled focus loss each discard the review; nothing is confirmed, retargeted or requested again.");

        _stage = "confirm-buy-" + _mode;
        await _driver.Activate(Node<Button>("PauseToRead"));
        await _driver.Until(() => !_game.IsLocallyPaused, "Time did not resume for confirmation.");
        await _driver.Activate(Node<Button>("ReviewOffer"));
        Require(_game.MarketDraft.Review?.Quote.QuoteId == buy.QuoteId, "The open offer could not be reviewed again.");
        await CrossEnabledConfirm();
        await _driver.Activate(Node<Button>("ConfirmTrade"));
        await _driver.Until(() => !_game.HasPendingAction && Status(buy.QuoteId) == PaidQuoteStatus.Filled, "The reviewed purchase did not settle.");
        await RepeatOnFilledOffer();
        await _driver.Activate(Node<Button>("PauseToRead"));
        await Settle("Purchase outcome");
        Require(OwnBerries() == berries + BuyUnits && View.Progression!.CurrencyBalance == purse - buy.TotalFor(BuyUnits),
            "The purchase did not move exactly the reviewed goods and money.");
        await Capture("buy-outcome");
        _checks.Add("One exact confirmation bought four of Wren's admitted berries with earned reward money; a repeat on the filled offer sent nothing.");

        _stage = "cancel-" + _mode;
        // A request asked while reading paused waits instead of deadlocking; the player may cancel it.
        string paused = MarketProbeEvidence.PublicHash(View);
        await _driver.Activate(Node<Button>("AskMerchant"));
        await _driver.Until(() => _game.CurrentInquiry?.State == LocalTradeInquiryState.Waiting
            && Node<Label>("RequestStatus").Text.Contains("World time is paused", StringComparison.Ordinal),
            "A paused request was not shown as waiting for running time.");
        await _driver.Delay(0.3);
        Require(_game.CurrentInquiry?.State == LocalTradeInquiryState.Waiting && MarketProbeEvidence.PublicHash(View) == paused,
            "A paused request was answered or advanced the world.");
        await _driver.Activate(Node<Button>("CancelRequest"));
        await _driver.Until(() => _game.CurrentInquiry?.State == LocalTradeInquiryState.Cancelled, "The waiting request was not cancelled.");
        Require(MarketProbeEvidence.PublicHash(View) == paused && OpenOffer() is null, "Cancelling a request changed the world.");
        _checks.Add("Back discards a review; a request asked while paused waits for running time and its cancellation sends nothing.");

        _stage = "decline-" + _mode;
        await _driver.Activate(Node<Button>("PauseToRead"));
        await _driver.Until(() => !_game.IsLocallyPaused, "Time did not resume for the second request.");
        await _driver.Activate(Node<Button>("AskMerchant"));
        await _driver.Until(() => _game.CurrentInquiry?.State == LocalTradeInquiryState.Declined, "Wren did not decline a request her stock cannot fill.");
        // The panel presents the settled answer on its next frame.
        await _driver.Until(() => Node<Label>("RequestStatus").Text.Contains("offered no terms", StringComparison.Ordinal),
            "The decline was not explained.");
        Require(OpenOffer() is null, "A decline produced an offer.");
        _checks.Add("Asking again for four after her stock fell to two receives Wren's genuine decline; no stock count is shown.");

        _stage = "show-" + _mode;
        await _driver.Activate(Node<Button>("SellMode"));
        Require(_game.MarketDraft.Direction == LocalTradeDirection.Sell && _game.MarketDraft.LotId is null, "Sell mode preselected a lot.");
        int lot = Array.FindIndex(Stall.OwnLots.ToArray(), value => value.Quantity == BuyUnits);
        Require(lot >= 0, "The bought lot is not listed as the player's own usable lot.");
        // Time runs here, so the step advances; the facts a command would change are compared instead of the whole view.
        int quotes = View.PaidQuotes.Count;
        string lots = string.Join(",", Stall.OwnLots.Select(value => $"{value.LotId}:{value.Revision}:{value.Quantity}"));
        await _driver.CancelChoice(Node<OptionButton>("MarketLot"), lot + 1, Node<Label>("MarketTitle"));
        Require(_game.IsMarketOpen && _game.MarketDraft.LotId is null && !_game.HasPendingAction && Stall.OwnShowing is null
            && View.PaidQuotes.Count == quotes && lots == string.Join(",", Stall.OwnLots.Select(value => $"{value.LotId}:{value.Revision}:{value.Quantity}")),
            "Cancelling the lot list chose a lot, closed the stall or sent a command.");
        await _driver.Choose(Node<OptionButton>("MarketLot"), lot + 1);
        _soldLot = Stall.OwnLots[lot].LotId;
        Require(_game.MarketDraft.LotId == _soldLot && _game.MarketDraft.Quantity == SellUnits, "Lot selection substituted a lot or quantity.");
        _checks.Add("The lot list opens, reaches a row and cancels through the route without choosing; the same route then chooses that exact lot.");
        await _driver.Activate(Node<Button>("ShowLot"));
        await _driver.Until(() => !_game.HasPendingAction && Stall.OwnShowing is { IsLive: true, MatchesOwnLot: true } shown
            && shown.LotId == _soldLot && shown.Units == SellUnits, "The exact lot was not shown.");
        Require(OwnBerries() == BuyUnits, "Showing moved goods.");

        _stage = "sell-" + _mode;
        await _driver.Activate(Node<Button>("AskMerchant"));
        await _driver.Until(() => OpenOffer() is { Side: SpotQuoteSide.IssuerBuys } offer && offer.LotId == _soldLot,
            "Wren did not offer to buy the shown lot.");
        PaidQuoteView sell = OpenOffer()!;
        _sellQuote = sell.QuoteId;
        await _driver.Activate(Node<Button>("PauseToRead"));
        await Settle("Sale reading pause");
        await _driver.Activate(Node<Button>("ReviewOffer"));
        Require(_game.MarketDraft.Review?.Quote.QuoteId == sell.QuoteId, "Sale review did not capture the exact offer.");
        Require(Node<Label>("ConfirmText").Text.Contains("1 berry ", StringComparison.Ordinal), "The single sold unit is not named in the singular.");
        await Capture("sell-review");
        await _driver.Activate(Node<Button>("PauseToRead"));
        await _driver.Until(() => !_game.IsLocallyPaused, "Time did not resume for the sale.");
        ulong beforeSale = View.Progression!.CurrencyBalance;
        await _driver.Activate(Node<Button>("ConfirmTrade"));
        await _driver.Until(() => !_game.HasPendingAction && Status(sell.QuoteId) == PaidQuoteStatus.Filled, "The reviewed sale did not settle.");
        await _driver.Activate(Node<Button>("PauseToRead"));
        await Settle("Sale outcome");
        Require(OwnBerries() == BuyUnits - SellUnits && View.Progression!.CurrencyBalance == beforeSale + sell.UnitPrice * SellUnits,
            "The sale did not move exactly the shown unit and Wren's payment.");
        await Capture("sell-outcome");
        _checks.Add("Wren's own proceeds paid for one shown berry through a separately reviewed and confirmed sale.");

        _stage = "history-" + _mode;
        await InspectRecord(buy.QuoteId, "bought 4");
        await Capture("history");
        await CloseStall();
        _checks.Add("The saved buy outcome is chosen from the outcome list and read through the explicit background record inspection, then the stall closes without a command.");
    }

    private async Task Reopen()
    {
        using var prior = JsonDocument.Parse(File.ReadAllText(Output("active", "json")));
        Require(prior.RootElement.GetProperty("Status").GetString() == "passed"
            && prior.RootElement.GetProperty("FinalHash").GetString() == _initial,
            "Separate process did not restore the exact final public state.");
        _buyQuote = EntityId.Parse(prior.RootElement.GetProperty("BuyQuoteId").GetString());
        _sellQuote = EntityId.Parse(prior.RootElement.GetProperty("SellQuoteId").GetString());
        Require(Status(_buyQuote.Value) == PaidQuoteStatus.Filled && Status(_sellQuote.Value) == PaidQuoteStatus.Filled
            && _game.CurrentInquiry is null && _game.MarketDraft.Review is null,
            "Reopened outcomes differ, or a transient request or review was restored.");
        _stage = "reopen";
        await MarketProbeEvidence.OpenStall(_driver, _game, Wheel, FarmSemanticAction.TradeOffers);
        await InspectRecord(_sellQuote.Value, "sold 1 berry");
        await Capture("reopen");
        await CloseStall();
        await _driver.Delay(0.4);
        Require(MarketProbeEvidence.PublicHash(View) == _initial && _game.IsLocallyPaused, "Paused reopening and inspection changed the public world.");
        _checks.Add("A separate paused process shows both saved outcomes and reads a saved record without advancing the world.");
    }

    /// <summary>
    /// With time running and an enabled Confirm, focus (or the pointer) passes over Confirm and away again without
    /// activating it: moving across a commitment sends nothing.
    /// </summary>
    private async Task CrossEnabledConfirm()
    {
        Button confirm = Node<Button>("ConfirmTrade");
        Button back = Node<Button>("BackFromReview");
        await _driver.Until(() => !confirm.Disabled, "Confirm did not become available with time running.");
        if (_route == "pointer")
        {
            await _driver.Hover(confirm);
            await _driver.Hover(back);
        }
        else
        {
            await _driver.Reach(confirm);
            await _driver.Reach(back);
        }
        await _driver.Delay(0.2);
        Require(!_game.HasPendingAction && Status(_buyQuote!.Value) == PaidQuoteStatus.Open && _game.MarketDraft.Review is not null,
            "Moving across the enabled Confirm submitted or discarded the purchase.");
        _checks.Add("Moving focus across an enabled Confirm and away again submits nothing and keeps the review.");
    }

    /// <summary>The same route activation on the filled offer's disabled Confirm must send nothing.</summary>
    private async Task RepeatOnFilledOffer()
    {
        Button confirm = Node<Button>("ConfirmTrade");
        await _driver.Until(() => confirm.Disabled, "A filled offer still exposes an enabled confirmation.");
        string held = MarketProbeEvidence.PublicHash(View);
        await _driver.Attempt(confirm);
        await _driver.Delay(0.2);
        Require(!_game.HasPendingAction && Status(_buyQuote!.Value) == PaidQuoteStatus.Filled
            && View.PaidQuotes.Count(quote => quote.Status == PaidQuoteStatus.Filled) == 1,
            "Repeated confirmation submitted another command.");
        _observations.Add(new { Label = "repeat-attempt", Before = held, After = MarketProbeEvidence.PublicHash(View) });
    }

    private async Task CloseStall()
    {
        if (_route == "pointer") await _driver.Click(Node<Button>("CloseMarket"));
        else if (_route == "controller") _driver.Joy(JoyButton.B);
        else _driver.Tap(Key.Escape);
        await _driver.Until(() => !_game.IsMarketOpen, "The stall panel did not close.");
    }

    private async Task InspectRecord(EntityId quoteId, string expected)
    {
        PaidQuoteView[] rows = View.PaidQuotes.Where(quote => MarketActionOffers.BelongsTo(Stall, quote))
            .OrderByDescending(quote => quote.IssuedAtStep).ThenBy(quote => quote.QuoteId).ToArray();
        int index = Array.FindIndex(rows, quote => quote.QuoteId == quoteId);
        Require(index >= 0, "The outcome is absent from the live list.");
        await _driver.Choose(Node<OptionButton>("MarketHistory"), index + 1);
        await _driver.Activate(Node<Button>("CheckRecord"));
        await _driver.Until(() => Node<Label>("HistoryDetails").Text.Contains("Saved record:", StringComparison.Ordinal)
            && Node<Label>("HistoryDetails").Text.Contains(expected, StringComparison.Ordinal), "Saved-record inspection did not complete.");
    }

    private PaidQuoteView? OpenOffer() => MarketActionOffers.OpenOffer(View, Stall);
    private PaidQuoteStatus? Status(EntityId id) => View.PaidQuotes.FirstOrDefault(quote => quote.QuoteId == id)?.Status;
    private ulong OwnBerries() => Stall.OwnLots.Aggregate(0UL, (sum, lot) => sum + lot.Quantity);

    private async Task Settle(string label)
    {
        await _driver.Until(() => _game.IsLocallyPaused && !_game.HasPendingAction, label + " did not settle paused.");
        await _driver.Delay(0.25);
        string held = MarketProbeEvidence.PublicHash(View);
        await _driver.Delay(0.25);
        Require(MarketProbeEvidence.PublicHash(View) == held, label + ": paused world changed.");
    }

    private void Observe(string label) => _observations.Add(MarketProbeEvidence.Observe(label, _game, View, GetViewport()));

    private async Task Capture(string stage)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        if (_game.IsMarketOpen)
        {
            Rect2 viewport = GetViewport().GetVisibleRect();
            Require(viewport.Encloses(Node<Button>("CloseMarket").GetGlobalRect())
                && viewport.Encloses(Node<Label>("MarketTime").GetGlobalRect()), "Stall header is clipped outside the viewport.");
        }
        using Image image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(Output(stage, "png")) == Error.Ok, "Market image could not be saved.");
    }

    private string Output(string stage, string extension) => Path.ChangeExtension(_reportPath, null) + "." + stage + "." + extension;

    private void Report(string status, string? error) => File.WriteAllText(Output(_reopening ? "reopen" : "active",
        status == "passed" ? "json" : "failed.json"), JsonSerializer.Serialize(new
        {
            Kind = "native-trading-v1", Mode = _reopening ? "reopen" : _mode, Route = _route, Status = status, Error = error, Stage = _stage,
            Genesis = _genesisPath, InitialHash = _initial, FinalHash = _game.CurrentView is null ? null : MarketProbeEvidence.PublicHash(View),
            BuyQuoteId = _buyQuote?.ToString(), SellQuoteId = _sellQuote?.ToString(), SoldLotId = _soldLot?.ToString(),
            BuyUnits, SellUnits, Checks = _checks, Inputs = _driver.Inputs, Observations = _observations,
            Display = DisplayServer.GetName(), FocusRequests = 0,
            Unfocusable = GetWindow().Unfocusable, NativeFocused = GetWindow().HasFocus(), ObservedNativeFocus = _observedNativeFocus,
            StartupNoFocus = ProjectSettings.GetSetting("display/window/size/no_focus").AsBool(),
            _game.IsLocallyPaused, _game.IsWindowFocused, DurationMilliseconds = _driver.ElapsedMilliseconds,
            Limit = "Every wheel, stall button and dropdown row uses the named injected route: keyboard arrows/Enter/Escape, "
                + "controller D-pad/A/B or pointer motion/wheel/clicks. Focus starts where the game puts it and moves only by those "
                + "events. Focus loss is injected through the fixture hook. Physical devices, OS focus, art, multiplayer Wait and "
                + "production gates remain open."
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");

    private static void Require(bool condition, string message) => MarketProbeDriver.Require(condition, message);
}
