using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;

namespace OpenFantasy.Hosting.Tests.Client.SelectedGifts;

public sealed class SelectedGiftBindingTests
{
    [Fact]
    public async Task Inventory_only_uses_the_operated_pack_and_exact_delivery_without_combat_or_private_history()
    {
        await using var fixture = await SelectedGiftClientFixture.Open(WorldControlCapabilities.Inventory);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        var observed = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var presentation = FarmInventoryPresentation.From(observed);
        var before = fixture.State;
        Assert.Null(observed.ControlledView!.Combat);
        Assert.Null(observed.ControlledView.Recovery);
        Assert.Null(observed.ControlledView.Production);
        Assert.Null(presentation.ContextBlocker);
        Assert.False(presentation.CanReadHistory);
        Assert.Equal(fixture.Owner, presentation.ActorId);
        Assert.Equal(fixture.Grantee, presentation.Selection!.HomeActorId);
        Assert.Equal(before.GetResident(fixture.Owner).InventoryCustodianId, presentation.Inventory.CustodianId);
        Assert.NotEqual(observed.HomeView.PlayerInventory.CustodianId, presentation.Inventory.CustodianId);
        Assert.Same(observed.ControlledView.Inventory, presentation.Inventory);

        var gifts = Prepare(presentation, fixture.Third);
        var client = Client(fixture, player);
        var offers = client.CreateOffers(observed, gifts: gifts);
        Assert.Equal(4, offers.Count);
        var offer = offers[FarmSemanticAction.ProposeMaterial];
        Assert.True(offer.CanExecute, offer.BlockingReason);
        var command = offer.ToCommand(new(0xdada7100, 1));
        var intent = Assert.IsType<ProposeMaterialIntent>(command.Intent);
        Assert.Same(gifts.PreparedGift, intent);
        Assert.Equal(fixture.Owner, command.ActorId);
        Assert.Equal(fixture.Delivery(fixture.Grantee).PrincipalId, command.PrincipalId);
        Assert.Equal(fixture.Delivery(fixture.Grantee).SessionId, command.OriginSessionId);
        Assert.Equal(acquired.Witness, command.ControlWitness);
        Assert.Equal(presentation.Inventory.CustodianId, intent.Terms.SourceCustodianId);
        Assert.Equal(before.GetResident(fixture.Third).InventoryCustodianId, intent.Terms.DestinationCustodianId);
        Assert.Same(before, fixture.State);
        Assert.False(fixture.Authority.IsIdentityOccupiedForHost(command.CommandId));
    }

    [Theory]
    [InlineData(WorldControlCapabilities.Inventory, false)]
    [InlineData(WorldControlCapabilities.Inventory | WorldControlCapabilities.PrivateHistory, true)]
    public async Task History_permission_is_independent_of_current_inventory(WorldControlCapabilities scopes, bool history)
    {
        await using var fixture = await SelectedGiftClientFixture.Open(scopes);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        var observed = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        Assert.Equal(history, FarmInventoryPresentation.From(observed).CanReadHistory);
        Assert.NotNull(observed.ControlledView!.MaterialProposals);
    }

    [Fact]
    public async Task Withheld_inventory_is_not_replaced_with_the_home_pack_or_gift_actions()
    {
        await using var fixture = await SelectedGiftClientFixture.Open(WorldControlCapabilities.PrivateHistory);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        var observed = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        Assert.Null(observed.ControlledView!.Inventory);
        Assert.Null(observed.ControlledView.MaterialProposals);
        Assert.Throws<InvalidOperationException>(() => FarmInventoryPresentation.From(observed));
        Assert.Empty(Client(fixture, player).CreateOffers(observed));
    }

    [Fact]
    public async Task Actor_cycle_discards_draft_and_emitted_offer_even_when_stock_and_actor_are_unchanged()
    {
        await using var fixture = await SelectedGiftClientFixture.Open(WorldControlCapabilities.Inventory);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        var original = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var presentation = FarmInventoryPresentation.From(original);
        var gifts = Prepare(presentation, fixture.Third);
        var client = Client(fixture, player);
        var offer = client.CreateOffers(original, gifts: gifts)[FarmSemanticAction.ProposeMaterial];
        var captured = gifts.PreparedGift;
        var before = fixture.State;
        gifts.ObserveScope(FarmInventoryPresentation.From(player.ReadSelectedObservation()));
        Assert.Same(captured, gifts.PreparedGift);
        await player.SelectActorAsync(original.Selection, fixture.Grantee);
        var returned = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var current = FarmInventoryPresentation.From(returned);
        Assert.False(gifts.MatchesScope(current));
        Assert.False(client.CreateOffers(returned, gifts: gifts)[FarmSemanticAction.ProposeMaterial].CanExecute);
        Assert.Throws<InvalidOperationException>(() => { _ = client.SubmitOffer(offer); });
        gifts.ObserveScope(current);
        Assert.Null(gifts.PreparedGift);
        Assert.Null(gifts.LotId);
        Assert.Null(gifts.RecipientActorId);
        Assert.Null(gifts.SelectedOffer);
        Assert.False(gifts.Prepare(current));
        Assert.Same(before, fixture.State);
        Assert.Equal(original.Selection.Generation + 2, returned.Selection.Generation);
    }

