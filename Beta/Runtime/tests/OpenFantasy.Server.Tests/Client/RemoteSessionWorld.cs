using System.Net;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.Client;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests;

internal sealed class RemoteSessionWorld : IAsyncDisposable
{
    internal ServerFiles Files { get; } = new();
    internal string Database => Path.Combine(Files.DirectoryPath, "world.sqlite3");
    internal X509Certificate2 Certificate { get; } = ServerWorld.CreateCertificate();
    internal CargoPlayerHost Host { get; private set; } = null!;
    internal PlayerWebSocketServer Server { get; private set; } = null!;
    internal WorldSnapshot Genesis { get; } = CargoOutcomeWorldAdmission.CreateEmbedded(CargoOutcomeWorldAdmission.SeedText, "Remote Player");
    internal PlayerConnectionInvitation Invitation { get; private set; } = null!;
    internal ProtocolTap? Tap { get; private set; }

    internal static async Task<RemoteSessionWorld> Open()
    {
        var world = new RemoteSessionWorld();
        try
        {
            using (var authority = SqliteWorldAuthority.CreateNew(world.Database, world.Genesis)) { }
            world.Host = await CargoPlayerHost.OpenExistingAsync(world.Database, ServerWorld.Cancellation);
            world.Server = await PlayerWebSocketServer.StartAsync(world.Host.Admission,
                new(IPAddress.Loopback, 0, world.Certificate), ServerWorld.Cancellation);
            var actor = Assert.Single(world.Host.Players);
            var credential = world.Host.Issue(actor, TimeSpan.FromMinutes(2));
            world.Invitation = new(world.Server.Endpoint, world.Host.Admission.Compatibility, actor, credential.Export());
            return world;
        }
        catch { await world.DisposeAsync(); throw; }
    }

    internal async Task<PlayerProtocolClient> ConnectProtocol()
    {
        var socket = new ClientWebSocket();
        socket.Options.AddSubProtocol(PlayerWebSocketConnector.Subprotocol);
        socket.Options.RemoteCertificateValidationCallback = (_, certificate, _, errors) => certificate is not null
            && (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) == SslPolicyErrors.None
            && certificate.GetCertHashString(HashAlgorithmName.SHA256) == Certificate.GetCertHashString(HashAlgorithmName.SHA256);
        await socket.ConnectAsync(Server.Endpoint, ServerWorld.Cancellation);
        Tap = new ProtocolTap(new WebSocketPlayerTransport(socket));
        return await PlayerProtocolClient.ConnectAsync(Tap, Invitation, ServerWorld.Cancellation);
    }

    public async ValueTask DisposeAsync()
    {
        try { if (Server is not null) await Server.DisposeAsync(); }
        finally
        {
            if (Host is not null) await Host.DisposeAsync();
            Certificate.Dispose();
            Files.Dispose();
        }
    }
}

/// <summary>
/// A real scheduled-trading fixture using the retained native-market genesis and merchant configuration.
/// Wren already stands in trading range with her admitted berry supply. No setup move, grant, quote or
/// response is fabricated; only the presentation bootstrap is explicitly supplied for this supported fixture.
/// The production cargo/TLS opener remains covered separately by <see cref="RemoteSessionWorld"/>.
/// </summary>
internal sealed class RemoteScheduledMarketWorld : IAsyncDisposable
{
    private readonly ServerFiles _files = new();
    private Task? _server;
    internal SqliteWorldAuthority Authority { get; private set; } = null!;
    internal LocalWorldSession Host { get; private set; } = null!;
    internal MarketBinding Stall { get; private set; } = null!;
    internal ProtocolTap Tap { get; private set; } = null!;
    internal PlayerProtocolClient Protocol { get; private set; } = null!;

    internal static async Task<RemoteScheduledMarketWorld> Open()
    {
        var world = new RemoteScheduledMarketWorld();
        try
        {
            WorldSnapshot genesis = NativeTradingWorld.CreateEmbedded("encounter-lifecycle-world", "Traveler");
            var player = Assert.Single(genesis.Residents, actor => actor.ControllerKind == ResidentControllerKind.Player);
            var control = Assert.Single(genesis.Inventory.Controls, value => value.ActorId == player.ActorId);
            var delivery = new TrustedCommandDelivery(control.PrincipalId, control.SessionId);
            world.Stall = NativeTradingWorld.FindStall(genesis)
                ?? throw new InvalidDataException("The retained native-market fixture must have its admitted stall.");
            world.Authority = SqliteWorldAuthority.CreateNew(Path.Combine(world._files.DirectoryPath, "market.sqlite3"), genesis);
            world.Host = new LocalWorldSession(world.Authority, player.ActorId, delivery, NativeTradingPolicy.Configuration(genesis));
            await world.Host.Player.DisposeAsync();
            var wood = Assert.Single(genesis.Gathering!.Nodes, node => node.Definition.Kind == GatherNodeKind.Oak)
                .Definition.YieldDefinition.DefinitionId;
            var bootstrap = new PlayerPresentationBootstrap(world.Stall.MerchantActorId, wood,
                world.Stall.Commodity.DefinitionId, false, []);
            var admission = new PlayerAdmissionService(world.Host, FarmPackageCatalog.LoadEmbedded());
            var credential = admission.Issue(player.ActorId, delivery, TimeSpan.FromMinutes(2), presentation: bootstrap);
            var invitation = new PlayerConnectionInvitation(new Uri("wss://localhost/player"), admission.Compatibility,
                player.ActorId, credential.Export());
            var pair = InMemoryPlayerTransport.CreatePair();
            world.Tap = new ProtocolTap(pair.Client);
            async Task Serve()
            {
                try
                {
                    var channel = await admission.AcceptAsync(pair.Server, ServerWorld.Cancellation);
                    await new PlayerCommandSession(channel).RunAsync(ServerWorld.Cancellation);
                }
                catch (OperationCanceledException) when (ServerWorld.Cancellation.IsCancellationRequested) { }
            }
            world._server = Serve();
            world.Protocol = await PlayerProtocolClient.ConnectAsync(world.Tap, invitation, ServerWorld.Cancellation);
            return world;
        }
        catch { await world.DisposeAsync(); throw; }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Protocol is not null) await Protocol.DisposeAsync();
            if (Tap is not null) await Tap.DisposeAsync();
            if (_server is not null) await _server.WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
        }
        finally
        {
            if (Host is not null) await Host.DisposeAsync();
            else if (Authority is not null) Authority.Dispose();
            _files.Dispose();
        }
    }
}
