using OpenFantasy.Client;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class FarmConstructionClientTests
{
    [Fact]
    public void Fresh_construction_save_starts_before_quest_or_work_with_one_explicit_tool()
    {
        using var files = new StorageFiles();
        WorldSnapshot gathering = GatheringFarmAdmission.CreateEmbedded("fresh construction", "Traveler");
        using SqliteWorldAuthority authority = FarmConstructionSaveOpener.Open(files.Path, gathering, null, null, null);
        WorldSnapshot state = authority.InspectForHost().State;
        Assert.Equal((ushort)5, state.ProfileVersion);
        Assert.Equal(0UL, state.Revision);
        Assert.Equal(0UL, state.AdvancingSteps);
        Assert.Empty(state.Quests!.Participations);
        Assert.Empty(state.Production!.Batches);
        Assert.Empty(state.Space.Structures);
        Assert.All(state.Construction!.Projects, project =>
        {
            Assert.Equal(0UL, project.Progress.CompletedUnits);
            Assert.False(project.IsCompleted);
            var tool = Assert.Single(state.Inventory.Custody.Lots,
                lot => lot.Definition == project.Definition.RequiredTool.Item);
            Assert.Equal(1UL, tool.Quantity);
            Assert.Equal(state.GetResident(project.BuilderActorId).InventoryCustodianId, tool.CustodianId);
        });
    }

    [Fact]
    public void Actual_completed_quest_source_is_retained_by_the_client_upgrade()
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        var fixture = new ConstructionUpgradeFixture();
        WorldSnapshot before = fixture.CreateSource(source.Path, completeQuest: true);
        byte[] sourceBytes = File.ReadAllBytes(source.Path);
        WorldSnapshot gathering = GatheringFarmAdmission.CreateEmbedded("construction-recovery", "Traveler");
        using (SqliteWorldAuthority authority = FarmConstructionSaveOpener.Open(destination.Path, gathering, source.Path, null, null))
        {
            WorldSnapshot current = authority.InspectForHost().State;
            Assert.Equal((ushort)5, current.ProfileVersion);
            Assert.Equal(QuestCodecV1.Encode(before.Quests!), QuestCodecV1.Encode(current.Quests!));
            Assert.Equal(before.Calendar, current.Calendar);
            Assert.Equal(before.Dialogues, current.Dialogues);
            Assert.All(current.Construction!.Projects, project => Assert.Equal(0UL, project.Progress.CompletedUnits));
        }
        Assert.Equal(sourceBytes, File.ReadAllBytes(source.Path));
    }

    [Fact]
    public void Existing_gathering_source_gets_each_preserved_intermediate_before_construction()
    {
        using var gatheringFile = new StorageFiles();
        using var productionFile = new StorageFiles();
        using var questFile = new StorageFiles();
        using var constructionFile = new StorageFiles();
        WorldSnapshot gathering = GatheringFarmAdmission.CreateEmbedded("preserved chain", "Traveler");
        using (SqliteWorldAuthority.CreateNew(gatheringFile.Path, gathering)) { }
        byte[] original = File.ReadAllBytes(gatheringFile.Path);
        using (SqliteWorldAuthority result = FarmConstructionSaveOpener.Open(constructionFile.Path, gathering,
            questFile.Path, productionFile.Path, gatheringFile.Path))
        {
            Assert.Equal((ushort)5, result.InspectForHost().State.ProfileVersion);
        }
        Assert.Equal(original, File.ReadAllBytes(gatheringFile.Path));
        using SqliteWorldAuthority production = SqliteWorldAuthority.OpenExisting(productionFile.Path);
        using SqliteWorldAuthority quests = SqliteWorldAuthority.OpenExisting(questFile.Path);
        Assert.Equal((ushort)3, production.InspectForHost().State.ProfileVersion);
        Assert.Equal((ushort)4, quests.InspectForHost().State.ProfileVersion);
    }

    [Fact]
    public void Missing_intermediate_destination_cannot_discard_existing_earlier_progress()
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        WorldSnapshot gathering = GatheringFarmAdmission.CreateEmbedded("missing intermediate", "Traveler");
        using (SqliteWorldAuthority.CreateNew(source.Path, gathering)) { }
        byte[] before = File.ReadAllBytes(source.Path);
        Assert.Throws<ArgumentException>(() => FarmConstructionSaveOpener.Open(destination.Path, gathering,
            null, null, source.Path));
        Assert.Equal(before, File.ReadAllBytes(source.Path));
        Assert.False(File.Exists(destination.Path));
    }

    [Theory]
    [InlineData("-wal")]
    [InlineData("-shm")]
    [InlineData("-journal")]
    public void Orphaned_source_recovery_files_are_not_replaced_by_a_fresh_world(string suffix)
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        WorldSnapshot gathering = GatheringFarmAdmission.CreateEmbedded("orphaned predecessor", "Traveler");
        byte[] marker = "retained source recovery data"u8.ToArray();
        File.WriteAllBytes(source.Path + suffix, marker);
        Assert.Throws<InvalidDataException>(() => FarmConstructionSaveOpener.Open(destination.Path, gathering,
            source.Path, null, null));
        Assert.Equal(marker, File.ReadAllBytes(source.Path + suffix));
        Assert.False(File.Exists(destination.Path));
    }

    [Fact]
    public void Existing_construction_destination_wins_over_unused_incomplete_predecessors()
    {
        using var destination = new StorageFiles();
        using var old = new StorageFiles();
        WorldSnapshot gathering = GatheringFarmAdmission.CreateEmbedded("existing destination", "Traveler");
        using (FarmConstructionSaveOpener.Open(destination.Path, gathering, null, null, null)) { }
        File.WriteAllText(old.Path + "-wal", "unused old recovery bytes");
        byte[] before = File.ReadAllBytes(destination.Path);
        using (SqliteWorldAuthority authority = FarmConstructionSaveOpener.Open(destination.Path, gathering,
            old.Path, null, null))
        {
            Assert.Equal((ushort)5, authority.InspectForHost().State.ProfileVersion);
        }
        Assert.Equal(before, File.ReadAllBytes(destination.Path));
        Assert.Equal("unused old recovery bytes", File.ReadAllText(old.Path + "-wal"));
    }
}
