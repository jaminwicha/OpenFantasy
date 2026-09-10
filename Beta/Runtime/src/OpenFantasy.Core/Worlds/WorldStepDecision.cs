using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Worlds;

public enum WorldCommittedIdentityKind { CommitCause = 1, Command = 2, Result = 3, SpawnedEntity = 4 }

public sealed record WorldCommittedIdentity(EntityId Identity, WorldCommittedIdentityKind Kind);

/// <summary>A private prepared transition. Only the durable world owner may publish its outcomes.</summary>
public sealed class WorldStepDecision
{
    internal WorldStepDecision(WorldSnapshot candidate, IEnumerable<WorldActionReceipt> receipts,
        IEnumerable<WorldCommittedIdentity> identities)
    {
        Candidate = candidate;
        Receipts = Array.AsReadOnly(receipts.OrderBy(receipt => receipt.CommandId).ToArray());
        Identities = Array.AsReadOnly(identities.OrderBy(identity => identity.Identity).ToArray());
    }

    public WorldSnapshot Candidate { get; }
    public ReadOnlyCollection<WorldActionReceipt> Receipts { get; }
    public ReadOnlyCollection<WorldCommittedIdentity> Identities { get; }
}
