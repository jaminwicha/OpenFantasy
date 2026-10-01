using Microsoft.Data.Sqlite;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

/// <summary>
/// One in-process durable local-world owner. The host authenticates callers and selects fixed-step
/// inputs before this privileged boundary; pending client input is not an accepted durable command.
/// </summary>
public sealed partial class SqliteWorldAuthority : IDisposable
{
    private readonly object _gate = new();
    private readonly WorldDatabase _database;
    private readonly WorldIdentityIndex _genesisIdentities;
    private readonly WorldCommitPreparation _preparation = new();
    private WorldSnapshot _state;
    private ContentHash _stateHash;
    private WorldJournalUsage _usage;
    private bool _disposed;
    private bool _requiresRecovery;

    private SqliteWorldAuthority(WorldDatabase database, RecoveredWorld recovered)
    {
        _database = database;
        _genesisIdentities = recovered.Genesis.IdentityIndex;
        _state = recovered.State;
        _stateHash = recovered.StateHash;
        _usage = recovered.Usage;
    }

    public static SqliteWorldAuthority CreateNew(string path, WorldSnapshot genesis)
    {
        ArgumentNullException.ThrowIfNull(genesis);
        WorldGenesisAdmission.Validate(genesis);
        byte[] bytes = WorldSnapshotCodec.Encode(genesis);
        WorldSnapshot accepted = WorldSnapshotCodec.Decode(bytes);
        // Each interpretation is named. A fresh consented world shares the coordinated physical
        // layout; its genesis, never the schema number, selects how the stored roots are read.
        int schemaVersion = accepted.ProfileVersion switch
        {
            27 or 28 or 30 or 32 or 33 or 34 => WorldDatabaseSchemaEight.Version,
            25 or 26 => WorldDatabaseSchemaSeven.Version,
            24 => WorldDatabaseSchemaSix.Version,
            22 or 23 => WorldDatabaseSchemaFive.Version,
            16 or 17 or 18 => WorldDatabaseSchemaFour.Version,
            3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 19 or 20 or 21 => WorldDatabaseSchemaThree.Version,
            _ => WorldDatabaseSchema.Version
        };
        WorldDatabase database = WorldDatabase.Open(path, create: true, schemaVersion);
        try
        {
            database.Initialize(bytes);
            return new SqliteWorldAuthority(database, new RecoveredWorld(accepted, accepted,
                ContentHash.Compute(bytes), new WorldJournalUsage(0, 0, 0, 0, 0)));
        }
        catch
        {
            database.Dispose();
            throw;
        }
    }

