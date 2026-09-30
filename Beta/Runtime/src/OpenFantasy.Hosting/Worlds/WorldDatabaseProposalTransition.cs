using Microsoft.Data.Sqlite;

namespace OpenFantasy.Hosting.Worlds;

internal enum WorldProposalTransitionBoundary
{
    AfterBackup,
    BeforeCommit,
    AfterCommit,
    BeforePublish,
    AfterPublish
}

internal sealed partial class WorldDatabase
{
    /// <summary>
    /// Appends the typed transition commit, the archive closing the exact stored source head, the cause
    /// identity, the saved-proposal head and any scheduled checkpoint in one transaction. It adds rows
    /// only; no earlier row, table, index or schema metadata changes.
    /// </summary>
    internal void AppendProposalTransition(PreparedWorldProposalTransition transition, bool writeCheckpoint,
        Action<WorldProposalTransitionBoundary>? fault)
    {
        ArgumentNullException.ThrowIfNull(transition);
        if (SchemaVersion != WorldDatabaseSchemaEight.Version)
        {
            throw new InvalidDataException("A proposal transition requires the exact coordinated archive schema.");
        }
        using SqliteTransaction sql = BeginWrite();
        WriteCommit(transition.Stored, sql);
        WriteMaterialCareArchive(transition.Archive, sql);
        foreach (StoredWorldIdentity identity in transition.Identities)
        {
            ReserveIdentity(identity, sql);
        }
        WriteHead(transition.Stored.Sequence, transition.Stored.AdvancingSteps, transition.CandidateBytes, sql);
        if (writeCheckpoint)
        {
            WriteCheckpoint(new(transition.Stored.Sequence, transition.Stored.AdvancingSteps,
                transition.CandidateBytes), sql);
        }
        RequireValidForeignKeys(sql);
        fault?.Invoke(WorldProposalTransitionBoundary.BeforeCommit);
        sql.Commit();
        fault?.Invoke(WorldProposalTransitionBoundary.AfterCommit);
        _lease.ValidateMetadata();
        _lease.ValidateSchema(WorldDatabaseSchemaEight.Tables, WorldDatabaseSchemaEight.Indexes);
        RequireValidForeignKeys();
    }
}
