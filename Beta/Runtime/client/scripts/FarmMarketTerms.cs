using OpenFantasy.Core.Markets;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>
/// Readable stall terms from participant-safe views only: price, units, dues, totals, affordability and the time an
/// offer stays open while the world runs. It never states a merchant's stock, funds or rule. Windows count
/// fixed-world advancing steps; the player reads them as running time, and the step basis stays a diagnostic.
/// </summary>
internal static class FarmMarketTerms
{
    internal static string Merchant(LocalWorldView view, LocalMarketStallView stall) => Merchant(FarmMarketPresentation.From(view), stall);
    internal static string Goods(LocalWorldView view, LocalMarketStallView stall) => Goods(FarmMarketPresentation.From(view), stall);
    internal static string Summary(LocalWorldView view, PaidQuoteView quote, ulong units, string goods) => Summary(FarmMarketPresentation.From(view), quote, units, goods);
    internal static string Describe(LocalWorldView view, PaidQuoteView quote, ulong units, string merchant, string goods) => Describe(FarmMarketPresentation.From(view), quote, units, merchant, goods);
    internal static string Restate(LocalWorldView view, PaidQuoteView quote, ulong units, string merchant, string goods) => Restate(FarmMarketPresentation.From(view), quote, units, merchant, goods);
    internal static string Outcome(LocalWorldView view, PaidQuoteView quote, string merchant, string goods) => Outcome(FarmMarketPresentation.From(view), quote, merchant, goods);

    internal static string Merchant(FarmMarketPresentation view, LocalMarketStallView stall) =>
        view.Name(stall.MerchantActorId);

    /// <summary>The stall's public goods name, such as "berries".</summary>
    internal static string Goods(FarmMarketPresentation view, LocalMarketStallView stall) => stall.CommodityName.Replace('_', ' ');

    /// <summary>A count with goods that agree with it: "1 berry", "4 berries".</summary>
    internal static string Count(ulong units, string goods) => $"{units} {(units == 1 ? Singular(goods) : goods)}";

    /// <summary>
    /// The English singular of a plural source name ("berries" → "berry", "apples" → "apple"); a name without a
    /// recognised plural ending is already a mass or singular noun and stays unchanged.
    /// </summary>
    internal static string Singular(string goods)
    {
        if (goods.Length > 3 && goods.EndsWith("ies", StringComparison.Ordinal)) return goods[..^3] + "y";
        if (goods.Length > 1 && goods.EndsWith('s') && !goods.EndsWith("ss", StringComparison.Ordinal)) return goods[..^1];
        return goods;
    }

    /// <summary>The running world time that advancing steps take at the local session's fixed cutoff.</summary>
    internal static string Seconds(ulong steps)
    {
        double seconds = steps * LocalWorldSession.StepInterval.TotalSeconds;
        return seconds >= 90 ? $"about {seconds / 60:0.#} min" : $"about {Math.Max(1, Math.Round(seconds)):0} s";
    }

    /// <summary>Steps left before the offer stops being acceptable, or zero once it is due.</summary>
    internal static ulong RemainingSteps(PaidQuoteView quote, ulong advancingStep) =>
        quote.ExpiresAtStep > advancingStep ? quote.ExpiresAtStep - advancingStep : 0;

    /// <summary>How long the offer stays open in player terms, including what pausing does to that clock.</summary>
    internal static string Window(PaidQuoteView quote, ulong advancingStep, bool shared = false)
    {
        ulong remaining = RemainingSteps(quote, advancingStep);
        return remaining == 0 ? "This offer's time is up."
            : $"Open for {Seconds(remaining)} more while time runs; " + (shared
                ? "other players may continue while your input is paused." : "pausing stops this clock.");
    }

    /// <summary>Short card text: what changes hands and the total, with a shortfall when the purse cannot pay.</summary>
    internal static string Summary(FarmMarketPresentation view, PaidQuoteView quote, ulong units, string goods)
    {
        string unit = view.CurrencyUnit;
        if (quote.Side != SpotQuoteSide.IssuerSells)
            return $"Sell {Count(units, goods)} · receive {checked(quote.UnitPrice * units)} {unit}";
        ulong total = quote.TotalFor(units);
        ulong purse = view.CurrencyBalance ?? 0;
        return $"Buy {Count(units, goods)} · pay {total} {unit}" + (purse < total ? $" · you have {purse}" : "");
    }

    /// <summary>The precise inspection shared by hover, focus and the always-visible inspector.</summary>
    internal static string Describe(FarmMarketPresentation view, PaidQuoteView quote, ulong units, string merchant, string goods)
    {
        string unit = view.CurrencyUnit;
        ulong step = view.Markets?.AdvancingStep ?? quote.IssuedAtStep;
        bool buying = quote.Side == SpotQuoteSide.IssuerSells;
        string fee = quote.UnitFees == 0 ? "no market due"
            : buying ? $"market due {quote.UnitFees} {unit} each, paid by you"
            : $"market due {quote.UnitFees} {unit} each, paid by {merchant}";
        string trade = buying
            ? $"Buy {Count(units, goods)} from {merchant} at {quote.UnitPrice} {unit} each, {fee}: total {quote.TotalFor(units)} {unit}."
            : $"Sell {Count(units, goods)} to {merchant} at {quote.UnitPrice} {unit} each, {fee}: you receive {checked(quote.UnitPrice * units)} {unit}.";
        string state = quote.Status == PaidQuoteStatus.Open ? Window(quote, step, view.SharedWorld) : $"This offer is {StatusWord(quote.Status)}.";
        return $"{trade} {merchant} offers up to {Count(quote.QuantityAllowance, goods)}. {state}{Affordability(view, quote, units)} "
            + $"{merchant} keeps one standing offer for you; if you leave it, it closes on its own.";
    }

