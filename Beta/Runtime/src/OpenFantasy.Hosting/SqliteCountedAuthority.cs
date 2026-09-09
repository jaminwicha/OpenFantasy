using Microsoft.Data.Sqlite;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;

namespace OpenFantasy.Hosting;

/// <summary>
/// Durable counted-domain authority for an embedded or dedicated host. Call from the host's
/// serialized work loop, never an engine frame callback. Authentication precedes this boundary.
/// </summary>
public sealed class SqliteCountedAuthority : IDisposable
{
    private readonly object _gate = new();
    private readonly SqliteAuthorityDatabase _database;
    private readonly CountedAuthorityState _genesis;
    private readonly AuthorityCommitPreparation _preparation = new();
    private CountedAuthorityState _state;
    private ulong _sequence;
    private bool _disposed;
    private bool _requiresRecovery;

    private SqliteCountedAuthority(SqliteAuthorityDatabase database, RecoveredAuthority recovered)
    {
        _database = database;
        _genesis = recovered.Genesis;
        _state = recovered.State;
        _sequence = recovered.Sequence;
    }

    public static SqliteCountedAuthority CreateNew(string path, CountedAuthorityState genesis)
    {
        ArgumentNullException.ThrowIfNull(genesis);
        byte[] accepted = AuthorityEncodingV1.Encode(genesis);
        SqliteAuthorityDatabase database = SqliteAuthorityDatabase.Open(path, create: true);
        try
        {
            database.Initialize(accepted);
            return new SqliteCountedAuthority(database, new RecoveredAuthority(genesis, genesis, 0));
        }
        catch
        {
            database.Dispose();
            throw;
        }
    }

    public static SqliteCountedAuthority OpenExisting(string path)
    {
        SqliteAuthorityDatabase database = SqliteAuthorityDatabase.Open(path, create: false);
        try
        {
            return new SqliteCountedAuthority(database, new AuthorityJournalRecovery().Recover(database));
        }
        catch
        {
            database.Dispose();
            throw;
        }
    }

    public CommandReceipt Execute(CommandEnvelope command, TrustedCommandDelivery delivery) => Execute(command, delivery, null);

    internal CommandReceipt Execute(CommandEnvelope command, TrustedCommandDelivery delivery, Action<AuthorityCommitBoundary>? fault)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(delivery);
        lock (_gate)
        {
            EnsureAvailable();
            if (command.WorldId != _state.WorldId || command.PrincipalId != delivery.PrincipalId
                || !_state.CanControl(delivery, command.ControlledActorId))
            {
                throw new AuthorityDispatchException("The delivery cannot access this command stream.");
            }

            using SqliteTransaction transaction = _database.BeginWrite();
            StoredAuthorityCommit? prior = _database.FindCommand(command.PrincipalId, command.CommandId, transaction);
            if (prior is not null)
            {
                if (!prior.Command.AsSpan().SequenceEqual(CommandCodecV1.Encode(command)))
                {
                    throw new AuthorityDispatchException("The command identity already names different content.");
                }
                return CommandReceiptCodecV1.Decode(prior.Receipt);
            }

            // Origin session is immutable retry content; only a fresh command needs to match it.
            if (command.OriginSessionId != delivery.SessionId)
            {
                throw new AuthorityDispatchException("A fresh command must originate in the current session.");
            }

            PreparedAuthorityCommit prepared = _preparation.Prepare(_state, _genesis, command, delivery,
                checked(_sequence + 1), identity => _database.ContainsIdentity(identity, transaction));
            try
            {
                _database.WriteCommit(prepared.Stored, transaction);
                foreach (StoredIdentity identity in prepared.Identities)
                {
                    _database.ReserveIdentity(identity.Identity, identity.Sequence, identity.Kind, transaction);
                }
                _database.WriteHead(prepared.Stored.Sequence, prepared.Stored.AfterState, transaction);
                fault?.Invoke(AuthorityCommitBoundary.BeforeCommit);
                transaction.Commit();
                fault?.Invoke(AuthorityCommitBoundary.AfterCommit);
                _state = prepared.State;
                _sequence = prepared.Stored.Sequence;
                return prepared.Receipt;
            }
            catch
            {
                // Commit failures and lost replies have an uncertain outcome for the caller.
                // Close, reopen and submit the exact command; never synthesize a game rejection.
                _requiresRecovery = true;
                throw;
            }
        }
    }

    /// <summary>Privileged host inspection. Player projections must be produced separately.</summary>
    public AuthorityInspection InspectForHost()
    {
        lock (_gate)
        {
            EnsureAvailable();
            return new AuthorityInspection(_state, _sequence, _database.Inspect());
        }
    }

    private void EnsureAvailable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_requiresRecovery)
        {
            throw new InvalidOperationException("The world must be reopened before another operation.");
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

public sealed record AuthorityInspection(CountedAuthorityState State, ulong CommitSequence, SqliteAuthorityDiagnostics Database);
public sealed class AuthorityDispatchException(string message) : InvalidOperationException(message);
internal enum AuthorityCommitBoundary { BeforeCommit, AfterCommit }
