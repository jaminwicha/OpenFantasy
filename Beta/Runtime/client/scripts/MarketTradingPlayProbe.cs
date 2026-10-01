using System.Diagnostics;
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
/// purchase, receives a genuine decline, shows and sells one lot, inspects a saved record and closes. A separate
/// process reopens paused and inspects the saved outcomes without advancing the world.
/// </summary>
internal sealed partial class MarketTradingPlayProbe(FarmGame game, string reportPath, string genesisPath, string mode) : Node
{
    private const ulong BuyUnits = 4;
    private const ulong SellUnits = 1;
    private const string ReopenPrefix = "reopen-";
    /// <summary>A paused reopening names the device route it uses, such as "reopen-controller".</summary>
    private readonly bool _reopening = mode.StartsWith(ReopenPrefix, StringComparison.Ordinal);
    private readonly string _route = mode.StartsWith(ReopenPrefix, StringComparison.Ordinal) ? mode[ReopenPrefix.Length..] : mode;
    private readonly Stopwatch _elapsed = Stopwatch.StartNew();
    private readonly List<string> _checks = [];
    private readonly List<object> _inputs = [];
    private readonly List<object> _observations = [];
    private string _stage = "opening";
    private string? _initial;
    private EntityId? _buyQuote;
    private EntityId? _sellQuote;
    private EntityId? _soldLot;
    private bool _observedNativeFocus;
    private bool _mouseEntered;
    private LocalWorldView View => game.CurrentView ?? throw new InvalidOperationException("No public world.");
    private FarmQuickWheel Wheel => game.GetNode<FarmQuickWheel>("FarmQuickWheel");
    private FarmMarketPanel Panel => game.GetNode<FarmMarketPanel>("FarmMarketPanel");
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
            await Until(() => game.CurrentView?.Markets?.Stalls.Count == 1 && game.CurrentOffers.ContainsKey(FarmSemanticAction.TradeBuy),
                "Market world did not open.", 30);
            game.InjectFixtureFocus(true);
            Require(game.IsLocallyPaused && !View.IsInEncounter && Stall.IsAvailableInPerson,
                "Market fixture must open paused in exploration beside Wren's stall.");
            _initial = PublicHash();
            Observe("opened");
            if (_reopening) await Reopen();
            else await Exercise();
            Require(game.IsLocallyPaused && !game.HasPendingAction && !game.IsMarketOpen, "The journey did not end paused and closed.");
            Observe("final");
            await Capture(_reopening ? "reopen-final" : "final");
            Deadline();
            Report("passed", null);
            game.RequestClose();
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

        _stage = "open-" + mode;
        await OpenStall(FarmSemanticAction.TradeBuy);
        Require(PublicHash() == _initial && game.MarketDraft.Direction == LocalTradeDirection.Buy && game.MarketDraft.Review is null,
            "Opening the stall from a paused wheel changed the world or preselected terms.");
        _checks.Add("A paused wheel choice opens the stall for inspection without a command or preselected terms.");

        _stage = "ask-buy-" + mode;
        await Activate(Node<Button>("PauseToRead"));
        await Until(() => !game.IsLocallyPaused, "The stall's explicit pause control did not resume time.");
        for (ulong unit = 1; unit < BuyUnits; unit++) await Activate(Node<Button>("MarketMore"));
        Require(game.MarketDraft.Quantity == BuyUnits, "Quantity selection failed.");
        await Activate(Node<Button>("AskMerchant"));
        await Until(() => OpenOffer() is { Side: SpotQuoteSide.IssuerSells } offer && offer.QuantityAllowance == BuyUnits,
            "Wren did not answer the request while the open panel kept time running.");
        PaidQuoteView buy = OpenOffer()!;
        _buyQuote = buy.QuoteId;
        await Activate(Node<Button>("PauseToRead"));
        await Settle("Reading pause");
        ulong readStep = View.Markets!.AdvancingStep;
        await Delay(0.4);
        Require(View.Markets!.AdvancingStep == readStep && OpenOffer()?.QuoteId == buy.QuoteId,
            "A reading pause aged or replaced the offer.");
        Require(Node<Label>("OfferDetails").Text.Contains($"{buy.UnitPrice} ", StringComparison.Ordinal)
            && Node<Label>("OfferDetails").Text.Contains("more world steps", StringComparison.Ordinal),
            "The inspector does not show the exact price and remaining window.");
        await Capture("buy-offer");
        _checks.Add("The panel left time running so Wren answered; an explicit reading pause then held the exact offer and its window.");

