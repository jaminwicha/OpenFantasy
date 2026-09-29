using OpenFantasy.Core;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Simulation;

public sealed partial class LocalWorldSession
{
    private EntityId _inspectedProposalId;
    private Task<MaterialProposalView?>? _proposalInspection;

    /// <summary>
    /// Explicit participant inspection, including archived outcomes. Starts no simulation step and
    /// never replays on the calling thread. One lookup may be outstanding; an identical request
    /// shares that task and a different request is refused. There is no result cache or auto-retry.
    /// </summary>
    public Task<MaterialProposalView?> InspectMaterialProposalAsync(EntityId proposalId)
    {
        lock (_inputs)
        {
            EnsureInputAvailable();
            if (_proposalInspection is { IsCompleted: false } inspection)
            {
                if (_inspectedProposalId == proposalId)
                {
                    return inspection;
                }
                throw new AuthorityDispatchException("One proposal inspection is already awaiting its result.");
            }
            _inspectedProposalId = proposalId;
            _proposalInspection = InspectProposalCoreAsync(proposalId);
            return _proposalInspection;
        }
    }

    private async Task<MaterialProposalView?> InspectProposalCoreAsync(EntityId proposalId)
    {
        await _serialization.WaitAsync(_stop.Token).ConfigureAwait(false);
        try
        {
            lock (_inputs) { EnsureInputAvailable(); }
            try
            {
                // Acquiring an available session slot is cheap; the durable read must run on a
                // worker even when that acquisition completed synchronously on a UI thread.
                return await Task.Run(() => _authority.ReadRecordedProposalForController(proposalId, _playerActorId, _delivery,
                    (state, archived) => MaterialProposalService.ProjectRecorded(state, archived,
                        proposalId, _playerActorId))).ConfigureAwait(false);
            }
            catch (AuthorityDispatchException) { throw; }
            catch (Exception error) { Fail(error); throw; }
        }
        finally { _serialization.Release(); }
    }

    private async Task FinishProposalInspectionAsync()
    {
        // Stop prevents new inspection admission before this method is called. Waiting here keeps
        // the durable owner alive until a selected replay completes or a queued query is cancelled.
        Task<MaterialProposalView?>? inspection;
        lock (_inputs) { inspection = _proposalInspection; }
        if (inspection is null) { return; }
        try { await inspection.ConfigureAwait(false); }
        catch (OperationCanceledException) when (_stopRequested) { }
        catch (ObjectDisposedException) when (_stopRequested) { }
        catch (AuthorityDispatchException) { }
    }
}
