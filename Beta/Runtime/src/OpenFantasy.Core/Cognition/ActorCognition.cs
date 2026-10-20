namespace OpenFantasy.Core.Cognition;

/// <summary>
/// A transition that would exceed a finite cognition bound. Nothing is truncated: the world boundary that caused it
/// rolls back and the authority stops, as owner-archive overflow does. It never becomes a rejected gameplay action, so
/// it deliberately derives from none of the argument, operation or overflow exceptions that admission converts into
/// refusals.
/// </summary>
public sealed class CognitionCapacityException(string message) : Exception(message);

/// <summary>
/// One actor's persistent cognition: evidence actually acquired, the current intention and the reason it defers
/// something preferred, and remembered decisions. Beliefs are derived from evidence on demand, so cognition never
/// stores a second copy of world truth. A newer observation of a source, or a newer outcome of the actor's own
/// gathering there, replaces an older one of the same kind that nothing cites, as market price knowledge does; the
/// replaced value stays in the journal and archived roots, and cited evidence is always kept. A change past a finite
/// development bound throws <see cref="CognitionCapacityException"/>; nothing is silently dropped, so an unresolved
/// commitment or its provenance cannot be lost. The bounds are workload limits, not character-depth caps. Pending
/// reconsideration is persistent state in the advancing-step domain.
/// </summary>
public sealed class ActorCognition
{
    public const int MaximumEvidence = 64;
    public const int MaximumMemory = 64;

    public ActorCognition(EntityId actorId, ulong revision, IEnumerable<CognitionEvidence> evidence,
        CognitionIntention? intention, IEnumerable<CognitionMemory> memory, CognitionWake? wake = null,
        CurrentDeferral? deferral = null)
    {
        Wake = wake?.Validated();
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(memory);
        ActorId = actorId;
        Revision = revision;
        Evidence = evidence.ToList().AsReadOnly();
        Intention = intention;
        Memory = memory.ToList().AsReadOnly();
        Deferral = deferral;
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
        if (intention is not null && (deferral is null || deferral.Since < intention.AdoptedAt))
            throw new ArgumentException("A held intention keeps its current deferral, given no earlier than its adoption.");
        if (intention is null && deferral is { Reason: not CognitionDeferral.None })
            throw new ArgumentException("Without an intention there is nothing to defer.");
        var cited = Cited(intention, Memory);
        if (Evidence.Where(entry => IsReplaceable(entry.Kind) && !cited.Contains(entry.EvidenceId))
                .GroupBy(entry => (entry.Kind, entry.SubjectId)).Any(group => group.Count() > 1))
            throw new ArgumentException("A newer observation or gathering outcome replaces an older one that nothing cites.");
    }

    public EntityId ActorId { get; }
    public ulong Revision { get; }
    public IReadOnlyList<CognitionEvidence> Evidence { get; }
    public CognitionIntention? Intention { get; }
    public IReadOnlyList<CognitionMemory> Memory { get; }
    public CognitionWake? Wake { get; }

    /// <summary>Why the actor defers something preferred, present once a decision has been applied.</summary>
    public CurrentDeferral? Deferral { get; }

    public static ActorCognition Empty(EntityId actorId) => new(actorId, 0, [], null, []);

    public CognitionBeliefs Beliefs => CognitionBeliefs.From(Evidence);

