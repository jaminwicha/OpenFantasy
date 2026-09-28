using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Production;

/// <summary>Authenticates the existing raw transfer operation and records its exact place in material history.</summary>
public sealed class MaterialTransferPlanner
{
    public MaterialOperationPlan Plan(CountedAuthorityState inventory, ProductionSnapshot production,
        CommandEnvelope command, TrustedCommandDelivery delivery, Func<EntityId, bool> historical)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(delivery);
        MaterialContinuationCommit.Require(inventory, production, historical);
        MaterialOperationPlan Reject(WorldActionRejection reason) => new(inventory, production, reason);
        if (production.MaterialHistory!.Operations.Count == MaterialOperationHistory.MaximumOperations
            || MaterialContinuationCommit.Occupied(inventory, production, historical, command.CommandId))
            return Reject(WorldActionRejection.Unavailable);
        try
        {
            ulong sequence = checked(production.Revision + 1);
            AuthorityDecision result = new CountedTransferAuthority().Decide(inventory, command, delivery, new(inventory.WorldId, sequence));
            if (result.Receipt.Status != CommandReceiptStatus.Applied)
                return Reject(result.Receipt.RejectionReason == CommandRejectionReason.NotAuthorized
                    ? WorldActionRejection.NotAuthorized : WorldActionRejection.Unavailable);
            if (result.MovedLotId is { } moved && moved != command.Payload.LotId
                && MaterialContinuationCommit.Occupied(inventory, production, historical, moved)
                || !MaterialContinuationCommit.HasCapacity(result.Candidate, production))
                return Reject(WorldActionRejection.Unavailable);
            return MaterialContinuationCommit.Prepare(result.Candidate, production, new MaterialTransferOperation(sequence, command, delivery));
        }
        catch (OverflowException) { return Reject(WorldActionRejection.Unavailable); }
        catch (ProductionFormatException error) when (error.Failure == ProductionFormatFailure.LimitExceeded)
        { return Reject(WorldActionRejection.Unavailable); }
    }
}
