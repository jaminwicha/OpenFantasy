using Godot;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>
/// The stall panel. Its controls record explicit choices in the draft, explain exact terms on hover, focus and in
/// the always-visible inspector, and request the game's separate commands: ask, cancel, show, confirm, inspect a
/// saved record, pause and close. It never submits anything itself and owns no world state.
/// </summary>
internal sealed partial class FarmMarketPanel : CanvasLayer
{
    private Control _root = null!;
    private Label _title = null!;
    private Label _time = null!;
    private Label _quantity = null!;
    private Label _requestStatus = null!;
    private Label _offerSummary = null!;
    private Label _offerDetails = null!;
    private Label _confirmText = null!;
    private Label _historyDetails = null!;
    private Label _notice = null!;
    private Control _lotRow = null!;
    private Control _confirmBox = null!;
    private Button _buyMode = null!;
    private Button _sellMode = null!;
    private Button _less = null!;
    private Button _more = null!;
    private Button _show = null!;
    private Button _ask = null!;
    private Button _cancel = null!;
    private Button _review = null!;
    private Button _confirm = null!;
    private Button _back = null!;
    private Button _record = null!;
    private Button _pause = null!;
    private Button _close = null!;
    private OptionButton _lots = null!;
    private OptionButton _history = null!;
    private Control? _returnFocus;
    private LocalWorldView? _view;
    private LocalMarketStallView? _stall;
    private LocalMarketLotView[] _lotRows = [];
    private PaidQuoteView[] _historyRows = [];
    private string[] _shownLots = [];
    private string[] _shownHistory = [];
    private EntityId? _selectedRecord;
    private string? _recordText;
    private string _focusShape = "";
    private bool _updating;

    internal FarmMarketDraft? Draft { get; set; }
    internal bool IsOpen => _root.Visible;
    internal event Action<FarmSemanticAction>? ActionRequested;
    internal event Action? AskRequested;
    internal event Action? CancelRequested;
    internal event Action<EntityId>? RecordRequested;

    public override void _Ready()
    {
        _root = GetNode<Control>("%MarketRoot");
        _title = GetNode<Label>("%MarketTitle");
        _time = GetNode<Label>("%MarketTime");
        _quantity = GetNode<Label>("%MarketQuantity");
        _requestStatus = GetNode<Label>("%RequestStatus");
        _offerSummary = GetNode<Label>("%OfferSummary");
        _offerDetails = GetNode<Label>("%OfferDetails");
        _confirmText = GetNode<Label>("%ConfirmText");
        _historyDetails = GetNode<Label>("%HistoryDetails");
        _notice = GetNode<Label>("%MarketNotice");
        _lotRow = GetNode<Control>("%LotRow");
        _confirmBox = GetNode<Control>("%ConfirmBox");
        _buyMode = GetNode<Button>("%BuyMode");
        _sellMode = GetNode<Button>("%SellMode");
        _less = GetNode<Button>("%MarketLess");
        _more = GetNode<Button>("%MarketMore");
        _show = GetNode<Button>("%ShowLot");
        _ask = GetNode<Button>("%AskMerchant");
        _cancel = GetNode<Button>("%CancelRequest");
        _review = GetNode<Button>("%ReviewOffer");
        _confirm = GetNode<Button>("%ConfirmTrade");
        _back = GetNode<Button>("%BackFromReview");
        _record = GetNode<Button>("%CheckRecord");
        _pause = GetNode<Button>("%PauseToRead");
        _close = GetNode<Button>("%CloseMarket");
        _lots = GetNode<OptionButton>("%MarketLot");
        _history = GetNode<OptionButton>("%MarketHistory");
        _buyMode.Pressed += () => ChooseDirection(LocalTradeDirection.Buy);
        _sellMode.Pressed += () => ChooseDirection(LocalTradeDirection.Sell);
        _less.Pressed += () => ChangeQuantity(-1);
        _more.Pressed += () => ChangeQuantity(1);
        _lots.ItemSelected += ChooseLot;
        _history.ItemSelected += ChooseRecord;
        _show.Pressed += () => ActionRequested?.Invoke(FarmSemanticAction.ShowTradeLot);
        _ask.Pressed += () => AskRequested?.Invoke();
        _cancel.Pressed += () => CancelRequested?.Invoke();
        _review.Pressed += Review;
        _back.Pressed += Back;
        _confirm.Pressed += () => ActionRequested?.Invoke(FarmSemanticAction.ConfirmTrade);
        _record.Pressed += () => { if (_selectedRecord is { } id) RecordRequested?.Invoke(id); };
        _pause.Pressed += () => ActionRequested?.Invoke(FarmSemanticAction.TogglePause);
        _close.Pressed += () => ActionRequested?.Invoke(FarmSemanticAction.CloseMarket);
    }

