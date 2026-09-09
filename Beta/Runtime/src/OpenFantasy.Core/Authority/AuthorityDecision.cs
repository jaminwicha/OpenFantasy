using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Authority;

/// <summary>Private authority output; only the durable caller may commit and publish its receipt.</summary>
public sealed class AuthorityDecision
{
    internal AuthorityDecision(CountedAuthorityState original, CountedAuthorityState candidate,
        CommandReceipt receipt, EntityId? movedLotId)
    {
        BaseStateHash = original.ContentHash;
        Candidate = candidate;
        Receipt = receipt;
        MovedLotId = movedLotId;
    }

    public ContentHash BaseStateHash { get; }
    public CountedAuthorityState Candidate { get; }
    public CommandReceipt Receipt { get; }
    public EntityId? MovedLotId { get; }
}
