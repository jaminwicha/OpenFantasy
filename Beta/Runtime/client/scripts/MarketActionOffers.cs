using System.Security.Cryptography;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>A nonbinding stall request ready to ask, or the known reason it cannot be asked now.</summary>
internal sealed record FarmMarketRequest(LocalTradeInquiry? Inquiry, string? BlockingReason);

/// <summary>
/// Stall offers read from one public snapshot. The wheel's Buy, Sell and Offers choices only open the stall panel.
/// Showing a lot and confirming a reviewed offer are separate world commands with exact captured intents; asking
/// is a nonbinding session request. Nothing here selects another lot, quote, merchant or quantity than the
/// player's explicit choices, and nothing asks again or accepts on the player's behalf.
/// </summary>
internal static class MarketActionOffers
{
    internal static bool IsStallChoice(FarmSemanticAction action) =>
        action is FarmSemanticAction.TradeBuy or FarmSemanticAction.TradeSell or FarmSemanticAction.TradeOffers;

    /// <summary>The stall the panel addresses: the one available in person, else the first known stall.</summary>
    internal static LocalMarketStallView? ContextStall(LocalWorldView view) =>
        view.Markets?.Stalls.FirstOrDefault(stall => stall.IsAvailableInPerson) ?? view.Markets?.Stalls.FirstOrDefault();

    internal static void Add(LocalWorldView view, FarmActionScope scope, FarmMarketDraft? draft,
        IDictionary<FarmSemanticAction, FarmActionOffer> offers)
    {
        if (ContextStall(view) is not { } stall) return;
        string merchant = FarmMarketTerms.Merchant(view, stall);
        string goods = FarmMarketTerms.Goods(view, stall);
        string? context = ContextBlocker(view, stall, merchant);
        offers[FarmSemanticAction.TradeBuy] = FarmActionOffer.Panel(FarmSemanticAction.TradeBuy, scope, $"Buy {goods}",
            merchant, stall.MerchantActorId, $"Open {merchant}'s stall to choose how many {goods} to ask for. Asking binds nothing; "
            + "nothing is bought until you confirm one exact reviewed offer.", context);
        offers[FarmSemanticAction.TradeSell] = FarmActionOffer.Panel(FarmSemanticAction.TradeSell, scope, $"Sell {goods}",
            merchant, stall.MerchantActorId, $"Open {merchant}'s stall to choose one of your own lots to show and sell. Showing "
            + "and asking sell nothing.", context ?? (stall.OwnLots.Count == 0 ? $"You have no usable {goods} to sell." : null));
        offers[FarmSemanticAction.TradeOffers] = FarmActionOffer.Panel(FarmSemanticAction.TradeOffers, scope, "Offers",
            merchant, stall.MerchantActorId, $"Inspect {merchant}'s answers, any open offer's timing and your saved outcomes. "
            + "Inspection spends nothing.", null);
        if (draft?.MarketId != stall.MarketId) return;
        offers[FarmSemanticAction.ShowTradeLot] = Show(view, scope, stall, draft, merchant, goods, context);
        offers[FarmSemanticAction.ConfirmTrade] = Confirm(view, scope, stall, draft, merchant, goods, context);
    }

    /// <summary>
    /// The player's nonbinding request from its explicit choices. It names only the stall, commodity, direction,
    /// units and, to sell, the player's own shown lot; it never names a price, a merchant lot or a quote.
    /// </summary>
    internal static FarmMarketRequest PrepareRequest(LocalWorldView view, LocalMarketStallView stall, FarmMarketDraft draft,
        LocalTradeInquiryView? current)
    {
        string merchant = FarmMarketTerms.Merchant(view, stall);
        string goods = FarmMarketTerms.Goods(view, stall);
        string? blocker = ContextBlocker(view, stall, merchant)
            ?? (draft.MarketId != stall.MarketId ? "Open this stall to choose a request."
            : current?.State == LocalTradeInquiryState.Waiting ? $"Your request is waiting for {merchant}'s answer. Cancel it to change it."
            : OpenOffer(view, stall) is not null ? $"{merchant} keeps one standing offer for you. Accept it or let it close first."
            : draft.Quantity > stall.MaximumUnitsPerOffer ? $"{merchant} offers at most {stall.MaximumUnitsPerOffer} {goods} at once."
            : draft.Direction == LocalTradeDirection.Sell ? SellBlocker(stall, draft, merchant, goods) : null);
        if (blocker is not null) return new(null, blocker);
        var inquiry = new LocalTradeInquiry(stall.MarketId, stall.Commodity, draft.Direction, draft.Quantity,
            draft.Direction == LocalTradeDirection.Sell ? draft.LotId : null);
        return new(inquiry, null);
    }

