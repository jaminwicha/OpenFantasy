using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Copies a production world, appends funded quest admission in schema three, and publishes only verified replay.</summary>
internal sealed class WorldQuestUpgrade
{
    internal void Publish(string sourcePath, string destinationPath, WorldQuestMigration request,
        Action<WorldQuestUpgradeBoundary>? fault)
    {
        ArgumentNullException.ThrowIfNull(request);
        var publication = new WorldUpgradePublication(sourcePath, destinationPath);
        using WorldDatabase original = WorldDatabase.Open(publication.SourcePath, create: false);
        if (original.SchemaVersion != WorldDatabaseSchemaThree.Version)
        {
            throw new InvalidDataException("The quest source must retain the exact third world schema.");
        }
        RecoveredWorld retained = new WorldJournalRecovery().Recover(original);
        PreparedWorldQuestMigration migration = new WorldQuestMigrationPreparation().Prepare(retained.State,
            retained.Genesis.IdentityIndex, retained.StateHash, request, identity => original.ContainsIdentity(identity));
        WorldJournalAdvance advance = WorldJournalPolicy.Advance(retained.Usage, migration.Stored.Sequence,
            migration.Stored.AdvancingSteps, 0, (ulong)migration.Stored.AcceptedBatch.Length,
            (ulong)migration.Stored.Outcomes.Length);

        string staging = publication.CreateStagingCopy(original, "quest");
        fault?.Invoke(WorldQuestUpgradeBoundary.AfterBackup);
        using (WorldDatabase candidate = WorldDatabase.Open(staging, create: false))
        {
            WorldUpgradePublication.RequireExactCopy(original, candidate, retained);
            candidate.AppendQuestMigration(migration, advance.WriteCheckpoint, fault);
            WorldUpgradePublication.RequireExactReplay(candidate, migration.Stored, migration.CandidateBytes, advance.Usage);
            candidate.CheckpointForPublication();
        }
        publication.Publish(() => fault?.Invoke(WorldQuestUpgradeBoundary.BeforePublish),
            () => fault?.Invoke(WorldQuestUpgradeBoundary.AfterPublish));
    }
}
