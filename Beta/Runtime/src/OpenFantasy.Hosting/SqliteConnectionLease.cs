using Microsoft.Data.Sqlite;

namespace OpenFantasy.Hosting;

/// <summary>Exclusive local connection ownership and native setup shared by explicit save profiles.</summary>
internal sealed class SqliteConnectionLease : IDisposable
{
    internal const int WorldApplicationId = 0x4f464157;
    private readonly SqliteConnection _connection;
    private readonly int _applicationId;
    private int _schemaVersion;

    static SqliteConnectionLease() => SQLitePCL.Batteries_V2.Init();

    private SqliteConnectionLease(SqliteConnection connection, int applicationId, int schemaVersion)
    {
        _connection = connection;
        _applicationId = applicationId;
        _schemaVersion = schemaVersion;
    }

    internal static SqliteConnectionLease Open(string path, bool create, int applicationId, int schemaVersion)
    {
        if (schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }
        return Open(path, create, applicationId, schemaVersion, [schemaVersion]);
    }

    internal static SqliteConnectionLease OpenSupportedWorld(string path)
        => Open(path, false, WorldApplicationId, 0, [2, 3, 4, 5, 6, 7, 8, 9, 10, 11]);

    private static SqliteConnectionLease Open(string path, bool create, int applicationId, int schemaVersion,
        IReadOnlyList<int> supportedVersions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (applicationId <= 0 || (create && schemaVersion <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), "A save profile requires positive format identifiers.");
        }
        string absolute = Path.GetFullPath(path);
        if (create)
        {
            SqliteFileSet.RequireUnused(absolute);
            using var created = new FileStream(absolute, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        }
        else if (!File.Exists(absolute))
        {
            throw new FileNotFoundException("The world database does not exist.", absolute);
        }

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = absolute,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
            DefaultTimeout = 1
        }.ToString());
        try
        {
            connection.Open();
            if (!create)
            {
                using SqliteCommand version = connection.CreateCommand();
                version.CommandText = "PRAGMA user_version";
                schemaVersion = Convert.ToInt32(version.ExecuteScalar());
                if (!supportedVersions.Contains(schemaVersion))
                {
                    throw new InvalidDataException("The world database format is unsupported.");
                }
            }
            var lease = new SqliteConnectionLease(connection, applicationId, schemaVersion);
            if (!create)
            {
                lease.ValidateMetadata();
            }
            lease.Execute("PRAGMA locking_mode=EXCLUSIVE");
            if (!string.Equals((string?)lease.Scalar("PRAGMA journal_mode=WAL"), "wal", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The world requires SQLite WAL mode.");
            }
            lease.Execute("PRAGMA synchronous=FULL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=1000");
            using (SqliteTransaction ownership = lease.BeginWrite())
            {
                ownership.Commit();
            }
            if (!create)
            {
                lease.ValidateMetadata();
            }
            if (!string.Equals((string?)lease.Scalar("PRAGMA quick_check"), "ok", StringComparison.Ordinal))
            {
                throw new InvalidDataException("The world database failed its integrity check.");
            }
            Version native = Version.Parse((string)lease.Scalar("SELECT sqlite_version()")!);
            if (native < new Version(3, 51, 3))
            {
                throw new InvalidDataException("The loaded SQLite runtime predates the required WAL correction.");
            }
            return lease;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    internal SqliteTransaction BeginWrite() => _connection.BeginTransaction(deferred: false);
    internal int SchemaVersion => _schemaVersion;

    internal void BackupToNewFile(string path)
    {
        string absolute = Path.GetFullPath(path);
        SqliteFileSet.RequireUnused(absolute);
        using (var created = new FileStream(absolute, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None)) { }
        using var destination = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = absolute,
            Mode = SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        }.ToString());
        destination.Open();
        using (SqliteCommand settings = destination.CreateCommand())
        {
            settings.CommandText = "PRAGMA synchronous=FULL";
            settings.ExecuteNonQuery();
        }
        _connection.BackupDatabase(destination);
    }

    internal void SetMigrationForeignKeys(bool enabled)
    {
        Execute(enabled ? "PRAGMA foreign_keys=ON" : "PRAGMA foreign_keys=OFF");
        if (Convert.ToInt32(Scalar("PRAGMA foreign_keys")) != (enabled ? 1 : 0))
        {
            throw new InvalidOperationException("The staged migration could not set foreign-key enforcement.");
        }
    }

    internal void StageWorldSchemaThree(SqliteTransaction transaction)
    {
        if (_applicationId != WorldApplicationId || _schemaVersion != 2)
        {
            throw new InvalidOperationException("Only the exact second world schema can be upgraded.");
        }
        using SqliteCommand command = CreateCommand("PRAGMA user_version=3", transaction);
        command.ExecuteNonQuery();
    }

    internal void AcceptWorldSchemaThree()
    {
        if (_applicationId != WorldApplicationId || _schemaVersion != 2
            || Convert.ToInt64(Scalar("PRAGMA application_id")) != WorldApplicationId
            || Convert.ToInt64(Scalar("PRAGMA user_version")) != 3)
        {
            throw new InvalidDataException("The staged world schema transition did not commit.");
        }
        _schemaVersion = 3;
    }

    internal void CheckpointForPublication()
    {
        using SqliteCommand command = CreateCommand("PRAGMA wal_checkpoint(TRUNCATE)");
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read() || reader.GetInt32(0) != 0 || reader.GetInt32(1) != 0 || reader.GetInt32(2) != 0)
        {
            throw new IOException("The staged world could not checkpoint its committed WAL before publication.");
        }
    }

    internal void StageControllerRecoverySchema(SqliteTransaction transaction)
    {
        if (_applicationId != WorldApplicationId || _schemaVersion != 9)
            throw new InvalidOperationException("Only schema nine can explicitly activate controller recovery.");
        using var command = CreateCommand("PRAGMA user_version=10", transaction);
        command.ExecuteNonQuery();
    }

    internal void AcceptControllerRecoverySchema()
    {
        if (_applicationId != WorldApplicationId || _schemaVersion != 9 || Convert.ToInt64(Scalar("PRAGMA user_version")) != 10)
            throw new InvalidDataException("Controller recovery activation did not commit.");
        _schemaVersion = 10;
    }

    internal void InitializeMetadata(SqliteTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        using SqliteCommand command = CreateCommand(
            $"PRAGMA application_id={_applicationId}; PRAGMA user_version={_schemaVersion}", transaction);
        command.ExecuteNonQuery();
    }

    internal void ValidateMetadata()
    {
        if (Convert.ToInt64(Scalar("PRAGMA application_id")) != _applicationId
            || Convert.ToInt64(Scalar("PRAGMA user_version")) != _schemaVersion)
        {
            throw new InvalidDataException("The world database format is unsupported.");
        }
    }

    internal void ValidateSchema(IReadOnlyDictionary<string, string> tables,
        IReadOnlyDictionary<string, string> indexes, SqliteTransaction? transaction = null)
    {
        int tableCount = 0;
        int indexCount = 0;
        using SqliteCommand command = CreateCommand("SELECT type,name,tbl_name,sql FROM sqlite_schema", transaction);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string type = reader.GetString(0);
            string name = reader.GetString(1);
            string table = reader.GetString(2);
            if (type == "table" && table == name && tables.TryGetValue(name, out string? definition)
                && !reader.IsDBNull(3) && reader.GetString(3) == definition)
            {
                tableCount++;
            }
            else if (type == "index" && reader.IsDBNull(3)
                && indexes.TryGetValue(name, out string? indexedTable) && indexedTable == table)
            {
                indexCount++;
            }
            else
            {
                throw new InvalidDataException("The world database schema has unexpected objects or definitions.");
            }
        }
        if (tableCount != tables.Count || indexCount != indexes.Count)
        {
            throw new InvalidDataException("The world database schema is incomplete.");
        }
    }

    internal SqliteCommand CreateCommand(string sql, SqliteTransaction? transaction = null,
        params (string Name, object Value)[] parameters)
    {
        if (transaction is not null && !ReferenceEquals(transaction.Connection, _connection))
        {
            throw new ArgumentException("A transaction must belong to this connection lease.", nameof(transaction));
        }
        SqliteCommand command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        }
        return command;
    }

    internal SqliteAuthorityDiagnostics Inspect() => new(
        (string)Scalar("SELECT sqlite_version()")!, (string)Scalar("SELECT sqlite_source_id()")!,
        (string)Scalar("PRAGMA journal_mode")!, Convert.ToInt32(Scalar("PRAGMA synchronous")),
        Convert.ToInt32(Scalar("PRAGMA foreign_keys")), (string)Scalar("PRAGMA locking_mode")!);

    private object? Scalar(string sql)
    {
        using SqliteCommand command = CreateCommand(sql);
        return command.ExecuteScalar();
    }

    private void Execute(string sql)
    {
        using SqliteCommand command = CreateCommand(sql);
        command.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();
}
