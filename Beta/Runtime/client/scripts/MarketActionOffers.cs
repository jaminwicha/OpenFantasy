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
    internal static LocalMarketStallView? ContextStall(LocalWorldView view) => ContextStall(FarmMarketPresentation.From(view));
    internal static LocalMarketStallView? ContextStall(LocalWorldView view, EntityId? chosen) => ContextStall(FarmMarketPresentation.From(view), chosen);
    internal static void Add(LocalWorldView view, FarmActionScope scope, FarmMarketDraft? draft, IDictionary<FarmSemanticAction, FarmActionOffer> offers) => Add(FarmMarketPresentation.From(view), scope, draft, offers);
    internal static FarmMarketRequest PrepareRequest(LocalWorldView view, LocalMarketStallView stall, FarmMarketDraft draft, LocalTradeInquiryView? current) => PrepareRequest(FarmMarketPresentation.From(view), stall, draft, current);
    internal static PaidQuoteView? OpenOffer(LocalWorldView view, LocalMarketStallView stall) => OpenOffer(FarmMarketPresentation.From(view), stall);
    internal static bool PinsOnOpening(LocalWorldView view, LocalMarketStallView stall, EntityId? chosen) => PinsOnOpening(FarmMarketPresentation.From(view), stall, chosen);

    internal static bool IsStallChoice(FarmSemanticAction action) =>
        action is FarmSemanticAction.TradeBuy or FarmSemanticAction.TradeSell or FarmSemanticAction.TradeOffers;

    /// <summary>The stall the panel addresses: the one available in person, else the first known stall.</summary>
    internal static LocalMarketStallView? ContextStall(FarmMarketPresentation view) =>
        view.Markets?.Stalls.FirstOrDefault(stall => stall.IsAvailableInPerson) ?? view.Markets?.Stalls.FirstOrDefault();

    /// <summary>
    /// The stall the player addresses. An explicit choice is kept by its stable market identity: it is that stall
    /// or nothing, even when it is out of reach or another stall is nearer, so no choice is ever retargeted. Without
    /// a choice the stall available in person is offered, then the first stall in identity order; that fallback is
    /// only addressed for display and is never pinned unless it is in reach (see <see cref="PinsOnOpening"/>).
    /// </summary>
    internal static LocalMarketStallView? ContextStall(FarmMarketPresentation view, EntityId? chosenMarketId) =>
        chosenMarketId is { } chosen ? view.Markets?.Stalls.FirstOrDefault(stall => stall.MarketId == chosen) : ContextStall(view);

    internal static void Add(FarmMarketPresentation view, FarmActionScope scope, FarmMarketDraft? draft,
        IDictionary<FarmSemanticAction, FarmActionOffer> offers)
    {
        if (ContextStall(view, draft?.MarketId) is not { } stall) return;
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
        if (draft?.MarketId != stall.MarketId || !draft.MatchesScope(view)) return;
        offers[FarmSemanticAction.ShowTradeLot] = Show(view, scope, stall, draft, merchant, goods, context);
        offers[FarmSemanticAction.WithdrawTradeLot] = new(FarmSemanticAction.WithdrawTradeLot, scope, "Stop showing", merchant,
            stall.MerchantActorId, FarmActionOffers.NoCost, "Your lot is no longer shown. Existing offers keep their own terms.",
            context is null && stall.OwnShowing is { IsLive: true } ? new WithdrawMarketLotDisclosureIntent(stall.MarketId) : null,
            context ?? (stall.OwnShowing is not { IsLive: true } ? "There is no active showing at this stall." : null),
            detailText: "Ends your showing. It moves no goods and does not withdraw an offer already issued.");
        offers[FarmSemanticAction.ConfirmTrade] = Confirm(view, scope, stall, draft, merchant, goods, context);
    }

    /// <summary>
    /// The player's nonbinding request from its explicit choices. It names only the stall, commodity, direction,
    /// units and, to sell, the player's own shown lot; it never names a price, a merchant lot or a quote.
    /// </summary>
    internal static FarmMarketRequest PrepareRequest(FarmMarketPresentation view, LocalMarketStallView stall, FarmMarketDraft draft,
        LocalTradeInquiryView? current)
    {
        string merchant = FarmMarketTerms.Merchant(view, stall);
        string goods = FarmMarketTerms.Goods(view, stall);
        string? blocker = !draft.MatchesScope(view) ? "The shopper changed. Reopen this stall before asking."
            : ContextBlocker(view, stall, merchant)
            ?? (draft.MarketId != stall.MarketId ? "Open this stall to choose a request."
            : current?.State == LocalTradeInquiryState.Waiting
                ? current.Inquiry.MarketId == stall.MarketId ? $"Your request is waiting for {merchant}'s answer. Cancel it to change it."
                    : "Your request at another stall is still waiting. Cancel it to ask here."
            : OpenOffer(view, stall) is not null ? $"{merchant} keeps one standing offer for you. Accept it or let it close first."
            : draft.Quantity > stall.MaximumUnitsPerOffer ? $"{merchant} offers at most {stall.MaximumUnitsPerOffer} {goods} at once."
            : draft.Direction == LocalTradeDirection.Sell ? SellBlocker(stall, draft, merchant, goods) : null);
        if (blocker is not null) return new(null, blocker);
        var inquiry = new LocalTradeInquiry(stall.MarketId, stall.Commodity, draft.Direction, draft.Quantity,
            draft.Direction == LocalTradeDirection.Sell ? draft.LotId : null);
        return new(inquiry, null);
    }

    /// <summary>The one open offer from this stall's merchant to the player, which the player may review.</summary>
    internal static PaidQuoteView? OpenOffer(FarmMarketPresentation view, LocalMarketStallView stall) => view.PaidQuotes
        .Where(quote => BelongsTo(stall, quote) && quote.Status == PaidQuoteStatus.Open)
        .OrderByDescending(quote => quote.IssuedAtStep).ThenBy(quote => quote.QuoteId).FirstOrDefault();

    /// <summary>
    /// A quote the player received from this stall: the canonical market quote record names this exact market, and
    /// the quote's merchant and goods agree with it. Merchant and commodity alone cannot tell two same-goods stalls
    /// of one merchant apart, and a quote issued outside any market belongs to no stall.
    /// </summary>
    internal static bool BelongsTo(LocalMarketStallView stall, PaidQuoteView quote) =>
        !quote.ViewerIsIssuer && quote.MarketId == stall.MarketId && quote.IssuerActorId == stall.MerchantActorId
        && quote.Definition.Equals(stall.Commodity);

    private static string? SellBlocker(LocalMarketStallView stall, FarmMarketDraft draft, string merchant, string goods) =>
        draft.LotId is null ? $"Choose one exact lot of your own {goods}."
        : stall.OwnShowing is not { } shown || shown.LotId != draft.LotId || !shown.IsLive || !shown.MatchesOwnLot
            ? $"Show that lot to {merchant} first; she can only offer for goods you showed."
        : draft.Quantity > shown.Units ? $"You showed {shown.Units} {goods}; show more before asking to sell {draft.Quantity}."
        : null;

    private static FarmActionOffer Show(FarmMarketPresentation view, FarmActionScope scope, LocalMarketStallView stall,
        FarmMarketDraft draft, string merchant, string goods, string? context)
    {
        ulong window = Math.Min(NativeTradingPolicy.ShowingWindowSteps, stall.MaximumWindowSteps);
        LocalMarketLotView? lot = stall.OwnLots.FirstOrDefault(value => value.LotId == draft.LotId);
        string? blocker = context
            ?? (draft.Direction != LocalTradeDirection.Sell ? "Showing a lot belongs to selling."
            : draft.LotId is null ? $"Choose one exact lot of your own {goods}."
            : lot is null ? "That lot is no longer usable in your pack. Choose a lot again."
            : draft.Quantity > lot.Quantity ? $"That lot holds only {FarmMarketTerms.Count(lot.Quantity, goods)}." : null);
        DiscloseMarketLotIntent? intent = blocker is null ? stall.PrepareShowing(lot!.LotId, draft.Quantity, window) : null;
        if (blocker is null && intent is null) blocker = $"{merchant} cannot be shown goods here right now.";
        string detail = $"Shows {FarmMarketTerms.Count(draft.Quantity, goods)} from that exact lot to {merchant} for "
            + $"{FarmMarketTerms.Seconds(window)} while time runs. Showing sells, reserves and moves nothing and agrees to "
            + $"no price; it only lets {merchant} answer a request to buy what you showed.";
        return new FarmActionOffer(FarmSemanticAction.ShowTradeLot, scope, $"Show to {merchant}", merchant, stall.MerchantActorId,
            FarmActionOffers.NoCost, $"You showed {FarmMarketTerms.Count(draft.Quantity, goods)} to {merchant}. Nothing was sold.",
            intent, blocker, detailText: detail);
    }

    private static FarmActionOffer Confirm(FarmMarketPresentation view, FarmActionScope scope, LocalMarketStallView stall,
        FarmMarketDraft draft, string merchant, string goods, string? context)
    {
        FarmMarketReview? review = draft.Review;
        PaidQuoteView? current = review is null ? null
            : view.PaidQuotes.FirstOrDefault(quote => quote.QuoteId == review.Quote.QuoteId);
        bool buying = review?.Quote.Side == SpotQuoteSide.IssuerSells;
        string? blocker = context ?? ConfirmBlocker(view, stall, review, current, merchant, goods);
        if (review is not null)
        {
            if (review.Scope != view.Scope) blocker ??= "The controlled character changed. Review this offer again.";
            else if (review.ActorRevision != view.ActorRevision) blocker ??= "Your character changed since review. Review this offer again.";
            scope = scope with { ExpectedActorRevision = review.ActorRevision };
        }
        EntityId? commandId = blocker is null ? NewCommandId() : null;
        WorldActionIntent? intent = blocker is null
            ? review!.Quote.PrepareAcceptance(commandId!.Value, new TrustedCommandDelivery(scope.PrincipalId, scope.SessionId), review!.Units)
            : null;
        string unit = view.CurrencyUnit;
        string label = buying ? "Confirm purchase" : "Confirm sale";
        string cost = review is null ? "Review one offer first"
            : buying ? $"{checked(review.Payment + review.Fees)} {unit}" : FarmMarketTerms.Count(review.Units, goods);
        string completion = review is null ? ""
            : buying ? $"You bought {FarmMarketTerms.Count(review.Units, goods)} from {merchant} for {checked(review.Payment + review.Fees)} {unit}."
            : $"You sold {FarmMarketTerms.Count(review.Units, goods)} to {merchant} for {review.Payment} {unit}.";
        string detail = review is null ? "Confirmation restates one reviewed offer exactly; it never asks again or picks newer terms."
            : FarmMarketTerms.Describe(view, review.Quote, review.Units, merchant, goods);
        return new FarmActionOffer(FarmSemanticAction.ConfirmTrade, scope, label, merchant, stall.MerchantActorId, cost,
            completion, intent, blocker, detailText: detail, commandId: commandId);
    }

    private static string? ConfirmBlocker(FarmMarketPresentation view, LocalMarketStallView stall, FarmMarketReview? review,
        PaidQuoteView? current, string merchant, string goods)
    {
        if (review is null) return "Review one offer first; confirming restates its exact terms.";
        if (current is null) return "This offer is no longer listed. Check its saved record.";
        if (current.Status != PaidQuoteStatus.Open)
            return $"This offer is {FarmMarketTerms.StatusWord(current.Status)}. Ask {merchant} again for new terms.";
        if (FarmMarketTerms.RemainingSteps(current, view.Markets?.AdvancingStep ?? current.IssuedAtStep) == 0)
            return "This offer’s time is up. Wait for its recorded closure before asking again.";
        if (current.QuoteHash != review.Quote.QuoteHash || !BelongsTo(stall, current))
            return "These terms are not the ones you reviewed. Review the offer again.";
        if (current.ObservedIssuerRevision is null) return $"{merchant} is not within reach to complete the trade.";
        if (current.ObservedIssuerRevision != review.Quote.ObservedIssuerRevision)
            return $"{merchant} moved since you reviewed this offer. Review it again.";
        if (current.Side == SpotQuoteSide.IssuerSells)
        {
            ulong total = checked(review.Payment + review.Fees);
            ulong balance = view.CurrencyBalance ?? 0;
            return balance < total ? $"You need {total} {view.CurrencyUnit}; you have {balance}." : null;
        }
        return stall.OwnLots.FirstOrDefault(lot => lot.LotId == current.LotId) is { } own
            && own.Revision == current.ExpectedLotRevision && own.Quantity >= review.Units
            ? null : $"The {goods} you showed changed. Show them again and ask again.";
    }

    /// <summary>
    /// Stall actions belong to ordinary unheld exploration in person. Inspecting answers and records stays
    /// possible, but requests and commitments wait for that boundary.
    /// </summary>
    private static string? ContextBlocker(FarmMarketPresentation view, LocalMarketStallView stall, string merchant) =>
        view.ContextBlocker ?? (!stall.IsAvailableInPerson ? OutOfReach(view, stall, merchant) : null);

    /// <summary>
    /// Why an addressed stall cannot trade here, without retargeting it: a stall that is in reach instead is named
    /// with the explicit Offers → Stall route, and with several stalls the route to another choice is explained.
    /// </summary>
    private static string OutOfReach(FarmMarketPresentation view, LocalMarketStallView stall, string merchant)
    {
        LocalMarketStallView[] stalls = view.Markets?.Stalls.ToArray() ?? [];
        if (stalls.FirstOrDefault(other => other.MarketId != stall.MarketId && other.IsAvailableInPerson) is { } nearby)
            return $"{merchant} is not within reach. {FarmMarketTerms.Merchant(view, nearby)}'s stall is here: "
                + "open Offers and choose it under Stall.";
        return stalls.Length > 1
            ? $"{merchant} must be within reach and able to trade in person. Walk to her stall, or open Offers and choose another under Stall."
            : $"{merchant} must be within reach and able to trade in person.";
    }

    /// <summary>
    /// Whether opening a stall's panel makes it the explicit choice: an existing choice stays, the stall in reach is
    /// chosen, and a world's only stall is chosen. With several stalls and none in reach nothing is pinned.
    /// </summary>
    internal static bool PinsOnOpening(FarmMarketPresentation view, LocalMarketStallView stall, EntityId? chosenMarketId) =>
        chosenMarketId == stall.MarketId || stall.IsAvailableInPerson || view.Markets?.Stalls.Count == 1;

    private static EntityId NewCommandId() => EntityId.FromBytes(RandomNumberGenerator.GetBytes(EntityId.ByteLength));
}
