using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Construction;

/// <summary>Qualifies one present builder's work, retaining or committing the complete physical candidate atomically.</summary>
public sealed class ConstructionPlanner
{
    public ConstructionPlan Plan(WorldSnapshot world, CountedAuthorityState inventory, ProductionSnapshot production,
        ConstructionSnapshot construction, LocalSpatialSnapshot space, WorldActionCommand command,
        bool isAdvancingStep, bool held, Func<EntityId, bool>? occupied = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(construction);
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(command);
        if (production.IsContinued && production.Continuation?.SupportsCompleteWorld != true)
        {
            throw new ArgumentException("Construction requires qualified archived producer and ingredient witnesses.", nameof(production));
        }
        return PlanQualified(world, inventory, production, construction, space, command, isAdvancingStep, held, occupied, false);
    }

    internal ConstructionPlan PlanContinued(WorldSnapshot world, CountedAuthorityState inventory, ProductionSnapshot production,
        ConstructionSnapshot construction, LocalSpatialSnapshot space, WorldActionCommand command,
        bool isAdvancingStep, bool held, Func<EntityId, bool> occupied) =>
        PlanQualified(world, inventory, production, construction, space, command, isAdvancingStep, held, occupied, true);

    private static ConstructionPlan PlanQualified(WorldSnapshot world, CountedAuthorityState inventory, ProductionSnapshot production,
        ConstructionSnapshot construction, LocalSpatialSnapshot space, WorldActionCommand command,
        bool isAdvancingStep, bool held, Func<EntityId, bool>? occupied, bool continued)
    {
        ConstructionWorkResult result = ConstructionWorkKernel.Plan(new LiveConstructionDecisionSource(world),
            inventory, production, construction, new LiveConstructionGeometry(space), command,
            isAdvancingStep, held, occupied, continued, continued ? request => new ConstructionCompletionEvidence(
                world, space, command, inventory, request.AfterInventory, production, construction,
                request.Completed, request.Project, request.Batch) : null);
        if (result.CandidateStructure is not null && result.CompleteSpace is null)
        {
            throw new InvalidOperationException("Ordinary construction completion requires its fully admitted spatial candidate.");
        }
        return new(result.CandidateInventory, result.CandidateProduction, result.CandidateConstruction,
            result.CompleteSpace ?? space, result.Rejection, result.CreatedIds, result.Evidence);
    }
}
