namespace OpenFantasy.Hosting.Tests.Simulation;

/// <summary>Controlled monotonic host time; advancing it releases only already-due timer callbacks.</summary>
internal sealed class ManualSessionTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<ManualTimer> _timers = [];
    private long _timestamp;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() { lock (_gate) { return _timestamp; } }
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());
    internal bool HasPendingTimer { get { lock (_gate) { return _timers.Any(timer => timer.Due.HasValue); } } }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        lock (_gate)
        {
            _timers.Add(timer);
            timer.Change(dueTime, period);
        }
        return timer;
    }

    internal void Advance(TimeSpan elapsed)
    {
        List<ManualTimer> due;
        lock (_gate)
        {
            _timestamp = checked(_timestamp + elapsed.Ticks);
            due = _timers.Where(timer => timer.Due is { } deadline && deadline <= _timestamp).ToList();
            foreach (ManualTimer timer in due)
            {
                timer.Due = timer.Period == Timeout.InfiniteTimeSpan ? null : checked(_timestamp + timer.Period.Ticks);
            }
        }
        foreach (ManualTimer timer in due) { timer.Fire(); }
    }

    private sealed class ManualTimer(ManualSessionTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        internal long? Due { get; set; }
        internal TimeSpan Period { get; private set; }
        private bool _disposed;
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner._gate)
            {
                if (_disposed) { return false; }
                Due = dueTime == Timeout.InfiniteTimeSpan ? null : checked(owner._timestamp + dueTime.Ticks);
                Period = period;
                return true;
            }
        }
        internal void Fire() { if (!_disposed) { callback(state); } }
        public void Dispose() { lock (owner._gate) { _disposed = true; Due = null; } }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
