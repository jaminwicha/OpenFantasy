using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Structures;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Terrain;

/// <summary>Prepares one owned work action; the complete world transaction publishes labor, terrain and material together.</summary>
public sealed class ExcavationPlanner
{
    public ExcavationPlan Plan(WorldSnapshot world, CountedAuthorityState inventory, LocalSpatialSnapshot space,
        GatheringSnapshot gathering, ProductionSnapshot production, ExcavationSnapshot excavation,
        WorldActionCommand command, bool advancing, bool held, Func<EntityId, bool> occupied,
        ConstructionPhysicalSnapshot? physical = null) => PlanCore(world, inventory, space, gathering, production,
            excavation, command, advancing, held, occupied, physical, null);

    // Only the world consequence owner can supply a completed, basis-bound calculation.
    internal ExcavationPlan PlanResolved(WorldSnapshot world, WorldActionCommand command,
        Func<EntityId, bool> occupied, ConstructionSettlementResolution completedResolution) =>
        PlanCore(world, world.Inventory, world.Space, world.Gathering!, world.Production!, world.Excavation!,
            command, true, false, occupied, world.ConstructionPhysical, completedResolution);

    private ExcavationPlan PlanCore(WorldSnapshot world, CountedAuthorityState inventory, LocalSpatialSnapshot space,
        GatheringSnapshot gathering, ProductionSnapshot production, ExcavationSnapshot excavation,
        WorldActionCommand command, bool advancing, bool held, Func<EntityId, bool> occupied,
        ConstructionPhysicalSnapshot? physical, ConstructionSettlementResolution? completedResolution)
    {
        if (production.IsContinued)
        {
            throw new ArgumentException("Excavation requires an admitted continuation issuance contract.", nameof(production));
        }
        physical ??= world.ConstructionPhysical;
        ExcavationPlan Reject(WorldActionRejection reason) => new(inventory, space, gathering, excavation, reason, null, physical);
        var eligibility = ValidateRequest(world, inventory, space, gathering, excavation, command, advancing, held);
        if (eligibility != WorldActionRejection.None) { return Reject(eligibility); }
        var intent = (ExcavateIntent)command.Intent;
        var site = excavation.Sites.Single(s => s.SiteId == intent.SiteId);
        var actor = space.GetActor(command.ActorId);
        var bag = world.GetResident(actor.ActorId).InventoryCustodianId;
        var storage = inventory.Custody.GetStorage(bag);
        try
        {
            var regions = CandidateRegions(space, site);
            LocalSpatialSnapshot changedSpace;
            ConstructionPhysicalSnapshot? changedPhysical = physical;
            if (world.StructuralSettlementPolicy is { } settlement)
            {
                if (completedResolution?.IsFailed == true) { return Reject(WorldActionRejection.Unavailable); }
                var settled = completedResolution is not null ? completedResolution.Result
                    : world.DescentMode == ConstructionDescentMode.EventOrdered ? new ConstructionSettlementResolver().ResolveEventOrdered(world, regions)
                    : world.CoupledSettlementPolicy is not null ? new ConstructionSettlementResolver().ResolveCoupled(world, regions)
                    : new ConstructionSettlementResolver().Resolve(space, regions,
                        physical ?? throw new ArgumentException("Settlement requires physical construction."), settlement);
                changedSpace = settled.Space;
                changedPhysical = settled.Physical;
            }
            else
            {
                changedSpace = new LocalSpatialSnapshot(space.Scope, regions, space.Actors, space.Structures,
                    space.GroundSupport, space.StructureSupport);
                physical?.ValidateStableGeometry(changedSpace);
            }
            if (gathering.Nodes.Any(n => ResourceInteractionQueries.QueryAnchor(changedSpace, n.Feet) != SpatialOccupancy.Clear))
            {
                return Reject(WorldActionRejection.Unavailable);
            }
            // Prove the complete eventual opening before spending even partial labor.
            if (world.LiquidCells?.Topology is not null)
            {
                WorldTerrainLiquidBinding.ValidateOpening(world, changedSpace);
            }
            var worked = site.Work();
            var changedGathering = gathering.SpendEffort(actor.ActorId);
            var changedExcavation = excavation.WithSite(worked);
            if (!worked.IsComplete)
            {
                return new(inventory, space, changedGathering, changedExcavation, WorldActionRejection.None, null, physical);
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
            return new(changedInventory, changedSpace, changedGathering, changedExcavation, WorldActionRejection.None, spawned, changedPhysical);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
    }

    internal WorldActionRejection ValidateRequest(WorldSnapshot world, CountedAuthorityState inventory,
        LocalSpatialSnapshot space, GatheringSnapshot gathering, ExcavationSnapshot excavation,
        WorldActionCommand command, bool advancing, bool held)
    {
        if (command.Intent is not ExcavateIntent intent || world.WorldId != command.WorldId || world.BranchId != command.BranchId)
        {
            throw new ArgumentException("Excavation requires the selected world's explicit action.");
        }
        if (!inventory.CanControl(new(command.PrincipalId, command.OriginSessionId), command.ActorId))
        {
            return WorldActionRejection.NotAuthorized;
        }
        var site = excavation.Sites.FirstOrDefault(s => s.SiteId == intent.SiteId);
        if (site is null || !advancing || held || site.IsComplete)
        {
            return WorldActionRejection.Unavailable;
        }
        if (site.OwnerActorId != command.ActorId)
        {
            return WorldActionRejection.NotAuthorized;
        }
        var actor = space.GetActor(command.ActorId);
        var region = space.Regions.Single(r => r.Bounds.Contains(site.Cell));
        var bag = world.GetResident(actor.ActorId).InventoryCustodianId;
        var storage = inventory.Custody.GetStorage(bag);
        if (actor.Revision != command.ExpectedActorRevision || site.Revision != intent.ExpectedSiteRevision
            || region.Revision != intent.ExpectedTerrainRevision || storage.Revision != intent.ExpectedDestinationRevision)
        {
            return WorldActionRejection.RevisionConflict;
        }
        var effort = gathering.GetActorWork(actor.ActorId);
        var tool = inventory.Custody.Lots.FirstOrDefault(l => l.LotId == intent.ToolLotId);
        if (!actor.Condition.SupportsFunction(BodyFunction.Manipulation) || effort.Effort < GatherRulesV1.EffortPerSwing
            || tool is null || tool.Definition != site.RequiredTool || tool.Revision != intent.ExpectedToolLotRevision
            || tool.ReservedBy is not null || tool.Protection.HasFlag(LotProtection.UserReserved)
            || !inventory.Policies.Any(p => p.CustodianId == tool.CustodianId && p.OwnerActorId == actor.ActorId)
            || !inventory.AllowsTransfer(actor.ActorId, tool.CustodianId, bag))
        {
            return WorldActionRejection.Unavailable;
        }
        try
        {
            return ExcavationAccess.CanReach(space, actor.ActorId, site)
                ? WorldActionRejection.None : WorldActionRejection.OutOfReach;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException)
        {
            return WorldActionRejection.Unavailable;
        }
    }

    internal static TerrainRegionSnapshot[] CandidateRegions(LocalSpatialSnapshot space, ExcavationSite site)
    {
        var region = space.Regions.Single(r => r.Bounds.Contains(site.Cell));
        var changed = region.Excavate(region.Revision, [site.Cell]);
        return space.Regions.Select(r => ReferenceEquals(r, region) ? changed : r).ToArray();
    }
}
