using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Authority.ConsentedCustody;

/// <summary>
/// Private authority output: one complete candidate or the untouched original, never a partial
/// state. Only the durable caller may commit the candidate and publish the receipt.
/// </summary>
public sealed class ConsentedTransferDecision
{
    internal ConsentedTransferDecision(CountedAuthorityState original, CountedAuthorityState candidate,
        ConsentedTransferReceipt receipt)
    {
        BaseStateHash = original.ContentHash;
        Candidate = candidate;
        Receipt = receipt;
    }

    public ContentHash BaseStateHash { get; }
    public CountedAuthorityState Candidate { get; }
    public ConsentedTransferReceipt Receipt { get; }
    public bool IsApplied => Receipt.Status == CommandReceiptStatus.Applied;
    public EntityId? MovedLotId => Receipt.MovedLotId;

    /// <summary>
    /// Returns the candidate only for the exact complete state that was decided, including its
    /// controls and access policies. Any other current state makes this plan stale.
    /// </summary>
    public CountedAuthorityState ApplyTo(CountedAuthorityState current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.ContentHash != BaseStateHash)
        {
            throw new CustodyConflictException(CustodyConflict.StalePlan);
        }

        return Candidate;
    }
}
