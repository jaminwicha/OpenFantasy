namespace OpenFantasy.Core.Cognition;

/// <summary>What kind of fact one actor acquired. Each kind names an acquisition path, not a world truth.</summary>
public enum EvidenceKind
{
    /// <summary>The knower received an item lot through a committed transfer from a named counterpart.</summary>
    ItemReceived = 1,
    /// <summary>The knower, as a quest giver, received the committed qualifying delivery for that quest.</summary>
    QuestDeliveryReceived = 2,
    /// <summary>The knower's own committed gathering attempt and its actual yield.</summary>
    GatherOutcome = 3,
    /// <summary>The knower directly observed a gathering source at a place under the admitted perception rule.</summary>
    SourceObserved = 4,
    /// <summary>The knower's own committed work on a project.</summary>
    ProjectWorked = 5,
}

/// <summary>
/// One fact an actor actually acquired from a committed outcome: from which command, involving whom, about which
/// subject, in what quantity and when. It never asserts anything the knower did not receive. A failed or refused
/// command produces no evidence, and nothing here is visible to another actor unless that actor acquires its own.
/// </summary>
public sealed class CognitionEvidence
{
    /// <summary>Reserved spawn-ordinal range for evidence identities, distinct from other derivations of the same cause.</summary>
    public const ulong IdentityOrdinalBase = 0x4E50_4330_0000_0000UL;

    public CognitionEvidence(EntityId evidenceId, EntityId knowerId, EvidenceKind kind, EntityId sourceCommandId,
        EntityId? counterpartActorId, EntityId subjectId, ulong quantity, WorldInstant acquiredAt)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (evidenceId == knowerId || evidenceId == sourceCommandId || evidenceId == subjectId)
            throw new ArgumentException("Evidence needs its own identity, distinct from its knower, cause and subject.");
        if (counterpartActorId == knowerId)
            throw new ArgumentException("A counterpart is someone other than the knower.");
        bool needsCounterpart = kind is EvidenceKind.ItemReceived or EvidenceKind.QuestDeliveryReceived;
        if (needsCounterpart != counterpartActorId.HasValue)
            throw new ArgumentException("Only received items and deliveries name a counterpart.");
        if (kind is EvidenceKind.ItemReceived or EvidenceKind.QuestDeliveryReceived && quantity == 0)
            throw new ArgumentException("A received item or delivery has a positive quantity.");
        EvidenceId = evidenceId;
        KnowerId = knowerId;
        Kind = kind;
        SourceCommandId = sourceCommandId;
        CounterpartActorId = counterpartActorId;
        SubjectId = subjectId;
        Quantity = quantity;
        AcquiredAt = acquiredAt;
    }

    public EntityId EvidenceId { get; }
    public EntityId KnowerId { get; }
    public EvidenceKind Kind { get; }
    public EntityId SourceCommandId { get; }
    public EntityId? CounterpartActorId { get; }
    /// <summary>The lot, quest, gathering source or project the evidence is about.</summary>
    public EntityId SubjectId { get; }
    public ulong Quantity { get; }
    public WorldInstant AcquiredAt { get; }

    /// <summary>
    /// The identity of the evidence one committed command gives one knower. The producing transaction assigns
    /// ordinals in canonical knower order, so identities are unique per cause and replay derives the same ones.
    /// </summary>
    public static EntityId DeriveIdentity(EntityId sourceCommandId, ulong knowerOrdinal) =>
        EntityId.DeriveSpawned(sourceCommandId, checked(IdentityOrdinalBase + knowerOrdinal));
}
