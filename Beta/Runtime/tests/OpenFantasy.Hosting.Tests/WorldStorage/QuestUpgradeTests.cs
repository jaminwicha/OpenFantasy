using System.Diagnostics;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.WorldStorage.GatheringWorldFixture;
using static OpenFantasy.Hosting.Tests.WorldStorage.QuestUpgradeFixture;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class QuestUpgradeTests(ITestOutputHelper output)
{
    [Fact]
    public void CraftedProductionHistoryUpgradesThenPaysOneExactRewardAfterRestart()
    {
        using var gathering = new StorageFiles();
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        QuestUpgradeHistory history = CreateCraftedHistory(gathering.Path, source.Path);
        WorldSnapshot before = history.State;
        byte[] sourceBytes = File.ReadAllBytes(source.Path);
        byte[] gatheringBytes = File.ReadAllBytes(gathering.Path);
        WorldQuestMigration request = Request(before);
        var timer = Stopwatch.StartNew();
        WorldActionCommand accept;
        WorldActionCommand complete;
        byte[] acceptedReceipt;
        byte[] completedReceipt;
        byte[] final;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.UpgradeProductionToQuests(source.Path, destination.Path, request))
        {
            WorldInspection inspection = authority.InspectForHost();
            WorldSnapshot world = inspection.State;
            Assert.Equal((ushort)4, world.ProfileVersion);
            Assert.Equal(before.Revision + 1, world.Revision);
            Assert.Equal(before.AdvancingSteps, world.AdvancingSteps);
            Assert.Equal(before.Calendar, world.Calendar);
            Assert.Equal(before.Production!.ContentHash, world.Production!.ContentHash);
            Assert.Equal(before.Gathering!.ContentHash, world.Gathering!.ContentHash);
            Assert.Equal(before.Inventory.Custody.Lots, world.Inventory.Custody.Lots);
            Assert.Equal(before.Inventory.Sources, world.Inventory.Sources);
            Assert.Equal(before.Inventory.AdmittedUnitCapacity, world.Inventory.AdmittedUnitCapacity);
            Assert.Equal(before.Dialogues, world.Dialogues);
            Assert.Equal(1, inspection.Database.ForeignKeys);
            Assert.Equal((ulong)history.Commands.Count, inspection.Journal.CommandOutcomeCount);

            accept = Accept(world);
            WorldStepDecision accepted = authority.Commit(Batch(world, accept));
            Assert.Equal(WorldActionRejection.None, accepted.Receipts.Single().Rejection);
            world = accepted.Candidate;
            acceptedReceipt = WorldReceiptCodecV1.Encode(accepted.Receipts.Single());
            complete = Complete(world);
            WorldStepDecision completed = authority.Commit(Batch(world, complete));
            Assert.Equal(WorldActionRejection.None, completed.Receipts.Single().Rejection);
            world = completed.Candidate;
            completedReceipt = WorldReceiptCodecV1.Encode(completed.Receipts.Single());
            Assert.Equal(ActorQuestStatus.Completed, world.Quests!.Participations.Single().Status);
            Assert.Equal(200UL, world.Progression!.Currency.GetActorAccount(Id(10)).Balance);
            Assert.Equal(0UL, world.Progression.Currency.GetAccount(Reserve).Balance);
            Assert.Equal(3, world.Progression.GetActor(Id(10)).Level);
            Assert.Equal(38UL, world.Progression.GetActor(Id(10)).Experience);
            Assert.Equal(before.Calendar, world.Calendar);
            Assert.Equal(before.AdvancingSteps, world.AdvancingSteps);
            Assert.All(world.Inventory.Custody.Lots.Where(lot => lot.CustodianId == Project),
                lot => Assert.Equal(Reservation, lot.ReservedBy));
            Assert.Equal(6UL, world.Inventory.Custody.Lots.Where(lot => lot.CustodianId == Project)
                .Aggregate(0UL, (sum, lot) => sum + lot.Quantity));
            Assert.Equal(before.Production.ContentHash, world.Production!.ContentHash);
            final = WorldSnapshotCodec.Encode(world);
        }
        timer.Stop();
        Assert.Equal(sourceBytes, File.ReadAllBytes(source.Path));
        Assert.Equal(gatheringBytes, File.ReadAllBytes(gathering.Path));
        using (SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(destination.Path))
        {
            Assert.Equal(final, WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
            for (int index = 0; index < history.Commands.Count; index++)
            {
                Assert.Equal(history.Receipts[index], WorldReceiptCodecV1.Encode(reopened.FindReceipt(history.Commands[index], Delivery)!));
            }
            Assert.Equal(acceptedReceipt, WorldReceiptCodecV1.Encode(reopened.FindReceipt(accept, Delivery)!));
            Assert.Equal(completedReceipt, WorldReceiptCodecV1.Encode(reopened.FindReceipt(complete, Delivery)!));
            var changed = new WorldActionCommand(complete.CommandId, complete.PrincipalId, complete.OriginSessionId,
                complete.ActorId, complete.WorldId, complete.BranchId, complete.ExpectedActorRevision + 1, complete.Intent);
            Assert.Throws<AuthorityDispatchException>(() => reopened.FindReceipt(changed, Delivery));
            Assert.Equal(final, WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
        }
        using WorldDatabase oldDatabase = WorldDatabase.Open(source.Path, create: false);
        using WorldDatabase newDatabase = WorldDatabase.Open(destination.Path, create: false);
        Assert.Equal(3, oldDatabase.SchemaVersion);
        Assert.Equal(3, newDatabase.SchemaVersion);
        Assert.Equal(oldDatabase.ReadGenesis(), newDatabase.ReadGenesis());
        StoredWorldCommit[] oldRows = oldDatabase.ReadJournal().ToArray();
        StoredWorldCommit[] retainedRows = newDatabase.ReadJournal().Where(row => row.Sequence <= before.Revision).ToArray();
        Assert.Equal(oldRows.Length, retainedRows.Length);
        for (int index = 0; index < oldRows.Length; index++)
        {
            Assert.Equal(oldRows[index].AcceptedBatch, retainedRows[index].AcceptedBatch);
            Assert.Equal(oldRows[index].Outcomes, retainedRows[index].Outcomes);
            Assert.Equal(oldRows[index].BeforeHash, retainedRows[index].BeforeHash);
            Assert.Equal(oldRows[index].AfterHash, retainedRows[index].AfterHash);
        }
        StoredWorldCommit migration = newDatabase.ReadJournal(before.Revision).First();
        Assert.Equal(WorldCommitKind.ProfileMigration, migration.Kind);
        Assert.Equal(WorldQuestMigrationCodecV1.Encode(request), migration.AcceptedBatch);
        Assert.Equal(oldDatabase.ReadIdentities().ToArray(), newDatabase.ReadIdentities()
            .Where(identity => identity.Sequence <= before.Revision).ToArray());
        Assert.Contains(newDatabase.ReadIdentities(), identity => identity.Identity == Project && identity.Kind == WorldIdentityKind.SpawnedEntity);
        output.WriteLine("Tiny crafted-save quest upgrade plus acceptance/completion: sourceCommits={0}, elapsedMs={1:F3}, sourceBytes={2}, destinationBytes={3}",
            before.Revision, timer.Elapsed.TotalMilliseconds, sourceBytes.Length, new FileInfo(destination.Path).Length);
    }

    [Theory]
    [InlineData((int)WorldQuestUpgradeBoundary.AfterBackup)]
    [InlineData((int)WorldQuestUpgradeBoundary.BeforeCommit)]
    [InlineData((int)WorldQuestUpgradeBoundary.AfterCommit)]
    [InlineData((int)WorldQuestUpgradeBoundary.BeforePublish)]
    [InlineData((int)WorldQuestUpgradeBoundary.AfterPublish)]
    public void InterruptedQuestUpgradePreservesSourceAndPublishesAtMostOneCandidate(int value)
    {
        var boundary = (WorldQuestUpgradeBoundary)value;
        using var gathering = new StorageFiles();
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        WorldSnapshot state = CreateCraftedHistory(gathering.Path, source.Path).State;
        byte[] before = File.ReadAllBytes(source.Path);

        Assert.Throws<IOException>(() => SqliteWorldAuthority.UpgradeProductionToQuests(source.Path, destination.Path,
            Request(state), at => { if (at == boundary) throw new IOException("injected quest upgrade interruption"); }));

        Assert.Equal(before, File.ReadAllBytes(source.Path));
        Assert.Equal(boundary == WorldQuestUpgradeBoundary.AfterPublish, File.Exists(destination.Path));
        using SqliteWorldAuthority original = SqliteWorldAuthority.OpenExisting(source.Path);
        Assert.Equal(state.ContentHash, original.InspectForHost().State.ContentHash);
        if (File.Exists(destination.Path))
        {
            using SqliteWorldAuthority published = SqliteWorldAuthority.OpenExisting(destination.Path);
            Assert.Equal((ushort)4, published.InspectForHost().State.ProfileVersion);
            Assert.Equal(state.Revision + 1, published.InspectForHost().State.Revision);
            Assert.Equal(state.AdvancingSteps, published.InspectForHost().State.AdvancingSteps);
        }
    }

    [Theory]
    [InlineData("-wal", false)]
    [InlineData("-shm", false)]
    [InlineData("-journal", false)]
    [InlineData("-wal", true)]
    [InlineData("-shm", true)]
    [InlineData("-journal", true)]
    public void QuestPublicationPreservesInitiallyPresentAndLateDestinationSidecars(string suffix, bool late)
    {
        using var gathering = new StorageFiles();
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        WorldSnapshot state = CreateCraftedHistory(gathering.Path, source.Path).State;
        byte[] before = File.ReadAllBytes(source.Path);
        byte[] marker = "existing destination recovery data"u8.ToArray();
        if (!late) File.WriteAllBytes(destination.Path + suffix, marker);

        Assert.Throws<IOException>(() => SqliteWorldAuthority.UpgradeProductionToQuests(source.Path, destination.Path,
            Request(state), at =>
            {
                if (late && at == WorldQuestUpgradeBoundary.BeforePublish) File.WriteAllBytes(destination.Path + suffix, marker);
            }));

        Assert.Equal(marker, File.ReadAllBytes(destination.Path + suffix));
        Assert.Equal(before, File.ReadAllBytes(source.Path));
        Assert.False(File.Exists(destination.Path));
    }

    [Fact]
    public void FreshQuestGenesisUsesSchemaThreeWithoutInventingMigrationRows()
    {
        using var files = new StorageFiles();
        WorldSnapshot basis = GatheringWorldFixture.Create();
        var production = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, basis.AdmissionVersion,
            basis.Calendar, 0, 0, gathering: basis.Gathering, production: ProductionUpgradeFixture.Admission(basis));
        WorldSnapshot added = new WorldQuestMigrationProcessor().Decide(production, Request(production)).Candidate;
        var genesis = new WorldSnapshot(added.Inventory, added.Space, added.Residents, added.AdmissionVersion,
            added.Calendar, 0, 0, gathering: added.Gathering, production: added.Production,
            progression: added.Progression, quests: added.Quests);
        using (SqliteWorldAuthority.CreateNew(files.Path, genesis)) { }
        using (SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(genesis.ContentHash, reopened.InspectForHost().State.ContentHash);
        }
        using WorldDatabase database = WorldDatabase.Open(files.Path, create: false);
        Assert.Equal(3, database.SchemaVersion);
        Assert.Empty(database.ReadJournal());
        Assert.Empty(database.ReadIdentities());
    }
}
