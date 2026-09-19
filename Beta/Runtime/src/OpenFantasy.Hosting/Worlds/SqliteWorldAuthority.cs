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
public sealed class SqliteWorldAuthority : IDisposable
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
        if (genesis.Revision != 0 || genesis.AdvancingSteps != 0 || genesis.HasReadingHold)
        {
            throw new ArgumentException("A new local world requires commit zero, step zero and no committed dialogue.", nameof(genesis));
        }
        byte[] bytes = WorldSnapshotCodec.Encode(genesis);
        WorldSnapshot accepted = WorldSnapshotCodec.Decode(bytes);
        WorldDatabase database = WorldDatabase.Open(path, create: true,
            accepted.ProfileVersion is 3 or 4 or 5 or 6 or 7 or 8 or 9 ? WorldDatabaseSchemaThree.Version : WorldDatabaseSchema.Version);
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
                (ulong)prepared.Stored.Outcomes.Length);
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
                (ulong)prepared.Stored.AcceptedBatch.Length, (ulong)prepared.Stored.Outcomes.Length);
            try
            {
                _database.WriteCommit(prepared.Stored, transaction);
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
    /// remains immutable retry content; fresh requests must instead match the current delivery session.
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
