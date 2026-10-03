using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation.PrivateHistoryDirectory;

namespace OpenFantasy.Hosting.Tests.Client.SharedHistory;

public sealed class HistoryModelTests(PrivateHistoryDirectoryFixture fixture) : IClassFixture<PrivateHistoryDirectoryFixture>
{
    [Fact]
    public async Task Actual_owned_facade_discovers_pages_and_both_exact_detail_kinds_without_mutating_the_world()
    {
        await using var harness = new HistoryModelHarness(fixture);
        var before = harness.Authority.ReadStateForHost().ContentHash;
        var facade = new GatedHistoryFacade(harness.Application);
        var model = new FarmHistoryModel();
        model.Open(facade);
        await HistoryModelHarness.Settle(model);
        Assert.Equal(FarmHistoryState.Ready, model.State);
        Assert.Equal(harness.Application.ReadView().PlayerActorId, model.Selection!.ActorId);
        Assert.Null(model.SelectedEntry);
        var first = model.Page!;
        var proposal = model.Entries.First(entry => entry.Kind == LocalPrivateHistoryKind.MaterialProposal);
        Assert.True(model.Select(model.Capture, proposal.Kind, proposal.SubjectId));
        Assert.True(model.Inspect(model.Capture, proposal.Kind, proposal.SubjectId));
        await HistoryModelHarness.Settle(model);
        Assert.Equal(proposal.SubjectId, model.Detail!.MaterialProposal!.ProposalId);
        Assert.Null(model.Detail.PaidQuote);
        Assert.Equal(proposal.ClosedAtSequence, model.Detail.MaterialProposal.ClosedAtSequence);
        Assert.Equal(1, facade.ProposalCalls);
        var oldCapture = model.Capture;
        Assert.True(model.Next(oldCapture));
        Assert.Equal(first.Next, facade.LastPageRequest!.Before);
        Assert.Null(model.Page);
        Assert.Null(model.Detail);
        Assert.False(model.Select(oldCapture, proposal.Kind, proposal.SubjectId));
        await HistoryModelHarness.Settle(model);
        for (int page = 0; !model.Entries.Any(entry => entry.Kind == LocalPrivateHistoryKind.PaidQuote); page++)
        {
            Assert.True(page < 5);
            Assert.True(model.Next(model.Capture));
            await HistoryModelHarness.Settle(model);
        }
        var quote = model.Entries.First(entry => entry.Kind == LocalPrivateHistoryKind.PaidQuote);
        Assert.True(model.Select(model.Capture, quote.Kind, quote.SubjectId));
        Assert.True(model.Inspect(model.Capture, quote.Kind, quote.SubjectId));
        await HistoryModelHarness.Settle(model);
        Assert.Equal(quote.SubjectId, model.Detail!.PaidQuote!.QuoteId);
        Assert.Null(model.Detail.MaterialProposal);
        Assert.Equal(1, facade.QuoteCalls);
        Assert.True(model.Refresh(model.Capture));
        Assert.Null(facade.LastPageRequest!.Before);
        await HistoryModelHarness.Settle(model);
        Assert.Equal(first.Entries, model.Page!.Entries);
        int reads = facade.PageCalls;
        for (int frame = 0; frame < 10; frame++) model.Update();
        Assert.Equal(reads, facade.PageCalls);
        Assert.Equal(before, harness.Authority.ReadStateForHost().ContentHash);
        await model.DrainAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Closed_reopened_visit_drops_delayed_page_or_failure_without_consuming_a_new_read(bool failure)
    {
        await using var harness = new HistoryModelHarness(fixture);
        var gate = GatedHistoryFacade.Signal();
        var facade = new GatedHistoryFacade(harness.Application) { PageGate = gate };
        var model = new FarmHistoryModel();
        model.Open(facade);
        await facade.PageCaptured.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var oldCapture = model.Capture;
        model.Close("Focus left the game.");
        Assert.Null(model.Page);
        model.Open(facade);
        Assert.Equal(FarmHistoryState.Busy, model.State);
        Assert.Equal(1, facade.PageCalls);
        string? currentNotice = model.Notice;
        if (failure) gate.SetException(new InvalidOperationException("Obsolete failure must stay out of the new visit."));
        else gate.SetResult();
        await HistoryModelHarness.Settle(model);
        Assert.Equal(currentNotice, model.Notice);
        Assert.Null(model.Page);
        Assert.False(model.Refresh(oldCapture));
        facade.PageGate = null;
        Assert.True(model.Refresh(model.Capture));
        await HistoryModelHarness.Settle(model);
        Assert.Equal(2, facade.PageCalls);
        Assert.NotNull(model.Page);
        await model.DrainAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Changing_the_row_discards_a_delayed_detail_or_failure_without_auto_inspection(bool failure)
    {
        await using var harness = new HistoryModelHarness(fixture);
        var gate = GatedHistoryFacade.Signal();
        var facade = new GatedHistoryFacade(harness.Application) { DetailGate = gate };
        var model = new FarmHistoryModel();
        model.Open(facade);
        await HistoryModelHarness.Settle(model);
        var first = model.Entries[0];
        var second = model.Entries[1];
        Assert.True(model.Select(model.Capture, first.Kind, first.SubjectId));
        Assert.True(model.Inspect(model.Capture, first.Kind, first.SubjectId));
        await facade.DetailCaptured.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(model.Select(model.Capture, second.Kind, second.SubjectId));
        string? currentNotice = model.Notice;
        if (failure) gate.SetException(new InvalidOperationException("An obsolete row failed."));
        else gate.SetResult();
        await HistoryModelHarness.Settle(model);
        Assert.Null(model.Detail);
        Assert.Equal(second, model.SelectedEntry);
        Assert.Equal(currentNotice, model.Notice);
        Assert.Equal(1, facade.ProposalCalls);
        Assert.False(model.Inspect(model.Capture, first.Kind, first.SubjectId));
        facade.DetailGate = null;
        Assert.True(model.Inspect(model.Capture, second.Kind, second.SubjectId));
        await HistoryModelHarness.Settle(model);
        Assert.Equal(second.SubjectId, model.Detail!.MaterialProposal!.ProposalId);
        await model.DrainAsync();
    }

    [Theory]
    [InlineData("status")]
    [InlineData("sequence")]
    [InlineData("counterparty")]
    public async Task Detail_must_match_the_displayed_terminal_summary_even_when_subject_and_selection_match(string mismatch)
    {
        await using var harness = new HistoryModelHarness(fixture);
        var facade = new GatedHistoryFacade(harness.Application)
        {
            PageTransform = result =>
            {
                var entry = result.Value.Entries[0];
                entry = mismatch switch
                {
                    "status" => entry with { MaterialProposalStatus = MaterialProposalStatus.Accepted },
                    "sequence" => entry with { ClosedAtSequence = entry.ClosedAtSequence - 1 },
                    _ => entry with { CounterpartyActorId = new EntityId(0xfeed, 123) }
                };
                return result with { Value = new(result.Value.Request, result.Value.ThroughRevision, [entry], null) };
            }
        };
        var model = new FarmHistoryModel();
        model.Open(facade);
        await HistoryModelHarness.Settle(model);
        var row = Assert.Single(model.Entries);
        Assert.True(model.Select(model.Capture, row.Kind, row.SubjectId));
        Assert.True(model.Inspect(model.Capture, row.Kind, row.SubjectId));
        await HistoryModelHarness.Settle(model);
        Assert.Equal(FarmHistoryState.Failed, model.State);
        Assert.Null(model.Detail);
        await model.DrainAsync();
    }

    [Fact]
    public async Task A_page_with_a_different_complete_request_is_refused_without_automatic_retry()
    {
        await using var harness = new HistoryModelHarness(fixture);
        var facade = new GatedHistoryFacade(harness.Application)
        {
            PageTransform = result =>
            {
                Assert.NotNull(result.Value.Next);
                var different = new LocalPrivateHistoryPageRequest(result.Value.Next);
                return result with { Value = new(different, result.Value.ThroughRevision, [], null) };
            }
        };
        var model = new FarmHistoryModel();
        model.Open(facade);
        await HistoryModelHarness.Settle(model);
        Assert.Equal(FarmHistoryState.Failed, model.State);
        Assert.Null(model.Page);
        Assert.Null(model.SelectedEntry);
        for (int frame = 0; frame < 10; frame++) model.Update();
        Assert.Equal(1, facade.PageCalls);
        facade.PageTransform = null;
        Assert.True(model.Refresh(model.Capture));
        await HistoryModelHarness.Settle(model);
        Assert.Equal(FarmHistoryState.Ready, model.State);
        Assert.Equal(2, facade.PageCalls);
        await model.DrainAsync();
    }

    [Fact]
    public async Task Shutdown_observes_the_outstanding_failure_and_cannot_reopen_or_retain_private_state()
    {
        await using var harness = new HistoryModelHarness(fixture);
        var gate = GatedHistoryFacade.Signal();
        var facade = new GatedHistoryFacade(harness.Application) { PageGate = gate };
        var model = new FarmHistoryModel();
        model.Open(facade);
        await facade.PageCaptured.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Task draining = model.DrainAsync();
        Assert.False(draining.IsCompleted);
        Assert.False(model.IsOpen);
        Assert.Null(model.Page);
        Assert.Null(model.Selection);
        gate.SetException(new InvalidOperationException("Shutdown owns this delivered failure."));
        await draining.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(model.IsBusy);
        model.Open(facade);
        Assert.False(model.IsOpen);
        Assert.Equal(1, facade.PageCalls);
    }
}
