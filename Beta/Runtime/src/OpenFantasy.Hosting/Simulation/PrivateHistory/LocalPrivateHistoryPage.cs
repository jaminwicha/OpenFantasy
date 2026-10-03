namespace OpenFantasy.Hosting.Simulation;

/// <summary>A copied, bounded participant directory page whose navigation is tied to one exact request.</summary>
public sealed class LocalPrivateHistoryPage
{
    public const int MaximumEntries = 16;

    public LocalPrivateHistoryPage(LocalPrivateHistoryPageRequest request, ulong throughRevision,
        IReadOnlyList<LocalPrivateHistoryEntry> entries, LocalPrivateHistoryCursor? next)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count > MaximumEntries) throw new ArgumentException("The history page exceeds its entry bound.", nameof(entries));
        Request = request;
        ThroughRevision = throughRevision;
        Entries = Array.AsReadOnly(entries.ToArray());
        Next = next;
        ValidateFor(request);
    }

    public LocalPrivateHistoryPageRequest Request { get; }
    public ulong ThroughRevision { get; }
    public IReadOnlyList<LocalPrivateHistoryEntry> Entries { get; }
    public LocalPrivateHistoryCursor? Next { get; }

    public void ValidateFor(LocalPrivateHistoryPageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        if (Request != request || Entries.Count > MaximumEntries
            || request.Before is { } before && ThroughRevision != before.ThroughRevision)
            throw new ArgumentException("The history page does not match its request or page bound.");
        var subjects = new HashSet<OpenFantasy.Core.EntityId>();
        LocalPrivateHistoryEntry? previous = null;
        foreach (LocalPrivateHistoryEntry entry in Entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
            entry.Validate();
            if (entry.ClosedAtSequence > ThroughRevision || !subjects.Add(entry.SubjectId)
                || request.Before is { } cursor && LocalPrivateHistoryOrder.Compare(entry, cursor) <= 0
                || previous is not null && LocalPrivateHistoryOrder.Compare(previous, entry) >= 0)
                throw new ArgumentException("The history page contains an out-of-order, duplicate or out-of-window entry.");
            previous = entry;
        }
        if (Next is { } next)
        {
            next.Validate();
            if (Entries.Count != MaximumEntries || previous is null
                || next != new LocalPrivateHistoryCursor(ThroughRevision, previous.ClosedAtSequence,
                    previous.Kind, previous.SubjectId))
                throw new ArgumentException("The next history cursor must name the full page's final entry.");
        }
    }
}
