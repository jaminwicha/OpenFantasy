using System.Net.WebSockets;

namespace OpenFantasy.Hosting.Networking.Client;

/// <summary>Creates the native client's TLS transport using platform certificate and hostname validation.</summary>
public static class PlayerWebSocketConnector
{
    public const string Subprotocol = "openfantasy.v1";
    public static readonly TimeSpan ConnectionLimit = TimeSpan.FromSeconds(10);

    public static async Task<PlayerProtocolClient> ConnectAsync(PlayerConnectionInvitation invitation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        long started = TimeProvider.System.GetTimestamp();
        using var deadline = new CancellationTokenSource(ConnectionLimit);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var socket = new ClientWebSocket();
        socket.Options.AddSubProtocol(Subprotocol);
        // No certificate callback, cookies, browser Origin or compression. Private credentials follow validated TLS.
        PlayerProtocolClient? client = null;
        try
        {
            await socket.ConnectAsync(invitation.Endpoint, linked.Token).ConfigureAwait(false);
            if (socket.SubProtocol != Subprotocol) throw new FormatException("The server did not select the player subprotocol.");
            client = await PlayerProtocolClient.ConnectAsync(new WebSocketPlayerTransport(socket), invitation, linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
            if (TimeProvider.System.GetElapsedTime(started) >= ConnectionLimit)
                throw new TimeoutException("The player connection exceeded its startup deadline.");
            return client;
        }
        catch
        {
            if (client is not null) await client.DisposeAsync().ConfigureAwait(false);
            socket.Dispose();
            throw;
        }
    }
}
