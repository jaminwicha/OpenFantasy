using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Quests;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Proposes ordinary movement and work for a funded, explicitly assigned local commitment.</summary>
internal sealed class LocalConstructionController
{
    internal const string PolicyVersion = "local.carried-joinery.v1";
    private readonly ConstructionPlanner _planner = new();

    internal ConstructionProject? FindCommitment(WorldSnapshot world, EntityId actorId) =>
        world.Construction?.Projects.FirstOrDefault(project => !project.IsCompleted
            && project.BuilderActorId == actorId && world.Quests!.Participations.Any(participation =>
                participation.QuestId == project.QuestId && participation.Status == ActorQuestStatus.Completed));

    internal WorldMovementInput? SelectMovement(WorldSnapshot world, ConstructionProject project)
        => SelectMovement(world, project.BuilderActorId, project.WorkFeet);

    internal bool NeedsReturnHome(WorldSnapshot world, WorldResident resident)
    {
        if (world.Construction?.Projects.Any(project => project.IsCompleted
                && project.BuilderActorId == resident.ActorId) != true)
        {
            return false;
        }
        SubunitPosition feet = world.Space.GetActor(resident.ActorId).Feet;
        Int128 x = (Int128)feet.X - resident.HomeFeet.X;
        Int128 y = (Int128)feet.Y - resident.HomeFeet.Y;
        Int128 radius = (Int128)resident.WanderRadiusCells * SubunitPosition.SubunitsPerCell;
        // Check each axis before squaring so extreme valid world coordinates cannot overflow.
        return Int128.Abs(x) > radius || Int128.Abs(y) > radius || x * x + y * y > radius * radius;
    }

    internal WorldMovementInput? SelectReturnHome(WorldSnapshot world, WorldResident resident)
        => SelectMovement(world, resident.ActorId, resident.HomeFeet);

    private static WorldMovementInput? SelectMovement(WorldSnapshot world, EntityId actorId, SubunitPosition destination)
    {
        ActorPresence actor = world.Space.GetActor(actorId);
        if (actor.Feet == destination)
        {
            return null;
        }
        if (((Int128)destination.X - actor.Feet.X) % WorldMovementInput.DisplacementPerStep != 0
            || ((Int128)destination.Y - actor.Feet.Y) % WorldMovementInput.DisplacementPerStep != 0)
        {
            return null;
        }
        GroundRouteResult route = world.Space.QueryGroundRoute(actor.ActorId, destination);
        if (route.Status != GroundRouteStatus.Reachable || route.NextWaypoint is not { } next)
        {
            return null;
        }
        long nextX = checked(next.X - actor.Feet.X);
        long nextY = checked(next.Y - actor.Feet.Y);
        if (nextX % WorldMovementInput.DisplacementPerStep != 0 || nextY % WorldMovementInput.DisplacementPerStep != 0)
        {
            return null;
        }
        long x = Math.Clamp(nextX, -WorldMovementInput.DisplacementPerStep,
            WorldMovementInput.DisplacementPerStep);
        long y = x == 0 ? Math.Clamp(nextY, -WorldMovementInput.DisplacementPerStep,
            WorldMovementInput.DisplacementPerStep) : 0;
        return x == 0 && y == 0 ? null : new WorldMovementInput(Delivery(world, actor.ActorId), actor.ActorId, x, y);
    }

    internal WorldActionCommand? SelectWork(WorldSnapshot world, ConstructionProject project, Func<EntityId> newIdentity)
    {
        EntityId builder = project.BuilderActorId;
        ActorPresence actor = world.Space.GetActor(builder);
        if (actor.Feet != project.WorkFeet || world.HasReadingHold)
        {
            return null;
        }
        EntityId toolBag = world.GetResident(builder).InventoryCustodianId;
        CountedSourceLot? tool = world.Inventory.Custody.Lots.FirstOrDefault(lot => lot.CustodianId == toolBag
            && lot.Definition == project.Definition.RequiredTool.Item && lot.Protection == LotProtection.None
            && lot.ReservedBy is null);
        if (tool is null)
        {
            return null;
        }
        var intent = new WorkConstructionIntent(project.ProjectId, project.Revision,
            world.Inventory.Custody.GetStorage(project.ProjectCustodianId).Revision, tool.LotId, tool.Revision);
        TrustedCommandDelivery delivery = Delivery(world, builder);
        var command = new WorldActionCommand(newIdentity(), delivery.PrincipalId, delivery.SessionId,
            builder, world.WorldId, world.BranchId, actor.Revision, intent);
        // Private immutable planning avoids endless rejected work receipts at an obstructed site.
        // The selected world batch must still recheck every dependency before committing anything.
        ConstructionPlan planned = _planner.Plan(world, world.Inventory, world.Production!, world.Construction!,
            world.Space, command, isAdvancingStep: true, held: false);
        return planned.Succeeded ? command : null;
    }

    private static TrustedCommandDelivery Delivery(WorldSnapshot world, EntityId actorId)
    {
        ActorControlBinding binding = world.Inventory.Controls.Single(value => value.ActorId == actorId);
        return new TrustedCommandDelivery(binding.PrincipalId, binding.SessionId);
    }
}
