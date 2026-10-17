using Godot;
using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client.Networking;

/// <summary>Ordinary stall input with exact selected stock, distinct review and a single deliberate confirmation.</summary>
internal sealed partial class NativeRosterPlayProbe
{
    private FarmActionOffer? _reviewedTrade;
    private readonly List<object> _tradeCaptures = [];
    private FarmMarketPanel TradePanel => _game.GetNode<FarmMarketPanel>("FarmMarketPanel");
    private FarmQuickWheel TradeWheel => _game.GetNode<FarmQuickWheel>("FarmQuickWheel");
    private LocalMarketView? TradeMarkets => Selected.ControlledView is { } controlled ? controlled.Trade?.Markets : Selected.HomeView.Markets;
    private IReadOnlyList<PaidQuoteView> TradeQuotes => Selected.ControlledView is { } controlled
        ? (IReadOnlyList<PaidQuoteView>?)controlled.Trade?.Quotes ?? Array.Empty<PaidQuoteView>() : Selected.HomeView.PaidQuotes;
    private Task TradeUntil(Func<bool> predicate, string message) => _driver.Until(predicate, message, 5);

    private async Task OpenTrade(bool browseStalls = false)
    {
        await CloseSelectionPanel();
        MarketProbeDriver.Require(!_game.IsInventoryOpen && !_game.IsRecipesOpen && !_game.HasPendingAction,
            "Close other inspections before opening the stall.");
        var selection = Selected.Selection;
        int before = _submissions.Count;
        if (!_game.IsMarketOpen)
        {
            MarketProbeDriver.Require(!_game.IsWheelOpen, "Close the previous wheel before opening trade.");
            var opening = browseStalls ? FarmSemanticAction.TradeOffers : FarmSemanticAction.TradeBuy;
            await TradeUntil(() => _game.CurrentOffers.GetValueOrDefault(opening)?.CanExecute == true
                && _game.PresentedSelection?.Selection == selection, "The selected shopper has no executable presented trade choice.");
            _driver.Tap(Key.V);
            await TradeUntil(() => _game.IsWheelOpen, "The real action wheel did not open.");
            for (int pages = 0; TradeWheel.Model?.PageName != "Market"; pages++)
            {
                MarketProbeDriver.Require(pages < 5, "The actual wheel contains no Market page.");
                _driver.Tap(Key.Q);
                await _driver.Frame();
            }
            _driver.Joy(browseStalls ? JoyButton.DpadDown : JoyButton.DpadUp);
            await TradeUntil(() => TradeWheel.Model?.Selected?.Action == opening
                && TradeWheel.Model.Selected.Offer.CanExecute, "The requested Market sector was not selected and executable.");
            _driver.Joy(JoyButton.A);
        }
        await TradeUntil(() => _game.IsMarketOpen && TradePanel.IsOpen
            && _game.CurrentMarketPresentation?.Selection == selection, "The shared stall did not present the selected shopper.");
        MarketProbeDriver.Require(_submissions.Count == before && !_game.HasPendingAction && Selected.Selection == selection,
            "Opening the shared stall spent or changed selection.");
    }

    private async Task CloseTrade()
    {
        int before = _submissions.Count;
        if (_game.IsMarketOpen) await _driver.Click(TradePanel.GetNode<Button>("%CloseMarket"));
        await TradeUntil(() => !_game.IsMarketOpen, "The actual Close stall button did not close.");
        MarketProbeDriver.Require(_submissions.Count == before && !_game.HasPendingAction,
            "Closing the stall submitted a command.");
    }

    private async Task ChooseTradeDirection(string direction)
    {
        MarketProbeDriver.Require(direction is "Buy" or "Sell", "Trading input requires an explicit Buy or Sell.");
        await OpenTrade();
        var expected = direction == "Buy" ? LocalTradeDirection.Buy : LocalTradeDirection.Sell;
        int before = _submissions.Count;
        await _driver.Click(TradePanel.GetNode<Button>(expected == LocalTradeDirection.Buy ? "%BuyMode" : "%SellMode"));
        await TradeUntil(() => _game.MarketDraft.Direction == expected, "The actual direction control did not retain its choice.");
        MarketProbeDriver.Require(_game.MarketDraft.Review is null && _submissions.Count == before,
            "Changing trade direction submitted or retained reviewed terms.");
    }