        _stage = "review-buy-" + mode;
        string held = PublicHash();
        await Activate(Node<Button>("ReviewOffer"));
        await Until(() => game.MarketDraft.Review?.Quote.QuoteId == buy.QuoteId, "Review did not capture the exact offer.");
        Require(Node<Button>("ConfirmTrade").Disabled && Node<Label>("ConfirmText").Text.Contains("Resume world time", StringComparison.Ordinal),
            "Confirmation was available while reading paused.");
        await Capture("buy-review");
        await Activate(Node<Button>("BackFromReview"));
        Require(game.MarketDraft.Review is null && !Node<Control>("ConfirmBox").Visible, "Back kept the reviewed offer.");
        await Activate(Node<Button>("ReviewOffer"));
        Require(game.MarketDraft.Review?.Quote.QuoteId == buy.QuoteId, "Explicit re-review after Back failed.");
        await Activate(Node<Button>("MarketLess"));
        Require(game.MarketDraft.Review is null && !Node<Control>("ConfirmBox").Visible, "A changed quantity kept the reviewed offer.");
        await Activate(Node<Button>("MarketMore"));
        await Activate(Node<Button>("ReviewOffer"));
        Require(game.MarketDraft.Review?.Quote.QuoteId == buy.QuoteId, "Re-review after an explicit change failed.");
        game.InjectFixtureFocus(false);
        await Until(() => !game.IsWindowFocused && game.MarketDraft.Review is null, "Focus loss kept the reviewed offer armed.");
        game.InjectFixtureFocus(true);
        await Delay(0.25);
        Require(PublicHash() == held && !game.HasPendingAction && game.IsMarketOpen,
            "Review, changed selection or focus loss sent a command.");
        _checks.Add("A changed quantity and controlled focus loss each discard the review; nothing is confirmed, retargeted or requested again.");

        _stage = "confirm-buy-" + mode;
        await Activate(Node<Button>("PauseToRead"));
        await Until(() => !game.IsLocallyPaused, "Time did not resume for confirmation.");
        await Activate(Node<Button>("ReviewOffer"));
        Require(game.MarketDraft.Review?.Quote.QuoteId == buy.QuoteId, "The open offer could not be reviewed again.");
        await Activate(Node<Button>("ConfirmTrade"));
        await Until(() => !game.HasPendingAction && Status(buy.QuoteId) == PaidQuoteStatus.Filled, "The reviewed purchase did not settle.");
        await Attempt(Node<Button>("ConfirmTrade"));
        await Activate(Node<Button>("PauseToRead"));
        await Settle("Purchase outcome");
        Require(OwnBerries() == berries + BuyUnits && View.Progression!.CurrencyBalance == purse - buy.TotalFor(BuyUnits),
            "The purchase did not move exactly the reviewed goods and money.");
        await Capture("buy-outcome");
        _checks.Add("One exact confirmation bought four of Wren's admitted berries with earned reward money; a repeat on the filled offer sent nothing.");

        _stage = "cancel-" + mode;
        // A request asked while reading paused waits instead of deadlocking; the player may cancel it.
        string paused = PublicHash();
        await Activate(Node<Button>("AskMerchant"));
        await Until(() => game.CurrentInquiry?.State == LocalTradeInquiryState.Waiting
            && Node<Label>("RequestStatus").Text.Contains("World time is paused", StringComparison.Ordinal),
            "A paused request was not shown as waiting for running time.");
        await Delay(0.3);
        Require(game.CurrentInquiry?.State == LocalTradeInquiryState.Waiting && PublicHash() == paused,
            "A paused request was answered or advanced the world.");
        await Activate(Node<Button>("CancelRequest"));
        await Until(() => game.CurrentInquiry?.State == LocalTradeInquiryState.Cancelled, "The waiting request was not cancelled.");
        Require(PublicHash() == paused && OpenOffer() is null, "Cancelling a request changed the world.");
        _checks.Add("Back discards a review; a request asked while paused waits for running time and its cancellation sends nothing.");

