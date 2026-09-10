using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Publishes one verified continuation while retaining the original world's complete history.</summary>
internal sealed class WorldProductionUpgrade
{
    internal void Publish(string sourcePath, string destinationPath, WorldProductionMigration request,
        Action<WorldProductionUpgradeBoundary>? fault)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentNullException.ThrowIfNull(request);
        string source = Path.GetFullPath(sourcePath);
        string destination = Path.GetFullPath(destinationPath);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("A production upgrade requires a distinct, unused destination.");
        }
        SqliteFileSet.RequireUnused(destination);

        using WorldDatabase original = WorldDatabase.Open(source, create: false);
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

        string directory = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(directory);
        string stagingDirectory = Path.Combine(directory, "." + Path.GetFileName(destination)
            + ".production-upgrade-" + Guid.NewGuid().ToString("N") + ".pending");
        Directory.CreateDirectory(stagingDirectory);
        string staging = Path.Combine(stagingDirectory, "world.sqlite");
        original.BackupToNewFile(staging);
        fault?.Invoke(WorldProductionUpgradeBoundary.AfterBackup);

        using (WorldDatabase candidate = WorldDatabase.Open(staging, create: false))
        {
            RecoveredWorld copied = new WorldJournalRecovery().Recover(candidate);
            if (copied.StateHash != retained.StateHash || copied.Usage != retained.Usage)
            {
                throw new InvalidDataException("The consistent backup does not match the recovered source world.");
            }
            WorldBackupComparison.RequireExactCopy(original, candidate);
            candidate.UpgradeProductionSchema(migration, advance.WriteCheckpoint, fault);
            RecoveredWorld upgraded = new WorldJournalRecovery().Recover(candidate);
            if (upgraded.StateHash != migration.Stored.AfterHash || upgraded.Usage != advance.Usage
                || !WorldSnapshotCodec.Encode(upgraded.State).AsSpan().SequenceEqual(migration.CandidateBytes))
            {
                throw new InvalidDataException("The staged production continuation does not match complete replay.");
            }
            candidate.CheckpointForPublication();
        }
        if (File.Exists(staging + "-wal") && new FileInfo(staging + "-wal").Length != 0)
        {
            throw new IOException("The staged world still requires a WAL file and cannot be published.");
        }
        fault?.Invoke(WorldProductionUpgradeBoundary.BeforePublish);
        SqliteFileSet.RequireUnused(destination);
        File.Move(staging, destination, overwrite: false);
        fault?.Invoke(WorldProductionUpgradeBoundary.AfterPublish);
        // A failed upgrade retains its private staging directory for diagnosis; it is never auto-selected.
        if (!Directory.EnumerateFileSystemEntries(stagingDirectory).Any())
        {
            Directory.Delete(stagingDirectory);
        }
    }
}
