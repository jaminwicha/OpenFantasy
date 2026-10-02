using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

public sealed partial class LocalWorldSession
{
    // Only an observed transport loss starts this monotonic episode. Restoring a save does not.
    private readonly Dictionary<EntityId, DisconnectAllowance> _disconnectAllowances = [];

    internal async Task CompleteNetworkAdmissionAsync(LocalPlayerConnection player, CancellationToken cancellationToken,
        Action validateAdmission)
    {
        ArgumentNullException.ThrowIfNull(validateAdmission);
        await _serialization.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_inputs) { EnsurePlayerAvailable(player); }
            // Final credential/deadline validation linearizes establishment here, after any gate wait.
            // A refused credential is ordinary admission failure, not an authority failure.
            validateAdmission();
            lock (_inputs)
            {
                EnsurePlayerAvailable(player);
                player.NetworkEstablished = true;
            }
            try { RestorePlayerControlIfSafe(_authority.ReadStateForHost(), player); }
            catch (Exception error) { Fail(error); throw; }
        }
        finally { _serialization.Release(); }
    }

    internal async Task ConfirmTransportDisconnectedAsync(LocalPlayerConnection player, WorldControllerDisconnectReason reason)
    {
        if (!Enum.IsDefined(reason)) throw new ArgumentOutOfRangeException(nameof(reason));
        await _serialization.WaitAsync().ConfigureAwait(false);
        try
        {
            bool recordsLoss;
            lock (_inputs)
            {
                if (!IsCurrentPlayer(player)) return;
                recordsLoss = player.NetworkEstablished && !_stopRequested && _failure is null;
            }
            WorldSnapshot? state = recordsLoss ? _authority.ReadStateForHost() : null;
            if (state?.Controllers?.Get(player.ActorId) is { Mode: WorldControllerMode.Human } assignment)
            {
                long confirmedAt = _time.GetTimestamp();
                state = CommitControllerEvent(state, new WorldControllerEvent(player.ActorId,
                    WorldControllerEventKind.Disconnected, assignment.Revision, reason: reason));
                _disconnectAllowances[player.ActorId] = new(state.Controllers!.Get(player.ActorId).DisconnectId!.Value,
                    confirmedAt);
            }
            DetachPlayer(player);
        }
        catch (Exception error) { Fail(error); throw; }
        finally { _serialization.Release(); }
        await AwaitPlayerInspectionsAsync(player).ConfigureAwait(false);
    }

    /// <summary>External control events run even when every gameplay port is unready.</summary>
    private WorldSnapshot TickControllers(WorldSnapshot state)
    {
        if (state.Controllers is null) return state;
        foreach (LocalPlayerConnection player in _players.Values)
        {
            if (player.NetworkEstablished && !player.ControlAvailable)
                state = RestorePlayerControlIfSafe(state, player);
        }
        foreach ((EntityId actor, DisconnectAllowance allowance) in _disconnectAllowances.OrderBy(pair => pair.Key).ToArray())
        {
            WorldControllerAssignment assignment = state.Controllers!.Get(actor);
            if (assignment.Mode != WorldControllerMode.ReconnectAllowance || assignment.DisconnectId != allowance.EpisodeId)
            {
                _disconnectAllowances.Remove(actor);
                continue;
            }
            TimeSpan elapsed = _time.GetElapsedTime(allowance.ConfirmedAt, _time.GetTimestamp());
            if (elapsed < TimeSpan.FromMilliseconds(WorldControllerState.ReconnectAllowanceMilliseconds)) continue;
            state = CommitControllerEvent(state, new WorldControllerEvent(actor, WorldControllerEventKind.AllowanceExpired,
                assignment.Revision, allowance.EpisodeId, checked((ulong)(elapsed.Ticks / TimeSpan.TicksPerMillisecond))));
            _disconnectAllowances.Remove(actor);
        }
        return state;
    }

    /// <summary>
    /// A returning controller may finish already accepted work before becoming ready. This cannot
    /// select a new human choice or start exploration; the normal required-decision rules still hold.
    /// </summary>
    private bool CanContinueAcceptedControlReturn(WorldSnapshot state)
    {
        LocalPlayerConnection[] returning = _players.Values
            .Where(player => player.NetworkEstablished && !player.ControlAvailable).ToArray();
        if (returning.Length == 0) return false;
        return state.Care?.Pending is not null
            || state.EncounterLifecycle?.Phase is EncounterLifecyclePhase.Confrontation or EncounterLifecyclePhase.Staging
            || returning.Any(player => state.Pacing?.Encounter?.Decisions.Any(choice => choice.ActorId == player.ActorId) == true
                || WorldEncounterRetreats.HoldsActor(state, player.ActorId));
    }

    private WorldSnapshot RestorePlayerControlIfSafe(WorldSnapshot state, LocalPlayerConnection player)
    {
        if (state.Controllers?.Get(player.ActorId) is not { } assignment) return state;
        if (assignment.Mode != WorldControllerMode.Human)
        {
            if (!WorldControllerBoundaries.CanRestoreHuman(state, player.ActorId)) return state;
            state = CommitControllerEvent(state, new WorldControllerEvent(player.ActorId,
                WorldControllerEventKind.Reconnected, assignment.Revision, assignment.DisconnectId));
        }
        _disconnectAllowances.Remove(player.ActorId);
        // Publication precedes permission. A readiness request made while control was unavailable
        // is never retained and replayed here; the client must explicitly become ready again.
        PublishViewIfChanged(state);
        lock (_inputs)
        {
            player.ControlAvailable = true;
            player.Ready = false;
            player.ClearMovement();
        }
        return state;
    }

    private WorldSnapshot CommitControllerEvent(WorldSnapshot state, WorldControllerEvent controllerEvent)
    {
        var batch = new WorldStepBatch(NewCause(null), state.WorldId, state.BranchId, state.Revision,
            state.AdvancingSteps, WorldBatchKind.ControllerLifecycle, [], [], state.ProfileVersion, controllerEvent);
        WorldSnapshot committed = _authority.Commit(batch).Candidate;
        PublishViewIfChanged(committed);
        return committed;
    }

    private bool IsCurrentPlayer(LocalPlayerConnection player) => player.Connected
        && _players.TryGetValue(player.ActorId, out LocalPlayerConnection? current) && ReferenceEquals(current, player);

    private static void EnsureControlAvailable(LocalPlayerConnection player)
    {
        if (!player.ControlAvailable)
            throw new AuthorityDispatchException("This character is awaiting a safe return of player control.");
    }

    private sealed record DisconnectAllowance(EntityId EpisodeId, long ConfirmedAt);
}
