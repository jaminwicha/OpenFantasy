using System.Net.WebSockets;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Networking;

namespace OpenFantasy.Server.Tests;

public sealed class ServerShutdownTests
{
    [Fact]
    public async Task Cancelled_shutdown_wait_does_not_abandon_an_admitted_authority_read()
    {
        await using var world = await ServerWorld.Open();
        await using var transport = await world.Connect();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task held = Task.Run(() => world.Authority.ReadForController(world.Actor, world.Delivery, state =>
        {
            entered.SetResult();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5), ServerWorld.Cancellation));
            return state.Revision;
        }), ServerWorld.Cancellation);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
            await ServerWorld.Send(transport, new(PlayerMessageKind.Query, 2,
                PlayerQueryCodec.Encode(new(PlayerQueryKind.InspectPaidQuote, subjectId: new EntityId(9, 99)))));
            await ServerWorld.Send(transport, new(PlayerMessageKind.Query, 3,
                PlayerQueryCodec.Encode(new(PlayerQueryKind.ReadTradeInquiry))));
            Assert.Equal(PlayerRequestRefusal.Busy, PlayerGameplayCodec.ReadRefusal(await ServerWorld.Receive(transport)));
            using var stopWait = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => world.Server.StopAsync(stopWait.Token));
            Assert.False(world.Server.Completion.IsCompleted);
        }
        finally
        {
            release.Set();
            await held;
        }
        await world.Server.Completion.WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
        Assert.False(world.Admission.HostCompletion.IsCompleted);
        Assert.Equal(0UL, world.Authority.ReadStateForHost().Revision);
    }

    [Fact]
    public async Task Pending_handshake_limit_refuses_extra_peer_and_releases_capacity_on_stop()
    {
        await using var world = await ServerWorld.Open();
        var pending = new List<ClientWebSocket>();
        try
        {
            for (int index = 0; index < PlayerAdmissionService.MaximumPendingHandshakes; index++)
            {
                ClientWebSocket socket = world.NewSocket();
                pending.Add(socket);
                await socket.ConnectAsync(world.Server.Endpoint, ServerWorld.Cancellation);
            }
            using var extra = world.NewSocket();
            // Admission abort may arrive before ClientWebSocket publishes the HTTP upgrade to its caller.
            await Assert.ThrowsAsync<WebSocketException>(async () =>
            {
                await extra.ConnectAsync(world.Server.Endpoint, ServerWorld.Cancellation);
                await using var transport = new WebSocketPlayerTransport(extra);
                await transport.ReceiveAsync(ServerWorld.Cancellation).AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
            });
            Assert.False(world.Server.Completion.IsCompleted);
            await world.Server.StopAsync(ServerWorld.Cancellation).WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
            Assert.True(world.Server.Completion.IsCompletedSuccessfully);
        }
        finally { foreach (ClientWebSocket socket in pending) socket.Dispose(); }
    }
}
