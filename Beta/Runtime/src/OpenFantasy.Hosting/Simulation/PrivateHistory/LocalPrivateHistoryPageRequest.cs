using OpenFantasy.Core;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>An exclusive directory position beneath the first page's inclusive revision ceiling.</summary>
public sealed record LocalPrivateHistoryCursor(ulong ThroughRevision, ulong ClosedAtSequence,
    LocalPrivateHistoryKind Kind, EntityId SubjectId)
{
    public void Validate()
    {
        if (ClosedAtSequence == 0 || ClosedAtSequence > ThroughRevision
            || Kind is not (LocalPrivateHistoryKind.PaidQuote or LocalPrivateHistoryKind.MaterialProposal))
            throw new ArgumentException("The history cursor has an invalid ceiling or terminal identity.");
    }
}

/// <summary>One fixed-size directory page. A cursor is a filter and never an authorization token.</summary>
public sealed record LocalPrivateHistoryPageRequest(LocalPrivateHistoryCursor? Before = null)
{
    public void Validate() => Before?.Validate();
}

internal static class LocalPrivateHistoryOrder
{
    internal static int Compare(LocalPrivateHistoryEntry left, LocalPrivateHistoryEntry right) =>
        Compare(left.ClosedAtSequence, left.Kind, left.SubjectId,
            right.ClosedAtSequence, right.Kind, right.SubjectId);

    internal static int Compare(LocalPrivateHistoryEntry entry, LocalPrivateHistoryCursor cursor) =>
        Compare(entry.ClosedAtSequence, entry.Kind, entry.SubjectId,
            cursor.ClosedAtSequence, cursor.Kind, cursor.SubjectId);

    private static int Compare(ulong leftSequence, LocalPrivateHistoryKind leftKind, EntityId leftId,
        ulong rightSequence, LocalPrivateHistoryKind rightKind, EntityId rightId)
    {
        int sequence = rightSequence.CompareTo(leftSequence);
        if (sequence != 0) return sequence;
        int kind = leftKind.CompareTo(rightKind);
        return kind != 0 ? kind : leftId.CompareTo(rightId);
    }
}
