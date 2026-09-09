namespace OpenFantasy.Core.Observations;

/// <summary>
/// One actor's complete current resource-knowledge view at a pinned authority head. Historical
/// causes and all other knowledge domains remain separately retained by their actual owners.
/// </summary>
public sealed class ResourceKnowledgeSnapshot
{
    public const int MaximumEntries = 1024;

    public ResourceKnowledgeSnapshot(ObservationScope scope, EntityId disclosureEpoch,
        ulong disclosureRevision, ulong pinnedCommitRevision,
        IEnumerable<CommittedResourceObservation> entries)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(entries);
        var ordered = new List<CommittedResourceObservation>();
        var identities = new HashSet<EntityId>();
        var revisions = new HashSet<ulong>();
        var privateSources = new HashSet<EntityId>();
        var disclosedIdentities = new HashSet<EntityId>
        {
            scope.WorldId, scope.BranchId, scope.ActorId, disclosureEpoch
        };
        foreach (CommittedResourceObservation entry in entries)
        {
            if (ordered.Count == MaximumEntries)
            {
                throw new ArgumentException("The actor knowledge snapshot exceeds its entry limit.", nameof(entries));
            }

            ArgumentNullException.ThrowIfNull(entry);
            if (entry.Scope != scope || entry.RecordedCommitSequence > pinnedCommitRevision
                || entry.Observation.DisclosureRevision > disclosureRevision
                || !identities.Add(entry.Observation.DisclosureId)
                || !revisions.Add(entry.Observation.DisclosureRevision))
            {
                throw new ArgumentException("Knowledge entries do not match the declared snapshot.", nameof(entries));
            }

            privateSources.Add(entry.PrivateSourceEventId);
            disclosedIdentities.Add(entry.Observation.DisclosureId);
            disclosedIdentities.Add(entry.Observation.KnownSubjectId);
            disclosedIdentities.Add(entry.Observation.KnownMaterial.DefinitionId);
            if (entry.Observation.KnownReporterId is EntityId reporter)
            {
                disclosedIdentities.Add(reporter);
            }

            ordered.Add(entry);
        }

        if (privateSources.Overlaps(disclosedIdentities))
        {
            throw new ArgumentException("Private provenance aliases a disclosed identity.", nameof(entries));
        }

        ordered.Sort((left, right) => left.Observation.DisclosureRevision.CompareTo(right.Observation.DisclosureRevision));
        Scope = scope;
        DisclosureEpoch = disclosureEpoch;
        DisclosureRevision = disclosureRevision;
        PinnedCommitRevision = pinnedCommitRevision;
        Entries = Array.AsReadOnly(ordered.ToArray());
    }

    public ObservationScope Scope { get; }
    public EntityId DisclosureEpoch { get; }
    public ulong DisclosureRevision { get; }
    public ulong PinnedCommitRevision { get; }
    internal IReadOnlyList<CommittedResourceObservation> Entries { get; }

    internal bool ContainsPrivateSource(EntityId identity) =>
        Entries.Any(entry => entry.PrivateSourceEventId == identity);
}
