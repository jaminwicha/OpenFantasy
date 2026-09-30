using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Markets;

/// <summary>
/// The counterparty's submitted acceptance of one whole quote: the exact quote bytes by hash, the
/// selected quantity and both money totals it agrees to. Claims prove nothing; the acceptance
/// identity is a permanent command identity spent by exactly one settled fill.
/// </summary>
public sealed class SpotAcceptance
{
    public SpotAcceptance(EntityId acceptanceId, EntityId principalId, EntityId originSessionId, EntityId quoteId,
        ContentHash quoteHash, ulong quantity, ulong payment, ulong fees)
    {
        ArgumentNullException.ThrowIfNull(quoteHash);
        ArgumentOutOfRangeException.ThrowIfZero(quantity);
        if (acceptanceId == quoteId)
        {
            throw new ArgumentException("An acceptance and its quote are different permanent identities.",
                nameof(acceptanceId));
        }

        AcceptanceId = acceptanceId;
        PrincipalId = principalId;
        OriginSessionId = originSessionId;
        QuoteId = quoteId;
        QuoteHash = quoteHash;
        Quantity = quantity;
        Payment = payment;
        Fees = fees;
    }

    public EntityId AcceptanceId { get; }
    public EntityId PrincipalId { get; }
    public EntityId OriginSessionId { get; }
    public EntityId QuoteId { get; }
    public ContentHash QuoteHash { get; }
    public ulong Quantity { get; }

    /// <summary>Whole units the buyer pays the seller.</summary>
    public ulong Payment { get; }

    /// <summary>Whole units the buyer pays the quote's named fee payees together.</summary>
    public ulong Fees { get; }
    public ContentHash ContentHash => ContentHash.Compute(SpotExchangeCodecV1.Encode(this));

    /// <summary>Binds the whole quote, the allowance and both exact money totals; no repricing or retargeting.</summary>
    public bool Binds(SpotQuote quote)
    {
        ArgumentNullException.ThrowIfNull(quote);
        return QuoteId == quote.QuoteId && QuoteHash == quote.ContentHash && Quantity <= quote.QuantityAllowance
            && Payment == quote.PaymentFor(Quantity) && Fees == quote.FeesFor(Quantity);
    }
}
