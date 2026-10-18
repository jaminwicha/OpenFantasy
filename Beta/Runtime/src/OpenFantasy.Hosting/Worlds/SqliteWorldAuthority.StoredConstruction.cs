using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction.Ownership;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Worlds;

public sealed partial class SqliteWorldAuthority
{
    private StoredConstructionInterval? _storedConstruction;
    private HashSet<EntityId>? _storedConstructionIdentities;
    internal int OwnerArchiveReads => _database.OwnerArchiveReads;
    internal Action<ContentHash>? BeforeOwnerRead { get => _database.BeforeOwnerRead; set => _database.BeforeOwnerRead = value; }

    /// <summary>Admits exact dependency metadata, then releases complete inventory/production/terrain residency.</summary>
    internal StoredConstructionInterval EvictForStoredConstruction(WorldActionCommand command)
        => EvictForStoredConstruction([command]);

    internal StoredConstructionInterval EvictForStoredConstruction(IEnumerable<WorldActionCommand> commands)
    {
        lock (_gate)
        {
            EnsureAvailable();
            if (!_database.SeparatesOwners) throw new InvalidOperationException("Typed owner storage is required.");
            var world = _state;
            var interval = StoredConstructionInterval.Capture(world, commands);
            if (interval.DirectoryHash != _stateHash) throw new InvalidDataException("Work differs from the committed owners.");
            // Profile five has no prospective encounter families; keep actual entity reservations only.
            var identities = world.IdentityIndex.CopyEntityReservations();
            _storedConstruction = interval;
            _storedConstructionIdentities = identities.ToHashSet();
            _residentState = null;
            _constructionLocality = null;
            _constructionLocalityHash = null;
            return interval;
        }
    }

    /// <summary>Publishes selected partial work with every due update; final installation uses ordinary full admission.</summary>
    internal WorldActionReceipt CommitStoredConstruction(WorldStepBatch batch, Action<AuthorityCommitBoundary>? fault = null)
    {
        if (batch.Actions.Count != 1) throw new AuthorityDispatchException("A single receipt requires one selected action.");
        return CommitStoredConstructionBatch(batch, fault).Single();
    }

    internal IReadOnlyList<WorldActionReceipt> CommitStoredConstructionBatch(WorldStepBatch batch,
        Action<AuthorityCommitBoundary>? fault = null)
    {
        lock (_gate)
        {
            EnsureAvailable();
            if (_storedConstruction is not { } interval || interval.DirectoryHash != _stateHash)
                throw new AuthorityDispatchException("No exact stored construction interval is admitted.");
            StoredConstructionStep? step;
            try
            {
                step = interval.Prepare(batch, _stateHash, _database.ReadOwner, identity =>
                    _storedConstructionIdentities!.Contains(identity) || _detachedGenesisIdentities.Contains(identity)
                    || _database.ContainsIdentity(identity));
            }
            catch (Exception error) when (error is FormatException or InvalidDataException or IOException or Microsoft.Data.Sqlite.SqliteException)
            {
                _requiresRecovery = true;
                throw;
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
            {
                throw new AuthorityDispatchException("Selected work does not match the current stored interval.");
            }
            // No candidate, identity or receipt has been published. The normal interpreter owns completion.
            if (step is null) return Commit(batch, fault).Receipts;
            var candidate = step.Candidate;
            var stored = new StoredWorldCommit(candidate.Revision,
                batch.Kind == WorldBatchKind.AdvancingStep ? WorldCommitKind.AdvancingStep : WorldCommitKind.HeldInteraction,
                batch.CauseId, candidate.AdvancingSteps, WorldStepCodec.Encode(batch),
                WorldReceiptCodecV1.EncodeBatch(step.Receipts), _stateHash, candidate.DirectoryHash);
            var writes = new WorldCommitWriteSet(candidate.CopyDirectory(), stored,
                batch.Actions.Zip(step.Receipts, (command, receipt) => new StoredWorldCommandOutcome(command.PrincipalId,
                    command.CommandId, candidate.Revision, WorldActionCodec.Encode(command),
                    WorldReceiptCodecV1.Encode(receipt))).ToArray(),
                step.Identities.Select(value => new StoredWorldIdentity(value.Identity, candidate.Revision, value.Kind switch
                {
                    WorldCommittedIdentityKind.CommitCause => WorldIdentityKind.CommitCause,
                    WorldCommittedIdentityKind.Command => WorldIdentityKind.Command,
                    WorldCommittedIdentityKind.Result => WorldIdentityKind.Result,
                    _ => throw new InvalidDataException("Partial work cannot create a material identity.")
                })).ToArray(), ChangedOwners: step.ChangedOwners);
            var advance = WorldJournalPolicy.Advance(_usage, stored.Sequence, stored.AdvancingSteps, (ulong)batch.Actions.Count,
                (ulong)stored.AcceptedBatch.Length, (ulong)stored.Outcomes.Length, profileVersion: 5);
            using var transaction = _database.BeginWrite();
            try
            {
                PublishWrites(writes, advance, null, transaction, fault);
                _storedConstruction = candidate;
                _stateHash = candidate.DirectoryHash;
                _residentState = null;
                _usage = advance.Usage;
                return step.Receipts;
            }
            catch { _requiresRecovery = true; throw; }
        }
    }
}
