using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;

namespace OpenFantasy.Hosting.Tests.Client;

public sealed class MaterialGiftSelectionTests(MaterialProposalWorldFixture world)
    : IClassFixture<MaterialProposalWorldFixture>
{
    [Fact]
    public void Selection_has_no_default_and_preparation_captures_only_explicit_positive_raw_terms()
    {
        using var live = world.Open();
        WorldSnapshot before = live.State;
        var view = new LocalWorldView(before, world.Player);
        var selection = new FarmGiftSelection();
        var factory = Factory(world, before);
        Assert.Null(selection.LotId);
        Assert.Null(selection.LotKind);
        Assert.Null(selection.RecipientActorId);
        Assert.Null(selection.SelectedOffer);
        Assert.Equal(1UL, selection.Quantity);
        Assert.False(selection.Prepare(view));
        Assert.All(factory.Create(view, giftSelection: selection).Where(pair =>
            MaterialProposalActionOffers.IsGiftAction(pair.Key)), pair => Assert.False(pair.Value.CanExecute));

        LocalMaterialProposalLot lot = view.MaterialProposals!.Preparation!.Lots.First(value =>
            value.Kind == ConsentedMaterialLotKind.CountedSource);
        Assert.True(selection.SelectLot(view, lot.Kind, lot.LotId));
        Assert.False(selection.Prepare(view));
        Assert.True(selection.SelectRecipient(view, world.Helper));
        Assert.False(selection.SetQuantity(0));
        Assert.Equal(1UL, selection.Quantity);
        Assert.True(selection.SetQuantity(lot.Quantity));
        Assert.True(selection.Prepare(view));
        ProposeMaterialIntent intent = selection.PreparedGift!;
        Assert.Equal(view.MaterialProposals.Preparation.Prepare(lot.Kind, lot.LotId, world.Helper, lot.Quantity), intent);
        Assert.True(view.MaterialProposals.Preparation.CanStillPropose(intent));
        Assert.Same(intent, factory.Create(view, giftSelection: selection)[FarmSemanticAction.ProposeMaterial].Intent);
        Assert.Contains(lot.Quantity.ToString(), factory.Create(view, giftSelection: selection)[FarmSemanticAction.ProposeMaterial].CostText);
        Assert.Equal(before.ContentHash, live.State.ContentHash);
        Assert.Empty(live.Book.Open);

        Assert.False(selection.SelectRecipient(view, world.Player));
        Assert.Null(selection.PreparedGift);
        Assert.False(selection.Prepare(view));
        Assert.Equal(world.Player, selection.RecipientActorId);
        Assert.True(selection.SelectRecipient(view, world.Helper));
        Assert.False(selection.SelectLot(view, ConsentedMaterialLotKind.Produced, world.Stock));
        Assert.False(selection.Prepare(view));
        Assert.Equal(world.Stock, selection.LotId);
        Assert.Equal(before.ContentHash, live.State.ContentHash);
    }

    [Fact]
    public void Captured_expiry_survives_real_steps_and_current_validity_does_not_renew_it()
    {
        using var live = world.Open();
        var view = new LocalWorldView(live.State, world.Player);
        FarmGiftSelection selection = Prepared(world, view);
        ProposeMaterialIntent original = selection.PreparedGift!;
        var shortLived = new ProposeMaterialIntent(original.Terms, live.State.AdvancingSteps + 3);
        Assert.True(view.MaterialProposals!.Preparation!.CanStillPropose(shortLived));
        live.Step();
        var next = new LocalWorldView(live.State, world.Player);
        Assert.True(next.MaterialProposals!.Preparation!.CanStillPropose(shortLived));
        Assert.True(next.MaterialProposals.Preparation.CanStillPropose(original));
        Assert.NotEqual(original.ExpiresAtAdvancingStep, next.MaterialProposals.Preparation.ExpiresAtAdvancingStep);
        Assert.Same(original, selection.PreparedGift);
        live.Step();
        var dueAtCreation = new LocalWorldView(live.State, world.Player);
        Assert.False(dueAtCreation.MaterialProposals!.Preparation!.CanStillPropose(shortLived));
        Assert.Equal(view.MaterialProposals.Preparation.ExpiresAtAdvancingStep, original.ExpiresAtAdvancingStep);
        Assert.Equal(1200UL, LocalMaterialProposalPreparation.OfferLifetimeSteps);
        Assert.Equal(WorldActionRejection.Unavailable, live.Submit(world.Player, shortLived));
        Assert.Empty(live.Book.Open);
    }

    [Fact]
    public void Current_validity_requires_the_complete_exact_public_stock_and_scope_pin()
    {
        using var live = world.Open();
        var view = new LocalWorldView(live.State, world.Player);
        var preparation = view.MaterialProposals!.Preparation!;
        ProposeMaterialIntent captured = Prepared(world, view).PreparedGift!;
        ConsentedMaterialTerms terms = captured.Terms;
        ProposeMaterialIntent Changed(EntityId? worldId = null, EntityId? recipient = null,
            EntityId? lotId = null, ulong? revision = null, ulong? quantity = null,
            OpenFantasy.Core.Custody.CountedItemDefinition? definition = null) => new(new(
                worldId ?? terms.WorldId, terms.BranchId, terms.SenderActorId, recipient ?? terms.RecipientActorId,
                terms.LotKind, lotId ?? terms.LotId, definition ?? terms.Definition,
                terms.SourceCustodianId, terms.DestinationCustodianId, quantity ?? terms.Quantity,
                revision ?? terms.ExpectedLotRevision), captured.ExpiresAtAdvancingStep);
        Assert.False(preparation.CanStillPropose(Changed(worldId: world.Stock)));
        Assert.False(preparation.CanStillPropose(Changed(recipient: world.Stock)));
        Assert.False(preparation.CanStillPropose(Changed(lotId: world.Stock)));
        Assert.False(preparation.CanStillPropose(Changed(revision: terms.ExpectedLotRevision + 1)));
        Assert.False(preparation.CanStillPropose(Changed(quantity: ulong.MaxValue)));
        Assert.False(preparation.CanStillPropose(Changed(definition: new(terms.Definition.DefinitionId,
            OpenFantasy.Core.Content.ContentHash.Compute([1, 2, 3])))));
        Assert.False(preparation.CanStillPropose(new(terms, preparation.ExpiresAtAdvancingStep + 1)));
        Assert.True(preparation.CanStillPropose(captured));
        Assert.Empty(live.Book.Open);
    }

    [Fact]
    public void Produced_selection_is_exact_and_spent_stock_stays_selected_without_fallback()
    {
        using var live = world.Open();
        TransformedLot stock = live.State.Production!.Lots.Single(value => value.LotId == world.Stock);
        var gift = live.Save(new(live.State.WorldId, live.State.BranchId, world.Helper, world.Player,
            ConsentedMaterialLotKind.Produced, stock.LotId, stock.Definition, world.HelperBag, world.PlayerBag, 1, stock.Revision));
        var incoming = live.Service.Find(gift.ProposalId, world.Player, world.DeliveryOf(live.State, world.Player))!;
        Assert.Equal(WorldActionRejection.None, live.Submit(world.Player, incoming.PrepareAcceptance()));
        EntityId child = EntityId.DeriveSpawned(gift.Command.CommandId, 0);
        var view = new LocalWorldView(live.State, world.Player);
        var selection = new FarmGiftSelection();
        Assert.True(selection.SelectLot(view, ConsentedMaterialLotKind.Produced, child));
        Assert.True(selection.SelectRecipient(view, world.Helper));
        Assert.True(selection.Prepare(view));
        ProposeMaterialIntent captured = selection.PreparedGift!;
        Assert.Equal((ConsentedMaterialLotKind.Produced, child, 1UL),
            (captured.Terms.LotKind, captured.Terms.LotId, captured.Terms.Quantity));

        var self = Assert.Single(view.Restorative!.SelfUseOffers).Intent!;
        var use = new UseRestorativeItemIntent(self.RecipientActorId, self.UseDefinitionId, self.UseDefinitionVersion,
            self.PermissionId, self.PermissionGrantVersion, self.ExpectedPermissionRevision,
            self.ExpectedPatientPresenceRevision, self.ExpectedPatientProgressionRevision, self.ExpectedStorageRevision,
            self.ExpectedProductionRevision, new(IngredientLotKind.Transformed, child, captured.Terms.ExpectedLotRevision, 1));
        var useCommand = live.Command(world.Player, use);
        Assert.Equal(WorldActionRejection.None, Assert.Single(live.Authority.Commit(
            world.Actions.Batch(live.State, WorldBatchKind.AdvancingStep, [useCommand])).Receipts).Rejection);
        var current = new LocalWorldView(live.State, world.Player);
        Assert.False(current.MaterialProposals!.Preparation!.CanStillPropose(captured));
        var offered = Factory(world, live.State).Create(current, giftSelection: selection)[FarmSemanticAction.ProposeMaterial];
        Assert.False(offered.CanExecute);
        Assert.Null(offered.Intent);
        Assert.Same(captured, selection.PreparedGift);
        Assert.False(selection.Prepare(current));
        Assert.Equal(child, selection.LotId);
        Assert.Equal(ConsentedMaterialLotKind.Produced, selection.LotKind);
        Assert.NotEmpty(current.MaterialProposals.Preparation.Lots);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Explicit_incoming_and_outgoing_rows_keep_terminal_results_inspectable(bool outgoing)
    {
        using var live = world.Open();
        var saved = live.Save(world.PinnedRawTerms(live.State, outgoing ? world.Player : world.Helper,
            outgoing ? world.Helper : world.Player));
        var before = live.State;
        var view = new LocalWorldView(before, world.Player);
        var selection = new FarmGiftSelection();
        Assert.Null(selection.SelectedOffer);
        Assert.True(selection.SelectOffer(view, saved.ProposalId));
        MaterialProposalView captured = selection.SelectedOffer!;
        var offers = Factory(world, before).Create(view, giftSelection: selection);
        Assert.Equal(!outgoing, offers[FarmSemanticAction.AcceptMaterial].CanExecute);
        Assert.Equal(!outgoing, offers[FarmSemanticAction.DeclineMaterial].CanExecute);
        Assert.Equal(outgoing, offers[FarmSemanticAction.WithdrawMaterial].CanExecute);
        if (!outgoing)
            Assert.Equal(captured.PrepareAcceptance(), offers[FarmSemanticAction.AcceptMaterial].Intent);
        FarmActionOffer close = offers[outgoing ? FarmSemanticAction.WithdrawMaterial : FarmSemanticAction.DeclineMaterial];
        Assert.Equal(WorldActionRejection.None, live.Submit(world.Player, close.Intent!));
        var current = new LocalWorldView(live.State, world.Player);
        var after = Factory(world, live.State).Create(current, giftSelection: selection);
        Assert.Same(captured, selection.SelectedOffer);
        Assert.False(after[close.Action].CanExecute);
        Assert.Contains(outgoing ? "Withdrawn" : "Declined", after[close.Action].DetailText);
        Assert.True(selection.SelectOffer(current, saved.ProposalId));
        Assert.Equal(outgoing ? MaterialProposalStatus.Withdrawn : MaterialProposalStatus.Declined,
            selection.SelectedOffer!.Status);
        Assert.Equal(before.Inventory.ContentHash, live.State.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, live.State.Production!.ContentHash);
        Assert.Equal(before.Care!.ContentHash, live.State.Care!.ContentHash);
        Assert.False(selection.SelectOffer(current, world.Stock));
        Assert.Null(selection.SelectedOffer);
    }

    internal static FarmGiftSelection Prepared(MaterialProposalWorldFixture world, LocalWorldView view)
    {
        var selection = new FarmGiftSelection();
        LocalMaterialProposalLot lot = view.MaterialProposals!.Preparation!.Lots.First();
        Assert.True(selection.SelectLot(view, lot.Kind, lot.LotId));
        Assert.True(selection.SelectRecipient(view, world.Helper));
        Assert.True(selection.Prepare(view));
        return selection;
    }

    internal static FarmActionOffers Factory(MaterialProposalWorldFixture world, WorldSnapshot state) =>
        new(world.DeliveryOf(state, world.Player), world.Helper,
            state.Gathering!.Nodes.Single(value => value.Definition.Kind == GatherNodeKind.Oak)
                .Definition.YieldDefinition.DefinitionId, null);
}
