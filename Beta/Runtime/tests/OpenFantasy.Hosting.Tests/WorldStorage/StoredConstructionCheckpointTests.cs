using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Construction.Ownership;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed partial class SeparatedTerrainStorageTests
{
    private const string StoredCheckpointEnvironment = "OPENFANTASY_STORED_CHECKPOINT_FIXTURE";

    [Fact]
    public void Stored_checkpoint_preparation_uses_lawful_ordinary_idle_and_selected_work()
    {
        using var files = new StorageFiles();
        var ready = Start(files.Path, typed: true);
        using var authority = ready.Authority;
        var world = authority.ReadStateForHost();
        var starting = world;
        for (int i = 0; i < 64; i++) world = authority.Commit(ready.Fixture.Batch(world, [], [])).Candidate;
        Assert.Equal(starting.AdvancingSteps + 64, world.AdvancingSteps);
        Assert.Equal(starting.Construction!.Projects.Single().Progress, world.Construction!.Projects.Single().Progress);
        Assert.All(world.Residents, actor => Assert.True(actor.ExperiencedTime.TotalMilliseconds
            > starting.GetResident(actor.ActorId).ExperiencedTime.TotalMilliseconds));
        var batch = Batch(world.Inventory, world.Space.Actors, world.Construction, world.Revision, world.AdvancingSteps, ready.Fixture);
        authority.EvictForStoredConstruction(batch.Actions.Single());
        var expected = new WorldStepProcessor().Decide(world, batch);
        Assert.Equal(WorldActionRejection.None, authority.CommitStoredConstruction(batch).Rejection);
        Assert.Equal(WorldSnapshotCodec.Encode(expected.Candidate), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
        Assert.Equal(0UL, authority.InspectForHost().Journal.LastCheckpointSteps);
    }

    [Fact]
    public void Prepare_real_stored_checkpoint_history()
    {
        string root = StoredCheckpointRoot();
        Assert.False(Directory.Exists(root), "Preparation requires a fresh evidence directory; retained attempts are immutable.");
        Directory.CreateDirectory(root);
        var timer = Stopwatch.StartNew();
        string livePath = Path.Combine(root, "work.sqlite3");
        var ready = Start(livePath, typed: true);
        WorldSnapshot before;
        WorldStepBatch selected;
        ContentHash retiredOwner;
        using (var authority = ready.Authority)
        {
            var world = authority.ReadStateForHost();
            retiredOwner = ConstructionOwnerDirectory.Capture(world).Owners.Single(owner => owner.Key.Kind == ConstructionOwnerKind.Timing).Hash;
            Assert.True(world.AdvancingSteps < WorldJournalPolicy.CheckpointStepInterval - 1);
            while (world.AdvancingSteps < WorldJournalPolicy.CheckpointStepInterval - 1)
                world = authority.Commit(ready.Fixture.Batch(world, [], [])).Candidate;
            Assert.Equal(0UL, authority.InspectForHost().Journal.LastCheckpointSteps);
            Assert.Equal(0UL, world.Construction!.Projects.Single().Progress.CompletedUnits);
            before = world;
            selected = Batch(world.Inventory, world.Space.Actors, world.Construction, world.Revision, world.AdvancingSteps, ready.Fixture);
        }
        CopyClosedCheckpointFile(livePath, Path.Combine(root, "before.sqlite3"));
        File.WriteAllBytes(Path.Combine(root, "before.ofc"), WorldSnapshotCodec.Encode(before));
        File.WriteAllBytes(Path.Combine(root, "selected.ofc"), WorldStepCodec.Encode(selected));
        double prefixSeconds = timer.Elapsed.TotalSeconds;
        timer.Restart();
        var expected = new WorldStepProcessor().Decide(before, selected);
        using (var authority = SqliteWorldAuthority.OpenExisting(livePath))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            authority.EvictForStoredConstruction(selected.Actions.Single());
            Assert.Equal(WorldReceiptCodecV1.Encode(expected.Receipts.Single()), WorldReceiptCodecV1.Encode(authority.CommitStoredConstruction(selected)));
            var after = authority.ReadStateForHost();
            Assert.Equal(WorldSnapshotCodec.Encode(expected.Candidate), WorldSnapshotCodec.Encode(after));
            Assert.Equal(WorldJournalPolicy.CheckpointStepInterval, after.AdvancingSteps);
            Assert.Equal(1UL, after.Construction!.Projects.Single().Progress.CompletedUnits);
            Assert.False(after.Construction.Projects.Single().IsCompleted);
            Assert.Equal(after.Revision, authority.InspectForHost().Journal.LastCheckpointSequence);
            Assert.Equal(after.AdvancingSteps, authority.InspectForHost().Journal.LastCheckpointSteps);
            File.WriteAllBytes(Path.Combine(root, "checkpoint.ofc"), WorldSnapshotCodec.Encode(after));
        }
        CopyClosedCheckpointFile(livePath, Path.Combine(root, "checkpoint.sqlite3"));
        double checkpointSeconds = timer.Elapsed.TotalSeconds;
        using (var database = WorldDatabase.Open(livePath, create: false))
        {
            Assert.Equal(2, database.ReadCheckpoints().Count());
            var checkpoint = database.ReadLatestCheckpoint();
            Assert.Equal(expected.Candidate.Revision, checkpoint.Sequence);
            Assert.Equal(ConstructionOwnerDirectory.Capture(expected.Candidate).CopyBytes(), checkpoint.State);
        }
        Assert.DoesNotContain(ConstructionOwnerDirectory.Capture(expected.Candidate).Owners, owner => owner.Hash == retiredOwner);
        var files = new[] { "before.sqlite3", "checkpoint.sqlite3", "before.ofc", "checkpoint.ofc", "selected.ofc" };
        var manifest = new StoredCheckpointManifest(ready.GenesisHash.ToString(), before.Revision,
            expected.Candidate.Revision, retiredOwner.ToString(), CheckpointFileHash(typeof(WorldSnapshot).Assembly.Location),
            CheckpointFileHash(typeof(SqliteWorldAuthority).Assembly.Location), files.ToDictionary(file => file, file => CheckpointFileHash(Path.Combine(root, file))),
            prefixSeconds, checkpointSeconds, new FileInfo(Path.Combine(root, "checkpoint.sqlite3")).Length);
        File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        output.WriteLine($"Actual typed checkpoint: advancing={expected.Candidate.AdvancingSteps}; revision={expected.Candidate.Revision}; prefixSeconds={prefixSeconds:F3}; checkpointAndReplaySeconds={checkpointSeconds:F3}; databaseBytes={manifest.DatabaseBytes}. Full original replay retained; warm-process fixture, not cold/scale acceptance.");
    }

    [Fact]
    public void Stored_checkpoint_reopens_continues_and_preserves_original_history()
    {
        var (root, manifest) = ReadStoredCheckpointFixture();
        string path = CopyCheckpointCase(root, "continuation", "checkpoint.sqlite3");
        var selected = WorldStepCodec.Decode(File.ReadAllBytes(Path.Combine(root, "selected.ofc")));
        WorldSnapshot world = WorldSnapshotCodec.Decode(File.ReadAllBytes(Path.Combine(root, "checkpoint.ofc")));
        using (var authority = SqliteWorldAuthority.OpenExisting(path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(world), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            Assert.Equal(manifest.CheckpointSequence, authority.InspectForHost().Journal.LastCheckpointSequence);
            var command = selected.Actions.Single();
            var originalReceipt = authority.FindReceipt(command, ConstructionUpgradeFixture.Delivery(world, command.ActorId));
            Assert.NotNull(originalReceipt);
            var next = StoredCheckpointWork(world, selected, 1);
            authority.EvictForStoredConstruction(next.Actions.Single());
            for (ulong i = 1; i < 200; i++)
            {
                var batch = i == 1 ? next : StoredCheckpointWork(world, selected, i);
                var expected = new WorldStepProcessor().Decide(world, batch);
                Assert.Equal(WorldReceiptCodecV1.Encode(expected.Receipts.Single()), WorldReceiptCodecV1.Encode(authority.CommitStoredConstruction(batch)));
                world = expected.Candidate;
            }
            AssertCompleted(world);
            Assert.Equal(WorldSnapshotCodec.Encode(world), WorldSnapshotCodec.Encode(authority.ReadStateForHost()));
            Assert.Equal(WorldReceiptCodecV1.Encode(originalReceipt!), WorldReceiptCodecV1.Encode(authority.FindReceipt(command,
                ConstructionUpgradeFixture.Delivery(world, command.ActorId))!));
        }
        using (var recovered = SqliteWorldAuthority.OpenExisting(path))
        {
            Assert.Equal(WorldSnapshotCodec.Encode(world), WorldSnapshotCodec.Encode(recovered.ReadStateForHost()));
            Assert.Equal(manifest.GenesisHash, recovered.ReadRecordedStateForHost(0).State.ContentHash.ToString());
            Assert.Equal(File.ReadAllBytes(Path.Combine(root, "before.ofc")),
                WorldSnapshotCodec.Encode(recovered.ReadRecordedStateForHost(manifest.BeforeSequence).State));
            Assert.Equal(File.ReadAllBytes(Path.Combine(root, "checkpoint.ofc")),
                WorldSnapshotCodec.Encode(recovered.ReadRecordedStateForHost(manifest.CheckpointSequence).State));
        }
        string retainedPrefix = Path.Combine(Path.GetDirectoryName(path)!, "retained-prefix.sqlite3");
        CopyClosedCheckpointFile(Path.Combine(root, "checkpoint.sqlite3"), retainedPrefix);
        using var original = WorldDatabase.Open(retainedPrefix, create: false);
        using var continued = WorldDatabase.Open(path, create: false);
        Assert.Equal(original.ReadJournal().Select(CheckpointRow), continued.ReadJournal().Where(row => row.Sequence <= manifest.CheckpointSequence).Select(CheckpointRow));
        Assert.Equal(original.ReadCommandOutcomes().Select(CheckpointOutcome), continued.ReadCommandOutcomes().Where(row => row.Sequence <= manifest.CheckpointSequence).Select(CheckpointOutcome));
        Assert.Subset(continued.ReadIdentities().ToHashSet(), original.ReadIdentities().ToHashSet());
        Assert.Equal(original.ReadCheckpoints().Select(row => Convert.ToHexString(row.State)), continued.ReadCheckpoints().Select(row => Convert.ToHexString(row.State)));
    }

    [Fact]
    public void Stored_checkpoint_before_commit_fault_preserves_prior_boundary() => StoredCheckpointFault(AuthorityCommitBoundary.BeforeCommit);

    [Fact]
    public void Stored_checkpoint_after_commit_fault_preserves_complete_boundary() => StoredCheckpointFault(AuthorityCommitBoundary.AfterCommit);

    [Fact]
    public void Stored_checkpoint_missing_row_refuses_recovery() => StoredCheckpointTamper("missing-checkpoint");

    [Fact]
    public void Stored_checkpoint_substituted_valid_directory_refuses_recovery() => StoredCheckpointTamper("substituted-checkpoint");

    [Fact]
    public void Stored_checkpoint_missing_earlier_owner_refuses_recovery() => StoredCheckpointTamper("missing-earlier-owner");

    private static void StoredCheckpointFault(AuthorityCommitBoundary boundary)
    {
        var (root, manifest) = ReadStoredCheckpointFixture();
        string path = CopyCheckpointCase(root, boundary.ToString(), "before.sqlite3");
        var batch = WorldStepCodec.Decode(File.ReadAllBytes(Path.Combine(root, "selected.ofc")));
        var before = WorldSnapshotCodec.Decode(File.ReadAllBytes(Path.Combine(root, "before.ofc")));
        var expected = new WorldStepProcessor().Decide(before, batch);
        using (var authority = SqliteWorldAuthority.OpenExisting(path))
        {
            authority.EvictForStoredConstruction(batch.Actions.Single());
            Assert.Throws<IOException>(() => authority.CommitStoredConstruction(batch, at =>
            {
                if (at == boundary) throw new IOException("Interrupt work and its due checkpoint together");
            }));
            Assert.Throws<InvalidOperationException>(() => authority.ReadStateForHost());
        }
        bool committed = boundary == AuthorityCommitBoundary.AfterCommit;
        using (var database = WorldDatabase.Open(path, create: false))
        {
            Assert.Equal(committed ? manifest.CheckpointSequence : 0UL, database.ReadLatestCheckpoint().Sequence);
            Assert.Equal(committed ? 2 : 1, database.ReadCheckpoints().Count());
        }
        using var recovered = SqliteWorldAuthority.OpenExisting(path);
        Assert.Equal(WorldSnapshotCodec.Encode(committed ? expected.Candidate : before), WorldSnapshotCodec.Encode(recovered.ReadStateForHost()));
        var command = batch.Actions.Single();
        var receipt = recovered.FindReceipt(command, ConstructionUpgradeFixture.Delivery(before, command.ActorId));
        if (committed) Assert.Equal(WorldReceiptCodecV1.Encode(expected.Receipts.Single()), WorldReceiptCodecV1.Encode(receipt!));
        else
        {
            Assert.Null(receipt);
            // Explicit recovery/retry proof: the failed transaction consumed neither command nor cause.
            recovered.EvictForStoredConstruction(command);
            Assert.Equal(WorldReceiptCodecV1.Encode(expected.Receipts.Single()), WorldReceiptCodecV1.Encode(recovered.CommitStoredConstruction(batch)));
            Assert.Equal(manifest.CheckpointSequence, recovered.InspectForHost().Journal.LastCheckpointSequence);
        }
        recovered.EvictForStoredConstruction(command);
        Assert.Throws<AuthorityDispatchException>(() => recovered.CommitStoredConstruction(batch));
    }

    private static void StoredCheckpointTamper(string mutation)
    {
        var (root, manifest) = ReadStoredCheckpointFixture();
        string path = CopyCheckpointCase(root, mutation, "checkpoint.sqlite3");
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            if (mutation == "missing-earlier-owner")
            {
                command.CommandText = "DELETE FROM world_owners WHERE hash=$hash";
                command.Parameters.AddWithValue("$hash", new ContentHash(manifest.RetiredOwnerHash).ToByteArray());
            }
            else if (mutation == "substituted-checkpoint")
            {
                byte[] older = ConstructionOwnerDirectory.Capture(WorldSnapshotCodec.Decode(File.ReadAllBytes(Path.Combine(root, "before.ofc")))).CopyBytes();
                command.CommandText = "UPDATE world_checkpoints SET state=$state,hash=$hash WHERE sequence<>$zero";
                command.Parameters.AddWithValue("$state", older);
                command.Parameters.AddWithValue("$hash", ContentHash.Compute(older).ToByteArray());
                command.Parameters.AddWithValue("$zero", WorldStorageBinary.Number(0));
            }
            else
            {
                command.CommandText = "DELETE FROM world_checkpoints WHERE sequence<>$zero";
                command.Parameters.AddWithValue("$zero", WorldStorageBinary.Number(0));
            }
            Assert.Equal(1, command.ExecuteNonQuery());
        }
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(path));
    }

    private static WorldStepBatch StoredCheckpointWork(WorldSnapshot world, WorldStepBatch initial, ulong ordinal)
    {
        var template = initial.Actions.Single();
        var project = world.Construction!.Projects.Single();
        var intent = (WorkConstructionIntent)template.Intent;
        var command = new WorldActionCommand(EntityId.DeriveSpawned(initial.CauseId, 50_000 + ordinal * 2),
            template.PrincipalId, template.OriginSessionId, template.ActorId, world.WorldId, world.BranchId,
            world.Space.GetActor(template.ActorId).Revision, intent with { ExpectedProjectRevision = project.Revision });
        return new(EntityId.DeriveSpawned(initial.CauseId, 50_001 + ordinal * 2), world.WorldId, world.BranchId,
            world.Revision, world.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [command], 5);
    }

    private static string StoredCheckpointRoot()
    {
        string? root = Environment.GetEnvironmentVariable(StoredCheckpointEnvironment);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Explicit retained checkpoint fixture directory is required.");
        return Path.GetFullPath(root);
    }

    private static (string Root, StoredCheckpointManifest Manifest) ReadStoredCheckpointFixture()
    {
        string root = StoredCheckpointRoot();
        var manifest = JsonSerializer.Deserialize<StoredCheckpointManifest>(File.ReadAllText(Path.Combine(root, "manifest.json")))!;
        foreach (var file in manifest.Files) Assert.Equal(file.Value, CheckpointFileHash(Path.Combine(root, file.Key)));
        Assert.Equal(manifest.CoreAssembly, CheckpointFileHash(typeof(WorldSnapshot).Assembly.Location));
        Assert.Equal(manifest.HostingAssembly, CheckpointFileHash(typeof(SqliteWorldAuthority).Assembly.Location));
        return (root, manifest);
    }

    private static string CopyCheckpointCase(string root, string name, string source)
    {
        string cases = Environment.GetEnvironmentVariable("OPENFANTASY_STORED_CHECKPOINT_CASES") ?? Path.Combine(root, "cases");
        string folder = Path.Combine(cases, name);
        Assert.False(Directory.Exists(folder), "Retain old attempts; select fresh case output rather than overwriting.");
        Directory.CreateDirectory(folder);
        string target = Path.Combine(folder, "world.sqlite3");
        CopyClosedCheckpointFile(Path.Combine(root, source), target);
        return target;
    }

    private static void CopyClosedCheckpointFile(string source, string destination)
    {
        Assert.False(File.Exists(source + "-wal") && new FileInfo(source + "-wal").Length != 0,
            "Close the authority and flush its WAL before copying the retained database.");
        File.Copy(source, destination, overwrite: false);
    }

    private static string CheckpointFileHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static object CheckpointRow(StoredWorldCommit row) => new { row.Sequence, row.Kind, row.CauseId,
        row.AdvancingSteps, row.BeforeHash, row.AfterHash, Batch = ContentHash.Compute(row.AcceptedBatch), Outcomes = ContentHash.Compute(row.Outcomes) };
    private static object CheckpointOutcome(StoredWorldCommandOutcome row) => new { row.PrincipalId, row.CommandId,
        row.Sequence, Command = ContentHash.Compute(row.Command), Receipt = ContentHash.Compute(row.Receipt) };
    private sealed record StoredCheckpointManifest(string GenesisHash, ulong BeforeSequence, ulong CheckpointSequence,
        string RetiredOwnerHash, string CoreAssembly, string HostingAssembly, Dictionary<string, string> Files,
        double PrefixSeconds, double CheckpointSeconds, long DatabaseBytes);
}
