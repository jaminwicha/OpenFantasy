using OpenFantasy.Client;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.Client.SelectedProduction;

namespace OpenFantasy.Hosting.Tests.Client.RestorativeChoices;

public sealed class SelectedCareChoiceTests
{
    [Theory]
    [InlineData(WorldControlCapabilities.Recovery, true, false)]
    [InlineData(WorldControlCapabilities.CareDisposition, false, true)]
    [InlineData(WorldControlCapabilities.Inventory, false, false)]
    public async Task Recovery_and_permission_disclosure_remain_independent(WorldControlCapabilities capability,
        bool recovery, bool disposition)
    {
        await using var fixture = await SelectedProductionClientFixture.Open(capability);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        var observed = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var presentation = FarmCarePresentation.From(observed);
        Assert.Equal(recovery, presentation.HasRecovery);
        Assert.Equal(disposition, presentation.HasCareDisposition);
        Assert.Equal(recovery, presentation.Remedies.Count > 0);
        Assert.Equal(disposition, presentation.Permissions.Count > 0);
        if (capability != WorldControlCapabilities.Inventory) Assert.Null(observed.ControlledView!.Inventory);
        var client = SelectedProductionBindingTests.Client(fixture, player);
        var library = new FarmCareLibraryModel();
        client.RefreshCare(library, observed);
        Assert.Equal(recovery, library.Entries.Any(entry => entry.Key.Kind == FarmCareChoiceKind.UseRemedy));
        Assert.Equal(disposition, library.Entries.Any(entry => entry.Key.Kind == FarmCareChoiceKind.WithdrawPermission));
        var offers = client.CreateOffers(observed);
        Assert.Equal(disposition, offers.ContainsKey(FarmSemanticAction.RevokeCarePermission));
        Assert.Equal(recovery || disposition, offers.ContainsKey(FarmSemanticAction.OpenCare));
        Assert.Equal(fixture.Owner, library.Scope!.ActorId);
        Assert.Equal(observed.Selection, library.Scope.Selection);
    }

    [Fact]
    public async Task Exact_selected_permission_executes_without_recovery_or_inventory_capabilities()
    {
        await using var fixture = await SelectedProductionClientFixture.Open(WorldControlCapabilities.CareDisposition);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        var observed = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        Assert.Null(observed.ControlledView!.Recovery);
        var client = SelectedProductionBindingTests.Client(fixture, player);
        var library = new FarmCareLibraryModel();
        client.RefreshCare(library, observed);
        var entry = library.Entries.First(value => value.Offer.CanExecute);
        Assert.Equal(FarmCareChoiceKind.WithdrawPermission, entry.Key.Kind);
        library.Select(entry.Key);
        Assert.True(library.Prepare(), library.Notice);
        var prepared = library.PreparedOffer!;
        client.RefreshCare(library, observed);
        Assert.Same(prepared, library.PreparedOffer);
        var before = fixture.State;
        player.ResumeSelected(observed);
        var pending = client.SubmitOffer(prepared);
        await fixture.Tick();
        var receipt = await pending;
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
        Assert.False(fixture.State.Care!.Ordinary!.Permissions.Single(value => value.PermissionId == entry.Key.Id).IsActive);
        Assert.Equal(before.Inventory.ContentHash, fixture.State.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, fixture.State.Production!.ContentHash);
        Assert.True((await fixture.Host.VerifyRecordedHistoryAsync()).ReproducesHead);
    }

    [Fact]
    public async Task Same_actor_reselected_in_a_new_generation_discards_selection_review_and_old_submission()
    {
        await using var fixture = await SelectedProductionClientFixture.Open(WorldControlCapabilities.CareDisposition);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        var first = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var client = SelectedProductionBindingTests.Client(fixture, player);
        var library = new FarmCareLibraryModel();
        client.RefreshCare(library, first);
        library.Select(library.Entries.First(entry => entry.Offer.CanExecute).Key);
        Assert.True(library.Prepare());
        var old = library.PreparedOffer!;
        var home = await player.SelectActorAsync(first.Selection, fixture.Grantee);
        var second = await player.SelectActorAsync(home.Selection, fixture.Owner);
        Assert.NotEqual(first.Selection.Generation, second.Selection.Generation);
        client.RefreshCare(library, second);
        Assert.Null(library.SelectedId);
        Assert.Null(library.PreparedOffer);
        Assert.Throws<InvalidOperationException>(() => { _ = client.SubmitOffer(old); });
    }

    [Fact]
    public async Task Presence_revision_and_cross_actor_scope_cannot_silently_retarget_review()
    {
        await using var fixture = await SelectedProductionClientFixture.Open(WorldControlCapabilities.CareDisposition);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        var observation = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var presentation = FarmCarePresentation.From(observation);
        var scope = FarmActionScope.From(fixture.Delivery(fixture.Grantee), observation);
        var library = new FarmCareLibraryModel();
        library.Refresh(presentation, scope);
        library.Select(library.Entries.First(entry => entry.Offer.CanExecute).Key);
        Assert.True(library.Prepare());
        library.Refresh(presentation, scope with { ExpectedActorRevision = scope.ExpectedActorRevision + 1 });
        Assert.Null(library.PreparedOffer);
        Assert.NotNull(library.SelectedId);
        Assert.Throws<ArgumentException>(() => library.Refresh(presentation, scope with { ActorId = fixture.Grantee }));
        Assert.Throws<ArgumentException>(() => library.Refresh(presentation, scope with
        { Selection = scope.Selection! with { Generation = scope.Selection!.Generation + 1 } }));
    }
}
