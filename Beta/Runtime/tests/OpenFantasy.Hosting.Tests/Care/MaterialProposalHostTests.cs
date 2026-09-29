using System.Text.Json;
using Microsoft.Data.Sqlite;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Care;

/// <summary>
/// The saved offer lifecycle on the real SQLite owner: separate authenticated submissions, real
/// intervening commits, disposal and recovery. Local trusted deliveries are not a network handshake.
/// </summary>
public sealed class MaterialProposalHostTests(MaterialProposalWorldFixture world) : IClassFixture<MaterialProposalWorldFixture>
{
    private const string StreamRefusal = "The delivery cannot access this actor command stream.";
    private const string QueryRefusal = "The delivery cannot access this actor's proposals.";
    private const string ChangedContent = "The command identity already names different content.";
    private const string StaleDispatch = "The selected batch cannot be committed in the current world.";

    private static byte[] Bytes(WorldActionReceipt receipt) => WorldReceiptCodecV1.Encode(receipt);

    [Fact]
    public void A_raw_split_offer_is_saved_inspected_and_accepted_by_separate_authenticated_submissions()
    {
        using var live = world.Open();
        Assert.Equal(30, live.State.ProfileVersion);
        ConsentedMaterialTerms terms = world.SelectRawTerms(live.Authority);
        WorldSnapshot before = live.State;
        WorldActionCommand create = world.Actions.Command(before, terms.SenderActorId,
            new ProposeMaterialIntent(terms, before.AdvancingSteps + 32));
        TrustedCommandDelivery sender = world.Actions.Delivery(create);
        WorldActionReceipt created = live.Service.Submit(create, sender);
        Assert.Equal(WorldActionRejection.None, created.Rejection);
        byte[] creationReceipt = Bytes(created);
        EntityId proposalId = WorldMaterialProposal.DeriveIdentity(create.CommandId);
        // Saving reserves and moves nothing; it is one ordinary elapsed step.
        WorldSnapshot saved = live.State;
        Assert.Equal(before.Inventory.ContentHash, saved.Inventory.ContentHash);
        Assert.Equal(before.Production!.ContentHash, saved.Production!.ContentHash);
        Assert.Equal((before.Revision + 1, before.AdvancingSteps + 1), (saved.Revision, saved.AdvancingSteps));
        Assert.Null(saved.Inventory.Custody.GetLot(terms.LotId).ReservedBy);

        live.Step();
        live.Reopen();
        WorldSnapshot state = live.State;
        TrustedCommandDelivery recipient = world.DeliveryOf(state, terms.RecipientActorId);
        MaterialProposalView view = Assert.IsType<MaterialProposalView>(
            live.Service.Find(proposalId, terms.RecipientActorId, recipient));
        Assert.Equal((MaterialProposalStatus.Open, terms, false, before.AdvancingSteps + 32),
            (view.Status, view.Terms, view.ViewerIsSender, view.ExpiresAtAdvancingStep));
        WorldActionCommand accept = world.Actions.Command(state, terms.RecipientActorId, view.PrepareAcceptance());
        WorldActionReceipt accepted = live.Service.Submit(accept, recipient);
        Assert.Equal(WorldActionRejection.None, accepted.Rejection);

        WorldSnapshot after = live.State;
        EntityId child = EntityId.DeriveSpawned(create.CommandId, 0);
        CountedSourceLot original = before.Inventory.Custody.GetLot(terms.LotId);
        CountedSourceLot moved = after.Inventory.Custody.GetLot(child);
        Assert.Equal((terms.DestinationCustodianId, 1UL, original.Source.SourceId, original.Source.Start),
            (moved.CustodianId, moved.Quantity, moved.Source.SourceId, moved.Source.Start));
        Assert.Equal(original.Quantity - 1, after.Inventory.Custody.GetLot(terms.LotId).Quantity);
        Assert.Equal((state.Revision + 1, state.AdvancingSteps + 1), (after.Revision, after.AdvancingSteps));
        Assert.Empty(after.Coordination!.Proposals!.Open);
        WorldMaterialProposalOutcome outcome = Assert.Single(after.Coordination.Proposals.Outcomes);
        Assert.Equal((WorldMaterialProposalOutcomeKind.Accepted, (EntityId?)accept.CommandId, after.Revision, child),
            (outcome.Kind, outcome.TerminalCommandId, outcome.WorldSequence, outcome.Settlement!.MovedLotId));
        Assert.Equal((before.Inventory.ContentHash, after.Inventory.ContentHash),
            (outcome.Settlement.OriginalInventoryHash, outcome.Settlement.CandidateInventoryHash));
        Assert.Empty(after.Coordination.ConsentedMaterial!.Transfers);

        // The creation receipt is a permanent separate fact; a retry returns the original outcome.
        Assert.Equal(creationReceipt, Bytes(live.Service.FindReceipt(create, sender)!));
        Assert.Equal(Bytes(accepted), Bytes(live.Service.Submit(accept, recipient)));
        Assert.Equal(creationReceipt, Bytes(live.Service.Submit(create, sender)));
        Assert.Equal(after.ContentHash, live.State.ContentHash);
        Assert.Equal(6, new[] { proposalId, child, create.CommandId, accept.CommandId, created.ResultId, accepted.ResultId }
            .Distinct().Count(identity => live.Authority.IsIdentityOccupiedForHost(identity)));

        // A later acceptance under a new identity learns only that nothing is available.
        Assert.Equal(WorldActionRejection.Unavailable, live.Submit(terms.RecipientActorId, view.PrepareAcceptance()));
        Assert.Equal(after.Inventory.ContentHash, live.State.Inventory.ContentHash);
        live.Reopen();
        Assert.Equal(creationReceipt, Bytes(live.Service.FindReceipt(create, sender)!));
        Assert.Equal(MaterialProposalStatus.Accepted, live.Service.Find(proposalId, terms.SenderActorId, sender)!.Status);
    }

