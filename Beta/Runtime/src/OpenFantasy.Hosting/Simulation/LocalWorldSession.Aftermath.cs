using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

public sealed partial class LocalWorldSession
{
    private bool TickAutonomousAftermath(WorldSnapshot state)
    {
        lock (_inputs)
        {
            // Accepted-work continuation during a control return grants no new automatic action.
            if (!_players.Values.Any(player => player.Ready)) return false;
        }
        WorldActionCommand? command = _autonomousAftermath.SelectChoice(state, _authority.IsIdentityOccupiedForHost);
        if (command is null) return false;
        WorldBatchKind kind = command.Intent is AcknowledgeCareIntent ? WorldBatchKind.Care : WorldBatchKind.EncounterLifecycle;
        var batch = new WorldStepBatch(NewCause(command.CommandId), state.WorldId, state.BranchId,
            state.Revision, state.AdvancingSteps, kind, [], [command], state.ProfileVersion);
        _authority.Preview(batch);
        CommitSelectedBatch(batch, (PendingLocalAction?)null);
        return true;
    }
}
