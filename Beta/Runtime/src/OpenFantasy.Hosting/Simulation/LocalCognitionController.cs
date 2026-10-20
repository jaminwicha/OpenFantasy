using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Cognition;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>
/// Host policy for the cognition interpretation. It proposes a resident's own reconsideration, a look at a source in
/// sight, and ordinary approach and gathering for a gathering intention. It reads only facts the resident owns or has
/// acquired, plus physical feasibility; world truth never becomes a belief here. Every effect is still an ordinary
/// command the world admits or refuses, and replay never runs this policy.
/// </summary>
internal sealed class LocalCognitionController
{
    internal const string PolicyVersion = "local.delayed-supply.v1";

    /// <summary>Authored motive of the first episode: a small personal stock while supplies are delayed.</summary>
    internal const ulong PersonalStockTarget = 2;

    private readonly GatherPlanner _gathering = new();

    internal static bool Applies(WorldSnapshot state) => state.Cognition is not null;

    /// <summary>The resident's funded commitment, known from its own intention rather than from world quest truth.</summary>
    internal static ConstructionProject? FundedCommitment(WorldSnapshot state, EntityId actorId) =>
        state.Cognition!.For(actorId).Intention is { Kind: IntentionKind.WorkFundedProject } intention
            ? OwnProject(state, actorId) is { } project && project.ProjectId == intention.SubjectId ? project : null
            : null;

    /// <summary>
    /// One discrete action for a resident with its own unfinished project: a due decision first, then gathering, then
    /// a look. Residents without such a project are not driven by this first episode's policy.
    /// </summary>
    internal WorldActionCommand? SelectAction(WorldSnapshot state, WorldResident resident, Func<EntityId> newIdentity)
    {
        if (state.HasReadingHold || OwnProject(state, resident.ActorId) is null) return null;
        ActorCognition cognition = state.Cognition!.For(resident.ActorId);
        if (Decide(state, resident.ActorId, cognition) is { } decision)
            return Command(state, resident.ActorId, newIdentity, new RecordCognitionDecisionIntent(cognition.Revision, decision));
        if (cognition.Intention is { Kind: IntentionKind.GatherPersonalStock } gather
            && Gather(state, resident.ActorId, gather.SubjectId, newIdentity) is { } swing)
            return swing;
        return Look(state, resident.ActorId, cognition, newIdentity);
    }

    /// <summary>Ordinary approach toward the standing cell of the source the resident intends to gather.</summary>
    internal WorldMovementInput? SelectMovement(WorldSnapshot state, WorldResident resident)
    {
        if (state.Cognition!.For(resident.ActorId).Intention is not { Kind: IntentionKind.GatherPersonalStock } gather
            || Standing(state, resident.ActorId, gather.SubjectId) is not { } standing
            || state.Space.GetActor(resident.ActorId).Feet == standing)
            return null;
        GroundRouteResult route = state.Space.QueryGroundRoute(resident.ActorId, standing);
        if (route.Status != GroundRouteStatus.Reachable || route.NextWaypoint is not { } next) return null;
        SubunitPosition feet = state.Space.GetActor(resident.ActorId).Feet;
        long x = Math.Clamp(checked(next.X - feet.X), -WorldMovementInput.DisplacementPerStep, WorldMovementInput.DisplacementPerStep);
        long y = x != 0 ? 0 : Math.Clamp(checked(next.Y - feet.Y), -WorldMovementInput.DisplacementPerStep, WorldMovementInput.DisplacementPerStep);
        return x == 0 && y == 0 ? null : new WorldMovementInput(Delivery(state, resident.ActorId), resident.ActorId, x, y);
    }

    /// <summary>
    /// The policy's decision at a due, unsuppressed wake for a resident with its own unfinished project, or null when
    /// nothing would change. Feasibility facts are the resident's own: its tools, stock, effort and its known sources'
    /// physical reachability.
    /// </summary>
    internal static CognitionDecision? Decide(WorldSnapshot state, EntityId actorId, ActorCognition cognition)
    {
        if (cognition.Wake is not { Suppression: WakeSuppression.None } wake || wake.DueStep > state.AdvancingSteps
            || OwnProject(state, actorId) is not { } project)
            return null;
        CognitionBeliefs beliefs = cognition.Beliefs;
        var known = beliefs.Sources.Select(source => Node(state, source.SourceId)).OfType<GatherNodeState>().ToList();
        var reachable = known.Where(node => Standing(state, actorId, node.NodeId) is not null).Select(node => node.NodeId).ToHashSet();
        bool tool = known.Any(node => Tool(state, actorId, node) is not null);
        ulong stock = known.Select(node => node.Definition.YieldDefinition).Distinct()
            .Aggregate(0UL, (total, definition) => checked(total + Holding(state, actorId, definition)));
        var situation = new CognitionSituation(state.AdvancingSteps, project.ProjectId, project.QuestId, !project.IsCompleted,
            tool, stock, PersonalStockTarget, state.Gathering!.GetActorWork(actorId).Effort >= GatherRulesV1.EffortPerSwing,
            state.HasReadingHold, reachable);
        CognitionDecision decision = CognitionPolicy.Decide(cognition, situation, state.Calendar);
        return ReferenceEquals(cognition.Adopt(decision, state.Calendar), cognition) ? null : decision;
    }

