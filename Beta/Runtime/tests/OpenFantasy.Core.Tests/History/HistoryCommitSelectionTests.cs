using OpenFantasy.Core.Commands;
using OpenFantasy.Core.History;

namespace OpenFantasy.Core.Tests.History;

public sealed class HistoryCommitSelectionTests
{
    private static readonly EntityId _world = new(0, 1);
    private static readonly EntityId _branch = new(0, 2);
    private static readonly WorldInstant _requested = new(10, 500);

    [Fact]
    public void SelectionUsesCommitOrderRatherThanTimestampOrderOrInputOrder()
    {
        HistoryRequest request = Request(6);
        HistoryCommitStamp[] candidates =
        [
            Commit(5, _requested), Commit(3, _requested.AddMilliseconds(1)),
            Commit(4, _requested.AddMilliseconds(-10)), Commit(1, _requested),
            Commit(2, _requested.AddMilliseconds(-20)), Commit(6, _requested)
        ];
        var selection = new HistoryCommitSelection(request, _branch, 6, candidates);
        Assert.Equal(new ulong[] { 1, 2, 4, 5, 6 }, selection.Commits.Select(commit => commit.Identity.Sequence));
        Assert.Equal(6, selection.CandidateCount);
        Assert.Equal(selection.Commits,
            new HistoryCommitSelection(request, _branch, 6, candidates.Reverse()).Commits);
        Assert.Equal(new ulong[] { 1, 2, 4 },
            new HistoryCommitSelection(Request(6, 4), _branch, 6, candidates).Commits
                .Select(commit => commit.Identity.Sequence));
    }

    [Fact]
    public void SelectionCopiesItsBatchAndCannotBeMovedByLaterHeadValues()
    {
        HistoryRequest request = Request(3);
        HistoryCommitStamp[] source = [Commit(3, _requested), Commit(1, _requested)];
        var selection = new HistoryCommitSelection(request, _branch, 3, source);
        source[0] = Commit(2, _requested);
        Assert.Equal(new ulong[] { 1, 3 }, selection.Commits.Select(commit => commit.Identity.Sequence));
        Assert.Throws<NotSupportedException>(() => ((IList<HistoryCommitStamp>)selection.Commits).Clear());
        Assert.Equal(3UL, selection.Request.Boundary.PinnedHeadRevision);
        Assert.Throws<ArgumentException>(() => new HistoryCommitSelection(request, _branch, 4, source));
    }

    [Fact]
    public void WrongScopeFutureCommitsAndDuplicateSequencesRejectBeforeFiltering()
    {
        HistoryRequest request = Request(3, 0);
        Assert.Throws<ArgumentException>(() => new HistoryCommitSelection(request, new EntityId(0, 99), 3, []));
        Assert.Throws<ArgumentException>(() => new HistoryCommitSelection(request, _branch, 2, []));
        Assert.Throws<ArgumentException>(() => new HistoryCommitSelection(request, _branch, 3,
            [new HistoryCommitStamp(new CommandCommitIdentity(new EntityId(0, 99), 1), _requested)]));
        Assert.Throws<ArgumentException>(() => new HistoryCommitSelection(request, _branch, 3,
            [Commit(4, _requested)]));
        Assert.Throws<ArgumentException>(() => new HistoryCommitSelection(request, _branch, 3,
            [Commit(1, new WorldInstant(0, 0).AddMilliseconds(-1))]));
        Assert.Throws<ArgumentException>(() => new HistoryCommitSelection(request, _branch, 3,
            [Commit(1, new WorldInstant(20, 0).AddMilliseconds(1))]));
        Assert.Throws<ArgumentException>(() => new HistoryCommitSelection(request, _branch, 3,
            [Commit(1, _requested), Commit(1, _requested.AddMilliseconds(1))]));
        Assert.Throws<ArgumentNullException>(() => new HistoryCommitSelection(request, _branch, 3, [null!]));
        Assert.Empty(new HistoryCommitSelection(request, _branch, 3, [Commit(1, _requested)]).Commits);
    }

    [Fact]
    public void CandidateLimitIsAnOperationBoundAndEnumerationStopsAtFirstExcess()
    {
        int count = 0;
        IEnumerable<HistoryCommitStamp> Candidates(int maximum)
        {
            for (int index = 1; index <= maximum; index++)
            {
                count++;
                yield return Commit((ulong)index, _requested);
            }
        }

        int limit = HistoryCommitSelection.MaximumCandidateCount;
        HistoryRequest request = Request(ulong.MaxValue);
        Assert.Equal(limit, new HistoryCommitSelection(request, _branch, ulong.MaxValue, Candidates(limit)).Commits.Count);
        count = 0;
        Assert.Throws<ArgumentException>(() =>
            new HistoryCommitSelection(request, _branch, ulong.MaxValue, Candidates(limit + 50)));
        Assert.Equal(limit + 1, count);
        Assert.Empty(new HistoryCommitSelection(Request(0), _branch, 0, []).Commits);
    }

    private static HistoryRequest Request(ulong headRevision, ulong? cursor = null) =>
        new(new HistoryAddress(_world, _branch, new EntityId(0, 3), 0, 0, 0),
            new HistoryBoundary(new WorldInstant(0, 0), new WorldInstant(20, 0),
                _requested, headRevision, cursor));

    private static HistoryCommitStamp Commit(ulong sequence, WorldInstant instant) =>
        new(new CommandCommitIdentity(_world, sequence), instant);
}
