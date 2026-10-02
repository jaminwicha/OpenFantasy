using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.Simulation.SharedPlayers;
using OpenFantasy.Hosting.Worlds;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;
using static OpenFantasy.Hosting.Tests.WorldStorage.WorldServiceFixture;

namespace OpenFantasy.Hosting.Tests.Networking.PlayerCommandRouting;

public sealed class PlayerCommandRoutingTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Pending_action_keeps_readiness_responsive_and_returns_only_the_exact_durable_receipt()
    {
        await using var fixture = await RoutingFixture.Open();
        WorldSnapshot genesis = fixture.Authority.ReadStateForHost();
        await fixture.Send(PlayerGameplayCodec.Readiness(1, true));
        WorldActionCommand command = Give(genesis, 400, 1);
        await fixture.Send(PlayerGameplayCodec.Command(2, command));
        await Until(() => fixture.Channel.Player.Pending is not null);
        await fixture.Send(PlayerGameplayCodec.Readiness(3, false));
        await fixture.Ping(4);
        Assert.Equal(LocalWorldRunState.Paused, fixture.Channel.Player.ReadRunState());
        await Tick(fixture.HostTime);
        Assert.Equal(genesis.ContentHash, fixture.Authority.ReadStateForHost().ContentHash);
        await fixture.Send(PlayerGameplayCodec.Readiness(5, true));
        await fixture.Ping(6);
        await Tick(fixture.HostTime);
        PlayerMessage response = await fixture.Receive();
        Assert.Equal(PlayerMessageKind.Receipt, response.Kind);
        Assert.Equal(2UL, response.RequestId);
        WorldActionReceipt receipt = WorldReceiptCodecV1.Decode(response.Payload);
        Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
        Assert.Equal(command.ContentHash, receipt.CommandHash);
        Assert.Equal(WorldReceiptCodecV1.Encode(fixture.Authority.FindReceipt(command, Delivery)!), response.Payload.ToArray());
        Assert.Equal(5UL, fixture.Authority.ReadStateForHost().Inventory.Custody.GetLot(Id(70)).Quantity);

        // An exact retry is valid even while unready and must not spend another item or interval.
        await fixture.Send(PlayerGameplayCodec.Readiness(7, false));
        await fixture.Send(PlayerGameplayCodec.Command(8, command));
        PlayerMessage retry = await fixture.Receive();
        Assert.Equal(8UL, retry.RequestId);
        Assert.Equal(response.Payload.ToArray(), retry.Payload.ToArray());
        Assert.Equal(1UL, fixture.Authority.ReadStateForHost().Revision);
        WorldActionCommand changed = Give(genesis, 400, 2);
        await fixture.Send(PlayerGameplayCodec.Command(9, changed));
        Assert.Equal(PlayerRequestRefusal.NotAdmitted, PlayerGameplayCodec.ReadRefusal(await fixture.Receive()));
        Assert.Equal(1UL, fixture.Authority.ReadStateForHost().Revision);
    }

    [Fact]
    public async Task Pending_duplicate_is_bounded_and_preflight_refusal_is_distinct_from_durable_rejection()
    {
        await using var fixture = await RoutingFixture.Open();
        WorldSnapshot genesis = fixture.Authority.ReadStateForHost();
        await fixture.Send(PlayerGameplayCodec.Command(1, Give(genesis, 401, 1)));
        Assert.Equal(PlayerRequestRefusal.NotAdmitted, PlayerGameplayCodec.ReadRefusal(await fixture.Receive()));
        Assert.Equal(0UL, fixture.Authority.ReadStateForHost().Revision);
        await fixture.Send(PlayerGameplayCodec.Readiness(2, true));
        WorldActionCommand original = Give(genesis, 402, 1);
        var stale = new WorldActionCommand(original.CommandId, original.PrincipalId, original.OriginSessionId,
            original.ActorId, original.WorldId, original.BranchId, 99, original.Intent);
        await fixture.Send(PlayerGameplayCodec.Command(3, stale));
        await Until(() => fixture.Channel.Player.Pending is not null);
        await fixture.Send(PlayerGameplayCodec.Command(4, stale));
        PlayerMessage busy = await fixture.Receive();
        Assert.Equal(4UL, busy.RequestId);
        Assert.Equal(PlayerRequestRefusal.Busy, PlayerGameplayCodec.ReadRefusal(busy));
        await Tick(fixture.HostTime);
        PlayerMessage response = await fixture.Receive();
        Assert.Equal(PlayerMessageKind.Receipt, response.Kind);
        WorldActionReceipt rejected = WorldReceiptCodecV1.Decode(response.Payload);
        Assert.Equal(CommandReceiptStatus.Rejected, rejected.Status);
        Assert.Equal(WorldActionRejection.RevisionConflict, rejected.Rejection);
        Assert.Equal(WorldReceiptCodecV1.Encode(fixture.Authority.FindReceipt(stale, Delivery)!), response.Payload.ToArray());
        Assert.Equal(6UL, fixture.Authority.ReadStateForHost().Inventory.Custody.GetLot(Id(70)).Quantity);
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("principal")]
    [InlineData("session")]
    [InlineData("world")]
    [InlineData("branch")]
    public async Task Forged_command_claims_cannot_replace_the_credential_selected_binding(string field)
    {
        await using var fixture = await RoutingFixture.Open();
        WorldSnapshot state = fixture.Authority.ReadStateForHost();
        WorldActionCommand actual = Give(state, 403, 1);
        var forged = new WorldActionCommand(actual.CommandId,
            field == "principal" ? Id(101) : actual.PrincipalId,
            field == "session" ? Id(111) : actual.OriginSessionId,
            field == "actor" ? Id(11) : actual.ActorId,
            field == "world" ? Id(999) : actual.WorldId,
            field == "branch" ? Id(999) : actual.BranchId, actual.ExpectedActorRevision, actual.Intent);
        await fixture.Send(PlayerGameplayCodec.Readiness(1, true));
        await fixture.Send(PlayerGameplayCodec.Command(2, forged));
        Assert.Equal(PlayerRequestRefusal.NotAdmitted, PlayerGameplayCodec.ReadRefusal(await fixture.Receive()));
        Assert.Equal(state.ContentHash, fixture.Authority.ReadStateForHost().ContentHash);
    }

    [Fact]
    public async Task Movement_uses_the_existing_lease_and_closing_one_channel_does_not_stop_its_peer()
    {
        await using var fixture = await RoutingFixture.Open();
        await using LocalPlayerConnection peer = await fixture.Host.ConnectPlayerAsync(Id(11), SharedPlayerSessionTests.OtherDelivery);
        peer.SetReady(true);
        peer.SetMovement(0, 32);
        await fixture.Send(PlayerGameplayCodec.Readiness(1, true));
        await fixture.Send(PlayerGameplayCodec.Movement(2, 32, 0));
        await fixture.Ping(3);
        await Tick(fixture.HostTime);
        WorldSnapshot moved = fixture.Authority.ReadStateForHost();
        Assert.Equal(32, moved.Space.GetActor(Id(10)).Feet.X);
        Assert.Equal(32, moved.Space.GetActor(Id(11)).Feet.Y);
        Assert.Equal(1UL, moved.AdvancingSteps);
        await fixture.Send(new PlayerMessage(PlayerMessageKind.Goodbye, 4, []));
        await fixture.Completion.WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
        peer.SetMovement(0, 32);
        await Tick(fixture.HostTime);
        Assert.Equal(32, fixture.Authority.ReadStateForHost().Space.GetActor(Id(10)).Feet.X);
        Assert.Equal(64, fixture.Authority.ReadStateForHost().Space.GetActor(Id(11)).Feet.Y);
        Assert.False(fixture.Host.Completion.IsCompleted);
    }

    [Theory]
    [InlineData("readiness")]
    [InlineData("movement")]
    [InlineData("request")]
    [InlineData("kind")]
    [InlineData("oversized-command")]
    public async Task Malformed_or_repeated_input_closes_only_its_own_port_without_a_world_receipt(string fault)
    {
        await using var fixture = await RoutingFixture.Open();
        await fixture.Ping(1);
        PlayerMessage message = fault switch
        {
            "readiness" => new(PlayerMessageKind.Readiness, 2, new byte[] { 2 }),
            "movement" => new(PlayerMessageKind.Movement, 2, new byte[] { 0, 0, 0, 32, 0, 0, 0, 32 }),
            "request" => new(PlayerMessageKind.Ping, 1, []),
            "oversized-command" => new(PlayerMessageKind.Command, 2, new byte[WorldActionCodec.MaximumCommandByteLength + 1]),
            _ => new(PlayerMessageKind.Receipt, 2, [])
        };
        await fixture.Send(message);
        await Assert.ThrowsAsync<FormatException>(() => fixture.Completion.WaitAsync(TimeSpan.FromSeconds(5), Cancellation));
        Assert.Equal(0UL, fixture.Authority.ReadStateForHost().Revision);
        Assert.Equal(LocalWorldRunState.Unavailable, fixture.Channel.Player.ReadRunState());
        await using LocalPlayerConnection reconnect = await fixture.Host.ConnectPlayerAsync(Id(10), Delivery);
    }

    [Fact]
    public async Task A_slow_response_cannot_block_other_players_and_has_an_absolute_send_deadline()
    {
        await using var fixture = await RoutingFixture.Open(blockResponses: true);
        await using LocalPlayerConnection peer = await fixture.Host.ConnectPlayerAsync(Id(11), SharedPlayerSessionTests.OtherDelivery);
        peer.SetReady(true);
        peer.SetMovement(0, 32);
        await fixture.Send(new PlayerMessage(PlayerMessageKind.Ping, 1, []));
        await fixture.Gate!.ResponseStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
        await Tick(fixture.HostTime);
        Assert.Equal(32, fixture.Authority.ReadStateForHost().Space.GetActor(Id(11)).Feet.Y);
        fixture.NetworkTime.Advance(PlayerCommandSession.SendLimit);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Completion.WaitAsync(TimeSpan.FromSeconds(5), Cancellation));
        Assert.Equal(LocalWorldRunState.Unavailable, fixture.Channel.Player.ReadRunState());
        Assert.Equal(LocalWorldRunState.Running, peer.ReadRunState());
    }

    [Fact]
    public async Task Lost_committed_response_is_recovered_once_after_actual_host_reopening()
    {
        await using var fixture = await RoutingFixture.Open(blockResponses: true);
        WorldActionCommand command = Give(fixture.Authority.ReadStateForHost(), 404, 1);
        await fixture.Send(PlayerGameplayCodec.Readiness(1, true));
        await fixture.Send(PlayerGameplayCodec.Command(2, command));
        await Until(() => fixture.Channel.Player.Pending is not null);
        await Tick(fixture.HostTime);
        await fixture.Gate!.ResponseStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
        byte[] committed = WorldReceiptCodecV1.Encode(fixture.Authority.FindReceipt(command, Delivery)!);
        fixture.Stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Completion.WaitAsync(TimeSpan.FromSeconds(5), Cancellation));
        await fixture.Host.StopAsync();

        var reopenedAuthority = SqliteWorldAuthority.OpenExisting(fixture.DatabasePath);
        var time = new ManualSessionTimeProvider();
        await using var reopenedHost = new LocalWorldSession(reopenedAuthority, Id(10), Delivery, time);
        await reopenedHost.Player.DisposeAsync();
        var service = new PlayerAdmissionService(reopenedHost, []);
        var credential = service.Issue(Id(10), Delivery, TimeSpan.FromMinutes(1));
        var pair = InMemoryPlayerTransport.CreatePair();
        await using var client = pair.Client;
        await client.SendAsync(PlayerMessageCodec.Encode(PlayerHandshakeCodec.Hello(1, service.Compatibility, credential.Export())), Cancellation);
        await using var channel = await service.AcceptAsync(pair.Server, Cancellation);
        await client.ReceiveAsync(Cancellation);
        Task run = new PlayerCommandSession(channel).RunAsync(Cancellation);
        await client.SendAsync(PlayerMessageCodec.Encode(PlayerGameplayCodec.Command(1, command)), Cancellation);
        PlayerMessage replay = PlayerMessageCodec.Decode((await client.ReceiveAsync(Cancellation))!);
        Assert.Equal(PlayerMessageKind.Receipt, replay.Kind);
        Assert.Equal(committed, replay.Payload.ToArray());
        Assert.Equal(1UL, reopenedAuthority.ReadStateForHost().Revision);
        Assert.Equal(5UL, reopenedAuthority.ReadStateForHost().Inventory.Custody.GetLot(Id(70)).Quantity);
        Assert.True((await reopenedHost.VerifyRecordedHistoryAsync()).ReproducesHead);
        await client.SendAsync(PlayerMessageCodec.Encode(new PlayerMessage(PlayerMessageKind.Goodbye, 2, [])), Cancellation);
        await run.WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
    }

    [Fact]
    public async Task Host_shutdown_releases_an_idle_receiver_and_does_not_leave_a_background_task()
    {
        await using var fixture = await RoutingFixture.Open();
        await fixture.Host.StopAsync();
        await fixture.Completion.WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
        Assert.Equal(LocalWorldRunState.Unavailable, fixture.Channel.Player.ReadRunState());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Closing_with_a_paused_pending_action_joins_its_waiter_without_committing_it(bool stopHost)
    {
        await using var fixture = await RoutingFixture.Open();
        await using LocalPlayerConnection peer = await fixture.Host.ConnectPlayerAsync(Id(11), SharedPlayerSessionTests.OtherDelivery);
        WorldActionCommand command = Give(fixture.Authority.ReadStateForHost(), 406, 1);
        await fixture.Send(PlayerGameplayCodec.Readiness(1, true));
        await fixture.Send(PlayerGameplayCodec.Command(2, command));
        await Until(() => fixture.Channel.Player.Pending is not null);
        await fixture.Send(PlayerGameplayCodec.Readiness(3, false));
        await fixture.Ping(4);
        if (stopHost) await fixture.Host.StopAsync();
        else await fixture.Send(new PlayerMessage(PlayerMessageKind.Goodbye, 5, []));
        await fixture.Completion.WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
        Assert.Null(fixture.Channel.Player.Pending);
        Assert.Equal(LocalWorldRunState.Unavailable, fixture.Channel.Player.ReadRunState());
        if (stopHost)
        {
            using var reopened = SqliteWorldAuthority.OpenExisting(fixture.DatabasePath);
            Assert.Equal(0UL, reopened.ReadStateForHost().Revision);
            Assert.Null(reopened.FindReceipt(command, Delivery));
        }
        else
        {
            Assert.Equal(0UL, fixture.Authority.ReadStateForHost().Revision);
            Assert.Null(fixture.Authority.FindReceipt(command, Delivery));
            peer.SetReady(true);
            peer.SetMovement(0, 32);
            await Tick(fixture.HostTime);
            Assert.Equal(32, fixture.Authority.ReadStateForHost().Space.GetActor(Id(11)).Feet.Y);
            Assert.Equal(6UL, fixture.Authority.ReadStateForHost().Inventory.Custody.GetLot(Id(70)).Quantity);
        }
    }

    [Fact]
    public async Task Exhausted_response_capacity_closes_the_connection_instead_of_creating_waiting_producers()
    {
        await using var fixture = await RoutingFixture.Open(blockResponses: true);
        await fixture.Send(new PlayerMessage(PlayerMessageKind.Ping, 1, []));
        await fixture.Gate!.ResponseStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
        for (ulong request = 2; request <= PlayerCommandSession.MaximumQueuedResponses + 2UL; request++)
            await fixture.Send(new PlayerMessage(PlayerMessageKind.Ping, request, []));
        IOException error = await Assert.ThrowsAsync<IOException>(() => fixture.Completion.WaitAsync(TimeSpan.FromSeconds(5), Cancellation));
        Assert.Contains("queue is full", error.Message, StringComparison.Ordinal);
        Assert.Equal(0UL, fixture.Authority.ReadStateForHost().Revision);
        Assert.Equal(LocalWorldRunState.Unavailable, fixture.Channel.Player.ReadRunState());
    }

    [Fact]
    public async Task No_response_inputs_also_have_a_bounded_work_rate()
    {
        await using var fixture = await RoutingFixture.Open();
        for (ulong request = 1; request <= PlayerCommandSession.MaximumInputsPerSecond + 1UL; request++)
            await fixture.Send(PlayerGameplayCodec.Readiness(request, false));
        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Completion.WaitAsync(TimeSpan.FromSeconds(5), Cancellation));
        Assert.Contains("input-rate limit", error.Message, StringComparison.Ordinal);
        Assert.Equal(0UL, fixture.Authority.ReadStateForHost().Revision);
    }

    [Fact]
    public async Task Actual_loopback_websocket_action_returns_the_database_receipt_and_shuts_down_cleanly()
    {
        await using var fixture = await RoutingFixture.Open(sockets: true);
        WorldActionCommand command = Give(fixture.Authority.ReadStateForHost(), 405, 1);
        await fixture.Send(PlayerGameplayCodec.Readiness(1, true));
        await fixture.Send(PlayerGameplayCodec.Command(2, command));
        await Until(() => fixture.Channel.Player.Pending is not null);
        await Tick(fixture.HostTime);
        PlayerMessage response = await fixture.Receive();
        Assert.Equal(PlayerMessageKind.Receipt, response.Kind);
        Assert.Equal(WorldReceiptCodecV1.Encode(fixture.Authority.FindReceipt(command, Delivery)!), response.Payload.ToArray());
        await fixture.Send(new PlayerMessage(PlayerMessageKind.Goodbye, 3, []));
        await fixture.Completion.WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
    }

    private sealed class RoutingFixture : IAsyncDisposable
    {
        private readonly SessionFiles _files = new();
        private readonly List<TcpClient> _sockets = [];
        private RoutingFixture()
        {
            Authority = SqliteWorldAuthority.CreateNew(_files.Path, SharedPlayerSessionTests.TwoPlayers());
            Host = new LocalWorldSession(Authority, Id(10), Delivery, HostTime);
        }
        internal SqliteWorldAuthority Authority { get; }
        internal LocalWorldSession Host { get; }
        internal string DatabasePath => _files.Path;
        internal ManualSessionTimeProvider HostTime { get; } = new();
        internal ManualSessionTimeProvider NetworkTime { get; } = new();
        internal CancellationTokenSource Stop { get; } = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        internal PlayerMessageTransport Client { get; private set; } = null!;
        internal AuthenticatedPlayerChannel Channel { get; private set; } = null!;
        internal Task Completion { get; private set; } = null!;
        internal ResponseGateTransport? Gate { get; private set; }

        internal static async Task<RoutingFixture> Open(bool blockResponses = false, bool sockets = false)
        {
            var fixture = new RoutingFixture();
            await fixture.Host.Player.DisposeAsync();
            var service = new PlayerAdmissionService(fixture.Host, []);
            var credential = service.Issue(Id(10), Delivery, TimeSpan.FromMinutes(1));
            PlayerMessageTransport server;
            if (sockets)
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
                deadline.CancelAfter(TimeSpan.FromSeconds(5));
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                try
                {
                    var client = new TcpClient();
                    fixture._sockets.Add(client);
                    ValueTask connecting = client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port, deadline.Token);
                    TcpClient accepted = await listener.AcceptTcpClientAsync(deadline.Token);
                    fixture._sockets.Add(accepted);
                    await connecting;
                    fixture.Client = new WebSocketPlayerTransport(WebSocket.CreateFromStream(client.GetStream(), false, null, TimeSpan.FromSeconds(30)));
                    server = new WebSocketPlayerTransport(WebSocket.CreateFromStream(accepted.GetStream(), true, null, TimeSpan.FromSeconds(30)));
                }
                finally { listener.Stop(); }
            }
            else
            {
                var pair = InMemoryPlayerTransport.CreatePair();
                fixture.Client = pair.Client;
                server = pair.Server;
            }
            if (blockResponses) server = fixture.Gate = new ResponseGateTransport(server);
            await fixture.Send(PlayerHandshakeCodec.Hello(1, service.Compatibility, credential.Export()));
            fixture.Channel = await service.AcceptAsync(server, Cancellation);
            Assert.Equal(PlayerMessageKind.Welcome, (await fixture.Receive()).Kind);
            fixture.Completion = new PlayerCommandSession(fixture.Channel, fixture.NetworkTime).RunAsync(fixture.Stop.Token);
            return fixture;
        }

        internal async Task Send(PlayerMessage message) => await Client.SendAsync(PlayerMessageCodec.Encode(message), Cancellation);
        internal async Task<PlayerMessage> Receive() => PlayerMessageCodec.Decode(
            (await Client.ReceiveAsync(Cancellation).AsTask().WaitAsync(TimeSpan.FromSeconds(5), Cancellation))!);
        internal async Task Ping(ulong request)
        {
            await Send(new PlayerMessage(PlayerMessageKind.Ping, request, []));
            PlayerMessage response = await Receive();
            Assert.Equal(PlayerMessageKind.Pong, response.Kind);
            Assert.Equal(request, response.RequestId);
        }
        public async ValueTask DisposeAsync()
        {
            Stop.Cancel();
            try { await Completion.WaitAsync(TimeSpan.FromSeconds(5), Cancellation); }
            catch (Exception error) when (error is OperationCanceledException or FormatException or IOException or InvalidDataException) { }
            await Client.DisposeAsync();
            foreach (TcpClient socket in _sockets) socket.Dispose();
            await Host.DisposeAsync();
            Stop.Dispose();
            _files.Dispose();
        }
    }

    private sealed class ResponseGateTransport(PlayerMessageTransport inner) : PlayerMessageTransport(PlayerMessageCodec.MaximumMessageBytes)
    {
        private int _sends;
        internal TaskCompletionSource ResponseStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async ValueTask SendMessageAsync(byte[] message, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _sends) > 1)
            {
                ResponseStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            await inner.SendAsync(message, cancellationToken);
        }
        protected override ValueTask<byte[]?> ReceiveMessageAsync(CancellationToken cancellationToken) => inner.ReceiveAsync(cancellationToken);
        protected override void CloseTransport()
        {
            ValueTask closing = inner.DisposeAsync();
            Assert.True(closing.IsCompletedSuccessfully);
        }
    }
}
