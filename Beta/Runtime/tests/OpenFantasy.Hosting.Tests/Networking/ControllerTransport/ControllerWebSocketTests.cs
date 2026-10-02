using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using static OpenFantasy.Hosting.Tests.Networking.ControllerTransport.ControllerTransportFixture;

namespace OpenFantasy.Hosting.Tests.Networking.ControllerTransport;

public sealed class ControllerWebSocketTests
{
    [Theory]
    [InlineData("close", WorldControllerDisconnectReason.PeerClosed)]
    [InlineData("text", WorldControllerDisconnectReason.ProtocolFailure)]
    [InlineData("abort", WorldControllerDisconnectReason.TransportLost)]
    [InlineData("reassembly-timeout", WorldControllerDisconnectReason.TransportLost)]
    public async Task Actual_websocket_end_origin_reaches_the_established_world_authority(
        string termination, WorldControllerDisconnectReason reason)
    {
        await using var fixture = await Open();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        using var clientTcp = new TcpClient();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            ValueTask connecting = clientTcp.ConnectAsync(IPAddress.Loopback,
                ((IPEndPoint)listener.LocalEndpoint).Port, deadline.Token);
            using TcpClient serverTcp = await listener.AcceptTcpClientAsync(deadline.Token);
            await connecting;
            using WebSocket clientSocket = WebSocket.CreateFromStream(clientTcp.GetStream(), false, null, Timeout.InfiniteTimeSpan);
            using WebSocket serverSocket = WebSocket.CreateFromStream(serverTcp.GetStream(), true, null, Timeout.InfiniteTimeSpan);
            var (client, channel) = await fixture.Connect(new WebSocketPlayerTransport(clientSocket), new WebSocketPlayerTransport(serverSocket));
            Task run = fixture.Run(channel);
            // A real response proves the established session is receiving before this socket is ended.
            await Send(client, new(PlayerMessageKind.Ping, 1, []));
            Assert.Equal(PlayerMessageKind.Pong, (await Receive(client)).Kind);
            if (termination == "close")
            {
                await clientSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, deadline.Token);
                await Finish(run);
            }
            else if (termination == "text")
            {
                await clientSocket.SendAsync(new byte[] { 65 }, WebSocketMessageType.Text, true, deadline.Token);
                await Assert.ThrowsAsync<InvalidDataException>(() => Finish(run));
            }
            else if (termination == "abort")
            {
                clientSocket.Abort();
                await Assert.ThrowsAsync<WebSocketException>(() => Finish(run));
            }
            else
            {
                await clientSocket.SendAsync(new byte[] { 1 }, WebSocketMessageType.Binary, false, deadline.Token);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(10), deadline.Token));
            }
            await fixture.AssertDisconnected(reason);
        }
        finally { listener.Stop(); }
    }
}
