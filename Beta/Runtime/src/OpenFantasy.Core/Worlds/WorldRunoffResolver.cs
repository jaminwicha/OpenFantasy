using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Exposure;
using OpenFantasy.Core.Geography;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>Admits or cancels funded receiving obligations without moving liquid or changing the fixed physical domain.</summary>
internal sealed class WorldRunoffResolver
{
    internal WorldRunoffResolution Resolve(WorldSnapshot world, LiquidReservoirState current, LocalSpatialSnapshot space,
        WorldActionCommand command, bool advancing, bool readingHold)
    {
        if (world.ProfileVersion is not 8 and not 9 and not 19 || command.Intent is not RunoffCommitmentIntent intent)
        {
            throw new InvalidOperationException("Runoff commitment actions require the physical reservoir world profile.");
        }
        WorldRunoffResolution Reject(WorldActionRejection reason) => new(current, reason);
        var capacity = current.Capacity;
        var domain = capacity.Domain;
        var delivery = new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId);
        if (!capacity.Inventory.CanControl(delivery, command.ActorId) || command.ActorId != domain.OwnerActorId)
        {
            return Reject(WorldActionRejection.NotAuthorized);
        }
        if (space.GetActor(command.ActorId).Revision != command.ExpectedActorRevision
            || intent.ExpectedCapacityRevision != capacity.Revision || intent.ExpectedReservoirHash != current.ContentHash)
        {
            return Reject(WorldActionRejection.RevisionConflict);
        }
        if (!advancing || readingHold || WorldRunoffReservationRules.HasCompetingPurpose(world, intent.ReservationId))
        {
            return Reject(WorldActionRejection.Unavailable);
        }
        try
        {
            WatershedRunoffCapacityPlan plan;
            if (intent is AdmitRunoffIntent admit)
            {
                WorldInstant publication = world.Calendar.AddMilliseconds(ExposurePacingV1.ExplorationMillisecondsPerStep);
                if (admit.Start < publication)
                {
                    return Reject(WorldActionRejection.Unavailable);
                }
                var request = new WatershedRunoffRequest(domain.Network, domain.Mapping, capacity.Inventory, capacity.Production,
                    admit.ReservationId, domain.OwnerActorId, admit.Start, admit.End, domain.Material, admit.Inputs, domain.Terminals);
                plan = capacity.PlanAdmit(new WatershedRunoffPlanner().Prepare(request));
            }
            else if (intent is CancelRunoffIntent)
            {
                plan = capacity.PlanCancel(intent.ReservationId);
            }
            else
            {
                throw new InvalidOperationException("Unsupported runoff commitment operation.");
            }
            return new(current.PlanApply(plan).ApplyTo(current), WorldActionRejection.None);
        }
        catch (Exception error) when (error is ArgumentException or OverflowException)
        {
            return Reject(WorldActionRejection.Unavailable);
        }
    }
}

internal sealed record WorldRunoffResolution(LiquidReservoirState Candidate, WorldActionRejection Rejection);