    private async Task ChooseTradeLot(EntityId lot)
    {
        MarketProbeDriver.Require(_game.IsMarketOpen && _game.MarketDraft.Direction == LocalTradeDirection.Sell,
            "Open Sell before choosing a lot.");
        await ChooseGiftRow(TradePanel.GetNode<OptionButton>("%MarketLot"), lot.ToString());
        MarketProbeDriver.Require(_game.MarketDraft.LotId == lot && _game.MarketDraft.Review is null,
            "The real lot row did not select the exact received lot.");
    }

    private async Task ChooseTradeStall(EntityId market)
    {
        await OpenTrade();
        int before = _submissions.Count;
        int row = Array.FindIndex(TradeMarkets!.Stalls.ToArray(), stall => stall.MarketId == market) + 1;
        MarketProbeDriver.Require(row > 0, "The requested market is absent from the actual stall rows.");
        await _driver.Choose(TradePanel.GetNode<OptionButton>("%MarketStall"), row);
        await TradeUntil(() => _game.MarketDraft.MarketId == market && !_game.HasMarketRequestInFlight,
            "The actual stall selector did not settle on the requested market.");
        MarketProbeDriver.Require(_submissions.Count == before && _game.MarketDraft.Review is null,
            "Choosing a stall submitted work or retained another stall's reviewed terms.");
    }

    private async Task ShowTradeLot()
    {
        FarmActionOffer? original = null;
        await TradeUntil(() => _game.CurrentOffers.TryGetValue(FarmSemanticAction.ShowTradeLot, out original)
            && original.CanExecute && original.Scope.Selection == Selected.Selection,
            "The exact selected lot cannot be shown at this stall.");
        FarmActionOffer captured = original!;
        var intent = (DiscloseMarketLotIntent)captured.Intent!;
        int before = _submissions.Count;
        await _driver.Click(TradePanel.GetNode<Button>("%ShowLot"));
        await TradeUntil(() => _submissions.Count == before + 1 && _submissions[^1].Receipt.IsCompleted && !_game.HasPendingAction
            && TradeMarkets?.Stalls.Single(stall => stall.MarketId == intent.MarketId).OwnShowing is { } shown
            && shown.LotId == intent.LotId && shown.LotRevision == intent.ExpectedLotRevision && shown.Units == intent.Quantity,
            "One explicit showing did not return its receipt and exact visible lot pins.");
        RequireTradeSubmission(captured, _submissions[^1]);
    }

    private async Task StopTradeShowing()
    {
        MarketProbeDriver.Require(_game.IsMarketOpen && _game.MarketDraft.MarketId is not null,
            "An explicit stall is required to stop showing.");
        var market = _game.MarketDraft.MarketId!.Value;
        var selection = Selected.Selection;
        var beforeLots = TradeMarkets!.Stalls.Single(stall => stall.MarketId == market).OwnLots.ToArray();
        var balance = _game.CurrentMarketPresentation!.CurrencyBalance;
        int before = _submissions.Count;
        await _driver.Click(TradePanel.GetNode<Button>("%StopShowing"));
        await TradeUntil(() => _submissions.Count == before + 1 && _submissions[^1].Receipt.IsCompleted
            && !_game.HasPendingAction && TradeMarkets!.Stalls.Single(stall => stall.MarketId == market).OwnShowing is null,
            "Stop showing did not return its receipt and withdraw the disclosure.");
        var submitted = _submissions[^1];
        var command = WorldActionCodec.Decode(submitted.CanonicalCommandBytes());
        MarketProbeDriver.Require(submitted.Receipt.IsCompletedSuccessfully && !submitted.IsRetry
            && submitted.Receipt.Result.Status == CommandReceiptStatus.Applied
            && command.Intent is WithdrawMarketLotDisclosureIntent withdrawn && withdrawn.MarketId == market
            && command.ActorId == selection.ActorId && command.ControlWitness == selection.ControlWitness
            && Selected.Selection == selection && _game.CurrentMarketPresentation!.CurrencyBalance == balance
            && TradeMarkets!.Stalls.Single(stall => stall.MarketId == market).OwnLots.SequenceEqual(beforeLots),
            "Stopping the showing changed its actor, finite lot, balance or receipt.");
    }

