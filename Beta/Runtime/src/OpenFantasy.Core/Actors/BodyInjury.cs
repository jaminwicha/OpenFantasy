namespace OpenFantasy.Core.Actors;

public enum BodyInjuryKind
{
    Scar = 1,
    MissingPart = 2,
    FunctionLoss = 3
}

/// <summary>A causal condition record. Creating it does not perform damage, healing or death.</summary>
public sealed record BodyInjury
{
    public BodyInjury(EntityId injuryId, EntityId causeId, BodySlotKey slot, BodyInjuryKind kind,
        BodyFunction? lostFunction = null)
    {
        ArgumentNullException.ThrowIfNull(slot);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if ((kind == BodyInjuryKind.FunctionLoss) != lostFunction.HasValue
            || lostFunction is BodyFunction function && !Enum.IsDefined(function))
        {
            throw new ArgumentException("Only a function-loss injury requires one known lost function.", nameof(lostFunction));
        }

        InjuryId = injuryId;
        CauseId = causeId;
        Slot = slot;
        Kind = kind;
        LostFunction = lostFunction;
    }

    public EntityId InjuryId { get; }
    public EntityId CauseId { get; }
    public BodySlotKey Slot { get; }
    public BodyInjuryKind Kind { get; }
    public BodyFunction? LostFunction { get; }
}
