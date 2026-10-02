using System.Reflection;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;
using static OpenFantasy.Hosting.Tests.Networking.ControllerTransport.ControllerTransportFixture;

namespace OpenFantasy.Hosting.Tests.Networking.ControllerTransport;

public sealed class ControllerAdmissionRaceTests
{
    [Theory]
    [InlineData("revoke")]
    [InlineData("credential-expiry")]
    [InlineData("handshake-deadline")]
    public async Task A_welcome_queued_at_host_serialization_revalidates_before_establishing(string invalidation)
    {
        var clock = new BoundaryClock();
        await using var fixture = await Open(admissionTime: clock, credentialLifetime: TimeSpan.FromSeconds(1));
        // This test controls an existing serialization boundary without introducing a runtime test hook.
        var serialization = (SemaphoreSlim)typeof(LocalWorldSession)
            .GetField("_serialization", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Host)!;
        var validatedBeforeWait = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool held = false;
        Task attempt = fixture.Connect(inner => new ControlledControllerTransport(inner, send: async (bytes, token) =>
        {
            await serialization.WaitAsync(token);
            held = true;
            await inner.SendAsync(bytes, token);
            // The second timestamp read after Welcome is the credential check before Complete's await.
            clock.Arm(2, () => validatedBeforeWait.TrySetResult());
        }));
        try
        {
            await validatedBeforeWait.Task.WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
            if (invalidation == "revoke") fixture.Admission.Revoke(fixture.Actor);
            else clock.Advance(invalidation == "credential-expiry" ? TimeSpan.FromSeconds(1) : PlayerAdmissionService.HandshakeLimit);
        }
        finally { if (held) serialization.Release(); }
        if (invalidation == "handshake-deadline")
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Finish(attempt));
        else await Assert.ThrowsAsync<PlayerAdmissionException>(() => Finish(attempt));
        fixture.AssertUnchanged();
        Assert.False(fixture.Host.Completion.IsCompleted);
        await using LocalPlayerConnection replacement = await fixture.Host.ConnectPlayerAsync(fixture.Actor, fixture.Delivery);
        Assert.Equal(LocalWorldRunState.Paused, replacement.ReadRunState());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_abort_after_establishment_is_distinct_from_shutdown_during_the_same_boundary(bool shutdown)
    {
        var clock = new BoundaryClock();
        await using var fixture = await Open(admissionTime: clock);
        Task attempt = fixture.Connect(inner => new ControlledControllerTransport(inner, send: async (bytes, token) =>
        {
            await inner.SendAsync(bytes, token);
            // Two outer validation reads, two serialized validation reads, then the final elapsed check.
            clock.Arm(5, () =>
            {
                if (shutdown) fixture.Stop.Cancel();
                else fixture.RequestAborted.Cancel();
            });
        }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Finish(attempt));
        if (shutdown) fixture.AssertUnchanged();
        else await fixture.AssertDisconnected(WorldControllerDisconnectReason.TransportLost);
    }

    [Fact]
    public async Task Request_abort_before_establishment_is_still_a_failed_welcome_without_an_event()
    {
        await using var fixture = await Open();
        Task attempt = fixture.Connect(inner => new ControlledControllerTransport(inner, send: async (bytes, token) =>
        {
            await inner.SendAsync(bytes, token);
            fixture.RequestAborted.Cancel();
        }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Finish(attempt));
        fixture.AssertUnchanged();
        Assert.False(fixture.Host.Completion.IsCompleted);
    }

    private sealed class BoundaryClock : TimeProvider
    {
        private readonly ManualSessionTimeProvider _time = new();
        private Action? _boundary;
        private int _reads;
        internal void Arm(int reads, Action boundary) { _boundary = boundary; Volatile.Write(ref _reads, reads); }
        internal void Advance(TimeSpan elapsed) => _time.Advance(elapsed);
        public override long TimestampFrequency => _time.TimestampFrequency;
        public override long GetTimestamp()
        {
            long observed = _time.GetTimestamp();
            if (Volatile.Read(ref _reads) > 0 && Interlocked.Decrement(ref _reads) == 0) _boundary!();
            return observed;
        }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            _time.CreateTimer(callback, state, dueTime, period);
    }
}
