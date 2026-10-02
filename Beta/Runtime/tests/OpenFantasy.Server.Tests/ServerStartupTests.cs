using System.Net;
using System.Net.Sockets;

namespace OpenFantasy.Server.Tests;

public sealed class ServerStartupTests
{
    [Fact]
    public async Task Failed_bind_cleans_partial_listener_and_leaves_authority_available()
    {
        await using var world = await ServerWorld.Open();
        var occupied = new TcpListener(IPAddress.Loopback, 0);
        occupied.Start();
        try
        {
            int port = ((IPEndPoint)occupied.LocalEndpoint).Port;
            await Assert.ThrowsAnyAsync<IOException>(() => PlayerWebSocketServer.StartAsync(world.Admission,
                new(IPAddress.Loopback, port, world.Certificate), ServerWorld.Cancellation));
            Assert.False(world.Admission.HostCompletion.IsCompleted);
            await using var valid = await world.Connect();
        }
        finally { occupied.Stop(); }
    }

    [Fact]
    public void Listener_requires_explicit_private_key_and_real_advertised_identity()
    {
        using var certificate = ServerWorld.CreateCertificate();
        using var publicOnly = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificate(certificate.RawData);
        Assert.Throws<ArgumentException>(() => new PlayerWebSocketServerOptions(IPAddress.Loopback, 0, publicOnly));
        Assert.Throws<ArgumentException>(() => new PlayerWebSocketServerOptions(IPAddress.Any, 0, certificate));
        Assert.Throws<ArgumentException>(() => new PlayerWebSocketServerOptions(IPAddress.Any, 0, certificate, "0.0.0.0"));
        Assert.Throws<ArgumentException>(() => new PlayerWebSocketServerOptions(IPAddress.Loopback, 0, certificate, "example.com/secret"));
        Assert.Equal("example.com", new PlayerWebSocketServerOptions(IPAddress.Any, 0, certificate, "example.com").AdvertisedHost);
    }
}