    [Fact]
    public void A_produced_split_offer_keeps_its_producer_through_acceptance_recraft_and_self_use()
    {
        using var live = world.Open();
        WorldSnapshot before = live.State;
        TransformedLot stock = before.Production!.Lots.Single(value => value.LotId == world.Stock);
        var terms = new ConsentedMaterialTerms(before.WorldId, before.BranchId, world.Helper, world.Player,
            ConsentedMaterialLotKind.Produced, stock.LotId, stock.Definition, world.HelperBag, world.PlayerBag, 3, stock.Revision);
        var offer = live.Save(terms);
        byte[] creationReceipt = Bytes(live.Service.FindReceipt(offer.Command, offer.Delivery)!);
        live.Reopen();

        TrustedCommandDelivery recipient = world.DeliveryOf(live.State, world.Player);
        MaterialProposalView view = live.Service.Find(offer.ProposalId, world.Player, recipient)!;
        ulong revision = live.State.Production!.Revision;
        WorldActionCommand accept = live.Command(world.Player, view.PrepareAcceptance());
        Assert.Equal(WorldActionRejection.None, live.Service.Submit(accept, recipient).Rejection);
        WorldSnapshot state = live.State;
        TransformedLot child = state.Production!.Lots.Single(value => value.LotId == EntityId.DeriveSpawned(offer.Command.CommandId, 0));
        Assert.Equal((world.PlayerBag, world.Producer, 0UL, 3UL),
            (child.CustodianId, child.Output.BatchId, child.Output.Start, child.Output.End));
        Assert.Equal(1UL, state.Production.Lots.Single(value => value.LotId == world.Stock).Quantity);
        // Custody movement is neither a craft nor a use.
        Assert.Equal(revision, state.Production.Revision);

        var session = new CareWorldSession(live.Authority);
        WorldActionCommand recraft = ConsentedWorldPublicationTests.DurableAccess.CraftFor(state, world.Actions, world.Player,
            world.Repack, new(IngredientLotKind.Transformed, child.LotId, child.Revision, 1));
        state = CareRewardJourney.Commit(session, world.Actions.Batch(state, WorldBatchKind.AdvancingStep, [recraft])).Candidate;
        ProductionBatch repacked = state.Production!.Batches.Single(value => value.CommandId == recraft.CommandId);
        RetiredIngredient input = Assert.Single(repacked.Ingredients);
        Assert.Equal((child.LotId, world.Producer, child.Output.Start, child.Output.Start + 1),
            (input.LotId, input.Output!.BatchId, input.Output.Start, input.Output.End));
        TransformedLot dose = state.Production.Lots.Single(value => value.Output.BatchId == repacked.BatchId);
        var prepared = Assert.IsType<UseRestorativeItemIntent>(WorldRestorativeActions.Prepare(state,
            world.Player, world.Player, world.Self.DefinitionId, null));
        var use = new UseRestorativeItemIntent(prepared.RecipientActorId, prepared.UseDefinitionId,
            prepared.UseDefinitionVersion, prepared.PermissionId, prepared.PermissionGrantVersion,
            prepared.ExpectedPermissionRevision, prepared.ExpectedPatientPresenceRevision,
            prepared.ExpectedPatientProgressionRevision, prepared.ExpectedStorageRevision,
            prepared.ExpectedProductionRevision, new(IngredientLotKind.Transformed, dose.LotId, dose.Revision, 1));
        int beforeHp = state.Progression!.GetActor(world.Player).CurrentHp;
        WorldActionCommand consume = world.Actions.Command(state, world.Player, use);
        state = CareRewardJourney.Commit(session, world.Actions.Batch(state, WorldBatchKind.AdvancingStep, [consume])).Candidate;
        Assert.True(state.Progression!.GetActor(world.Player).CurrentHp > beforeHp);
        Assert.Equal(repacked.BatchId, state.Production!.ConsumedItems.Single(value =>
            value.CommandId == consume.CommandId).Ingredient.Output!.BatchId);

        live.Reopen();
        Assert.Equal(WorldSnapshotCodec.Encode(state), WorldSnapshotCodec.Encode(live.State));
        Assert.Equal(creationReceipt, Bytes(live.Service.FindReceipt(offer.Command, offer.Delivery)!));
    }

