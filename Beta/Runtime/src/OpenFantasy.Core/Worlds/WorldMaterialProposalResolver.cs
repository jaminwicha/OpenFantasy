using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>One decided proposal submission: the candidate owners, or the untouched originals and a refusal.</summary>
internal sealed record WorldMaterialProposalPlan(CountedAuthorityState Inventory, ProductionSnapshot Production,
    WorldMaterialProposalBook Book, WorldActionRejection Rejection, IReadOnlyList<EntityId> CreatedIds,
    WorldMaterialProposalSettlement? Settlement, EntityId? CounterpartActorId);

/// <summary>
/// Decides one saved-proposal submission against the world's actual owners. Creation saves the
/// sender's own grant; acceptance rechecks current control of both parties, the exact saved terms,
/// expiry, presence, bodies, reach and both opportunities before the shared material decision runs.
/// A refused acceptance closes nothing: the offer stays open until it is accepted, declined,
/// withdrawn or expired. Missing, closed, expired and foreign proposals give one refusal.
/// All four submissions share one order: current control, the caller's own presence revision,
/// availability, and only then the proposal.
/// </summary>
internal static class WorldMaterialProposalResolver
{
    internal static WorldMaterialProposalPlan Resolve(WorldSnapshot state, WorldStepBatch batch,
        CountedAuthorityState inventory, LocalSpatialSnapshot space, ProductionSnapshot production,
        WorldMaterialProposalBook book, bool held, WorldActionCommand action, uint actionIndex, ulong sequence,
        IReadOnlySet<EntityId> worked, Func<EntityId, bool> occupied)
    {
        WorldMaterialProposalPlan Refuse(WorldActionRejection reason) =>
            new(inventory, production, book, reason, [], null, null);

        var delivery = new TrustedCommandDelivery(action.PrincipalId, action.OriginSessionId);
        if (action.WorldId != state.WorldId || action.BranchId != state.BranchId
            || !inventory.CanControl(delivery, action.ActorId))
        {
            return Refuse(WorldActionRejection.NotAuthorized);
        }
        // Every submission pins its own actor's presence, as ordinary actions do. The check follows
        // current authorization and precedes every proposal lookup, so it discloses no proposal.
        ActorPresence? presence = space.Actors.FirstOrDefault(value => value.ActorId == action.ActorId);
        if (presence is null)
        {
            return Refuse(WorldActionRejection.Unavailable);
        }
        if (presence.Revision != action.ExpectedActorRevision)
        {
            return Refuse(WorldActionRejection.RevisionConflict);
        }
        if (batch.Kind != WorldBatchKind.AdvancingStep || held || !WorldActorAvailability.PermitsAction(state, action))
        {
            return Refuse(WorldActionRejection.Unavailable);
        }
        return action.Intent switch
        {
            ProposeMaterialIntent propose => Create(state, inventory, production, book, action, propose, delivery,
                sequence, occupied, Refuse),
            AcceptMaterialProposalIntent accept => Accept(state, batch, inventory, space, production, book, action,
                accept, delivery, actionIndex, sequence, worked, occupied, Refuse),
            DeclineMaterialProposalIntent decline => Close(state, book, action, decline.ProposalId,
                WorldMaterialProposalOutcomeKind.Declined, sequence, inventory, production, Refuse),
            WithdrawMaterialProposalIntent withdraw => Close(state, book, action, withdraw.ProposalId,
                WorldMaterialProposalOutcomeKind.Withdrawn, sequence, inventory, production, Refuse),
            _ => throw new ArgumentException("Only saved proposal submissions are decided here.", nameof(action))
        };
    }

    private static WorldMaterialProposalPlan Create(WorldSnapshot state, CountedAuthorityState inventory,
        ProductionSnapshot production, WorldMaterialProposalBook book, WorldActionCommand action,
        ProposeMaterialIntent propose, TrustedCommandDelivery delivery, ulong sequence,
        Func<EntityId, bool> occupied, Func<WorldActionRejection, WorldMaterialProposalPlan> refuse)
    {
        ConsentedMaterialTerms terms = propose.Terms;
        if (terms.WorldId != state.WorldId || terms.BranchId != state.BranchId || terms.SenderActorId != action.ActorId
            || !Owns(inventory, terms.SenderActorId, terms.SourceCustodianId))
        {
            return refuse(WorldActionRejection.NotAuthorized);
        }
        ulong createdStep = checked(state.AdvancingSteps + 1);
        EntityId proposalId = WorldMaterialProposal.DeriveIdentity(action.CommandId);
        if (!Owns(inventory, terms.RecipientActorId, terms.DestinationCustodianId)
            || propose.ExpiresAtAdvancingStep <= createdStep
            || propose.ExpiresAtAdvancingStep - createdStep > WorldMaterialProposal.MaximumLifetimeSteps
            || !book.CanCreate || occupied(proposalId))
        {
            return refuse(WorldActionRejection.Unavailable);
        }
        // The pin must describe actual current stock; saving it still reserves nothing.
        WorldActionRejection pin = CheckPinnedLot(inventory, production, terms);
        if (pin != WorldActionRejection.None)
        {
            return refuse(pin);
        }
        var proposal = new WorldMaterialProposal(propose.ToGrant(action), delivery, sequence, createdStep,
            propose.ExpiresAtAdvancingStep);
        return new(inventory, production, book.Create(proposal), WorldActionRejection.None, [proposalId], null, null);
    }