    /// <summary>
    /// Adds evidence a committed outcome gave this actor at an advancing step, in canonical order, and makes
    /// reconsideration due at that step unless an earlier wake is already pending. A newer observation or gathering
    /// outcome replaces the held one of the same kind and subject when nothing cites it; the bound is checked after
    /// that replacement.
    /// </summary>
    public ActorCognition Acquire(CognitionEvidence evidence, ulong step)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.KnowerId != ActorId)
            throw new ArgumentException("An actor acquires only its own evidence.");
        if (Evidence.Any(entry => entry.EvidenceId == evidence.EvidenceId))
            throw new InvalidOperationException("This evidence was already acquired.");
        var cited = Cited(Intention, Memory);
        var ordered = Evidence.Where(entry => !Replaces(evidence, entry, cited)).Append(evidence).ToList();
        if (ordered.Count > MaximumEvidence)
            throw new CognitionCapacityException("The actor's evidence bound is reached.");
        ordered.Sort(Order);
        return new ActorCognition(ActorId, checked(Revision + 1), ordered, Intention, Memory,
            CognitionWake.Earliest(Wake, new CognitionWake(step, WakeCause.EvidenceAcquired)), Deferral);
    }

    /// <summary>Makes reconsideration due for a self-owned change or the actor's own finished action.</summary>
    public ActorCognition Schedule(CognitionWake wake)
    {
        ArgumentNullException.ThrowIfNull(wake);
        if (wake.Cause == WakeCause.Fallback || wake.Suppression != WakeSuppression.None)
            throw new ArgumentException("Only a decision schedules the fallback, and suppression is set separately.");
        CognitionWake next = CognitionWake.Earliest(Wake, wake);
        return next == Wake ? this
            : new ActorCognition(ActorId, checked(Revision + 1), Evidence, Intention, Memory, next, Deferral);
    }

    /// <summary>Suppresses or resumes the pending wake, keeping its due step and cause.</summary>
    public ActorCognition Suppress(WakeSuppression suppression)
    {
        if (Wake is null || Wake.Suppression == suppression) return this;
        return new ActorCognition(ActorId, checked(Revision + 1), Evidence, Intention, Memory,
            (Wake with { Suppression = suppression }).Validated(), Deferral);
    }

    /// <summary>
    /// Applies a recorded decision. Servicing replaces the pending wake with the decision's fallback. A reading hold
    /// suppresses reconsideration, so a held decision services nothing and keeps the pending wake. An unchanged
    /// intention with an unchanged reason adds no memory, and with an identical wake changes nothing. An unchanged
    /// intention with a new reason becomes the current deferral and one reconsidered memory. A new intention supersedes
    /// the previous one, or completes it when the decision reports it done, and both transitions are remembered with
    /// their reason and policy version. Without an intention nothing is deferred.
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
        if (next is null && decision.Deferral != CognitionDeferral.None)
            throw new ArgumentException("Without an intention there is nothing to defer.");
        var memory = Memory.ToList();
        if (unchanged)
        {
            if (Deferral?.Reason == decision.Deferral || (Deferral is null && next is null))
                return wake == Wake ? this
                    : new ActorCognition(ActorId, checked(Revision + 1), Evidence, Intention, Memory, wake, Deferral);
            if (Intention is not null)
                memory.Add(new CognitionMemory(Intention.Kind, Intention.SubjectId, MemoryOutcome.Reconsidered, at,
                    decision.Deferral, policy));
            if (memory.Count > MaximumMemory)
                throw new CognitionCapacityException("The actor's memory bound is reached.");
            return new ActorCognition(ActorId, checked(Revision + 1), Evidence, Intention, memory, wake,
                new CurrentDeferral(decision.Deferral, policy, at));
        }
        if (Intention is not null)
            memory.Add(new CognitionMemory(Intention.Kind, Intention.SubjectId,
                decision.CompletedCurrent ? MemoryOutcome.Completed : MemoryOutcome.Superseded, at, decision.Deferral, policy));
        if (next is not null)
            memory.Add(new CognitionMemory(next.Kind, next.SubjectId, MemoryOutcome.Adopted, at, decision.Deferral, policy,
                next.Basis));
        if (memory.Count > MaximumMemory)
            throw new CognitionCapacityException("The actor's memory bound is reached.");
        return new ActorCognition(ActorId, checked(Revision + 1), Evidence, next, memory, wake,
            new CurrentDeferral(decision.Deferral, policy, at));
    }

    /// <summary>Kinds whose newer entry shows the subject as it is now and so replaces an older uncited entry.</summary>
    internal static bool IsReplaceable(EvidenceKind kind) => kind is EvidenceKind.SourceObserved or EvidenceKind.GatherOutcome;

    private static bool Replaces(CognitionEvidence newer, CognitionEvidence held, IReadOnlySet<EntityId> cited) =>
        IsReplaceable(newer.Kind) && held.Kind == newer.Kind && held.SubjectId == newer.SubjectId
        && !cited.Contains(held.EvidenceId);

    private static HashSet<EntityId> Cited(CognitionIntention? intention, IEnumerable<CognitionMemory> memory) =>
        (intention?.Basis ?? []).Concat(memory.SelectMany(entry => entry.Basis)).ToHashSet();

    internal static int Order(CognitionEvidence left, CognitionEvidence right)
    {
        int byTime = left.AcquiredAt.CompareTo(right.AcquiredAt);
        return byTime != 0 ? byTime : left.EvidenceId.CompareTo(right.EvidenceId);
    }
}