    /// <summary>Opens on the chosen section; focus goes to that section's first choice, never to a commitment.</summary>
    internal void Open(bool offersSection)
    {
        _returnFocus = GetViewport().GuiGetFocusOwner();
        _selectedRecord = null;
        _recordText = null;
        _notice.Text = "";
        _focusShape = "";
        _root.Show();
        Refresh();
        (offersSection ? _history : _buyMode).GrabFocus();
    }

    internal void Close()
    {
        _root.Hide();
        if (IsInstanceValid(_returnFocus) && _returnFocus!.IsVisibleInTree()) _returnFocus.GrabFocus();
        _returnFocus = null;
    }

    /// <summary>A short message about the latest result or interruption; it changes no choice.</summary>
    internal void Notice(string text) => _notice.Text = text;

    internal void ShowRecord(EntityId quoteId, PaidQuoteView? record, string? message)
    {
        if (_selectedRecord != quoteId || _view is null || _stall is null) return;
        _recordText = message ?? (record is null ? "No saved record of that offer is available to you."
            : "Saved record: " + FarmMarketTerms.Outcome(_view, record, FarmMarketTerms.Merchant(_view, _stall),
                FarmMarketTerms.Goods(_view, _stall)) + (record.ClosedAtSequence is { } closed ? $" · closed at world revision {closed}" : ""));
        Refresh();
    }

    internal void Present(LocalWorldView view, LocalWorldRunState runState, LocalTradeInquiryView? inquiry,
        IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> offers, bool canCommit)
    {
        _view = view;
        _stall = Draft?.MarketId is { } market ? view.Markets?.Stalls.FirstOrDefault(value => value.MarketId == market) : null;
        if (!IsOpen || _stall is null || Draft is null) return;
        string merchant = FarmMarketTerms.Merchant(view, _stall);
        string goods = FarmMarketTerms.Goods(view, _stall);
        bool running = runState == LocalWorldRunState.Running;
        _title.Text = $"{merchant}'s stall · {goods}";
        _time.Text = (runState switch
        {
            LocalWorldRunState.Running => "World continuing",
            LocalWorldRunState.Paused => "World paused here · nothing advances",
            LocalWorldRunState.Waiting => "World waiting",
            LocalWorldRunState.Resolving => "Resolving world changes",
            _ => "World unavailable"
        }) + $" · step {view.Markets?.AdvancingStep:N0} · purse {view.Progression?.CurrencyBalance} {view.Progression?.CurrencyUnit}"
            + (_stall.IsAvailableInPerson ? "" : $" · {merchant} is not within reach");
        _pause.Text = runState == LocalWorldRunState.Paused ? "Resume time" : "Pause to read";
        _pause.TooltipText = "Pausing stops only this single-player world: offer windows and your request stop aging, "
            + $"and {merchant} cannot answer until time resumes. It is not a shared multiplayer Wait.";
        PresentChoices(view, goods);
        PresentRequest(view, inquiry, offers, running, canCommit, merchant);
        PresentOffer(view, offers, canCommit, merchant, goods);
        PresentHistory(view, merchant, goods);
        WireFocus();
    }

    private void PresentChoices(LocalWorldView view, string goods)
    {
        bool selling = Draft!.Direction == LocalTradeDirection.Sell;
        _buyMode.SetPressedNoSignal(!selling);
        _sellMode.SetPressedNoSignal(selling);
        _lotRow.Visible = selling;
        _show.Visible = selling;
        _quantity.Text = $"{Draft.Quantity} {goods}";
        LocalMarketLotView? lot = _stall!.OwnLots.FirstOrDefault(value => value.LotId == Draft.LotId);
        ulong maximum = selling && lot is not null ? Math.Min(lot.Quantity, _stall.MaximumUnitsPerOffer) : _stall.MaximumUnitsPerOffer;
        _less.Disabled = Draft.Quantity <= 1;
        _more.Disabled = Draft.Quantity >= maximum;
        _more.TooltipText = $"Choose one more unit, up to {maximum} for this request. Nothing is asked or moved.";
        // Keep the open popup's row identities stable until the player chooses or cancels.
        if (selling && !_lots.GetPopup().Visible)
        {
            _updating = true;
            _lotRows = _stall.OwnLots.ToArray();
            string prompt = Draft.LotId is not null && lot is null ? "Chosen lot is no longer usable" : "Choose one exact lot…";
            Fill(_lots, [prompt, .. _lotRows.Select((value, index) => $"{goods} × {value.Quantity} · lot {index + 1}")], ref _shownLots);
            _lots.Select(Array.FindIndex(_lotRows, value => value.LotId == Draft.LotId) + 1);
            _updating = false;
        }
    }

