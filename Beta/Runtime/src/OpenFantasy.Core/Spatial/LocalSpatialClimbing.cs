using OpenFantasy.Core.Actors;

namespace OpenFantasy.Core.Spatial;

public sealed partial class LocalSpatialSnapshot
{
    /// <summary>
    /// One explicit assisted ledge traversal: rise then traverse when ascending, reverse when descending.
    /// Full body sweeps and a continuous projected bearing corridor forbid ceiling clipping and gap jumping.
    /// </summary>
    public SpatialMoveResult PrepareClimb(EntityId actorId, ulong expectedRevision, long deltaX, long deltaY, long deltaZ)
    {
        _ = new ClimbIntent(deltaX, deltaY, deltaZ);
        ActorPresence actor = GetActor(actorId);
        if (actor.Revision != expectedRevision)
        {
            return new(SpatialMoveStatus.RevisionConflict);
        }
        if (!actor.Condition.CanLocomote(LocomotionMode.Ground))
        {
            return new(SpatialMoveStatus.NoGroundLocomotion);
        }
        if (GroundSupport is not GroundSupportMode.TerrainAndConstruction and not GroundSupportMode.SteppedTerrainAndConstruction
            || !actor.Condition.SupportsFunction(BodyFunction.Manipulation)
            || Math.Abs(deltaZ) > actor.Condition.Definition.ClearanceDimensions.HeightSubunits)
        {
            return new(SpatialMoveStatus.Unavailable);
        }
        try
        {
            SubunitPosition destination = actor.Feet.Offset(deltaX, deltaY, deltaZ);
            BodyDimensions dimensions = actor.Condition.Definition.ClearanceDimensions;
            BodyVolume start = _volumes[actorId];
            BodyVolume end = BodyVolume.At(destination, dimensions);
            SubunitPosition corner = deltaZ > 0 ? actor.Feet.Offset(0, 0, deltaZ) : actor.Feet.Offset(deltaX, deltaY, 0);
            BodyVolume bend = BodyVolume.At(corner, dimensions);
            foreach (SpatialOccupancy occupancy in new[] { QueryVolume(actorId, end),
                QueryClearance(actorId, start.Union(bend)), QueryClearance(actorId, bend.Union(end)),
                FootingQuery.QueryBand(_terrain, Structures, start.Union(end),
                    checked(Math.Min(actor.Feet.Z, destination.Z)
                        - (GroundSupport == GroundSupportMode.SteppedTerrainAndConstruction ? StepHeight(actor) : 0)),
                    Math.Max(actor.Feet.Z, destination.Z)) })
            {
                if (occupancy != SpatialOccupancy.Clear)
                {
                    return new(occupancy switch
                    {
                        SpatialOccupancy.BlockedActor => SpatialMoveStatus.BlockedActor,
                        SpatialOccupancy.BlockedStructure => SpatialMoveStatus.BlockedStructure,
                        SpatialOccupancy.BlockedTerrain => SpatialMoveStatus.BlockedTerrain,
                        SpatialOccupancy.Unsupported => SpatialMoveStatus.Unsupported,
                        _ => SpatialMoveStatus.Unavailable
                    });
                }
            }
            return new(SpatialMoveStatus.Prepared, actor.At(destination));
        }
        catch (OverflowException)
        {
            return new(SpatialMoveStatus.NumericLimit);
        }
    }
}
