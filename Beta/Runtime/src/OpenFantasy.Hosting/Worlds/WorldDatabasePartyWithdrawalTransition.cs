using Microsoft.Data.Sqlite;

namespace OpenFantasy.Hosting.Worlds;

internal enum WorldPartyWithdrawalTransitionBoundary
{
    AfterBackup,
    BeforeCommit,
    AfterCommit,
    BeforePublish,
    AfterPublish
}

internal sealed partial class WorldDatabase
{
    internal void AppendPartyWithdrawalTransition(ExpectedJournalEntry transition, bool writeCheckpoint,
        Action<WorldPartyWithdrawalTransitionBoundary>? fault)
    {
        if (SchemaVersion != WorldDatabaseSchemaTwelve.Version)
        {
            throw new InvalidDataException("Withdrawal transition requires effective-control recovery storage.");
        }
        using SqliteTransaction sql = BeginWrite();
        WriteCommit(transition.Stored, sql);
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
        fault?.Invoke(WorldPartyWithdrawalTransitionBoundary.BeforeCommit);
        sql.Commit();
        fault?.Invoke(WorldPartyWithdrawalTransitionBoundary.AfterCommit);
        _lease.ValidateMetadata();
        _lease.ValidateSchema(WorldDatabaseSchemaTwelve.Tables, WorldDatabaseSchemaTwelve.Indexes);
        RequireValidForeignKeys();
    }
}