    /// <summary>
    /// The confirmation card: exactly what changes hands for the reviewed units, the time still left and the purse
    /// afterwards, with the offer's current state rather than the state it had when reviewed.
    /// </summary>
    internal static string Restate(FarmMarketPresentation view, PaidQuoteView current, ulong units, string merchant, string goods)
    {
        string unit = view.CurrencyUnit;
        ulong purse = view.CurrencyBalance ?? 0;
        bool buying = current.Side == SpotQuoteSide.IssuerSells;
        ulong total = buying ? current.TotalFor(units) : checked(current.UnitPrice * units);
        string fee = current.UnitFees == 0 ? "no market due" : buying ? $"{current.UnitFees} {unit} due each" : $"{merchant} pays the due";
        string trade = buying
            ? $"Buy {Count(units, goods)} from {merchant} for {total} {unit} ({current.UnitPrice} each, {fee})."
            : $"Sell {Count(units, goods)} to {merchant} for {total} {unit} ({current.UnitPrice} each, {fee}).";
        if (current.Status != PaidQuoteStatus.Open) return $"{trade} This offer is {StatusWord(current.Status)}.";
        string after = buying
            ? purse >= total ? $" Purse after: {purse - total} {unit}." : $" Your purse holds {purse} {unit}."
            : $" Purse after: {checked(purse + total)} {unit}.";
        return trade + " " + Window(current, view.Markets?.AdvancingStep ?? current.IssuedAtStep, view.SharedWorld) + after;
    }

    /// <summary>A recorded outcome line for the history list and its explicit saved-record inspection.</summary>
    internal static string Outcome(FarmMarketPresentation view, PaidQuoteView quote, string merchant, string goods)
    {
        string unit = view.CurrencyUnit;
        string direction = quote.Side == SpotQuoteSide.IssuerSells ? "Buy from" : "Sell to";
        string result = quote.Status == PaidQuoteStatus.Filled && quote.FilledQuantity is { } filled
            ? quote.Side == SpotQuoteSide.IssuerSells
                ? $"bought {Count(filled, goods)} for {quote.FilledPayment + quote.FilledFees} {unit}"
                : $"sold {Count(filled, goods)} for {quote.FilledPayment} {unit}"
            : StatusWord(quote.Status);
        return $"{direction} {merchant} · {Count(quote.QuantityAllowance, goods)} at {quote.UnitPrice} {unit} · {result}";
    }

    /// <summary>Where a request stands, and what the player can do next, without implying stock or funds.</summary>
    internal static string Request(LocalTradeInquiryView? inquiry, ulong advancingStep, bool timeRunning, string merchant, string goods, bool shared = false)
    {
        if (inquiry is null) return $"Choose units and ask {merchant}. Asking binds nothing and reserves nothing.";
        string asked = $"{(inquiry.Inquiry.Direction == LocalTradeDirection.Buy ? "buy" : "sell")} {Count(inquiry.Inquiry.Quantity, goods)}";
        return inquiry.State switch
        {
            LocalTradeInquiryState.Waiting => timeRunning || shared
                ? $"Asked to {asked}. {merchant} answers shortly while time runs; the request lapses in "
                    + $"{Seconds(inquiry.ExpiresAtStep > advancingStep ? inquiry.ExpiresAtStep - advancingStep : 0)}."
                : $"Asked to {asked}. World time is paused: {merchant} answers only while time runs. Resume to hear her answer, or cancel.",
            LocalTradeInquiryState.Offered => $"Asked to {asked}. {merchant} offered terms. Review them before confirming.",
            LocalTradeInquiryState.Declined => $"Asked to {asked}. {merchant} offered no terms for that request.",
            LocalTradeInquiryState.Unavailable => $"Asked to {asked}. {merchant} could not answer where you stand.",
            LocalTradeInquiryState.Expired => $"Asked to {asked}. The request lapsed before an answer.",
            _ => $"Asked to {asked}. You cancelled the request."
        };
    }

    /// <summary>Optional diagnostics for hover: the fixed-world step basis that docs describe.</summary>
    internal static string Diagnostics(ulong advancingStep) =>
        $"Diagnostics: advancing step {advancingStep:N0}. Windows count advancing steps of about "
        + $"{LocalWorldSession.StepInterval.TotalMilliseconds:0} ms each while time runs.";

    /// <summary>A plain word for a closed offer's state.</summary>
    internal static string StatusWord(PaidQuoteStatus status) => status switch
    {
        PaidQuoteStatus.Open => "open",
        PaidQuoteStatus.Filled => "already filled",
        PaidQuoteStatus.Superseded => "replaced by a newer offer",
        PaidQuoteStatus.Withdrawn => "withdrawn",
        _ => "expired"
    };

    /// <summary>The player's purse beside a purchase total, naming a shortfall plainly.</summary>
    private static string Affordability(FarmMarketPresentation view, PaidQuoteView quote, ulong units)
    {
        if (view.CurrencyBalance is not { } balance) return "";
        string purse = $" Your purse: {balance} {view.CurrencyUnit}.";
        if (quote.Side != SpotQuoteSide.IssuerSells || balance >= quote.TotalFor(units)) return purse;
        return purse + $" That is {quote.TotalFor(units) - balance} {view.CurrencyUnit} short of this total.";
    }
}