    private void PresentRequest(LocalWorldView view, LocalTradeInquiryView? inquiry,
        IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> offers, bool running, bool canCommit, string merchant)
    {
        FarmMarketRequest request = MarketActionOffers.PrepareRequest(view, _stall!, Draft!, inquiry);
        _ask.Text = $"Ask {merchant}";
        _ask.Disabled = request.Inquiry is null;
        _ask.TooltipText = request.BlockingReason ?? $"Ask {merchant} for terms for exactly these choices. Asking binds, reserves "
            + "and spends nothing; she answers while world time advances.";
        _cancel.Disabled = inquiry?.State != LocalTradeInquiryState.Waiting;
        _cancel.TooltipText = "Withdraw the waiting request. An offer already made keeps its own window.";
        PresentCommit(_show, offers.GetValueOrDefault(FarmSemanticAction.ShowTradeLot), canCommit);
        string status = FarmMarketTerms.Request(inquiry, view.Markets?.AdvancingStep ?? 0, running, merchant);
        if (_stall!.OwnShowing is { } shown)
        {
            status += shown.IsLive && shown.MatchesOwnLot
                ? $"\nShowing {shown.Units} to {merchant} until step {shown.ExpiresAtStep:N0}."
                : "\nYour earlier showing no longer applies.";
        }
        _requestStatus.Text = status;
    }

    private void PresentOffer(LocalWorldView view, IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> offers,
        bool canCommit, string merchant, string goods)
    {
        PaidQuoteView? open = MarketActionOffers.OpenOffer(view, _stall!);
        FarmMarketReview? review = Draft!.Review;
        _offerSummary.Text = open is null ? $"No open offer from {merchant}."
            : FarmMarketTerms.Summary(view, open, Math.Min(Draft.Quantity, open.QuantityAllowance), goods);
        _offerDetails.Text = open is null ? "Ask for terms to receive an offer. Its price, units, dues, total and window appear here."
            : FarmMarketTerms.Describe(view, open, open.QuantityAllowance, merchant, goods);
        _review.Disabled = open is null || review is not null;
        _review.TooltipText = open is null ? "No offer to review." : _offerDetails.Text
            + "\nReviewing captures these exact terms for your choices; it sends nothing.";
        // While reviewing, the confirmation card restates the terms, so the longer inspector steps aside.
        _offerDetails.Visible = review is null;
        _confirmBox.Visible = review is not null;
        FarmActionOffer? confirm = offers.GetValueOrDefault(FarmSemanticAction.ConfirmTrade);
        if (review is not null && confirm is not null)
        {
            PaidQuoteView current = view.PaidQuotes.FirstOrDefault(quote => quote.QuoteId == review.Quote.QuoteId) ?? review.Quote;
            _confirm.Text = confirm.Label;
            _confirmText.Text = "You are about to: " + FarmMarketTerms.Restate(view, current, review.Units, merchant, goods)
                + (confirm.BlockingReason is { } reason ? "\nCannot confirm: " + reason
                    : canCommit ? "" : "\nResume world time to confirm.");
            _confirmText.TooltipText = FarmMarketTerms.Describe(view, current, review.Units, merchant, goods);
        }
        PresentCommit(_confirm, confirm, canCommit && review is not null);
    }

    private void PresentHistory(LocalWorldView view, string merchant, string goods)
    {
        if (_history.GetPopup().Visible) return;
        _updating = true;
        _historyRows = view.PaidQuotes.Where(quote => quote.IssuerActorId == _stall!.MerchantActorId && !quote.ViewerIsIssuer)
            .OrderByDescending(quote => quote.IssuedAtStep).ThenBy(quote => quote.QuoteId).ToArray();
        Fill(_history, ["Choose an offer or outcome…", .. _historyRows.Select(quote =>
            FarmMarketTerms.Outcome(view, quote, merchant, goods))], ref _shownHistory);
        _history.Select(Array.FindIndex(_historyRows, quote => quote.QuoteId == _selectedRecord) + 1);
        _updating = false;
        PaidQuoteView? selected = _historyRows.FirstOrDefault(quote => quote.QuoteId == _selectedRecord);
        _record.Disabled = _selectedRecord is null;
        _historyDetails.Text = _selectedRecord is null ? "Live answers and outcomes of this save segment are listed here."
            : (selected is null ? "Outside the live list; check its saved record." : FarmMarketTerms.Outcome(view, selected, merchant, goods))
                + "\n" + (_recordText ?? "Check the saved record to read it from history.");
    }

