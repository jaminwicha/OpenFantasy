using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Terrain;

/// <summary>Qualifies actual mining and gathering against one terrain, material and shared work owner.</summary>
public sealed class MaterialExcavationPlanner
{
    public MaterialExcavationPlan Plan(WorldSnapshot sourceWorld, MaterialExcavationState state, WorldActionCommand command,
        bool isAdvancingStep, bool held, Func<EntityId, bool> historicallyOccupied) =>
        PlanCore(sourceWorld, state, command, isAdvancingStep, held, historicallyOccupied, gather: false);

    public MaterialExcavationPlan PlanGather(WorldSnapshot sourceWorld, MaterialExcavationState state, WorldActionCommand command,
        Func<EntityId, bool> historicallyOccupied)
    {
        ArgumentNullException.ThrowIfNull(sourceWorld);
        return PlanCore(sourceWorld, state, command, true, sourceWorld.HasReadingHold, historicallyOccupied, gather: true);
    }

    private static MaterialExcavationPlan PlanCore(WorldSnapshot world, MaterialExcavationState state, WorldActionCommand command,
        bool advancing, bool held, Func<EntityId, bool> historical, bool gather)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(historical);
        if (state.Production.Continuation?.SupportsExcavation != true)
            throw new ArgumentException("Select excavation continuation from the actual complete owner before joined work.", nameof(state));
        if (gather ? command.Intent is not GatherIntent : command.Intent is not ExcavateIntent)
            throw new ArgumentException("The explicit work entrypoint requires its corresponding action intent.", nameof(command));
        MaterialExcavationPlan Reject(WorldActionRejection reason) => new(state, state, reason);
        if (!advancing || held || world.HasReadingHold || state.Production.MaterialHistory!.Operations.Count == MaterialOperationHistory.MaximumOperations)
            return Reject(WorldActionRejection.Unavailable);
        bool Occupied(EntityId id) => historical(id) || state.ContainsIdentity(id) || world.IdentityIndex.Contains(id);
        try
        {
            var frontier = ExcavationMaterialBoundary.Observe(world, command.ActorId);
            if (!state.Boundary.CanAdvance(frontier)) return Reject(WorldActionRejection.RevisionConflict);
            if (Occupied(command.CommandId)) return Reject(WorldActionRejection.Unavailable);
            LocalSpatialSnapshot space = state.CurrentSpace(world);
            GatheringSnapshot gathering = state.Gathering.AdvanceTo(world.ProcessIntervals);
            OpenFantasy.Core.Authority.CountedAuthorityState inventory;
            GatheringSnapshot afterGathering;
            LocalSpatialSnapshot afterSpace;
            EntityId? spawned;
            GatherIssueEvidence? gatherEvidence = null;
            if (gather)
            {
                EntityId issued = EntityId.DeriveSpawned(command.CommandId, 0);
                if (Occupied(issued)) return Reject(WorldActionRejection.Unavailable);
                var plan = new GatherPlanner().PlanWithEvidence(world, state.Inventory, gathering, command, space);
                if (plan.Rejection != WorldActionRejection.None) return Reject(plan.Rejection);
                inventory = plan.CandidateInventory;
                afterGathering = plan.CandidateGathering;
                afterSpace = space;
                spawned = plan.SpawnedId;
                gatherEvidence = plan.Evidence;
            }
            else
            {
                var plan = new ExcavationPlanner().PlanContinued(world, state.Inventory, space, gathering, state.Production,
                    state.Excavation, command, advancing, held, Occupied, state.Physical);
                if (!plan.Succeeded) return Reject(plan.Rejection);
                inventory = plan.Inventory;
                afterGathering = plan.Gathering;
                afterSpace = plan.Space;
                spawned = plan.SpawnedId;
            }
            if (!MaterialContinuationCommit.HasCapacity(inventory, state.Production)) return Reject(WorldActionRejection.Unavailable);
            var boundary = state.Boundary.Advance(command, frontier);
            if (!boundary.Regions.Select(value => value.ComputeContentHash()).SequenceEqual(afterSpace.Regions.Select(value => value.ComputeContentHash())))
                throw new ArgumentException("The actual planner must preserve the exact replayed terrain transition.");
            CountedSourceLot? issuedLot = spawned is { } identity ? inventory.Custody.GetLot(identity) : null;
            var evidence = new ExcavationWorkEvidence(world, state, space, gathering, boundary, afterSpace,
                afterGathering, inventory, command, issuedLot, gatherEvidence);
            var production = MaterialContinuationCommit.Prepare(inventory, state.Production,
                new MaterialExcavationWorkOperation(checked(state.Production.Revision + 1), evidence)).CandidateProduction;
            var candidate = new MaterialExcavationState(inventory, production, afterGathering, afterSpace, boundary);
            return new(state, candidate, createdIds: spawned is { } created ? [created] : [], evidence: evidence);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException or ProductionFormatException)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
    }
}
