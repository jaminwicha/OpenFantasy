using Microsoft.Data.Sqlite;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Worlds;

public sealed partial class SqliteWorldAuthority
{
    /// <summary>Null in production; holds an authorized directory read inside the actual storage gate in tests.</summary>
    internal Action? PrivateHistoryReadHoldsGate { get; set; }

    internal LocalPrivateHistoryPage ReadSelectedPrivateHistory(EntityId actor, TrustedCommandDelivery delivery,
        WorldControlWitness? witness, LocalPrivateHistoryPageRequest request)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            EnsureAvailable();
            RequireSelectedInspection(actor, delivery, witness, WorldControlCapabilities.PrivateHistory);
            ValidatePrivateHistoryRequest(request, _state.Revision);
            ulong throughRevision = request.Before?.ThroughRevision ?? _state.Revision;
            PrivateHistoryReadHoldsGate?.Invoke();
            try
            {
                RecordedPrivateHistoryPage recorded = new WorldJournalRecovery().ReadPrivateHistory(
                    _database, actor, request, throughRevision);
                if (recorded.SourceHeadRevision != _state.Revision || recorded.SourceHeadHash != _stateHash)
                    throw new InvalidDataException("History directory retrieval does not match the pinned live head.");
                return recorded.Page;
            }
            catch (Exception error) when (error is IOException or InvalidDataException or SqliteException)
            {
                _requiresRecovery = true;
                throw;
            }
        }
    }

    private static void ValidatePrivateHistoryRequest(LocalPrivateHistoryPageRequest request, ulong headRevision)
    {
        try { request.Validate(); }
        catch (ArgumentException)
        {
            throw new AuthorityDispatchException("The history directory cursor is malformed.");
        }
        if (request.Before is { } before && before.ThroughRevision > headRevision)
            throw new AuthorityDispatchException("The history directory cursor is beyond the current world.");
    }
}

internal sealed record RecordedPrivateHistoryPage(LocalPrivateHistoryPage Page,
    ulong SourceHeadRevision, ContentHash SourceHeadHash);
