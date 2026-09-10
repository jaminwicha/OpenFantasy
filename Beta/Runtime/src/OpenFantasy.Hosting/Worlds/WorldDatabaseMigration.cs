using Microsoft.Data.Sqlite;

namespace OpenFantasy.Hosting.Worlds;

internal sealed partial class WorldDatabase
{
    internal void UpgradeProductionSchema(PreparedWorldMigration migration, bool writeCheckpoint,
        Action<WorldProductionUpgradeBoundary>? fault)
    {
        if (SchemaVersion != WorldDatabaseSchema.Version)
        {
            throw new InvalidDataException("Only an exact second-schema staging copy can be upgraded.");
        }
        _lease.SetMigrationForeignKeys(false);
        try
        {
            using SqliteTransaction transaction = BeginWrite();
            RebuildMigrationTable("world_commits", transaction);
            RebuildMigrationTable("world_occupied_identity", transaction);
            _lease.StageWorldSchemaThree(transaction);
            fault?.Invoke(WorldProductionUpgradeBoundary.AfterSchemaChange);
            WriteCommit(migration.Stored, transaction);
            foreach (StoredWorldIdentity identity in migration.Identities)
            {
                ReserveIdentity(identity, transaction);
            }
            WriteHead(migration.Stored.Sequence, migration.Stored.AdvancingSteps, migration.CandidateBytes, transaction);
            if (writeCheckpoint)
            {
                WriteCheckpoint(new(migration.Stored.Sequence, migration.Stored.AdvancingSteps,
                    migration.CandidateBytes), transaction);
            }
            _lease.ValidateSchema(WorldDatabaseSchemaThree.Tables, WorldDatabaseSchemaThree.Indexes, transaction);
            RequireValidForeignKeys(transaction);
            fault?.Invoke(WorldProductionUpgradeBoundary.BeforeCommit);
            transaction.Commit();
            _lease.AcceptWorldSchemaThree();
            fault?.Invoke(WorldProductionUpgradeBoundary.AfterCommit);
        }
        finally
        {
            _lease.SetMigrationForeignKeys(true);
        }
        _lease.ValidateMetadata();
        _lease.ValidateSchema(WorldDatabaseSchemaThree.Tables, WorldDatabaseSchemaThree.Indexes);
        RequireValidForeignKeys();
    }

    private void RebuildMigrationTable(string name, SqliteTransaction transaction)
    {
        if (name is not "world_commits" and not "world_occupied_identity")
        {
            throw new ArgumentException("This upgrade only changes its two declared journal constraints.", nameof(name));
        }
        string temporary = "production_upgrade_" + name;
        string definition = WorldDatabaseSchemaThree.Tables[name].Replace("CREATE TABLE \"" + name + "\"",
            "CREATE TABLE \"" + temporary + "\"", StringComparison.Ordinal);
        using (SqliteCommand create = _lease.CreateCommand(definition, transaction))
        {
            create.ExecuteNonQuery();
        }
        long expected;
        using (SqliteCommand count = _lease.CreateCommand("SELECT COUNT(*) FROM " + name, transaction))
        {
            expected = Convert.ToInt64(count.ExecuteScalar());
        }
        using (SqliteCommand copy = _lease.CreateCommand("INSERT INTO " + temporary + " SELECT * FROM " + name, transaction))
        {
            if (copy.ExecuteNonQuery() != expected)
            {
                throw new InvalidDataException("The schema upgrade did not preserve every retained row.");
            }
        }
        using (SqliteCommand drop = _lease.CreateCommand("DROP TABLE " + name, transaction))
        {
            drop.ExecuteNonQuery();
        }
        using SqliteCommand rename = _lease.CreateCommand("ALTER TABLE " + temporary + " RENAME TO " + name, transaction);
        rename.ExecuteNonQuery();
    }

    private void RequireValidForeignKeys(SqliteTransaction? transaction = null)
    {
        using SqliteCommand command = _lease.CreateCommand("PRAGMA foreign_key_check", transaction);
        using SqliteDataReader reader = command.ExecuteReader();
        if (reader.Read())
        {
            throw new InvalidDataException("The staged migration contains broken foreign-key references.");
        }
    }
}