    [Fact]
    public async Task Actual_creation_and_withdrawal_keep_selected_scope_and_do_not_move_stock()
    {
        await using var fixture = await SelectedGiftClientFixture.Open(WorldControlCapabilities.Inventory);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        var original = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var client = Client(fixture, player);
        var gifts = Prepare(FarmInventoryPresentation.From(original), fixture.Third);
        var before = fixture.State;
        var creation = client.CreateOffers(original, gifts: gifts)[FarmSemanticAction.ProposeMaterial];
        player.ResumeSelected(original);
        var pending = client.SubmitOffer(creation);
        await SessionFixture.Until(() => player.Pending is not null);
        await fixture.Tick();
        var receipt = await pending;
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
        Assert.Same(pending, client.SubmitOffer(creation));
        player.PauseSelected(player.ReadSelectedObservation().Selection);
        var current = player.ReadSelectedObservation();
        var saved = Assert.Single(current.ControlledView!.MaterialProposals!.Offers);
        Assert.True(saved.ViewerIsSender);
        Assert.Equal(gifts.PreparedGift!.Terms, saved.Terms);
        Assert.True(gifts.SelectOffer(FarmInventoryPresentation.From(current), saved.ProposalId));
        var withdrawal = client.CreateOffers(current, gifts: gifts)[FarmSemanticAction.WithdrawMaterial];
        Assert.True(withdrawal.CanExecute, withdrawal.BlockingReason);
        Assert.False(client.CreateOffers(current, gifts: gifts)[FarmSemanticAction.AcceptMaterial].CanExecute);
        player.ResumeSelected(current);
        var terminal = client.SubmitOffer(withdrawal);
        await SessionFixture.Until(() => player.Pending is not null);
        await fixture.Tick();
        Assert.Equal(WorldActionRejection.None, (await terminal).Rejection);
        player.PauseSelected(player.ReadSelectedObservation().Selection);
        Assert.Empty(player.ReadSelectedObservation().ControlledView!.MaterialProposals!.Offers);
        Assert.Equal(before.Inventory.ContentHash, fixture.State.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, fixture.State.Production!.ContentHash);
        Assert.Equal(before.Care!.ContentHash, fixture.State.Care!.ContentHash);
        Assert.True((await fixture.Host.VerifyRecordedHistoryAsync()).ReproducesHead);
    }

    [Fact]
    public async Task Revocation_invalidates_review_and_returns_a_paused_home_selection()
    {
        await using var fixture = await SelectedGiftClientFixture.Open(WorldControlCapabilities.Inventory);
        var acquired = await fixture.Acquire();
        var player = acquired.Grantee.Player;
        var selected = await player.SelectActorAsync(player.ReadSelectedObservation().Selection, fixture.Owner);
        var client = Client(fixture, player);
        var gifts = Prepare(FarmInventoryPresentation.From(selected), fixture.Third);
        var review = client.CreateOffers(selected, gifts: gifts)[FarmSemanticAction.ProposeMaterial];
        fixture.Revoke();
        await fixture.Tick();
        var current = player.ReadSelectedObservation();
        Assert.Equal(fixture.Grantee, current.Selection.ActorId);
        Assert.False(current.IsReady);
        Assert.Throws<InvalidOperationException>(() => { _ = client.SubmitOffer(review); });
        gifts.ObserveScope(FarmInventoryPresentation.From(current));
        Assert.Null(gifts.PreparedGift);
        Assert.Empty(fixture.State.Coordination!.Proposals!.Open);
    }

    private static FarmGiftSelection Prepare(FarmInventoryPresentation view, EntityId recipient)
    {
        var gifts = new FarmGiftSelection();
        var lot = view.MaterialProposals!.Preparation!.Lots.First();
        Assert.True(gifts.SelectLot(view, lot.Kind, lot.LotId));
        Assert.True(gifts.SelectRecipient(view, recipient));
        Assert.True(gifts.Prepare(view));
        return gifts;
    }

    private static FarmClientSession Client(SelectedGiftClientFixture fixture, LocalPlayerConnection player) =>
        new(new SelectedGiftPort(player), fixture.Delivery(fixture.Grantee), fixture.Third, new(0xdada7101, 1), null);
}
