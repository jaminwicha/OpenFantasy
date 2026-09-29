using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ConsentedMaterialCarePolicyTests
{
    private const string ArchiveLimit = "The local world reached its development material and care archive envelope.";
    private const string FamilyRefusal = "World archive accounting must retain its explicit profile interpretation.";

    [Fact]
    public void Consented_archives_use_the_exact_joint_budget_and_never_extend_the_journal_budget()
    {
        var before = new WorldJournalUsage(42, 11, 1_024, 20, 20,
            MaterialCareArchiveCount: WorldJournalPolicy.MaximumMaterialCareArchiveCount - 1,
            MaterialCareArchiveBytes: WorldJournalPolicy.MaximumMaterialCareArchiveBytes - 700);
        WorldJournalAdvance accepted = WorldJournalPolicy.Advance(before, 43, 31, 2, 100, 200,
            profileVersion: 28, materialCareArchiveBytes: 700);
        var expected = new WorldJournalUsage(43, 13, 1_324, 20, 20,
            MaterialCareArchiveCount: WorldJournalPolicy.MaximumMaterialCareArchiveCount,
            MaterialCareArchiveBytes: WorldJournalPolicy.MaximumMaterialCareArchiveBytes);
        Assert.Equal(expected, accepted.Usage);
        Assert.False(accepted.WriteCheckpoint);
        Assert.Equal(WorldJournalPolicy.MaximumMaterialCareArchiveCount - 1, before.MaterialCareArchiveCount);

        foreach (WorldJournalUsage full in new[]
        {
            expected,
            expected with { MaterialCareArchiveCount = 0 },
            expected with { MaterialCareArchiveBytes = 0 }
        })
        {
            Assert.Equal(ArchiveLimit, Assert.Throws<WorldRecoveryLimitException>(() => WorldJournalPolicy.Advance(
                full, 44, 32, 1, 100, 200, profileVersion: 28, materialCareArchiveBytes: 1)).Message);
        }
        WorldJournalUsage withoutArchive = WorldJournalPolicy.Advance(expected, 44, 32, 1, 100, 200,
            profileVersion: 28).Usage;
        Assert.Equal(expected.MaterialCareArchiveCount, withoutArchive.MaterialCareArchiveCount);
        Assert.Equal(expected.MaterialCareArchiveBytes, withoutArchive.MaterialCareArchiveBytes);
        Assert.Equal(expected.AcceptedJournalBytes + 300, withoutArchive.AcceptedJournalBytes);

        var exhausted = new WorldJournalUsage(42, 11, WorldJournalPolicy.MaximumAcceptedJournalBytes, 20, 20);
        Assert.Equal("The local world reached its development recovery envelope.",
            Assert.Throws<WorldRecoveryLimitException>(() => WorldJournalPolicy.Advance(exhausted,
                43, 31, 1, 1, 1, profileVersion: 28, materialCareArchiveBytes: 1)).Message);
    }

    [Fact]
    public void Consented_checkpoint_scheduling_and_archive_family_refusals_keep_the_existing_contract()
    {
        foreach ((ulong sequence, ulong steps) in new[]
        {
            (WorldJournalPolicy.CheckpointStepInterval, WorldJournalPolicy.CheckpointStepInterval),
            (WorldJournalPolicy.CheckpointCommitInterval, 42UL)
        })
        {
            var before = new WorldJournalUsage(sequence - 1, 11, 1_024, 0, 0);
            WorldJournalAdvance result = WorldJournalPolicy.Advance(before, sequence, steps, 2, 100, 200,
                profileVersion: 28, materialCareArchiveBytes: 700);
            Assert.True(result.WriteCheckpoint);
            Assert.Equal((sequence, steps), (result.Usage.LastCheckpointSequence, result.Usage.LastCheckpointSteps));
            Assert.Equal((1UL, 700UL), (result.Usage.MaterialCareArchiveCount, result.Usage.MaterialCareArchiveBytes));
        }

        var initial = new WorldJournalUsage(0, 0, 0, 0, 0);
        foreach (ushort profile in new ushort[] { 26, 29, ushort.MaxValue })
        {
            Assert.Equal(FamilyRefusal, Assert.Throws<InvalidDataException>(() => WorldJournalPolicy.Advance(
                initial, 1, 1, 1, 10, 10, profileVersion: profile, materialCareArchiveBytes: 1)).Message);
        }
        foreach (WorldJournalUsage wrongFamily in new[]
        {
            initial with { ResourceArchiveCount = 1 },
            initial with { ResourceArchiveBytes = 1 }
        })
        {
            Assert.Equal(FamilyRefusal, Assert.Throws<InvalidDataException>(() => WorldJournalPolicy.Advance(
                wrongFamily, 1, 1, 1, 10, 10, profileVersion: 28)).Message);
        }
        foreach (ulong materialBytes in new[] { 0UL, 1UL })
        {
            Assert.Equal(FamilyRefusal, Assert.Throws<InvalidDataException>(() => WorldJournalPolicy.Advance(
                initial, 1, 1, 1, 10, 10, profileVersion: 28, resourceArchiveBytes: 1,
                materialCareArchiveBytes: materialBytes)).Message);
        }
    }
}
