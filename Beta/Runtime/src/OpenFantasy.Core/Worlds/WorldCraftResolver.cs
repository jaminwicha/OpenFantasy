using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>Joins body, presence and interaction holds to the portable material planner.</summary>
internal sealed class WorldCraftResolver
{
    private readonly InstantCraftPlanner _crafting = new();

    internal CraftPlan Resolve(WorldSnapshot world, CountedAuthorityState inventory, ProductionSnapshot production,
        WorldActionCommand command, bool held, Func<EntityId, bool> occupied, LocalSpatialSnapshot? currentSpace = null)
    {
        LocalSpatialSnapshot space = currentSpace ?? world.Space;
        CraftPlan Reject(WorldActionRejection rejection) => new(inventory, production, rejection, []);
        var delivery = new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId);
        if (!inventory.CanControl(delivery, command.ActorId))
        {
            return Reject(WorldActionRejection.NotAuthorized);
        }
        var actor = space.GetActor(command.ActorId);
        if (actor.Revision != command.ExpectedActorRevision)
        {
            return Reject(WorldActionRejection.RevisionConflict);
        }
        var intent = (CraftIntent)command.Intent;
        if (world.Construction?.IsConstructionRecipe(intent.Request.RecipeId) == true || held || !actor.Condition.SupportsFunction(BodyFunction.Manipulation)
            || intent.Request.CustodianId != world.GetResident(command.ActorId).InventoryCustodianId)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        CraftPlan plan = _crafting.Plan(inventory, production, delivery, actor.ActorId, command.CommandId, intent.Request,
            production.IsContinued ? identity => identity != command.CommandId && occupied(identity) : null);
        return plan.CreatedIds.Any(occupied) ? Reject(WorldActionRejection.Unavailable) : plan;
    }
}