    [Fact]
    public void Wrong_deliveries_and_roles_learn_nothing_and_the_first_terminal_action_wins_once()
    {
        using var live = world.Open();
        ConsentedMaterialTerms terms = world.SelectRawTerms(live.Authority);
        WorldSnapshot before = live.State;
        WorldActionCommand create = world.Actions.Command(before, terms.SenderActorId,
            new ProposeMaterialIntent(terms, before.AdvancingSteps + 64));
        TrustedCommandDelivery sender = world.Actions.Delivery(create);
        TrustedCommandDelivery recipient = world.DeliveryOf(before, terms.RecipientActorId);
        Assert.NotEqual(sender.PrincipalId, recipient.PrincipalId);
        var rolledOver = new TrustedCommandDelivery(sender.PrincipalId, recipient.SessionId);

        // Creation needs the sender's own current delivery.
        foreach (TrustedCommandDelivery wrong in new[] { recipient, rolledOver })
        {
            Assert.Equal(StreamRefusal, Assert.Throws<AuthorityDispatchException>(() => live.Service.Submit(create, wrong)).Message);
        }
        Assert.Equal(before.ContentHash, live.State.ContentHash);
        Assert.Equal(WorldActionRejection.None, live.Service.Submit(create, sender).Rejection);
        EntityId proposalId = WorldMaterialProposal.DeriveIdentity(create.CommandId);
        EntityId missing = WorldMaterialProposal.DeriveIdentity(proposalId);

        // A wrong delivery receives one refusal for an existing and for a missing offer.
        foreach (EntityId identity in new[] { proposalId, missing })
        {
            Assert.Equal(QueryRefusal, Assert.Throws<AuthorityDispatchException>(() =>
                live.Service.Find(identity, terms.RecipientActorId, sender)).Message);
            Assert.Equal(QueryRefusal, Assert.Throws<AuthorityDispatchException>(() =>
                live.Service.Find(identity, terms.SenderActorId, rolledOver)).Message);
        }
        Assert.Equal(QueryRefusal, Assert.Throws<AuthorityDispatchException>(() =>
            live.Service.List(terms.RecipientActorId, sender)).Message);
        Assert.Null(live.Service.Find(missing, terms.RecipientActorId, recipient));
        Assert.True(live.Service.Find(proposalId, terms.SenderActorId, sender)!.ViewerIsSender);
        MaterialProposalView view = Assert.Single(live.Service.List(terms.RecipientActorId, recipient));
        Assert.Equal(proposalId, view.ProposalId);

        // Roles are not interchangeable, and a missing offer gives the same refusal.
        var foreign = new AcceptMaterialProposalIntent(missing, view.GrantHash, view.Terms, view.SenderPresenceRevision!.Value);
        Assert.Equal(WorldActionRejection.Unavailable, live.Submit(terms.SenderActorId, view.PrepareAcceptance()));
        Assert.Equal(WorldActionRejection.Unavailable, live.Submit(terms.SenderActorId, new DeclineMaterialProposalIntent(proposalId)));
        Assert.Equal(WorldActionRejection.Unavailable, live.Submit(terms.RecipientActorId, new WithdrawMaterialProposalIntent(proposalId)));
        Assert.Equal(WorldActionRejection.Unavailable, live.Submit(terms.RecipientActorId, foreign));
        Assert.Equal(WorldActionRejection.Unavailable, live.Submit(terms.RecipientActorId, new DeclineMaterialProposalIntent(missing)));
        Assert.Equal(WorldActionRejection.Unavailable, live.Submit(terms.SenderActorId, new WithdrawMaterialProposalIntent(missing)));
        Assert.Equal(proposalId, Assert.Single(live.Book.Open).ProposalId);
        Assert.Empty(live.Book.Outcomes);
        Assert.Equal(before.Inventory.ContentHash, live.State.Inventory.ContentHash);

        // The recipient's acceptance cannot arrive under the sender's delivery.
        WorldActionCommand accept = live.Command(terms.RecipientActorId, view.PrepareAcceptance());
        Assert.Equal(StreamRefusal, Assert.Throws<AuthorityDispatchException>(() => live.Service.Submit(accept, sender)).Message);

        // Changed terms are refused without requoting, and the offer stays open.
        var requoted = new ConsentedMaterialTerms(terms.WorldId, terms.BranchId, terms.SenderActorId, terms.RecipientActorId,
            terms.LotKind, terms.LotId, terms.Definition, terms.SourceCustodianId, terms.DestinationCustodianId,
            terms.Quantity + 1, terms.ExpectedLotRevision);
        Assert.Equal(WorldActionRejection.RevisionConflict, live.Submit(terms.RecipientActorId,
            new AcceptMaterialProposalIntent(proposalId, view.GrantHash, requoted, view.SenderPresenceRevision.Value)));
        Assert.Single(live.Book.Open);

        WorldActionCommand decline = live.Command(terms.RecipientActorId, new DeclineMaterialProposalIntent(proposalId));
        WorldActionReceipt declined = live.Service.Submit(decline, recipient);
        Assert.Equal(WorldActionRejection.None, declined.Rejection);
        WorldMaterialProposalOutcome outcome = Assert.Single(live.Book.Outcomes);
        Assert.Equal((WorldMaterialProposalOutcomeKind.Declined, (EntityId?)decline.CommandId),
            (outcome.Kind, outcome.TerminalCommandId));
        WorldSnapshot closed = live.State;

        // Later, duplicate and altered requests neither move stock nor reopen the offer.
        Assert.Equal(Bytes(declined), Bytes(live.Service.Submit(decline, recipient)));
        Assert.Equal(closed.ContentHash, live.State.ContentHash);
        var altered = new WorldActionCommand(decline.CommandId, decline.PrincipalId, decline.OriginSessionId, decline.ActorId,
            decline.WorldId, decline.BranchId, decline.ExpectedActorRevision, new DeclineMaterialProposalIntent(missing));
        Assert.Equal(ChangedContent, Assert.Throws<AuthorityDispatchException>(() => live.Service.Submit(altered, recipient)).Message);
        Assert.Equal(closed.ContentHash, live.State.ContentHash);
        Assert.Equal(WorldActionRejection.Unavailable, live.Submit(terms.RecipientActorId, view.PrepareAcceptance()));
        Assert.Equal(WorldActionRejection.Unavailable, live.Submit(terms.SenderActorId, new WithdrawMaterialProposalIntent(proposalId)));
        Assert.Equal(before.Inventory.ContentHash, live.State.Inventory.ContentHash);
        Assert.Equal(MaterialProposalStatus.Declined, live.Service.Find(proposalId, terms.SenderActorId, sender)!.Status);
    }

    [Fact]
    public void Withdrawal_and_the_exact_expiry_boundary_close_an_offer_once()
    {
        using var live = world.Open();
        ConsentedMaterialTerms terms = world.SelectRawTerms(live.Authority);
        EntityId sender = terms.SenderActorId, recipient = terms.RecipientActorId;

        // An offer must outlive its creating step and stay inside the bounded lifetime.
        Assert.Equal(WorldActionRejection.Unavailable, live.Submit(sender,
            new ProposeMaterialIntent(terms, live.State.AdvancingSteps + 1)));
        Assert.Equal(WorldActionRejection.Unavailable, live.Submit(sender,
            new ProposeMaterialIntent(terms, live.State.AdvancingSteps + 2 + WorldMaterialProposal.MaximumLifetimeSteps)));
        Assert.Empty(live.Book.Open);

        var longest = live.Save(terms, WorldMaterialProposal.MaximumLifetimeSteps);
        Assert.Equal(WorldActionRejection.None, live.Submit(sender, new WithdrawMaterialProposalIntent(longest.ProposalId)));
        Assert.Equal(WorldActionRejection.Unavailable, live.Submit(sender, new WithdrawMaterialProposalIntent(longest.ProposalId)));
        MaterialProposalView withdrawn = live.Service.Find(longest.ProposalId, recipient, world.DeliveryOf(live.State, recipient))!;
        Assert.Equal(MaterialProposalStatus.Withdrawn, withdrawn.Status);
        Assert.Equal(WorldActionRejection.Unavailable, live.Submit(recipient, withdrawn.PrepareAcceptance()));

        // The shortest offer is acceptable in exactly the next step, whose own boundary would expire it.
        var shortest = live.Save(terms, lifetime: 1);
        WorldSnapshot final = live.State;
        Assert.Equal(final.AdvancingSteps + 1, Assert.Single(live.Book.Open).ExpiresAtAdvancingStep);
        MaterialProposalView open = live.Service.Find(shortest.ProposalId, recipient, world.DeliveryOf(final, recipient))!;
        Assert.Equal(WorldActionRejection.None, live.Submit(recipient, open.PrepareAcceptance()));
        Assert.Equal(WorldMaterialProposalOutcomeKind.Accepted, live.Book.FindOutcome(shortest.ProposalId)!.Kind);

        // Without an acceptance the same boundary closes the offer by world advancement alone.
        ConsentedMaterialTerms next = world.SelectRawTerms(live.Authority);
        var expiring = live.Save(next, lifetime: 1);
        WorldSnapshot pending = live.State;
        live.Step();
        WorldSnapshot after = live.State;
        Assert.Equal(pending.AdvancingSteps + 1, after.AdvancingSteps);
        Assert.Empty(live.Book.Open);
        WorldMaterialProposalOutcome expired = live.Book.FindOutcome(expiring.ProposalId)!;
        Assert.Equal((WorldMaterialProposalOutcomeKind.Expired, (EntityId?)null, after.Revision),
            (expired.Kind, expired.TerminalCommandId, expired.WorldSequence));
        Assert.Equal(pending.Inventory.ContentHash, after.Inventory.ContentHash);
        MaterialProposalView view = live.Service.Find(expiring.ProposalId, next.RecipientActorId,
            world.DeliveryOf(after, next.RecipientActorId))!;
        Assert.Equal((MaterialProposalStatus.Expired, (ulong?)after.Revision), (view.Status, view.ClosedAtSequence));
        Assert.Equal(WorldActionRejection.Unavailable, live.Submit(next.RecipientActorId, view.PrepareAcceptance()));
        Assert.Equal(WorldActionRejection.Unavailable, live.Submit(next.RecipientActorId, new DeclineMaterialProposalIntent(expiring.ProposalId)));
        Assert.Equal(WorldActionRejection.Unavailable, live.Submit(next.SenderActorId, new WithdrawMaterialProposalIntent(expiring.ProposalId)));
        Assert.Equal(after.Inventory.ContentHash, live.State.Inventory.ContentHash);
        Assert.True(live.Authority.IsIdentityOccupiedForHost(expiring.ProposalId));
    }

