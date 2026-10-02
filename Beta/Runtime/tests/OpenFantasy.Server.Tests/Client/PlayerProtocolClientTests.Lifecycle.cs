using System.Net.WebSockets;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.Client;

namespace OpenFantasy.Server.Tests;

public sealed partial class PlayerProtocolClientTests
{
    [Fact]
    public async Task Production_connector_refuses_an_untrusted_certificate()
    {
        await using var world = await ServerWorld.Open();
        var invitation = new PlayerConnectionInvitation(world.Server.Endpoint, world.Admission.Compatibility,
            world.Actor, world.Credential.Export());
        await Assert.ThrowsAsync<WebSocketException>(() => PlayerWebSocketConnector.ConnectAsync(invitation, ServerWorld.Cancellation));
        Assert.False(world.Server.Completion.IsCompleted);
        Assert.Equal(0UL, world.Authority.ReadStateForHost().Revision);
        Assert.Equal(PlayerWebSocketServer.Subprotocol, PlayerWebSocketConnector.Subprotocol);
    }

    [Fact]
    public async Task Cancellation_during_startup_closes_owned_transport_without_waiting_for_a_silent_peer()
    {
        var pair = InMemoryPlayerTransport.CreatePair();
        await using var peer = pair.Server;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ServerWorld.Cancellation);
        var connecting = PlayerProtocolClient.ConnectAsync(pair.Client, PlayerInvitationTests.Invitation(), stop.Token);
        Assert.Equal(PlayerMessageKind.Hello, (await ServerWorld.Receive(peer)).Kind);
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connecting.WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation));
        Assert.Null(await peer.ReceiveAsync(ServerWorld.Cancellation).AsTask().WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation));
    }

    [Fact]
    public async Task Startup_that_returns_a_full_view_after_its_deadline_is_not_reported_as_success()
    {
        await using var fixture = await ProtocolClientFixture.Create();
        var clock = new StartupClock();
        fixture.Transport.Incoming = message =>
        {
            if (message.Kind == PlayerMessageKind.Observation) clock.Expire();
            return message;
        };
        await Assert.ThrowsAsync<TimeoutException>(() => fixture.Connect(time: clock)
            .WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation));
        Assert.Equal(0UL, fixture.World.Authority.ReadStateForHost().Revision);
    }

    private sealed class StartupClock : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref _timestamp);
        internal void Advance(TimeSpan elapsed) => Interlocked.Add(ref _timestamp, elapsed.Ticks);
        internal void Expire() => Advance(TimeSpan.FromSeconds(11));
    }
}
