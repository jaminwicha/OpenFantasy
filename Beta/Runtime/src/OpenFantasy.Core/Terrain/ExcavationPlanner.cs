using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Terrain;

/// <summary>Prepares one owned work action; the complete world transaction publishes labor, terrain and material together.</summary>
public sealed class ExcavationPlanner
{
    public ExcavationPlan Plan(WorldSnapshot world, CountedAuthorityState inventory, LocalSpatialSnapshot space,
        GatheringSnapshot gathering, ProductionSnapshot production, ExcavationSnapshot excavation,
        WorldActionCommand command, bool advancing, bool held, Func<EntityId, bool> occupied)
    {
        ExcavationPlan Reject(WorldActionRejection reason) => new(inventory, space, gathering, excavation, reason, null);
        if (command.Intent is not ExcavateIntent intent || world.WorldId != command.WorldId || world.BranchId != command.BranchId)
        {
            throw new ArgumentException("Excavation requires the selected world's explicit action.");
        }
        if (!inventory.CanControl(new(command.PrincipalId, command.OriginSessionId), command.ActorId))
        {
            return Reject(WorldActionRejection.NotAuthorized);
        }
        var site = excavation.Sites.FirstOrDefault(s => s.SiteId == intent.SiteId);
        if (site is null || !advancing || held || site.IsComplete)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        if (site.OwnerActorId != command.ActorId)
        {
            return Reject(WorldActionRejection.NotAuthorized);
        }
        var actor = space.GetActor(command.ActorId);
        var region = space.Regions.Single(r => r.Bounds.Contains(site.Cell));
        var bag = world.GetResident(actor.ActorId).InventoryCustodianId;
        var storage = inventory.Custody.GetStorage(bag);
        if (actor.Revision != command.ExpectedActorRevision || site.Revision != intent.ExpectedSiteRevision
            || region.Revision != intent.ExpectedTerrainRevision || storage.Revision != intent.ExpectedDestinationRevision)
        {
            return Reject(WorldActionRejection.RevisionConflict);
        }
        var effort = gathering.GetActorWork(actor.ActorId);
        var tool = inventory.Custody.Lots.FirstOrDefault(l => l.LotId == intent.ToolLotId);
        if (!actor.Condition.SupportsFunction(BodyFunction.Manipulation) || effort.Effort < GatherRulesV1.EffortPerSwing
            || tool is null || tool.Definition != site.RequiredTool || tool.Revision != intent.ExpectedToolLotRevision
            || tool.ReservedBy is not null || tool.Protection.HasFlag(LotProtection.UserReserved)
            || !inventory.Policies.Any(p => p.CustodianId == tool.CustodianId && p.OwnerActorId == actor.ActorId)
            || !inventory.AllowsTransfer(actor.ActorId, tool.CustodianId, bag))
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        try
        {
            if (!ExcavationAccess.CanReach(space, actor.ActorId, site))
            {
                return Reject(WorldActionRejection.OutOfReach);
            }
            var changedRegion = region.Excavate(region.Revision, [site.Cell]);
            var changedSpace = new LocalSpatialSnapshot(space.Scope,
                space.Regions.Select(r => ReferenceEquals(r, region) ? changedRegion : r), space.Actors, space.Structures, space.GroundSupport);
            if (gathering.Nodes.Any(n => ResourceInteractionQueries.QueryAnchor(changedSpace, n.Feet) != SpatialOccupancy.Clear))
            {
                return Reject(WorldActionRejection.Unavailable);
            }
            var worked = site.Work();
            var changedGathering = gathering.SpendEffort(actor.ActorId);
            var changedExcavation = excavation.WithSite(worked);
            if (!worked.IsComplete)
            {
                return new(inventory, space, changedGathering, changedExcavation, WorldActionRejection.None, null);
            }
            if (inventory.Custody.Lots.Count + production.Lots.Count >= CustodySnapshot.MaximumLots
                || storage.UnitCapacity - inventory.Custody.GetStoredQuantity(bag) < site.Quantity)
            {
                return Reject(WorldActionRejection.Unavailable);
            }
            var spawned = EntityId.DeriveSpawned(command.CommandId, 0);
            if (occupied(spawned))
            {
                return Reject(WorldActionRejection.Unavailable);
            }
            var lot = new CountedSourceLot(spawned, bag, site.Material.Item, new(site.CountedSourceId, 0, site.Quantity), 0);
            var custody = new CustodySnapshot(inventory.WorldId, inventory.BranchId, checked(inventory.Custody.Revision + 1),
                inventory.Custody.Storages.Select(s => s.CustodianId == bag ? s with { Revision = checked(s.Revision + 1) } : s),
                inventory.Custody.Lots.Append(lot));
            var changedInventory = inventory.WithCustody(custody);
            production.ValidateForAuthority(changedInventory);
            return new(changedInventory, changedSpace, changedGathering, changedExcavation, WorldActionRejection.None, spawned);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
    }
}
