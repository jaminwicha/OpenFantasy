using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

public sealed class ResourceArchivePolicyTests
{
    [Fact]
    public void Archive_capacity_is_charged_separately_and_refuses_before_exceeding_either_bound()
    {
        var usage = new WorldJournalUsage(0, 0, 0, 0, 0,
            WorldJournalPolicy.MaximumResourceArchiveCount - 1, WorldJournalPolicy.MaximumResourceArchiveBytes - 123);
        var exact = WorldJournalPolicy.Advance(usage, 1, 0, 1, 11, 13, profileVersion: 25, resourceArchiveBytes: 123).Usage;
        Assert.Equal(WorldJournalPolicy.MaximumResourceArchiveCount, exact.ResourceArchiveCount);
        Assert.Equal(WorldJournalPolicy.MaximumResourceArchiveBytes, exact.ResourceArchiveBytes);
        Assert.Equal(24UL, exact.AcceptedJournalBytes);
        Assert.Equal(1UL, exact.CommandOutcomeCount);
        Assert.Throws<WorldRecoveryLimitException>(() => WorldJournalPolicy.Advance(usage, 1, 0, 1, 11, 13,
            profileVersion: 25, resourceArchiveBytes: 124));
        Assert.Throws<WorldRecoveryLimitException>(() => WorldJournalPolicy.Advance(usage with
            { ResourceArchiveCount = WorldJournalPolicy.MaximumResourceArchiveCount }, 1, 0, 1, 11, 13,
            profileVersion: 25, resourceArchiveBytes: 1));
        // Commands with no archive preserve those counters and still obey the original journal limits.
        var noArchive = WorldJournalPolicy.Advance(exact, 2, 0, 1, 11, 13, profileVersion: 25).Usage;
        Assert.Equal(exact.ResourceArchiveCount, noArchive.ResourceArchiveCount);
        Assert.Equal(exact.ResourceArchiveBytes, noArchive.ResourceArchiveBytes);
        Assert.Equal(48UL, noArchive.AcceptedJournalBytes);
        Assert.Throws<WorldRecoveryLimitException>(() => WorldJournalPolicy.Advance(usage with
            { AcceptedJournalBytes = WorldJournalPolicy.MaximumAcceptedJournalBytes }, 1, 0, 1, 11, 13,
            profileVersion: 25, resourceArchiveBytes: 123));
    }
}
