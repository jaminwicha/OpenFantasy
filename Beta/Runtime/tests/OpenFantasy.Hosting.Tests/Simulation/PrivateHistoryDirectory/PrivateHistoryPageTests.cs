using OpenFantasy.Core;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Tests.Simulation.PrivateHistoryDirectory;

public sealed class PrivateHistoryPageTests
{
    [Fact]
    public void Page_copies_rows_and_correlates_the_complete_request_and_next_cursor()
    {
        var rows = Enumerable.Range(1, 16).Select(index => Entry((ulong)index)).ToArray();
        var request = new LocalPrivateHistoryPageRequest();
        var next = new LocalPrivateHistoryCursor(20, 10, LocalPrivateHistoryKind.MaterialProposal, rows[^1].SubjectId);
        var page = new LocalPrivateHistoryPage(request, 20, rows, next);
        rows[0] = Entry(999);
        Assert.Equal(new EntityId(0x5157, 1), page.Entries[0].SubjectId);
        Assert.Equal(next, page.Next);
        Assert.Throws<ArgumentException>(() => page.ValidateFor(new(next)));
        var continuation = new LocalPrivateHistoryPage(new(next), 20, [Entry(17)], null);
        continuation.ValidateFor(new(next));
        Assert.Throws<ArgumentException>(() => new LocalPrivateHistoryPage(new(next), 21, [Entry(17)], null));
        Assert.Throws<ArgumentException>(() => new LocalPrivateHistoryPage(new(next), 20, [Entry(16)], null));
    }

    [Fact]
    public void Page_rejects_open_mixed_status_duplicate_unsorted_oversized_and_false_cursor_shapes()
    {
        var request = new LocalPrivateHistoryPageRequest();
        Assert.Throws<ArgumentException>(() => new LocalPrivateHistoryPage(request, 20, [Entry(2), Entry(1)], null));
        Assert.Throws<ArgumentException>(() => new LocalPrivateHistoryPage(request, 20, [Entry(1), Entry(1)], null));
        Assert.Throws<ArgumentException>(() => new LocalPrivateHistoryPage(request, 20,
            Enumerable.Range(1, 17).Select(value => Entry((ulong)value)).ToArray(), null));
        Assert.Throws<ArgumentException>(() => new LocalPrivateHistoryPage(request, 20, [Entry(1)],
            new(20, 10, LocalPrivateHistoryKind.MaterialProposal, Entry(1).SubjectId)));
        Assert.Throws<ArgumentException>(() => new LocalPrivateHistoryPage(request, 9, [Entry(1)], null));
        Assert.Throws<ArgumentException>(() => (Entry(1) with { MaterialProposalStatus = MaterialProposalStatus.Open }).Validate());
        Assert.Throws<ArgumentException>(() => (Entry(1) with { PaidQuoteStatus = PaidQuoteStatus.Expired }).Validate());
        Assert.Throws<ArgumentException>(() => (Entry(1) with { Kind = (LocalPrivateHistoryKind)99 }).Validate());
        var empty = new LocalPrivateHistoryPage(request, 0, [], null);
        Assert.Empty(empty.Entries);
        Assert.Null(empty.Next);
    }

    private static LocalPrivateHistoryEntry Entry(ulong id) => new(LocalPrivateHistoryKind.MaterialProposal,
        new(0x5157, id), new(0x5157, 100), 10, MaterialProposalStatus: MaterialProposalStatus.Withdrawn);
}
