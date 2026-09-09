namespace OpenFantasy.Core.Scheduling;

/// <summary>
/// Immutable due work pointing to an owner's retained work item and continuation ordinal.
/// It contains no callback, client timing, executable payload or inferred gameplay permission.
/// </summary>
public sealed record ScheduledWorkEntry : IComparable<ScheduledWorkEntry>
{
    public ScheduledWorkEntry(EntityId eventId, WorldInstant dueAt, ScheduledWorkPhase phase,
        EntityId workDomainId, EntityId actorId, EntityId workItemId, ulong continuationOrdinal)
    {
        if (!Enum.IsDefined(phase))
        {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }

        EventId = eventId;
        DueAt = dueAt;
        Phase = phase;
        WorkDomainId = workDomainId;
        ActorId = actorId;
        WorkItemId = workItemId;
        ContinuationOrdinal = continuationOrdinal;
    }

    public EntityId EventId { get; }
    public WorldInstant DueAt { get; }
    public ScheduledWorkPhase Phase { get; }
    public EntityId WorkDomainId { get; }
    public EntityId ActorId { get; }
    public EntityId WorkItemId { get; }
    public ulong ContinuationOrdinal { get; }

    public int CompareTo(ScheduledWorkEntry? other)
    {
        if (other is null)
        {
            return 1;
        }

        int comparison = DueAt.CompareTo(other.DueAt);
        if (comparison == 0)
        {
            comparison = Phase.CompareTo(other.Phase);
        }
        if (comparison == 0)
        {
            comparison = WorkDomainId.CompareTo(other.WorkDomainId);
        }
        if (comparison == 0)
        {
            comparison = ActorId.CompareTo(other.ActorId);
        }
        if (comparison == 0)
        {
            comparison = EventId.CompareTo(other.EventId);
        }
        return comparison;
    }
}
