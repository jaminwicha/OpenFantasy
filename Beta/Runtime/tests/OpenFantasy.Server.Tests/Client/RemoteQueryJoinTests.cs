using OpenFantasy.Core.Custody;
using OpenFantasy.Hosting.Networking.Client;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Server.Tests;

public sealed class RemoteQueryJoinTests
{
    [Theory]
    [InlineData("submit")]
    [InlineData("cancel")]
    [InlineData("proposal")]
    public async Task Disposal_joins_the_exact_typed_foreground_task_before_returning(string operation)
    {
        await using var world = await RemoteSessionWorld.Open();
        await using var session = await RemotePlayerSession.OpenAsync(await world.ConnectProtocol(), ServerWorld.Cancellation);
        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        world.Tap!.BeforeReceive = async (_, token) =>
        {
            blocked.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        };
        await blocked.Task.WaitAsync(TimeSpan.FromSeconds(3), ServerWorld.Cancellation);
        Task query = operation switch
        {
            "submit" => session.SubmitTradeInquiryAsync(Inquiry(world, session)),
            "cancel" => session.CancelTradeInquiryAsync(),
            _ => session.InspectMaterialProposalAsync(new(882, 1))
        };
        Assert.False(query.IsCompleted);
        await session.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3), ServerWorld.Cancellation);
        Assert.True(query.IsCompleted);
        Assert.True(session.Completion.IsCompleted);
        Assert.NotNull(await Record.ExceptionAsync(() => query));
        Assert.Equal(LocalWorldRunState.Unavailable, session.ReadRunState());
        Assert.False(world.Host.Session.Completion.IsCompleted);
    }

    [Fact]
    public async Task Invalid_and_closed_queries_return_faulted_tasks_without_throwing_from_the_facade()
    {
        await using var world = await RemoteSessionWorld.Open();
        await using var session = await RemotePlayerSession.OpenAsync(await world.ConnectProtocol(), ServerWorld.Cancellation);
        Task? invalid = null;
        Assert.Null(Record.Exception(() => { invalid = session.SubmitTradeInquiryAsync(null!); }));
        Assert.True(invalid!.IsFaulted);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => invalid);
        var inquiry = Inquiry(world, session);
        await session.DisposeAsync();
        Func<Task>[] closed = [() => session.SubmitTradeInquiryAsync(inquiry), () => session.CancelTradeInquiryAsync(),
            () => session.InspectPaidQuoteAsync(new(882, 2)), () => session.InspectMaterialProposalAsync(new(882, 3))];
        foreach (var start in closed)
        {
            Task? pending = null;
            Assert.Null(Record.Exception(() => { pending = start(); }));
            Assert.True(pending!.IsFaulted);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
        }
    }

    private static LocalTradeInquiry Inquiry(RemoteSessionWorld world, RemotePlayerSession session)
    {
        var source = world.Genesis.Inventory.Sources.First();
        return new(session.Presentation.CargoStalls[0].MarketId,
            new CountedItemDefinition(source.Definition.DefinitionId, source.Definition.Version), LocalTradeDirection.Buy, 1);
    }
}
