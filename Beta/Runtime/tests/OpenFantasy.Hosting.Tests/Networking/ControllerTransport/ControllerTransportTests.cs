using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;
using static OpenFantasy.Hosting.Tests.Networking.ControllerTransport.ControllerTransportFixture;

namespace OpenFantasy.Hosting.Tests.Networking.ControllerTransport;

public sealed class ControllerTransportTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Established_eof_and_goodbye_record_peer_closed_without_gameplay_time(bool goodbye)
    {
        await using var fixture = await Open();
        var (client, channel) = await fixture.Connect();
        Task run = fixture.Run(channel);
        if (goodbye) await Send(client, new(PlayerMessageKind.Goodbye, 1, []));
        else await client.DisposeAsync();
        await Finish(run);
        Assert.Equal(LocalWorldRunState.Unavailable, channel.Player.ReadRunState());
        await fixture.AssertDisconnected(WorldControllerDisconnectReason.PeerClosed);
    }

    [Theory]
    [InlineData("envelope")]
    [InlineData("payload")]
    [InlineData("query")]
    [InlineData("frame")]
    public async Task Malformed_established_input_records_protocol_failure_at_the_parse_boundary(string fault)
    {
        await using var fixture = await Open();
        int receives = 0;
        var (client, channel) = await fixture.Connect(inner => new ControlledControllerTransport(inner,
            receive: token => fault == "frame" && Interlocked.Increment(ref receives) > 1
                ? ValueTask.FromException<byte[]?>(new InvalidDataException("Malformed transport frame.")) : inner.ReceiveAsync(token)));
        Task run = fixture.Run(channel);
        if (fault == "envelope") await client.SendAsync(new byte[] { 1, 2, 3 }, Cancellation);
        else if (fault == "payload") await Send(client, new(PlayerMessageKind.Readiness, 1, new byte[] { 2 }));
        else if (fault == "query") await Send(client, new(PlayerMessageKind.Query, 1, new byte[] { 1, 255 }));
        if (fault == "frame") await Assert.ThrowsAsync<InvalidDataException>(() => Finish(run));
        else await Assert.ThrowsAsync<FormatException>(() => Finish(run));
        await fixture.AssertDisconnected(WorldControllerDisconnectReason.ProtocolFailure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_send_or_receive_io_failure_records_transport_lost(bool reading)
    {
        await using var fixture = await Open();
        int sends = 0, receives = 0;
        var (client, channel) = await fixture.Connect(inner => new ControlledControllerTransport(inner,
            send: (bytes, token) => !reading && Interlocked.Increment(ref sends) > 1
                ? ValueTask.FromException(new IOException("Injected response write loss.")) : inner.SendAsync(bytes, token),
            receive: token => reading && Interlocked.Increment(ref receives) > 1
                ? ValueTask.FromException<byte[]?>(new IOException("Injected receive loss.")) : inner.ReceiveAsync(token)));
        Task run = fixture.Run(channel);
        if (!reading) await Send(client, new(PlayerMessageKind.Ping, 1, []));
        await Assert.ThrowsAsync<IOException>(() => Finish(run));
        await fixture.AssertDisconnected(WorldControllerDisconnectReason.TransportLost);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_response_deadline_records_transport_lost_even_when_the_send_returns_success(bool lateSuccess)
    {
        await using var fixture = await Open();
        var sending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int sends = 0;
        var (client, channel) = await fixture.Connect(inner => new ControlledControllerTransport(inner, send: async (bytes, token) =>
        {
            if (Interlocked.Increment(ref sends) == 1) { await inner.SendAsync(bytes, token); return; }
            if (lateSuccess)
            {
                await inner.SendAsync(bytes, token);
                fixture.NetworkTime.Advance(PlayerCommandSession.SendLimit);
            }
            else
            {
                sending.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
        }));
        Task run = fixture.Run(channel);
        await Send(client, new(PlayerMessageKind.Ping, 1, []));
        if (lateSuccess) await Assert.ThrowsAsync<PlayerResponseTimeoutException>(() => Finish(run));
        else
        {
            await sending.Task.WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
            fixture.NetworkTime.Advance(PlayerCommandSession.SendLimit);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Finish(run));
        }
        await fixture.AssertDisconnected(WorldControllerDisconnectReason.TransportLost);
    }

    [Fact]
    public async Task Request_aborted_is_a_transport_loss_independent_of_caller_shutdown()
    {
        await using var fixture = await Open();
        var (_, channel) = await fixture.Connect();
        Task run = fixture.Run(channel);
        fixture.RequestAborted.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Finish(run));
        await fixture.AssertDisconnected(WorldControllerDisconnectReason.TransportLost);
    }

    [Fact]
    public async Task Adapter_protocol_abort_is_recorded_even_when_its_sender_finishes_before_the_receiver()
    {
        await using var fixture = await Open();
        var sending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var trigger = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseReceiver = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ControlledControllerTransport? server = null;
        int sends = 0, receives = 0;
        var (client, channel) = await fixture.Connect(inner => server = new ControlledControllerTransport(inner,
            send: async (bytes, token) =>
            {
                if (Interlocked.Increment(ref sends) == 1) { await inner.SendAsync(bytes, token); return; }
                sending.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            },
            receive: async token =>
            {
                if (Interlocked.Increment(ref receives) <= 2) return await inner.ReceiveAsync(token);
                await trigger.Task.WaitAsync(token);
                await server!.AbortTransportAsync(PlayerTransportTermination.ProtocolFailure);
                await releaseReceiver.Task;
                throw new InvalidDataException("The receiver reports its frame error after aborting its sender.");
            }));
        Task run = fixture.Run(channel);
        try
        {
            await Send(client, new(PlayerMessageKind.Ping, 1, []));
            await sending.Task.WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
            trigger.TrySetResult();
            await SessionFixture.Until(() => fixture.Authority.ReadStateForHost().Revision == 1);
        }
        finally { releaseReceiver.TrySetResult(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Finish(run));
        await fixture.AssertDisconnected(WorldControllerDisconnectReason.ProtocolFailure);
    }

    [Theory]
    [InlineData("caller")]
    [InlineData("channel")]
    [InlineData("transport")]
    [InlineData("host")]
    public async Task Local_shutdown_and_disposal_preserve_controller_history(string close)
    {
        await using var fixture = await Open();
        var (_, channel) = await fixture.Connect();
        Task run = fixture.Run(channel);
        if (close == "caller") fixture.Stop.Cancel();
        else if (close == "channel") await channel.DisposeAsync();
        else if (close == "transport") await channel.Transport.DisposeAsync();
        else await fixture.Host.StopAsync();
        try { await Finish(run); }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        if (close != "host") fixture.AssertUnchanged();
        await fixture.AssertUnchangedAfterStop();
    }

    [Fact]
    public async Task A_send_backend_format_exception_is_not_mislabeled_as_client_protocol_failure()
    {
        await using var fixture = await Open();
        int sends = 0;
        var (client, channel) = await fixture.Connect(inner => new ControlledControllerTransport(inner, send: (bytes, token) =>
            Interlocked.Increment(ref sends) == 1 ? inner.SendAsync(bytes, token)
                : ValueTask.FromException(new FormatException("Backend encoder failure."))));
        Task run = fixture.Run(channel);
        await Send(client, new(PlayerMessageKind.Ping, 1, []));
        await Assert.ThrowsAsync<FormatException>(() => Finish(run));
        fixture.AssertUnchanged();
    }

    [Fact]
    public async Task Authority_query_refusal_keeps_the_established_channel_and_history_unchanged()
    {
        await using var fixture = await Open();
        var (client, channel) = await fixture.Connect();
        Task run = fixture.Run(channel);
        await Send(client, new(PlayerMessageKind.Query, 1,
            PlayerQueryCodec.Encode(new(PlayerQueryKind.ReadPresentationBootstrap))));
        Assert.Equal(PlayerRequestRefusal.NotAdmitted, PlayerGameplayCodec.ReadRefusal(await Receive(client)));
        Assert.False(run.IsCompleted);
        fixture.AssertUnchanged();
        fixture.Stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Finish(run));
        fixture.AssertUnchanged();
    }

    [Fact]
    public async Task A_real_rejoin_restores_only_after_welcome_and_stale_closure_cannot_remove_the_replacement()
    {
        await using var fixture = await Open();
        var (firstClient, first) = await fixture.Connect();
        Task firstRun = fixture.Run(first);
        await firstClient.DisposeAsync();
        await Finish(firstRun);
        Assert.Equal(WorldControllerMode.ReconnectAllowance, fixture.Authority.ReadStateForHost().Controllers!.Get(fixture.Actor).Mode);
        var (secondClient, second) = await fixture.Connect();
        Task secondRun = fixture.Run(second);
        Assert.Equal(WorldControllerMode.Human, fixture.Authority.ReadStateForHost().Controllers!.Get(fixture.Actor).Mode);
        Assert.Equal(2UL, fixture.Authority.ReadStateForHost().Revision);
        Assert.False(second.Player.Ready);
        await first.Player.ConfirmTransportDisconnectedAsync(WorldControllerDisconnectReason.TransportLost);
        await first.ConfirmTransportDisconnectedAsync(WorldControllerDisconnectReason.ProtocolFailure);
        await Send(secondClient, new(PlayerMessageKind.Ping, 1, []));
        Assert.Equal(PlayerMessageKind.Pong, (await Receive(secondClient)).Kind);
        Assert.Equal(LocalWorldRunState.Paused, second.Player.ReadRunState());
        Assert.Equal(2UL, fixture.Authority.ReadStateForHost().Revision);
        fixture.Stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Finish(secondRun));
        Assert.Equal(2UL, fixture.Authority.ReadStateForHost().Revision);
    }

    [Theory]
    [InlineData("write")]
    [InlineData("deadline")]
    [InlineData("revoked")]
    public async Task Failed_welcome_never_establishes_or_disconnects_control(string failure)
    {
        await using var fixture = await Open();
        async Task Attempt() => await fixture.Connect(inner => new ControlledControllerTransport(inner, send: async (bytes, token) =>
        {
            if (failure == "write") throw new IOException("Welcome write failed.");
            await inner.SendAsync(bytes, token);
            if (failure == "deadline") fixture.NetworkTime.Advance(PlayerAdmissionService.HandshakeLimit);
            else fixture.Admission.Revoke(fixture.Actor);
        }));
        if (failure == "write") await Assert.ThrowsAsync<IOException>(Attempt);
        else if (failure == "deadline") await Assert.ThrowsAnyAsync<OperationCanceledException>(Attempt);
        else await Assert.ThrowsAsync<PlayerAdmissionException>(Attempt);
        fixture.AssertUnchanged();
        await using LocalPlayerConnection replacement = await fixture.Host.ConnectPlayerAsync(fixture.Actor, fixture.Delivery);
        Assert.Equal(LocalWorldRunState.Paused, replacement.ReadRunState());
    }

    [Fact]
    public async Task Legacy_profile35_peer_close_retains_its_original_bytes()
    {
        await using var fixture = await Open(legacy: true);
        var (client, channel) = await fixture.Connect();
        Task run = fixture.Run(channel);
        await client.DisposeAsync();
        await Finish(run);
        fixture.AssertUnchanged();
        Assert.Equal((ushort)35, fixture.Genesis.ProfileVersion);
        Assert.Null(fixture.Authority.ReadStateForHost().Controllers);
    }

    [Fact]
    public async Task A_completed_admission_at_the_absolute_deadline_is_an_established_transport_loss()
    {
        var clock = new AdmissionDeadlineClock();
        await using var fixture = await Open(admissionTime: clock);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Connect(inner =>
            new ControlledControllerTransport(inner, send: async (bytes, token) =>
            {
                await inner.SendAsync(bytes, token);
                clock.ArmAfterWelcome();
            })));
        await fixture.AssertDisconnected(WorldControllerDisconnectReason.TransportLost);
    }

    /// <summary>Only the post-establishment elapsed check reaches the limit; both final credential checks remain in time.</summary>
    private sealed class AdmissionDeadlineClock : TimeProvider
    {
        private readonly ManualSessionTimeProvider _time = new();
        private int _reads;
        internal void ArmAfterWelcome() => _reads = 5;
        public override long TimestampFrequency => _time.TimestampFrequency;
        public override long GetTimestamp()
        {
            if (_reads > 0 && --_reads == 0) _time.Advance(PlayerAdmissionService.HandshakeLimit);
            return _time.GetTimestamp();
        }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            _time.CreateTimer(callback, state, dueTime, period);
    }
}
