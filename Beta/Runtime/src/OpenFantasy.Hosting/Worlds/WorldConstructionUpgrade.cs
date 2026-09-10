using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Copies a quest world, appends construction and finite tool admission in schema three, and publishes only verified replay.</summary>
internal sealed class WorldConstructionUpgrade
{
    internal void Publish(string sourcePath, string destinationPath, WorldConstructionMigration reconstruction,
        Action<WorldConstructionUpgradeBoundary>? fault)
    {
        ArgumentNullException.ThrowIfNull(reconstruction);
        var publication = new WorldUpgradePublication(sourcePath, destinationPath);
        using WorldDatabase original = WorldDatabase.Open(publication.SourcePath, create: false);
        if (original.SchemaVersion != WorldDatabaseSchemaThree.Version)
        {
            throw new InvalidDataException("The construction source must retain the exact third world schema.");
        }
        RecoveredWorld retained = new WorldJournalRecovery().Recover(original);
        PreparedWorldConstructionMigration migration = new WorldConstructionMigrationPreparation().Prepare(retained.State,
            retained.Genesis.IdentityIndex, retained.StateHash, reconstruction, identity => original.ContainsIdentity(identity));
        WorldJournalAdvance advance = WorldJournalPolicy.Advance(retained.Usage, migration.Stored.Sequence,
            migration.Stored.AdvancingSteps, 0, (ulong)migration.Stored.AcceptedBatch.Length,
            (ulong)migration.Stored.Outcomes.Length);

        string staging = publication.CreateStagingCopy(original, "construction");
        fault?.Invoke(WorldConstructionUpgradeBoundary.AfterBackup);
        using (WorldDatabase candidate = WorldDatabase.Open(staging, create: false))
        {
            WorldUpgradePublication.RequireExactCopy(original, candidate, retained);
            candidate.AppendConstructionMigration(migration, advance.WriteCheckpoint, fault);
            WorldUpgradePublication.RequireExactReplay(candidate, migration.Stored, migration.CandidateBytes, advance.Usage);
            candidate.CheckpointForPublication();
        }
        publication.Publish(() => fault?.Invoke(WorldConstructionUpgradeBoundary.BeforePublish),
            () => fault?.Invoke(WorldConstructionUpgradeBoundary.AfterPublish));
    }
}
