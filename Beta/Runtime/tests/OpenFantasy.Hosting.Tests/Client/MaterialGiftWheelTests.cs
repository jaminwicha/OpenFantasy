using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;
using static OpenFantasy.Hosting.Tests.Client.MaterialGiftSelectionTests;

namespace OpenFantasy.Hosting.Tests.Client;

public sealed class MaterialGiftWheelTests(MaterialProposalWorldFixture world)
    : IClassFixture<MaterialProposalWorldFixture>
{
    [Fact]
    public void Gift_page_has_stable_sectors_and_refresh_keeps_captured_creation_expiry()
    {
        using var live = world.Open();
        var view = new LocalWorldView(live.State, world.Player);
        FarmGiftSelection selection = Prepared(world, view);
        var factory = Factory(world, live.State);
        var wheel = new FarmQuickWheelModel(factory.Create(view, giftSelection: selection));
        Assert.Equal(3, wheel.PageCount);
        Assert.Equal("Actions", wheel.PageName);
        Assert.Equal("Care", wheel.NextPageName);
        wheel.NextPage();
        Assert.Equal("Gifts", wheel.NextPageName);
        wheel.NextPage();
        Assert.Equal("Gifts", wheel.PageName);
        Assert.Equal("Actions", wheel.NextPageName);
        Assert.Null(wheel.Selected);
        Assert.Equal(new[] { FarmSemanticAction.ProposeMaterial, FarmSemanticAction.AcceptMaterial,
            FarmSemanticAction.DeclineMaterial, FarmSemanticAction.WithdrawMaterial }, wheel.Slots.Select(slot => slot!.Action));
        wheel.Select(0);
        ProposeMaterialIntent captured = Assert.IsType<ProposeMaterialIntent>(wheel.Selected!.Offer.Intent);
        live.Step();
        var current = new LocalWorldView(live.State, world.Player);
        wheel.Refresh(target => factory.Create(current, target, selection));
        Assert.True(wheel.Selected.Offer.CanExecute);
        Assert.Same(captured, wheel.Selected.Offer.Intent);
        Assert.Same(captured, selection.PreparedGift);
        Assert.True(selection.Prepare(current)); // An explicit new preparation is a new choice.
        Assert.NotEqual(captured.ExpiresAtAdvancingStep, selection.PreparedGift!.ExpiresAtAdvancingStep);
        wheel.Refresh(target => factory.Create(current, target, selection));
        Assert.False(wheel.Selected.Offer.CanExecute);
        Assert.Null(wheel.Selected.Offer.Intent);
        Assert.Equal(captured.ExpiresAtAdvancingStep,
            Assert.IsType<ProposeMaterialIntent>(factory.Create(view, giftSelection: Prepared(world, view))
                [FarmSemanticAction.ProposeMaterial].Intent).ExpiresAtAdvancingStep);
        Assert.Empty(live.Book.Open);
    }

    [Fact]
    public void Incoming_acceptance_keeps_sender_observation_but_decline_remains_valid_after_sender_moves()
    {
        using var live = world.Open();
        var saved = live.Save(world.PinnedRawTerms(live.State, world.Helper, world.Player));
        var selection = new FarmGiftSelection();
        var view = new LocalWorldView(live.State, world.Player);
        Assert.True(selection.SelectOffer(view, saved.ProposalId));
        var factory = Factory(world, live.State);
        var wheel = GiftWheel(factory.Create(view, giftSelection: selection));
        wheel.Select(1);
        AcceptMaterialProposalIntent captured = Assert.IsType<AcceptMaterialProposalIntent>(wheel.Selected!.Offer.Intent);
        MoveOneStep(live, world.Helper);
        var current = new LocalWorldView(live.State, world.Player);
        Assert.NotEqual(captured.ExpectedSenderRevision,
            current.Actors.Single(actor => actor.ActorId == world.Helper).PresenceRevision);
        var offers = factory.Create(current, giftSelection: selection);
        Assert.False(offers[FarmSemanticAction.AcceptMaterial].CanExecute);
        Assert.True(offers[FarmSemanticAction.DeclineMaterial].CanExecute);
        Assert.Equal(saved.ProposalId, Assert.IsType<DeclineMaterialProposalIntent>(offers[FarmSemanticAction.DeclineMaterial].Intent).ProposalId);
        Assert.Equal(captured, selection.SelectedOffer!.PrepareAcceptance());
        Assert.True(selection.SelectOffer(current, saved.ProposalId));
        Assert.True(factory.Create(current, giftSelection: selection)[FarmSemanticAction.AcceptMaterial].CanExecute);
        wheel.Refresh(target => factory.Create(current, target, selection));
        Assert.False(wheel.Selected.Offer.CanExecute);
        Assert.Null(wheel.Selected.Offer.Intent);
    }

    [Fact]
    public void Actor_revision_or_explicit_different_offer_cannot_rebind_an_open_gift_slot()
    {
        using var live = world.Open();
        var first = live.Save(world.PinnedRawTerms(live.State, world.Player, world.Helper));
        var second = live.Save(world.PinnedRawTerms(live.State, world.Player, world.Helper));
        var view = new LocalWorldView(live.State, world.Player);
        var selection = new FarmGiftSelection();
        Assert.True(selection.SelectOffer(view, first.ProposalId));
        var factory = Factory(world, live.State);
        var wheel = GiftWheel(factory.Create(view, giftSelection: selection));
        wheel.Select(3);
        Assert.True(selection.SelectOffer(view, second.ProposalId));
        wheel.Refresh(target => factory.Create(view, target, selection));
        Assert.False(wheel.Selected!.Offer.CanExecute);
        Assert.Null(wheel.Selected.Offer.Intent);

        wheel = GiftWheel(factory.Create(view, giftSelection: selection));
        wheel.Select(3);
        FarmActionOffer captured = wheel.Selected!.Offer;
        MoveOneStep(live, world.Player);
        var current = new LocalWorldView(live.State, world.Player);
        Assert.True(factory.Create(current, giftSelection: selection)[FarmSemanticAction.WithdrawMaterial].CanExecute);
        wheel.Refresh(target => factory.Create(current, target, selection));
        Assert.False(wheel.Selected.Offer.CanExecute);
        Assert.NotEqual(captured.Scope.ExpectedActorRevision,
            current.Actors.Single(actor => actor.ActorId == world.Player).PresenceRevision);
    }

    [Fact]
    public void Reading_hold_blocks_known_gifts_and_inventory_inspection_cannot_dispatch_them()
    {
        using var live = world.Open();
        var saved = live.Save(world.PinnedRawTerms(live.State, world.Helper, world.Player));
        var view = new LocalWorldView(live.State, world.Player);
        FarmGiftSelection selection = Prepared(world, view);
        Assert.True(selection.SelectOffer(view, saved.ProposalId));
        var speak = live.Command(world.Player, new SpeakToActorIntent(world.Helper,
            live.State.Space.GetActor(world.Helper).Revision));
        Assert.Equal(WorldActionRejection.None, Assert.Single(live.Authority.Commit(
            world.Actions.Batch(live.State, WorldBatchKind.AdvancingStep, [speak])).Receipts).Rejection);
        var held = new LocalWorldView(live.State, world.Player);
        Assert.True(held.HasReadingHold);
        var gifts = Factory(world, live.State).Create(held, giftSelection: selection)
            .Where(pair => MaterialProposalActionOffers.IsGiftAction(pair.Key)).ToArray();
        Assert.Equal(4, gifts.Length);
        Assert.All(gifts, pair =>
        {
            Assert.False(pair.Value.CanExecute);
            Assert.Null(pair.Value.Intent);
            Assert.Contains("conversation", pair.Value.BlockingReason);
        });
        var router = new FarmInputRouter();
        var ordinary = new FarmInputSituation(true, false, false);
        foreach (FarmSemanticAction action in gifts.Select(pair => pair.Key))
        {
            Assert.True(router.Permits(ordinary, action));
            Assert.False(router.Permits(ordinary with { ReadingHold = true }, action));
            Assert.False(router.Permits(ordinary with { IsInEncounter = true }, action));
            Assert.False(router.Permits(ordinary with { ActionPending = true }, action));
        }
        router.SetInventoryOpen(true);
        Assert.All(gifts, pair => Assert.False(router.Permits(ordinary, pair.Key)));
        router.SetInventoryOpen(false);
        router.TogglePause();
        Assert.All(gifts, pair => Assert.False(router.Permits(ordinary, pair.Key)));
    }

    [Fact]
    public void Older_restorative_profile_omits_gift_actions_and_page()
    {
        var ordinary = new OrdinaryCareWorldFixture();
        var view = new LocalWorldView(ordinary.Genesis, ordinary.Player);
        var control = ordinary.Genesis.Inventory.Controls.Single(value => value.ActorId == ordinary.Player);
        var factory = new FarmActionOffers(new(control.PrincipalId, control.SessionId), ordinary.Helper,
            ordinary.Genesis.Gathering!.Nodes.Single(value => value.Definition.Kind == GatherNodeKind.Oak)
                .Definition.YieldDefinition.DefinitionId, null);
        var offers = factory.Create(view, giftSelection: new FarmGiftSelection());
        Assert.DoesNotContain(offers.Keys, MaterialProposalActionOffers.IsGiftAction);
        var wheel = new FarmQuickWheelModel(offers);
        Assert.Equal(2, wheel.PageCount);
        wheel.NextPage();
        Assert.Equal("Care", wheel.PageName);
        Assert.Equal("Actions", wheel.NextPageName);
    }

    private static FarmQuickWheelModel GiftWheel(IReadOnlyDictionary<FarmSemanticAction, FarmActionOffer> offers)
    {
        var wheel = new FarmQuickWheelModel(offers);
        for (int page = 0; page < wheel.PageCount && wheel.PageName != "Gifts"; page++) wheel.NextPage();
        Assert.Equal("Gifts", wheel.PageName);
        return wheel;
    }

    private void MoveOneStep(MaterialProposalWorldFixture.LiveWorld live, EntityId actor)
    {
        ulong revision = live.State.Space.GetActor(actor).Revision;
        foreach ((long x, long y) in new (long, long)[] { (32, 0), (-32, 0), (0, 32), (0, -32) })
        {
            WorldSnapshot state = live.State;
            live.Authority.Commit(new WorldStepBatch(world.NextCause(state), state.WorldId, state.BranchId,
                state.Revision, state.AdvancingSteps, WorldBatchKind.AdvancingStep,
                [new WorldMovementInput(world.DeliveryOf(state, actor), actor, x, y)], [], state.ProfileVersion));
            if (live.State.Space.GetActor(actor).Revision != revision) return;
        }
        Assert.Fail("The fixture actor must be able to take one real ground step in a cardinal direction.");
    }
}
