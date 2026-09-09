using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Scheduling;

/// <summary>
/// A bounded immutable candidate selection from one exact queue snapshot. Selection removes no
/// work; the owner must complete its work and commit effects with the applied queue replacement.
/// </summary>
public sealed class DueWorkSelection
{
    internal DueWorkSelection(CalendarQueueScope scope, ulong baseRevision, string baseSnapshotHash,
        CalendarClockCoordinate cutoff, ScheduledWorkPhase phase, int maximumCount,
        IEnumerable<ScheduledWorkEntry> selectedWork, int pendingCount, bool hasMoreEligibleWork)
    {
        Scope = scope;
        BaseRevision = baseRevision;
        BaseSnapshotHash = baseSnapshotHash;
        Cutoff = cutoff;
        Phase = phase;
        MaximumCount = maximumCount;
        SelectedWork = new List<ScheduledWorkEntry>(selectedWork).AsReadOnly();
        PendingCount = pendingCount;
        HasMoreEligibleWork = hasMoreEligibleWork;
    }

    public CalendarQueueScope Scope { get; }
    public ulong BaseRevision { get; }
    public string BaseSnapshotHash { get; }
    public CalendarClockCoordinate Cutoff { get; }
    public ScheduledWorkPhase Phase { get; }
    public int MaximumCount { get; }
    public ReadOnlyCollection<ScheduledWorkEntry> SelectedWork { get; }
    public int PendingCount { get; }
    public bool HasMoreEligibleWork { get; }
}
