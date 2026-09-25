namespace OpenFantasy.Core.Encounters;

public enum EncounterActorControl
{
    RequiredHuman = 1,
    Automatic = 2
}

/// <summary>
/// Retained readiness debt, not an absolute calendar deadline. Restoring or rebinding an actor
/// must retain incurred debt; zero is an explicit ready state, not a restoration default.
/// </summary>
public sealed record EncounterActorTiming
{
    public EncounterActorTiming(EntityId actorId, uint normalizedSpeed, ulong remainingDelay,
        EncounterActorControl control)
    {
        if (normalizedSpeed == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(normalizedSpeed));
        }
        if (!Enum.IsDefined(control))
        {
            throw new ArgumentOutOfRangeException(nameof(control));
        }
        ActorId = actorId;
        NormalizedSpeed = normalizedSpeed;
        RemainingDelay = remainingDelay;
        Control = control;
    }

    public EntityId ActorId { get; }
    public uint NormalizedSpeed { get; }
    public ulong RemainingDelay { get; }
    public EncounterActorControl Control { get; }
    public bool IsDue => RemainingDelay == 0;

    /// <summary>Standard readiness is 100 units at normalized speed 100, rounded upward exactly.</summary>
    public ulong StandardDelay => (10_000UL + NormalizedSpeed - 1) / NormalizedSpeed;

    internal EncounterActorTiming WithDelay(ulong remainingDelay) =>
        new(ActorId, NormalizedSpeed, remainingDelay, Control);
}