    private async Task AskTrade()
    {
        MarketProbeDriver.Require(_game.IsMarketOpen, "Open a stall before asking.");
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        Task AskUntil(Func<bool> predicate, string message) => _driver.Until(predicate, message, 5 - elapsed.Elapsed.TotalSeconds);
        int before = _submissions.Count;
        var selection = Selected.Selection;
        var direction = _game.MarketDraft.Direction;
        var lot = _game.MarketDraft.LotId;
        var market = _game.MarketDraft.MarketId;
        Button ask = TradePanel.GetNode<Button>("%AskMerchant");
        // Opening the shared stall starts its ordinary current-inquiry read. Await that
        // observation and its presented input state before the one deliberate click.
        // Initial settling and the merchant response share the original five-second budget.
        await AskUntil(() => !_game.HasMarketRequestInFlight && ask.IsVisibleInTree() && !ask.Disabled
            && _game.CurrentMarketPresentation?.Selection == selection && _game.PresentedSelection?.Selection == selection,
            "The selected stall's initial inquiry read and Ask control did not settle within five seconds.");
        MarketProbeDriver.Require(Selected.Selection == selection && _game.MarketDraft.MarketId == market
            && _game.MarketDraft.Direction == direction && _game.MarketDraft.LotId == lot && _submissions.Count == before,
            "Settling the initial inquiry changed the selected request or submitted work.");
        await _driver.Click(ask);
        await AskUntil(() => !_game.HasMarketRequestInFlight && _game.CurrentInquiry is { State: LocalTradeInquiryState.Offered, QuoteId: not null } inquiry
            && TradeQuotes.Any(quote => quote.QuoteId == inquiry.QuoteId && quote.Status == PaidQuoteStatus.Open),
            "The selected shopper did not receive the merchant's original answer within five seconds.");
        var result = _game.CurrentInquiry!;
        var quote = TradeQuotes.Single(value => value.QuoteId == result.QuoteId);
        MarketProbeDriver.Require(Selected.Selection == selection && _submissions.Count == before
            && quote.CounterpartyActorId == selection.ActorId && quote.QuantityAllowance == _game.MarketDraft.Quantity
            && quote.Side == (direction == LocalTradeDirection.Buy ? SpotQuoteSide.IssuerSells : SpotQuoteSide.IssuerBuys)
            && (direction == LocalTradeDirection.Buy || quote.LotId == lot),
            "The inquiry changed scope, lot, units or submitted the shopper's money.");
    }

    private async Task ReviewTrade()
    {
        MarketProbeDriver.Require(_game.IsMarketOpen && _game.MarketDraft.Review is null, "Open an unreviewed quote first.");
        int before = _submissions.Count;
        var selection = Selected.Selection;
        await _driver.Click(TradePanel.GetNode<Button>("%ReviewOffer"));
        await TradeUntil(() => _game.MarketDraft.Review is not null && _game.CurrentOffers.GetValueOrDefault(FarmSemanticAction.ConfirmTrade)
            is { CanExecute: true } offer && offer.Scope.Selection == selection, "Review did not prepare the selected shopper's exact confirmation.");
        _reviewedTrade = _game.CurrentOffers[FarmSemanticAction.ConfirmTrade];
        var review = _game.MarketDraft.Review!;
        var balance = _game.CurrentMarketPresentation!.CurrencyBalance;
        bool drawn = DisplayServer.GetName() == "headless";
        void OnDraw() => drawn = true;
        RenderingServer.FramePostDraw += OnDraw;
        try { await TradeUntil(() => drawn, "The reviewed trade was not drawn before its held read interval."); }
        finally { RenderingServer.FramePostDraw -= OnDraw; }
        await _driver.Delay(0.3);
        MarketProbeDriver.Require(_game.MarketDraft.Review == review && _game.CurrentMarketPresentation!.CurrencyBalance == balance
            && _submissions.Count == before && !_game.HasPendingAction && Selected.Selection == selection
            && !TradePanel.GetNode<Button>("%ConfirmTrade").Disabled
            && _game.CurrentOffers.GetValueOrDefault(FarmSemanticAction.ConfirmTrade) is { CanExecute: true } retained
            && retained.Scope == _reviewedTrade.Scope && SameTradeIntent(retained.Intent, _reviewedTrade.Intent),
            "Reading reviewed terms spent funds or silently replaced the captured quote.");
    }

