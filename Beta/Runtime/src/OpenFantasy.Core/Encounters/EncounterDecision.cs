using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Encounters;

public enum EncounterDecisionKind
{
    Wait = 1,
    Action = 2
}

/// <summary>
/// A pinned request to schedule an action, not permission to perform its effects. Wait incurs
/// the same standard readiness cost; it never bypasses a required decision or advances global time.
/// </summary>
public sealed record EncounterDecision
{
    public EncounterDecision(EntityId actorId, EntityId decisionId, EncounterDecisionKind kind,
        ContentHash actionPin)
    {
        ArgumentNullException.ThrowIfNull(actionPin);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
        if (actorId == decisionId)
        {
            throw new ArgumentException("A decision cannot alias its actor.", nameof(decisionId));
        }
        ActorId = actorId;
        DecisionId = decisionId;
        Kind = kind;
        ActionPin = actionPin;
    }

    public EntityId ActorId { get; }
    public EntityId DecisionId { get; }
    public EncounterDecisionKind Kind { get; }
    public ContentHash ActionPin { get; }
}
