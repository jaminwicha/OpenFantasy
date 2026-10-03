using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

public sealed partial class SqliteWorldAuthority
{
    private ControllerRecoveryState? _controllerRecovery;
    public bool SupportsControllerRecovery => _database.SchemaVersion is WorldDatabaseSchemaTen.Version or WorldDatabaseSchemaEleven.Version;

    /// <summary>Explicit fresh operational policy; canonical controller world bytes remain unchanged.</summary>
    public static SqliteWorldAuthority CreateRecoverable(string path, WorldSnapshot genesis)
    {
        ArgumentNullException.ThrowIfNull(genesis);
        WorldGenesisAdmission.Validate(genesis);
        if (genesis.ProfileVersion is not (36 or 37 or 38) || genesis.Controllers is null)
            throw new ArgumentException("Recoverable storage requires an admitted controller genesis.", nameof(genesis));
        byte[] bytes = WorldSnapshotCodec.Encode(genesis);
        var accepted = WorldSnapshotCodec.Decode(bytes);
        WorldDatabase database = WorldDatabase.Open(path, true, WorldDatabaseSchemaTen.Version);
        try
        {
            database.Initialize(bytes);
            return new(database, new RecoveredWorld(accepted, accepted, ContentHash.Compute(bytes), new WorldJournalUsage(0, 0, 0, 0, 0)));
        }
        catch { database.Dispose(); throw; }
    }

    /// <summary>Publishes only a new destination, retaining every original canonical row and explicit unknown-duration provenance.</summary>
    public static SqliteWorldAuthority UpgradeControllerRecovery(string sourcePath, string destinationPath)
    {
        var publication = new WorldUpgradePublication(sourcePath, destinationPath);
        using (WorldDatabase original = WorldDatabase.Open(publication.SourcePath, false))
        {
            if (original.SchemaVersion != WorldDatabaseSchemaNine.Version)
                throw new InvalidDataException("Controller recovery activation requires an exact schema-nine source.");
            RecoveredWorld retained = new WorldJournalRecovery().Recover(original);
            string staging = publication.CreateStagingCopy(original, "controller-recovery");
            using (WorldDatabase candidate = WorldDatabase.Open(staging, false))
            {
                WorldUpgradePublication.RequireExactCopy(original, candidate, retained);
                candidate.ActivateControllerRecovery(retained.State);
                RecoveredWorld activated = new WorldJournalRecovery().Recover(candidate);
                if (activated.StateHash != retained.StateHash || activated.Usage != retained.Usage)
                    throw new InvalidDataException("Controller recovery activation changed canonical history.");
                candidate.CheckpointForPublication();
            }
            publication.Publish(() => { }, () => { });
        }
        return OpenExisting(destinationPath);
    }

    internal IReadOnlyList<ControllerAllowanceProgress> ReadControllerAllowancesForHost()
    {
        lock (_gate) { EnsureAvailable(); return RequireControllerRecovery().Allowances; }
    }

    internal ControllerRecoverySegment BeginControllerRecoverySegment(Action<AuthorityCommitBoundary>? fault = null)
    {
        lock (_gate)
        {
            EnsureAvailable();
            var current = RequireControllerRecovery();
            var next = current with { Generation = checked(current.Generation + 1) };
            PersistControllerRecovery(next, fault);
            return new(next.Generation, next.Allowances);
        }
    }

    internal void CheckpointControllerAllowances(ulong generation, IReadOnlyList<ControllerAllowanceProgress> progress,
        Action<AuthorityCommitBoundary>? fault = null)
    {
        ArgumentNullException.ThrowIfNull(progress);
        lock (_gate)
        {
            EnsureAvailable();
            var current = RequireControllerRecovery();
            if (generation == 0 || generation != current.Generation)
                throw new InvalidOperationException("A stale controller recovery segment cannot checkpoint progress.");
            // Copy before validation and persistence; a caller cannot mutate the accepted rowset afterwards.
            var next = current with { Allowances = Array.AsReadOnly(progress.ToArray()) };
            next.ValidateHead(_state);
            if (next.Allowances.Where((row, index) => row.ElapsedHostMilliseconds < current.Allowances[index].ElapsedHostMilliseconds).Any())
                throw new InvalidOperationException("Confirmed controller elapsed progress cannot decrease.");
            if (fault is null && next.Allowances.SequenceEqual(current.Allowances)) return;
            PersistControllerRecovery(next, fault);
        }
    }

    private ControllerRecoveryState RequireControllerRecovery() => _controllerRecovery
        ?? throw new InvalidOperationException("This save has not explicitly admitted controller recovery.");

    private void PersistControllerRecovery(ControllerRecoveryState next, Action<AuthorityCommitBoundary>? fault)
    {
        try
        {
            using var transaction = _database.BeginWrite();
            _database.WriteControllerRecovery(next, transaction);
            fault?.Invoke(AuthorityCommitBoundary.BeforeCommit);
            transaction.Commit();
            fault?.Invoke(AuthorityCommitBoundary.AfterCommit);
            _controllerRecovery = next;
        }
        catch { _requiresRecovery = true; throw; }
    }
}
