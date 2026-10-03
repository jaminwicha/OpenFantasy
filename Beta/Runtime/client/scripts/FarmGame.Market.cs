using Godot;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>The shared stall interface binds each request and result to its exact shopper and panel visit.</summary>
public partial class FarmGame
{
    private FarmMarketPanel _market = null!;
    private readonly FarmMarketDraft _marketDraft = new();
    private MarketInquiryWork? _marketInquiry;
    private MarketHistoryWork? _marketHistory;
    private readonly FarmMarketQueryLifetime _marketQueries = new();

    private sealed record MarketInquiryWork(FarmMarketQueryContext Context, Task<LocalTradeInquiryView?> Task, string? Success);
    private sealed record MarketHistoryWork(FarmMarketQueryContext Context, ulong RecordVersion, EntityId QuoteId,
        Task<PaidQuoteView?> Task);

    internal bool IsMarketOpen => _input.MarketOpen;
    internal bool HasMarketRequestInFlight => _marketInquiry is not null;
    internal FarmMarketDraft MarketDraft => _marketDraft;
    internal FarmMarketPresentation? CurrentMarketPresentation
    {
        get
        {
            if (_session is null) return null;
            var observation = _session.ReadSelection();
            if (observation?.ControlledView is { Trade: null }) return null;
            return observation is null ? FarmMarketPresentation.From(_session.World.ReadView(), _session.IsRemote)
                : FarmMarketPresentation.From(observation, _session.IsRemote);
        }
    }
    internal LocalTradeInquiryView? CurrentInquiry => CurrentMarketPresentation is { } view
        ? view.Selection is null ? _session?.World.ReadTradeInquiry()
            : _marketQueries.InquiryFor(view.Scope) : null;

    private void ReadyMarket()
    {
        _market = GD.Load<PackedScene>("res://scenes/FarmMarketPanel.tscn").Instantiate<FarmMarketPanel>();
        AddChild(_market);
        _market.Draft = _marketDraft;
        _market.ActionRequested += Perform;
        _market.AskRequested += AskMerchant;
        _market.CancelRequested += CancelMarketRequest;
        _market.RecordRequested += InspectQuoteRecord;
        _market.StallRequested += ChooseStall;
        _market.PresentLocal += () => { if (_session is not null) Present(); };
    }

    private void OpenMarket(FarmSemanticAction choice)
    {
        if (_session is null || CurrentMarketPresentation is not { } view) return;
        _marketDraft.ObserveScope(view);
        if (MarketActionOffers.ContextStall(view, _marketDraft.ChosenMarketId) is not { } stall) return;
        LocalTradeDirection? direction = choice switch
        {
            FarmSemanticAction.TradeBuy => LocalTradeDirection.Buy,
            FarmSemanticAction.TradeSell => LocalTradeDirection.Sell,
            _ => null
        };
        if (MarketActionOffers.PinsOnOpening(view, stall, _marketDraft.ChosenMarketId)) _marketDraft.Begin(view, stall.MarketId, direction);
        else _marketDraft.Look(view, stall.MarketId);
        _input.SetMarketOpen(true);
        _hud.SetCovered(true);
        try { SendMovement(0, 0); }
        catch (InvalidOperationException error) { Fail("The world has stopped. " + error.GetBaseException().Message); return; }
        _market.Open(choice == FarmSemanticAction.TradeOffers);
        _marketQueries.OpenVisit();
        if (!_marketDraft.IsExplicit)
            _market.Notice("No stall is within reach and none is chosen. Choose one under Stall, or close this and walk to a merchant.");
        Present();
    }

    private void CloseMarket()
    {
        _input.SetMarketOpen(false);
        _marketDraft.DiscardReview();
        _marketDraft.ReleaseLook();
        _hud.SetCovered(false);
        _market.Close();
    }

    private FarmMarketQueryContext CaptureMarket(FarmMarketPresentation view) => new(view.Scope, _market.PresentationLifetime, _marketDraft.MarketId);

    private bool OwnsMarket(FarmMarketQueryContext context) => _session is not null && _input.Focused && _input.MarketOpen
        && _market.IsOpen && _market.PresentationLifetime == context.Lifetime
        && _marketDraft.MarketId == context.MarketId && CurrentMarketPresentation?.Scope == context.Scope;

