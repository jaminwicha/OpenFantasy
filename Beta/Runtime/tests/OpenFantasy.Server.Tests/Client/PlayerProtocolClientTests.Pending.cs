using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Networking.Client;

namespace OpenFantasy.Server.Tests;

public sealed partial class PlayerProtocolClientTests
{
    [Fact]
    public async Task Concurrent_exact_action_shares_its_pending_task_and_sends_one_envelope()
    {
        await using var fixture = await ProtocolClientFixture.Create();
        var client = await fixture.Connect();
        client.SetReady(true);
        await client.PingAsync().WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
        var receiptWaiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Transport.BeforeReceive = async (message, token) =>
        {
            if (message.Kind != PlayerMessageKind.Receipt) return;
            receiptWaiting.TrySetResult();
            await release.Task.WaitAsync(token);
        };
        try
        {
            WorldActionCommand command = fixture.Speak(30);
            Task<WorldActionReceipt> first = client.SubmitActionAsync(command);
            await receiptWaiting.Task.WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
            Assert.False(first.IsCompleted);
            Assert.Same(first, client.SubmitActionAsync(command));
            var busy = await Assert.ThrowsAsync<PlayerRequestRefusedException>(() => client.SubmitActionAsync(fixture.Speak(31)));
            Assert.Equal(PlayerRequestRefusal.Busy, busy.Refusal);
            Assert.Single(fixture.Transport.Sent, message => message.Kind == PlayerMessageKind.Command);
            release.TrySetResult();
            var receipt = await first.WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
            Assert.Equal(WorldReceiptCodecV1.Encode(fixture.World.Authority.FindReceipt(command, fixture.World.Delivery)!),
                WorldReceiptCodecV1.Encode(receipt));
        }
        finally { release.TrySetResult(); }
    }

    [Fact]
    public async Task Established_blocked_send_expires_and_releases_a_queued_query_without_queue_overflow()
    {
        await using var fixture = await ProtocolClientFixture.Create();
        var client = await fixture.Connect();
        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Transport.BeforeSend = async (_, token) =>
        {
            blocked.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        };
        client.SetReady(true);
        await blocked.Task.WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation);
        Task query = client.QueryAsync(new(PlayerQueryKind.ReadTradeInquiry));
        // A bounded wall-clock check proves the real cancellation timer, not only timestamp arithmetic.
        await Assert.ThrowsAsync<TimeoutException>(() => client.Completion
            .WaitAsync(TimeSpan.FromSeconds(8), ServerWorld.Cancellation));
        Assert.True(client.Completion.IsFaulted);
        Assert.True(query.IsFaulted);
        await Assert.ThrowsAsync<TimeoutException>(() => query);
        Assert.DoesNotContain(fixture.Transport.Sent, message => message.Kind == PlayerMessageKind.Query);
    }

    [Fact]
    public async Task Established_send_returning_successfully_at_its_deadline_still_closes_the_client()
    {
        await using var fixture = await ProtocolClientFixture.Create();
        var clock = new StartupClock();
        var client = await fixture.Connect(time: clock);
        fixture.Transport.AfterSend = _ => clock.Advance(PlayerProtocolClient.SendLimit);
        client.SetReady(true);
        await Assert.ThrowsAsync<TimeoutException>(() => client.Completion
            .WaitAsync(TimeSpan.FromSeconds(5), ServerWorld.Cancellation));
        Assert.True(client.Completion.IsFaulted);
        Assert.Contains(fixture.Transport.Sent, message => message.Kind == PlayerMessageKind.Readiness);
    }
}
