namespace OpenFantasy.Core.Cognition;

/// <summary>What the actor is currently trying to do. An intention proposes ordinary actions; it grants nothing.</summary>
public enum IntentionKind
{
    /// <summary>Wait for a project's supplies; doing nothing is an honest outcome, not a failure.</summary>
    AwaitSupply = 1,
    /// <summary>Gather a small personal stock at a known source with an owned tool.</summary>
    GatherPersonalStock = 2,
    /// <summary>Work the actor's own funded project now that its supplies are known to have arrived.</summary>
    WorkFundedProject = 3,
}

/// <summary>Why the actor is not doing something it would otherwise prefer; never a hidden world fact.</summary>
public enum CognitionDeferral
{
    None = 0,
    MissingTool = 1,
    NoKnownSource = 2,
    SourceDepleted = 3,
    InsufficientEffort = 4,
    Unreachable = 5,
    ReadingHold = 6,
    StockTargetMet = 7,
    AwaitingSupply = 8,
}

/// <summary>An adopted intention, its subject, when it was adopted and the evidence it rests on.</summary>
public sealed class CognitionIntention
{
    /// <summary>A finite development bound on cited evidence; a decision rests on a few direct facts.</summary>
    public const int MaximumBasis = 8;

    public CognitionIntention(IntentionKind kind, EntityId subjectId, WorldInstant adoptedAt, IEnumerable<EntityId> basis)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        ArgumentNullException.ThrowIfNull(basis);
        Kind = kind;
        SubjectId = subjectId;
        AdoptedAt = adoptedAt;
        Basis = basis.ToList().AsReadOnly();
        if (Basis.Count > MaximumBasis) throw new ArgumentException("An intention cites at most its bounded evidence.");
        if (Basis.Distinct().Count() != Basis.Count) throw new ArgumentException("An intention cites each piece of evidence once.");
        if (kind == IntentionKind.WorkFundedProject && Basis.Count == 0)
            throw new ArgumentException("Working a funded project requires evidence that its supplies arrived.");
    }

    public IntentionKind Kind { get; }
    /// <summary>The project or gathering source the intention concerns.</summary>
    public EntityId SubjectId { get; }
    public WorldInstant AdoptedAt { get; }
    public IReadOnlyList<EntityId> Basis { get; }

    public bool SameAs(CognitionIntention other) =>
        other is not null && Kind == other.Kind && SubjectId == other.SubjectId;
}

/// <summary>
/// What happened to an intention, recorded so an actor can later explain it: adopted, completed, superseded, or kept
/// while the reason for deferring something preferred changed.
/// </summary>
public enum MemoryOutcome { Adopted = 1, Completed = 2, Superseded = 3, Reconsidered = 4 }

/// <summary>
/// Why the actor currently defers something it would otherwise prefer, under which policy that reason was first given
/// and since when. It sits beside the intention, so the actor's own state can say why it is waiting. A change of reason
/// with the intention kept becomes one remembered entry; a repeated reason changes nothing.
/// </summary>
public sealed record CurrentDeferral
{
    public CurrentDeferral(CognitionDeferral reason, string policyVersion, WorldInstant since)
    {
        if (!Enum.IsDefined(reason) || reason == CognitionDeferral.ReadingHold)
            throw new ArgumentException("A current deferral names a known reason; a reading hold services nothing.");
        Reason = reason;
        PolicyVersion = CognitionMemory.RequirePolicyVersion(policyVersion);
        Since = since;
    }

    public CognitionDeferral Reason { get; }
    public string PolicyVersion { get; }
    public WorldInstant Since { get; }
}

/// <summary>
/// One remembered decision: which intention, what happened to it, when, why and under which recorded policy version.
/// An adoption keeps the evidence it rested on, so the actor can later explain the episode from its own provenance;
/// an ending or a reconsidered reason keeps none.
/// </summary>
public sealed class CognitionMemory
{
    public const int MaximumPolicyVersionBytes = 64;

    public CognitionMemory(IntentionKind intention, EntityId subjectId, MemoryOutcome outcome, WorldInstant at,
        CognitionDeferral reason, string policyVersion, IEnumerable<EntityId>? basis = null)
    {
        PolicyVersion = RequirePolicyVersion(policyVersion);
        if (!Enum.IsDefined(intention) || !Enum.IsDefined(outcome) || !Enum.IsDefined(reason))
            throw new ArgumentOutOfRangeException(nameof(outcome), "A memory names a known intention, outcome and reason.");
        Intention = intention;
        SubjectId = subjectId;
        Outcome = outcome;
        At = at;
        Reason = reason;
        Basis = (basis ?? []).ToList().AsReadOnly();
        if (Basis.Count > CognitionIntention.MaximumBasis || Basis.Distinct().Count() != Basis.Count)
            throw new ArgumentException("A memory cites its bounded evidence once each.");
        if (outcome != MemoryOutcome.Adopted && Basis.Count != 0)
            throw new ArgumentException("Only an adoption remembers the evidence it rested on.");
        if (outcome == MemoryOutcome.Reconsidered && reason == CognitionDeferral.ReadingHold)
            throw new ArgumentException("A reading hold services nothing, so it is never a reconsidered reason.");
    }

    public IntentionKind Intention { get; }
    public EntityId SubjectId { get; }
    public MemoryOutcome Outcome { get; }
    public WorldInstant At { get; }
    public CognitionDeferral Reason { get; }
    public string PolicyVersion { get; }
    public IReadOnlyList<EntityId> Basis { get; }

    internal static string RequirePolicyVersion(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (System.Text.Encoding.UTF8.GetByteCount(value) > MaximumPolicyVersionBytes || value.Any(char.IsControl))
            throw new ArgumentException("A policy version is a short printable name.");
        return value;
    }
}
