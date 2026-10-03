using OpenFantasy.Core;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation.ActorSelection;

namespace OpenFantasy.Hosting.Tests.Simulation.PrivateHistoryDirectory;

public sealed class PrivateHistoryDirectorySessionTests
{
    [Fact]
    public async Task Actor_cycle_invalidates_directory_result_and_future_cursor_refusal_keeps_the_host_available()
    {
        await using var fixture = await ActorSelectionFixture.Open();
        var (grantee, third, _, _) = await fixture.Acquire();
        var player = grantee.Player;
        var selected = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var request = new LocalPrivateHistoryPageRequest();
        var result = await player.ReadSelectedPrivateHistoryAsync(selected.Selection, request);
        result.ValidateFor(selected, LocalSelectedQueryOperation.ReadPrivateHistory);
        result.Value.ValidateFor(request);
        Assert.Empty(result.Value.Entries);
        Assert.Equal(fixture.Owner, result.Selection.ActorId);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => third.Player.ReadSelectedPrivateHistoryAsync(selected.Selection, request));
        var home = await player.SelectActorAsync(selected.Selection, fixture.Grantee);
        var again = await player.SelectActorAsync(home.Selection, fixture.Owner);
        Assert.Throws<AuthorityDispatchException>(() => result.ValidateFor(again, LocalSelectedQueryOperation.ReadPrivateHistory));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => player.ReadSelectedPrivateHistoryAsync(selected.Selection, request));
        var future = new LocalPrivateHistoryPageRequest(new(fixture.State.Revision + 1, 1,
            LocalPrivateHistoryKind.PaidQuote, new(0x5157, 1)));
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => player.ReadSelectedPrivateHistoryAsync(again.Selection, future));
        Assert.Empty((await player.ReadSelectedPrivateHistoryAsync(again.Selection, request)).Value.Entries);
        Assert.Equal(LocalWorldRunState.Paused, player.ReadRunState());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Exact_request_shares_its_slot_different_cursor_is_busy_and_shutdown_joins_the_directory_read(bool stopHost)
    {
        await using var fixture = await ActorSelectionFixture.Open();
        var (grantee, third, _, _) = await fixture.Acquire();
        var player = grantee.Player;
        var selected = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Authority.PrivateHistoryReadHoldsGate = () =>
        {
            entered.SetResult();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        };
        Task<LocalSelectedQueryResult<LocalPrivateHistoryPage>>? pending = null;
        Task? stopping = null;
        try
        {
            var request = new LocalPrivateHistoryPageRequest();
            pending = player.ReadSelectedPrivateHistoryAsync(selected.Selection, request);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Same(pending, player.ReadSelectedPrivateHistoryAsync(selected.Selection, new()));
            var different = new LocalPrivateHistoryPageRequest(new(1, 1, LocalPrivateHistoryKind.PaidQuote, new(0x5157, 2)));
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => player.ReadSelectedPrivateHistoryAsync(selected.Selection, different));
            stopping = stopHost ? fixture.Stop() : player.DisposeAsync().AsTask();
            Assert.False(stopping.IsCompleted);
            Assert.False(pending.IsCompleted);
        }
        finally { release.Set(); }
        Assert.NotNull(pending);
        Assert.NotNull(stopping);
        if (stopHost) await Assert.ThrowsAsync<AuthorityDispatchException>(() => pending);
        else Assert.Empty((await pending).Value.Entries);
        await stopping.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.True(player.PrivateHistoryInspection.Latest!.IsCompleted);
        if (!stopHost)
        {
            fixture.Authority.PrivateHistoryReadHoldsGate = null;
            var remaining = third.Player.ReadSelectedObservation();
            Assert.Empty((await third.Player.ReadSelectedPrivateHistoryAsync(remaining.Selection, new())).Value.Entries);
            Assert.True((await fixture.Host.VerifyRecordedHistoryAsync()).ReproducesHead);
        }
    }

    [Fact]
    public async Task Revocation_while_waiting_for_storage_refuses_without_a_host_fault()
    {
        await using var fixture = await ActorSelectionFixture.Open();
        var (grantee, _, _, _) = await fixture.Acquire();
        var player = grantee.Player;
        var selected = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task held = Task.Run(() => fixture.Authority.ReadForController(fixture.Owner, fixture.Delivery(fixture.Owner), _ =>
        {
            entered.SetResult();
            Assert.True(release.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            return fixture.Revoke().Revision;
        }), TestContext.Current.CancellationToken);
        Task<LocalSelectedQueryResult<LocalPrivateHistoryPage>>? pending = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            pending = player.ReadSelectedPrivateHistoryAsync(selected.Selection, new());
            Assert.False(pending.IsCompleted);
        }
        finally { release.Set(); await held; }
        Assert.NotNull(pending);
        await Assert.ThrowsAsync<AuthorityDispatchException>(() => pending);
        Assert.True((await fixture.Host.VerifyRecordedHistoryAsync()).ReproducesHead);
        await fixture.Tick();
        Assert.Equal(fixture.Grantee, player.ReadSelectedObservation().Selection.ActorId);
    }

    [Fact]
    public async Task Home_directory_remains_readable_while_permanent_owner_control_is_delegated()
    {
        await using var fixture = await ActorSelectionFixture.Open();
        await fixture.Acquire();
        await using var owner = await fixture.Host.ConnectPlayerAsync(fixture.Owner, fixture.Delivery(fixture.Owner));
        Assert.False(owner.ControlAvailable);
        var home = owner.ReadSelectedObservation();
        var result = await owner.ReadSelectedPrivateHistoryAsync(home.Selection, new());
        result.ValidateFor(home, LocalSelectedQueryOperation.ReadPrivateHistory);
        Assert.Empty(result.Value.Entries);
        Assert.Equal(fixture.Owner, result.Selection.ActorId);
        Assert.Equal(LocalWorldRunState.Paused, owner.ReadRunState());
    }
}
