using System.Net;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Content.NativeCargo;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;
using OpenFantasy.Hosting.Simulation.Retreat;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests;

internal sealed class ServerWorld : IAsyncDisposable
{
    private bool _serverDisposed;
    internal string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "openfantasy-tls-" + Guid.NewGuid().ToString("N"));
    internal string Database => Path.Combine(DirectoryPath, "world.sqlite3");
    internal X509Certificate2 Certificate { get; }
    internal SqliteWorldAuthority Authority { get; }
    internal LocalWorldSession Host { get; }
    internal PlayerAdmissionService Admission { get; }
    internal PlayerWebSocketServer Server { get; private set; } = null!;
    internal EntityId Actor { get; }
    internal TrustedCommandDelivery Delivery { get; }
    internal PlayerJoinCredential Credential { get; }
    internal static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private ServerWorld()
    {
        Directory.CreateDirectory(DirectoryPath);
        Certificate = CreateCertificate();
        WorldSnapshot genesis = CargoOutcomeWorldAdmission.CreateEmbedded(CargoOutcomeWorldAdmission.SeedText, "Network Player");
        Actor = genesis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        var control = genesis.Inventory.Controls.Single(value => value.ActorId == Actor);
        Delivery = new(control.PrincipalId, control.SessionId);
        Authority = SqliteWorldAuthority.CreateNew(Database, genesis);
        Host = new LocalWorldSession(Authority, Actor, Delivery, Array.Empty<LocalMaterialGiftPolicy>(),
            NativeCargoWorld.Configuration(genesis), new LocalRegionObserver(Actor), LocalRetreatConfiguration.None);
        Admission = new PlayerAdmissionService(Host, FarmPackageCatalog.LoadEmbedded());
        Credential = Admission.Issue(Actor, Delivery, TimeSpan.FromMinutes(1), new LocalRegionObserver(Actor));
    }

    internal static async Task<ServerWorld> Open()
    {
        var world = new ServerWorld();
        await world.Host.Player.DisposeAsync();
        world.Server = await PlayerWebSocketServer.StartAsync(world.Admission, new(IPAddress.Loopback, 0, world.Certificate), Cancellation);
        return world;
    }

    internal ClientWebSocket NewSocket(bool trust = true, bool subprotocol = true)
    {
        var socket = new ClientWebSocket();
        socket.Options.CollectHttpResponseDetails = true;
        if (subprotocol) socket.Options.AddSubProtocol(PlayerWebSocketServer.Subprotocol);
        if (trust) socket.Options.RemoteCertificateValidationCallback = TrustTestCertificate;
        return socket;
    }

    internal bool TrustTestCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors) =>
        certificate is not null && (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) == SslPolicyErrors.None
        && certificate.GetCertHashString(HashAlgorithmName.SHA256) == Certificate.GetCertHashString(HashAlgorithmName.SHA256);

    internal async Task<WebSocketPlayerTransport> Connect()
    {
        ClientWebSocket socket = NewSocket();
        await socket.ConnectAsync(Server.Endpoint, Cancellation).WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
        var transport = new WebSocketPlayerTransport(socket);
        await Send(transport, PlayerHandshakeCodec.Hello(1, Admission.Compatibility, Credential.Export()));
        Assert.Equal(PlayerMessageKind.Welcome, (await Receive(transport)).Kind);
        return transport;
    }

    internal static async Task Send(PlayerMessageTransport transport, PlayerMessage message) =>
        await transport.SendAsync(PlayerMessageCodec.Encode(message), Cancellation);

    internal static async Task<PlayerMessage> Receive(PlayerMessageTransport transport)
    {
        byte[]? bytes = await transport.ReceiveAsync(Cancellation).AsTask().WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
        Assert.NotNull(bytes);
        return PlayerMessageCodec.Decode(bytes);
    }

    internal async Task DisposeFailedServer<T>() where T : Exception
    {
        await Assert.ThrowsAsync<T>(() => Server.DisposeAsync().AsTask());
        _serverDisposed = true;
    }

    internal static X509Certificate2 CreateCertificate()
    {
        using RSA key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(15));
    }

    public async ValueTask DisposeAsync()
    {
        try { if (!_serverDisposed && Server is not null) await Server.DisposeAsync(); }
        finally
        {
            await Host.DisposeAsync();
            Certificate.Dispose();
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
