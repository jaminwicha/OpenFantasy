using System.Collections.Concurrent;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.Client;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Server.Tests;

internal sealed class ProtocolClientFixture : IAsyncDisposable
{
    private readonly Task _server;
    private ProtocolClientFixture(ServerWorld world, ProtocolTap transport, Task server)
    {
        World = world;
        Transport = transport;
        _server = server;
    }

    internal ServerWorld World { get; }
    internal ProtocolTap Transport { get; }
    internal PlayerProtocolClient? Client { get; private set; }
    internal PlayerConnectionInvitation Invitation => new(new Uri("wss://localhost/player"), World.Admission.Compatibility,
        World.Actor, World.Credential.Export());

    internal static async Task<ProtocolClientFixture> Create()
    {
        var world = await ServerWorld.Open();
        var pair = InMemoryPlayerTransport.CreatePair();
        var tap = new ProtocolTap(pair.Client);
        // Peer end-of-stream closes the admitted session. The fixture owns the authority separately.
        async Task Serve()
        {
            try
            {
                var channel = await world.Admission.AcceptAsync(pair.Server, ServerWorld.Cancellation);
                await new PlayerCommandSession(channel).RunAsync(ServerWorld.Cancellation);
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        }
        return new(world, tap, Serve());
    }

    internal async Task<PlayerProtocolClient> Connect(CancellationToken? token = null, TimeProvider? time = null) =>
        Client = await PlayerProtocolClient.ConnectAsync(Transport, Invitation, token ?? ServerWorld.Cancellation, time);

    internal WorldActionCommand Speak(ulong id)
    {
        var view = Client!.ReadView();
        var player = view.Actors.Single(actor => actor.ActorId == view.PlayerActorId);
        var wren = view.Actors.Single(actor => actor.DisplayName == "Wren");
        var welcome = Client.Welcome;
        return new(new EntityId(900, id), welcome.Delivery.PrincipalId, welcome.Delivery.SessionId,
            welcome.ActorId, view.WorldId, view.BranchId, player.PresenceRevision,
            new SpeakToActorIntent(wren.ActorId, wren.PresenceRevision));
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Client is not null) await Client.DisposeAsync();
            await Transport.DisposeAsync();
            await _server.WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
        }
        finally { await World.DisposeAsync(); }
    }
}

internal sealed class ProtocolTap(PlayerMessageTransport inner) : PlayerMessageTransport(PlayerMessageCodec.MaximumMessageBytes)
{
    internal Func<PlayerMessage, PlayerMessage>? Incoming { get; set; }
    internal Func<PlayerMessage, CancellationToken, Task>? BeforeReceive { get; set; }
    internal Func<PlayerMessage, CancellationToken, Task>? BeforeSend { get; set; }
    internal Action<PlayerMessage>? AfterSend { get; set; }
    internal ConcurrentQueue<PlayerMessage> Sent { get; } = new();
    protected override async ValueTask SendMessageAsync(byte[] message, CancellationToken cancellationToken)
    {
        var decoded = PlayerMessageCodec.Decode(message);
        if (BeforeSend is { } before) await before(decoded, cancellationToken);
        await inner.SendAsync(message, cancellationToken);
        Sent.Enqueue(decoded);
        AfterSend?.Invoke(decoded);
    }
    protected override async ValueTask<byte[]?> ReceiveMessageAsync(CancellationToken cancellationToken)
    {
        byte[]? bytes = await inner.ReceiveAsync(cancellationToken);
        if (bytes is not null && BeforeReceive is { } before)
            await before(PlayerMessageCodec.Decode(bytes), cancellationToken);
        return bytes is null ? null : Incoming is { } map ? PlayerMessageCodec.Encode(map(PlayerMessageCodec.Decode(bytes))) : bytes;
    }
    protected override void CloseTransport() => inner.DisposeAsync().GetAwaiter().GetResult();
}