    [Fact]
    public void Competing_use_refuses_the_stale_offer_without_retargeting_and_leaves_it_open()
    {
        using var live = world.Open();
        ConsentedMaterialTerms terms = world.SelectRawTerms(live.Authority);
        var first = live.Save(terms);
        var second = live.Save(terms);
        Assert.Equal(2, live.Book.Open.Count);
        Assert.Null(live.State.Inventory.Custody.GetLot(terms.LotId).ReservedBy);
        TrustedCommandDelivery recipient = world.DeliveryOf(live.State, terms.RecipientActorId);

        Assert.Equal(WorldActionRejection.None, live.Submit(terms.RecipientActorId,
            live.Service.Find(first.ProposalId, terms.RecipientActorId, recipient)!.PrepareAcceptance()));
        WorldSnapshot settled = live.State;
        Assert.NotEqual(terms.ExpectedLotRevision, settled.Inventory.Custody.GetLot(terms.LotId).Revision);

        // The exact approved lot revision was spent by the competing settlement.
        MaterialProposalView stale = live.Service.Find(second.ProposalId, terms.RecipientActorId, recipient)!;
        Assert.Equal(WorldActionRejection.RevisionConflict, live.Submit(terms.RecipientActorId, stale.PrepareAcceptance()));
        WorldSnapshot refused = live.State;
        Assert.Equal(settled.Inventory.ContentHash, refused.Inventory.ContentHash);
        Assert.Equal((settled.Revision + 1, settled.AdvancingSteps + 1), (refused.Revision, refused.AdvancingSteps));
        Assert.False(live.Authority.IsIdentityOccupiedForHost(EntityId.DeriveSpawned(second.Command.CommandId, 0)));
        // A refused acceptance closes nothing; the sender still withdraws its own offer.
        Assert.Equal(second.ProposalId, Assert.Single(live.Book.Open).ProposalId);
        Assert.Equal(WorldActionRejection.None, live.Submit(terms.SenderActorId, new WithdrawMaterialProposalIntent(second.ProposalId)));
        Assert.Equal(settled.Inventory.ContentHash, live.State.Inventory.ContentHash);
    }

    [Fact]
    public void Coupled_physical_storage_refuses_a_valid_acceptance_without_moving_goods()
    {
        using var live = world.Open();
        WorldSnapshot before = live.State;
        var reservoir = before.Reservoirs!;
        EntityId receiver = reservoir.Geometries.Single().CustodianId;
        Assert.Equal(world.Helper, before.Inventory.Policies.Single(value => value.CustodianId == receiver).OwnerActorId);
        CountedSourceLot stock = before.Inventory.Custody.Lots.First(value => value.CustodianId == world.PlayerBag
            && value.Quantity > 1 && value.Protection == LotProtection.None && value.ReservedBy is null
            && !value.Definition.Equals(reservoir.Capacity.Domain.Material.Item));
        var terms = new ConsentedMaterialTerms(before.WorldId, before.BranchId, world.Player, world.Helper,
            ConsentedMaterialLotKind.CountedSource, stock.LotId, stock.Definition, world.PlayerBag, receiver, 1, stock.Revision);
        var offer = live.Save(terms);
        WorldSnapshot saved = live.State;
        TrustedCommandDelivery recipient = world.DeliveryOf(saved, world.Helper);
        MaterialProposalView view = live.Service.Find(offer.ProposalId, world.Helper, recipient)!;
        Assert.Equal(WorldActionRejection.Unavailable, live.Submit(world.Helper, view.PrepareAcceptance()));
        WorldSnapshot refused = live.State;
        Assert.Equal(saved.Inventory.ContentHash, refused.Inventory.ContentHash);
        Assert.Equal(saved.Production!.ContentHash, refused.Production!.ContentHash);
        Assert.Equal(reservoir.ContentHash, refused.Reservoirs!.ContentHash);
        Assert.Equal((saved.Revision + 1, saved.AdvancingSteps + 1), (refused.Revision, refused.AdvancingSteps));
        Assert.Equal(offer.ProposalId, Assert.Single(live.Book.Open).ProposalId);
        Assert.Empty(live.Book.Outcomes);
        Assert.False(live.Authority.IsIdentityOccupiedForHost(EntityId.DeriveSpawned(offer.Command.CommandId, 0)));
    }

    [Fact]
    public void Participants_who_walked_apart_are_out_of_reach_and_the_open_offer_settles_when_they_return()
    {
        using var live = world.Open();
        ConsentedMaterialTerms terms = world.SelectRawTerms(live.Authority);
        var offer = live.Save(terms);
        EntityId mover = terms.RecipientActorId;
        TrustedCommandDelivery recipient = world.DeliveryOf(live.State, mover);
        bool Reachable() => live.State.Space.QueryInteraction(mover, terms.SenderActorId)
            == OpenFantasy.Core.Spatial.SpatialInteraction.Reachable;
        void Walk(long x, long y)
        {
            // An accepted movement sample of the recipient's own current delivery.
            WorldSnapshot state = live.State;
            live.Authority.Commit(new WorldStepBatch(world.NextCause(state), state.WorldId, state.BranchId,
                state.Revision, state.AdvancingSteps, WorldBatchKind.AdvancingStep,
                [new WorldMovementInput(recipient, mover, x, y)], [], state.ProfileVersion));
        }
        Assert.True(Reachable());
        (long X, long Y) away = default;
        foreach ((long x, long y) in new (long, long)[] { (32, 0), (-32, 0), (0, 32), (0, -32) })
        {
            for (int step = 0; step < 24 && Reachable(); step++) Walk(x, y);
            if (Reachable()) continue;
            away = (x, y);
            break;
        }
        Assert.False(Reachable(), "The real recipient must be able to walk out of reach in this locality.");

        WorldSnapshot apart = live.State;
        MaterialProposalView view = live.Service.Find(offer.ProposalId, mover, recipient)!;
        Assert.Equal(WorldActionRejection.OutOfReach, live.Submit(mover, view.PrepareAcceptance()));
        WorldSnapshot refused = live.State;
        Assert.Equal(apart.Inventory.ContentHash, refused.Inventory.ContentHash);
        Assert.Equal((apart.Revision + 1, apart.AdvancingSteps + 1), (refused.Revision, refused.AdvancingSteps));
        Assert.Equal(offer.ProposalId, Assert.Single(live.Book.Open).ProposalId);
        Assert.Empty(live.Book.Outcomes);

        // The refusal closed nothing: after actually returning, a new acceptance settles the same offer.
        for (int step = 0; step < 48 && !Reachable(); step++) Walk(-away.X, -away.Y);
        Assert.True(Reachable());
        Assert.Equal(WorldActionRejection.None, live.Submit(mover,
            live.Service.Find(offer.ProposalId, mover, recipient)!.PrepareAcceptance()));
        Assert.Equal(terms.DestinationCustodianId,
            live.State.Inventory.Custody.GetLot(EntityId.DeriveSpawned(offer.Command.CommandId, 0)).CustodianId);
    }

