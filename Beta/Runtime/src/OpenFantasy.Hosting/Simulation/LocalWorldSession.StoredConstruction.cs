using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Diagnostics;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Simulation;

public sealed partial class LocalWorldSession
{
    private readonly int? _storedConstructionOwnersPerSlice;
    private StoredConstructionPublication? _pendingStoredConstruction;

    private bool BeginStoredConstruction(WorldStepBatch batch, PendingLocalAction? awaiting)
    {
        if (_storedConstructionOwnersPerSlice is null || awaiting is not null
            || batch.Kind != WorldBatchKind.AdvancingStep || batch.Movement.Count != 0
            || batch.Actions.Count == 0 || batch.Actions.Any(command => command.Intent is not WorkConstructionIntent))
        {
            return false;
        }
        _authority.EvictForStoredConstruction(batch.Actions);
        _pendingStoredConstruction = _authority.BeginStoredConstruction(batch);
        ContinueStoredConstruction();
        return true;
    }

    /// <summary>One serialized host opportunity; pending input remains queued behind the selected cutoff.</summary>
    private bool ContinueStoredConstruction()
    {
        if (_pendingStoredConstruction is not { } publication) { return false; }
        bool ready;
        lock (_inputs)
        {
            ready = !_stopRequested && _players.Values.Any(player => player.Ready);
        }
        if (!ready)
        {
            CancelStoredConstruction();
            return true;
        }
        IReadOnlyList<WorldActionReceipt> receipts;
        try
        {
            if (!_authority.ReadStoredConstruction(publication, _storedConstructionOwnersPerSlice!.Value))
            {
                return true;
            }
            HostTimingEvents.Log.Stage(TimingOwner, "commit.begin");
            receipts = _authority.PublishStoredConstruction(publication);
        }
        catch (AuthorityDispatchException) when (!_authority.IsStoredConstructionSourceCurrent(publication))
        {
            // A real intervention invalidated this unpublished selection. Never replay its old commands.
            CancelStoredConstruction();
            PublishViewIfChanged(_authority.ReadStateForHost());
            return true;
        }
        _pendingStoredConstruction = null;
        // Existing view and observation contracts require a complete committed world.
        var state = _authority.ReadStateForHost();
        HostTimingEvents.Log.Stage(TimingOwner, "commit.end", unchecked((long)state.Revision));
        ObserveCommittedBatch(state, receipts, []);
        return true;
    }

    private void CancelStoredConstruction()
    {
        if (_pendingStoredConstruction is not { } publication) { return; }
        _authority.CancelStoredConstruction(publication);
        _pendingStoredConstruction = null;
    }
}
