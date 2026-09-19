using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

internal sealed class WorldTransportResolver
{
    internal WorldTransportResolution Resolve(ReservoirTransportState current, LocalSpatialSnapshot space,
        WorldActionCommand command, bool advancing, bool readingHold, Func<EntityId, bool> occupied)
    {
        if (command.Intent is not ScheduleRunoffTransportIntent intent)
        {
            throw new InvalidOperationException("Transport scheduling requires its declared action interpreter.");
        }
        WorldTransportResolution Reject(WorldActionRejection reason) => new(current, reason, []);
        var inventory = current.Reservoirs.Capacity.Inventory;
        if (!inventory.CanControl(new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId), command.ActorId)
            || command.ActorId != current.Reservoirs.Capacity.Domain.OwnerActorId)
        {
            return Reject(WorldActionRejection.NotAuthorized);
        }
        if (space.GetActor(command.ActorId).Revision != command.ExpectedActorRevision
            || current.ContentHash != intent.ExpectedTransportHash || current.Revision != intent.ExpectedTransportRevision)
        {
            return Reject(WorldActionRejection.RevisionConflict);
        }
        if (!advancing || readingHold)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        EntityId stageId = EntityId.DeriveSpawned(command.CommandId, 0);
        if (occupied(stageId))
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        try
        {
            var plan = current.PlanSchedule(stageId, intent.ReservationId, intent.DueAt, intent.Routes);
            return new(plan.ApplyTo(current), WorldActionRejection.None, plan.CreatedIds);
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
    }
}

internal sealed record WorldTransportResolution(ReservoirTransportState Candidate, WorldActionRejection Rejection,
    IReadOnlyList<EntityId> CreatedIds);
