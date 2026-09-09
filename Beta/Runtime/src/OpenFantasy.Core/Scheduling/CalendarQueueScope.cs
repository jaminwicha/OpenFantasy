namespace OpenFantasy.Core.Scheduling;

/// <summary>
/// Names one branch calendar queue. The authority establishes these identity relationships;
/// this value neither converts time units nor permits comparison between independent clocks.
/// </summary>
public sealed record CalendarQueueScope(EntityId WorldId, EntityId BranchId, EntityId ClockId);