    private FarmMarketPresentation? DisplayedMarket => _market.CurrentPresentation is { } shown
        && CurrentMarketPresentation?.Scope == shown.Scope && _marketDraft.MatchesScope(shown) ? shown : null;

    private void AskMerchant()
    {
        if (_session is null || !_input.MarketOpen || !_input.Focused || HasMarketRequestInFlight || _marketHistory is not null
            || DisplayedMarket is not { } view
            || MarketActionOffers.ContextStall(view, _marketDraft.MarketId) is not { } stall) return;
        FarmMarketRequest request = MarketActionOffers.PrepareRequest(view, stall, _marketDraft, CurrentInquiry);
        if (request.Inquiry is not { } inquiry) { _market.Notice(request.BlockingReason ?? "That request is not available."); return; }
        try
        {
            _marketInquiry = new(CaptureMarket(view), QueryMarketInquiry(view, LocalSelectedQueryOperation.SubmitTradeInquiry, inquiry), null);
            _market.Notice(view.SharedWorld ? "Asked. Nothing is reserved; other ready players may keep time running."
                : _input.Paused ? "Asked. World time is paused, so the answer waits until you resume."
                : "Asked. Nothing is reserved; the answer comes shortly while time runs.");
        }
        catch (Exception error) { _market.Notice(error.Message); }
    }

    private void CancelMarketRequest()
    {
        if (_session is null || !_input.MarketOpen || !_input.Focused || HasMarketRequestInFlight || _marketHistory is not null
            || DisplayedMarket is not { } view) return;
        try { _marketInquiry = new(CaptureMarket(view), QueryMarketInquiry(view, LocalSelectedQueryOperation.CancelTradeInquiry),
            "Request cancelled. Nothing was reserved."); }
        catch (Exception error) { _market.Notice(error.Message); }
    }

    private Task<LocalTradeInquiryView?> QueryMarketInquiry(FarmMarketPresentation view,
        LocalSelectedQueryOperation operation, LocalTradeInquiry? inquiry = null) =>
        _marketQueries.Track(ExecuteMarketInquiry(view, operation, inquiry));

    private async Task<LocalTradeInquiryView?> ExecuteMarketInquiry(FarmMarketPresentation view,
        LocalSelectedQueryOperation operation, LocalTradeInquiry? inquiry = null)
    {
        if (view.Selection is not { } selection)
            return operation == LocalSelectedQueryOperation.SubmitTradeInquiry ? await _session!.AskAsync(inquiry!).ConfigureAwait(false)
                : await _session!.CancelRequestAsync().ConfigureAwait(false);
        var selected = _session!.SelectedSession!;
        if (operation == LocalSelectedQueryOperation.SubmitTradeInquiry)
        {
            var submitted = await selected.SubmitSelectedTradeInquiryAsync(selection, inquiry!).ConfigureAwait(false);
            submitted.ValidateFor(selected.ReadSelectedObservation(), operation);
            return submitted.Value;
        }
        var response = operation == LocalSelectedQueryOperation.CancelTradeInquiry
            ? await selected.CancelSelectedTradeInquiryAsync(selection).ConfigureAwait(false) : await selected.ReadSelectedTradeInquiryAsync(selection).ConfigureAwait(false);
        response.ValidateFor(selected.ReadSelectedObservation(), operation);
        return response.Value;
    }

    private void InspectQuoteRecord(EntityId quoteId)
    {
        if (_session is null || !_input.MarketOpen || !_input.Focused || _marketHistory is not null || HasMarketRequestInFlight
            || DisplayedMarket is not { CanReadHistory: true } view || _market.SelectedRecord != quoteId) return;
        _marketHistory = new(CaptureMarket(view), _market.RecordSelectionVersion, quoteId, ReadMarketHistory(view, quoteId));
        _market.ShowRecord(quoteId, null, "Reading the saved record…");
    }

    private Task<PaidQuoteView?> ReadMarketHistory(FarmMarketPresentation view, EntityId id) =>
        _marketQueries.Track(ExecuteMarketHistory(view, id));