    private WorldActionCommand? Gather(WorldSnapshot state, EntityId actorId, EntityId nodeId, Func<EntityId> newIdentity)
    {
        if (Node(state, nodeId) is not { } node || Tool(state, actorId, node) is not { } tool
            || state.Gathering!.GetActorWork(actorId).Effort < GatherRulesV1.EffortPerSwing
            || ResourceInteractionQueries.Query(state.Space, actorId, node.Feet) != SpatialInteraction.Reachable)
            return null;
        WorldActionCommand command = Command(state, actorId, newIdentity,
            new GatherIntent(nodeId, node.Revision, tool.LotId, tool.Revision));
        // Private preflight avoids endless refused receipts; the selected batch still rechecks everything.
        GatherPlan planned = _gathering.Plan(state, state.Inventory, state.Gathering!, command);
        return planned.Rejection == WorldActionRejection.None ? command : null;
    }

    /// <summary>A look at the first source in sight the resident has never observed, in identity order.</summary>
    private static WorldActionCommand? Look(WorldSnapshot state, EntityId actorId, ActorCognition cognition, Func<EntityId> newIdentity)
    {
        var known = cognition.Beliefs.Sources.Select(source => source.SourceId).ToHashSet();
        GatherNodeState? unseen = state.Gathering!.Nodes.OrderBy(node => node.NodeId).FirstOrDefault(node => !known.Contains(node.NodeId)
            && ResourceInteractionQueries.QueryWithin(state.Space, actorId, node.Feet, WorldCognitionPerception.ObservationRangeSubunits)
                == SpatialInteraction.Reachable);
        return unseen is null ? null
            : Command(state, actorId, newIdentity, new InspectGatherSourceIntent(unseen.NodeId, unseen.Revision));
    }

    private static ConstructionProject? OwnProject(WorldSnapshot state, EntityId actorId) =>
        state.Construction?.Projects.OrderBy(project => project.ProjectId)
            .FirstOrDefault(project => !project.IsCompleted && project.BuilderActorId == actorId);

    private static GatherNodeState? Node(WorldSnapshot state, EntityId nodeId) =>
        state.Gathering?.Nodes.FirstOrDefault(node => node.NodeId == nodeId);

    private static CountedSourceLot? Tool(WorldSnapshot state, EntityId actorId, GatherNodeState node)
    {
        EntityId bag = state.GetResident(actorId).InventoryCustodianId;
        return state.Inventory.Custody.Lots.Where(lot => lot.CustodianId == bag && lot.ReservedBy is null
                && (lot.Protection & LotProtection.UserReserved) == 0
                && state.Gathering!.Tools.Any(tool => tool.Definition == lot.Definition && tool.Kind == node.Definition.RequiredToolKind))
            .OrderBy(lot => lot.LotId).FirstOrDefault();
    }

    private static ulong Holding(WorldSnapshot state, EntityId actorId, CountedItemDefinition definition)
    {
        EntityId bag = state.GetResident(actorId).InventoryCustodianId;
        return state.Inventory.Custody.Lots.Where(lot => lot.CustodianId == bag && lot.Definition == definition)
            .Aggregate(0UL, (total, lot) => checked(total + lot.Quantity));
    }

    /// <summary>A cardinal neighbour of the source the resident already stands on or can reach by ground.</summary>
    private static SubunitPosition? Standing(WorldSnapshot state, EntityId actorId, EntityId nodeId)
    {
        if (Node(state, nodeId) is not { } node) return null;
        SubunitPosition feet = state.Space.GetActor(actorId).Feet;
        foreach (SubunitPosition candidate in new[] { node.Feet.Offset(0, -SubunitPosition.SubunitsPerCell, 0),
            node.Feet.Offset(-SubunitPosition.SubunitsPerCell, 0, 0), node.Feet.Offset(SubunitPosition.SubunitsPerCell, 0, 0),
            node.Feet.Offset(0, SubunitPosition.SubunitsPerCell, 0) })
        {
            if (feet == candidate || state.Space.QueryGroundRoute(actorId, candidate).Status == GroundRouteStatus.Reachable)
                return candidate;
        }
        return null;
    }

    private static WorldActionCommand Command(WorldSnapshot state, EntityId actorId, Func<EntityId> newIdentity, WorldActionIntent intent)
    {
        TrustedCommandDelivery delivery = Delivery(state, actorId);
        return new WorldActionCommand(newIdentity(), delivery.PrincipalId, delivery.SessionId, actorId, state.WorldId,
            state.BranchId, state.Space.GetActor(actorId).Revision, intent);
    }

    private static TrustedCommandDelivery Delivery(WorldSnapshot state, EntityId actorId)
    {
        ActorControlBinding binding = state.Inventory.Controls.Single(value => value.ActorId == actorId);
        return new TrustedCommandDelivery(binding.PrincipalId, binding.SessionId);
    }
}