    private static WorldMaterialProposalPlan Close(WorldSnapshot state, WorldMaterialProposalBook book,
        WorldActionCommand action, EntityId proposalId, WorldMaterialProposalOutcomeKind kind, ulong sequence,
        CountedAuthorityState inventory, ProductionSnapshot production,
        Func<WorldActionRejection, WorldMaterialProposalPlan> refuse)
    {
        WorldMaterialProposal? proposal = book.FindOpen(proposalId);
        bool withdraws = kind == WorldMaterialProposalOutcomeKind.Withdrawn;
        if (proposal is null || proposal.IsDue(state.AdvancingSteps) || !book.CanClose
            || (withdraws ? proposal.Terms.SenderActorId : proposal.Terms.RecipientActorId) != action.ActorId
            || withdraws && proposal.Grant.PrincipalId != action.PrincipalId)
        {
            return refuse(WorldActionRejection.Unavailable);
        }
        return new(inventory, production, book.Close(new(proposal, kind, sequence, action.CommandId, null)),
            WorldActionRejection.None, [], null, null);
    }

    private static WorldMaterialProposalPlan Accept(WorldSnapshot state, WorldStepBatch batch,
        CountedAuthorityState inventory, LocalSpatialSnapshot space, ProductionSnapshot production,
        WorldMaterialProposalBook book, WorldActionCommand action, AcceptMaterialProposalIntent accept,
        TrustedCommandDelivery recipientDelivery, uint actionIndex, ulong sequence, IReadOnlySet<EntityId> worked,
        Func<EntityId, bool> occupied, Func<WorldActionRejection, WorldMaterialProposalPlan> refuse)
    {
        WorldMaterialProposal? proposal = book.FindOpen(accept.ProposalId);
        if (proposal is null || proposal.Terms.RecipientActorId != action.ActorId
            || proposal.IsDue(state.AdvancingSteps) || !book.CanClose)
        {
            return refuse(WorldActionRejection.Unavailable);
        }
        ConsentedMaterialTerms terms = proposal.Terms;
        if (accept.Terms != terms || accept.GrantHash != proposal.GrantHash)
        {
            // The recipient approved something other than the saved offer; nothing is requoted.
            return refuse(WorldActionRejection.RevisionConflict);
        }
        // The saved grant stays valid only while its named principal's recorded binding still
        // controls the source actor. It is compared with current control, never treated as a delivery.
        EntityId senderId = terms.SenderActorId;
        if (!inventory.CanControl(proposal.SenderDelivery, senderId)
            || batch.Actions.Any(other => other.ActorId == senderId)
            || worked.Contains(senderId) || worked.Contains(action.ActorId)
            || !WorldActorAvailability.CanAct(state, senderId))
        {
            return refuse(WorldActionRejection.Unavailable);
        }
        ActorPresence? sender = space.Actors.FirstOrDefault(value => value.ActorId == senderId);
        ActorPresence? recipient = space.Actors.FirstOrDefault(value => value.ActorId == action.ActorId);
        if (sender is null || recipient is null)
        {
            return refuse(WorldActionRejection.Unavailable);
        }
        // The recipient's own presence revision was already enforced for every submission.
        if (sender.Revision != accept.ExpectedSenderRevision)
        {
            return refuse(WorldActionRejection.RevisionConflict);
        }
        if (!sender.Condition.SupportsFunction(BodyFunction.Manipulation)
            || !recipient.Condition.SupportsFunction(BodyFunction.Manipulation))
        {
            return refuse(WorldActionRejection.Unavailable);
        }
        if (space.QueryInteraction(sender.ActorId, recipient.ActorId) != SpatialInteraction.Reachable
            || space.QueryInteraction(recipient.ActorId, sender.ActorId) != SpatialInteraction.Reachable)
        {
            return refuse(WorldActionRejection.OutOfReach);
        }
        var approval = new ConsentedMaterialApproval(proposal.Grant, proposal.SenderDelivery,
            accept.ToMaterialAcceptance(action, proposal.CreationCommandId), recipientDelivery);
        if (!approval.IsApprovedByBothParticipants(inventory))
        {
            return refuse(WorldActionRejection.Unavailable);
        }
        // Only this proposal's own grant and the current acceptance are exempt from occupancy.
        bool MaterialOccupied(EntityId identity) => identity != proposal.CreationCommandId
            && identity != action.CommandId && occupied(identity);
        try
        {
            ConsentedMaterialTransferOutcome outcome = ConsentedMaterialTransferStep.Apply(inventory, production.Lots,
                approval, MaterialOccupied, MaterialOccupied);
            if (outcome.Rejection != WorldActionRejection.None || outcome.MovedLotId is not { } movedLotId)
            {
                return refuse(outcome.Rejection == WorldActionRejection.None
                    ? WorldActionRejection.Unavailable : outcome.Rejection);
            }
            // Custody movement is not a craft or a use, so the production revision keeps its meaning.
            var candidate = new ProductionSnapshot(production.WorldId, production.BranchId, production.Revision,
                production.Recipes, production.Batches, outcome.ProducedLots,
                production.SupportsConsumption ? production.ConsumedItems : null, production.Continuation);
            candidate.ValidateForAuthority(outcome.Inventory);
            WorldMaterialProposalSettlement settlement = Witness(actionIndex, approval, recipientDelivery, movedLotId,
                inventory, production, outcome.Inventory, candidate);
            var closed = new WorldMaterialProposalOutcome(proposal, WorldMaterialProposalOutcomeKind.Accepted,
                sequence, action.CommandId, settlement);
            return new(outcome.Inventory, candidate, book.Close(closed), WorldActionRejection.None,
                movedLotId != terms.LotId ? [movedLotId] : [], settlement, senderId);
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            return refuse(WorldActionRejection.Unavailable);
        }
    }

