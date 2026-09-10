using Microsoft.Data.Sqlite;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.WorldStorage.GatheringWorldFixture;
using static OpenFantasy.Hosting.Tests.WorldStorage.ProductionUpgradeFixture;

namespace OpenFantasy.Hosting.Tests.WorldStorage;

public sealed class SqliteFileSetTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("-wal")]
    [InlineData("-shm")]
    [InlineData("-journal")]
    public void NewWorldCountedWorldAndBackupRefuseExistingSidecarsWithoutTouchingThem(string suffix)
    {
        byte[] retained = "preexisting SQLite sidecar; do not consume or discard"u8.ToArray();
        using var world = new StorageFiles();
        using var counted = new StorageFiles();
        using var backup = new StorageFiles();
        using var source = new StorageFiles();
        foreach (string path in new[] { world.Path, counted.Path, backup.Path })
        {
            File.WriteAllBytes(path + suffix, retained);
        }
        Assert.Throws<IOException>(() => SqliteWorldAuthority.CreateNew(world.Path, Create()));
        Assert.Throws<IOException>(() => SqliteCountedAuthority.CreateNew(counted.Path, AuthorityWorldFixture.Genesis()));
        using (SqliteWorldAuthority.CreateNew(source.Path, Create())) { }
        using (WorldDatabase database = WorldDatabase.Open(source.Path, create: false))
        {
            Assert.Throws<IOException>(() => database.BackupToNewFile(backup.Path));
        }
        foreach (string path in new[] { world.Path, counted.Path, backup.Path })
        {
            Assert.False(File.Exists(path));
            Assert.Equal(retained, File.ReadAllBytes(path + suffix));
        }
    }

    [Theory]
    [InlineData("-wal", false)]
    [InlineData("-shm", false)]
    [InlineData("-journal", false)]
    [InlineData("-wal", true)]
    [InlineData("-shm", true)]
    [InlineData("-journal", true)]
    public void UpgradeRefusesSidecarsBothInitiallyAndImmediatelyBeforePublication(string suffix, bool injectedAtPublication)
    {
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        WorldSnapshot state = Create();
        using (SqliteWorldAuthority.CreateNew(source.Path, state)) { }
        byte[] original = File.ReadAllBytes(source.Path);
        byte[] retained = "unrelated destination sidecar"u8.ToArray();
        if (!injectedAtPublication)
        {
            File.WriteAllBytes(destination.Path + suffix, retained);
        }
        bool reachedPublication = false;
        Assert.Throws<IOException>(() => SqliteWorldAuthority.UpgradeGatheringToProduction(source.Path, destination.Path,
            Request(state), boundary =>
            {
                if (boundary == WorldProductionUpgradeBoundary.BeforePublish)
                {
                    reachedPublication = true;
                    File.WriteAllBytes(destination.Path + suffix, retained);
                }
            }));
        Assert.Equal(injectedAtPublication, reachedPublication);
        Assert.False(File.Exists(destination.Path));
        Assert.Equal(retained, File.ReadAllBytes(destination.Path + suffix));
        Assert.Equal(original, File.ReadAllBytes(source.Path));
        using SqliteWorldAuthority reopened = SqliteWorldAuthority.OpenExisting(source.Path);
        Assert.Equal(state.ContentHash, reopened.InspectForHost().State.ContentHash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-wal")]
    [InlineData("-shm")]
    [InlineData("-journal")]
    public void DirectoryAtAnyRequiredFilePathIsPreservedAndRejected(string suffix)
    {
        using var destination = new StorageFiles();
        Directory.CreateDirectory(destination.Path + suffix);
        string sentinel = Path.Combine(destination.Path + suffix, "retained.txt");
        File.WriteAllText(sentinel, "retain this directory");
        Assert.Throws<IOException>(() => SqliteWorldAuthority.CreateNew(destination.Path, Create()));
        Assert.Equal("retain this directory", File.ReadAllText(sentinel));
    }

    [Fact]
    public void ActualValidOrphanWalIsRejectedBeforeSQLiteCanRecoverItIntoTheNewWorld()
    {
        using var donor = new StorageFiles();
        using var source = new StorageFiles();
        using var destination = new StorageFiles();
        using var unsafeControl = new StorageFiles();
        WorldSnapshot state = Create();
        using (SqliteWorldAuthority.CreateNew(source.Path, state)) { }
        byte[] original = File.ReadAllBytes(source.Path);
        byte[] orphan;
        using (SqliteConnection connection = Open(donor.Path))
        {
            Execute(connection, "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; CREATE TABLE marker(value TEXT); INSERT INTO marker VALUES('orphan history');");
            orphan = File.ReadAllBytes(donor.Path + "-wal");
        }
        Assert.True(orphan.Length > 32);

        // A deliberately unsafe temporary control reproduces SQLite's orphan-WAL recovery behavior.
        using (SqliteConnection connection = Open(unsafeControl.Path))
        {
            Execute(connection, "PRAGMA journal_mode=WAL; CREATE TABLE marker(value TEXT); INSERT INTO marker VALUES('candidate');");
        }
        File.WriteAllBytes(unsafeControl.Path + "-wal", orphan);
        using (SqliteConnection connection = Open(unsafeControl.Path))
        {
            using SqliteCommand value = connection.CreateCommand();
            value.CommandText = "SELECT value FROM marker";
            Assert.Equal("orphan history", value.ExecuteScalar());
            value.CommandText = "SELECT sqlite_version()";
            string nativeVersion = Assert.IsType<string>(value.ExecuteScalar());
            output.WriteLine("Orphan WAL replacement reproduced with actual native SQLite {0}.", nativeVersion);
        }

        Assert.Throws<IOException>(() => SqliteWorldAuthority.UpgradeGatheringToProduction(source.Path, destination.Path,
            Request(state), boundary =>
            {
                if (boundary == WorldProductionUpgradeBoundary.BeforePublish)
                {
                    File.WriteAllBytes(destination.Path + "-wal", orphan);
                }
            }));
        Assert.False(File.Exists(destination.Path));
        Assert.Equal(orphan, File.ReadAllBytes(destination.Path + "-wal"));
        Assert.Equal(original, File.ReadAllBytes(source.Path));
    }

    private static SqliteConnection Open(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate, Cache = SqliteCacheMode.Private, Pooling = false
        }.ToString());
        connection.Open();
        return connection;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
