using OpenFantasy.Core;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Simulation;

public sealed partial class LocalWorldSession
{
    private readonly LocalInspectionSlot<MaterialProposalView?> _proposalInspection =
        new("One proposal inspection is already awaiting its result.");

    /// <summary>
    /// Explicit participant inspection, including archived outcomes. Starts no simulation step and
    /// never replays on the calling thread. One lookup may be outstanding; an identical request shares
    /// that task and a different request is refused. There is no result cache or auto-retry.
    /// </summary>
    public Task<MaterialProposalView?> InspectMaterialProposalAsync(EntityId proposalId)
    {
        lock (_inputs)
        {
            EnsureInputAvailable();
            return _proposalInspection.Admit(proposalId, InspectProposalCoreAsync);
        }
    }

    private Task<MaterialProposalView?> InspectProposalCoreAsync(EntityId proposalId) =>
        ReadHistoryAsync(() => _authority.ReadRecordedProposalForController(proposalId, _playerActorId, _delivery,
            (state, archived) => MaterialProposalService.ProjectRecorded(state, archived, proposalId, _playerActorId)));

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
        lock (_inputs) { inspections = [_proposalInspection.Latest, _quoteInspection.Latest]; }
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
