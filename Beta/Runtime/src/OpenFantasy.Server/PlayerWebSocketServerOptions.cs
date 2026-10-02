using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace OpenFantasy.Server;

/// <summary>One explicit HTTPS listener. The caller keeps its certificate alive until the server is disposed.</summary>
public sealed class PlayerWebSocketServerOptions
{
    public PlayerWebSocketServerOptions(IPAddress listenAddress, int port, X509Certificate2 certificate,
        string? advertisedHost = null)
    {
        ArgumentNullException.ThrowIfNull(listenAddress);
        ArgumentNullException.ThrowIfNull(certificate);
        if (listenAddress.AddressFamily is not (AddressFamily.InterNetwork or AddressFamily.InterNetworkV6))
            throw new ArgumentException("The server requires a concrete IP address family.", nameof(listenAddress));
        if (port is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        if (!certificate.HasPrivateKey) throw new ArgumentException("HTTPS requires a certificate with its private key.", nameof(certificate));
        bool wildcard = listenAddress.Equals(IPAddress.Any) || listenAddress.Equals(IPAddress.IPv6Any);
        if (wildcard && advertisedHost is null)
            throw new ArgumentException("A wildcard listener requires an explicit advertised host.", nameof(advertisedHost));
        string host = advertisedHost ?? listenAddress.ToString();
        if (string.IsNullOrWhiteSpace(host) || host.Length > 253 || host.Any(char.IsWhiteSpace)
            || Uri.CheckHostName(host) == UriHostNameType.Unknown)
            throw new ArgumentException("The advertised host must be one IP address or DNS hostname without a port or path.", nameof(advertisedHost));
        if (IPAddress.TryParse(host, out IPAddress? address)
            && (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)))
            throw new ArgumentException("The advertised host cannot be a wildcard address.", nameof(advertisedHost));
        ListenAddress = listenAddress.AddressFamily == AddressFamily.InterNetworkV6
            ? new IPAddress(listenAddress.GetAddressBytes(), listenAddress.ScopeId) : new IPAddress(listenAddress.GetAddressBytes());
        Port = port;
        Certificate = certificate;
        AdvertisedHost = host;
    }

    public IPAddress ListenAddress { get; }
    public int Port { get; }
    public X509Certificate2 Certificate { get; }
    public string AdvertisedHost { get; }
}