    [Fact]
    public void An_open_offer_spans_an_owner_published_cut_and_settles_after_reopening()
    {
        using var live = world.Open();
        // An accepted, a declined and an expired offer close inside the segment the cut will archive.
        ConsentedMaterialTerms early = world.SelectRawTerms(live.Authority);
        TrustedCommandDelivery earlyRecipient = world.DeliveryOf(live.State, early.RecipientActorId);
        var taken = live.Save(early);
        byte[] takenReceipt = Bytes(live.Service.FindReceipt(taken.Command, taken.Delivery)!);
        Assert.Equal(WorldActionRejection.None, live.Submit(early.RecipientActorId,
            live.Service.Find(taken.ProposalId, early.RecipientActorId, earlyRecipient)!.PrepareAcceptance()));
        var refused = live.Save(world.SelectRawTerms(live.Authority));
        Assert.Equal(WorldActionRejection.None, live.Submit(refused.Command.ActorId == early.SenderActorId
            ? early.RecipientActorId : early.SenderActorId, new DeclineMaterialProposalIntent(refused.ProposalId)));
        var lapsed = live.Save(world.SelectRawTerms(live.Authority), lifetime: 1);
        live.Step();
        Assert.Equal(WorldMaterialProposalOutcomeKind.Expired, live.Book.FindOutcome(lapsed.ProposalId)!.Kind);

        ConsentedMaterialTerms terms = world.SelectRawTerms(live.Authority);
        var spanning = live.Save(terms, lifetime: 4_096);
        WorldMaterialCareArchiveSegment? archive = null;
        WorldSnapshot? closing = null;
        for (int cycle = 0; archive is null; cycle++)
        {
            Assert.True(cycle <= WorldMaterialProposalBook.MaximumOutcomes, "Outcome pressure must publish a cut within one window.");
            WorldSnapshot state = live.State;
            WorldActionCommand[] offers =
            [
                world.Actions.Command(state, world.Helper, new ProposeMaterialIntent(
                    world.PinnedRawTerms(state, world.Helper, world.Player), state.AdvancingSteps + 64)),
                world.Actions.Command(state, world.Player, new ProposeMaterialIntent(
                    world.PinnedRawTerms(state, world.Player, world.Helper), state.AdvancingSteps + 64))
            ];
            archive = Commit(live, state, offers, ref closing);
            state = live.State;
            WorldActionCommand[] withdrawals = offers.Select(offer => world.Actions.Command(state, offer.ActorId,
                new WithdrawMaterialProposalIntent(WorldMaterialProposal.DeriveIdentity(offer.CommandId)))).ToArray();
            archive ??= Commit(live, state, withdrawals, ref closing);
        }

        // The owner closed the complete root itself, with the spanning offer still open inside it.
        Assert.Equal(0UL, archive.Reference.Ordinal);
        Assert.Equal(WorldSnapshotCodec.Encode(closing!), archive.Encode());
        WorldMaterialProposalBook closed = closing!.Coordination!.Proposals!;
        Assert.Contains(closed.Open, value => value.ProposalId == spanning.ProposalId);
        Assert.InRange(closed.Outcomes.Count, WorldMaterialProposalBook.MaximumOutcomes - 4, WorldMaterialProposalBook.MaximumOutcomes);
        Assert.Contains(live.Book.Open, value => value.ProposalId == spanning.ProposalId);
        Assert.True(live.Book.Outcomes.Count <= 2);
        Assert.Equal(1UL, live.Authority.InspectForHost().Journal.MaterialCareArchiveCount);

        live.Reopen();
        WorldSnapshot continued = live.State;
        Assert.NotNull(continued.Coordination!.Anchor);
        TrustedCommandDelivery recipient = world.DeliveryOf(continued, terms.RecipientActorId);
        MaterialProposalView view = live.Service.Find(spanning.ProposalId, terms.RecipientActorId, recipient)!;
        Assert.Equal(MaterialProposalStatus.Open, view.Status);
        Assert.Equal(WorldActionRejection.None, live.Submit(terms.RecipientActorId, view.PrepareAcceptance()));
        CountedSourceLot moved = live.State.Inventory.Custody.GetLot(EntityId.DeriveSpawned(spanning.Command.CommandId, 0));
        Assert.Equal((terms.DestinationCustodianId, 1UL), (moved.CustodianId, moved.Quantity));
        Assert.Equal(Bytes(live.Service.FindReceipt(spanning.Command, spanning.Delivery)!),
            Bytes(live.Service.Submit(spanning.Command, spanning.Delivery)));

        // Later historical retrieval authenticates the archived lifecycle through the current head.
        RecordedWorldMaterialCareArchive recorded = live.Authority.ReadMaterialCareArchiveForHost(0);
        Assert.Equal(archive.Encode(), recorded.Segment.Encode());
        WorldMaterialProposalBook archived = WorldSnapshotCodec.Decode(recorded.Segment.Encode()).Coordination!.Proposals!;
        Assert.Equal(closed.ContentHash, archived.ContentHash);
        EntityId[] lifecycles = [taken.ProposalId, refused.ProposalId, lapsed.ProposalId];
        Assert.All(archived.Outcomes.Where(value => !lifecycles.Contains(value.Proposal.ProposalId)),
            value => Assert.Equal(WorldMaterialProposalOutcomeKind.Withdrawn, value.Kind));
        RecordedWorldState created = live.Authority.ReadRecordedStateForHost(
            closed.Open.Single(value => value.ProposalId == spanning.ProposalId).CreatedSequence);
        Assert.Contains(created.State.Coordination!.Proposals!.Open, value => value.ProposalId == spanning.ProposalId);

        // Identities of the closed segment stay occupied; none can name a new cause or offer.
        EntityId spent = archived.Outcomes[0].Proposal.ProposalId;
        Assert.DoesNotContain(live.Book.Outcomes, value => value.Proposal.ProposalId == spent);
        Assert.True(live.Authority.IsIdentityOccupiedForHost(spent));
        Assert.True(live.Authority.IsIdentityOccupiedForHost(archived.Outcomes[0].TerminalCommandId!.Value));
        WorldSnapshot head = live.State;
        Assert.Equal(StaleDispatch, Assert.Throws<AuthorityDispatchException>(() => live.Authority.Commit(new WorldStepBatch(
            spent, head.WorldId, head.BranchId, head.Revision, head.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [],
            head.ProfileVersion))).Message);
        Assert.Equal(head.ContentHash, live.State.ContentHash);
        Assert.Equal(WorldActionRejection.Unavailable, live.Submit(terms.SenderActorId, new WithdrawMaterialProposalIntent(spent)));

        // Both participants still retrieve every archived terminal result by its exact identity.
        live.Reopen();
        WorldSnapshot current = live.State;
        (EntityId Actor, TrustedCommandDelivery Delivery)[] participants =
        [
            (world.Helper, world.DeliveryOf(current, world.Helper)), (world.Player, world.DeliveryOf(current, world.Player))
        ];
        EntityId recalled = archived.Outcomes.First(value =>
            value.Kind == WorldMaterialProposalOutcomeKind.Withdrawn).Proposal.ProposalId;
        foreach ((EntityId identity, MaterialProposalStatus status) in new[]
        {
            (taken.ProposalId, MaterialProposalStatus.Accepted), (refused.ProposalId, MaterialProposalStatus.Declined),
            (lapsed.ProposalId, MaterialProposalStatus.Expired), (recalled, MaterialProposalStatus.Withdrawn)
        })
        {
            WorldMaterialProposalOutcome outcome = archived.FindOutcome(identity)!;
            foreach ((EntityId actor, TrustedCommandDelivery delivery) in participants)
            {
                // The bounded current query no longer holds it; the recorded lookup does.
                Assert.Null(live.Service.Find(identity, actor, delivery));
                MaterialProposalView found = live.Service.FindRecorded(identity, actor, delivery)!;
                Assert.Equal((identity, status, (ulong?)outcome.WorldSequence, outcome.Proposal.Terms,
                    outcome.Proposal.GrantHash, outcome.Proposal.Terms.SenderActorId == actor),
                    (found.ProposalId, found.Status, found.ClosedAtSequence, found.Terms, found.GrantHash, found.ViewerIsSender));
            }
        }
        // The live owner answers the same lookup for the offer that settled after the cut.
        Assert.Equal(MaterialProposalStatus.Accepted,
            live.Service.FindRecorded(spanning.ProposalId, terms.SenderActorId, spanning.Delivery)!.Status);

        // A wrong delivery learns nothing about an archived, a live or a missing identity.
        EntityId missing = WorldMaterialProposal.DeriveIdentity(taken.ProposalId);
        var rolledOver = new TrustedCommandDelivery(participants[0].Delivery.PrincipalId, participants[1].Delivery.SessionId);
        foreach (EntityId identity in new[] { taken.ProposalId, spanning.ProposalId, missing })
        {
            Assert.Equal(QueryRefusal, Assert.Throws<AuthorityDispatchException>(() =>
                live.Service.FindRecorded(identity, participants[0].Actor, participants[1].Delivery)).Message);
            Assert.Equal(QueryRefusal, Assert.Throws<AuthorityDispatchException>(() =>
                live.Service.FindRecorded(identity, participants[0].Actor, rolledOver)).Message);
        }
        foreach ((EntityId actor, TrustedCommandDelivery delivery) in participants)
        {
            Assert.Null(live.Service.FindRecorded(missing, actor, delivery));
        }
        // Another controlled actor takes no part: an archived offer reads exactly like a missing one.
        ActorControlBinding? outsider = current.Inventory.Controls.FirstOrDefault(value =>
            value.ActorId != world.Helper && value.ActorId != world.Player);
        Assert.True(outsider is not null, "The admitted world must hold a third controlled actor for the foreign lookup.");
        var outside = new TrustedCommandDelivery(outsider!.PrincipalId, outsider.SessionId);
        foreach (EntityId identity in new[] { taken.ProposalId, lapsed.ProposalId, spanning.ProposalId, missing })
        {
            Assert.Null(live.Service.FindRecorded(identity, outsider.ActorId, outside));
        }

        // Retrieval published nothing, and the creation receipt is still the original fact.
        Assert.Equal(WorldSnapshotCodec.Encode(current), WorldSnapshotCodec.Encode(live.State));
        Assert.Equal(1UL, live.Authority.InspectForHost().Journal.MaterialCareArchiveCount);
        Assert.Equal(takenReceipt, Bytes(live.Service.FindReceipt(taken.Command, taken.Delivery)!));

        // The exclusive live lease rejects another SQL writer. After disposal, damage the
        // disposable save and prove the recorded-outcome replay rejects even a matching stored hash.
        byte[] alteredArchive = archive.Encode();
        alteredArchive[^1] ^= 1;
        void DamageArchive()
        {
            using var edit = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = live.Files.Path, Pooling = false, DefaultTimeout = 1
            }.ToString());
            edit.Open();
            using SqliteCommand damage = edit.CreateCommand();
            damage.CommandText = "UPDATE world_material_care_segments SET state=$state,boundary_hash=$hash";
            damage.Parameters.AddWithValue("$state", alteredArchive);
            damage.Parameters.AddWithValue("$hash", ContentHash.Compute(alteredArchive).ToByteArray());
            Assert.Equal(1, damage.ExecuteNonQuery());
        }
        Assert.Equal(5, Assert.Throws<SqliteException>(DamageArchive).SqliteErrorCode);
        Assert.Equal(MaterialProposalStatus.Accepted, live.Service.FindRecorded(taken.ProposalId,
            participants[0].Actor, participants[0].Delivery)!.Status);
        live.Authority.Dispose();
        DamageArchive();
        Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(live.Files.Path));
        using var database = WorldDatabase.Open(live.Files.Path, create: false);
        Assert.Equal("A stored material and care archive differs from its exact replay boundary.",
            Assert.Throws<InvalidDataException>(() => new WorldJournalRecovery()
                .ReadMaterialProposalOutcome(database, taken.ProposalId)).Message);
    }

    private WorldMaterialCareArchiveSegment? Commit(MaterialProposalWorldFixture.LiveWorld live, WorldSnapshot state,
        WorldActionCommand[] actions, ref WorldSnapshot? closing)
    {
        // The cut follows the owner's own reserved outcome window and nothing here seals a segment.
        WorldMaterialProposalBook book = state.Coordination!.Proposals!;
        int creations = actions.Count(action => action.Intent is ProposeMaterialIntent);
        bool expected = book.Outcomes.Count + book.Open.Count + creations + 1 > WorldMaterialProposalBook.MaximumOutcomes;
        WorldStepDecision decision = live.Authority.Commit(world.Actions.Batch(state, WorldBatchKind.AdvancingStep, actions));
        Assert.All(decision.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
        Assert.Equal(expected, decision.SealedMaterialCareSegment is not null);
        if (decision.SealedMaterialCareSegment is not null) closing = state;
        return decision.SealedMaterialCareSegment;
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Injected_loss_publishes_a_creation_or_an_acceptance_completely_or_not_at_all(bool acceptance, bool afterCommit)
    {
        using var live = world.Open();
        ConsentedMaterialTerms terms = world.SelectRawTerms(live.Authority);
        WorldActionCommand target;
        TrustedCommandDelivery delivery;
        if (acceptance)
        {
            var offer = live.Save(terms);
            delivery = world.DeliveryOf(live.State, terms.RecipientActorId);
            target = live.Command(terms.RecipientActorId,
                live.Service.Find(offer.ProposalId, terms.RecipientActorId, delivery)!.PrepareAcceptance());
        }
        else
        {
            target = live.Command(terms.SenderActorId, new ProposeMaterialIntent(terms, live.State.AdvancingSteps + 32));
            delivery = world.Actions.Delivery(target);
        }
        WorldSnapshot before = live.State;
        byte[] beforeBytes = WorldSnapshotCodec.Encode(before);
        EntityId cause = world.NextCause(before);
        WorldStepBatch batch = SqliteWorldAuthority.SelectMaterialProposalBatch(before, target, cause);
        WorldStepDecision expected = live.Authority.Preview(batch);
        WorldActionReceipt receipt = Assert.Single(expected.Receipts);
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
        // Cause, command and result, with the proposal identity or the settled split child.
        Assert.Equal(4, expected.Identities.Count);
        using var evidence = new StorageFiles();
        string beforeCopy = Path.Combine(Path.GetDirectoryName(evidence.Path)!, "before.sqlite3");
        live.Authority.BackupForHost(beforeCopy);

        Assert.Throws<IOException>(() => live.Authority.SubmitMaterialProposalCommand(target, delivery, cause, boundary =>
        {
            if (boundary == (afterCommit ? AuthorityCommitBoundary.AfterCommit : AuthorityCommitBoundary.BeforeCommit))
                throw new IOException("Injected loss at the proposal publication boundary.");
        }));
        Assert.Throws<InvalidOperationException>(() => live.Authority.ReadStateForHost());
        live.Reopen();

        WorldSnapshot recovered = live.State;
        if (afterCommit)
        {
            Assert.Equal(WorldSnapshotCodec.Encode(expected.Candidate), WorldSnapshotCodec.Encode(recovered));
            Assert.Equal(Bytes(receipt), Bytes(live.Service.FindReceipt(target, delivery)!));
            Assert.All(expected.Identities, identity => Assert.True(live.Authority.IsIdentityOccupiedForHost(identity.Identity)));
            // The retry returns the original outcome and publishes nothing more.
            Assert.Equal(Bytes(receipt), Bytes(live.Service.Submit(target, delivery)));
            Assert.Equal(recovered.ContentHash, live.State.ContentHash);
        }
        else
        {
            Assert.Equal(beforeBytes, WorldSnapshotCodec.Encode(recovered));
            Assert.Null(live.Service.FindReceipt(target, delivery));
            Assert.All(expected.Identities, identity => Assert.False(live.Authority.IsIdentityOccupiedForHost(identity.Identity)));
            // The unpublished submission is still fresh and publishes exactly the previewed root.
            Assert.Equal(Bytes(receipt), Bytes(live.Authority.SubmitMaterialProposalCommand(target, delivery, cause)));
            Assert.Equal(WorldSnapshotCodec.Encode(expected.Candidate), WorldSnapshotCodec.Encode(live.State));
        }
        Assert.Equal(acceptance ? 1 : 0, live.Book.Outcomes.Count);
        Assert.Equal(acceptance ? 0 : 1, live.Book.Open.Count);
        Export(acceptance, afterCommit, beforeCopy, live, before, batch, expected, target, receipt);
    }

    /// <summary>A disposable self-contained fixture for separate real-process loss qualification.</summary>
    private static void Export(bool acceptance, bool afterCommit, string beforeCopy, MaterialProposalWorldFixture.LiveWorld live,
        WorldSnapshot before, WorldStepBatch batch, WorldStepDecision expected, WorldActionCommand command, WorldActionReceipt receipt)
    {
        if (Environment.GetEnvironmentVariable(MaterialProposalWorldFixture.EvidenceVariable) is not { Length: > 0 } root) return;
        string directory = Path.Combine(root, (acceptance ? "acceptance" : "creation") + (afterCommit ? "-after-commit" : "-before-commit"));
        Directory.CreateDirectory(directory);
        File.Copy(beforeCopy, Path.Combine(directory, "before.sqlite3"), overwrite: true);
        string after = Path.Combine(directory, "after.sqlite3");
        File.Delete(after);
        live.Authority.BackupForHost(after);
        File.WriteAllBytes(Path.Combine(directory, "before.ofc"), WorldSnapshotCodec.Encode(before));
        File.WriteAllBytes(Path.Combine(directory, "batch.ofc"), WorldStepCodec.Encode(batch));
        File.WriteAllBytes(Path.Combine(directory, "after.ofc"), WorldSnapshotCodec.Encode(expected.Candidate));
        File.WriteAllBytes(Path.Combine(directory, "command.ofc"), WorldActionCodec.Encode(command));
        File.WriteAllBytes(Path.Combine(directory, "receipt.ofc"), WorldReceiptCodecV1.Encode(receipt));
        File.WriteAllText(Path.Combine(directory, "metadata.json"), JsonSerializer.Serialize(new
        {
            profileVersion = 30, schemaVersion = WorldDatabaseSchemaEight.Version,
            beforeRevision = before.Revision, beforeHash = before.ContentHash.Value,
            afterRevision = expected.Candidate.Revision, afterHash = expected.Candidate.ContentHash.Value,
            principal = Convert.ToHexString(command.PrincipalId.ToByteArray()).ToLowerInvariant(),
            command = Convert.ToHexString(command.CommandId.ToByteArray()).ToLowerInvariant(),
            identities = expected.Identities.Select(value => new
            {
                identity = Convert.ToHexString(value.Identity.ToByteArray()).ToLowerInvariant(), kind = value.Kind.ToString()
            }),
            note = "Fresh profile-30 world. batch.ofc applied to before.sqlite3 publishes after.ofc, receipt.ofc and the "
                + "listed identities in one transaction. Exception injection is not process-loss proof."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    [Theory]
    [InlineData("head-without-offer", "The stored world head does not match exact replay.")]
    [InlineData("foreign-genesis", "The local world journal is discontinuous.")]
    [InlineData("changed-journal-terms", "A stored world batch or outcome differs from deterministic replay.")]
    [InlineData("missing-creation-receipt", "The retained actor-command table is incomplete.")]
    [InlineData("altered-creation-receipt", "A stored actor command or receipt differs from its replayed commit.")]
    [InlineData("missing-proposal-identity", "The retained world-identity table is incomplete.")]
    [InlineData("altered-proposal-identity", "A retained identity differs from its replayed commit or role.")]
    public void Recovery_refuses_damaged_proposal_state_journal_receipts_and_identities(string corruption, string reason)
    {
        using var live = world.Open();
        ConsentedMaterialTerms terms = world.SelectRawTerms(live.Authority);
        WorldSnapshot beforeOffer = live.State;
        var offer = live.Save(terms);
        ulong created = live.State.Revision;
        TrustedCommandDelivery recipient = world.DeliveryOf(live.State, terms.RecipientActorId);
        WorldActionCommand accept = live.Command(terms.RecipientActorId,
            live.Service.Find(offer.ProposalId, terms.RecipientActorId, recipient)!.PrepareAcceptance());
        byte[] acceptanceReceipt = Bytes(live.Service.Submit(accept, recipient));
        byte[] final = WorldSnapshotCodec.Encode(live.State);
        using var files = new StorageFiles();
        live.Authority.BackupForHost(files.Path);
        using (var control = SqliteWorldAuthority.OpenExisting(files.Path, world.GenesisBytes))
        {
            Assert.Equal(final, WorldSnapshotCodec.Encode(control.ReadStateForHost()));
        }

        switch (corruption)
        {
            case "head-without-offer":
                // A genuine earlier root of this world with its own recomputed hash.
                byte[] earlier = WorldSnapshotCodec.Encode(beforeOffer);
                Mutate(files, "UPDATE world_head SET state=$state,hash=$hash WHERE singleton=1",
                    ("$state", earlier), ("$hash", ContentHash.Compute(earlier).ToByteArray()));
                break;
            case "foreign-genesis":
                // The same fresh basis under the paired interpretation, with matching checksums.
                byte[] hash = ContentHash.Compute(world.ConsentedGenesisBytes).ToByteArray();
                Mutate(files, "UPDATE world_genesis SET state=$state,hash=$hash WHERE singleton=1",
                    ("$state", world.ConsentedGenesisBytes), ("$hash", hash));
                Mutate(files, "UPDATE world_checkpoints SET state=$state,hash=$hash WHERE sequence=$zero",
                    ("$state", world.ConsentedGenesisBytes), ("$hash", hash), ("$zero", WorldStorageBinary.Number(0)));
                break;
            case "changed-journal-terms":
                // A well-formed batch whose saved offer names a different expiry under the same identities.
                var intent = (ProposeMaterialIntent)offer.Command.Intent;
                var changed = new WorldActionCommand(offer.Command.CommandId, offer.Command.PrincipalId,
                    offer.Command.OriginSessionId, offer.Command.ActorId, offer.Command.WorldId, offer.Command.BranchId,
                    offer.Command.ExpectedActorRevision, new ProposeMaterialIntent(intent.Terms, intent.ExpiresAtAdvancingStep + 1));
                byte[] mutated;
                using (var database = WorldDatabase.Open(files.Path, create: false))
                {
                    WorldStepBatch stored = WorldStepCodec.Decode(database.ReadJournal().Single(value => value.Sequence == created).AcceptedBatch);
                    mutated = WorldStepCodec.Encode(new WorldStepBatch(stored.CauseId, stored.WorldId, stored.BranchId,
                        stored.ExpectedRevision, stored.ExpectedAdvancingSteps, stored.Kind, stored.Movement, [changed],
                        stored.ProfileVersion));
                }
                Mutate(files, "UPDATE world_commits SET accepted_batch=$batch WHERE sequence=$sequence",
                    ("$batch", mutated), ("$sequence", WorldStorageBinary.Number(created)));
                break;
            case "missing-creation-receipt":
                Mutate(files, "DELETE FROM world_command_outcomes WHERE principal=$principal AND command_id=$command",
                    ("$principal", offer.Command.PrincipalId.ToByteArray()), ("$command", offer.Command.CommandId.ToByteArray()));
                break;
            case "altered-creation-receipt":
                // The recipient's genuine acceptance receipt is still not the creation receipt.
                Mutate(files, "UPDATE world_command_outcomes SET receipt=$receipt WHERE principal=$principal AND command_id=$command",
                    ("$receipt", acceptanceReceipt), ("$principal", offer.Command.PrincipalId.ToByteArray()),
                    ("$command", offer.Command.CommandId.ToByteArray()));
                break;
            case "missing-proposal-identity":
                Mutate(files, "DELETE FROM world_occupied_identity WHERE identity=$identity",
                    ("$identity", offer.ProposalId.ToByteArray()));
                break;
            case "altered-proposal-identity":
                Mutate(files, "UPDATE world_occupied_identity SET kind=$kind WHERE identity=$identity",
                    ("$kind", (int)WorldIdentityKind.Command), ("$identity", offer.ProposalId.ToByteArray()));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(corruption));
        }
        Assert.Equal(reason, Assert.Throws<InvalidDataException>(() => SqliteWorldAuthority.OpenExisting(files.Path)).Message);
        Assert.Equal(reason, Assert.Throws<InvalidDataException>(() =>
            SqliteWorldAuthority.OpenExisting(files.Path, world.GenesisBytes)).Message);
    }

    /// <summary>Applies one deliberate edit to a disposable copy and proves it changed exactly one row.</summary>
    private static void Mutate(StorageFiles files, string sql, params (string Name, object Value)[] parameters)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = files.Path, Mode = SqliteOpenMode.ReadWrite, Pooling = false
        }.ToString());
        connection.Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        foreach ((string name, object value) in parameters) command.Parameters.AddWithValue(name, value);
        Assert.Equal(1, command.ExecuteNonQuery());
    }
}
