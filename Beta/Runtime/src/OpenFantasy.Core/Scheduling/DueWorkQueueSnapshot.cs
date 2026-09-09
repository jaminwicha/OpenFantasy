using System.Collections.ObjectModel;
using System.Security.Cryptography;

namespace OpenFantasy.Core.Scheduling;

/// <summary>
/// Immutable, bounded state for one calendar queue. Tactical readiness and exposure clocks require
/// separate typed queues; the host must never put those numeric values into this calendar.
/// </summary>
public sealed class DueWorkQueueSnapshot
{
    public const int MaximumEntryCount = 1024;
    public const int MaximumSelectionCount = 256;
    private readonly ReadOnlyCollection<ScheduledWorkEntry> _entries;

    public DueWorkQueueSnapshot(CalendarQueueScope scope, ulong revision,
        IEnumerable<ScheduledWorkEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(entries);
        List<ScheduledWorkEntry> snapshot = [];
        HashSet<EntityId> eventIds = [];
        foreach (ScheduledWorkEntry entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
            if (snapshot.Count == MaximumEntryCount)
            {
                throw new ArgumentException("The calendar queue exceeds its retained-entry limit.", nameof(entries));
            }
            if (!eventIds.Add(entry.EventId))
            {
                throw new ArgumentException("A pending event identity can occur only once in a queue.", nameof(entries));
            }
            snapshot.Add(entry);
        }

        snapshot.Sort();
        Scope = scope;
        Revision = revision;
        _entries = snapshot.AsReadOnly();
    }

    public CalendarQueueScope Scope { get; }
    public ulong Revision { get; }
    public ReadOnlyCollection<ScheduledWorkEntry> Entries => _entries;

    public string ComputeSnapshotHash() => Convert.ToHexString(
        SHA256.HashData(CalendarQueueCodecV1.Encode(this))).ToLowerInvariant();

    /// <summary>
    /// Selects one explicit phase without removing other overdue phases. The host owns global
    /// step ordering and must drain transitions before admitting commands and later phases.
    /// </summary>
    public DueWorkSelection SelectDue(CalendarClockCoordinate cutoff, ScheduledWorkPhase phase, int maximumCount)
    {
        ArgumentNullException.ThrowIfNull(cutoff);
        if (cutoff.Scope != Scope)
        {
            throw new ArgumentException("The cutoff belongs to a different calendar queue.", nameof(cutoff));
        }
        if (!Enum.IsDefined(phase))
        {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }
        if (maximumCount is < 1 or > MaximumSelectionCount)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }

        List<ScheduledWorkEntry> selected = [];
        bool hasMoreEligibleWork = false;
        foreach (ScheduledWorkEntry entry in _entries)
        {
            if (entry.DueAt > cutoff.Instant)
            {
                break;
            }
            if (entry.Phase != phase)
            {
                continue;
            }
            if (selected.Count == maximumCount)
            {
                hasMoreEligibleWork = true;
                break;
            }
            selected.Add(entry);
        }

        return new DueWorkSelection(Scope, Revision, ComputeSnapshotHash(), cutoff, phase,
            maximumCount, selected, _entries.Count - selected.Count, hasMoreEligibleWork);
    }

    /// <summary>
    /// Creates a candidate replacement acknowledging the selected owner work. This does not
    /// execute work or commit effects. Applying against a changed snapshot rejects without mutation.
    /// </summary>
    public DueWorkQueueSnapshot ApplySelection(DueWorkSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.Scope != Scope || selection.BaseRevision != Revision ||
            selection.BaseSnapshotHash != ComputeSnapshotHash())
        {
            throw new InvalidOperationException("The selection was prepared against a different queue snapshot.");
        }
        if (selection.SelectedWork.Count == 0)
        {
            return this;
        }

        ulong nextRevision = checked(Revision + 1);
        HashSet<EntityId> completed = selection.SelectedWork.Select(entry => entry.EventId).ToHashSet();
        return new DueWorkQueueSnapshot(Scope, nextRevision,
            _entries.Where(entry => !completed.Contains(entry.EventId)));
    }
}
