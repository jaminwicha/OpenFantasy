using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

internal sealed partial class FarmClientSession
{
    internal ISelectedPlayerSession? SelectedSession => World as ISelectedPlayerSession;

    internal LocalSelectedPlayerObservation? ReadSelection() =>
        SelectedSession is { SupportsActorSelection: true } selected ? selected.ReadSelectedObservation() : null;

    internal async Task<LocalTradeInquiryView> AskSelectedAsync(LocalSelectedPlayerObservation observed, LocalTradeInquiry inquiry)
    {
        var result = await SelectedSession!.SubmitSelectedTradeInquiryAsync(observed.Selection, inquiry);
        result.ValidateFor(SelectedSession.ReadSelectedObservation(), LocalSelectedQueryOperation.SubmitTradeInquiry);
        return result.Value;
    }

    internal async Task<LocalTradeInquiryView?> CancelSelectedRequestAsync(LocalSelectedPlayerObservation observed)
    {
        var result = await SelectedSession!.CancelSelectedTradeInquiryAsync(observed.Selection);
        result.ValidateFor(SelectedSession.ReadSelectedObservation(), LocalSelectedQueryOperation.CancelTradeInquiry);
        return result.Value;
    }

    internal IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> CreateOffers(LocalSelectedPlayerObservation observed,
        FarmTarget? target = null, FarmGiftSelection? gifts = null, FarmMarketDraft? market = null)
    {
        FarmActionScope scope = FarmActionScope.From(_delivery, observed);
        IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> offers;
        if (observed.ControlledView is not null)
            offers = FarmSelectedActionOffers.Create(observed, scope, target, WrenActorId, gifts, WoodDefinitionId, BerryDefinitionId);
        else
        {
            var home = _offers.Create(observed.HomeView, target, null, market).ToDictionary(pair => pair.Key,
                pair => pair.Value.WithScope(scope));
            MaterialProposalActionOffers.Add(FarmInventoryPresentation.From(observed), scope, gifts, home,
                WoodDefinitionId, BerryDefinitionId);
            offers = home;
        }
        foreach (FarmActionOffer offer in offers.Values) _issuedOffers.Add(offer, new OfferedAction());
        return offers;
    }

    internal FarmActionOffer ReviewSelectedQuote(LocalSelectedPlayerObservation observed, PaidQuoteView quote, ulong units)
    {
        if (observed.ControlledView?.Trade is not { } trade
            || !trade.Quotes.Any(current => current.QuoteId == quote.QuoteId && current.QuoteHash == quote.QuoteHash))
            throw new InvalidOperationException("This offer is not in the selected character's current trade view.");
        FarmActionOffer offer = FarmSelectedActionOffers.ConfirmQuote(FarmActionScope.From(_delivery, observed), quote,
            units, _delivery, trade.Wallet?.Unit ?? "coins");
        _issuedOffers.Add(offer, new OfferedAction());
        return offer;
    }

    internal WorldActionCommand PreparePermissionCommand(LocalControlPermissionBoard board, ControlPermissionIntent intent)
    {
        LocalWorldView home = World.ReadView();
        if (board.WorldId != home.WorldId || board.BranchId != home.BranchId || board.HomeActorId != home.PlayerActorId)
            throw new InvalidOperationException("The control board belongs to another home character or world.");
        return new(NewCommandId(), _delivery.PrincipalId, _delivery.SessionId, board.HomeActorId,
            board.WorldId, board.BranchId, board.HomeActorRevision, intent);
    }
}
