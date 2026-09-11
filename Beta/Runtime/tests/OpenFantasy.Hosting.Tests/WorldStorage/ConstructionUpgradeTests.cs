using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ConstructionUpgradeTests
{
    [Fact]
    public void Completed_quest_upgrades_without_resetting_reward_stock_dialogue_or_history()
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        var fixture = new ConstructionUpgradeFixture();
        WorldSnapshot before = fixture.CreateSource(source.Path, completeQuest: true);
        byte[] sourceBytes = File.ReadAllBytes(source.Path);
        WorldConstructionMigration request = fixture.Request(before);
        byte[] upgraded;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.UpgradeQuestsToConstruction(source.Path,
            destination.Path, request))
        {
            WorldSnapshot state = authority.InspectForHost().State;
            Assert.Equal((ushort)5, state.ProfileVersion);
            Assert.Equal(before.Revision + 1, state.Revision);
            Assert.Equal(before.AdvancingSteps, state.AdvancingSteps);
            Assert.Equal(before.Calendar, state.Calendar);
            Assert.Equal(WorldSnapshotCodec.Encode(before),
                WorldSnapshotCodec.Encode(authority.ReadRecordedStateForHost(before.Revision).State));
            Assert.Equal(state.ContentHash, authority.ReadRecordedStateForHost(state.Revision).StateHash);
            // Reopening reconstructs immutable class instances; compare every public value, not object identity.
            Assert.Equivalent(before.Residents, state.Residents, strict: true);
            Assert.Equivalent(before.Dialogues, state.Dialogues, strict: true);
            Assert.Equal(QuestCodecV1.Encode(before.Quests!), QuestCodecV1.Encode(state.Quests!));
            Assert.Equal(ProgressionCodecV1.Encode(before.Progression!), ProgressionCodecV1.Encode(state.Progression!));
            Assert.Equal(before.Gathering!.ContentHash, state.Gathering!.ContentHash);
            Assert.All(before.Inventory.Custody.Lots, lot => Assert.Equivalent(lot,
                state.Inventory.Custody.Lots.Single(current => current.LotId == lot.LotId), strict: true));
            Assert.All(before.Inventory.Sources, entry => Assert.Equivalent(entry,
                state.Inventory.Sources.Single(current => current.SourceId == entry.SourceId), strict: true));
            Assert.Equal(before.Inventory.AdmittedUnitCapacity + 1, state.Inventory.AdmittedUnitCapacity);
            Assert.Equal(before.Inventory.Sources.Count + 1, state.Inventory.Sources.Count);
            Assert.Equal(before.Production!.Batches, state.Production!.Batches);
            Assert.Equal(before.Production.Lots, state.Production.Lots);
            Assert.All(before.Production.Recipes, recipe => Assert.Contains(state.Production.Recipes,
                current => current.DefinitionId == recipe.DefinitionId && current.Version == recipe.Version));
            Assert.NotNull(state.Construction);
            upgraded = WorldSnapshotCodec.Encode(state);
        }
        Assert.Equal(sourceBytes, File.ReadAllBytes(source.Path));
        using (SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(destination.Path))
        {
            Assert.Equal(upgraded, WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
        }
        using WorldDatabase original = WorldDatabase.Open(source.Path, create: false);
        using WorldDatabase continuation = WorldDatabase.Open(destination.Path, create: false);
        Assert.Equal(3, original.SchemaVersion);
        Assert.Equal(3, continuation.SchemaVersion);
        Assert.Equal(original.ReadGenesis(), continuation.ReadGenesis());
        StoredWorldCommit[] oldRows = original.ReadJournal().ToArray();
        StoredWorldCommit[] retainedRows = continuation.ReadJournal().Where(row => row.Sequence <= before.Revision).ToArray();
        Assert.Equal(oldRows.Length, retainedRows.Length);
        for (int index = 0; index < oldRows.Length; index++)
        {
            Assert.Equal(oldRows[index].AcceptedBatch, retainedRows[index].AcceptedBatch);
            Assert.Equal(oldRows[index].Outcomes, retainedRows[index].Outcomes);
            Assert.Equal(oldRows[index].BeforeHash, retainedRows[index].BeforeHash);
            Assert.Equal(oldRows[index].AfterHash, retainedRows[index].AfterHash);
        }
        Assert.Equal(original.ReadIdentities().ToArray(), continuation.ReadIdentities()
            .Where(identity => identity.Sequence <= before.Revision).ToArray());
        Assert.Equal(WorldConstructionMigrationCodecV1.Encode(request), continuation.ReadJournal(before.Revision).Single().AcceptedBatch);
    }

    [Theory]
    [InlineData((int)WorldConstructionUpgradeBoundary.AfterBackup)]
    [InlineData((int)WorldConstructionUpgradeBoundary.BeforeCommit)]
    [InlineData((int)WorldConstructionUpgradeBoundary.AfterCommit)]
    [InlineData((int)WorldConstructionUpgradeBoundary.BeforePublish)]
    [InlineData((int)WorldConstructionUpgradeBoundary.AfterPublish)]
    public void Interrupted_publication_retains_the_source_and_publishes_at_most_one_continuation(int boundaryValue)
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        var fixture = new ConstructionUpgradeFixture();
        WorldSnapshot before = fixture.CreateSource(source.Path);
        byte[] sourceBytes = File.ReadAllBytes(source.Path);
        var boundary = (WorldConstructionUpgradeBoundary)boundaryValue;
        Assert.Throws<IOException>(() => SqliteWorldAuthority.UpgradeQuestsToConstruction(source.Path, destination.Path,
            fixture.Request(before), at => { if (at == boundary) throw new IOException("injected construction interruption"); }));
        Assert.Equal(sourceBytes, File.ReadAllBytes(source.Path));
        Assert.Equal(boundary == WorldConstructionUpgradeBoundary.AfterPublish, File.Exists(destination.Path));
        using SqliteWorldAuthority original = SqliteWorldAuthority.OpenExisting(source.Path);
        Assert.Equal(before.ContentHash, original.InspectForHost().State.ContentHash);
        if (File.Exists(destination.Path))
        {
            using SqliteWorldAuthority published = SqliteWorldAuthority.OpenExisting(destination.Path);
            WorldSnapshot state = published.InspectForHost().State;
            Assert.Equal((ushort)5, state.ProfileVersion);
            Assert.Equal(before.Revision + 1, state.Revision);
            Assert.Equal(before.AdvancingSteps, state.AdvancingSteps);
        }
    }

    [Theory]
    [InlineData("-wal", false)]
    [InlineData("-shm", false)]
    [InlineData("-journal", false)]
    [InlineData("-wal", true)]
    [InlineData("-shm", true)]
    [InlineData("-journal", true)]
    public void Existing_and_late_destination_recovery_files_are_preserved(string suffix, bool late)
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        var fixture = new ConstructionUpgradeFixture();
        WorldSnapshot before = fixture.CreateSource(source.Path);
        byte[] original = File.ReadAllBytes(source.Path);
        byte[] marker = "unrelated destination recovery data"u8.ToArray();
        if (!late) File.WriteAllBytes(destination.Path + suffix, marker);
        Assert.Throws<IOException>(() => SqliteWorldAuthority.UpgradeQuestsToConstruction(source.Path, destination.Path,
            fixture.Request(before), at =>
            {
                if (late && at == WorldConstructionUpgradeBoundary.BeforePublish)
                {
                    File.WriteAllBytes(destination.Path + suffix, marker);
                }
            }));
        Assert.Equal(original, File.ReadAllBytes(source.Path));
        Assert.Equal(marker, File.ReadAllBytes(destination.Path + suffix));
        Assert.False(File.Exists(destination.Path));
    }
}