    private static void PresentCommit(Button button, FarmActionOffer? offer, bool canCommit)
    {
        button.Disabled = offer is null || !offer.CanExecute || !canCommit;
        button.TooltipText = offer is null ? "Unavailable here." : $"{offer.Label}: {offer.CostText}"
            + (offer.DetailText is { } detail ? "\n" + detail : "")
            + (offer.BlockingReason is { } reason ? "\nUnavailable: " + reason : canCommit ? "" : "\nResume world time first.");
    }

    private void ChooseDirection(LocalTradeDirection direction)
    {
        if (Draft is null) return;
        Draft.SetDirection(direction);
        Refresh();
    }

    private void ChangeQuantity(int change)
    {
        if (Draft is null || _stall is null) return;
        ulong next = change < 0 ? Draft.Quantity - 1 : Draft.Quantity + 1;
        Draft.SetQuantity(next, _stall.MaximumUnitsPerOffer);
        Refresh();
    }

    private void ChooseLot(long index)
    {
        if (_updating || Draft is null || _stall is null) return;
        if (index <= 0 || index > _lotRows.Length) Draft.ClearLot();
        else Draft.SelectLot(_stall, _lotRows[index - 1].LotId);
        Refresh();
    }

    private void ChooseRecord(long index)
    {
        if (_updating) return;
        _selectedRecord = index <= 0 || index > _historyRows.Length ? null : _historyRows[index - 1].QuoteId;
        _recordText = null;
        Refresh();
    }

    private void Review()
    {
        if (Draft is null || _view is null || _stall is null) return;
        _notice.Text = MarketActionOffers.OpenOffer(_view, _stall) is { } open && Draft.CaptureReview(_view, _stall, open)
            ? "Reviewed. Confirm restates exactly these terms; Back or any change discards them."
            : "That offer does not match your current choices. Change units or lot, or ask again.";
        Refresh();
        if (_confirmBox.Visible) _back.GrabFocus();
    }

    private void Back()
    {
        Draft?.DiscardReview();
        _notice.Text = "Review discarded. Nothing was sent.";
        Refresh();
        _review.GrabFocus();
    }

    /// <summary>Re-presents the last observation immediately after a local choice, without a new world read.</summary>
    private void Refresh()
    {
        if (_view is not null) PresentLocal?.Invoke();
    }

    /// <summary>The game re-presents with its current offers, so a choice and its offers stay one coherent snapshot.</summary>
    internal event Action? PresentLocal;

    private static void Fill(OptionButton control, string[] labels, ref string[] shown)
    {
        if (shown.SequenceEqual(labels)) return;
        control.Clear();
        foreach (string label in labels) control.AddItem(label);
        shown = labels;
    }

    /// <summary>Explicit cyclic focus over the visible controls, so arrows, D-pad and Tab never leave the panel.</summary>
    private void WireFocus()
    {
        Control[] chain = new Control[] { _buyMode, _sellMode, _lots, _less, _more, _show, _ask, _cancel, _review, _confirm,
            _back, _history, _record, _pause, _close }.Where(control => control.IsVisibleInTree()).ToArray();
        string shape = string.Join(",", chain.Select(control => control.Name.ToString()));
        if (shape == _focusShape) return;
        _focusShape = shape;
        for (int index = 0; index < chain.Length; index++)
        {
            NodePath previous = chain[index].GetPathTo(chain[(index + chain.Length - 1) % chain.Length]);
            NodePath next = chain[index].GetPathTo(chain[(index + 1) % chain.Length]);
            chain[index].FocusPrevious = previous;
            chain[index].FocusNext = next;
            chain[index].FocusNeighborTop = previous;
            chain[index].FocusNeighborLeft = previous;
            chain[index].FocusNeighborBottom = next;
            chain[index].FocusNeighborRight = next;
        }
        if (GetViewport().GuiGetFocusOwner() is not { } focus || !focus.IsVisibleInTree() || !_root.IsAncestorOf(focus))
            chain.FirstOrDefault()?.GrabFocus();
    }
}
