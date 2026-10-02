using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Server.Tests;

public sealed class PlayerResponseTimeoutTests
{
    [Fact]
    public async Task A_successful_send_at_its_deadline_closes_only_its_player_and_the_listener_still_serves_tls()
    {
        await using var world = await ServerWorld.Open();
        var pair = InMemoryPlayerTransport.CreatePair();
        await using var client = pair.Client;
        var clock = new SendClock();
        var late = new LateSuccessfulSend(pair.Server, clock);
        LocalPlayerConnection player = await world.Host.ConnectPlayerAsync(world.Actor, world.Delivery);
        var channel = new AuthenticatedPlayerChannel(player, late, () => { });
        Task session = new PlayerCommandSession(channel, clock).RunAsync(ServerWorld.Cancellation);
        await ServerWorld.Send(client, new(PlayerMessageKind.Ping, 1, []));
        PlayerMessage pong = await ServerWorld.Receive(client);
        Assert.Equal((PlayerMessageKind.Pong, 1UL), (pong.Kind, pong.RequestId));
        var timeout = await Assert.ThrowsAsync<PlayerResponseTimeoutException>(() =>
            session.WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation));
        Assert.True(late.CompletedSuccessfully);
        Assert.True(late.DeadlineWasCancelled);
        Assert.Equal(PlayerCommandSession.SendLimit, clock.GetElapsedTime(0, clock.GetTimestamp()));
        Assert.Equal(LocalWorldRunState.Unavailable, player.ReadRunState());

        // This is the same failure-disposition boundary invoked by the real HTTP request's catch.
        world.Server.HandleConnectionFailure(timeout, cancelled: false);
        Assert.False(world.Server.Completion.IsCompleted);
        Assert.False(world.Admission.HostCompletion.IsCompleted);
        await using var subsequent = await world.Connect();
        await ServerWorld.Send(subsequent, new(PlayerMessageKind.Ping, 2, []));
        PlayerMessage reply = await ServerWorld.Receive(subsequent);
        Assert.Equal((PlayerMessageKind.Pong, 2UL), (reply.Kind, reply.RequestId));
        Assert.False(world.Server.Completion.IsCompleted);
        Assert.Equal(0UL, world.Authority.ReadStateForHost().Revision);
    }

    [Fact]
    public async Task A_generic_backend_timeout_remains_a_listener_failure()
    {
        await using var world = await ServerWorld.Open();
        await using var peer = await world.Connect();
        var backend = new TimeoutException("The backend operation timed out.");
        world.Server.HandleConnectionFailure(backend, cancelled: false);
        var failure = await Assert.ThrowsAsync<TimeoutException>(() =>
            world.Server.Completion.WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation));
        Assert.Same(backend, failure);
        Assert.False(world.Admission.HostCompletion.IsCompleted);
        await world.DisposeFailedServer<TimeoutException>();
    }

    /// <summary>Writes the actual Pong before advancing time. Returning successfully after cancellation models late completion.</summary>
    private sealed class LateSuccessfulSend(PlayerMessageTransport inner, SendClock clock)
        : PlayerMessageTransport(PlayerMessageCodec.MaximumMessageBytes)
    {
        internal bool CompletedSuccessfully { get; private set; }
        internal bool DeadlineWasCancelled { get; private set; }

        protected override async ValueTask SendMessageAsync(byte[] message, CancellationToken cancellationToken)
        {
            await inner.SendAsync(message, cancellationToken);
            clock.Advance(PlayerCommandSession.SendLimit);
            DeadlineWasCancelled = cancellationToken.IsCancellationRequested;
            CompletedSuccessfully = true;
        }

        protected override ValueTask<byte[]?> ReceiveMessageAsync(CancellationToken cancellationToken) =>
            inner.ReceiveAsync(cancellationToken);

        protected override void CloseTransport() => inner.DisposeAsync().GetAwaiter().GetResult();
    }

    /// <summary>Deterministic monotonic time, including the response cancellation timer; no five-second wall-clock sleep.</summary>
    private sealed class SendClock : TimeProvider
    {
        private readonly object _gate = new();
        private readonly List<ClockTimer> _timers = [];
        private long _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() { lock (_gate) { return _timestamp; } }
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ClockTimer(this, callback, state);
            lock (_gate)
            {
                _timers.Add(timer);
                timer.Change(dueTime, period);
            }
            return timer;
        }

        internal void Advance(TimeSpan elapsed)
        {
            ClockTimer[] due;
            lock (_gate)
            {
                _timestamp = checked(_timestamp + elapsed.Ticks);
                due = _timers.Where(timer => timer.Due is { } deadline && deadline <= _timestamp).ToArray();
                foreach (var timer in due)
                    timer.Due = timer.Period == Timeout.InfiniteTimeSpan ? null : checked(_timestamp + timer.Period.Ticks);
            }
            foreach (var timer in due) timer.Fire();
        }

        private sealed class ClockTimer(SendClock clock, TimerCallback callback, object? state) : ITimer
        {
            internal long? Due { get; set; }
            internal TimeSpan Period { get; private set; }
            private bool _disposed;
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (clock._gate)
                {
                    if (_disposed) return false;
                    Period = period;
                    Due = dueTime == Timeout.InfiniteTimeSpan ? null : checked(clock._timestamp + dueTime.Ticks);
                    return true;
                }
            }
            internal void Fire() { if (!_disposed) callback(state); }
            public void Dispose() { lock (clock._gate) { _disposed = true; Due = null; } }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
