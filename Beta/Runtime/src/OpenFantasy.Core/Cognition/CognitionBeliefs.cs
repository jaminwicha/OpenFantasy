namespace OpenFantasy.Core.Cognition;

/// <summary>
/// A known gathering source: the evidence of the actor's latest observation, when it was made, whether the source is
/// believed depleted, and what the actor's own latest gathering there yielded, if it has gathered there.
/// </summary>
public sealed record KnownSource(EntityId SourceId, EntityId EvidenceId, WorldInstant ObservedAt, bool BelievedDepleted,
    ulong? LatestYield = null);

/// <summary>
/// Propositions derived from one actor's evidence. They can be stale: a fact that changed elsewhere does not change
/// a belief until the actor acquires new evidence. "No delivery received" is not "nobody delivered"; a source not
/// yet observed is unknown, not absent.
/// </summary>
public sealed class CognitionBeliefs
{
    private readonly Dictionary<EntityId, EntityId> _deliveries;
    private readonly Dictionary<EntityId, KnownSource> _sources;

    private CognitionBeliefs(Dictionary<EntityId, EntityId> deliveries, Dictionary<EntityId, KnownSource> sources)
    {
        _deliveries = deliveries;
        _sources = sources;
    }

    /// <summary>Known sources in canonical identity order.</summary>
    public IReadOnlyList<KnownSource> Sources => _sources.Values.OrderBy(source => source.SourceId).ToList().AsReadOnly();

    /// <summary>The evidence that a quest's qualifying delivery reached this actor, if it did.</summary>
    public EntityId? DeliveryEvidence(EntityId questId) => _deliveries.TryGetValue(questId, out EntityId evidence) ? evidence : null;

    public static CognitionBeliefs From(IEnumerable<CognitionEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var deliveries = new Dictionary<EntityId, EntityId>();
        var sources = new Dictionary<EntityId, KnownSource>();
        var yields = new Dictionary<EntityId, ulong>();
        foreach (CognitionEvidence entry in evidence.OrderBy(entry => entry, Comparer<CognitionEvidence>.Create(ActorCognition.Order)))
        {
            switch (entry.Kind)
            {
                case EvidenceKind.QuestDeliveryReceived:
                    deliveries.TryAdd(entry.SubjectId, entry.EvidenceId);
                    break;
                case EvidenceKind.SourceObserved:
                    // A newer observation shows the source as it is now, replacing an older depletion belief.
                    sources[entry.SubjectId] = new KnownSource(entry.SubjectId, entry.EvidenceId, entry.AcquiredAt, entry.Quantity == 0);
                    break;
                case EvidenceKind.GatherOutcome:
                    // Older outcomes are replaced, so the held one is the actor's latest gathering at that source.
                    yields[entry.SubjectId] = entry.Quantity;
                    if (sources.TryGetValue(entry.SubjectId, out KnownSource? known) && entry.Quantity == 0)
                        sources[entry.SubjectId] = known with { BelievedDepleted = true };
                    break;
            }
        }
        foreach ((EntityId sourceId, ulong yield) in yields)
        {
            if (sources.TryGetValue(sourceId, out KnownSource? known)) sources[sourceId] = known with { LatestYield = yield };
        }
        return new CognitionBeliefs(deliveries, sources);
    }
}
