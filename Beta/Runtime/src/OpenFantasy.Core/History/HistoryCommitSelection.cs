using System.Collections.ObjectModel;

namespace OpenFantasy.Core.History;

/// <summary>
/// Validates and filters one bounded batch of caller-supplied commit metadata.
/// The storage resolver supplies ancestry-resolved branch membership; this class
/// neither infers ancestry nor proves that the batch covers the complete prefix.
/// </summary>
public sealed class HistoryCommitSelection
{
    public const int MaximumCandidateCount = 4_096;

    public HistoryCommitSelection(HistoryRequest request, EntityId sourceBranchId,
        ulong sourcePinnedHeadRevision, IEnumerable<HistoryCommitStamp> candidates)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidates);
        if (sourceBranchId != request.Address.BranchId)
        {
            throw new ArgumentException("The resolved stream must name the requested branch.",
                nameof(sourceBranchId));
        }

        if (sourcePinnedHeadRevision != request.Boundary.PinnedHeadRevision)
        {
            throw new ArgumentException("The resolved stream must use the request's pinned head revision.",
                nameof(sourcePinnedHeadRevision));
        }

        var ordered = new SortedDictionary<ulong, HistoryCommitStamp>();
        int candidateCount = 0;
        foreach (HistoryCommitStamp commit in candidates)
        {
            ArgumentNullException.ThrowIfNull(commit, nameof(candidates));
            candidateCount++;
            if (candidateCount > MaximumCandidateCount)
            {
                throw new ArgumentException("The candidate batch exceeds its bounded selection limit.",
                    nameof(candidates));
            }

            if (commit.Identity.WorldId != request.Address.WorldId)
            {
                throw new ArgumentException("A candidate commit belongs to a different world.",
                    nameof(candidates));
            }

            if (commit.Identity.Sequence > sourcePinnedHeadRevision)
            {
                throw new ArgumentException("A candidate commit exceeds the resolved stream's pinned head.",
                    nameof(candidates));
            }

            if (commit.AuthoritativeInstant < request.Boundary.HistoricalOrigin
                || commit.AuthoritativeInstant > request.Boundary.KnownHead)
            {
                throw new ArgumentException("A candidate commit is outside the available history window.",
                    nameof(candidates));
            }

            if (!ordered.TryAdd(commit.Identity.Sequence, commit))
            {
                throw new ArgumentException("The candidate stream contains a duplicate commit sequence.",
                    nameof(candidates));
            }
        }

        Request = request;
        CandidateCount = candidateCount;
        Commits = Array.AsReadOnly(ordered.Values.Where(request.Includes).ToArray());
    }

    public HistoryRequest Request { get; }
    public int CandidateCount { get; }
    public ReadOnlyCollection<HistoryCommitStamp> Commits { get; }
}
