using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>Authorizes and funds an assisted traversal before publishing feet and effort together.</summary>
internal static class WorldClimbResolver
{
    internal static (LocalSpatialSnapshot Space, GatheringSnapshot Gathering, WorldActionRejection Rejection) Resolve(
        CountedAuthorityState inventory, LocalSpatialSnapshot space, GatheringSnapshot gathering,
        WorldActionCommand command, bool advancing, bool held)
    {
        if (!inventory.CanControl(new(command.PrincipalId, command.OriginSessionId), command.ActorId))
        {
            return (space, gathering, WorldActionRejection.NotAuthorized);
        }
        if (!advancing || held || gathering.GetActorWork(command.ActorId).Effort < GatherRulesV1.EffortPerSwing)
        {
            return (space, gathering, WorldActionRejection.Unavailable);
        }
        var intent = (ClimbIntent)command.Intent;
        SpatialMoveResult move = space.PrepareClimb(command.ActorId, command.ExpectedActorRevision,
            intent.DeltaX, intent.DeltaY, intent.DeltaZ);
        if (move.Status != SpatialMoveStatus.Prepared)
        {
            return (space, gathering, move.Status == SpatialMoveStatus.RevisionConflict
                ? WorldActionRejection.RevisionConflict : WorldActionRejection.Unavailable);
        }
        return (space.WithActors(space.Actors.Select(actor => actor.ActorId == command.ActorId ? move.Candidate! : actor)),
            gathering.SpendEffort(command.ActorId), WorldActionRejection.None);
    }
}
