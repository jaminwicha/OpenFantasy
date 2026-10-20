namespace OpenFantasy.Core.Cognition;

/// <summary>
/// A transition that would exceed a finite cognition bound. Nothing is truncated: the world boundary that caused it
/// rolls back and the authority stops, as owner-archive overflow does. It never becomes a rejected gameplay action, so
/// it deliberately derives from none of the argument, operation or overflow exceptions that admission converts into
/// refusals.
/// </summary>
public sealed class CognitionCapacityException(string message) : Exception(message);

/// <summary>
/// One actor's persistent cognition: evidence actually acquired, the current intention and remembered decisions.
/// Beliefs are derived from evidence on demand, so cognition never stores a second copy of world truth. A change past
/// a finite development bound throws <see cref="CognitionCapacityException"/>; nothing is silently dropped, so an
/// unresolved commitment or its provenance cannot be lost. The bounds are workload limits, not character-depth caps.
/// Pending reconsideration is persistent state in the advancing-step domain.
/// </summary>
public sealed class ActorCognition
{
    public const int MaximumEvidence = 64;
    public const int MaximumMemory = 64;

    public ActorCognition(EntityId actorId, ulong revision, IEnumerable<CognitionEvidence> evidence,
        CognitionIntention? intention, IEnumerable<CognitionMemory> memory, CognitionWake? wake = null)
    {
        Wake = wake?.Validated();
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(memory);
        ActorId = actorId;
        Revision = revision;
        Evidence = evidence.ToList().AsReadOnly();
        Intention = intention;
        Memory = memory.ToList().AsReadOnly();
        if (Evidence.Count > MaximumEvidence || Memory.Count > MaximumMemory)
            throw new ArgumentException("Cognition exceeds its finite development bounds.");
        if (Evidence.Any(entry => entry.KnowerId != actorId))
            throw new ArgumentException("An actor's cognition holds only evidence that actor acquired.");
        if (Evidence.Select(entry => entry.EvidenceId).Distinct().Count() != Evidence.Count)
            throw new ArgumentException("Each piece of evidence is held once.");
        for (int index = 1; index < Evidence.Count; index++)
        {
            if (Order(Evidence[index - 1], Evidence[index]) >= 0)
                throw new ArgumentException("Evidence is held in canonical acquisition order.");
        }
        for (int index = 1; index < Memory.Count; index++)
        {
            if (Memory[index].At < Memory[index - 1].At) throw new ArgumentException("Memories are held in chronological order.");
        }
        var known = Evidence.Select(entry => entry.EvidenceId).ToHashSet();
        if (intention is not null && intention.Basis.Any(id => !known.Contains(id)))
            throw new ArgumentException("An intention rests only on evidence the actor holds.");
        if (Memory.Any(entry => entry.Basis.Any(id => !known.Contains(id))))
            throw new ArgumentException("A memory cites only evidence the actor holds.");
    }

    public EntityId ActorId { get; }
    public ulong Revision { get; }
    public IReadOnlyList<CognitionEvidence> Evidence { get; }
    public CognitionIntention? Intention { get; }
    public IReadOnlyList<CognitionMemory> Memory { get; }
    public CognitionWake? Wake { get; }

    public static ActorCognition Empty(EntityId actorId) => new(actorId, 0, [], null, []);

    public CognitionBeliefs Beliefs => CognitionBeliefs.From(Evidence);

    /// <summary>
    /// Adds evidence a committed outcome gave this actor at an advancing step, in canonical order, and makes
    /// reconsideration due at that step unless an earlier wake is already pending.
    /// </summary>
    public ActorCognition Acquire(CognitionEvidence evidence, ulong step)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (Evidence.Any(entry => entry.EvidenceId == evidence.EvidenceId))
            throw new InvalidOperationException("This evidence was already acquired.");
        if (Evidence.Count == MaximumEvidence)
            throw new CognitionCapacityException("The actor's evidence bound is reached.");
        var ordered = Evidence.Append(evidence).ToList();
        ordered.Sort(Order);
        return new ActorCognition(ActorId, checked(Revision + 1), ordered, Intention, Memory,
            CognitionWake.Earliest(Wake, new CognitionWake(step, WakeCause.EvidenceAcquired)));
    }

    /// <summary>Makes reconsideration due for a self-owned change or the actor's own finished action.</summary>
    public ActorCognition Schedule(CognitionWake wake)
    {
        ArgumentNullException.ThrowIfNull(wake);
        if (wake.Cause == WakeCause.Fallback || wake.Suppression != WakeSuppression.None)
            throw new ArgumentException("Only a decision schedules the fallback, and suppression is set separately.");
        CognitionWake next = CognitionWake.Earliest(Wake, wake);
        return next == Wake ? this : new ActorCognition(ActorId, checked(Revision + 1), Evidence, Intention, Memory, next);
    }

    /// <summary>Suppresses or resumes the pending wake, keeping its due step and cause.</summary>
    public ActorCognition Suppress(WakeSuppression suppression)
    {
        if (Wake is null || Wake.Suppression == suppression) return this;
        return new ActorCognition(ActorId, checked(Revision + 1), Evidence, Intention, Memory,
            (Wake with { Suppression = suppression }).Validated());
    }

    /// <summary>
    /// Applies a recorded decision. Servicing replaces the pending wake with the decision's fallback. An unchanged
    /// intention adds no memory, and an identical decision with an identical wake changes nothing. A reading hold
    /// suppresses reconsideration, so a held decision services nothing and keeps the pending wake. A new intention
    /// supersedes the previous one, or completes it when the decision reports it done, and both transitions are
    /// remembered with their reason and policy version.
    /// </summary>
    public ActorCognition Adopt(CognitionDecision decision, WorldInstant at)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(decision.NextWake);
        string policy = CognitionMemory.RequirePolicyVersion(decision.PolicyVersion);
        CognitionWake wake = decision.NextWake.Validated();
        if (wake.Cause != WakeCause.Fallback || wake.Suppression != WakeSuppression.None)
            throw new ArgumentException("A decision schedules an unsuppressed fallback wake.");
        CognitionIntention? next = decision.Intention;
        bool unchanged = (next is null && Intention is null) || (next is not null && Intention is not null && next.SameAs(Intention));
        if (decision.Deferral == CognitionDeferral.ReadingHold)
            return unchanged ? this : throw new ArgumentException("A held decision cannot change the intention.");
        if (unchanged)
            return wake == Wake ? this : new ActorCognition(ActorId, checked(Revision + 1), Evidence, Intention, Memory, wake);
        var memory = Memory.ToList();
        if (Intention is not null)
            memory.Add(new CognitionMemory(Intention.Kind, Intention.SubjectId,
                decision.CompletedCurrent ? MemoryOutcome.Completed : MemoryOutcome.Superseded, at, decision.Deferral, policy));
        if (next is not null)
            memory.Add(new CognitionMemory(next.Kind, next.SubjectId, MemoryOutcome.Adopted, at, decision.Deferral, policy,
                next.Basis));
        if (memory.Count > MaximumMemory)
            throw new CognitionCapacityException("The actor's memory bound is reached.");
        return new ActorCognition(ActorId, checked(Revision + 1), Evidence, next, memory, wake);
    }

    internal static int Order(CognitionEvidence left, CognitionEvidence right)
    {
        int byTime = left.AcquiredAt.CompareTo(right.AcquiredAt);
        return byTime != 0 ? byTime : left.EvidenceId.CompareTo(right.EvidenceId);
    }
}