    private static WorldActionRejection CheckPinnedLot(CountedAuthorityState inventory, ProductionSnapshot production,
        ConsentedMaterialTerms terms)
    {
        if (terms.LotKind == ConsentedMaterialLotKind.Produced)
        {
            TransformedLot? lot = production.Lots.FirstOrDefault(value => value.LotId == terms.LotId);
            if (lot is null || lot.CustodianId != terms.SourceCustodianId || !lot.Definition.Equals(terms.Definition)
                || lot.Quantity < terms.Quantity)
            {
                return WorldActionRejection.Unavailable;
            }
            return lot.Revision == terms.ExpectedLotRevision ? WorldActionRejection.None : WorldActionRejection.RevisionConflict;
        }
        CountedSourceLot? raw = inventory.Custody.Lots.FirstOrDefault(value => value.LotId == terms.LotId);
        if (raw is null || raw.CustodianId != terms.SourceCustodianId || !raw.Definition.Equals(terms.Definition)
            || raw.Quantity < terms.Quantity)
        {
            return WorldActionRejection.Unavailable;
        }
        return raw.Revision == terms.ExpectedLotRevision ? WorldActionRejection.None : WorldActionRejection.RevisionConflict;
    }

    private static bool Owns(CountedAuthorityState inventory, EntityId actorId, EntityId custodianId) =>
        inventory.Policies.Any(policy => policy.CustodianId == custodianId && policy.OwnerActorId == actorId);

    private static WorldMaterialProposalSettlement Witness(uint actionIndex, ConsentedMaterialApproval approval,
        TrustedCommandDelivery recipientDelivery, EntityId movedLotId, CountedAuthorityState inventory,
        ProductionSnapshot production, CountedAuthorityState candidateInventory, ProductionSnapshot candidateProduction)
    {
        if (approval.Command.Terms.LotKind == ConsentedMaterialLotKind.Produced)
        {
            ProductionOutputInterval moved = candidateProduction.Lots.Single(value => value.LotId == movedLotId).Output;
            return new(actionIndex, approval.Acceptance, recipientDelivery, movedLotId, moved.BatchId, moved.OutputIndex,
                moved.Start, moved.End, inventory.ContentHash, production.ContentHash, candidateInventory.ContentHash,
                candidateProduction.ContentHash);
        }
        SourceInterval source = candidateInventory.Custody.GetLot(movedLotId).Source;
        return new(actionIndex, approval.Acceptance, recipientDelivery, movedLotId, source.SourceId, 0, source.Start,
            source.End, inventory.ContentHash, production.ContentHash, candidateInventory.ContentHash,
            candidateProduction.ContentHash);
    }
}
