using System.Collections;
using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Commands;

/// <summary>An immutable, canonically ordered collection with one revision per owner.</summary>
public sealed class ExpectedRevisionSet : IReadOnlyList<ExpectedRevision>
{
    public const int MaximumCount = 64;
    private readonly ReadOnlyCollection<ExpectedRevision> _revisions;

    public ExpectedRevisionSet(IEnumerable<ExpectedRevision> revisions)
    {
        ArgumentNullException.ThrowIfNull(revisions);
        List<ExpectedRevision> snapshot = [];
        foreach (ExpectedRevision revision in revisions)
        {
            ArgumentNullException.ThrowIfNull(revision);
            if (snapshot.Count == MaximumCount)
            {
                throw new ArgumentException("A command can expect at most 64 revisions.", nameof(revisions));
            }

            snapshot.Add(revision);
        }

        snapshot.Sort(CompareTargets);
        for (int index = 1; index < snapshot.Count; index++)
        {
            if (CompareTargets(snapshot[index - 1], snapshot[index]) == 0)
            {
                throw new ArgumentException("A revision owner cannot occur twice.", nameof(revisions));
            }
        }

        _revisions = snapshot.AsReadOnly();
    }

    public int Count => _revisions.Count;
    public ExpectedRevision this[int index] => _revisions[index];
    public IEnumerator<ExpectedRevision> GetEnumerator() => _revisions.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal static int CompareTargets(ExpectedRevision left, ExpectedRevision right)
    {
        int kindOrder = left.TargetKind.CompareTo(right.TargetKind);
        return kindOrder != 0 ? kindOrder : left.TargetId.CompareTo(right.TargetId);
    }
}
