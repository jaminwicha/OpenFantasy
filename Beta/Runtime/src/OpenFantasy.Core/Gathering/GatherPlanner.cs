using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Gathering;

/// <summary>
/// Plans one source swing against admitted current state. The host authenticates delivery and the
/// world processor passes sequential candidates, checks historical identities, and commits atomically.
/// </summary>
public sealed class GatherPlanner
{
    public GatherPlan Plan(WorldSnapshot world, CountedAuthorityState currentInventory,
        GatheringSnapshot gathering, WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(currentInventory);
        ArgumentNullException.ThrowIfNull(gathering);
        ArgumentNullException.ThrowIfNull(command);
        if (command.Intent is not GatherIntent intent || command.WorldId != world.WorldId || command.BranchId != world.BranchId
            || currentInventory.WorldId != world.WorldId || currentInventory.BranchId != world.BranchId
            || gathering.WorldId != world.WorldId || gathering.BranchId != world.BranchId || gathering.AdvancingStep != world.AdvancingSteps)
        {
            throw new ArgumentException("Gather planning requires one exact world, branch, step and supported action.");
        }
        GatherPlan Reject(WorldActionRejection rejection) => new(currentInventory, gathering, rejection, null);
        if (!currentInventory.CanControl(new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId), command.ActorId))
        {
            return Reject(WorldActionRejection.NotAuthorized);
        }
        if (world.HasReadingHold)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        ActorPresence actor = world.Space.GetActor(command.ActorId);
        if (actor.Revision != command.ExpectedActorRevision)
        {
            return Reject(WorldActionRejection.RevisionConflict);
        }
        GatherNodeState? node = gathering.Nodes.FirstOrDefault(value => value.NodeId == intent.NodeId);
        if (node is null)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        if (node.Revision != intent.ExpectedNodeRevision)
        {
            return Reject(WorldActionRejection.RevisionConflict);
        }
        if (node.RemainingSwings == 0)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        if (ResourceInteractionQueries.Query(world.Space, actor.ActorId, node.Feet) != SpatialInteraction.Reachable)
        {
            return Reject(WorldActionRejection.OutOfReach);
        }
        if (!actor.Condition.SupportsFunction(BodyFunction.Manipulation))
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        CountedSourceLot? toolLot = currentInventory.Custody.Lots.FirstOrDefault(value => value.LotId == intent.ToolLotId);
        if (toolLot is null || !currentInventory.Policies.Any(policy => policy.CustodianId == toolLot.CustodianId && policy.OwnerActorId == actor.ActorId))
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        if (toolLot.Revision != intent.ExpectedToolLotRevision || toolLot.ReservedBy is not null
            || (toolLot.Protection & LotProtection.UserReserved) != 0)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        GatherToolDefinition? tool = gathering.Tools.FirstOrDefault(value => value.Definition == toolLot.Definition);
        if (tool is null || tool.Kind != node.Definition.RequiredToolKind)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        GatherActorWork effort = gathering.GetActorWork(actor.ActorId);
        if (effort.Effort < GatherRulesV1.EffortPerSwing || node.UnitCapacity - node.NextUnissuedUnit < tool.Power
            || currentInventory.Custody.Lots.Count == CustodySnapshot.MaximumLots)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        EntityId destinationId = world.GetResident(actor.ActorId).InventoryCustodianId;
        if (!currentInventory.AllowsTransfer(actor.ActorId, toolLot.CustodianId, destinationId))
        {
            return Reject(WorldActionRejection.NotAuthorized);
        }
        CustodianStorage storage = currentInventory.Custody.GetStorage(destinationId);
        if (storage.UnitCapacity - currentInventory.Custody.GetStoredQuantity(destinationId) < tool.Power)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        EntityId spawned = EntityId.DeriveSpawned(command.CommandId, 0);
        if (spawned == command.CommandId || currentInventory.ContainsIdentity(spawned) || world.IdentityIndex.Contains(spawned)
            || gathering.Nodes.Any(value => value.NodeId == spawned || value.Definition.DefinitionId == spawned))
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        try
        {
            var source = currentInventory.Sources.FirstOrDefault(value => value.SourceId == node.SourceId);
            if (source is null || source.Definition != node.Definition.YieldDefinition || source.UnitCapacity != node.UnitCapacity
                || currentInventory.Custody.Lots.Any(value => value.Source.SourceId == node.SourceId
                    && value.Source.End > node.NextUnissuedUnit))
            {
                return Reject(WorldActionRejection.Unavailable);
            }
            GatherNodeState harvested = node.Harvest(tool.Power, gathering.AdvancingStep);
            var lot = new CountedSourceLot(spawned, destinationId, node.Definition.YieldDefinition,
                new SourceInterval(node.SourceId, node.NextUnissuedUnit, harvested.NextUnissuedUnit), 0);
            var custody = new CustodySnapshot(currentInventory.WorldId, currentInventory.BranchId,
                checked(currentInventory.Custody.Revision + 1), currentInventory.Custody.Storages.Select(value =>
                    value.CustodianId == destinationId ? value with { Revision = checked(value.Revision + 1) } : value),
                [.. currentInventory.Custody.Lots, lot]);
            return new GatherPlan(currentInventory.WithCustody(custody), gathering.Harvest(harvested, effort.Spend()),
                WorldActionRejection.None, spawned);
        }
        catch (OverflowException)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
    }
}
