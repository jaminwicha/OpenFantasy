namespace OpenFantasy.Core.Spatial;

public sealed partial class LocalSpatialSnapshot
{
    public GroundRouteResult QueryGroundRoute(EntityId actorId, SubunitPosition destination,
        int maximumExpansions = GroundRouteSearch.MaximumExpansions) =>
        QueryGroundRoute(actorId, GetActor(actorId).Feet, destination, maximumExpansions);

    /// <summary>
    /// Geometric route for this actor's real body from explicit anchors. It does not relocate the actor,
    /// grant control, or prove routes beyond this complete admitted local geometry and search budget.
    /// </summary>
    public GroundRouteResult QueryGroundRoute(EntityId actorId, SubunitPosition from, SubunitPosition destination,
        int maximumExpansions = GroundRouteSearch.MaximumExpansions) =>
        new GroundRouteSearch(this, actorId, maximumExpansions).Find(from, destination);

    internal SpatialOccupancy QueryGroundSegment(EntityId actorId, SubunitPosition from, SubunitPosition to)
    {
        Int128 dx = (Int128)to.X - from.X;
        Int128 dy = (Int128)to.Y - from.Y;
        if (from.Z != to.Z || (dx == 0) == (dy == 0)
            || Int128.Abs(dx) > MaximumMoveSubunits || Int128.Abs(dy) > MaximumMoveSubunits)
        {
            throw new ArgumentException("A route segment requires one bounded same-floor cardinal displacement.");
        }
        var dimensions = GetActor(actorId).Condition.Definition.ClearanceDimensions;
        return QueryVolume(actorId, BodyVolume.At(from, dimensions).Union(BodyVolume.At(to, dimensions)));
    }
}
