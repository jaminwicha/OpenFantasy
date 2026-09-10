using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Publishes one verified continuation while retaining the original world's complete history.</summary>
internal sealed class WorldProductionUpgrade
{
    internal void Publish(string sourcePath, string destinationPath, WorldProductionMigration request,
        Action<WorldProductionUpgradeBoundary>? fault)
    {
        ArgumentNullException.ThrowIfNull(request);
        var publication = new WorldUpgradePublication(sourcePath, destinationPath);
        using WorldDatabase original = WorldDatabase.Open(publication.SourcePath, create: false);
        if (original.SchemaVersion != WorldDatabaseSchema.Version)
        {
            throw new InvalidDataException("The source must retain the exact gathering world schema.");
        }
        RecoveredWorld retained = new WorldJournalRecovery().Recover(original);
        PreparedWorldMigration migration = new WorldMigrationPreparation().Prepare(retained.State,
            retained.Genesis.IdentityIndex, retained.StateHash, request, identity => original.ContainsIdentity(identity));
        WorldJournalAdvance advance = WorldJournalPolicy.Advance(retained.Usage, migration.Stored.Sequence,
            migration.Stored.AdvancingSteps, 0, (ulong)migration.Stored.AcceptedBatch.Length,
            (ulong)migration.Stored.Outcomes.Length);

        string staging = publication.CreateStagingCopy(original, "production");
        fault?.Invoke(WorldProductionUpgradeBoundary.AfterBackup);

        using (WorldDatabase candidate = WorldDatabase.Open(staging, create: false))
        {
            WorldUpgradePublication.RequireExactCopy(original, candidate, retained);
            candidate.UpgradeProductionSchema(migration, advance.WriteCheckpoint, fault);
            WorldUpgradePublication.RequireExactReplay(candidate, migration.Stored, migration.CandidateBytes, advance.Usage);
            candidate.CheckpointForPublication();
        }
        publication.Publish(() => fault?.Invoke(WorldProductionUpgradeBoundary.BeforePublish),
            () => fault?.Invoke(WorldProductionUpgradeBoundary.AfterPublish));
    }
}
