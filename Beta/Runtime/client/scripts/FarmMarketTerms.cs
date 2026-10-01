using OpenFantasy.Core.Markets;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>
/// Readable stall terms from participant-safe views only: price, units, dues, totals, age and remaining window in
/// fixed-world steps with their nominal local duration. It never states a merchant's stock, funds or rule.
/// </summary>
internal static class FarmMarketTerms
{
    internal static string Merchant(LocalWorldView view, LocalMarketStallView stall) =>
        MaterialProposalActionOffers.ActorName(view, stall.MerchantActorId);

    /// <summary>The stall's public goods name, such as "berries".</summary>
    internal static string Goods(LocalWorldView view, LocalMarketStallView stall) => stall.CommodityName.Replace('_', ' ');

    /// <summary>The nominal duration of advancing steps at the local session's fixed cutoff, while time runs.</summary>
    internal static string Seconds(ulong steps)
    {
        double seconds = steps * LocalWorldSession.StepInterval.TotalSeconds;
        return seconds >= 120 ? $"about {seconds / 60:0.#} min" : $"about {seconds:0} s";
    }

    /// <summary>Steps left before the offer stops being acceptable, or zero once it is due.</summary>
    internal static ulong RemainingSteps(PaidQuoteView quote, ulong advancingStep) =>
        quote.ExpiresAtStep > advancingStep ? quote.ExpiresAtStep - advancingStep : 0;

    /// <summary>Short card text: icon-side summary of what changes hands and the total.</summary>
    internal static string Summary(LocalWorldView view, PaidQuoteView quote, ulong units, string goods)
    {
        string unit = view.Progression?.CurrencyUnit ?? "coins";
        return quote.Side == SpotQuoteSide.IssuerSells
            ? $"Buy {units} {goods} · pay {quote.TotalFor(units)} {unit}"
            : $"Sell {units} {goods} · receive {checked(quote.UnitPrice * units)} {unit}";
    }

    /// <summary>The precise inspection shared by hover, focus and the explicit review card.</summary>
    internal static string Describe(LocalWorldView view, PaidQuoteView quote, ulong units, string merchant, string goods)
    {
        string unit = view.Progression?.CurrencyUnit ?? "coins";
        ulong step = view.Markets?.AdvancingStep ?? quote.IssuedAtStep;
        ulong age = step > quote.IssuedAtStep ? step - quote.IssuedAtStep : 0;
        ulong remaining = RemainingSteps(quote, step);
        bool buying = quote.Side == SpotQuoteSide.IssuerSells;
        string fee = quote.UnitFees == 0 ? "no market due"
            : buying ? $"market due {quote.UnitFees} {unit} each, paid by you"
            : $"market due {quote.UnitFees} {unit} each, paid by {merchant}";
        string trade = buying
            ? $"Buy {units} {goods} from {merchant} at {quote.UnitPrice} {unit} each, {fee}: total {quote.TotalFor(units)} {unit}."
            : $"Sell {units} {goods} to {merchant} at {quote.UnitPrice} {unit} each, {fee}: you receive {checked(quote.UnitPrice * units)} {unit}.";
        string window = remaining == 0 ? "This offer's window has closed."
            : $"Offered {Steps(age)} ago; acceptable for {remaining:N0} more world steps ({Seconds(remaining)} while time advances).";
        string balance = view.Progression is { } progression ? $" Your purse: {progression.CurrencyBalance} {unit}." : "";
        return $"{trade} Offer limit {quote.QuantityAllowance} {goods}. {window}{balance} {merchant} keeps one standing offer for you; "
            + "ignoring it lets it close on its own. Status: " + quote.Status + ".";
    }

