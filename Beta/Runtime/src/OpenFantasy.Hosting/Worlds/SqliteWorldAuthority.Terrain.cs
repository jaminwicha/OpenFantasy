using OpenFantasy.Core.Construction.Ownership;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

public sealed partial class SqliteWorldAuthority
{
    private int _completeEncodingOverhead;

    /// <summary>Explicit internal storage experiment; never a default creation or implicit migration.</summary>
    internal static SqliteWorldAuthority CreateWithSeparatedTerrain(string path, WorldSnapshot genesis) =>
        CreateSeparated(path, genesis, WorldDatabaseSchemaThirteen.Version);

    internal static SqliteWorldAuthority CreateWithTypedOwners(string path, WorldSnapshot genesis) =>
        CreateSeparated(path, genesis, WorldDatabaseSchemaFourteen.Version);

    private static SqliteWorldAuthority CreateSeparated(string path, WorldSnapshot genesis, int schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(genesis);
        if (genesis.ProfileVersion != 5) throw new ArgumentException("Separated terrain requires profile five.", nameof(genesis));
        WorldGenesisAdmission.Validate(genesis);
        byte[] original = WorldSnapshotCodec.Encode(genesis);
        var accepted = WorldSnapshotCodec.Decode(original);
        var database = WorldDatabase.Open(path, true, schemaVersion);
        try
        {
            database.Initialize(original);
            return new(database, new(accepted, accepted, ContentHash.Compute(database.EncodeHead(accepted)), new(0, 0, 0, 0, 0)));
        }
        catch { database.Dispose(); throw; }
    }

    internal ConstructionOwnerDirectory ReadOwnersForHost()
    {
        lock (_gate)
        {
            EnsureAvailable();
            if (!_database.SeparatesOwners) throw new InvalidOperationException("This save has no typed owner directory.");
            var directory = _residentState is { } world ? ConstructionOwnerDirectory.Capture(world)
                : ConstructionOwnerDirectory.Capture(_constructionLocality ?? throw new InvalidOperationException("No admitted owners are resident."));
            if (directory.Hash != _stateHash) throw new InvalidDataException("Resident owners do not match the committed head.");
            return directory;
        }
    }

    internal int TerrainArchiveReads => _database.TerrainArchiveReads;
    internal Action? BeforeTerrainRead { get => _database.BeforeTerrainRead; set => _database.BeforeTerrainRead = value; }

    /// <summary>Captures the current exact locality and releases authoritative terrain residency.</summary>
    internal ConstructionLocalityState EvictConstructionTerrain(EntityId projectId, int maximumMetadataPages)
    {
        lock (_gate)
        {
            EnsureAvailable();
            if (!_database.SeparatesTerrain) throw new InvalidOperationException("This save retains complete-root storage.");
            var current = _state;
            var locality = ConstructionLocalityState.Capture(current, projectId, maximumMetadataPages);
            byte[] image = ConstructionLocalityEncoding.Encode(locality);
            if (ContentHash.Compute(WorldStateImage.Capture(locality, _database.StateEncoding).Bytes) != _stateHash) throw new InvalidDataException("Locality does not match the committed image.");
            _completeEncodingOverhead = checked(WorldSnapshotCodec.Encode(current).Length - image.Length);
            _constructionLocality = locality;
            _constructionLocalityHash = _stateHash;
            _constructionMetadataPages = maximumMetadataPages;
            _residentState = null;
            return locality;
        }
    }

    private WorldSnapshot RestoreTerrain()
    {
        try
        {
            var locality = _constructionLocality ?? throw new InvalidOperationException("No admitted locality is resident.");
            return LoadTerrain(locality);
        }
        catch { _requiresRecovery = true; throw; }
    }

    private WorldSnapshot LoadTerrain(ConstructionLocalityState locality,
        Microsoft.Data.Sqlite.SqliteTransaction? transaction = null)
    {
        var restored = locality.Rehydrate(locality.Terrain.Select(reference => _database.ReadTerrain(reference, transaction)));
        _ = WorldSnapshotCodec.Encode(restored); // Preserve the complete legacy admission/storage envelope.
        if (!ConstructionLocalityEncoding.Encode(restored).AsSpan().SequenceEqual(ConstructionLocalityEncoding.Encode(locality)))
            throw new InvalidDataException("Restored terrain and owners do not match their exact locality image.");
        return restored;
    }

    /// <summary>Publishes one selected work boundary without reading terrain except for final physical installation.</summary>
    internal ConstructionLocalityStep CommitWithoutTerrain(WorldStepBatch batch, Action<AuthorityCommitBoundary>? fault = null)
    {
        ArgumentNullException.ThrowIfNull(batch);
        lock (_gate)
        {
            EnsureAvailable();
            if (!_database.SeparatesTerrain || _residentState is not null || _constructionLocality is not { } source
                || _constructionLocalityHash != _stateHash)
                throw new AuthorityDispatchException("Selected unloaded work requires the current evicted locality.");
            foreach (var command in batch.Actions)
                if (!source.Inventory.CanControl(new(command.PrincipalId, command.OriginSessionId), command.ActorId))
                    throw new AuthorityDispatchException("The selected work requires its actual current controller.");
            using var transaction = _database.BeginWrite();
            ConstructionLocalityStep step;
            try
            {
                step = source.PrepareStep(batch, source.Occupancy, source.Terrain,
                    identity => _detachedGenesisIdentities.Contains(identity) || _database.ContainsIdentity(identity, transaction));
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
            {
                throw new AuthorityDispatchException("The selected work does not match the current unloaded interval.");
            }
            byte[] after = ConstructionLocalityEncoding.Encode(step.Candidate);
            int completeLength = checked(after.Length + _completeEncodingOverhead);
            if (completeLength <= 0 || completeLength > WorldSnapshotCodec.MaximumStateByteLength)
                throw new InvalidDataException("Unloaded work exceeds the complete admitted storage envelope.");
            WorldSnapshot? completed = null;
            if (!source.Construction.FindProject(source.ProjectId)!.IsCompleted
                && step.Candidate.Construction.FindProject(source.ProjectId)!.IsCompleted)
            {
                try
                {
                    completed = LoadTerrain(step.Candidate, transaction);
                    if (WorldSnapshotCodec.Encode(completed).Length != completeLength)
                        throw new InvalidDataException("Complete installation disagrees with the retained byte envelope.");
                }
                catch { _requiresRecovery = true; throw; }
            }
            var image = _database.SeparatesOwners ? WorldStateImage.Capture(step.Candidate, _database.StateEncoding) : new WorldStateImage(after);
            var writes = WorldCommitPreparation.PrepareLocality(_stateHash, batch, step, image);
            var stored = writes.Stored;
            var advance = WorldJournalPolicy.Advance(_usage, stored.Sequence, step.Candidate.AdvancingSteps, 1,
                (ulong)stored.AcceptedBatch.Length, (ulong)stored.Outcomes.Length, profileVersion: 5);
            try
            {
                PublishWrites(writes, advance, null, transaction, fault);
                _constructionLocality = step.Candidate;
                _constructionLocalityHash = _stateHash = stored.AfterHash;
                _residentState = completed;
                _usage = advance.Usage;
                return step;
            }
            catch { _requiresRecovery = true; throw; }
        }
    }
}
