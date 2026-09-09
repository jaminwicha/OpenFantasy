using System.Buffers.Binary;
using Microsoft.Data.Sqlite;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Commands;

namespace OpenFantasy.Hosting;

/// <summary>One lifetime SQLite connection; all access is serialized by its owning authority.</summary>
internal sealed class SqliteAuthorityDatabase : IDisposable
{
    private const int ApplicationId = 0x4f464157;
    private const int SchemaVersion = 1;
    private readonly SqliteConnection _connection;
    private static readonly IReadOnlyDictionary<string, string> TableDefinitions = new Dictionary<string, string>
    {
        ["genesis"] = """
            CREATE TABLE genesis (singleton INTEGER PRIMARY KEY CHECK(singleton=1), state BLOB NOT NULL,
                hash BLOB NOT NULL CHECK(length(hash)=32))
            """,
        ["world_head"] = """
            CREATE TABLE world_head (singleton INTEGER PRIMARY KEY CHECK(singleton=1),
                sequence BLOB NOT NULL CHECK(length(sequence)=8), state BLOB NOT NULL,
                hash BLOB NOT NULL CHECK(length(hash)=32))
            """,
        ["command_journal"] = """
            CREATE TABLE command_journal (
                sequence BLOB PRIMARY KEY CHECK(length(sequence)=8),
                principal BLOB NOT NULL CHECK(length(principal)=16),
                command_id BLOB NOT NULL CHECK(length(command_id)=16),
                command BLOB NOT NULL, receipt BLOB NOT NULL,
                before_hash BLOB NOT NULL CHECK(length(before_hash)=32),
                after_state BLOB NOT NULL, after_hash BLOB NOT NULL CHECK(length(after_hash)=32),
                UNIQUE(principal,command_id))
            """,
        ["occupied_identity"] = """
            CREATE TABLE occupied_identity (
                identity BLOB PRIMARY KEY CHECK(length(identity)=16),
                sequence BLOB NOT NULL REFERENCES command_journal(sequence),
                kind INTEGER NOT NULL CHECK(kind IN (1,2,3)))
            """,
    };


    static SqliteAuthorityDatabase() => SQLitePCL.Batteries_V2.Init();

    private SqliteAuthorityDatabase(SqliteConnection connection) => _connection = connection;