    /// <summary>
    /// The confirmation card: exactly what changes hands for the reviewed units, the window still left and the
    /// purse afterwards, with the offer's current status rather than the status it had when reviewed.
    /// </summary>
    internal static string Restate(LocalWorldView view, PaidQuoteView current, ulong units, string merchant, string goods)
    {
        string unit = view.Progression?.CurrencyUnit ?? "coins";
        ulong purse = view.Progression?.CurrencyBalance ?? 0;
        ulong remaining = RemainingSteps(current, view.Markets?.AdvancingStep ?? current.IssuedAtStep);
        bool buying = current.Side == SpotQuoteSide.IssuerSells;
        ulong total = buying ? current.TotalFor(units) : checked(current.UnitPrice * units);
        string fee = current.UnitFees == 0 ? "no market due" : buying ? $"{current.UnitFees} {unit} due each" : $"{merchant} pays the due";
        string trade = buying
            ? $"Buy {units} {goods} from {merchant} for {total} {unit} ({current.UnitPrice} each, {fee})."
            : $"Sell {units} {goods} to {merchant} for {total} {unit} ({current.UnitPrice} each, {fee}).";
        string window = remaining == 0 ? " The window has closed."
            : $" Acceptable for {remaining:N0} more world steps ({Seconds(remaining)} while time advances).";
        string after = current.Status != PaidQuoteStatus.Open ? ""
            : buying ? purse >= total ? $" Purse after: {purse - total} {unit}." : ""
            : $" Purse after: {checked(purse + total)} {unit}.";
        return trade + window + after + " Status: " + current.Status + ".";
    }

    private static string Steps(ulong steps) => steps == 1 ? "1 step" : $"{steps:N0} steps";

    /// <summary>A recorded outcome line for the history list and its explicit saved-record inspection.</summary>
    internal static string Outcome(LocalWorldView view, PaidQuoteView quote, string merchant, string goods)
    {
        string unit = view.Progression?.CurrencyUnit ?? "coins";
        string direction = quote.Side == SpotQuoteSide.IssuerSells ? "Buy from" : "Sell to";
        string result = quote.Status == PaidQuoteStatus.Filled && quote.FilledQuantity is { } filled
            ? quote.Side == SpotQuoteSide.IssuerSells
                ? $"bought {filled} {goods} for {quote.FilledPayment + quote.FilledFees} {unit}"
                : $"sold {filled} {goods} for {quote.FilledPayment} {unit}"
            : quote.Status.ToString().ToLowerInvariant();
        return $"{direction} {merchant} · {quote.QuantityAllowance} {goods} at {quote.UnitPrice} {unit} · {result}";
    }

    /// <summary>Where a request stands, and what the player can do next, without implying stock or funds.</summary>
    internal static string Request(LocalTradeInquiryView? inquiry, ulong advancingStep, bool timeRunning, string merchant)
    {
        if (inquiry is null) return $"Choose units and ask {merchant}. Asking binds nothing and reserves nothing.";
        string asked = $"{(inquiry.Inquiry.Direction == LocalTradeDirection.Buy ? "Buy" : "Sell")} {inquiry.Inquiry.Quantity}";
        return inquiry.State switch
        {
            LocalTradeInquiryState.Waiting => timeRunning
                ? $"Asked: {asked}. {merchant} answers at the next world step while time advances; the request lapses in "
                    + $"{(inquiry.ExpiresAtStep > advancingStep ? inquiry.ExpiresAtStep - advancingStep : 0):N0} steps."
                : $"Asked: {asked}. World time is paused: {merchant} answers only while time advances. Resume to hear her answer, or cancel.",
            LocalTradeInquiryState.Offered => $"Asked: {asked}. {merchant} offered terms. Review them before confirming.",
            LocalTradeInquiryState.Declined => $"Asked: {asked}. {merchant} offered no terms for that request.",
            LocalTradeInquiryState.Unavailable => $"Asked: {asked}. {merchant} could not answer where you stand.",
            LocalTradeInquiryState.Expired => $"Asked: {asked}. The request lapsed before an answer.",
            _ => $"Asked: {asked}. You cancelled the request."
        };
    }
}
