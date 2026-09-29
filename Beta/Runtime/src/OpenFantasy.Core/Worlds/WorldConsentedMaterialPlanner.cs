using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// One decided paired material transaction: the complete candidate owners with their evidence, or the
/// untouched originals and one refusal per submission. Nothing is published by planning.
/// </summary>
public sealed class WorldConsentedMaterialPlan
{
    internal WorldConsentedMaterialPlan(CountedAuthorityState originalInventory, ProductionSnapshot originalProduction,
        CountedAuthorityState candidateInventory, ProductionSnapshot candidateProduction,
        WorldActionRejection offerRejection, WorldActionRejection acceptanceRejection,
        WorldConsentedTransferEvidence? evidence)
    {
        bool applied = offerRejection == WorldActionRejection.None && acceptanceRejection == WorldActionRejection.None;
        if (applied != (evidence is not null) || !applied && (offerRejection == WorldActionRejection.None
            || acceptanceRejection == WorldActionRejection.None))
        {
            throw new ArgumentException("A paired transaction applies for both submissions with evidence, or for neither.");
        }
        BaseInventoryHash = originalInventory.ContentHash;
        BaseProductionHash = originalProduction.ContentHash;
        CandidateInventory = candidateInventory;
        CandidateProduction = candidateProduction;
        OfferRejection = offerRejection;
        AcceptanceRejection = acceptanceRejection;
        Evidence = evidence;
    }

    public ContentHash BaseInventoryHash { get; }
    public ContentHash BaseProductionHash { get; }
    public CountedAuthorityState CandidateInventory { get; }
    public ProductionSnapshot CandidateProduction { get; }
    public WorldActionRejection OfferRejection { get; }
    public WorldActionRejection AcceptanceRejection { get; }
    public WorldConsentedTransferEvidence? Evidence { get; }
    public bool IsApplied => Evidence is not null;
    public EntityId? MovedLotId => Evidence?.MovedLotId;

    /// <summary>Returns the candidates only for the exact complete owners that were decided.</summary>
    public (CountedAuthorityState Inventory, ProductionSnapshot Production) ApplyTo(
        CountedAuthorityState currentInventory, ProductionSnapshot currentProduction)
    {
        ArgumentNullException.ThrowIfNull(currentInventory);
        ArgumentNullException.ThrowIfNull(currentProduction);
        if (!IsApplied)
        {
            throw new InvalidOperationException("A refused paired transaction has no candidate to apply.");
        }
        if (currentInventory.ContentHash != BaseInventoryHash || currentProduction.ContentHash != BaseProductionHash)
        {
            throw new CustodyConflictException(CustodyConflict.StalePlan);
        }
        return (CandidateInventory, CandidateProduction);
    }
}

/// <summary>
/// Decides one paired offer and acceptance against the world's actual owners. Each submission is
/// decided with its own principal, session and actor; neither is derived from the other's payload.
/// Control is decided before anything about stock, identities or presence is disclosed. Both bodies,
/// both presence revisions, reach and the advancing, unheld step are required before the shared
/// material decision step runs. The planner performs no IO and publishes nothing.
/// </summary>
public sealed class WorldConsentedMaterialPlanner
{
    /// <param name="historicallyOccupied">Complete permanent identity occupancy of closed history.</param>
    public WorldConsentedMaterialPlan Plan(WorldSnapshot world, WorldActionCommand offer, WorldActionCommand acceptance,
        WorldBatchKind kind, TrustedCommandDelivery senderDelivery, TrustedCommandDelivery recipientDelivery,
        Func<EntityId, bool>? historicallyOccupied = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(acceptance);
        ArgumentNullException.ThrowIfNull(senderDelivery);
        ArgumentNullException.ThrowIfNull(recipientDelivery);
        if (world.Coordination?.Anchor is not null && historicallyOccupied is null)
        {
            throw new ArgumentException("A continued world requires complete historical identity occupancy.", nameof(historicallyOccupied));
        }
        if (world.Production is not { } production)
        {
            throw new InvalidOperationException("This saved profile does not interpret material transfers.");
        }
        bool Occupied(EntityId identity) => world.IdentityIndex.Contains(identity)
            || historicallyOccupied?.Invoke(identity) == true;
        uint offerIndex = offer.ActorId.CompareTo(acceptance.ActorId) < 0 ? 0U : 1U;
        return Decide(world, world.Inventory, world.Space, production, world.HasReadingHold, kind,
            offer, offerIndex, senderDelivery, acceptance, 1U - offerIndex, recipientDelivery,
            checked(world.Revision + 1), Occupied);
    }

    internal static WorldConsentedMaterialPlan Decide(WorldSnapshot world, CountedAuthorityState inventory,
        LocalSpatialSnapshot space, ProductionSnapshot production, bool held, WorldBatchKind kind,
        WorldActionCommand offer, uint offerIndex, TrustedCommandDelivery senderDelivery,
        WorldActionCommand acceptance, uint acceptanceIndex, TrustedCommandDelivery recipientDelivery,
        ulong sequence, Func<EntityId, bool> occupied)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(acceptance);
        if (offer.Intent is not OfferMaterialIntent offered || acceptance.Intent is not AcceptMaterialIntent accepted)
        {
            throw new ArgumentException("A paired material transaction needs one offer and one acceptance.");
        }
        if (production.IsContinued && production.Continuation?.SupportsConsentedWorld != true)
        {
            throw new InvalidOperationException("Continued production does not interpret paired material transfers.");
        }
        WorldConsentedMaterialPlan Refuse(WorldActionRejection offerReason, WorldActionRejection acceptanceReason) =>
            new(inventory, production, inventory, production, offerReason, acceptanceReason, null);
        WorldConsentedMaterialPlan RefuseBoth(WorldActionRejection reason) => Refuse(reason, reason);

