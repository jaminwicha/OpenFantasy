using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>The outcome of one explicit saved-history check: the verified head and whether replay reproduced it.</summary>
public sealed record LocalHistoryVerification(ulong Revision, bool ReproducesHead);

public sealed partial class LocalWorldSession
{
    private Task<LocalHistoryVerification>? _historyVerification;

    /// <summary>
    /// Explicit integrity check of the saved history: one complete verified replay of the recorded journal to the
    /// current head, in the background history slot and under the authority's writer gate, exactly like an archived
    /// inspection. Only whether the replay reproduced the pinned head is returned; no recorded state leaves the host.
    /// No step is selected while it reads, and published views stay readable throughout. A running check is shared.
    /// </summary>
    public Task<LocalHistoryVerification> VerifyRecordedHistoryAsync()
    {
        lock (_inputs)
        {
            EnsureInputAvailable();
            if (_historyVerification is { IsCompleted: false } running) return running;
            return _historyVerification = ReadHistoryAsync(() =>
            {
                WorldSnapshot head = _authority.ReadStateForHost();
                RecordedWorldState recorded = _authority.ReadRecordedStateForHost(head.Revision);
                return new LocalHistoryVerification(head.Revision, recorded.State.ContentHash == head.ContentHash);
            });
        }
    }

    /// <summary>
    /// Explicit participant inspection, including archived outcomes. Starts no simulation step and
    /// never replays on the calling thread. One lookup may be outstanding; an identical request shares
    /// that task and a different request is refused. There is no result cache or auto-retry.
    /// </summary>
    public Task<MaterialProposalView?> InspectMaterialProposalAsync(EntityId proposalId) => InspectMaterialProposalAsync(_localPlayer, proposalId);

    internal Task<MaterialProposalView?> InspectMaterialProposalAsync(LocalPlayerConnection player, EntityId proposalId)
    {
        lock (_inputs)
        {
            EnsurePlayerAvailable(player);
            return player.ProposalInspection.Admit(proposalId, subject => ReadHistoryAsync(() =>
                _authority.ReadRecordedProposalForController(subject, player.ActorId, player.Delivery,
                    (state, archived) => MaterialProposalService.ProjectRecorded(state, archived, subject, player.ActorId))));
        }
    }

    /// <summary>
    /// Runs one durable participant history read in the session's serialized slot, on a worker. It selects
    /// no step; a backend failure faults the session rather than returning a stale or cached answer.
    /// </summary>
    private async Task<T> ReadHistoryAsync<T>(Func<T> read)
    {
        await _serialization.WaitAsync(_stop.Token).ConfigureAwait(false);
        try
        {
            lock (_inputs) { EnsureInputAvailable(); }
            try
            {
                // Acquiring an available session slot is cheap; the durable read must run on a
                // worker even when that acquisition completed synchronously on a UI thread.
                return await Task.Run(read).ConfigureAwait(false);
            }
            catch (AuthorityDispatchException) { throw; }
            catch (Exception error) { Fail(error); throw; }
        }
        finally { _serialization.Release(); }
    }

    private async Task FinishInspectionsAsync()
    {
        // Stop prevents new inspection admission before this method is called. Waiting here keeps
        // the durable owner alive until a selected replay completes or a queued query is cancelled.
        Task?[] inspections;
        lock (_inputs)
        {
            inspections = _players.Values.SelectMany(player => new Task?[]
                { player.ProposalInspection.Latest, player.QuoteInspection.Latest }).Append(_historyVerification).ToArray();
        }
        foreach (Task? inspection in inspections)
        {
            if (inspection is null) { continue; }
            try { await inspection.ConfigureAwait(false); }
            catch (OperationCanceledException) when (_stopRequested) { }
            catch (ObjectDisposedException) when (_stopRequested) { }
            catch (AuthorityDispatchException) { }
        }
    }
}
