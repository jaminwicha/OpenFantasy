using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;

namespace OpenFantasy.Hosting.Tests.Networking.PlayerQueries;

/// <summary>A real admitted transport attached to an existing saved-world host; owns only its connection.</summary>
internal sealed class QuerySessionConnection : IAsyncDisposable
{
    private readonly List<TcpClient> _sockets = [];
    private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
    private ulong _requestId = 1;
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    internal ManualSessionTimeProvider NetworkTime { get; } = new();
    internal PlayerMessageTransport Client { get; private set; } = null!;
    internal AuthenticatedPlayerChannel Channel { get; private set; } = null!;
    internal Task Completion { get; private set; } = null!;

    internal static async Task<QuerySessionConnection> Open(LocalWorldSession host, EntityId actor,
        TrustedCommandDelivery delivery, bool sockets = false)
    {
        var connection = new QuerySessionConnection();
        if (host.Player.ActorId == actor) await host.Player.DisposeAsync();
        var service = new PlayerAdmissionService(host, []);
        var credential = service.Issue(actor, delivery, TimeSpan.FromMinutes(1));
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
                connection._sockets.Add(client);
                ValueTask connecting = client.ConnectAsync(IPAddress.Loopback,
                    ((IPEndPoint)listener.LocalEndpoint).Port, deadline.Token);
                TcpClient accepted = await listener.AcceptTcpClientAsync(deadline.Token);
                connection._sockets.Add(accepted);
                await connecting;
                connection.Client = new WebSocketPlayerTransport(WebSocket.CreateFromStream(client.GetStream(), false, null, TimeSpan.FromSeconds(30)));
                server = new WebSocketPlayerTransport(WebSocket.CreateFromStream(accepted.GetStream(), true, null, TimeSpan.FromSeconds(30)));
            }
            finally { listener.Stop(); }
        }
        else
        {
            var pair = InMemoryPlayerTransport.CreatePair();
            connection.Client = pair.Client;
            server = pair.Server;
        }
        await connection.Send(PlayerHandshakeCodec.Hello(1, service.Compatibility, credential.Export()));
        connection.Channel = await service.AcceptAsync(server, Cancellation);
        Assert.Equal(PlayerMessageKind.Welcome, (await connection.Receive()).Kind);
        connection.Completion = new PlayerCommandSession(connection.Channel, connection.NetworkTime).RunAsync(connection._stop.Token);
        return connection;
    }

    internal ulong NextId() => ++_requestId;
    internal async Task Send(PlayerMessage message) => await Client.SendAsync(PlayerMessageCodec.Encode(message), Cancellation);
    internal Task SendQuery(ulong requestId, PlayerQuery query) =>
        Send(new PlayerMessage(PlayerMessageKind.Query, requestId, PlayerQueryCodec.Encode(query)));
    internal async Task<PlayerQueryResult> Query(PlayerQuery query)
    {
        ulong requestId = NextId();
        await SendQuery(requestId, query);
        PlayerMessage response = await Receive();
        Assert.Equal(PlayerMessageKind.QueryResult, response.Kind);
        Assert.Equal(requestId, response.RequestId);
        PlayerQueryResult result = PlayerQueryResultCodec.Decode(response.Payload);
        LocalWorldView scope = Channel.Player.ReadView();
        result.ValidateFor(query, scope.WorldId, scope.BranchId, Channel.Player.ActorId);
        return result;
    }
    internal async Task<PlayerMessage> Receive()
    {
        byte[]? bytes = await Client.ReceiveAsync(Cancellation).AsTask().WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
        if (bytes is null)
        {
            if (Completion?.IsCompleted == true) await Completion;
            throw new EndOfStreamException("The connection closed before its expected response.");
        }
        return PlayerMessageCodec.Decode(bytes);
    }
    internal async Task Ping()
    {
        ulong requestId = NextId();
        await Send(new PlayerMessage(PlayerMessageKind.Ping, requestId, []));
        PlayerMessage response = await Receive();
        Assert.Equal((PlayerMessageKind.Pong, requestId), (response.Kind, response.RequestId));
    }
    internal void Stop() => _stop.Cancel();
    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        try { await Completion.WaitAsync(TimeSpan.FromSeconds(5), Cancellation); }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        finally
        {
            await Client.DisposeAsync();
            foreach (TcpClient socket in _sockets) socket.Dispose();
            _stop.Dispose();
        }
    }
}
