using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.Simulation.SharedPlayers;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Networking.PlayerObservations;

internal sealed class ObservationSessionFixture : IAsyncDisposable
{
    private readonly SessionFiles _files = new();
    private readonly List<TcpClient> _sockets = [];
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private ObservationSessionFixture(WorldSnapshot genesis, EntityId? actor)
    {
        ActorId = actor ?? genesis.Residents.First(resident => resident.ControllerKind == ResidentControllerKind.Player).ActorId;
        var control = genesis.Inventory.Controls.Single(value => value.ActorId == ActorId);
        Delivery = new(control.PrincipalId, control.SessionId);
        Authority = SqliteWorldAuthority.CreateNew(_files.Path, genesis);
        Host = new LocalWorldSession(Authority, ActorId, Delivery, HostTime);
    }
    internal EntityId ActorId { get; }
    internal TrustedCommandDelivery Delivery { get; }
    internal SqliteWorldAuthority Authority { get; }
    internal LocalWorldSession Host { get; }
    internal ManualSessionTimeProvider HostTime { get; } = new();
    internal ManualSessionTimeProvider NetworkTime { get; } = new();
    internal CancellationTokenSource Stop { get; } = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
    internal PlayerMessageTransport Client { get; private set; } = null!;
    internal AuthenticatedPlayerChannel Channel { get; private set; } = null!;
    internal Task Completion { get; private set; } = null!;
    internal ObservationGateTransport? Gate { get; private set; }

    internal static async Task<ObservationSessionFixture> Open(WorldSnapshot? genesis = null, bool sockets = false,
        bool blockObservations = false, EntityId? actor = null)
    {
        var fixture = new ObservationSessionFixture(genesis ?? SharedPlayerSessionTests.TwoPlayers(), actor);
        await fixture.Host.Player.DisposeAsync();
        var service = new PlayerAdmissionService(fixture.Host, []);
        var credential = service.Issue(fixture.ActorId, fixture.Delivery, TimeSpan.FromMinutes(1), new LocalRegionObserver(fixture.ActorId));
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
        if (blockObservations) server = fixture.Gate = new ObservationGateTransport(server);
        await fixture.Send(PlayerHandshakeCodec.Hello(1, service.Compatibility, credential.Export()));
        fixture.Channel = await service.AcceptAsync(server, Cancellation);
        Assert.Equal(PlayerMessageKind.Welcome, (await fixture.Receive()).Kind);
        fixture.Completion = new PlayerCommandSession(fixture.Channel, fixture.NetworkTime).RunAsync(fixture.Stop.Token);
        return fixture;
    }

    internal async Task Send(PlayerMessage message) => await Client.SendAsync(PlayerMessageCodec.Encode(message), Cancellation);
    internal Task Subscribe(ulong request) => Send(new PlayerMessage(PlayerMessageKind.Observation, request, []));
    internal async Task<PlayerMessage> Receive()
    {
        byte[]? bytes = await Client.ReceiveAsync(Cancellation).AsTask().WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
        if (bytes is null)
        {
            if (Completion?.IsCompleted == true) await Completion;
            throw new EndOfStreamException("The player channel closed before its expected response.");
        }
        return PlayerMessageCodec.Decode(bytes);
    }
    internal async Task<PlayerObservationFrame> ReceiveObservation()
    {
        PlayerMessage message = await Receive();
        Assert.Equal(PlayerMessageKind.Observation, message.Kind);
        Assert.Equal(0UL, message.RequestId);
        return PlayerObservationFrameCodec.Decode(message.Payload);
    }
    internal async Task Ping(ulong request)
    {
        await Send(new PlayerMessage(PlayerMessageKind.Ping, request, []));
        PlayerMessage response = await Receive();
        Assert.Equal(PlayerMessageKind.Pong, response.Kind);
        Assert.Equal(request, response.RequestId);
    }
    internal async Task ExpectQuiet()
    {
        // This bounded negative observation uses only the reusable in-memory transport.
        using var quiet = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        quiet.CancelAfter(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client.ReceiveAsync(quiet.Token).AsTask());
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

internal sealed class ObservationGateTransport(PlayerMessageTransport inner) : PlayerMessageTransport(PlayerMessageCodec.MaximumMessageBytes)
{
    private int _observationSends;
    internal int ObservationSends => Volatile.Read(ref _observationSends);
    internal TaskCompletionSource ObservationStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    protected override async ValueTask SendMessageAsync(byte[] message, CancellationToken cancellationToken)
    {
        if (PlayerMessageCodec.Decode(message).Kind == PlayerMessageKind.Observation)
        {
            Interlocked.Increment(ref _observationSends);
            ObservationStarted.TrySetResult();
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
