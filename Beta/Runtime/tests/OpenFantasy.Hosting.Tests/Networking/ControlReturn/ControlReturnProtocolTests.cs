using OpenFantasy.Core.Worlds;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.Client;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation.Controllers.Conservative;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.Networking.ControllerTransport;

namespace OpenFantasy.Hosting.Tests.Networking.ControlReturn;

public sealed class ControlReturnProtocolTests
{
    [Fact]
    public async Task Explicit_readiness_after_dropped_early_readiness_reaches_the_restored_host()
    {
        await using var fixture = await ConservativeControllerFixture.Open();
        var first = await fixture.Join();
        first.Channel.Player.SetReady(true);
        await fixture.ChooseWaits(first.Channel.Player);
        await fixture.LoseTransport(first);
        await using var connection = await Connect(fixture);
        Assert.False(connection.Port.ControlAvailable);
        PlayerControlStatus pending = connection.Client.ReadControlStatus();
        Assert.Equal(PlayerControlState.Pending, pending.State);
        Assert.Throws<InvalidOperationException>(() => connection.Client.Resume(pending));

        connection.Client.SetReady(true);
        await connection.Client.PingAsync(); // Ordered protocol barrier; early readiness must not be retained.
        Assert.False(connection.Port.Ready);
        await using var opponent = await fixture.Connect(fixture.Opponent);
        await fixture.ChooseWaits(opponent);
        await fixture.Until(() => fixture.Control.Mode == WorldControllerMode.Human);
        opponent.SetReady(false);
        Assert.True(connection.Port.ControlAvailable);
        Assert.False(connection.Port.Ready);

        await SessionFixture.Until(() => connection.Client.ReadControlStatus().State == PlayerControlState.Restored);
        PlayerControlStatus restored = connection.Client.ReadControlStatus();
        Assert.Equal(pending.PortId, restored.PortId);
        Assert.Equal(fixture.Actor, restored.ActorId);
        Assert.True(restored.Generation > pending.Generation);
        Assert.True(restored.ObservationRevision > 0);
        Assert.Throws<InvalidOperationException>(() => connection.Client.Resume(pending));
        Assert.Throws<InvalidOperationException>(() => connection.Client.Resume(restored with { PortId = new EntityId(8, 9) }));
        Assert.Throws<InvalidOperationException>(() => connection.Client.Resume(restored with { ObservationRevision = restored.ObservationRevision + 1 }));

        // Focus/startup readiness cannot substitute for this new deliberate choice.
        connection.Client.SetReady(true);
        await connection.Client.PingAsync();
        Assert.False(connection.Port.Ready);
        LocalWorldView earlierView = connection.Client.ReadView();
        var earlierWorld = fixture.State.ContentHash;
        opponent.SetReady(true);
        await fixture.Tick();
        opponent.SetReady(false);
        Assert.NotEqual(earlierWorld, fixture.State.ContentHash);
        await SessionFixture.Until(() => !ReferenceEquals(earlierView, connection.Client.ReadView()));
        Assert.Equal(restored, connection.Client.ReadControlStatus());
        Assert.False(connection.Port.Ready);
        connection.Client.Resume(restored);
        await connection.Client.PingAsync();
        Assert.True(connection.Port.Ready);
        await SessionFixture.Until(() => connection.Client.ReadControlStatus().State == PlayerControlState.Available);
        connection.Client.SetReady(false);
        await connection.Client.PingAsync();
        Assert.False(connection.Port.Ready);
        connection.Client.SetReady(true);
        await connection.Client.PingAsync();
        Assert.True(connection.Port.Ready); // Normal focus return retains the same admitted binding.
    }

