using System.Net.WebSockets;
using OpenFantasy.Core;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.Client;

namespace OpenFantasy.Server.Tests;

public sealed partial class PlayerProtocolClientTests
{
    [Fact]
    public async Task Actual_tls_client_validates_initial_view_and_startup_cancellation_does_not_own_connected_lifetime()
    {
        await using var world = await ServerWorld.Open();
        using var socket = world.NewSocket();
        await socket.ConnectAsync(world.Server.Endpoint, ServerWorld.Cancellation);
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(ServerWorld.Cancellation);
        var invitation = new PlayerConnectionInvitation(world.Server.Endpoint, world.Admission.Compatibility,
            world.Actor, world.Credential.Export());
        await using var client = await PlayerProtocolClient.ConnectAsync(new WebSocketPlayerTransport(socket), invitation, startup.Token);
        startup.Cancel();
        Assert.Equal(world.Actor, client.Welcome.ActorId);
        Assert.Equal(world.Actor, client.ReadView().PlayerActorId);
        Assert.NotNull(client.ReadView().Regional);
        await client.PingAsync().WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
        var result = await client.QueryAsync(new(PlayerQueryKind.ReadTradeInquiry)).WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
        Assert.Null(result.Inquiry);
        Assert.Equal(0UL, world.Authority.ReadStateForHost().AdvancingSteps);
        Assert.False(client.Completion.IsCompleted);
    }

    [Fact]
    public async Task In_memory_client_keeps_preflight_refusal_separate_from_committed_receipts_and_exact_retry()
    {
        await using var fixture = await ProtocolClientFixture.Create();
        var client = await fixture.Connect();
        var command = fixture.Speak(10);
        var refused = await Assert.ThrowsAsync<PlayerRequestRefusedException>(() => client.SubmitActionAsync(command)
            .WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation));
        Assert.Equal(PlayerRequestRefusal.NotAdmitted, refused.Refusal);
        Assert.Equal(0UL, fixture.World.Authority.ReadStateForHost().Revision);
        client.SetReady(true);
        await client.PingAsync().WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
        WorldActionReceipt receipt = await client.SubmitActionAsync(command).WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
        Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
        Assert.Equal(command.ContentHash, receipt.CommandHash);
        Assert.Equal(WorldReceiptCodecV1.Encode(fixture.World.Authority.FindReceipt(command, fixture.World.Delivery)!), WorldReceiptCodecV1.Encode(receipt));
        client.SetReady(false);
        await client.PingAsync().WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
        var before = fixture.World.Authority.ReadStateForHost().ContentHash;
        WorldActionReceipt retry = await client.SubmitActionAsync(command).WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
        Assert.Equal(WorldReceiptCodecV1.Encode(receipt), WorldReceiptCodecV1.Encode(retry));
        Assert.Equal(before, fixture.World.Authority.ReadStateForHost().ContentHash);
    }

    [Fact]
    public async Task Pending_history_keeps_control_and_ping_live_and_disposal_releases_client_waiters()
    {
        await using var fixture = await ProtocolClientFixture.Create();
        var client = await fixture.Connect();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task hold = Task.Run(() => fixture.World.Authority.ReadForController(fixture.World.Actor, fixture.World.Delivery, state =>
        {
            entered.SetResult();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5), ServerWorld.Cancellation));
            return state.Revision;
        }), ServerWorld.Cancellation);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
            Task pending = client.QueryAsync(new(PlayerQueryKind.InspectPaidQuote, subjectId: new EntityId(700, 1)));
            var busy = await Assert.ThrowsAsync<PlayerRequestRefusedException>(() => client.QueryAsync(new(PlayerQueryKind.ReadTradeInquiry)));
            Assert.Equal(PlayerRequestRefusal.Busy, busy.Refusal);
            client.SetReady(false);
            await client.PingAsync().WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
            Assert.False(pending.IsCompleted);
            await client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
            Exception? ended = await Record.ExceptionAsync(() => pending.WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation));
            Assert.NotNull(ended);
            Assert.IsNotType<TimeoutException>(ended);
        }
        finally { release.Set(); await hold; }
    }

    [Fact]
    public async Task Unchanged_input_is_bounded_and_stop_remains_ordered_before_ping()
    {
        await using var fixture = await ProtocolClientFixture.Create();
        var client = await fixture.Connect(time: new StationaryInputClock());
        client.SetReady(true);
        for (int n = 0; n < 500; n++) client.SetReady(true);
        client.SetMovement(32, 0);
        for (int n = 0; n < 500; n++) client.SetMovement(32, 0);
        client.SetMovement(0, 0);
        client.SetReady(false);
        await client.PingAsync().WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
        var sent = fixture.Transport.Sent.ToArray();
        Assert.Equal(2, sent.Count(message => message.Kind == PlayerMessageKind.Readiness));
        PlayerMessage[] movement = sent.Where(message => message.Kind == PlayerMessageKind.Movement).ToArray();
        Assert.Equal(2, movement.Length);
        Assert.Equal(new byte[8], movement[^1].Payload.ToArray());
        Assert.Equal(sent.Select(message => message.RequestId).Distinct().Count(), sent.Length);
        Assert.Equal(sent.Select(message => message.RequestId).Order(), sent.Select(message => message.RequestId));
    }
    private sealed class StationaryInputClock : TimeProvider
    {
        public override long GetTimestamp() => 0;
    }
}
