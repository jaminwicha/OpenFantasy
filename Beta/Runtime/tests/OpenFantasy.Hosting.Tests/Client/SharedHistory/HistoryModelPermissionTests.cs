using OpenFantasy.Client;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Client.SelectedGifts;

namespace OpenFantasy.Hosting.Tests.Client.SharedHistory;

public sealed class HistoryModelPermissionTests
{
    [Theory]
    [InlineData(WorldControlCapabilities.PrivateHistory, true)]
    [InlineData(WorldControlCapabilities.Inventory, false)]
    [InlineData(WorldControlCapabilities.Trade, false)]
    public async Task Actual_history_only_selection_is_independent_of_inventory_and_trade(
        WorldControlCapabilities capabilities, bool permitted)
    {
        await using var fixture = await SelectedGiftClientFixture.Open(capabilities);
        var (grantee, _, _, _) = await fixture.Acquire();
        var selected = await grantee.Player.SelectActorAsync(grantee.Player.ReadSelectedObservation().Selection, fixture.Owner);
        var facade = new GatedHistoryFacade(new PortHistoryFacade(grantee.Player));
        var model = new FarmHistoryModel();
        model.Open(facade);
        if (permitted)
        {
            Assert.Null(selected.ControlledView!.Inventory);
            Assert.Null(selected.ControlledView.Trade);
            await HistoryModelHarness.Settle(model);
            Assert.Equal(FarmHistoryState.Empty, model.State);
            Assert.Equal(fixture.Owner, model.Selection!.ActorId);
            Assert.Equal(1, facade.PageCalls);
            Assert.NotNull(model.Page);
            fixture.Revoke();
            await fixture.Tick();
            model.Update();
            Assert.False(model.IsOpen);
            Assert.Null(model.Page);
            Assert.Null(model.Selection);
        }
        else
        {
            Assert.Equal(FarmHistoryState.Denied, model.State);
            Assert.Equal(0, facade.PageCalls);
            Assert.Null(model.Page);
            Assert.Null(model.Selection);
            Assert.False(model.CanRefresh);
        }
        await model.DrainAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Returning_to_the_same_actor_with_a_new_generation_discards_a_delayed_page_or_failure(bool failure)
    {
        await using var fixture = await SelectedGiftClientFixture.Open(WorldControlCapabilities.PrivateHistory);
        var (grantee, _, _, _) = await fixture.Acquire();
        var player = grantee.Player;
        var selected = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var gate = GatedHistoryFacade.Signal();
        var facade = new GatedHistoryFacade(new PortHistoryFacade(player)) { PageGate = gate };
        var model = new FarmHistoryModel();
        model.Open(facade);
        await facade.PageCaptured.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var home = await player.SelectActorAsync(selected.Selection, fixture.Grantee);
        var returned = await player.SelectActorAsync(home.Selection, fixture.Owner);
        Assert.Equal(selected.Selection.ActorId, returned.Selection.ActorId);
        Assert.NotEqual(selected.Selection.Generation, returned.Selection.Generation);
        model.Update();
        Assert.False(model.IsOpen);
        string? notice = model.Notice;
        if (failure) gate.SetException(new InvalidOperationException("The previous selection failed."));
        else gate.SetResult();
        await HistoryModelHarness.Settle(model);
        Assert.Null(model.Page);
        Assert.Null(model.Detail);
        Assert.Equal(notice, model.Notice);
        Assert.Equal(1, facade.PageCalls);
        await model.DrainAsync();
    }

    [Fact]
    public async Task Unsupported_session_is_distinct_from_an_empty_authorized_history()
    {
        var model = new FarmHistoryModel();
        model.Open(null);
        Assert.True(model.IsOpen);
        Assert.Equal(FarmHistoryState.Unsupported, model.State);
        Assert.Null(model.Page);
        Assert.False(model.IsBusy);
        Assert.False(model.CanRefresh);
        Assert.False(model.Next(model.Capture));
        await model.DrainAsync();
    }
}
