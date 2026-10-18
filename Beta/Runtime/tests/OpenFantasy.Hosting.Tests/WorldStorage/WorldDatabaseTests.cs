using Microsoft.Data.Sqlite;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class WorldDatabaseTests
{
    [Fact]
    public void CreationAndReopenRetainGenesisHeadAndInitialCheckpoint()
    {
        using var files = new StorageFiles();
        byte[] genesis = "storage-genesis"u8.ToArray();
        using (WorldDatabase database = WorldDatabase.Open(files.Path, create: true))
        {
            database.Initialize(genesis);
            Assert.Equal(genesis, database.ReadGenesis());
            Assert.Equal(0UL, database.ReadHead().Sequence);
            Assert.Equal(0UL, database.ReadHead().AdvancingSteps);
            Assert.Equal(genesis, database.ReadHead().State);
            Assert.Equal(genesis, database.ReadLatestCheckpoint().State);
            Assert.Empty(database.ReadJournal());
            Assert.Empty(database.ReadCommandOutcomes());
            Assert.Empty(database.ReadIdentities());
            SqliteAuthorityDiagnostics diagnostics = database.Inspect();
            Assert.Equal("wal", diagnostics.JournalMode);
            Assert.Equal(2, diagnostics.Synchronous);
            Assert.Equal(1, diagnostics.ForeignKeys);
            Assert.Equal("exclusive", diagnostics.LockingMode);
        }
        using WorldDatabase reopened = WorldDatabase.Open(files.Path, create: false);
        Assert.Equal(genesis, reopened.ReadGenesis());
        Assert.Equal(genesis, reopened.ReadHead().State);
        Assert.Equal(0UL, reopened.ReadLatestCheckpoint().Sequence);
    }

    [Fact]
    public void OneTransactionRetainsCompactCommitOutcomesIdentitiesAndCheckpoint()
    {
        using var files = new StorageFiles();
        byte[] genesis = "before"u8.ToArray();
        byte[] after = "after-with-persistent-spatial-and-clock-state"u8.ToArray();
        byte[] batch = "typed-step-input"u8.ToArray();
        byte[] outcomes = "typed-step-outcomes"u8.ToArray();
        byte[] command = "exact-player-command"u8.ToArray();
        byte[] receipt = "exact-player-receipt"u8.ToArray();
        using (WorldDatabase database = WorldDatabase.Open(files.Path, create: true))
        {
            database.Initialize(genesis);
            using SqliteTransaction transaction = database.BeginWrite();
            database.WriteCommit(new(1, WorldCommitKind.AdvancingStep, Id(10), 1, batch, outcomes,
                ContentHash.Compute(genesis), ContentHash.Compute(after)), transaction);
            database.WriteCommandOutcome(new(Id(20), Id(21), 1, command, receipt), transaction);
            database.ReserveIdentity(new(Id(10), 1, WorldIdentityKind.CommitCause), transaction);
            database.ReserveIdentity(new(Id(21), 1, WorldIdentityKind.Command), transaction);
            database.WriteHead(1, 1, after, transaction);
            database.WriteCheckpoint(new(1, 1, after), transaction);
            transaction.Commit();
        }
        using WorldDatabase reopened = WorldDatabase.Open(files.Path, create: false);
        StoredWorldCommit commit = Assert.Single(reopened.ReadJournal());
        Assert.Equal(batch, commit.AcceptedBatch);
        Assert.Equal(outcomes, commit.Outcomes);
        Assert.Equal(ContentHash.Compute(after), commit.AfterHash);
        Assert.Equal(after, reopened.ReadHead().State);
        Assert.Equal(after, reopened.ReadLatestCheckpoint().State);
        StoredWorldCommandOutcome result = Assert.Single(reopened.ReadCommandOutcomes());
        Assert.Equal(command, result.Command);
        Assert.Equal(receipt, result.Receipt);
        Assert.Equal(receipt, reopened.FindCommand(Id(20), Id(21))!.Receipt);
        Assert.Null(reopened.FindCommand(Id(22), Id(21)));
        Assert.Equal(2, reopened.ReadIdentities().Count());
        Assert.True(reopened.ContainsIdentity(Id(10)));
        Assert.Empty(reopened.ReadJournal(1));
        Assert.Empty(reopened.ReadCommandOutcomes(1));
    }

    [Fact]
    public void DisposalBeforeCommitRollsBackEveryTableTogether()
    {
        using var files = new StorageFiles();
        byte[] genesis = "before-rollback"u8.ToArray();
        using (WorldDatabase database = WorldDatabase.Open(files.Path, create: true))
        {
            database.Initialize(genesis);
            using SqliteTransaction transaction = database.BeginWrite();
            WriteSingleCommit(database, transaction, 1, 1, "after"u8.ToArray());
            database.WriteCheckpoint(new(1, 1, "after"u8.ToArray()), transaction);
        }
        using WorldDatabase reopened = WorldDatabase.Open(files.Path, create: false);
        Assert.Equal(genesis, reopened.ReadHead().State);
        Assert.Equal(0UL, reopened.ReadHead().Sequence);
        Assert.Empty(reopened.ReadJournal());
        Assert.Empty(reopened.ReadCommandOutcomes());
        Assert.Empty(reopened.ReadIdentities());
        Assert.Equal(0UL, reopened.ReadLatestCheckpoint().Sequence);
    }

    [Fact]
    public void UnsignedCountersRoundTripAndSortBeyondSignedRange()
    {
        using var files = new StorageFiles();
        ulong high = (ulong)long.MaxValue + 1;
        using (WorldDatabase database = WorldDatabase.Open(files.Path, create: true))
        {
            database.Initialize("genesis"u8.ToArray());
            using SqliteTransaction transaction = database.BeginWrite();
            WriteSingleCommit(database, transaction, high, high, "high"u8.ToArray());
            WriteSingleCommit(database, transaction, ulong.MaxValue, ulong.MaxValue, "maximum"u8.ToArray());
            database.WriteCheckpoint(new(ulong.MaxValue, ulong.MaxValue, "maximum"u8.ToArray()), transaction);
            transaction.Commit();
        }
        using WorldDatabase reopened = WorldDatabase.Open(files.Path, create: false);
        Assert.Equal(new[] { high, ulong.MaxValue }, reopened.ReadJournal().Select(row => row.Sequence).ToArray());
        Assert.Equal(ulong.MaxValue, Assert.Single(reopened.ReadJournal(high)).Sequence);
        Assert.Equal(ulong.MaxValue, reopened.ReadHead().Sequence);
        Assert.Equal(ulong.MaxValue, reopened.ReadHead().AdvancingSteps);
        Assert.Equal(ulong.MaxValue, reopened.ReadLatestCheckpoint().Sequence);
        Assert.Equal(ulong.MaxValue, reopened.ReadLatestCheckpoint().AdvancingSteps);
    }

    [Fact]
    public void VersionOneAndVersionTwoRefuseEachOthersFilesWithoutMigration()
    {
        using var counted = new StorageFiles();
        using var world = new StorageFiles();
        using (SqliteCountedAuthority.CreateNew(counted.Path, AuthorityWorldFixture.Genesis())) { }
        using (WorldDatabase database = WorldDatabase.Open(world.Path, create: true))
        {
            database.Initialize("new-world"u8.ToArray());
        }
        Assert.Throws<InvalidDataException>(() => WorldDatabase.Open(counted.Path, create: false));
        Assert.Throws<InvalidDataException>(() => SqliteCountedAuthority.OpenExisting(world.Path));
        using SqliteCountedAuthority original = SqliteCountedAuthority.OpenExisting(counted.Path);
        Assert.Equal(0UL, original.InspectForHost().CommitSequence);
        using WorldDatabase unchanged = WorldDatabase.Open(world.Path, create: false);
        Assert.Equal("new-world"u8.ToArray(), unchanged.ReadGenesis());
    }

    [Fact]
    public void CreationNeverOverwritesAndOnlyOneLeaseOwnsTheWorld()
    {
        using var files = new StorageFiles();
        using (WorldDatabase database = WorldDatabase.Open(files.Path, create: true))
        {
            database.Initialize("owned"u8.ToArray());
            Assert.Throws<IOException>(() => WorldDatabase.Open(files.Path, create: true));
            Assert.ThrowsAny<Exception>(() => WorldDatabase.Open(files.Path, create: false));
        }
        using WorldDatabase reopened = WorldDatabase.Open(files.Path, create: false);
        Assert.Equal("owned"u8.ToArray(), reopened.ReadGenesis());
    }

    [Theory]
    [InlineData("CREATE TRIGGER suppress_head BEFORE UPDATE ON world_head BEGIN SELECT RAISE(IGNORE); END;")]
    [InlineData("CREATE VIEW hidden_world AS SELECT * FROM world_head;")]
    [InlineData("ALTER TABLE world_head ADD COLUMN extra BLOB;")]
    public void UnexpectedSchemaObjectsOrDefinitionsRejectBeforeReadiness(string mutation)
    {
        using var files = new StorageFiles();
        using (WorldDatabase database = WorldDatabase.Open(files.Path, create: true))
        {
            database.Initialize("schema"u8.ToArray());
        }
        files.Execute(mutation);
        Assert.Throws<InvalidDataException>(() => WorldDatabase.Open(files.Path, create: false));
    }

    [Fact]
    public void CorruptAndOversizedStoredStateRejectInsteadOfResetting()
    {
        using var files = new StorageFiles();
        using (WorldDatabase database = WorldDatabase.Open(files.Path, create: true))
        {
            database.Initialize("original"u8.ToArray());
        }
        files.Execute("UPDATE world_head SET state=x'01'");
        using (WorldDatabase corrupted = WorldDatabase.Open(files.Path, create: false))
        {
            Assert.Throws<InvalidDataException>(() => corrupted.ReadHead());
        }
        files.Execute($"PRAGMA ignore_check_constraints=ON; UPDATE world_head SET state=zeroblob({WorldDatabase.MaximumStateByteLength + 1});");
        Assert.ThrowsAny<Exception>(() =>
        {
            using WorldDatabase oversized = WorldDatabase.Open(files.Path, create: false);
            oversized.ReadHead();
        });
    }

    [Fact]
    public void DuplicateOutcomeCauseAndIdentityCannotOverwriteCommittedRows()
    {
        using var files = new StorageFiles();
        using WorldDatabase database = WorldDatabase.Open(files.Path, create: true);
        database.Initialize("duplicates"u8.ToArray());
        using (SqliteTransaction transaction = database.BeginWrite())
        {
            WriteSingleCommit(database, transaction, 1, 1, "accepted"u8.ToArray());
            transaction.Commit();
        }
        using (SqliteTransaction transaction = database.BeginWrite())
        {
            Assert.Throws<SqliteException>(() => database.WriteCommandOutcome(
                new(Id(30), Id(1), 1, "altered"u8.ToArray(), "altered"u8.ToArray()), transaction));
            Assert.Throws<SqliteException>(() => database.ReserveIdentity(new(Id(1), 1, WorldIdentityKind.Result), transaction));
            Assert.Throws<SqliteException>(() => database.WriteCommit(new(2, WorldCommitKind.HeldInteraction,
                Id(1), 1, [1], [2], ContentHash.Compute([1]), ContentHash.Compute([2])), transaction));
        }
        Assert.Equal("accepted"u8.ToArray(), database.ReadHead().State);
        Assert.Single(database.ReadJournal());
        Assert.Single(database.ReadCommandOutcomes());
        Assert.Single(database.ReadIdentities());
    }

    [Fact]
    public void OrphanCommandOutcomeCannotBecomeAnAcceptedExistingDatabase()
    {
        using var files = new StorageFiles();
        using (WorldDatabase database = WorldDatabase.Open(files.Path, create: true))
        {
            database.Initialize("foreign-keys"u8.ToArray());
        }
        files.Execute("""
            PRAGMA foreign_keys=OFF;
            INSERT INTO world_command_outcomes VALUES(zeroblob(16),zeroblob(16),x'0000000000000001',x'01',x'02');
            """);
        Assert.Throws<InvalidDataException>(() => WorldDatabase.Open(files.Path, create: false));
    }

    [Fact]
    public void InvalidKindsSequencesAndByteBoundsCannotWritePartialRows()
    {
        using var files = new StorageFiles();
        using WorldDatabase database = WorldDatabase.Open(files.Path, create: true);
        database.Initialize("bounds"u8.ToArray());
        using (SqliteTransaction transaction = database.BeginWrite())
        {
            StoredWorldCommit valid = new(1, WorldCommitKind.AdvancingStep, Id(40), 1,
                [1], [2], ContentHash.Compute([3]), ContentHash.Compute([4]));
            Assert.Throws<ArgumentOutOfRangeException>(() => database.WriteCommit(valid with { Sequence = 0 }, transaction));
            Assert.Throws<ArgumentOutOfRangeException>(() => database.WriteCommit(valid with { Kind = (WorldCommitKind)99 }, transaction));
            Assert.Throws<ArgumentException>(() => database.WriteCommit(valid with { AcceptedBatch = [] }, transaction));
            Assert.Throws<ArgumentException>(() => database.WriteCommit(valid with
            {
                Outcomes = new byte[WorldDatabase.MaximumOutcomesByteLength + 1]
            }, transaction));
            Assert.Throws<ArgumentException>(() => database.WriteCommandOutcome(new(Id(41), Id(42), 1,
                new byte[WorldDatabase.MaximumCommandByteLength + 1], [1]), transaction));
            Assert.Throws<ArgumentOutOfRangeException>(() => database.ReserveIdentity(new(Id(43), 1,
                (WorldIdentityKind)99), transaction));
            Assert.Throws<ArgumentException>(() => database.WriteCheckpoint(new(1, 1, []), transaction));
            transaction.Commit();
        }
        Assert.Empty(database.ReadJournal());
        Assert.Empty(database.ReadCommandOutcomes());
        Assert.Empty(database.ReadIdentities());
        Assert.Equal(0UL, database.ReadHead().Sequence);
    }

    private static void WriteSingleCommit(WorldDatabase database, SqliteTransaction transaction,
        ulong sequence, ulong steps, byte[] state)
    {
        database.WriteCommit(new(sequence, WorldCommitKind.AdvancingStep, Id(sequence), steps,
            [1, 2], [3, 4], ContentHash.Compute([5]), ContentHash.Compute(state)), transaction);
        database.WriteCommandOutcome(new(Id(30), Id(sequence), sequence, [6], [7]), transaction);
        database.ReserveIdentity(new(Id(sequence), sequence, WorldIdentityKind.CommitCause), transaction);
        database.WriteHead(sequence, steps, state, transaction);
    }

    private static EntityId Id(ulong number) => new(0, number);
}

internal sealed class StorageFiles : IDisposable
{
    private readonly string _directory;
    private readonly bool _retain;

    internal StorageFiles()
    {
        string? retainedRoot = Environment.GetEnvironmentVariable("OPENFANTASY_STORAGE_ARTIFACTS");
        _retain = !string.IsNullOrWhiteSpace(retainedRoot);
        _directory = System.IO.Path.Combine(_retain ? retainedRoot! : System.IO.Path.GetTempPath(),
            "openfantasy-storage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        Path = System.IO.Path.Combine(_directory, "world.sqlite3");
    }

    internal string Path { get; }

    internal void Execute(string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path, Mode = SqliteOpenMode.ReadWrite, Pooling = false
        }.ToString());
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (!_retain) Directory.Delete(_directory, recursive: true);
    }
}