    internal static SqliteAuthorityDatabase Open(string path, bool create)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string absolute = Path.GetFullPath(path);
        if (create)
        {
            // Existing, corrupt and partially created worlds are never silently replaced.
            using var created = new FileStream(absolute, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        }
        else if (!File.Exists(absolute))
        {
            throw new FileNotFoundException("The world database does not exist.", absolute);
        }

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = absolute, Mode = SqliteOpenMode.ReadWrite, Cache = SqliteCacheMode.Private,
            Pooling = false, DefaultTimeout = 1
        }.ToString());
        try
        {
            connection.Open();
            var database = new SqliteAuthorityDatabase(connection);
            if (!create)
            {
                if (Convert.ToInt64(database.Scalar("PRAGMA application_id")) != ApplicationId
                    || Convert.ToInt64(database.Scalar("PRAGMA user_version")) != SchemaVersion)
                {
                    throw new InvalidDataException("The world database format is unsupported.");
                }
            }

            database.Execute("PRAGMA locking_mode=EXCLUSIVE");
            if (!string.Equals((string?)database.Scalar("PRAGMA journal_mode=WAL"), "wal", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The world requires SQLite WAL mode.");
            }
            database.Execute("PRAGMA synchronous=FULL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=1000");
            using (SqliteTransaction ownership = database.BeginWrite())
            {
                ownership.Commit();
            }
            if (!create)
            {
                // Repeat metadata checks under lifetime ownership: admission cannot race another writer.
                if (Convert.ToInt64(database.Scalar("PRAGMA application_id")) != ApplicationId
                    || Convert.ToInt64(database.Scalar("PRAGMA user_version")) != SchemaVersion)
                {
                    throw new InvalidDataException("The world database format changed during admission.");
                }
                database.ValidateSchema();
            }
            if (!string.Equals((string?)database.Scalar("PRAGMA quick_check"), "ok", StringComparison.Ordinal))
            {
                throw new InvalidDataException("The world database failed its integrity check.");
            }
            Version native = Version.Parse((string)database.Scalar("SELECT sqlite_version()")!);
            if (native < new Version(3, 51, 3))
            {
                throw new InvalidDataException("The loaded SQLite runtime predates the required WAL correction.");
            }
            return database;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    internal SqliteTransaction BeginWrite() => _connection.BeginTransaction(deferred: false);

    internal void Initialize(byte[] genesis)
    {
        using SqliteTransaction transaction = BeginWrite();
        Execute($"PRAGMA application_id={ApplicationId}; PRAGMA user_version={SchemaVersion}", transaction);
        foreach (string definition in TableDefinitions.Values)
        {
            Execute(definition, transaction);
        }
        byte[] hash = ContentHash.Compute(genesis).ToByteArray();
        ChangeExactlyOne("INSERT INTO genesis VALUES(1,$state,$hash)", transaction, ("$state", genesis), ("$hash", hash));
        ChangeExactlyOne("INSERT INTO world_head VALUES(1,$sequence,$state,$hash)", transaction,
            ("$sequence", Number(0)), ("$state", genesis), ("$hash", hash));
        transaction.Commit();
        ValidateSchema();
    }

    internal byte[] ReadGenesis()
    {
        using SqliteCommand command = Command("SELECT state,hash FROM genesis WHERE singleton=1");
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidDataException("The world has no accepted genesis.");
        }
        return ReadState(reader, 0, 1);
    }

    internal StoredAuthorityHead ReadHead(SqliteTransaction? transaction = null)
    {
        using SqliteCommand command = Command("SELECT sequence,state,hash FROM world_head WHERE singleton=1", transaction);
        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidDataException("The world has no current state.");
        }
        return new StoredAuthorityHead(ReadNumber(reader, 0), ReadState(reader, 1, 2));
    }

    internal StoredAuthorityCommit? FindCommand(EntityId principal, EntityId commandId, SqliteTransaction transaction)
    {
        using SqliteCommand command = Command("""
            SELECT sequence,principal,command_id,command,receipt,before_hash,after_state,after_hash
            FROM command_journal WHERE principal=$principal AND command_id=$command
            """, transaction, ("$principal", principal.ToByteArray()), ("$command", commandId.ToByteArray()));
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadCommit(reader) : null;
    }

    internal IEnumerable<StoredAuthorityCommit> ReadJournal()
    {
        using SqliteCommand command = Command("""
            SELECT sequence,principal,command_id,command,receipt,before_hash,after_state,after_hash
            FROM command_journal ORDER BY sequence
            """);
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return ReadCommit(reader);
        }
    }

    internal bool ContainsIdentity(EntityId identity, SqliteTransaction? transaction = null) =>
        Scalar("SELECT 1 FROM occupied_identity WHERE identity=$id", transaction, ("$id", identity.ToByteArray())) is not null;

    internal IEnumerable<StoredIdentity> ReadIdentities()
    {
        using SqliteCommand command = Command("SELECT identity,sequence,kind FROM occupied_identity ORDER BY identity");
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return new StoredIdentity(EntityId.FromBytes(Blob(reader, 0, 16, 16)),
                ReadNumber(reader, 1), reader.GetInt32(2));
        }
    }

    internal void ReserveIdentity(EntityId identity, ulong sequence, int kind, SqliteTransaction transaction) =>
        ChangeExactlyOne("INSERT INTO occupied_identity VALUES($id,$sequence,$kind)", transaction,
            ("$id", identity.ToByteArray()), ("$sequence", Number(sequence)), ("$kind", kind));

    internal void WriteCommit(StoredAuthorityCommit value, SqliteTransaction transaction) => ChangeExactlyOne("""
        INSERT INTO command_journal VALUES($sequence,$principal,$id,$command,$receipt,$before,$state,$after)
        """, transaction, ("$sequence", Number(value.Sequence)), ("$principal", value.PrincipalId.ToByteArray()),
        ("$id", value.CommandId.ToByteArray()), ("$command", value.Command), ("$receipt", value.Receipt),
        ("$before", value.BeforeHash.ToByteArray()), ("$state", value.AfterState),
        ("$after", ContentHash.Compute(value.AfterState).ToByteArray()));

    internal void WriteHead(ulong sequence, byte[] state, SqliteTransaction transaction) =>
        ChangeExactlyOne("UPDATE world_head SET sequence=$sequence,state=$state,hash=$hash WHERE singleton=1", transaction,
            ("$sequence", Number(sequence)), ("$state", state), ("$hash", ContentHash.Compute(state).ToByteArray()));

    internal SqliteAuthorityDiagnostics Inspect() => new(
        (string)Scalar("SELECT sqlite_version()")!, (string)Scalar("SELECT sqlite_source_id()")!,
        (string)Scalar("PRAGMA journal_mode")!, Convert.ToInt32(Scalar("PRAGMA synchronous")),
        Convert.ToInt32(Scalar("PRAGMA foreign_keys")), (string)Scalar("PRAGMA locking_mode")!);

    private static StoredAuthorityCommit ReadCommit(SqliteDataReader reader) => new(
        ReadNumber(reader, 0), EntityId.FromBytes(Blob(reader, 1, 16, 16)),
        EntityId.FromBytes(Blob(reader, 2, 16, 16)), Blob(reader, 3, 1, CommandCodecV1.MaximumEnvelopeByteLength),
        Blob(reader, 4, 1, CommandReceiptCodecV1.MaximumReceiptByteLength), new ContentHash(Convert.ToHexString(Blob(reader, 5, 32, 32)).ToLowerInvariant()),
        ReadState(reader, 6, 7));

    private static byte[] ReadState(SqliteDataReader reader, int stateIndex, int hashIndex)
    {
        byte[] bytes = Blob(reader, stateIndex, 1, AuthorityEncodingV1.MaximumStateByteLength);
        if (!ContentHash.Compute(bytes).ToByteArray().AsSpan().SequenceEqual(Blob(reader, hashIndex, 32, 32)))
        {
            throw new InvalidDataException("Stored authority state does not match its checksum.");
        }
        return bytes;
    }

    private static byte[] Blob(SqliteDataReader reader, int index, int minimum, int maximum)
    {
        if (reader.GetFieldType(index) != typeof(byte[]))
        {
            throw new InvalidDataException("A stored field is not binary.");
        }
        long length = reader.GetBytes(index, 0, null, 0, 0);
        if (length < minimum || length > maximum)
        {
            throw new InvalidDataException("A stored field exceeds its format bounds.");
        }
        return reader.GetFieldValue<byte[]>(index);
    }

    private static ulong ReadNumber(SqliteDataReader reader, int index) => BinaryPrimitives.ReadUInt64BigEndian(Blob(reader, index, 8, 8));
    private static byte[] Number(ulong value)
    {
        byte[] result = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(result, value);
        return result;
    }

    private void ValidateSchema()
    {
        var expectedIndexes = new Dictionary<string, string>
        {
            ["sqlite_autoindex_command_journal_1"] = "command_journal",
            ["sqlite_autoindex_command_journal_2"] = "command_journal",
            ["sqlite_autoindex_occupied_identity_1"] = "occupied_identity"
        };
        int tableCount = 0;
        int indexCount = 0;
        using SqliteCommand command = Command("SELECT type,name,tbl_name,sql FROM sqlite_schema");
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            string type = reader.GetString(0);
            string name = reader.GetString(1);
            string table = reader.GetString(2);
            if (type == "table" && table == name && TableDefinitions.TryGetValue(name, out string? definition)
                && !reader.IsDBNull(3) && reader.GetString(3) == definition)
            {
                tableCount++;
            }
            else if (type == "index" && reader.IsDBNull(3)
                && expectedIndexes.TryGetValue(name, out string? indexedTable) && indexedTable == table)
            {
                indexCount++;
            }
            else
            {
                throw new InvalidDataException("The world database schema has unexpected objects or definitions.");
            }
        }
        if (tableCount != TableDefinitions.Count || indexCount != expectedIndexes.Count)
        {
            throw new InvalidDataException("The world database schema is incomplete.");
        }
    }

    private void ChangeExactlyOne(string sql, SqliteTransaction transaction, params (string Name, object Value)[] parameters)
    {
        using SqliteCommand command = Command(sql, transaction, parameters);
        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidDataException("A required durable write did not affect exactly one row.");
        }
    }

    private SqliteCommand Command(string sql, SqliteTransaction? transaction = null, params (string Name, object Value)[] parameters)
    {
        SqliteCommand command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        }
        return command;
    }

    private object? Scalar(string sql, SqliteTransaction? transaction = null, params (string Name, object Value)[] parameters)
    {
        using SqliteCommand command = Command(sql, transaction, parameters);
        return command.ExecuteScalar();
    }

    private void Execute(string sql, SqliteTransaction? transaction = null, params (string Name, object Value)[] parameters)
    {
        using SqliteCommand command = Command(sql, transaction, parameters);
        command.ExecuteNonQuery();
    }

    public void Dispose() => _connection.Dispose();
}

internal sealed record StoredAuthorityHead(ulong Sequence, byte[] State);
internal sealed record StoredAuthorityCommit(ulong Sequence, EntityId PrincipalId, EntityId CommandId,
    byte[] Command, byte[] Receipt, ContentHash BeforeHash, byte[] AfterState);
public sealed record SqliteAuthorityDiagnostics(string Version, string SourceId, string JournalMode,
    int Synchronous, int ForeignKeys, string LockingMode);
internal sealed record StoredIdentity(EntityId Identity, ulong Sequence, int Kind);
