using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Diagnostics;
using OpenFantasy.Hosting.Networking;

namespace OpenFantasy.Hosting.Tests.Networking;

public sealed class WebSocketPlayerTransportTests
{
    [Fact]
    public async Task Actual_loopback_sockets_reassemble_binary_fragments_and_return_identical_envelopes()
    {
        await using var pair = await SocketPair.Open();
        await using var server = new WebSocketPlayerTransport(pair.Server);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        byte[] expected = PlayerMessageCodec.Encode(new PlayerMessage(PlayerMessageKind.Command, 17,
            Enumerable.Range(0, 20_000).Select(value => (byte)(value % 256)).ToArray()));
        await pair.Client.SendAsync(expected.AsMemory(0, 7000), WebSocketMessageType.Binary, false, deadline.Token);
        await pair.Client.SendAsync(expected.AsMemory(7000), WebSocketMessageType.Binary, true, deadline.Token);
        byte[] observed = (await server.ReceiveAsync(deadline.Token))!;
        Assert.Equal(expected, observed);
        Assert.Equal(17UL, PlayerMessageCodec.Decode(observed).RequestId);
        await using var client = new WebSocketPlayerTransport(pair.Client);
        await server.SendAsync(observed, deadline.Token);
        Assert.Equal(expected, await client.ReceiveAsync(deadline.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Oversized_fragmented_and_text_messages_abort_the_connection(bool text)
    {
        await using var pair = await SocketPair.Open();
        await using var server = new WebSocketPlayerTransport(pair.Server, 64);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        if (text)
            await pair.Client.SendAsync("hello"u8.ToArray().AsMemory(), WebSocketMessageType.Text, true, deadline.Token);
        else
        {
            await pair.Client.SendAsync(new byte[40].AsMemory(), WebSocketMessageType.Binary, false, deadline.Token);
            await pair.Client.SendAsync(new byte[25].AsMemory(), WebSocketMessageType.Binary, true, deadline.Token);
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => server.ReceiveAsync(deadline.Token).AsTask());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => server.ReceiveAsync(deadline.Token).AsTask());
    }

    [Fact]
    public async Task Peer_close_returns_eof_and_disposal_terminates_an_outstanding_receive()
    {
        await using (var pair = await SocketPair.Open())
        {
            await using var server = new WebSocketPlayerTransport(pair.Server);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await pair.Client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "complete", deadline.Token);
            Assert.Null(await server.ReceiveAsync(deadline.Token));
        }
        await using var second = await SocketPair.Open();
        await using var pendingServer = new WebSocketPlayerTransport(second.Server);
        Task<byte[]?> pending = pendingServer.ReceiveAsync(TestContext.Current.CancellationToken).AsTask();
        await pendingServer.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Close_inside_an_unfinished_message_is_not_a_complete_empty_message(int fragmentLength)
    {
        await using var pair = await SocketPair.Open();
        await using var server = new WebSocketPlayerTransport(pair.Server);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await pair.Client.SendAsync(new byte[fragmentLength].AsMemory(), WebSocketMessageType.Binary, false, deadline.Token);
        await pair.Client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "complete", deadline.Token);
        await Assert.ThrowsAsync<InvalidDataException>(() => server.ReceiveAsync(deadline.Token).AsTask());
    }

    [Fact]
    public async Task Empty_continuations_cannot_consume_unlimited_receive_work()
    {
        await using var pair = await SocketPair.Open();
        await using var server = new WebSocketPlayerTransport(pair.Server);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        for (int part = 0; part < WebSocketPlayerTransport.MaximumReceiveParts; part++)
            await pair.Client.SendAsync(ReadOnlyMemory<byte>.Empty, WebSocketMessageType.Binary, false, deadline.Token);
        await Assert.ThrowsAsync<InvalidDataException>(() => server.ReceiveAsync(deadline.Token).AsTask());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => server.ReceiveAsync(deadline.Token).AsTask());
    }

    [Fact]
    public async Task A_partial_message_has_an_absolute_deadline_and_cannot_resume_after_cancellation()
    {
        await using var pair = await SocketPair.Open();
        await using var server = new WebSocketPlayerTransport(pair.Server);
        using var outer = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await pair.Client.SendAsync(new byte[] { 1 }.AsMemory(), WebSocketMessageType.Binary, false, outer.Token);
        var watch = Stopwatch.StartNew();
        Exception? failure = await Record.ExceptionAsync(() => server.ReceiveAsync(outer.Token).AsTask());
        Assert.True(failure is OperationCanceledException or InvalidDataException);
        Assert.True(watch.Elapsed >= WebSocketPlayerTransport.ReassemblyLimit);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(8));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => server.ReceiveAsync(outer.Token).AsTask());
    }

    private sealed class SocketPair : IAsyncDisposable
    {
        private readonly TcpClient _client;
        private readonly TcpClient _server;
        private SocketPair(TcpClient client, TcpClient server)
        {
            _client = client;
            _server = server;
            Client = WebSocket.CreateFromStream(client.GetStream(), false, null, Timeout.InfiniteTimeSpan);
            Server = WebSocket.CreateFromStream(server.GetStream(), true, null, Timeout.InfiniteTimeSpan);
        }

        internal WebSocket Client { get; }
        internal WebSocket Server { get; }

        internal static async Task<SocketPair> Open()
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var client = new TcpClient { NoDelay = true };
            try
            {
                Task<TcpClient> accepted = listener.AcceptTcpClientAsync(deadline.Token).AsTask();
                await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint, deadline.Token);
                var server = await accepted;
                server.NoDelay = true;
                return new SocketPair(client, server);
            }
            catch { client.Dispose(); throw; }
            finally { listener.Stop(); }
        }

        public ValueTask DisposeAsync()
        {
            Client.Dispose();
            Server.Dispose();
            _client.Dispose();
            _server.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
