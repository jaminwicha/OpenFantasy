namespace OpenFantasy.Core.Progression.ResourceHistory;

/// <summary>One complete actor revision with a named law, actual before/after state and causal reference.</summary>
public sealed class ActorResourceChange
{
    public ActorResourceChange(int ordinal, ResourceChangeCause cause, ActorResourceOperation operation,
        ulong requestedAmount, ActorProgressionState before, ActorProgressionState after)
    {
        ArgumentNullException.ThrowIfNull(cause);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (ordinal < 0 || ordinal >= ResourceChangeBatch.MaximumChanges || !Enum.IsDefined(operation))
            throw new ArgumentException("A resource change requires a supported operation and bounded ordinal.");
        var calculated = Calculate(before, operation, requestedAmount).Candidate;
        if (before.Revision == after.Revision || !Same(calculated, after))
            throw new ArgumentException("A resource change must describe the complete next revision produced by its named law.");
        Ordinal = ordinal;
        Cause = cause;
        Operation = operation;
        RequestedAmount = requestedAmount;
        Before = before;
        After = after;
    }

    public int Ordinal { get; }
    public ResourceChangeCause Cause { get; }
    public ActorResourceOperation Operation { get; }
    public ulong RequestedAmount { get; }
    public ActorProgressionState Before { get; }
    public ActorProgressionState After { get; }

    public static ActorResourceChange FromTransition(int ordinal, ResourceChangeCause cause, ActorResourceTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        return new(ordinal, cause, transition.Operation, transition.RequestedAmount, transition.Before, transition.Candidate);
    }

    internal ActorProgressionState ApplyTo(ActorProgressionState current)
    {
        if (!Same(current, Before))
            throw new ArgumentException("The resource change does not continue the exact admitted actor revision and rule pins.");
        return Calculate(current, Operation, RequestedAmount).Candidate;
    }

    internal static bool Same(ActorProgressionState left, ActorProgressionState right) =>
        left.ActorId == right.ActorId && left.Revision == right.Revision
        && left.Profile.DefinitionId == right.Profile.DefinitionId && left.Profile.Version == right.Profile.Version
        && left.Curve.DefinitionId == right.Curve.DefinitionId && left.Curve.Version == right.Curve.Version
        && left.Level == right.Level && left.Experience == right.Experience
        && left.CurrentHp == right.CurrentHp && left.CurrentMp == right.CurrentMp;

    private static ActorResourceTransition Calculate(ActorProgressionState actor, ActorResourceOperation operation, ulong amount)
        => operation switch
        {
            ActorResourceOperation.Damage => actor.TakeDamage(checked((uint)amount)),
            ActorResourceOperation.HealthRestoration => actor.RestoreHealth(checked((uint)amount)),
            ActorResourceOperation.MagicExpenditure => actor.SpendMagic(checked((uint)amount)),
            ActorResourceOperation.ExperienceReward => actor.GainExperience(amount),
            _ => throw new ArgumentException("Unsupported resource operation.", nameof(operation))
        };
}