    [Fact]
    public async Task Delayed_unbound_true_cannot_become_ready_after_safe_restoration()
    {
        await using var fixture = await ConservativeControllerFixture.Open();
        var first = await fixture.Join();
        first.Channel.Player.SetReady(true);
        await fixture.ChooseWaits(first.Channel.Player);
        await fixture.LoseTransport(first);
        var returning = await fixture.Join();
        using var stop = new CancellationTokenSource();
        Task run = new PlayerCommandSession(returning.Channel).RunAsync(stop.Token);
        try
        {
            Assert.Equal(PlayerControlState.Pending, returning.Channel.Player.ReadControlStatus().State);
            await ControllerTransportFixture.Send(returning.Client, new(PlayerMessageKind.Observation, 2, []));
            PlayerControlStatus pending = PlayerControlCodec.Decode((await ReceiveKind(PlayerMessageKind.ControlStatus)).Payload);
            Assert.Equal(PlayerControlState.Pending, pending.State);
            // Construct before restoration, retain the exact bytes, deliver only after the boundary.
            PlayerMessage early = PlayerGameplayCodec.Readiness(3, true);
            PlayerMessage earlyBound = PlayerGameplayCodec.Readiness(5, true, pending);
            await using var opponent = await fixture.Connect(fixture.Opponent);
            await fixture.ChooseWaits(opponent);
            await fixture.Until(() => fixture.Control.Mode == WorldControllerMode.Human);
            opponent.SetReady(false);
            var before = fixture.State.ContentHash;
            PlayerControlStatus restored = PlayerControlCodec.Decode((await ReceiveKind(PlayerMessageKind.ControlStatus)).Payload);
            Assert.Equal(PlayerControlState.Restored, restored.State);
            await Barrier(early);
            Assert.False(returning.Channel.Player.Ready);
            await Barrier(earlyBound);
            Assert.False(returning.Channel.Player.Ready);
            await Barrier(PlayerGameplayCodec.Readiness(7, true, restored with { PortId = new EntityId(99, 9) }));
            Assert.False(returning.Channel.Player.Ready);
            await Barrier(PlayerGameplayCodec.Readiness(9, true, restored with { ActorId = fixture.Opponent }));
            Assert.False(returning.Channel.Player.Ready);
            await Barrier(PlayerGameplayCodec.Readiness(11, true, restored with { ObservationStreamId = new EntityId(99, 10) }));
            Assert.False(returning.Channel.Player.Ready);
            await Barrier(PlayerGameplayCodec.Readiness(13, true, restored with { ObservationRevision = restored.ObservationRevision + 1 }));
            Assert.False(returning.Channel.Player.Ready);
            await Barrier(PlayerGameplayCodec.Readiness(15, true, restored with { State = PlayerControlState.Pending, Generation = 0 }));
            Assert.False(returning.Channel.Player.Ready);
            Assert.Equal(PlayerControlState.Restored, returning.Channel.Player.ReadControlStatus().State);
            Assert.Equal(before, fixture.State.ContentHash);

            // Another participant advances actual paid timing while this restored port remains unready.
            // Its new observation must not invalidate the original restoration boundary.
            PlayerControlStatus samePort = returning.Channel.Player.ReadControlStatus();
            opponent.SetReady(true);
            await fixture.Tick();
            opponent.SetReady(false);
            Assert.NotEqual(before, fixture.State.ContentHash);
            PlayerObservationFrame newer = PlayerObservationFrameCodec.Decode((await ReceiveKind(PlayerMessageKind.Observation)).Payload);
            Assert.Equal(restored.ObservationStreamId, newer.StreamId);
            Assert.True(newer.Revision > restored.ObservationRevision);
            Assert.Equal(samePort, returning.Channel.Player.ReadControlStatus());
            Assert.False(returning.Channel.Player.Ready);
            var advanced = fixture.State.ContentHash;

            await Barrier(PlayerGameplayCodec.Readiness(17, true, restored));
            Assert.True(returning.Channel.Player.Ready);
            await Barrier(PlayerGameplayCodec.Readiness(19, false));
            Assert.False(returning.Channel.Player.Ready);
            await Barrier(PlayerGameplayCodec.Readiness(21, true));
            Assert.False(returning.Channel.Player.Ready); // A valid Resume never reopens the legacy bypass.
            await Barrier(PlayerGameplayCodec.Readiness(23, true, restored));
            Assert.True(returning.Channel.Player.Ready);
            Assert.Equal(advanced, fixture.State.ContentHash);
        }
        finally
        {
            bool stoppedRunningSession = !run.IsCompleted;
            stop.Cancel();
            try { await ControllerTransportFixture.Finish(run); }
            catch (OperationCanceledException) when (stoppedRunningSession && stop.IsCancellationRequested) { }
        }

        async Task Barrier(PlayerMessage input)
        {
            await ControllerTransportFixture.Send(returning.Client, input);
            await ControllerTransportFixture.Send(returning.Client, new(PlayerMessageKind.Ping, input.RequestId + 1, []));
            Assert.Equal(input.RequestId + 1, (await ReceiveKind(PlayerMessageKind.Pong)).RequestId);
        }

        async Task<PlayerMessage> ReceiveKind(PlayerMessageKind kind)
        {
            for (int count = 0; count < 8; count++)
            {
                PlayerMessage message = await ControllerTransportFixture.Receive(returning.Client);
                if (message.Kind == kind) return message;
                Assert.True(message.Kind is PlayerMessageKind.Observation or PlayerMessageKind.ControlStatus);
            }
            throw new InvalidOperationException("Expected protocol response did not arrive within the bounded frame sequence.");
        }
    }

    private static async Task<Connection> Connect(ConservativeControllerFixture fixture)
    {
        var admission = new PlayerAdmissionService(fixture.Host, []);
        var credential = admission.Issue(fixture.Actor, fixture.Delivery(fixture.Actor), TimeSpan.FromHours(1));
        var pair = InMemoryPlayerTransport.CreatePair();
        var invitation = new PlayerConnectionInvitation(new Uri("wss://localhost/player"),
            admission.Compatibility, fixture.Actor, credential.Export());
        Task<AuthenticatedPlayerChannel> accept = admission.AcceptAsync(pair.Server);
        Task<PlayerProtocolClient> connect = PlayerProtocolClient.ConnectAsync(pair.Client, invitation);
        AuthenticatedPlayerChannel channel = await accept;
        var stop = new CancellationTokenSource();
        Task run = new PlayerCommandSession(channel).RunAsync(stop.Token);
        return new(await connect, channel.Player, run, stop);
    }

    private sealed class Connection(PlayerProtocolClient client, LocalPlayerConnection port, Task run,
        CancellationTokenSource stop) : IAsyncDisposable
    {
        internal PlayerProtocolClient Client { get; } = client;
        internal LocalPlayerConnection Port { get; } = port;
        public async ValueTask DisposeAsync()
        {
            stop.Cancel();
            await Client.DisposeAsync();
            try { await run.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            stop.Dispose();
        }
    }
}