    private async Task<PaidQuoteView?> ExecuteMarketHistory(FarmMarketPresentation view, EntityId id)
    {
        if (view.Selection is not { } selection) return await _session!.World.InspectPaidQuoteAsync(id).ConfigureAwait(false);
        var selected = _session!.SelectedSession!;
        var response = await selected.InspectSelectedPaidQuoteHistoryAsync(selection, id).ConfigureAwait(false);
        response.ValidateFor(selected.ReadSelectedObservation(), LocalSelectedQueryOperation.InspectPaidQuoteHistory, id);
        return response.Value;
    }

    private void CompleteMarketTasks()
    {
        if (_marketInquiry is { Task.IsCompleted: true } work)
        {
            _marketInquiry = null;
            LocalTradeInquiryView? inquiry = null;
            string? failure = null;
            try { inquiry = work.Task.GetAwaiter().GetResult(); }
            catch (Exception error) { failure = "Request unavailable: " + error.GetBaseException().Message; }
            _marketQueries.Observed(work.Task);
            if (_marketQueries.CompleteInquiry(work.Context, OwnsMarket(work.Context) ? work.Context : null,
                inquiry, failure is not null, System.Environment.TickCount64))
            {
                string? outcome = work.Success is null ? null
                    : inquiry?.State == LocalTradeInquiryState.Cancelled ? work.Success
                    : "No waiting request was cancelled. Any offer already issued keeps its own terms.";
                if ((failure ?? outcome) is { } notice) _market.Notice(notice);
            }
        }
        if (_marketHistory is { Task.IsCompleted: true } history)
        {
            _marketHistory = null;
            PaidQuoteView? record = null;
            string? failure = null;
            try { record = history.Task.GetAwaiter().GetResult(); }
            catch (Exception error) { failure = "Saved-record inspection unavailable: " + error.GetBaseException().Message; }
            _marketQueries.Observed(history.Task);
            if (OwnsMarket(history.Context) && _market.RecordSelectionVersion == history.RecordVersion
                && _market.SelectedRecord == history.QuoteId && CurrentMarketPresentation is { CanReadHistory: true })
                _market.ShowRecord(history.QuoteId, record, failure ?? (record is null && history.Context.Scope.Selection is not null
                    ? "No terminal recorded outcome is available for this selection." : null));
        }
        // One bounded current-inquiry refresh at a time; no quote history is polled. Closed visits do not issue reads.
        if (_input.MarketOpen && _input.Focused && _marketInquiry is null && _marketHistory is null
            && CurrentMarketPresentation is { Selection: not null } view
            && _marketQueries.ShouldRead(view.Scope, System.Environment.TickCount64))
        {
            _marketInquiry = new(CaptureMarket(view), QueryMarketInquiry(view, LocalSelectedQueryOperation.ReadTradeInquiry), null);
            _marketQueries.ReadStarted(System.Environment.TickCount64);
        }
    }

    private void ChooseStall(EntityId marketId)
    {
        if (_session is null || !_input.MarketOpen || DisplayedMarket is not { } view
            || view.Markets?.Stalls.Any(stall => stall.MarketId == marketId) != true) return;
        if (_marketDraft.ChosenMarketId == marketId) return;
        _marketDraft.Begin(view, marketId, null);
        _marketQueries.OpenVisit();
        _market.Notice("Now addressing " + _market.StallLabel(view, marketId) + ". Earlier choices were cleared.");
        Present();
    }

    private void PresentMarket(LocalWorldView home)
    {
        if (!_input.MarketOpen || _session is null) return;
        FarmMarketPresentation? view = _presentedSelection?.ControlledView is { Trade: null } ? null
            : _presentedSelection is { } observation ? FarmMarketPresentation.From(observation, _session.IsRemote)
            : FarmMarketPresentation.From(home, _session.IsRemote);
        if (view is null || MarketActionOffers.ContextStall(view, _marketDraft.MarketId) is null)
        { CloseMarket(); return; }
        _market.Present(view, _session.World.ReadRunState(), CurrentInquiry, _presented,
            _input.Permits(Situation(), FarmSemanticAction.ConfirmTrade), _session.IsRemote);
    }

    private void InterruptMarketReview(string reason)
    {
        if (!_input.MarketOpen || _marketDraft.Review is null) return;
        _marketDraft.DiscardReview();
        _market.Notice(reason);
    }
}
