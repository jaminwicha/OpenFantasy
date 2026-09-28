using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class MaterialCareJournalPolicyTests
{
    [Fact]
    public void A_joint_archive_keeps_separate_accounting_and_preserves_checkpoint_progress()
    {
        var before = new WorldJournalUsage(42, 11, 1_024, 20, 20,
            MaterialCareArchiveCount: 2, MaterialCareArchiveBytes: 500);

        WorldJournalAdvance result = WorldJournalPolicy.Advance(before, 43, 31, 2, 100, 200,
            profileVersion: 27, materialCareArchiveBytes: 700);

        Assert.Equal(new WorldJournalUsage(43, 13, 1_324, 20, 20,
            MaterialCareArchiveCount: 3, MaterialCareArchiveBytes: 1_200), result.Usage);
        Assert.False(result.WriteCheckpoint);
        Assert.Equal(2UL, before.MaterialCareArchiveCount);
    }

    [Fact]
    public void The_last_archive_slot_and_byte_are_usable_but_neither_budget_can_be_exceeded()
    {
        var before = new WorldJournalUsage(42, 11, 1_024, 20, 20,
            MaterialCareArchiveCount: WorldJournalPolicy.MaximumMaterialCareArchiveCount - 1,
            MaterialCareArchiveBytes: WorldJournalPolicy.MaximumMaterialCareArchiveBytes - 700);
        WorldJournalUsage full = WorldJournalPolicy.Advance(before, 43, 31, 1, 100, 200,
            profileVersion: 27, materialCareArchiveBytes: 700).Usage;

        Assert.Equal(WorldJournalPolicy.MaximumMaterialCareArchiveCount, full.MaterialCareArchiveCount);
        Assert.Equal(WorldJournalPolicy.MaximumMaterialCareArchiveBytes, full.MaterialCareArchiveBytes);
        Assert.Throws<WorldRecoveryLimitException>(() => WorldJournalPolicy.Advance(full, 44, 32, 1, 100, 200,
            profileVersion: 27, materialCareArchiveBytes: 1));
        Assert.Throws<WorldRecoveryLimitException>(() => WorldJournalPolicy.Advance(
            full with { MaterialCareArchiveCount = 0 }, 44, 32, 1, 100, 200,
            profileVersion: 27, materialCareArchiveBytes: 1));
        Assert.Throws<WorldRecoveryLimitException>(() => WorldJournalPolicy.Advance(
            full with { MaterialCareArchiveBytes = 0 }, 44, 32, 1, 100, 200,
            profileVersion: 27, materialCareArchiveBytes: 1));

        WorldJournalUsage unchangedArchive = WorldJournalPolicy.Advance(full, 44, 32, 1, 100, 200,
            profileVersion: 27).Usage;
        Assert.Equal(full.MaterialCareArchiveCount, unchangedArchive.MaterialCareArchiveCount);
        Assert.Equal(full.MaterialCareArchiveBytes, unchangedArchive.MaterialCareArchiveBytes);
    }

    [Fact]
    public void Archive_families_cannot_be_substituted_or_combined()
    {
        var initial = new WorldJournalUsage(0, 0, 0, 0, 0);
        Assert.Throws<InvalidDataException>(() => WorldJournalPolicy.Advance(initial, 1, 1, 1, 10, 10,
            profileVersion: 26, materialCareArchiveBytes: 1));
        Assert.Throws<InvalidDataException>(() => WorldJournalPolicy.Advance(initial, 1, 1, 1, 10, 10,
            profileVersion: 27, resourceArchiveBytes: 1));
        Assert.Throws<InvalidDataException>(() => WorldJournalPolicy.Advance(initial, 1, 1, 1, 10, 10,
            profileVersion: 27, resourceArchiveBytes: 1, materialCareArchiveBytes: 1));
        Assert.Throws<InvalidDataException>(() => WorldJournalPolicy.Advance(
            initial with { ResourceArchiveCount = 1, ResourceArchiveBytes = 1 }, 1, 1, 1, 10, 10,
            profileVersion: 27));
        Assert.Throws<InvalidDataException>(() => WorldJournalPolicy.Advance(
            initial with { MaterialCareArchiveCount = 1, MaterialCareArchiveBytes = 1 }, 1, 1, 1, 10, 10,
            profileVersion: 26));
    }

    [Fact]
    public void Coordinated_archives_do_not_extend_the_existing_journal_budget()
    {
        var initial = new WorldJournalUsage(0, 0, WorldJournalPolicy.MaximumAcceptedJournalBytes, 0, 0);
        Assert.Throws<WorldRecoveryLimitException>(() => WorldJournalPolicy.Advance(initial, 1, 1, 1, 1, 1,
            profileVersion: 27, materialCareArchiveBytes: 1));
    }
}
