using OpenFantasy.Core.Markets;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// One submission of the paid spot-exchange interpretation. Each is a separate command with its own
/// identity and receipt; no earlier profile interprets it, and neither is a gift, reward or care act.
/// </summary>
public abstract record PaidExchangeIntent : WorldActionIntent;

/// <summary>
/// The issuer's fixed quote to one named counterparty. The quote identity derives from the command
/// identity and the issuer's claims equal the command's; issuance reserves no stock and moves no money.
/// </summary>
public sealed record IssueSpotQuoteIntent : PaidExchangeIntent
{
    public IssueSpotQuoteIntent(SpotQuote quote)
    {
        ArgumentNullException.ThrowIfNull(quote);
        Quote = quote;
    }

    public SpotQuote Quote { get; }
}

/// <summary>
/// The counterparty's own acceptance of one open quote under the identity of its command. It restates
/// the exact quote hash, quantity and both money totals and pins the issuer's observed presence revision.
/// </summary>
public sealed record AcceptSpotQuoteIntent : PaidExchangeIntent
{
    public AcceptSpotQuoteIntent(SpotAcceptance acceptance, ulong expectedIssuerRevision)
    {
        ArgumentNullException.ThrowIfNull(acceptance);
        Acceptance = acceptance;
        ExpectedIssuerRevision = expectedIssuerRevision;
    }

    public SpotAcceptance Acceptance { get; }
    public ulong ExpectedIssuerRevision { get; }
}

/// <summary>
/// The issuer's own withdrawal of one of its open quotes, for example when the pinned stock went stale or
/// protected, or the offer is simply unwanted. It releases the quote's open slot; it pays nothing, moves
/// no goods, advances no market revision and uses no work opportunity.
/// </summary>
public sealed record WithdrawSpotQuoteIntent(EntityId QuoteId) : PaidExchangeIntent;
