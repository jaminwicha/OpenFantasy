using OpenFantasy.Core.Care;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Care;
using static OpenFantasy.Hosting.Tests.Simulation.SessionFixture;

namespace OpenFantasy.Hosting.Tests.Simulation;

public sealed class ScheduledMaterialPreparationTests(MaterialProposalWorldFixture world)
    : IClassFixture<MaterialProposalWorldFixture>
{
    [Fact]
    public async Task An_explicit_own_lot_and_recipient_produce_exact_terms_and_only_submission_saves_the_offer()
    {
        using var live = world.Open();
        var before = live.State;
        var clock = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(live.Authority, world.Player,
            world.DeliveryOf(before, world.Player), clock);
        var draft = session.ReadView().MaterialProposals!.Preparation!;
        var lot = draft.Lots.First(value => value.Kind == ConsentedMaterialLotKind.CountedSource);
        var intent = draft.Prepare(lot.Kind, lot.LotId, world.Helper, 1)!;
        var actual = before.Inventory.Custody.Lots.Single(value => value.LotId == lot.LotId);
        Assert.Equal(new ConsentedMaterialTerms(before.WorldId, before.BranchId, world.Player, world.Helper,
            lot.Kind, lot.LotId, actual.Definition, world.PlayerBag, world.HelperBag, 1, actual.Revision), intent.Terms);
        Assert.Equal(before.AdvancingSteps + 1 + LocalMaterialProposalPreparation.OfferLifetimeSteps, intent.ExpiresAtAdvancingStep);
        Assert.DoesNotContain(world.Player, draft.RecipientActorIds);
        Assert.DoesNotContain(draft.Lots, value => value.LotId == world.Stock);
        Assert.Null(draft.Prepare(ConsentedMaterialLotKind.Produced, world.Stock, world.Helper, 1));
        Assert.Null(draft.Prepare(lot.Kind, lot.LotId, world.Player, 1));
        Assert.Null(draft.Prepare(lot.Kind, lot.LotId, world.Helper, 0));
        Assert.Null(draft.Prepare(lot.Kind, lot.LotId, world.Helper, lot.Quantity + 1));
        await Tick(clock, TimeSpan.FromHours(1));
        Assert.Equal(before.ContentHash, live.State.ContentHash);
        session.SetReady(true);
        var command = live.Command(world.Player, intent);
        Assert.Equal(WorldActionRejection.None, (await ScheduledMaterialProposalSessionTests.Submit(session, clock, command)).Rejection);
        var saved = live.Book.FindOpen(WorldMaterialProposal.DeriveIdentity(command.CommandId))!;
        Assert.Equal(intent.Terms, saved.Terms);
        Assert.Equal(intent.ExpiresAtAdvancingStep, saved.ExpiresAtAdvancingStep);
        Assert.Equal(actual, live.State.Inventory.Custody.Lots.Single(value => value.LotId == lot.LotId));
        Assert.Equal(intent.ExpiresAtAdvancingStep, draft.ExpiresAtAdvancingStep);
    }

    [Fact]
    public async Task A_transferred_produced_lot_is_selectable_and_a_spent_captured_pin_is_not_retargeted()
    {
        using var live = world.Open();
        var stock = live.State.Production!.Lots.Single(value => value.LotId == world.Stock);
        var offer = live.Save(new(live.State.WorldId, live.State.BranchId, world.Helper, world.Player,
            ConsentedMaterialLotKind.Produced, stock.LotId, stock.Definition, world.HelperBag, world.PlayerBag, 1, stock.Revision));
        Assert.Equal(WorldActionRejection.None, live.Submit(world.Player, live.Service.Find(offer.ProposalId, world.Player,
            world.DeliveryOf(live.State, world.Player))!.PrepareAcceptance()));
        var child = OpenFantasy.Core.EntityId.DeriveSpawned(offer.Command.CommandId, 0);
        var clock = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(live.Authority, world.Player,
            world.DeliveryOf(live.State, world.Player), clock);
        var draft = session.ReadView().MaterialProposals!.Preparation!;
        var exact = Assert.Single(draft.Lots, value => value.LotId == child);
        Assert.Equal(ConsentedMaterialLotKind.Produced, exact.Kind);
        var captured = draft.Prepare(exact.Kind, child, world.Helper, 1)!;
        var self = Assert.Single(session.ReadView().Restorative!.SelfUseOffers).Intent!;
        var use = new UseRestorativeItemIntent(self.RecipientActorId, self.UseDefinitionId, self.UseDefinitionVersion,
            self.PermissionId, self.PermissionGrantVersion, self.ExpectedPermissionRevision,
            self.ExpectedPatientPresenceRevision, self.ExpectedPatientProgressionRevision, self.ExpectedStorageRevision,
            self.ExpectedProductionRevision, new(OpenFantasy.Core.Production.IngredientLotKind.Transformed, child, exact.Revision, 1));
        session.SetReady(true);
        Assert.Equal(WorldActionRejection.None, (await ScheduledMaterialProposalSessionTests.Submit(session, clock,
            live.Command(world.Player, use))).Rejection);
        Assert.DoesNotContain(session.ReadView().MaterialProposals!.Preparation!.Lots, value => value.LotId == child);
        Assert.Null(session.ReadView().MaterialProposals!.Preparation!.Prepare(exact.Kind, child, world.Helper, 1));
        var stale = live.Command(world.Player, captured);
        var receipt = await ScheduledMaterialProposalSessionTests.Submit(session, clock, stale);
        Assert.Equal(WorldActionRejection.Unavailable, receipt.Rejection);
        Assert.Null(live.Book.FindOpen(WorldMaterialProposal.DeriveIdentity(stale.CommandId)));
        Assert.Equal(child, captured.Terms.LotId);
        Assert.Equal(draft.ExpiresAtAdvancingStep, captured.ExpiresAtAdvancingStep);
    }

    [Fact]
    public async Task A_reading_hold_keeps_existing_offers_inspectable_but_hides_creation_choices()
    {
        using var live = world.Open();
        var saved = live.Save(world.PinnedRawTerms(live.State, world.Helper, world.Player));
        var clock = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(live.Authority, world.Player,
            world.DeliveryOf(live.State, world.Player), clock);
        session.SetReady(true);
        var speak = live.Command(world.Player, new SpeakToActorIntent(world.Helper,
            live.State.Space.GetActor(world.Helper).Revision));
        Assert.Equal(WorldActionRejection.None, (await ScheduledMaterialProposalSessionTests.Submit(session, clock, speak)).Rejection);
        Assert.True(live.State.HasReadingHold);
        Assert.Null(session.ReadView().MaterialProposals!.Preparation);
        var before = live.State.ContentHash;
        Assert.Equal(saved.ProposalId, (await session.InspectMaterialProposalAsync(saved.ProposalId))!.ProposalId);
        await Tick(clock);
        Assert.Equal(before, live.State.ContentHash);
    }
}
