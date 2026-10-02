using System.Threading.Channels;
using OpenFantasy.Hosting.Networking;

namespace OpenFantasy.Hosting.Tests.Networking;

public sealed class InMemoryPlayerTransportTests
{
    [Fact]
    public async Task Bounded_ordered_delivery_copies_bytes_and_releases_backpressure_after_a_read()
    {
        var (client, server) = InMemoryPlayerTransport.CreatePair();
        await using var clientLifetime = client;
        await using var serverLifetime = server;
        for (byte index = 0; index < InMemoryPlayerTransport.MaximumQueuedMessages; index++)
        {
            byte[] bytes = [index];
            await client.SendAsync(bytes, TestContext.Current.CancellationToken);
            bytes[0] = 255;
        }
        Task blocked = client.SendAsync(new byte[] { 8 }, TestContext.Current.CancellationToken).AsTask();
        Assert.False(blocked.IsCompleted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SendAsync(new byte[] { 9 }, TestContext.Current.CancellationToken).AsTask());
        Assert.Equal(new byte[] { 0 }, await server.ReceiveAsync(TestContext.Current.CancellationToken));
        await blocked.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        for (byte index = 1; index <= 8; index++)
            Assert.Equal(new byte[] { index }, await server.ReceiveAsync(TestContext.Current.CancellationToken));
        await server.SendAsync(new byte[] { 42 }, TestContext.Current.CancellationToken);
        Assert.Equal(new byte[] { 42 }, await client.ReceiveAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Receive_cancellation_releases_its_slot_and_peer_closure_is_observable()
    {
        var (client, server) = InMemoryPlayerTransport.CreatePair(64);
        await using var clientLifetime = client;
        await using var serverLifetime = server;
        using var cancellation = new CancellationTokenSource();
        Task<byte[]?> read = server.ReceiveAsync(cancellation.Token).AsTask();
        await Assert.ThrowsAsync<InvalidOperationException>(() => server.ReceiveAsync(TestContext.Current.CancellationToken).AsTask());
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
        await Assert.ThrowsAsync<InvalidDataException>(() => client.SendAsync(new byte[65], TestContext.Current.CancellationToken).AsTask());
        await client.SendAsync(new byte[] { 7 }, TestContext.Current.CancellationToken);
        Assert.Equal(new byte[] { 7 }, await server.ReceiveAsync(TestContext.Current.CancellationToken));
        await client.DisposeAsync();
        Assert.Null(await server.ReceiveAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ChannelClosedException>(() => server.SendAsync(new byte[] { 1 }, TestContext.Current.CancellationToken).AsTask());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.ReceiveAsync(TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task Disposal_cancels_a_blocked_send_without_needing_the_peer_to_drain()
    {
        var (client, server) = InMemoryPlayerTransport.CreatePair();
        await using var clientLifetime = client;
        await using var serverLifetime = server;
        for (int index = 0; index < InMemoryPlayerTransport.MaximumQueuedMessages; index++)
            await client.SendAsync(new byte[] { 1 }, TestContext.Current.CancellationToken);
        Task waiting = client.SendAsync(new byte[] { 2 }, TestContext.Current.CancellationToken).AsTask();
        await client.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }
}
