using Microsoft.Data.Sqlite;

namespace OpenFantasy.Hosting;

/// <summary>Exclusive local connection ownership and native setup shared by explicit save profiles.</summary>
internal sealed class SqliteConnectionLease : IDisposable
{
    internal const int WorldApplicationId = 0x4f464157;
    private readonly SqliteConnection _connection;
    private readonly int _applicationId;
    private readonly int _schemaVersion;

    static SqliteConnectionLease() => SQLitePCL.Batteries_V2.Init();

    private SqliteConnectionLease(SqliteConnection connection, int applicationId, int schemaVersion)
    {
        _connection = connection;
        _applicationId = applicationId;
        _schemaVersion = schemaVersion;
    }

    internal static SqliteConnectionLease Open(string path, bool create, int applicationId, int schemaVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (applicationId <= 0 || schemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), "A save profile requires positive format identifiers.");
        }
        string absolute = Path.GetFullPath(path);
        if (create)
        {
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
        IReadOnlyDictionary<string, string> indexes)
    {
        int tableCount = 0;
        int indexCount = 0;
        using SqliteCommand command = CreateCommand("SELECT type,name,tbl_name,sql FROM sqlite_schema");
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
