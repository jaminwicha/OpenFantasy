using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Tests.Client.SelectedTrading;

public sealed class MarketQueryLifetimeTests
{
    private static EntityId Id(ulong value) => new(0x6d61726b65740000, value);
    private static FarmMarketQueryContext Context(ulong lifetime, ulong generation = 1) => new(
        new(Id(1), Id(2), Id(3), new LocalActorSelection(Id(4), Id(3), Id(3), generation, null)), lifetime, Id(5));

    [Fact]
    public async Task Late_reply_after_reopening_does_not_consume_the_new_visits_initial_read()
    {
        var queries = new FarmMarketQueryLifetime();
        var first = Context(1);
        queries.OpenVisit();
        Assert.True(queries.ShouldRead(first.Scope, 0));
        Assert.True(queries.CompleteInquiry(first, first, null, false, 0));
        Assert.False(queries.ShouldRead(first.Scope, 1_000));
        var delayed = new TaskCompletionSource<LocalTradeInquiryView?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<LocalTradeInquiryView?> admitted = queries.Track(delayed.Task);
        queries.OpenVisit();
        var reopened = Context(3);
        delayed.SetResult(null);
        var result = await admitted;
        queries.Observed(admitted);
        Assert.False(queries.CompleteInquiry(first, reopened, result, false, 2_000));
        // The old completion cannot impose its own delay or erase the mandatory initial read.
        Assert.True(queries.ShouldRead(reopened.Scope, 0));
        Assert.True(queries.CompleteInquiry(reopened, reopened, null, false, 2_000));
        Assert.False(queries.ShouldRead(reopened.Scope, 3_000));
    }

    [Fact]
    public void Same_actor_new_generation_and_stall_visit_are_different_query_lifetimes()
    {
        var queries = new FarmMarketQueryLifetime();
        var original = Context(1);
        var selectedAgain = Context(1, generation: 3);
        queries.OpenVisit();
        Assert.False(queries.CompleteInquiry(original, selectedAgain, null, false, 10));
        Assert.True(queries.ShouldRead(selectedAgain.Scope, 10));
        var revisited = original with { Lifetime = 3 };
        Assert.False(queries.CompleteInquiry(original, revisited, null, true, 10));
        Assert.True(queries.ShouldRead(revisited.Scope, 10));
    }

    [Fact]
    public void A_refused_current_read_stops_refresh_until_explicit_reopen()
    {
        var queries = new FarmMarketQueryLifetime();
        var context = Context(1);
        queries.OpenVisit();
        queries.ReadStarted(0);
        Assert.False(queries.ShouldRead(context.Scope, 499));
        Assert.True(queries.CompleteInquiry(context, context, null, true, 100));
        Assert.False(queries.ShouldRead(context.Scope, 10_000));
        queries.OpenVisit();
        Assert.True(queries.ShouldRead(context.Scope, 10_000));
    }

    [Fact]
    public async Task Shutdown_waits_for_every_admitted_facade_and_observes_both_failures()
    {
        var queries = new FarmMarketQueryLifetime();
        var first = new TaskCompletionSource<LocalTradeInquiryView?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource<PaidQuoteView?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = queries.Track(first.Task);
        _ = queries.Track(second.Task);
        Task drain = queries.DrainAsync();
        Assert.False(drain.IsCompleted);
        first.SetException(new InvalidOperationException("Selection was returned while the inquiry completed."));
        Assert.False(drain.IsCompleted);
        second.SetException(new InvalidOperationException("History was refused during shutdown."));
        await drain;
        Assert.True(first.Task.IsFaulted);
        Assert.True(second.Task.IsFaulted);
        Assert.True(drain.IsCompletedSuccessfully);
    }
}
