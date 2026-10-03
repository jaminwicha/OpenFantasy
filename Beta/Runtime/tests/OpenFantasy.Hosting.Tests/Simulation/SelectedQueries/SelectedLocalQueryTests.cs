using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation.ActorSelection;

namespace OpenFantasy.Hosting.Tests.Simulation.SelectedQueries;

public sealed class SelectedLocalQueryTests
{
    [Fact]
    public async Task Actor_cycle_invalidates_captured_results_and_cancels_only_the_outgoing_inquiry()
    {
        await using var fixture = await ActorSelectionFixture.Open();
        var (grantee, third, _, _) = await fixture.Acquire();
        var player = grantee.Player;
        var first = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var market = first.HomeView.Markets!.Stalls.First();
        var inquiry = new LocalTradeInquiry(market.MarketId, market.Commodity, LocalTradeDirection.Buy, 1);
        var outsider = third.Player.ReadSelectedObservation();
        var otherInquiry = await third.Player.SubmitSelectedTradeInquiryAsync(outsider.Selection, inquiry);
        var before = fixture.State;
        var admitted = await player.SubmitSelectedTradeInquiryAsync(first.Selection, inquiry);
        Assert.Equal(LocalTradeInquiryState.Waiting, admitted.Value.State);
        admitted.ValidateFor(first, LocalSelectedQueryOperation.SubmitTradeInquiry);
        Assert.Equal(fixture.Owner, admitted.Selection.ActorId);
        Assert.Equal(fixture.Grantee, player.ActorId);
        var unknown = new EntityId(0x5e1ec7ed, 1);
        var oldResult = await player.InspectSelectedOpenPaidQuoteAsync(first.Selection, unknown);
        Assert.Null(oldResult.Value);
        oldResult.ValidateFor(first, LocalSelectedQueryOperation.InspectOpenPaidQuote, unknown);
        Assert.False(first.IsReady);

        var home = await player.SelectActorAsync(first.Selection, fixture.Grantee);
        var second = await player.SelectActorAsync(home.Selection, fixture.Owner);
        Assert.Equal(first.Selection.ActorId, second.Selection.ActorId);
        Assert.True(second.Selection.Generation > first.Selection.Generation);
        Assert.Throws<AuthorityDispatchException>(() => oldResult.ValidateFor(second,
            LocalSelectedQueryOperation.InspectOpenPaidQuote, unknown));
        Assert.Throws<AuthorityDispatchException>(() => oldResult.ValidateFor(first,
            LocalSelectedQueryOperation.InspectPaidQuoteHistory, unknown));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => player.CancelSelectedTradeInquiryAsync(first.Selection));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => player.InspectSelectedOpenPaidQuoteAsync(first.Selection, unknown));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => third.Player.InspectSelectedOpenPaidQuoteAsync(second.Selection, unknown));
        Assert.Null((await player.ReadSelectedTradeInquiryAsync(second.Selection)).Value);
        Assert.Equal(otherInquiry.Value, (await third.Player.ReadSelectedTradeInquiryAsync(outsider.Selection)).Value);
        Assert.Equal(LocalTradeInquiryState.Waiting,
            (await player.SubmitSelectedTradeInquiryAsync(second.Selection, inquiry)).Value.State);
        Assert.Equal(LocalTradeInquiryState.Cancelled,
            (await player.CancelSelectedTradeInquiryAsync(second.Selection)).Value!.State);
        Assert.Same(before, fixture.State);
        Assert.Equal(LocalWorldRunState.Paused, player.ReadRunState());
    }

    [Fact]
    public async Task Pending_permanent_home_can_inspect_but_cannot_mutate_while_its_actor_is_delegated()
    {
        await using var fixture = await ActorSelectionFixture.Open();
        var (grantee, _, _, witness) = await fixture.Acquire();
        // This trusted provisional port has not completed network admission and cannot restore control.
        await using var owner = await fixture.Host.ConnectPlayerAsync(fixture.Owner, fixture.Delivery(fixture.Owner));
        var home = owner.ReadSelectedObservation();
        Assert.False(owner.NetworkEstablished);
        Assert.False(owner.ControlAvailable);
        var before = fixture.State;
        var unknown = new EntityId(0x5e1ec7ed, 2);
        Assert.Null((await owner.InspectSelectedOpenPaidQuoteAsync(home.Selection, unknown)).Value);
        Assert.Null((await owner.InspectSelectedMaterialProposalHistoryAsync(home.Selection, unknown)).Value);
        Assert.Null((await owner.ReadSelectedTradeInquiryAsync(home.Selection)).Value);
        var market = home.HomeView.Markets!.Stalls.First();
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => owner.SubmitSelectedTradeInquiryAsync(home.Selection,
            new(market.MarketId, market.Commodity, LocalTradeDirection.Buy, 1)));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => owner.CancelSelectedTradeInquiryAsync(home.Selection));
        Assert.Equal(witness, grantee.Player.LiveControlWitness);
        Assert.Same(before, fixture.State);
        Assert.Equal(LocalWorldRunState.Paused, owner.ReadRunState());
    }

    [Fact]
    public async Task Shared_slots_deduplicate_exact_requests_bound_other_kinds_and_join_on_shutdown()
    {
        await using var fixture = await ActorSelectionFixture.Open();
        var (grantee, _, _, _) = await fixture.Acquire();
        var player = grantee.Player;
        var selected = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task held = Task.Run(() => fixture.Authority.ReadForController(fixture.Owner, fixture.Delivery(fixture.Owner), state =>
        {
            entered.SetResult();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            return state.Revision;
        }), TestContext.Current.CancellationToken);
        var subject = new EntityId(0x5e1ec7ed, 3);
        Task<LocalSelectedQueryResult<PaidQuoteView?>>? quote = null;
        Task<LocalSelectedQueryResult<MaterialProposalView?>>? proposal = null;
        Task? stopping = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            quote = player.InspectSelectedOpenPaidQuoteAsync(selected.Selection, subject);
            Assert.Same(quote, player.InspectSelectedOpenPaidQuoteAsync(selected.Selection, subject));
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => player.InspectSelectedPaidQuoteHistoryAsync(selected.Selection, subject));
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => player.InspectPaidQuoteAsync(subject));
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => player.InspectSelectedOpenPaidQuoteAsync(selected.Selection,
                new EntityId(0x5e1ec7ed, 4)));
            proposal = player.InspectSelectedOpenMaterialProposalAsync(selected.Selection, subject);
            Assert.Same(proposal, player.InspectSelectedOpenMaterialProposalAsync(selected.Selection, subject));
            player.ResumeSelected(selected);
            player.PauseSelected(selected.Selection);
            Assert.False(player.Ready);
            stopping = fixture.Stop();
            Assert.False(stopping.IsCompleted);
            Assert.False(quote.IsCompleted);
        }
        finally { release.Set(); await held; }
        Assert.NotNull(quote);
        Assert.NotNull(proposal);
        Assert.NotNull(stopping);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => quote);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => proposal);
        await stopping.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(player.QuoteInspection.Latest!.IsCompleted);
        Assert.True(player.ProposalInspection.Latest!.IsCompleted);
    }

    [Fact]
    public async Task Revocation_before_a_waiting_read_executes_refuses_without_failing_the_host()
    {
        await using var fixture = await ActorSelectionFixture.Open();
        var (grantee, _, _, _) = await fixture.Acquire();
        var player = grantee.Player;
        var selected = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task held = Task.Run(() => fixture.Authority.ReadForController(fixture.Owner, fixture.Delivery(fixture.Owner), state =>
        {
            entered.SetResult();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            // A genuine trusted owner command changes the durable authority before the waiting reader acquires it.
            return fixture.Revoke().Revision;
        }), TestContext.Current.CancellationToken);
        Task<LocalSelectedQueryResult<PaidQuoteView?>>? pending = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            pending = player.InspectSelectedPaidQuoteHistoryAsync(selected.Selection, new EntityId(0x5e1ec7ed, 5));
            Assert.False(pending.IsCompleted);
        }
        finally { release.Set(); await held; }
        Assert.NotNull(pending);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => pending);
        Assert.Equal(LocalWorldRunState.Paused, player.ReadRunState());
        Assert.True((await fixture.Host.VerifyRecordedHistoryAsync()).ReproducesHead);
        await fixture.Tick();
        Assert.Equal(fixture.Grantee, player.ReadSelectedObservation().Selection.ActorId);
    }
}
