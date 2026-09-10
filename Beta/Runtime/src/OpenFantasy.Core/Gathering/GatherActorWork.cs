namespace OpenFantasy.Core.Gathering;

/// <summary>Banked effort for the same admitted actor schema regardless of controller.</summary>
public sealed record GatherActorWork
{
    public GatherActorWork(EntityId actorId, uint effort)
    {
        if (effort > GatherRulesV1.MaximumEffort)
        {
            throw new ArgumentOutOfRangeException(nameof(effort));
        }
        ActorId = actorId;
        Effort = effort;
    }
    public EntityId ActorId { get; }
    public uint Effort { get; }
    internal GatherActorWork Advance() => new(ActorId, Math.Min(GatherRulesV1.MaximumEffort,
        Effort + GatherRulesV1.EffortPerAdvancingStep));
    internal GatherActorWork Spend() => new(ActorId, checked(Effort - GatherRulesV1.EffortPerSwing));
}
