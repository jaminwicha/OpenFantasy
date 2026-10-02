using Microsoft.Data.Sqlite;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>
/// Explicit world-schema storage under one exclusive lease. The owning service validates/replays
/// canonical domain state, serializes callers and publishes only after its supplied transaction commits.
/// </summary>
internal sealed partial class WorldDatabase : IDisposable
{
    internal const int MaximumStateByteLength = 16 * 1024 * 1024;
    internal const int MaximumBatchByteLength = 256 * 1024;
    internal const int MaximumOutcomesByteLength = 256 * 1024;
    internal const int MaximumCommandByteLength = 16 * 1024;
    internal const int MaximumReceiptByteLength = 16 * 1024;
    private readonly SqliteConnectionLease _lease;

    private WorldDatabase(SqliteConnectionLease lease) => _lease = lease;

    internal static WorldDatabase Open(string path, bool create, int schemaVersion = WorldDatabaseSchema.Version)
    {
        if (schemaVersion is not WorldDatabaseSchema.Version and not WorldDatabaseSchemaThree.Version
            and not WorldDatabaseSchemaFour.Version and not WorldDatabaseSchemaFive.Version
            and not WorldDatabaseSchemaSix.Version and not WorldDatabaseSchemaSeven.Version
            and not WorldDatabaseSchemaEight.Version and not WorldDatabaseSchemaNine.Version and not WorldDatabaseSchemaTen.Version)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        }
        SqliteConnectionLease lease = create
            ? SqliteConnectionLease.Open(path, true, SqliteConnectionLease.WorldApplicationId, schemaVersion)
            : SqliteConnectionLease.OpenSupportedWorld(path);
        try
        {
            if (!create)
            {
                lease.ValidateSchema(TablesFor(lease.SchemaVersion), IndexesFor(lease.SchemaVersion));
                using SqliteCommand foreignKeys = lease.CreateCommand("PRAGMA foreign_key_check");
                using SqliteDataReader violations = foreignKeys.ExecuteReader();
                if (violations.Read())
                {
                    throw new InvalidDataException("Stored world rows contain broken foreign-key references.");
                }
            }
            return new WorldDatabase(lease);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    internal SqliteTransaction BeginWrite() => _lease.BeginWrite();
    internal SqliteAuthorityDiagnostics Inspect() => _lease.Inspect();
    internal int SchemaVersion => _lease.SchemaVersion;
    internal void BackupToNewFile(string path) => _lease.BackupToNewFile(path);
    internal void CheckpointForPublication() => _lease.CheckpointForPublication();

    internal void Initialize(byte[] genesis)
    {
        WorldStorageBinary.RequireBytes(genesis, MaximumStateByteLength, nameof(genesis));
        using SqliteTransaction transaction = BeginWrite();
        _lease.InitializeMetadata(transaction);
        foreach (string definition in TablesFor(SchemaVersion).Values)
        {
            using SqliteCommand command = _lease.CreateCommand(definition, transaction);
            command.ExecuteNonQuery();
        }
        byte[] hash = ContentHash.Compute(genesis).ToByteArray();
        ChangeExactlyOne("INSERT INTO world_genesis VALUES(1,$state,$hash)", transaction,
            ("$state", genesis), ("$hash", hash));
        ChangeExactlyOne("INSERT INTO world_head VALUES(1,$sequence,$steps,$state,$hash)", transaction,
            ("$sequence", WorldStorageBinary.Number(0)), ("$steps", WorldStorageBinary.Number(0)),
            ("$state", genesis), ("$hash", hash));
        WriteCheckpoint(new(0, 0, genesis), transaction);
        if (SchemaVersion == WorldDatabaseSchemaTen.Version)
            InitializeControllerRecovery(OpenFantasy.Core.Worlds.WorldSnapshotCodec.Decode(genesis), false, transaction);
        transaction.Commit();
        _lease.ValidateMetadata();
        _lease.ValidateSchema(TablesFor(SchemaVersion), IndexesFor(SchemaVersion));
    }

    private static IReadOnlyDictionary<string, string> TablesFor(int version) => version switch
    {
        WorldDatabaseSchema.Version => WorldDatabaseSchema.Tables,
        WorldDatabaseSchemaThree.Version => WorldDatabaseSchemaThree.Tables,
        WorldDatabaseSchemaFour.Version => WorldDatabaseSchemaFour.Tables,
        WorldDatabaseSchemaFive.Version => WorldDatabaseSchemaFive.Tables,
        WorldDatabaseSchemaSix.Version => WorldDatabaseSchemaSix.Tables,
        WorldDatabaseSchemaSeven.Version => WorldDatabaseSchemaSeven.Tables,
        WorldDatabaseSchemaEight.Version => WorldDatabaseSchemaEight.Tables,
        WorldDatabaseSchemaNine.Version => WorldDatabaseSchemaNine.Tables,
        WorldDatabaseSchemaTen.Version => WorldDatabaseSchemaTen.Tables,
        _ => throw new InvalidDataException("The world database schema is unsupported.")
    };

    private static IReadOnlyDictionary<string, string> IndexesFor(int version) => version switch
    {
        WorldDatabaseSchema.Version => WorldDatabaseSchema.Indexes,
        WorldDatabaseSchemaThree.Version => WorldDatabaseSchemaThree.Indexes,
        WorldDatabaseSchemaFour.Version => WorldDatabaseSchemaFour.Indexes,
        WorldDatabaseSchemaFive.Version => WorldDatabaseSchemaFive.Indexes,
        WorldDatabaseSchemaSix.Version => WorldDatabaseSchemaSix.Indexes,
        WorldDatabaseSchemaSeven.Version => WorldDatabaseSchemaSeven.Indexes,
        WorldDatabaseSchemaEight.Version => WorldDatabaseSchemaEight.Indexes,
        WorldDatabaseSchemaNine.Version => WorldDatabaseSchemaNine.Indexes,
        WorldDatabaseSchemaTen.Version => WorldDatabaseSchemaTen.Indexes,
        _ => throw new InvalidDataException("The world database schema is unsupported.")
    };

    internal byte[] ReadGenesis()
    {
        using SqliteCommand command = _lease.CreateCommand("SELECT state,hash FROM world_genesis WHERE singleton=1");
        using SqliteDataReader reader = command.ExecuteReader();
        RequireRow(reader, "The world has no accepted genesis.");
        return WorldStorageBinary.ReadState(reader, 0, 1);
    }

    internal StoredWorldHead ReadHead(SqliteTransaction? transaction = null)
    {
        using SqliteCommand command = _lease.CreateCommand(
            "SELECT sequence,advancing_steps,state,hash FROM world_head WHERE singleton=1", transaction);
        using SqliteDataReader reader = command.ExecuteReader();
        RequireRow(reader, "The world has no current head.");
        return new(WorldStorageBinary.ReadNumber(reader, 0), WorldStorageBinary.ReadNumber(reader, 1),
            WorldStorageBinary.ReadState(reader, 2, 3));
    }

    internal IEnumerable<StoredWorldCommit> ReadJournal(ulong afterSequence = 0)
    {
        using SqliteCommand command = _lease.CreateCommand("""
            SELECT sequence,kind,cause_id,advancing_steps,accepted_batch,outcomes,before_hash,after_hash
            FROM world_commits WHERE sequence>$after ORDER BY sequence
            """, null, ("$after", WorldStorageBinary.Number(afterSequence)));
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            var kind = (WorldCommitKind)reader.GetInt32(1);
            if (!Enum.IsDefined(kind))
            {
                throw new InvalidDataException("The stored world commit kind is unsupported.");
            }
            yield return new(WorldStorageBinary.ReadNumber(reader, 0), kind,
                WorldStorageBinary.ReadIdentity(reader, 2), WorldStorageBinary.ReadNumber(reader, 3),
                WorldStorageBinary.ReadBlob(reader, 4, 1, MaximumBatchByteLength),
                WorldStorageBinary.ReadBlob(reader, 5, 1, MaximumOutcomesByteLength),
                WorldStorageBinary.ReadHash(reader, 6), WorldStorageBinary.ReadHash(reader, 7));
        }
    }

