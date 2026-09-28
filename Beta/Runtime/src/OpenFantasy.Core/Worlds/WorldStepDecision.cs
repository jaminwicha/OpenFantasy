using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Worlds;

public enum WorldCommittedIdentityKind { CommitCause = 1, Command = 2, Result = 3, SpawnedEntity = 4, AdmittedDefinition = 5 }

public sealed record WorldCommittedIdentity(EntityId Identity, WorldCommittedIdentityKind Kind);

/// <summary>A private prepared transition. Only the durable world owner may publish its outcomes.</summary>
public sealed class WorldStepDecision
{
    private static readonly ReadOnlyCollection<WorldMaterialActionEvidence> NoMaterialEvidence =
        Array.AsReadOnly(Array.Empty<WorldMaterialActionEvidence>());

    internal WorldStepDecision(WorldSnapshot candidate, IEnumerable<WorldActionReceipt> receipts,
        IEnumerable<WorldCommittedIdentity> identities, WorldResourceArchiveSegment? sealedResourceSegment = null,
        IEnumerable<WorldMaterialActionEvidence>? materialEvidence = null)
    {
        Candidate = candidate;
        Receipts = Array.AsReadOnly(receipts.OrderBy(receipt => receipt.CommandId).ToArray());
        Identities = Array.AsReadOnly(identities.OrderBy(identity => identity.Identity).ToArray());
        SealedResourceSegment = sealedResourceSegment;
        MaterialEvidence = SnapshotEvidence(materialEvidence);
    }

    private static ReadOnlyCollection<WorldMaterialActionEvidence> SnapshotEvidence(
        IEnumerable<WorldMaterialActionEvidence>? materialEvidence)
    {
        if (materialEvidence is null or ICollection<WorldMaterialActionEvidence> { Count: 0 })
        {
            return NoMaterialEvidence;
        }
        WorldMaterialActionEvidence[] captured = materialEvidence.ToArray();
        for (int index = 1; index < captured.Length; index++)
        {
            if (captured[index].ActionOrdinal <= captured[index - 1].ActionOrdinal)
            {
                throw new ArgumentException("Material evidence must follow the selected batch order.", nameof(materialEvidence));
            }
        }
        return Array.AsReadOnly(captured);
    }

    public WorldSnapshot Candidate { get; }
    public ReadOnlyCollection<WorldActionReceipt> Receipts { get; }
    public ReadOnlyCollection<WorldCommittedIdentity> Identities { get; }
    public WorldResourceArchiveSegment? SealedResourceSegment { get; }

    /// <summary>
    /// Bounded per-action observations captured only by the explicit material-evidence path, in the
    /// selected batch order. Ordinary decisions carry none. The entries are structural observations of
    /// this private transition; they are not published, persisted or authenticated by this decision.
    /// </summary>
    public ReadOnlyCollection<WorldMaterialActionEvidence> MaterialEvidence { get; }
}
