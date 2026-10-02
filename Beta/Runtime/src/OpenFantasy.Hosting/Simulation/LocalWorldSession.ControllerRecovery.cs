using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Simulation;

public sealed partial class LocalWorldSession
{
    private ulong? _controllerRecoveryGeneration;
    private long? _controllerStopTimestamp;

    /// <summary>Only a live host starts a fenced clock segment; replay and inspection do not.</summary>
    private void StartControllerRecovery()
    {
        if (!_authority.SupportsControllerRecovery) return;
        ControllerRecoverySegment segment = _authority.BeginControllerRecoverySegment();
        _controllerRecoveryGeneration = segment.Generation;
        long startedAt = _time.GetTimestamp();
        foreach (ControllerAllowanceProgress progress in segment.Allowances)
        {
            _disconnectAllowances.Add(progress.ActorId,
                new(progress.DisconnectId, startedAt, progress.ElapsedHostMilliseconds));
        }
    }

    /// <summary>A fixed recovered subtotal plus this segment avoids charging any interval twice.</summary>
    private long ControllerObservationTimestamp()
    {
        lock (_inputs) { return _controllerStopTimestamp ?? _time.GetTimestamp(); }
    }

    private ulong ObserveAllowanceMilliseconds(DisconnectAllowance allowance, long observedAt)
    {
        // A loss already being committed when shutdown was requested cannot charge negative time.
        TimeSpan elapsed = observedAt < allowance.ConfirmedAt
            ? TimeSpan.Zero : _time.GetElapsedTime(allowance.ConfirmedAt, observedAt);
        ulong milliseconds = checked((ulong)(elapsed.Ticks / TimeSpan.TicksPerMillisecond));
        if (_controllerRecoveryGeneration is null) return milliseconds;
        ulong remaining = WorldControllerState.ReconnectAllowanceMilliseconds - allowance.BaseElapsedMilliseconds;
        return allowance.BaseElapsedMilliseconds + Math.Min(milliseconds, remaining);
    }

    private void CheckpointControllerRecovery(WorldSnapshot state, long? observedTimestamp = null)
    {
        if (_controllerRecoveryGeneration is not { } generation) return;
        long observedAt = observedTimestamp ?? ControllerObservationTimestamp();
        var progress = state.Controllers!.Assignments
            .Where(assignment => assignment.Mode == WorldControllerMode.ReconnectAllowance)
            .Select(assignment =>
            {
                if (!_disconnectAllowances.TryGetValue(assignment.ActorId, out DisconnectAllowance? allowance)
                    || allowance.EpisodeId != assignment.DisconnectId)
                    throw new InvalidOperationException("A live controller allowance has no matching measured clock segment.");
                return new ControllerAllowanceProgress(assignment.ActorId, assignment.Revision,
                    allowance.EpisodeId, ObserveAllowanceMilliseconds(allowance, observedAt));
            }).ToArray();
        _authority.CheckpointControllerAllowances(generation, progress);
    }
}