    public static SqliteWorldAuthority OpenExisting(string path)
    {
        WorldDatabase database = WorldDatabase.Open(path, create: false);
        try
        {
            return new SqliteWorldAuthority(database, new WorldJournalRecovery().Recover(database));
        }
        catch
        {
            database.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens an existing save only when it began from the exact expected fresh genesis. The complete
    /// journal and its archives are recovered once, exactly as ordinary opening does; the expectation
    /// selects a save and never creates one, replaces its history or stands in for that recovery.
    /// </summary>
    public static SqliteWorldAuthority OpenExisting(string path, ReadOnlySpan<byte> expectedGenesis)
    {
        byte[] expected = expectedGenesis.ToArray();
        WorldSnapshot fresh = WorldSnapshotCodec.Decode(expected);
        WorldGenesisAdmission.Validate(fresh);
        if (!WorldSnapshotCodec.Encode(fresh).AsSpan().SequenceEqual(expected))
        {
            throw new ArgumentException("The expected genesis must be its exact canonical encoding.", nameof(expectedGenesis));
        }
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("An expected genesis selects an existing world; it cannot create one.", path);
        }
        WorldDatabase database = WorldDatabase.Open(path, create: false);
        try
        {
            RecoveredWorld recovered = new WorldJournalRecovery().Recover(database);
            if (recovered.Genesis.ContentHash != ContentHash.Compute(expected)
                || !database.ReadGenesis().AsSpan().SequenceEqual(expected))
            {
                throw new InvalidDataException("This save did not begin from the expected genesis.");
            }
            return new SqliteWorldAuthority(database, recovered);
        }
        catch
        {
            database.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Copies and upgrades an exact gathering save into a fresh destination. The original remains
    /// a recovery copy; ordinary opening never migrates, overwrites, or silently selects that old copy.
    /// </summary>
    public static SqliteWorldAuthority UpgradeGatheringToProduction(string sourcePath, string destinationPath,
        WorldProductionMigration request) => UpgradeGatheringToProduction(sourcePath, destinationPath, request, null);

    internal static SqliteWorldAuthority UpgradeGatheringToProduction(string sourcePath, string destinationPath,
        WorldProductionMigration request, Action<WorldProductionUpgradeBoundary>? fault)
    {
        new WorldProductionUpgrade().Publish(sourcePath, destinationPath, request, fault);
        return OpenExisting(destinationPath);
    }

    /// <summary>
    /// Copies an exact production save and adds explicitly funded quest admission. The original
    /// retains all prior progress; ordinary opening does not upgrade or select a fallback save.
    /// </summary>
    public static SqliteWorldAuthority UpgradeProductionToQuests(string sourcePath, string destinationPath,
        WorldQuestMigration request) => UpgradeProductionToQuests(sourcePath, destinationPath, request, null);

    internal static SqliteWorldAuthority UpgradeProductionToQuests(string sourcePath, string destinationPath,
        WorldQuestMigration request, Action<WorldQuestUpgradeBoundary>? fault)
    {
        new WorldQuestUpgrade().Publish(sourcePath, destinationPath, request, fault);
        return OpenExisting(destinationPath);
    }

    /// <summary>
    /// Copies an exact quest save and admits construction definitions, a finite tool and an unstarted
    /// project. Existing progress and rewards remain in the original and its verified continuation.
    /// </summary>
    public static SqliteWorldAuthority UpgradeQuestsToConstruction(string sourcePath, string destinationPath,
        WorldConstructionMigration request) => UpgradeQuestsToConstruction(sourcePath, destinationPath, request, null);

    internal static SqliteWorldAuthority UpgradeQuestsToConstruction(string sourcePath, string destinationPath,
        WorldConstructionMigration request, Action<WorldConstructionUpgradeBoundary>? fault)
    {
        new WorldConstructionUpgrade().Publish(sourcePath, destinationPath, request, fault);
        return OpenExisting(destinationPath);
    }

    /// <summary>
    /// Copies an exact lived coordinated save and continues it under the saved-proposal interpretation
    /// in a new destination. The source and its complete history stay unchanged; opening never transitions.
    /// </summary>
    public static SqliteWorldAuthority UpgradeCoordinatedToProposals(string sourcePath, string destinationPath,
        WorldProposalTransition request) => UpgradeCoordinatedToProposals(sourcePath, destinationPath, request, null);

    internal static SqliteWorldAuthority UpgradeCoordinatedToProposals(string sourcePath, string destinationPath,
        WorldProposalTransition request, Action<WorldProposalTransitionBoundary>? fault)
    {
        new WorldProposalTransitionUpgrade().Publish(sourcePath, destinationPath, request, fault);
        return OpenExisting(destinationPath);
    }

    /// <summary>Commits one host-selected batch. A stale or reused batch is a dispatch failure, never a second execution.</summary>
    public WorldStepDecision Commit(WorldStepBatch batch) => Commit(batch, null);

    /// <summary>Same-assembly scheduler preflight. This candidate reserves nothing and must never be published.</summary>
    internal WorldStepDecision Preview(WorldStepBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        lock (_gate)
        {
            EnsureAvailable();
            PreparedWorldCommit prepared = _preparation.Prepare(_state, _genesisIdentities, _stateHash,
                batch, identity => _database.ContainsIdentity(identity));
            WorldJournalPolicy.Advance(_usage, prepared.Stored.Sequence, prepared.Stored.AdvancingSteps,
                (ulong)prepared.Commands.Count, (ulong)prepared.Stored.AcceptedBatch.Length,
                (ulong)prepared.Stored.Outcomes.Length,
                _state.PendingExcavation is null ? prepared.Decision.Candidate.PendingExcavation : null,
                _state.ProfileVersion, (ulong)(prepared.ResourceArchive?.State.Length ?? 0),
                (ulong)(prepared.MaterialCareArchive?.State.Length ?? 0));
            return prepared.Decision;
        }
    }

    internal bool IsIdentityOccupiedForHost(EntityId identity)
    {
        lock (_gate)
        {
            EnsureAvailable();
            return _state.IdentityIndex.Contains(identity) || _genesisIdentities.Contains(identity)
                || _database.ContainsIdentity(identity);
        }
    }

    /// <summary>Immutable state for the serialized host loop, without querying native diagnostics every step.</summary>
    internal WorldSnapshot ReadStateForHost()
    {
        lock (_gate)
        {
            EnsureAvailable();
            return _state;
        }
    }

    /// <summary>Creates a consistent new-file backup under the existing exclusive writer lease.</summary>
    internal void BackupForHost(string path)
    {
        lock (_gate)
        {
            EnsureAvailable();
            _database.BackupToNewFile(path);
        }
    }

    internal WorldStepDecision Commit(WorldStepBatch batch, Action<AuthorityCommitBoundary>? fault)
    {
        ArgumentNullException.ThrowIfNull(batch);
        lock (_gate)
        {
            EnsureAvailable();
            using SqliteTransaction transaction = _database.BeginWrite();
            PreparedWorldCommit prepared = _preparation.Prepare(_state, _genesisIdentities, _stateHash,
                batch, identity => _database.ContainsIdentity(identity, transaction));
            WorldJournalAdvance advance = WorldJournalPolicy.Advance(_usage, prepared.Stored.Sequence,
                prepared.Stored.AdvancingSteps, (ulong)prepared.Commands.Count,
                (ulong)prepared.Stored.AcceptedBatch.Length, (ulong)prepared.Stored.Outcomes.Length,
                _state.PendingExcavation is null ? prepared.Decision.Candidate.PendingExcavation : null,
                _state.ProfileVersion, (ulong)(prepared.ResourceArchive?.State.Length ?? 0),
                (ulong)(prepared.MaterialCareArchive?.State.Length ?? 0));
            try
            {
                _database.WriteCommit(prepared.Stored, transaction);
                if (prepared.ResourceArchive is { } archive)
                {
                    _database.WriteResourceArchive(archive, transaction);
                }
                if (prepared.MaterialCareArchive is { } materialCareArchive)
                {
                    _database.WriteMaterialCareArchive(materialCareArchive, transaction);
                }
                foreach (StoredWorldCommandOutcome command in prepared.Commands)
                {
                    _database.WriteCommandOutcome(command, transaction);
                }
                foreach (StoredWorldIdentity identity in prepared.Identities)
                {
                    _database.ReserveIdentity(identity, transaction);
                }
                _database.WriteHead(prepared.Stored.Sequence, prepared.Stored.AdvancingSteps,
                    prepared.CandidateBytes, transaction);
                if (advance.WriteCheckpoint)
                {
                    _database.WriteCheckpoint(new(prepared.Stored.Sequence, prepared.Stored.AdvancingSteps,
                        prepared.CandidateBytes), transaction);
                }
                fault?.Invoke(AuthorityCommitBoundary.BeforeCommit);
                transaction.Commit();
                fault?.Invoke(AuthorityCommitBoundary.AfterCommit);
                _state = prepared.Decision.Candidate;
                _stateHash = prepared.Stored.AfterHash;
                _usage = advance.Usage;
                return prepared.Decision;
            }
            catch
            {
                _requiresRecovery = true;
                throw;
            }
        }
    }

    /// <summary>
    /// Retrieves an exact terminal receipt only after current control checks. An old origin session
    /// remains immutable retry content for terminal or retained pending commands; fresh requests
    /// must instead match the current delivery session. A pending command has no terminal receipt yet.
    /// </summary>
    public WorldActionReceipt? FindReceipt(WorldActionCommand command, TrustedCommandDelivery delivery)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(delivery);
        lock (_gate)
        {
            EnsureAvailable();
            if (command.WorldId != _state.WorldId || command.BranchId != _state.BranchId
                || command.PrincipalId != delivery.PrincipalId || !_state.Inventory.CanControl(delivery, command.ActorId))
            {
                throw new AuthorityDispatchException("The delivery cannot access this actor command stream.");
            }
            StoredWorldCommandOutcome? prior = _database.FindCommand(command.PrincipalId, command.CommandId);
            if (prior is not null)
            {
                if (!prior.Command.AsSpan().SequenceEqual(WorldActionCodec.Encode(command)))
                {
                    throw new AuthorityDispatchException("The command identity already names different content.");
                }
                return WorldReceiptCodecV1.Decode(prior.Receipt);
            }
            if (_state.PendingExcavation is { } pending && pending.Command.CommandId == command.CommandId)
            {
                if (!WorldActionCodec.Encode(pending.Command).AsSpan().SequenceEqual(WorldActionCodec.Encode(command)))
                {
                    throw new AuthorityDispatchException("The pending command identity already names different content.");
                }
                return null;
            }
            if (command.OriginSessionId != delivery.SessionId)
            {
                throw new AuthorityDispatchException("A fresh command must originate in the current session.");
            }
            return null;
        }
    }

    /// <summary>Privileged inspection. The returned root is not a player-facing permission-filtered projection.</summary>
    public WorldInspection InspectForHost()
    {
        lock (_gate)
        {
            EnsureAvailable();
            return new WorldInspection(_state, _database.Inspect(), _usage);
        }
    }

    /// <summary>
    /// Reconstructs an exact recorded commit without moving the live head or replaying side effects.
    /// Privileged synchronous operator work: it verifies the complete bounded journal under the
    /// writer lock, so it must not run on a client frame or latency-sensitive simulation callback.
    /// Arbitrary interior instants, generated prehistory and branch traversal are not provided here.
    /// </summary>
    public RecordedWorldState ReadRecordedStateForHost(ulong revision)
    {
        lock (_gate)
        {
            EnsureAvailable();
            if (revision > _state.Revision)
            {
                throw new ArgumentOutOfRangeException(nameof(revision), "The requested commit exceeds the current recorded head.");
            }
            try
            {
                RecordedWorldState recorded = new WorldJournalRecovery().ReadRecordedState(_database, revision);
                if (recorded.SourceHeadRevision != _state.Revision || recorded.SourceHeadHash != _stateHash)
                {
                    throw new InvalidDataException("Historical reconstruction does not match the pinned live head.");
                }
                return recorded;
            }
            catch (Exception error) when (error is IOException or InvalidDataException or SqliteException)
            {
                _requiresRecovery = true;
                throw;
            }
        }
    }

    /// <summary>
    /// Retrieves jointly closed material, care and resource history after complete journal verification.
    /// Privileged operator work under the writer lock; it does not confer player knowledge.
    /// </summary>
    public RecordedWorldMaterialCareArchive ReadMaterialCareArchiveForHost(ulong ordinal)
    {
        lock (_gate)
        {
            EnsureAvailable();
            if (ordinal >= _usage.MaterialCareArchiveCount)
            {
                throw new ArgumentOutOfRangeException(nameof(ordinal), "The requested material and care archive has not been published.");
            }
            try
            {
                RecordedWorldMaterialCareArchive recorded = new WorldJournalRecovery().ReadMaterialCareArchive(_database, ordinal);
                if (recorded.SourceHeadRevision != _state.Revision || recorded.SourceHeadHash != _stateHash)
                {
                    throw new InvalidDataException("Archive retrieval does not match the pinned live head.");
                }
                return recorded;
            }
            catch (Exception error) when (error is IOException or InvalidDataException or SqliteException)
            {
                _requiresRecovery = true;
                throw;
            }
        }
    }

    /// <summary>
    /// Retrieves one archive from actual storage after complete replay verification under the writer
    /// lock. Privileged operator work; this grants no player visibility and is not a frame callback.
    /// </summary>
    public RecordedWorldResourceArchive ReadResourceArchiveForHost(ulong ordinal)
    {
        lock (_gate)
        {
            EnsureAvailable();
            if (ordinal >= _usage.ResourceArchiveCount)
            {
                throw new ArgumentOutOfRangeException(nameof(ordinal), "The requested resource archive has not been published.");
            }
            try
            {
                RecordedWorldResourceArchive recorded = new WorldJournalRecovery().ReadResourceArchive(_database, ordinal);
                if (recorded.SourceHeadRevision != _state.Revision || recorded.SourceHeadHash != _stateHash)
                {
                    throw new InvalidDataException("Archive retrieval does not match the pinned live head.");
                }
                return recorded;
            }
            catch (Exception error) when (error is IOException or InvalidDataException or SqliteException)
            {
                _requiresRecovery = true;
                throw;
            }
        }
    }

    /// <summary>
    /// Reconstructs one recorded batch's accepted material observations after complete journal,
    /// archive, receipt, identity and head verification. Privileged synchronous operator work under
    /// the writer lock; it does not publish state or provide a player-visible inspection projection.
    /// </summary>
    public RecordedWorldMaterialEvidence ReadMaterialEvidenceForHost(ulong sequence)
    {
        lock (_gate)
        {
            EnsureAvailable();
            if (_state.ProfileVersion != 26)
                throw new InvalidOperationException("Live material observation requires the ordinary-care world interpretation.");
            if (sequence == 0 || sequence > _state.Revision)
                throw new ArgumentOutOfRangeException(nameof(sequence), "Material observation requires an existing non-genesis commit.");
            try
            {
                var recorded = new WorldJournalRecovery().ReadMaterialEvidence(_database, sequence);
                if (recorded.SourceHeadRevision != _state.Revision || recorded.SourceHeadHash != _stateHash)
                    throw new InvalidDataException("Material observation does not match the pinned live head.");
                return recorded;
            }
            catch (Exception error) when (error is IOException or InvalidDataException or SqliteException)
            {
                _requiresRecovery = true;
                throw;
            }
        }
    }

    private void EnsureAvailable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_requiresRecovery)
        {
            throw new InvalidOperationException("The local world must be reopened before another operation.");
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _database.Dispose();
            _disposed = true;
        }
    }
}

public sealed record WorldInspection(WorldSnapshot State, SqliteAuthorityDiagnostics Database, WorldJournalUsage Journal);
