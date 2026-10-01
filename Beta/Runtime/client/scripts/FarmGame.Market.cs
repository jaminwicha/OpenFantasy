using Godot;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>
/// The stall panel's place in the game. Opening it suppresses walking but keeps the current host timing, so a
/// request is answered while the panel stays open; only the player's explicit pause stops time. Requests are
/// nonbinding session input; showing a lot and confirming a reviewed offer are ordinary world commands.
/// </summary>
public partial class FarmGame
{
    private FarmMarketPanel _market = null!;
    private readonly FarmMarketDraft _marketDraft = new();
    private Task<LocalTradeInquiryView>? _marketRequest;
    private Task<LocalTradeInquiryView?>? _marketCancellation;
    private Task<PaidQuoteView?>? _quoteRecord;
    private EntityId? _inspectingQuoteId;

    internal bool IsMarketOpen => _input.MarketOpen;
    internal bool HasMarketRequestInFlight => _marketRequest is not null || _marketCancellation is not null;
    /// <summary>The player's explicit stall choices; probes read it and change it only through panel input.</summary>
    internal FarmMarketDraft MarketDraft => _marketDraft;
    internal LocalTradeInquiryView? CurrentInquiry => _session?.World.ReadTradeInquiry();

    private void ReadyMarket()
    {
        _market = GD.Load<PackedScene>("res://scenes/FarmMarketPanel.tscn").Instantiate<FarmMarketPanel>();
        AddChild(_market);
        _market.Draft = _marketDraft;
        _market.ActionRequested += Perform;
        _market.AskRequested += AskMerchant;
        _market.CancelRequested += CancelMarketRequest;
        _market.RecordRequested += InspectQuoteRecord;
        _market.PresentLocal += () => { if (_session is not null) Present(_session.World.ReadView()); };
    }

    /// <summary>A wheel stall choice opens the panel on its section; it asks, shows and spends nothing.</summary>
    private void OpenMarket(FarmSemanticAction choice)
    {
        if (_session is null || _session.World.ReadView() is not { } view
            || MarketActionOffers.ContextStall(view) is not { } stall) return;
        LocalTradeDirection? direction = choice switch
        {
            FarmSemanticAction.TradeBuy => LocalTradeDirection.Buy,
            FarmSemanticAction.TradeSell => LocalTradeDirection.Sell,
            _ => null
        };
        _marketDraft.Begin(view, stall.MarketId, direction);
        _input.SetMarketOpen(true);
        _hud.SetCovered(true);
        try { _session.World.SetMovement(0, 0); }
        catch (InvalidOperationException error) { Fail("The world has stopped. " + error.GetBaseException().Message); return; }
        _market.Open(choice == FarmSemanticAction.TradeOffers);
        Present(_session.World.ReadView());
    }

    private void CloseMarket()
    {
        _input.SetMarketOpen(false);
        _marketDraft.DiscardReview();
        _hud.SetCovered(false);
        _market.Close();
    }

    private void AskMerchant()
    {
        if (_session is null || !_input.MarketOpen || HasMarketRequestInFlight
            || MarketActionOffers.ContextStall(_session.World.ReadView()) is not { } stall) return;
        LocalWorldView view = _session.World.ReadView();
        FarmMarketRequest request = MarketActionOffers.PrepareRequest(view, stall, _marketDraft, _session.World.ReadTradeInquiry());
        if (request.Inquiry is not { } inquiry)
        {
            _market.Notice(request.BlockingReason ?? "That request is not available.");
            return;
        }
        try
        {
            _marketRequest = _session.AskAsync(inquiry);
            _market.Notice(_input.Paused ? "Asked. World time is paused, so the answer waits until you resume."
                : "Asked. Nothing is reserved; the answer arrives at a coming world step.");
        }
        catch (Exception error)
        {
            _market.Notice(error.Message);
        }
    }

    private void CancelMarketRequest()
    {
        if (_session is null || !_input.MarketOpen || HasMarketRequestInFlight) return;
        try { _marketCancellation = _session.CancelRequestAsync(); }
        catch (Exception error) { _market.Notice(error.Message); }
    }

    /// <summary>Explicit saved-record inspection runs in the session's background history slot, never in a frame.</summary>
    private void InspectQuoteRecord(EntityId quoteId)
    {
        if (_session is null || !_input.MarketOpen || !_input.Focused || _quoteRecord is not null) return;
        try
        {
            _inspectingQuoteId = quoteId;
            _quoteRecord = _session.World.InspectPaidQuoteAsync(quoteId);
            _market.ShowRecord(quoteId, null, "Reading the saved record…");
        }
        catch (Exception error)
        {
            _market.ShowRecord(quoteId, null, error.Message);
        }
    }

    /// <summary>Applies finished market tasks; called once per frame before presentation.</summary>
    private void CompleteMarketTasks()
    {
        if (_marketRequest is { IsCompleted: true } request)
        {
            _marketRequest = null;
            if (!request.IsCompletedSuccessfully)
                _market.Notice("The request was not admitted. " + request.Exception?.GetBaseException().Message);
        }
        if (_marketCancellation is { IsCompleted: true } cancellation)
        {
            _marketCancellation = null;
            _market.Notice(cancellation.IsCompletedSuccessfully ? "Request cancelled. Nothing was reserved."
                : "The request could not be cancelled. " + cancellation.Exception?.GetBaseException().Message);
        }
        if (_quoteRecord is { IsCompleted: true } record)
        {
            _quoteRecord = null;
            _market.ShowRecord(_inspectingQuoteId!.Value, record.IsCompletedSuccessfully ? record.Result : null,
                record.IsCompletedSuccessfully ? null : "Saved-record inspection failed; the save was retained.");
        }
    }

    private void PresentMarket(LocalWorldView view)
    {
        if (!_input.MarketOpen || _session is null) return;
        if (MarketActionOffers.ContextStall(view) is null)
        {
            CloseMarket();
            return;
        }
        _market.Present(view, _session.World.ReadRunState(), _session.World.ReadTradeInquiry(), _presented,
            _input.Permits(Situation(), FarmSemanticAction.ConfirmTrade));
    }

    /// <summary>Focus loss or controller disconnection discards a reviewed offer: confirming needs a fresh review.</summary>
    private void InterruptMarketReview(string reason)
    {
        if (!_input.MarketOpen || _marketDraft.Review is null) return;
        _marketDraft.DiscardReview();
        _market.Notice(reason);
    }
}
