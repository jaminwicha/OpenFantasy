using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Production;

/// <summary>Moves and reserves actual existing raw intervals; it never issues new material.</summary>
public sealed class MaterialSourceDeliveryPlanner
{
    public MaterialOperationPlan Plan(CountedAuthorityState inventory, ProductionSnapshot production,
        TrustedCommandDelivery delivery, EntityId actorId, EntityId commandId, SourceDeliveryRequest request,
        Func<EntityId, bool> historical)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ArgumentNullException.ThrowIfNull(request);
        MaterialContinuationCommit.Require(inventory, production, historical);
        MaterialOperationPlan Reject(WorldActionRejection reason) => new(inventory, production, reason);
        if (production.MaterialHistory!.Operations.Count == MaterialOperationHistory.MaximumOperations)
            return Reject(WorldActionRejection.Unavailable);
        SourceDeliveryPlan result = SourceDeliveryPlanner.Prepare(inventory, delivery, actorId, commandId, request,
            id => production.ContainsIdentity(id) || historical(id), candidate =>
            {
                if (!MaterialContinuationCommit.HasCapacity(candidate, production))
                    throw new ArgumentException("Combined material cannot exceed destination capacity.");
            });
        if (!result.Succeeded) return Reject(result.Rejection);
        try
        {
            return MaterialContinuationCommit.Prepare(result.Candidate, production,
                new MaterialDeliveryOperation(checked(production.Revision + 1), commandId, actorId, delivery, request));
        }
        catch (OverflowException) { return Reject(WorldActionRejection.Unavailable); }
        catch (ProductionFormatException error) when (error.Failure == ProductionFormatFailure.LimitExceeded)
        { return Reject(WorldActionRejection.Unavailable); }
    }
}