    /// <summary>The one open offer from this stall's merchant to the player, which the player may review.</summary>
    internal static PaidQuoteView? OpenOffer(LocalWorldView view, LocalMarketStallView stall) => view.PaidQuotes
        .Where(quote => !quote.ViewerIsIssuer && quote.IssuerActorId == stall.MerchantActorId && quote.Status == PaidQuoteStatus.Open)
        .OrderByDescending(quote => quote.IssuedAtStep).ThenBy(quote => quote.QuoteId).FirstOrDefault();

    private static string? SellBlocker(LocalMarketStallView stall, FarmMarketDraft draft, string merchant, string goods) =>
        draft.LotId is null ? $"Choose one exact lot of your own {goods}."
        : stall.OwnShowing is not { } shown || shown.LotId != draft.LotId || !shown.IsLive || !shown.MatchesOwnLot
            ? $"Show that lot to {merchant} first; she can only offer for goods you showed."
        : draft.Quantity > shown.Units ? $"You showed {shown.Units} {goods}; show more before asking to sell {draft.Quantity}."
        : null;

    private static FarmActionOffer Show(LocalWorldView view, FarmActionScope scope, LocalMarketStallView stall,
        FarmMarketDraft draft, string merchant, string goods, string? context)
    {
        ulong window = Math.Min(NativeTradingPolicy.ShowingWindowSteps, stall.MaximumWindowSteps);
        LocalMarketLotView? lot = stall.OwnLots.FirstOrDefault(value => value.LotId == draft.LotId);
        string? blocker = context
            ?? (draft.Direction != LocalTradeDirection.Sell ? "Showing a lot belongs to selling."
            : draft.LotId is null ? $"Choose one exact lot of your own {goods}."
            : lot is null ? "That lot is no longer usable in your pack. Choose a lot again."
            : draft.Quantity > lot.Quantity ? $"That lot holds only {lot.Quantity} {goods}." : null);
        DiscloseMarketLotIntent? intent = blocker is null ? stall.PrepareShowing(lot!.LotId, draft.Quantity, window) : null;
        if (blocker is null && intent is null) blocker = $"{merchant} cannot be shown goods here right now.";
        string detail = $"Shows {draft.Quantity} {goods} from that exact lot to {merchant} for {window:N0} world steps "
            + $"({FarmMarketTerms.Seconds(window)} while time advances). Showing sells, reserves and moves nothing and agrees to "
            + $"no price; it only lets {merchant} answer a request to buy those units.";
        return new FarmActionOffer(FarmSemanticAction.ShowTradeLot, scope, $"Show to {merchant}", merchant, stall.MerchantActorId,
            FarmActionOffers.NoCost, $"You showed {draft.Quantity} {goods} to {merchant}. Nothing was sold.", intent, blocker,
            detailText: detail);
    }

