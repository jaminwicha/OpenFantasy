namespace OpenFantasy.Core.Progression;

public enum ActorResourceOperation
{
    Damage,
    HealthRestoration,
    MagicExpenditure,
    ExperienceReward
}

/// <summary>Actual values and capacities across one immutable actor transition; not a second resource owner.</summary>
public readonly struct ActorResourceDelta
{
    internal ActorResourceDelta(int before, int after, int maximumBefore, int maximumAfter)
    {
        Before = before;
        After = after;
        MaximumBefore = maximumBefore;
        MaximumAfter = maximumAfter;
    }

    public int Before { get; }
    public int After { get; }
    public int MaximumBefore { get; }
    public int MaximumAfter { get; }
    public int Delta => After - Before;
    public bool Changed => Before != After || MaximumBefore != MaximumAfter;
}

/// <summary>
/// A named operation's private candidate and actual resource effects. Permission, cause ordering,
/// costs and publication belong to the composing authority; this result is not durable history.
/// </summary>
public sealed class ActorResourceTransition
{
    internal ActorResourceTransition(ActorProgressionState before, ActorProgressionState candidate,
        ActorResourceOperation operation, ulong requestedAmount)
    {
        if (before.ActorId != candidate.ActorId || !Enum.IsDefined(operation)
            || !ReferenceEquals(before.Profile, candidate.Profile) || !ReferenceEquals(before.Curve, candidate.Curve)
            || !ReferenceEquals(before, candidate) && candidate.Revision != checked(before.Revision + 1))
        {
            throw new ArgumentException("A resource transition requires one actor, pinned rules and one complete actor revision.");
        }
        Before = before;
        Candidate = candidate;
        Operation = operation;
        RequestedAmount = requestedAmount;
        Health = new(before.CurrentHp, candidate.CurrentHp, before.Stats.MaxHp, candidate.Stats.MaxHp);
        Magic = new(before.CurrentMp, candidate.CurrentMp, before.Stats.MaxMp, candidate.Stats.MaxMp);
    }

    public ActorProgressionState Before { get; }
    public ActorProgressionState Candidate { get; }
    public ActorResourceOperation Operation { get; }
    public ulong RequestedAmount { get; }
    public ActorResourceDelta Health { get; }
    public ActorResourceDelta Magic { get; }
    public bool ChangesResources => Health.Changed || Magic.Changed;
}
