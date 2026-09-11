using System.Diagnostics;
using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.WorldStorage.GatheringWorldFixture;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;
using static OpenFantasy.Hosting.Tests.WorldStorage.ProductionUpgradeFixture;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class ProductionUpgradeTests(ITestOutputHelper output)
{
    [Fact]
    public void RealGatheringHistoryAndHeldDialogueSurviveCopyUpgradeWithoutElapsedTime()
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        WorldSnapshot current = Create();
        var oldCommands = new List<WorldActionCommand>();
        var oldReceipts = new List<byte[]>();
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.CreateNew(source.Path, current))
        {
            for (ulong swing = 0; swing < 5; swing++)
            {
                while (current.Gathering!.GetActorWork(Id(10)).Effort < 400)
                {
                    current = authority.Commit(Batch(current)).Candidate;
                }
                Commit(Swing(current, swing));
            }
            Commit(Give(current, 91_000, 2));
            Commit(new WorldActionCommand(Id(91_001), Delivery.PrincipalId, Delivery.SessionId, Id(10),
                current.WorldId, current.BranchId, current.Space.GetActor(Id(10)).Revision,
                new SpeakToActorIntent(Id(11), current.Space.GetActor(Id(11)).Revision)));

            void Commit(WorldActionCommand command)
            {
                WorldStepDecision decision = authority.Commit(Batch(current, command));
                Assert.Equal(CommandReceiptStatus.Applied, decision.Receipts.Single().Status);
                current = decision.Candidate;
                oldCommands.Add(command);
                oldReceipts.Add(WorldReceiptCodecV1.Encode(decision.Receipts.Single()));
            }
        }
        Assert.True(current.HasReadingHold);
        Assert.Equal(0, Node(current).RemainingSwings);
        byte[] originalFile = File.ReadAllBytes(source.Path);
        byte[] before = WorldSnapshotCodec.Encode(current);
        WorldProductionMigration request = Request(current);
        var timer = Stopwatch.StartNew();
        using (SqliteWorldAuthority upgraded = SqliteWorldAuthority.UpgradeGatheringToProduction(source.Path, destination.Path, request))
        {
            WorldSnapshot candidate = upgraded.InspectForHost().State;
            Assert.Equal((ushort)3, candidate.ProfileVersion);
            Assert.Equal(current.Revision + 1, candidate.Revision);
            Assert.Equal(current.AdvancingSteps, candidate.AdvancingSteps);
            Assert.Equal(current.Calendar, candidate.Calendar);
            Assert.Equal(before, WorldSnapshotCodec.Encode(upgraded.ReadRecordedStateForHost(current.Revision).State));
            Assert.Equal(candidate.ContentHash, upgraded.ReadRecordedStateForHost(candidate.Revision).StateHash);
            Assert.Equal(current.Inventory.ContentHash, candidate.Inventory.ContentHash);
            Assert.Equal(current.Gathering!.ContentHash, candidate.Gathering!.ContentHash);
            var oldFields = new WorldSnapshot(candidate.Inventory, candidate.Space, candidate.Residents,
                current.AdmissionVersion, candidate.Calendar, current.Revision, candidate.AdvancingSteps,
                candidate.Dialogues, candidate.Gathering);
            Assert.Equal(before, WorldSnapshotCodec.Encode(oldFields));
            Assert.Empty(candidate.Production!.Batches);
            Assert.Empty(candidate.Production.Lots);
            Assert.Equal(oldCommands.Count, (int)upgraded.InspectForHost().Journal.CommandOutcomeCount);
            for (int index = 0; index < oldCommands.Count; index++)
            {
                Assert.Equal(oldReceipts[index], WorldReceiptCodecV1.Encode(upgraded.FindReceipt(oldCommands[index], Delivery)!));
            }
        }
        timer.Stop();
        Assert.Equal(originalFile, File.ReadAllBytes(source.Path));
        using (SqliteWorldAuthority original = SqliteWorldAuthority.OpenExisting(source.Path))
        {
            Assert.Equal(before, WorldSnapshotCodec.Encode(original.InspectForHost().State));
        }
        using WorldDatabase oldDatabase = WorldDatabase.Open(source.Path, create: false);
        using WorldDatabase newDatabase = WorldDatabase.Open(destination.Path, create: false);
        Assert.Equal(2, oldDatabase.SchemaVersion);
        Assert.Equal(3, newDatabase.SchemaVersion);
        Assert.Equal(oldDatabase.ReadGenesis(), newDatabase.ReadGenesis());
        Assert.Equal(oldDatabase.ReadIdentities().ToArray(), newDatabase.ReadIdentities()
            .Where(identity => identity.Sequence <= current.Revision).ToArray());
        StoredWorldCommit migration = Assert.Single(newDatabase.ReadJournal(current.Revision));
        Assert.Equal(WorldCommitKind.ProfileMigration, migration.Kind);
        Assert.Equal(WorldProductionMigrationCodecV1.Encode(request), migration.AcceptedBatch);
        output.WriteLine("Tiny gathered-save upgrade: sourceCommits={0}, sourceSteps={1}, elapsedMs={2:F3}, sourceBytes={3}, destinationBytes={4}",
            current.Revision, current.AdvancingSteps, timer.Elapsed.TotalMilliseconds, originalFile.Length,
            new FileInfo(destination.Path).Length);
    }

    [Fact]
    public void CraftAfterUpgradeConsumesOnceAndOldAndNewReceiptsRemainExactAfterRestart()
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        WorldSnapshot state = Create();
        WorldActionCommand gather = Swing(state);
        byte[] gatherReceipt;
        using (SqliteWorldAuthority original = SqliteWorldAuthority.CreateNew(source.Path, state))
        {
            WorldStepDecision decision = original.Commit(Batch(state, gather));
            state = decision.Candidate;
            gatherReceipt = WorldReceiptCodecV1.Encode(decision.Receipts.Single());
        }
        WorldActionCommand craft;
        byte[] craftReceipt;
        byte[] after;
        using (SqliteWorldAuthority authority = SqliteWorldAuthority.UpgradeGatheringToProduction(source.Path, destination.Path, Request(state)))
        {
            state = authority.InspectForHost().State;
            craft = Craft(state);
            WorldStepDecision result = authority.Commit(Batch(state, craft));
            Assert.Equal(CommandReceiptStatus.Applied, result.Receipts.Single().Status);
            state = result.Candidate;
            Assert.Single(state.Production!.Batches);
            Assert.Equal(2UL, Assert.Single(state.Production.Lots).Quantity);
            Assert.Equal(4UL, state.Inventory.Custody.GetLot(Id(70)).Quantity);
            Assert.Equal(3, state.Inventory.Sources.Count);
            after = WorldSnapshotCodec.Encode(state);
            craftReceipt = WorldReceiptCodecV1.Encode(result.Receipts.Single());
        }
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(destination.Path);
        Assert.Equal(after, WorldSnapshotCodec.Encode(reopened.InspectForHost().State));
        Assert.Equal(gatherReceipt, WorldReceiptCodecV1.Encode(reopened.FindReceipt(gather, Delivery)!));
        Assert.Equal(craftReceipt, WorldReceiptCodecV1.Encode(reopened.FindReceipt(craft, Delivery)!));
        Assert.Single(reopened.InspectForHost().State.Production!.Batches);
        Assert.Equal(2UL, reopened.InspectForHost().Journal.CommandOutcomeCount);
    }

    [Theory]
    [InlineData((int)WorldProductionUpgradeBoundary.AfterBackup)]
    [InlineData((int)WorldProductionUpgradeBoundary.AfterSchemaChange)]
    [InlineData((int)WorldProductionUpgradeBoundary.BeforeCommit)]
    [InlineData((int)WorldProductionUpgradeBoundary.AfterCommit)]
    [InlineData((int)WorldProductionUpgradeBoundary.BeforePublish)]
    [InlineData((int)WorldProductionUpgradeBoundary.AfterPublish)]
    public void InterruptedUpgradeRetainsOriginalAndPublishesAtMostOneContinuation(int boundaryValue)
    {
        var boundary = (WorldProductionUpgradeBoundary)boundaryValue;
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        WorldSnapshot state = Create();
        using (SqliteWorldAuthority.CreateNew(source.Path, state)) { }
        byte[] before = File.ReadAllBytes(source.Path);
        Assert.Throws<IOException>(() => SqliteWorldAuthority.UpgradeGatheringToProduction(source.Path,
            destination.Path, Request(state), at => { if (at == boundary) throw new IOException("injected migration interruption"); }));
        Assert.Equal(before, File.ReadAllBytes(source.Path));
        using (SqliteWorldAuthority original = SqliteWorldAuthority.OpenExisting(source.Path))
        {
            Assert.Equal(state.ContentHash, original.InspectForHost().State.ContentHash);
        }
        Assert.Equal(boundary == WorldProductionUpgradeBoundary.AfterPublish, File.Exists(destination.Path));
        if (File.Exists(destination.Path))
        {
            using SqliteWorldAuthority published = SqliteWorldAuthority.OpenExisting(destination.Path);
            Assert.Equal(1UL, published.InspectForHost().State.Revision);
            Assert.Equal(0UL, published.InspectForHost().State.AdvancingSteps);
            Assert.Equal((ushort)3, published.InspectForHost().State.ProfileVersion);
        }
    }

    [Fact]
    public void ExistingDestinationSamePathStaleHeadAndCompetingOwnerCannotReplaceProgress()
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        WorldSnapshot state = Create();
        using (SqliteWorldAuthority owner = SqliteWorldAuthority.CreateNew(source.Path, state))
        {
            Assert.ThrowsAny<Exception>(() => SqliteWorldAuthority.UpgradeGatheringToProduction(source.Path, destination.Path, Request(state)));
            owner.Commit(Batch(state));
        }
        Assert.Throws<InvalidOperationException>(() => SqliteWorldAuthority.UpgradeGatheringToProduction(source.Path, destination.Path, Request(state)));
        Assert.False(File.Exists(destination.Path));
        Assert.Throws<IOException>(() => SqliteWorldAuthority.UpgradeGatheringToProduction(source.Path, source.Path, Request(state)));
        File.WriteAllBytes(destination.Path, "existing destination"u8.ToArray());
        byte[] bytes = File.ReadAllBytes(destination.Path);
        Assert.Throws<IOException>(() => SqliteWorldAuthority.UpgradeGatheringToProduction(source.Path, destination.Path, Request(state)));
        Assert.Equal(bytes, File.ReadAllBytes(destination.Path));
    }

    [Fact]
    public void NewProductionGenesisUsesSchemaThreeAndDoesNotInventAMigration()
    {
        using var files = new StorageFiles();
        WorldSnapshot old = Create();
        var production = Admission(old);
        var state = new WorldSnapshot(old.Inventory, old.Space, old.Residents,
            WorldProductionMigration.ComputeTargetAdmissionVersion(old.AdmissionVersion, production), old.Calendar,
            0, 0, gathering: old.Gathering, production: production);
        using (SqliteWorldAuthority.CreateNew(files.Path, state)) { }
        using (SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(files.Path))
        {
            Assert.Equal(state.ContentHash, reopened.InspectForHost().State.ContentHash);
        }
        using WorldDatabase database = WorldDatabase.Open(files.Path, create: false);
        Assert.Equal(3, database.SchemaVersion);
        Assert.Empty(database.ReadJournal());
        Assert.Empty(database.ReadIdentities());
    }
}