    private async Task ConfirmTrade(string expected)
    {
        var captured = _reviewedTrade ?? throw new InvalidOperationException("No native trade was reviewed.");
        int before = _submissions.Count;
        var confirm = TradePanel.GetNode<Button>("%ConfirmTrade");
        if (expected == "blocked")
        {
            MarketProbeDriver.Require(!_game.IsMarketOpen || _game.MarketDraft.Review is null || confirm.Disabled,
                "The invalidated trade capture is still executable.");
            if (_game.IsMarketOpen && confirm.IsVisibleInTree()) await _driver.Click(confirm, requireEnabled: false);
            else _driver.Tap(Key.Enter);
            await _driver.Frame();
            await _driver.Frame();
            MarketProbeDriver.Require(_submissions.Count == before && !_game.HasPendingAction,
                "An invalidated trade capture submitted a command.");
            return;
        }
        MarketProbeDriver.Require(expected == "Applied" && !_game.IsLocallyPaused && _game.IsMarketOpen
            && _game.CurrentOffers.GetValueOrDefault(FarmSemanticAction.ConfirmTrade) is { CanExecute: true } current
            && current.Scope == captured.Scope && SameTradeIntent(current.Intent, captured.Intent) && captured.Scope.Selection == Selected.Selection,
            "The original trade review is stale; it will not be replaced or retried.");
        var acceptance = ((AcceptSpotQuoteIntent)captured.Intent!).Acceptance;
        await _driver.Click(confirm);
        await TradeUntil(() => _submissions.Count == before + 1 && _submissions[^1].Receipt.IsCompleted
            && !_game.HasPendingAction && TradeQuotes.All(quote => quote.QuoteId != acceptance.QuoteId || quote.Status != PaidQuoteStatus.Open),
            "One native confirmation did not settle its original quote and receipt.");
        RequireTradeSubmission(captured, _submissions[^1]);
    }

    private static void RequireTradeSubmission(FarmActionOffer captured, FarmSubmission submission)
    {
        var command = WorldActionCodec.Decode(submission.CanonicalCommandBytes());
        MarketProbeDriver.Require(!submission.IsRetry && submission.Receipt.IsCompletedSuccessfully
            && submission.Receipt.Result.Status == CommandReceiptStatus.Applied
            && submission.Receipt.Result.Rejection == WorldActionRejection.None
            && command.ActorId == captured.Scope.ActorId && command.PrincipalId == captured.Scope.PrincipalId
            && command.OriginSessionId == captured.Scope.SessionId && command.WorldId == captured.Scope.WorldId
            && command.BranchId == captured.Scope.BranchId && command.ExpectedActorRevision == captured.Scope.ExpectedActorRevision
            && command.ControlWitness == captured.Scope.Selection?.ControlWitness && SameTradeIntent(command.Intent, captured.Intent)
            && (command.Intent is not AcceptSpotQuoteIntent acceptance || acceptance.Acceptance.AcceptanceId == command.CommandId),
            "Native trade changed reviewed pins, truthful operator or receipt.");
    }

    private static bool SameTradeIntent(WorldActionIntent? left, WorldActionIntent? right)
    {
        if (left is AcceptSpotQuoteIntent a && right is AcceptSpotQuoteIntent b)
            return a.ExpectedIssuerRevision == b.ExpectedIssuerRevision && a.Acceptance.QuoteId == b.Acceptance.QuoteId
                && a.Acceptance.QuoteHash == b.Acceptance.QuoteHash && a.Acceptance.Quantity == b.Acceptance.Quantity
                && a.Acceptance.Payment == b.Acceptance.Payment && a.Acceptance.Fees == b.Acceptance.Fees
                && a.Acceptance.PrincipalId == b.Acceptance.PrincipalId && a.Acceptance.OriginSessionId == b.Acceptance.OriginSessionId;
        if (left is DiscloseMarketLotIntent showing && right is DiscloseMarketLotIntent captured)
            return showing.MarketId == captured.MarketId && showing.LotKind == captured.LotKind && showing.LotId == captured.LotId
                && showing.Definition == captured.Definition && showing.ExpectedLotRevision == captured.ExpectedLotRevision
                && showing.Quantity == captured.Quantity && showing.SellerAccountId == captured.SellerAccountId
                && showing.ExpiresAtStep >= captured.ExpiresAtStep;
        return left == right;
    }

