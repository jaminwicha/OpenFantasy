using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Authority.ConsentedMaterial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Production;

/// <summary>
/// Decides one bilateral approved gift of actual raw or produced stock and records its exact place
/// and result in ordered material history. The planner performs no IO, stores no offer, proves no
/// spatial reach and advances no clock.
/// </summary>
public sealed class ConsentedMaterialTransferPlanner
{
    /// <param name="historical">
    /// Complete-history identity occupancy for this world, including retired lots and every earlier
    /// command, acceptance and spawned identity. A current segment alone cannot establish freshness.
    /// </param>
    public ConsentedMaterialTransferPlan Plan(CountedAuthorityState inventory, ProductionSnapshot production,
        ConsentedMaterialApproval approval, Func<EntityId, bool> historical)
    {
        ArgumentNullException.ThrowIfNull(approval);
        MaterialContinuationCommit.Require(inventory, production, historical);
        if (production.Continuation is not { SupportsConsentedTransfer: true })
        {
            throw new ArgumentException("Consented material transfer requires its explicit continued capability.",
                nameof(production));
        }

        ConsentedMaterialTransferPlan Reject(WorldActionRejection reason) =>
            new(inventory, production, inventory, production, approval, reason, null);
        // Authentication precedes every availability report, including a full history window.
        if (!approval.IsApprovedByBothParticipants(inventory))
        {
            return Reject(WorldActionRejection.NotAuthorized);
        }

        if (production.MaterialHistory!.Operations.Count == MaterialOperationHistory.MaximumOperations)
        {
            return Reject(WorldActionRejection.Unavailable);
        }

        try
        {
            bool Occupied(EntityId identity) =>
                MaterialContinuationCommit.Occupied(inventory, production, historical, identity);
            ConsentedMaterialTransferOutcome outcome = ConsentedMaterialTransferStep.Apply(inventory,
                production.Lots, approval, Occupied, Occupied);
            if (outcome.Rejection != WorldActionRejection.None || outcome.MovedLotId is not { } movedLotId)
            {
                return Reject(outcome.Rejection);
            }

            ulong sequence = checked(production.Revision + 1);
            var operation = new MaterialConsentedTransferOperation(sequence, approval, movedLotId);
            var candidate = new ProductionSnapshot(production.WorldId, production.BranchId, sequence,
                production.Recipes, production.Batches, outcome.ProducedLots,
                production.SupportsConsumption ? production.ConsumedItems : null, production.Continuation,
                production.MaterialHistory.Append(production.Continuation, operation));
            candidate.ValidateForAuthority(outcome.Inventory);
            _ = candidate.ContentHash;
            return new(inventory, production, outcome.Inventory, candidate, approval,
                WorldActionRejection.None, movedLotId);
        }
        catch (OverflowException)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        catch (ProductionFormatException error) when (error.Failure == ProductionFormatFailure.LimitExceeded)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
    }
}