        _stage = "decline-" + mode;
        await Activate(Node<Button>("PauseToRead"));
        await Until(() => !game.IsLocallyPaused, "Time did not resume for the second request.");
        await Activate(Node<Button>("AskMerchant"));
        await Until(() => game.CurrentInquiry?.State == LocalTradeInquiryState.Declined, "Wren did not decline a request her stock cannot fill.");
        // The panel presents the settled answer on its next frame.
        await Until(() => Node<Label>("RequestStatus").Text.Contains("offered no terms", StringComparison.Ordinal),
            "The decline was not explained.");
        Require(OpenOffer() is null, "A decline produced an offer.");
        _checks.Add("Asking again for four after her stock fell to two receives Wren's genuine decline; no stock count is shown.");

        _stage = "show-" + mode;
        await Activate(Node<Button>("SellMode"));
        Require(game.MarketDraft.Direction == LocalTradeDirection.Sell && game.MarketDraft.LotId is null, "Sell mode preselected a lot.");
        int lot = Array.FindIndex(Stall.OwnLots.ToArray(), value => value.Quantity == BuyUnits);
        Require(lot >= 0, "The bought lot is not listed as the player's own usable lot.");
        await Choose(Node<OptionButton>("MarketLot"), lot + 1);
        _soldLot = Stall.OwnLots[lot].LotId;
        Require(game.MarketDraft.LotId == _soldLot && game.MarketDraft.Quantity == SellUnits, "Lot selection substituted a lot or quantity.");
        await Activate(Node<Button>("ShowLot"));
        await Until(() => !game.HasPendingAction && Stall.OwnShowing is { IsLive: true, MatchesOwnLot: true } shown
            && shown.LotId == _soldLot && shown.Units == SellUnits, "The exact lot was not shown.");
        Require(OwnBerries() == BuyUnits, "Showing moved goods.");

        _stage = "sell-" + mode;
        await Activate(Node<Button>("AskMerchant"));
        await Until(() => OpenOffer() is { Side: SpotQuoteSide.IssuerBuys } offer && offer.LotId == _soldLot,
            "Wren did not offer to buy the shown lot.");
        PaidQuoteView sell = OpenOffer()!;
        _sellQuote = sell.QuoteId;
        await Activate(Node<Button>("PauseToRead"));
        await Settle("Sale reading pause");
        await Activate(Node<Button>("ReviewOffer"));
        Require(game.MarketDraft.Review?.Quote.QuoteId == sell.QuoteId, "Sale review did not capture the exact offer.");
        await Capture("sell-review");
        await Activate(Node<Button>("PauseToRead"));
        await Until(() => !game.IsLocallyPaused, "Time did not resume for the sale.");
        ulong beforeSale = View.Progression!.CurrencyBalance;
        await Activate(Node<Button>("ConfirmTrade"));
        await Until(() => !game.HasPendingAction && Status(sell.QuoteId) == PaidQuoteStatus.Filled, "The reviewed sale did not settle.");
        await Activate(Node<Button>("PauseToRead"));
        await Settle("Sale outcome");
        Require(OwnBerries() == BuyUnits - SellUnits && View.Progression!.CurrencyBalance == beforeSale + sell.UnitPrice * SellUnits,
            "The sale did not move exactly the shown unit and Wren's payment.");
        await Capture("sell-outcome");
        _checks.Add("Wren's own proceeds paid for one shown berry through a separately reviewed and confirmed sale.");

