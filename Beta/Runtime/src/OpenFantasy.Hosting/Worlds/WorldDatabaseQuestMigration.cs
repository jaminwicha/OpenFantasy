using Microsoft.Data.Sqlite;

namespace OpenFantasy.Hosting.Worlds;

internal sealed partial class WorldDatabase
{
    internal void AppendQuestMigration(PreparedWorldQuestMigration migration, bool writeCheckpoint,
        Action<WorldQuestUpgradeBoundary>? fault)
    {
        if (SchemaVersion != WorldDatabaseSchemaThree.Version)
        {
            throw new InvalidDataException("Quest admission requires the exact third world schema.");
        }
        using SqliteTransaction transaction = BeginWrite();
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
        // This migration adds data only. Foreign keys stay enabled and no schema metadata changes.
        RequireValidForeignKeys(transaction);
        fault?.Invoke(WorldQuestUpgradeBoundary.BeforeCommit);
        transaction.Commit();
        fault?.Invoke(WorldQuestUpgradeBoundary.AfterCommit);
        _lease.ValidateMetadata();
        _lease.ValidateSchema(WorldDatabaseSchemaThree.Tables, WorldDatabaseSchemaThree.Indexes);
        RequireValidForeignKeys();
    }
}
