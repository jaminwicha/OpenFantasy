namespace OpenFantasy.Core.Cognition;

/// <summary>A known gathering source: where the actor last observed it and the evidence for that.</summary>
public sealed record KnownSource(EntityId SourceId, EntityId EvidenceId, WorldInstant ObservedAt, bool BelievedDepleted);

/// <summary>
/// Propositions derived from one actor's evidence. They can be stale: a fact that changed elsewhere does not change
/// a belief until the actor acquires new evidence. "No delivery received" is not "nobody delivered"; a source not
/// yet observed is unknown, not absent.
/// </summary>
public sealed class CognitionBeliefs
{
    private readonly Dictionary<EntityId, EntityId> _deliveries;
    private readonly Dictionary<EntityId, KnownSource> _sources;

    private CognitionBeliefs(Dictionary<EntityId, EntityId> deliveries, Dictionary<EntityId, KnownSource> sources, ulong gathered)
    {
        _deliveries = deliveries;
        _sources = sources;
        GatheredQuantity = gathered;
    }

    /// <summary>Units the actor's own committed gathering attempts actually yielded.</summary>
    public ulong GatheredQuantity { get; }

    /// <summary>Known sources in canonical identity order.</summary>
    public IReadOnlyList<KnownSource> Sources => _sources.Values.OrderBy(source => source.SourceId).ToList().AsReadOnly();

    /// <summary>The evidence that a quest's qualifying delivery reached this actor, if it did.</summary>
    public EntityId? DeliveryEvidence(EntityId questId) => _deliveries.TryGetValue(questId, out EntityId evidence) ? evidence : null;

    public static CognitionBeliefs From(IEnumerable<CognitionEvidence> evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var deliveries = new Dictionary<EntityId, EntityId>();
        var sources = new Dictionary<EntityId, KnownSource>();
        ulong gathered = 0;
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
                    gathered = checked(gathered + entry.Quantity);
                    if (sources.TryGetValue(entry.SubjectId, out KnownSource? known) && entry.Quantity == 0)
                        sources[entry.SubjectId] = known with { BelievedDepleted = true };
                    break;
            }
        }
        return new CognitionBeliefs(deliveries, sources, gathered);
    }
}
