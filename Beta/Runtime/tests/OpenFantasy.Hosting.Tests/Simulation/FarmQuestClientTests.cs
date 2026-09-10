using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class FarmQuestClientTests
{
    [Fact]
    public void ExistingGatheringSourceWithoutIntermediateDestinationCannotBecomeFreshQuestGenesis()
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        WorldSnapshot genesis = FarmGenesis();
        using (SqliteWorldAuthority.CreateNew(source.Path, genesis)) { }
        byte[] before = File.ReadAllBytes(source.Path);

        Assert.Throws<ArgumentException>(() => FarmQuestSaveOpener.Open(destination.Path, genesis,
            productionSourcePath: null, gatheringSourcePath: source.Path));

        Assert.Equal(before, File.ReadAllBytes(source.Path));
        Assert.False(File.Exists(destination.Path));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(destination.Path)!));
    }

    [Theory]
    [InlineData("-wal")]
    [InlineData("-shm")]
    [InlineData("-journal")]
    public void IncompleteRetainedSourceIsNotSilentlyReplacedWithNewGenesis(string suffix)
    {
        using var orphan = new StorageFiles();
        using var intermediate = new StorageFiles();
        using var destination = new StorageFiles();
        byte[] marker = "retained source recovery information"u8.ToArray();
        File.WriteAllBytes(orphan.Path + suffix, marker);

        Assert.Throws<InvalidDataException>(() => FarmQuestSaveOpener.Open(destination.Path, FarmGenesis(),
            intermediate.Path, orphan.Path));

        Assert.Equal(marker, File.ReadAllBytes(orphan.Path + suffix));
        Assert.False(File.Exists(orphan.Path));
        Assert.False(File.Exists(intermediate.Path));
        Assert.False(File.Exists(destination.Path));
    }

    [Fact]
    public void ActualProductionProgressIsCopiedAndExistingQuestDestinationTakesPrecedence()
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        using var ignored = new StorageFiles();
        WorldSnapshot gathering = FarmGenesis();
        WorldSnapshot production = FarmSaveOpener.ProductionGenesis(gathering);
        using (SqliteWorldAuthority original = SqliteWorldAuthority.CreateNew(source.Path, production))
        {
            production = original.Commit(Batch(production, 98_001)).Candidate;
        }
        byte[] before = File.ReadAllBytes(source.Path);
        byte[] questBytes;
        using (SqliteWorldAuthority quest = FarmQuestSaveOpener.Open(destination.Path, gathering, source.Path, null))
        {
            WorldSnapshot state = quest.InspectForHost().State;
            Assert.Equal((ushort)4, state.ProfileVersion);
            Assert.Equal(production.Revision + 1, state.Revision);
            Assert.Equal(production.Calendar, state.Calendar);
            Assert.Equal(production.AdvancingSteps, state.AdvancingSteps);
            Assert.Equal(production.Inventory.Custody.Lots, state.Inventory.Custody.Lots);
            Assert.Equal(production.Production!.ContentHash, state.Production!.ContentHash);
            Assert.Equal(production.Gathering!.ContentHash, state.Gathering!.ContentHash);
            questBytes = WorldSnapshotCodec.Encode(state);
        }
        Assert.Equal(before, File.ReadAllBytes(source.Path));
        File.WriteAllBytes(ignored.Path + "-wal", "unused older source"u8.ToArray());
        using (SqliteWorldAuthority selected = FarmQuestSaveOpener.Open(destination.Path, gathering, ignored.Path, null))
        {
            Assert.Equal(questBytes, WorldSnapshotCodec.Encode(selected.InspectForHost().State));
        }
        using (SqliteWorldAuthority selected = FarmQuestSaveOpener.Open(destination.Path, gathering,
            productionSourcePath: null, gatheringSourcePath: source.Path))
        {
            Assert.Equal(questBytes, WorldSnapshotCodec.Encode(selected.InspectForHost().State));
        }
        Assert.Equal("unused older source"u8.ToArray(), File.ReadAllBytes(ignored.Path + "-wal"));
        Assert.Equal(before, File.ReadAllBytes(source.Path));
    }

    [Fact]
    public void ClientAcceptsExplicitlyThenSelectsExactlySixAvailableRawUnits()
    {
        WorldSnapshot world = SelectionWorld();
        world = Apply(world, 98_101, new SpeakToActorIntent(Id(11), 0));
        LocalWorldView offered = new(world, Id(10));
        var acceptance = Assert.IsType<AcceptDeliveryQuestIntent>(FarmQuestActions.Request(offered));
        Assert.Equal(0UL, acceptance.ExpectedParticipationRevision);
        world = Apply(world, 98_102, acceptance);
        LocalWorldView accepted = new(world, Id(10));

        var completion = Assert.IsType<CompleteDeliveryQuestIntent>(FarmQuestActions.Request(accepted));

        Assert.Equal(10UL, accepted.Quests.Single().EligibleQuantity);
        Assert.Equal(6UL, completion.Selections.Aggregate(0UL, (sum, selection) => sum + selection.Quantity));
        Assert.Equal(new[] { Id(72), Id(73) }, completion.Selections.Select(selection => selection.LotId));
        Assert.Equal(new ulong[] { 5, 1 }, completion.Selections.Select(selection => selection.Quantity));
        Assert.DoesNotContain(completion.Selections, selection => selection.LotId == Id(70));
        Assert.Equal(accepted.PlayerInventory.Revision, completion.ExpectedInventoryRevision);
        Assert.Equal(1UL, completion.ExpectedParticipationRevision);

        world = Apply(world, 98_103, new CloseDialogueIntent(world.Dialogues.Single().DialogueId));
        Assert.Throws<InvalidOperationException>(() => FarmQuestActions.Request(new LocalWorldView(world, Id(10))));
    }

    private static WorldSnapshot SelectionWorld()
    {
        WorldSnapshot gathering = GatheringWorldFixture.Create();
        CountedAuthorityState prior = gathering.Inventory;
        CountedItemDefinition material = prior.Sources.Single(source => source.SourceId == Id(71)).Definition;
        CountedSourceLot[] wood =
        [
            new(Id(70), Id(60), material, new SourceInterval(Id(71), 0, 4), 0, LotProtection.Favorite),
            new(Id(72), Id(60), material, new SourceInterval(Id(71), 4, 9), 0),
            new(Id(73), Id(60), material, new SourceInterval(Id(71), 9, 14), 0)
        ];
        var inventory = new CountedAuthorityState(new CustodySnapshot(prior.WorldId, prior.BranchId,
            prior.Custody.Revision, prior.Custody.Storages,
            prior.Custody.Lots.Where(lot => lot.Source.SourceId != Id(71)).Concat(wood)),
            prior.AdmittedUnitCapacity + 8, prior.Actors, prior.Controls, prior.Policies,
            prior.Sources.Select(source => source.SourceId == Id(71) ? new AdmittedCountedSource(Id(71), material, 14) : source),
            prior.ReservationIds);
        gathering = new WorldSnapshot(inventory, gathering.Space, gathering.Residents, gathering.AdmissionVersion,
            gathering.Calendar, gathering.Revision, gathering.AdvancingSteps, gathering: gathering.Gathering);
        WorldSnapshot production = new WorldProductionMigrationProcessor().Decide(gathering,
            ProductionUpgradeFixture.Request(gathering)).Candidate;
        return new WorldQuestMigrationProcessor().Decide(production, QuestUpgradeFixture.Request(production)).Candidate;
    }

    private static WorldSnapshot FarmGenesis() => GatheringFarmAdmission.CreateEmbedded("aldermere-first-walk-v1", "Traveler");

    private static WorldStepBatch Batch(WorldSnapshot state, ulong cause, WorldActionCommand[]? actions = null) =>
        new(Id(cause), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
            state.HasReadingHold ? WorldBatchKind.HeldInteraction : WorldBatchKind.AdvancingStep,
            [], actions ?? [], state.ProfileVersion);

    private static WorldSnapshot Apply(WorldSnapshot world, ulong commandId, WorldActionIntent intent)
    {
        var command = new WorldActionCommand(Id(commandId), Delivery.PrincipalId, Delivery.SessionId, Id(10),
            world.WorldId, world.BranchId, world.Space.GetActor(Id(10)).Revision, intent);
        return new WorldStepProcessor().Decide(world, Batch(world, commandId + 1000, [command])).Candidate;
    }
}