    private async Task CaptureTrade(string label)
    {
        MarketProbeDriver.Require(_game.IsMarketOpen && label.Length is > 0 and <= 64 && _tradeCaptures.Count < 64,
            "Trade capture needs an open stall and bounded label.");
        var root = TradePanel.GetNode<Control>("%MarketRoot");
        foreach (string name in new[] { "MarketTitle", "MarketTime", "OfferSummary", "OfferDetails", "ConfirmText", "MarketNotice" })
        {
            var text = TradePanel.GetNode<Label>("%" + name);
            if (!text.IsVisibleInTree()) continue;
            await _driver.Hover(text);
            await Capture(5, "-trade-" + name);
            MarketProbeDriver.Require(text.GetLineCount() == text.GetVisibleLineCount()
                && GetViewport().GetVisibleRect().Encloses(text.GetGlobalRect()), "A rendered trade explanation is clipped.");
            RequireRecipeScrollVisible(text);
            _tradeCaptures.Add(new { Sequence = _sequence, Label = label, Control = name,
                Image = $"{_sequence:D4}-trade-{name}.png", text.Text, Bounds = Bounds(text.GetGlobalRect()),
                Lines = text.GetLineCount(), VisibleLines = text.GetVisibleLineCount(), Trading = TradeEvidence() });
        }
        await Capture(5, "-trade");
        _tradeCaptures.Add(new { Sequence = _sequence, Label = label, Image = $"{_sequence:D4}-trade.png",
            Bounds = Bounds(root.GetGlobalRect()), Trading = TradeEvidence(),
            Controls = new[] { "BuyMode", "SellMode", "MarketLot", "ShowLot", "AskMerchant", "ReviewOffer", "ConfirmTrade", "CheckRecord" }
                .Select(name => TradePanel.GetNode<Control>("%" + name)).Select(control => new
                { Name = control.Name.ToString(), Visible = control.IsVisibleInTree(), Bounds = Bounds(control.GetGlobalRect()),
                    Disabled = control is BaseButton button && button.Disabled }).ToArray() });
    }

    private object? TradeEvidence()
    {
        if (_game.CurrentSelection is not { } observation) return null;
        var presentation = _game.CurrentMarketPresentation;
        return new
        {
            Actor = observation.Selection.ActorId.ToString(), observation.Selection.Generation,
            TradeDisclosed = observation.ControlledView is null || observation.ControlledView.Trade is not null,
            InventoryDisclosed = observation.ControlledView is null || observation.ControlledView.Inventory is not null,
            PrivateHistory = observation.Selection.ControlWitness is null
                || (observation.Selection.ControlWitness.Capabilities & WorldControlCapabilities.PrivateHistory) != 0,
            Open = _game.IsMarketOpen, Wallet = presentation?.CurrencyBalance,
            InquiryPending = _game.HasMarketRequestInFlight,
            AskDisabled = TradePanel.GetNode<Button>("%AskMerchant").Disabled,
            Draft = new { Market = _game.MarketDraft.MarketId?.ToString(), Direction = _game.MarketDraft.Direction.ToString(),
                _game.MarketDraft.Quantity, Lot = _game.MarketDraft.LotId?.ToString(),
                Review = _game.MarketDraft.Review?.Quote.QuoteId.ToString() },
            Inquiry = _game.CurrentInquiry is { } inquiry ? new { State = inquiry.State.ToString(), Quote = inquiry.QuoteId?.ToString() } : null,
            Stalls = TradeMarkets?.Stalls.Select(stall => new { Market = stall.MarketId.ToString(), Merchant = stall.MerchantActorId.ToString(),
                stall.IsAvailableInPerson, Lots = stall.OwnLots.Select(lot => new { Lot = lot.LotId.ToString(), lot.Quantity, lot.Revision }).ToArray(),
                Showing = stall.OwnShowing is { } shown ? new { Lot = shown.LotId.ToString(), shown.LotRevision, shown.Units, shown.IsLive, shown.MatchesOwnLot } : null }).ToArray() ?? [],
            Quotes = TradeQuotes.Select(quote => new { Id = quote.QuoteId.ToString(), Market = quote.MarketId?.ToString(), Side = quote.Side.ToString(),
                Actor = quote.CounterpartyActorId.ToString(), Lot = quote.LotId.ToString(), quote.ExpectedLotRevision, quote.UnitPrice,
                quote.UnitFees, quote.QuantityAllowance, Status = quote.Status.ToString(), Hash = quote.QuoteHash.Value }).ToArray(),
            Reviewed = _reviewedTrade is { } offer ? new { Actor = offer.Scope.ActorId.ToString(), Generation = offer.Scope.Selection?.Generation,
                Witness = WitnessBytes(offer.Scope.Selection?.ControlWitness), offer.Scope.ExpectedActorRevision,
                Quote = (offer.Intent as AcceptSpotQuoteIntent)?.Acceptance.QuoteId.ToString(), offer.CostText, offer.DetailText } : null
        };
    }
}
