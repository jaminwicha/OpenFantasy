using System.Diagnostics;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldExchange.Scheduled;

namespace OpenFantasy.Hosting.Tests.HistoryQueries;

public sealed class ArchivedQuoteReplayCostTests(ITestOutputHelper output)
{
    [Fact]
    public void Participant_query_retains_real_archived_market_terms_and_reports_synchronous_cost()
    {
        using var world = new ScheduledTradingWorld();
        var purchase = world.BuyOneFromWrenDirectly();
        ulong publishedAt = world.State.Revision;
        world.CutArchive();
        Assert.Null(world.State.Coordination!.PaidExchange!.FindOutcome(purchase.Quote.QuoteId));
        Assert.Null(world.State.Coordination.Markets!.FindQuote(purchase.Quote.QuoteId));
        world.Reopen();
        var before = world.Authority.InspectForHost();
        var delivery = world.DeliveryOf(world.Player);
        PaidQuoteView? Read() => world.Authority.ReadRecordedPaidQuoteForController(purchase.Quote.QuoteId,
            world.Player, delivery, (state, outcome) =>
                PaidQuoteProjection.ProjectRecorded(state, outcome, purchase.Quote.QuoteId, world.Player));
        Assert.NotNull(Read()); // Warm the actual query path, outside the measured sample.
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        long started = Stopwatch.GetTimestamp();
        const int queries = 2;
        for (int index = 0; index < queries; index++)
        {
            PaidQuoteView view = Assert.IsType<PaidQuoteView>(Read());
            Assert.Equal((PaidQuoteStatus.Filled, publishedAt, purchase.Quote.ContentHash, world.Fixture.WrenMarket.MarketId),
                (view.Status, view.ClosedAtSequence!.Value, view.QuoteHash, view.MarketId!.Value));
            Assert.Null(view.ObservedIssuerRevision);
        }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        output.WriteLine($"archive-query; queries:{queries}; journal-revision:{before.State.Revision}; archives:{before.Journal.MaterialCareArchiveCount}; milliseconds:{Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3}; bytes:{bytes}");
        Assert.Same(before.State, world.State);
        Assert.Equal(before.Journal, world.Authority.InspectForHost().Journal);
    }
}
