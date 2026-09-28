using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Production;

/// <summary>Composes a genuine gather plan with exact material history; it admits no complete continued world.</summary>
public sealed class MaterialGatherPlanner
{
    public MaterialOperationPlan Plan(WorldSnapshot sourceWorld, CountedAuthorityState inventory,
        ProductionSnapshot production, GatheringSnapshot actualGathering, WorldActionCommand command,
        Func<EntityId, bool> historical)
    {
        ArgumentNullException.ThrowIfNull(sourceWorld);
        ArgumentNullException.ThrowIfNull(actualGathering);
        ArgumentNullException.ThrowIfNull(command);
        MaterialContinuationCommit.Require(inventory, production, historical);
        if (production.Continuation?.SupportsExcavation == true)
            throw new ArgumentException("Excavation and gathering must share their actual work-opportunity owner.", nameof(production));
        MaterialOperationPlan Reject(WorldActionRejection reason) => new(inventory, production, reason, gathering: actualGathering);
        if (production.MaterialHistory!.Operations.Count == MaterialOperationHistory.MaximumOperations)
            return Reject(WorldActionRejection.Unavailable);
        GatheringSnapshot? prior = production.MaterialHistory.Gathering;
        if (prior is null || sourceWorld.ProcessIntervals < prior.AdvancingStep
            || actualGathering.ContentHash != prior.AdvanceTo(sourceWorld.ProcessIntervals).ContentHash)
            return Reject(WorldActionRejection.RevisionConflict);
        EntityId spawned = EntityId.DeriveSpawned(command.CommandId, 0);
        if (MaterialContinuationCommit.Occupied(inventory, production, historical, command.CommandId)
            || MaterialContinuationCommit.Occupied(inventory, production, historical, spawned)
            || sourceWorld.IdentityIndex.Contains(command.CommandId))
            return Reject(WorldActionRejection.Unavailable);
        GatherPlan plan = new GatherPlanner().PlanWithEvidence(sourceWorld, inventory, actualGathering, command);
        if (plan.Rejection != WorldActionRejection.None) return Reject(plan.Rejection);
        if (!MaterialContinuationCommit.HasCapacity(plan.CandidateInventory, production))
            return Reject(WorldActionRejection.Unavailable);
        GatherIssueEvidence evidence = plan.Evidence ?? throw new InvalidOperationException("Accepted gather requires its actual issuing proof.");
        try
        {
            return MaterialContinuationCommit.Prepare(plan.CandidateInventory, production,
                new MaterialGatherOperation(checked(production.Revision + 1), evidence), evidence, plan.CandidateGathering);
        }
        catch (OverflowException) { return Reject(WorldActionRejection.Unavailable); }
        catch (ProductionFormatException error) when (error.Failure == ProductionFormatFailure.LimitExceeded)
        { return Reject(WorldActionRejection.Unavailable); }
    }
}