        _stage = "history-" + mode;
        await InspectRecord(buy.QuoteId, "bought 4");
        await Capture("history");
        await CloseStall();
        _checks.Add("The saved buy outcome is read through the explicit background record inspection, then the stall closes without a command.");
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
            && game.CurrentInquiry is null && game.MarketDraft.Review is null,
            "Reopened outcomes differ, or a transient request or review was restored.");
        _stage = "reopen";
        await OpenStall(FarmSemanticAction.TradeOffers);
        await InspectRecord(_sellQuote.Value, "sold 1");
        await Capture("reopen");
        await CloseStall();
        await Delay(0.4);
        Require(PublicHash() == _initial && game.IsLocallyPaused, "Paused reopening and inspection changed the public world.");
        _checks.Add("A separate paused process shows both saved outcomes and reads a saved record without advancing the world.");
    }

    private async Task OpenStall(FarmSemanticAction choice)
    {
        if (_route == "pointer") await Click(game.GetNode<FarmHud>("FarmHud").GetNode<Button>("%OpenWheel"));
        else if (_route == "controller") Joy(JoyButton.RightStick);
        else Tap(Key.V);
        await Until(() => game.IsWheelOpen, "Wheel did not open.");
        Require(Wheel.Model!.Selected is null, "Opening the wheel preselected a choice.");
        for (int page = 0; Wheel.Model.PageName != "Market"; page++)
        {
            Require(page < 4, "The wheel has no Market page.");
            if (_route == "pointer") await Click(Wheel.GetNode<Button>("%NextWheelPage"));
            else if (_route == "controller") Joy(JoyButton.RightShoulder);
            else Tap(Key.Q);
            await Frame();
        }
        int index = Array.FindIndex(Wheel.Model.Slots.ToArray(), slot => slot?.Action == choice);
        Require(index >= 0, "Market sector absent.");
        if (_route == "pointer") await Point(Wheel.GetNode<Control>("%Disc"), index);
        else if (_route == "controller") Joy(new[] { JoyButton.DpadUp, JoyButton.DpadRight, JoyButton.DpadDown, JoyButton.DpadLeft }[index]);
        else Tap(new[] { Key.Up, Key.Right, Key.Down, Key.Left }[index]);
        await Until(() => Wheel.Model?.Selected?.Action == choice, "Market sector selection failed.");
        Require(Wheel.Model!.Selected!.Offer.OpensPanel && Wheel.Model.Selected.Offer.CanExecute, "The stall choice is unavailable.");
        await Frame();
        if (_route == "pointer") await Click(Wheel.GetNode<Button>("%ConfirmChoice"));
        else if (_route == "controller") Joy(JoyButton.A);
        else Tap(Key.Enter);
        await Until(() => game.IsMarketOpen && !game.IsWheelOpen, "The stall panel did not open from the wheel.");
        await Frame();
    }

    private async Task CloseStall()
    {
        if (_route == "pointer") await Click(Node<Button>("CloseMarket"));
        else if (_route == "controller") Joy(JoyButton.B);
        else Tap(Key.Escape);
        await Until(() => !game.IsMarketOpen, "The stall panel did not close.");
    }

    private async Task InspectRecord(EntityId quoteId, string expected)
    {
        PaidQuoteView[] rows = View.PaidQuotes.Where(quote => quote.IssuerActorId == Stall.MerchantActorId && !quote.ViewerIsIssuer)
            .OrderByDescending(quote => quote.IssuedAtStep).ThenBy(quote => quote.QuoteId).ToArray();
        int index = Array.FindIndex(rows, quote => quote.QuoteId == quoteId);
        Require(index >= 0, "The outcome is absent from the live list.");
        await Choose(Node<OptionButton>("MarketHistory"), index + 1);
        await Activate(Node<Button>("CheckRecord"));
        await Until(() => Node<Label>("HistoryDetails").Text.Contains("Saved record:", StringComparison.Ordinal)
            && Node<Label>("HistoryDetails").Text.Contains(expected, StringComparison.Ordinal), "Saved-record inspection did not complete.");
    }

    private PaidQuoteView? OpenOffer() => MarketActionOffers.OpenOffer(View, Stall);
    private PaidQuoteStatus? Status(EntityId id) => View.PaidQuotes.FirstOrDefault(quote => quote.QuoteId == id)?.Status;
    private ulong OwnBerries() => Stall.OwnLots.Aggregate(0UL, (sum, lot) => sum + lot.Quantity);

    private async Task Choose(OptionButton control, int index)
    {
        Require(control.IsVisibleInTree() && index < control.ItemCount, "Dropdown choice unavailable: " + control.Name);
        control.GrabFocus();
        Tap(Key.Enter);
        await Until(() => control.GetPopup().Visible, "Dropdown did not open for explicit input.");
        for (int step = 0; control.GetPopup().GetFocusedItem() != index && step < control.ItemCount; step++)
        {
            Tap(Key.Down);
            await Frame();
        }
        Require(control.GetPopup().GetFocusedItem() == index, "Dropdown focus did not reach the selected row.");
        Tap(Key.Enter);
        await Until(() => !control.GetPopup().Visible && control.Selected == index, "Dropdown input did not select the exact row.");
        await Frame();
    }

    /// <summary>The requested device route activates one focused panel control through ordinary GUI input.</summary>
    private async Task Activate(Button button)
    {
        await Until(() => button.IsVisibleInTree() && !button.Disabled, "Market control is unavailable: " + button.Name);
        button.GrabFocus();
        await Frame();
        void Trace(InputEvent input) => _inputs.Add(new { Stage = _stage, Control = button.Name.ToString(),
            Event = input.AsText(), Pressed = input.IsPressed(), Accept = input.IsAction("ui_accept"), Focused = button.HasFocus() });
        button.GuiInput += Trace;
        try
        {
            if (_route == "pointer") await Click(button);
            else if (_route == "controller") Joy(JoyButton.A);
            else Tap(Key.Enter);
            await Frame();
            await Frame();
        }
        finally { button.GuiInput -= Trace; }
    }

    /// <summary>The same device input on a disabled commitment must produce nothing.</summary>
    private async Task Attempt(Button button)
    {
        await Frame();
        Require(button.Disabled, "A filled offer still exposes an enabled confirmation.");
        string held = PublicHash();
        button.GrabFocus();
        await Frame();
        if (_route == "pointer") await Click(button, requireEnabled: false);
        else if (_route == "controller") Joy(JoyButton.A);
        else Tap(Key.Enter);
        await Delay(0.2);
        Require(!game.HasPendingAction && Status(_buyQuote!.Value) == PaidQuoteStatus.Filled
            && View.PaidQuotes.Count(quote => quote.Status == PaidQuoteStatus.Filled) == 1,
            "Repeated confirmation submitted another command.");
        _observations.Add(new { Label = "repeat-attempt", Before = held, After = PublicHash() });
    }

    private async Task Settle(string label)
    {
        await Until(() => game.IsLocallyPaused && !game.HasPendingAction, label + " did not settle paused.");
        await Delay(0.25);
        string held = PublicHash();
        await Delay(0.25);
        Require(PublicHash() == held, label + ": paused world changed.");
    }

    private string PublicHash() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(View))));
    private void Observe(string label) => _observations.Add(new { Label = label, View, game.IsLocallyPaused,
        game.HasPendingAction, game.IsMarketOpen, game.IsWheelOpen });
    private void Tap(Key key)
    {
        _inputs.Add(new { Stage = _stage, Key = key.ToString() });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
        Input.FlushBufferedEvents();
    }
    private void Joy(JoyButton button)
    {
        _inputs.Add(new { Stage = _stage, Controller = button.ToString() });
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = true });
        Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = button, Pressed = false });
        Input.FlushBufferedEvents();
    }
    private void EnterViewport()
    {
        if (_mouseEntered) return;
        GetViewport().NotifyMouseEntered();
        _mouseEntered = true;
    }
    private async Task Point(Control disc, int sector)
    {
        EnterViewport();
        Vector2 direction = new[] { Vector2.Up, Vector2.Right, Vector2.Down, Vector2.Left }[sector];
        Vector2 at = disc.GetGlobalRect().GetCenter() + direction * 116;
        _inputs.Add(new { Stage = _stage, PointerSector = sector, X = at.X, Y = at.Y });
        GetViewport().PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at }, true);
        await Frame();
    }
    private async Task Click(Button button, bool requireEnabled = true)
    {
        Require(button.IsVisibleInTree() && (!requireEnabled || !button.Disabled), "Pointer control is unavailable: " + button.Name);
        EnterViewport();
        Vector2 at = button.GetGlobalRect().GetCenter();
        _inputs.Add(new { Stage = _stage, PointerControl = button.Name.ToString(), X = at.X, Y = at.Y });
        GetViewport().PushInput(new InputEventMouseMotion { Position = at, GlobalPosition = at }, true);
        await Frame();
        GetViewport().PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, Pressed = true }, true);
        await Frame();
        GetViewport().PushInput(new InputEventMouseButton { Position = at, GlobalPosition = at, ButtonIndex = MouseButton.Left, Pressed = false }, true);
    }
    private async Task Until(Func<bool> predicate, string failure, double seconds = 10)
    {
        var watch = Stopwatch.StartNew();
        while (true)
        {
            bool satisfied = predicate();
            Deadline();
            Require(watch.Elapsed.TotalSeconds < seconds, failure);
            if (satisfied) return;
            await Frame();
        }
    }
    private async Task Delay(double seconds)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed.TotalSeconds < seconds) await Frame();
    }
    private async Task Frame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Deadline();
    }
    private void Deadline() => Require(!game.HasFailed && _elapsed.Elapsed.TotalSeconds < 150,
        "Market journey failed or exceeded its 150-second bound at " + _stage);
    private async Task Capture(string stage)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        if (game.IsMarketOpen)
        {
            Rect2 viewport = GetViewport().GetVisibleRect();
            Require(viewport.Encloses(Node<Button>("CloseMarket").GetGlobalRect())
                && viewport.Encloses(Node<Label>("MarketTime").GetGlobalRect()), "Stall header is clipped outside the viewport.");
        }
        using Image image = GetViewport().GetTexture().GetImage();
        Require(image.SavePng(Output(stage, "png")) == Error.Ok, "Market image could not be saved.");
    }
    private string Output(string stage, string extension) => Path.ChangeExtension(reportPath, null) + "." + stage + "." + extension;
    private void Report(string status, string? error) => File.WriteAllText(Output(_reopening ? "reopen" : "active",
        status == "passed" ? "json" : "failed.json"), JsonSerializer.Serialize(new
        {
            Kind = "native-trading-v1", Mode = _reopening ? "reopen" : mode, Route = _route, Status = status, Error = error, Stage = _stage,
            Genesis = genesisPath, InitialHash = _initial, FinalHash = game.CurrentView is null ? null : PublicHash(),
            BuyQuoteId = _buyQuote?.ToString(), SellQuoteId = _sellQuote?.ToString(), SoldLotId = _soldLot?.ToString(),
            BuyUnits, SellUnits, Checks = _checks, Inputs = _inputs, Observations = _observations,
            Display = DisplayServer.GetName(), FocusRequests = 0,
            Unfocusable = GetWindow().Unfocusable, NativeFocused = GetWindow().HasFocus(), ObservedNativeFocus = _observedNativeFocus,
            StartupNoFocus = ProjectSettings.GetSetting("display/window/size/no_focus").AsBool(),
            game.IsLocallyPaused, game.IsWindowFocused, DurationMilliseconds = _elapsed.ElapsedMilliseconds,
            Limit = "Wheel and stall controls use the named injected route after explicit Control focus; dropdowns use injected keyboard "
                + "input in every mode. Focus loss is injected through the fixture hook. Physical devices, OS focus, art, multiplayer "
                + "Wait and production gates remain open."
        }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