    private static FarmActionOffer Confirm(LocalWorldView view, FarmActionScope scope, LocalMarketStallView stall,
        FarmMarketDraft draft, string merchant, string goods, string? context)
    {
        FarmMarketReview? review = draft.Review;
        PaidQuoteView? current = review is null ? null
            : view.PaidQuotes.FirstOrDefault(quote => quote.QuoteId == review.Quote.QuoteId);
        bool buying = review?.Quote.Side == SpotQuoteSide.IssuerSells;
        string? blocker = context ?? ConfirmBlocker(view, stall, review, current, merchant, goods);
        EntityId? commandId = blocker is null ? NewCommandId() : null;
        WorldActionIntent? intent = blocker is null
            ? current!.PrepareAcceptance(commandId!.Value, new TrustedCommandDelivery(scope.PrincipalId, scope.SessionId), review!.Units)
            : null;
        string unit = view.Progression?.CurrencyUnit ?? "coins";
        string label = buying ? "Confirm purchase" : "Confirm sale";
        string cost = review is null ? "Review one offer first"
            : buying ? $"{checked(review.Payment + review.Fees)} {unit}" : $"{review.Units} {goods}";
        string completion = review is null ? ""
            : buying ? $"You bought {review.Units} {goods} from {merchant} for {checked(review.Payment + review.Fees)} {unit}."
            : $"You sold {review.Units} {goods} to {merchant} for {review.Payment} {unit}.";
        string detail = review is null ? "Confirmation restates one reviewed offer exactly; it never asks again or picks newer terms."
            : FarmMarketTerms.Describe(view, review.Quote, review.Units, merchant, goods);
        return new FarmActionOffer(FarmSemanticAction.ConfirmTrade, scope, label, merchant, stall.MerchantActorId, cost,
            completion, intent, blocker, detailText: detail, commandId: commandId);
    }

    private static string? ConfirmBlocker(LocalWorldView view, LocalMarketStallView stall, FarmMarketReview? review,
        PaidQuoteView? current, string merchant, string goods)
    {
        if (review is null) return "Review one offer first; confirming restates its exact terms.";
        if (current is null) return "This offer is no longer in your live list. Check its saved record.";
        if (current.Status != PaidQuoteStatus.Open)
            return $"This offer is {current.Status.ToString().ToLowerInvariant()}. Ask {merchant} again for new terms.";
        if (current.QuoteHash != review.Quote.QuoteHash || current.IssuerActorId != stall.MerchantActorId)
            return "These terms are not the ones you reviewed. Review the offer again.";
        if (current.ObservedIssuerRevision is null) return $"{merchant} is not within reach to complete the trade.";
        if (current.ObservedIssuerRevision != review.Quote.ObservedIssuerRevision)
            return $"{merchant} moved since you reviewed this offer. Review it again.";
        if (current.Side == SpotQuoteSide.IssuerSells)
        {
            ulong total = checked(review.Payment + review.Fees);
            ulong balance = view.Progression?.CurrencyBalance ?? 0;
            return balance < total ? $"You need {total} {view.Progression?.CurrencyUnit}; you have {balance}." : null;
        }
        return stall.OwnLots.FirstOrDefault(lot => lot.LotId == current.LotId) is { } own
            && own.Revision == current.ExpectedLotRevision && own.Quantity >= review.Units
            ? null : $"The {goods} you showed changed. Show them and ask again.";
    }

    /// <summary>
    /// Stall actions belong to ordinary unheld exploration in person. Inspecting answers and records stays
    /// possible, but requests and commitments wait for that boundary.
    /// </summary>
    private static string? ContextBlocker(LocalWorldView view, LocalMarketStallView stall, string merchant) =>
        view.HasReadingHold ? "Finish the current conversation before trading."
        : view.IsResolvingConsequences ? "Wait for the current world changes to finish."
        : view.Care?.PendingAcknowledgmentId is not null ? "The accepted recovery treatment must finish first."
        : view.EncounterLifecycle is { } lifecycle && lifecycle.Phase != EncounterLifecyclePhase.Exploration
            ? "Trading happens during ordinary exploration."
        : view.EncounterLifecycle is { PlayerCanAct: false } ? "You cannot act until your current recovery ends."
        : !stall.IsAvailableInPerson ? $"{merchant} must be within reach and able to trade in person."
        : null;

    private static EntityId NewCommandId() => EntityId.FromBytes(RandomNumberGenerator.GetBytes(EntityId.ByteLength));
}