    internal StoredWorldCheckpoint ReadLatestCheckpoint()
    {
        using SqliteCommand command = _lease.CreateCommand("""
            SELECT sequence,advancing_steps,state,hash FROM world_checkpoints ORDER BY sequence DESC LIMIT 1
            """);
        using SqliteDataReader reader = command.ExecuteReader();
        RequireRow(reader, "The world has no retained checkpoint.");
        return ReadCheckpoint(reader);
    }

    internal IEnumerable<StoredWorldCheckpoint> ReadCheckpoints()
    {
        using SqliteCommand command = _lease.CreateCommand(
            "SELECT sequence,advancing_steps,state,hash FROM world_checkpoints ORDER BY sequence");
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return ReadCheckpoint(reader);
        }
    }

    internal StoredWorldCommandOutcome? FindCommand(EntityId principal, EntityId commandId,
        SqliteTransaction? transaction = null)
    {
        using SqliteCommand command = _lease.CreateCommand("""
            SELECT principal,command_id,sequence,command,receipt FROM world_command_outcomes
            WHERE principal=$principal AND command_id=$command
            """, transaction, ("$principal", principal.ToByteArray()), ("$command", commandId.ToByteArray()));
        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadOutcome(reader) : null;
    }

    internal IEnumerable<StoredWorldCommandOutcome> ReadCommandOutcomes(ulong afterSequence = 0)
    {
        using SqliteCommand command = _lease.CreateCommand("""
            SELECT principal,command_id,sequence,command,receipt FROM world_command_outcomes
            WHERE sequence>$after ORDER BY sequence,principal,command_id
            """, null, ("$after", WorldStorageBinary.Number(afterSequence)));
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            yield return ReadOutcome(reader);
        }
    }

    internal bool ContainsIdentity(EntityId identity, SqliteTransaction? transaction = null)
    {
        using SqliteCommand command = _lease.CreateCommand(
            "SELECT 1 FROM world_occupied_identity WHERE identity=$id", transaction, ("$id", identity.ToByteArray()));
        return command.ExecuteScalar() is not null;
    }

    internal IEnumerable<StoredWorldIdentity> ReadIdentities()
    {
        using SqliteCommand command = _lease.CreateCommand(
            "SELECT identity,sequence,kind FROM world_occupied_identity ORDER BY identity");
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            var kind = (WorldIdentityKind)reader.GetInt32(2);
            if (!Enum.IsDefined(kind))
            {
                throw new InvalidDataException("The stored world identity kind is unsupported.");
            }
            yield return new(WorldStorageBinary.ReadIdentity(reader, 0),
                WorldStorageBinary.ReadNumber(reader, 1), kind);
        }
    }

    internal void WriteCommit(StoredWorldCommit value, SqliteTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(value);
        WorldStorageBinary.RequirePositiveSequence(value.Sequence);
        if (!Enum.IsDefined(value.Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "The world commit kind is unsupported.");
        }
        WorldStorageBinary.RequireBytes(value.AcceptedBatch, MaximumBatchByteLength, nameof(value.AcceptedBatch));
        WorldStorageBinary.RequireBytes(value.Outcomes, MaximumOutcomesByteLength, nameof(value.Outcomes));
        ArgumentNullException.ThrowIfNull(value.BeforeHash);
        ArgumentNullException.ThrowIfNull(value.AfterHash);
        ChangeExactlyOne("""
            INSERT INTO world_commits VALUES($sequence,$kind,$cause,$steps,$batch,$outcomes,$before,$after)
            """, transaction, ("$sequence", WorldStorageBinary.Number(value.Sequence)), ("$kind", (int)value.Kind),
            ("$cause", value.CauseId.ToByteArray()), ("$steps", WorldStorageBinary.Number(value.AdvancingSteps)),
            ("$batch", value.AcceptedBatch), ("$outcomes", value.Outcomes),
            ("$before", value.BeforeHash.ToByteArray()), ("$after", value.AfterHash.ToByteArray()));
    }

    internal void WriteHead(ulong sequence, ulong advancingSteps, byte[] state, SqliteTransaction transaction)
    {
        WorldStorageBinary.RequirePositiveSequence(sequence);
        WorldStorageBinary.RequireBytes(state, MaximumStateByteLength, nameof(state));
        ChangeExactlyOne("""
            UPDATE world_head SET sequence=$sequence,advancing_steps=$steps,state=$state,hash=$hash WHERE singleton=1
            """, transaction, ("$sequence", WorldStorageBinary.Number(sequence)),
            ("$steps", WorldStorageBinary.Number(advancingSteps)), ("$state", state),
            ("$hash", ContentHash.Compute(state).ToByteArray()));
    }

    internal void WriteCommandOutcome(StoredWorldCommandOutcome value, SqliteTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(value);
        WorldStorageBinary.RequirePositiveSequence(value.Sequence);
        WorldStorageBinary.RequireBytes(value.Command, MaximumCommandByteLength, nameof(value.Command));
        WorldStorageBinary.RequireBytes(value.Receipt, MaximumReceiptByteLength, nameof(value.Receipt));
        ChangeExactlyOne("INSERT INTO world_command_outcomes VALUES($principal,$id,$sequence,$command,$receipt)", transaction,
            ("$principal", value.PrincipalId.ToByteArray()), ("$id", value.CommandId.ToByteArray()),
            ("$sequence", WorldStorageBinary.Number(value.Sequence)), ("$command", value.Command), ("$receipt", value.Receipt));
    }

    internal void WriteCheckpoint(StoredWorldCheckpoint value, SqliteTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(value);
        WorldStorageBinary.RequireBytes(value.State, MaximumStateByteLength, nameof(value.State));
        ChangeExactlyOne("INSERT INTO world_checkpoints VALUES($sequence,$steps,$state,$hash)", transaction,
            ("$sequence", WorldStorageBinary.Number(value.Sequence)),
            ("$steps", WorldStorageBinary.Number(value.AdvancingSteps)), ("$state", value.State),
            ("$hash", ContentHash.Compute(value.State).ToByteArray()));
    }

    internal void ReserveIdentity(StoredWorldIdentity value, SqliteTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(value);
        WorldStorageBinary.RequirePositiveSequence(value.Sequence);
        if (!Enum.IsDefined(value.Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "The world identity kind is unsupported.");
        }
        ChangeExactlyOne("INSERT INTO world_occupied_identity VALUES($id,$sequence,$kind)", transaction,
            ("$id", value.Identity.ToByteArray()), ("$sequence", WorldStorageBinary.Number(value.Sequence)),
            ("$kind", (int)value.Kind));
    }

    private static StoredWorldCheckpoint ReadCheckpoint(SqliteDataReader reader) => new(
        WorldStorageBinary.ReadNumber(reader, 0), WorldStorageBinary.ReadNumber(reader, 1),
        WorldStorageBinary.ReadState(reader, 2, 3));

    private static StoredWorldCommandOutcome ReadOutcome(SqliteDataReader reader) => new(
        WorldStorageBinary.ReadIdentity(reader, 0), WorldStorageBinary.ReadIdentity(reader, 1),
        WorldStorageBinary.ReadNumber(reader, 2), WorldStorageBinary.ReadBlob(reader, 3, 1, MaximumCommandByteLength),
        WorldStorageBinary.ReadBlob(reader, 4, 1, MaximumReceiptByteLength));

    private static void RequireRow(SqliteDataReader reader, string message)
    {
        if (!reader.Read())
        {
            throw new InvalidDataException(message);
        }
    }

    private void ChangeExactlyOne(string sql, SqliteTransaction transaction,
        params (string Name, object Value)[] parameters)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        using SqliteCommand command = _lease.CreateCommand(sql, transaction, parameters);
        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidDataException("A required world write did not affect exactly one row.");
        }
    }

    public void Dispose() => _lease.Dispose();
}
