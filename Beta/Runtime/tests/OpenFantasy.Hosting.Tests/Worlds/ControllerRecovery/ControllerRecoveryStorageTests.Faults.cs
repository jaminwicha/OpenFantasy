using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.ControllerRecovery;

public sealed partial class ControllerRecoveryStorageTests
{
    [Theory]
    [InlineData(WorldControllerEventKind.Disconnected, false)]
    [InlineData(WorldControllerEventKind.Disconnected, true)]
    [InlineData(WorldControllerEventKind.AllowanceExpired, false)]
    [InlineData(WorldControllerEventKind.AllowanceExpired, true)]
    [InlineData(WorldControllerEventKind.Reconnected, false)]
    [InlineData(WorldControllerEventKind.Reconnected, true)]
    public void Event_and_progress_rows_recover_atomically_before_or_after_commit(WorldControllerEventKind kind, bool after)
    {
        using var files = new StorageFiles();
        WorldSnapshot before, expected;
        using (var authority = SqliteWorldAuthority.CreateRecoverable(files.Path, Fresh.Value))
        {
            before = Fresh.Value;
            if (kind != WorldControllerEventKind.Disconnected)
                before = authority.Commit(Event(before, WorldControllerEventKind.Disconnected, 30)).Candidate;
            var segment = authority.BeginControllerRecoverySegment();
            authority.CheckpointControllerAllowances(segment.Generation,
                segment.Allowances.Select(row => row with { ElapsedHostMilliseconds = 30_000 }).ToArray());
            var batch = Event(before, kind, 31);
            expected = new WorldStepProcessor().Decide(before, batch).Candidate;
            Assert.Throws<IOException>(() => authority.Commit(batch, Fault(after)));
            Assert.Throws<InvalidOperationException>(() => authority.ReadStateForHost());
            Assert.Throws<InvalidOperationException>(() => authority.ReadControllerAllowancesForHost());
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(Fresh.Value));
        WorldSnapshot actual = after ? expected : before;
        Assert.Equal(WorldSnapshotCodec.Encode(actual), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.Equal(actual.Controllers!.Assignments.Count(row => row.Mode == WorldControllerMode.ReconnectAllowance),
            reopened.ReadControllerAllowancesForHost().Count);
        if (reopened.ReadControllerAllowancesForHost().Count != 0)
            Assert.Equal(kind == WorldControllerEventKind.Disconnected ? 0UL : 30_000UL,
                Assert.Single(reopened.ReadControllerAllowancesForHost()).ElapsedHostMilliseconds);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Operational_checkpoint_or_segment_fault_requires_reopen_without_canonical_changes(bool beginSegment, bool after)
    {
        using var files = new StorageFiles();
        WorldSnapshot before;
        ulong generation;
        using (var authority = SqliteWorldAuthority.CreateRecoverable(files.Path, Fresh.Value))
        {
            before = authority.Commit(Event(Fresh.Value, WorldControllerEventKind.Disconnected, 40)).Candidate;
            var segment = authority.BeginControllerRecoverySegment(); generation = segment.Generation;
            if (beginSegment) Assert.Throws<IOException>(() => authority.BeginControllerRecoverySegment(Fault(after)));
            else Assert.Throws<IOException>(() => authority.CheckpointControllerAllowances(segment.Generation,
                segment.Allowances.Select(row => row with { ElapsedHostMilliseconds = 1_234 }).ToArray(), Fault(after)));
            Assert.Throws<InvalidOperationException>(() => authority.ReadStateForHost());
            Assert.Throws<InvalidOperationException>(() => authority.BeginControllerRecoverySegment());
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(Fresh.Value));
        Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.Equal(!beginSegment && after ? 1_234UL : 0UL, Assert.Single(reopened.ReadControllerAllowancesForHost()).ElapsedHostMilliseconds);
        Assert.Equal(generation + (beginSegment && after ? 2UL : 1UL), reopened.BeginControllerRecoverySegment().Generation);
    }

    private static Action<AuthorityCommitBoundary> Fault(bool after) => boundary =>
    {
        if (boundary == (after ? AuthorityCommitBoundary.AfterCommit : AuthorityCommitBoundary.BeforeCommit))
            throw new IOException("Injected controller recovery transaction boundary failure.");
    };
}
