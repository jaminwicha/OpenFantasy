using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>Shared file ownership, backup verification and publication for the explicit save upgrades.</summary>
internal sealed class WorldUpgradePublication
{
    private string? _stagingDirectory;
    private string? _stagingPath;

    internal WorldUpgradePublication(string sourcePath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        SourcePath = Path.GetFullPath(sourcePath);
        DestinationPath = Path.GetFullPath(destinationPath);
        if (string.Equals(SourcePath, DestinationPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("A world upgrade requires a distinct, unused destination.");
        }
        SqliteFileSet.RequireUnused(DestinationPath);
    }

    internal string SourcePath { get; }
    internal string DestinationPath { get; }

    internal string CreateStagingCopy(WorldDatabase original, string profile)
    {
        if (_stagingPath is not null || profile is not "production" and not "quest" and not "construction" and not "proposal")
        {
            throw new InvalidOperationException("This publication requires one explicitly selected staging copy.");
        }
        string directory = Path.GetDirectoryName(DestinationPath)!;
        Directory.CreateDirectory(directory);
        _stagingDirectory = Path.Combine(directory, "." + Path.GetFileName(DestinationPath)
            + "." + profile + "-upgrade-" + Guid.NewGuid().ToString("N") + ".pending");
        Directory.CreateDirectory(_stagingDirectory);
        _stagingPath = Path.Combine(_stagingDirectory, "world.sqlite");
        original.BackupToNewFile(_stagingPath);
        return _stagingPath;
    }

    internal static void RequireExactCopy(WorldDatabase original, WorldDatabase candidate, RecoveredWorld retained)
    {
        RecoveredWorld copied = new WorldJournalRecovery().Recover(candidate);
        if (copied.StateHash != retained.StateHash || copied.Usage != retained.Usage)
        {
            throw new InvalidDataException("The consistent backup does not match the recovered source world.");
        }
        WorldBackupComparison.RequireExactCopy(original, candidate);
    }

    internal static void RequireExactReplay(WorldDatabase candidate, StoredWorldCommit expected,
        byte[] expectedState, WorldJournalUsage expectedUsage)
    {
        RecoveredWorld upgraded = new WorldJournalRecovery().Recover(candidate);
        if (upgraded.StateHash != expected.AfterHash || upgraded.Usage != expectedUsage
            || !WorldSnapshotCodec.Encode(upgraded.State).AsSpan().SequenceEqual(expectedState))
        {
            throw new InvalidDataException("The staged world continuation does not match complete replay.");
        }
    }

    internal void Publish(Action beforePublish, Action afterPublish)
    {
        if (_stagingPath is null || _stagingDirectory is null)
        {
            throw new InvalidOperationException("Publication requires its verified staging copy.");
        }
        if (File.Exists(_stagingPath + "-wal") && new FileInfo(_stagingPath + "-wal").Length != 0)
        {
            throw new IOException("The staged world still requires a WAL file and cannot be published.");
        }
        beforePublish();
        SqliteFileSet.RequireUnused(DestinationPath);
        File.Move(_stagingPath, DestinationPath, overwrite: false);
        afterPublish();
        // Failed private copies remain available for diagnosis and are never auto-selected.
        if (!Directory.EnumerateFileSystemEntries(_stagingDirectory).Any())
        {
            Directory.Delete(_stagingDirectory);
        }
    }
}