        bool senderControls = offer.PrincipalId == senderDelivery.PrincipalId
            && offer.OriginSessionId == senderDelivery.SessionId && inventory.CanControl(senderDelivery, offer.ActorId);
        bool recipientControls = acceptance.PrincipalId == recipientDelivery.PrincipalId
            && acceptance.OriginSessionId == recipientDelivery.SessionId && inventory.CanControl(recipientDelivery, acceptance.ActorId);
        if (!senderControls || !recipientControls)
        {
            // A participant whose own control holds learns only that the pair could not complete.
            return Refuse(senderControls ? WorldActionRejection.Unavailable : WorldActionRejection.NotAuthorized,
                recipientControls ? WorldActionRejection.Unavailable : WorldActionRejection.NotAuthorized);
        }

        ConsentedMaterialTerms terms = offered.Terms;
        var approval = new ConsentedMaterialApproval(offered.ToMaterialCommand(offer), senderDelivery,
            accepted.ToMaterialAcceptance(acceptance), recipientDelivery);
        if (offer.WorldId != world.WorldId || offer.BranchId != world.BranchId
            || acceptance.WorldId != world.WorldId || acceptance.BranchId != world.BranchId
            || terms.SenderActorId != offer.ActorId || terms.RecipientActorId != acceptance.ActorId
            || !approval.IsApprovedByBothParticipants(inventory))
        {
            return RefuseBoth(WorldActionRejection.NotAuthorized);
        }
        if (kind != WorldBatchKind.AdvancingStep || held)
        {
            return RefuseBoth(WorldActionRejection.Unavailable);
        }

        ActorPresence? sender = space.Actors.FirstOrDefault(value => value.ActorId == offer.ActorId);
        ActorPresence? recipient = space.Actors.FirstOrDefault(value => value.ActorId == acceptance.ActorId);
        if (sender is null || recipient is null)
        {
            return RefuseBoth(WorldActionRejection.Unavailable);
        }
        if (sender.Revision != offer.ExpectedActorRevision || recipient.Revision != offered.ExpectedRecipientRevision
            || recipient.Revision != acceptance.ExpectedActorRevision || sender.Revision != accepted.ExpectedSenderRevision)
        {
            return RefuseBoth(WorldActionRejection.RevisionConflict);
        }
        if (!WorldActorAvailability.PermitsAction(world, offer) || !WorldActorAvailability.PermitsAction(world, acceptance)
            || !sender.Condition.SupportsFunction(BodyFunction.Manipulation)
            || !recipient.Condition.SupportsFunction(BodyFunction.Manipulation))
        {
            return RefuseBoth(WorldActionRejection.Unavailable);
        }
        if (space.QueryInteraction(sender.ActorId, recipient.ActorId) != SpatialInteraction.Reachable
            || space.QueryInteraction(recipient.ActorId, sender.ActorId) != SpatialInteraction.Reachable)
        {
            return RefuseBoth(WorldActionRejection.OutOfReach);
        }

        try
        {
            ConsentedMaterialTransferOutcome outcome = ConsentedMaterialTransferStep.Apply(inventory, production.Lots,
                approval, occupied, occupied);
            if (outcome.Rejection != WorldActionRejection.None || outcome.MovedLotId is not { } movedLotId)
            {
                return RefuseBoth(outcome.Rejection);
            }
            // Custody movement is not a craft or a use, so the production revision keeps its meaning.
            var candidate = new ProductionSnapshot(production.WorldId, production.BranchId, production.Revision,
                production.Recipes, production.Batches, outcome.ProducedLots,
                production.SupportsConsumption ? production.ConsumedItems : null, production.Continuation);
            candidate.ValidateForAuthority(outcome.Inventory);
            WorldConsentedTransferEvidence evidence = Witness(sequence, offerIndex, acceptanceIndex, approval,
                movedLotId, inventory, production, outcome.Inventory, candidate);
            return new(inventory, production, outcome.Inventory, candidate, WorldActionRejection.None,
                WorldActionRejection.None, evidence);
        }
        catch (OverflowException)
        {
            return RefuseBoth(WorldActionRejection.Unavailable);
        }
    }

    private static WorldConsentedTransferEvidence Witness(ulong sequence, uint offerIndex, uint acceptanceIndex,
        ConsentedMaterialApproval approval, EntityId movedLotId, CountedAuthorityState inventory,
        ProductionSnapshot production, CountedAuthorityState candidateInventory, ProductionSnapshot candidateProduction)
    {
        if (approval.Command.Terms.LotKind == ConsentedMaterialLotKind.Produced)
        {
            ProductionOutputInterval moved = candidateProduction.Lots.Single(value => value.LotId == movedLotId).Output;
            return new(sequence, offerIndex, acceptanceIndex, approval, movedLotId, moved.BatchId, moved.OutputIndex,
                moved.Start, moved.End, inventory.ContentHash, production.ContentHash, candidateInventory.ContentHash,
                candidateProduction.ContentHash);
        }
        SourceInterval source = candidateInventory.Custody.GetLot(movedLotId).Source;
        return new(sequence, offerIndex, acceptanceIndex, approval, movedLotId, source.SourceId, 0, source.Start,
            source.End, inventory.ContentHash, production.ContentHash, candidateInventory.ContentHash,
            candidateProduction.ContentHash);
    }
}
