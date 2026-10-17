namespace OpenFantasy.Core.Spatial;

internal sealed partial class SpatialMovementQueries
{
    internal SpatialOccupancy QueryOccupancy(EntityId actorId, SubunitPosition feet)
    {
        ActorPresence actor = GetActor(actorId);
        return QueryVolume(actorId, BodyVolume.At(feet, actor.Condition.Definition.ClearanceDimensions));
    }

    internal SpatialOccupancy QueryGroundSegment(EntityId actorId, SubunitPosition from, SubunitPosition to)
    {
        Int128 dx = (Int128)to.X - from.X;
        Int128 dy = (Int128)to.Y - from.Y;
        if (from.Z != to.Z || (dx == 0) == (dy == 0)
            || Int128.Abs(dx) > LocalSpatialSnapshot.MaximumMoveSubunits || Int128.Abs(dy) > LocalSpatialSnapshot.MaximumMoveSubunits)
        {
            throw new ArgumentException("A route segment requires one bounded same-floor cardinal displacement.");
        }
        var dimensions = GetActor(actorId).Condition.Definition.ClearanceDimensions;
        return QueryVolume(actorId, BodyVolume.At(from, dimensions).Union(BodyVolume.At(to, dimensions)));
    }
}
