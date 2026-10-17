namespace OpenFantasy.Core.Spatial;

public sealed partial class LocalSpatialSnapshot
{
    public const int DefaultWalkingRouteExpansions = 2048;
    public const int MaximumWalkingRouteExpansions = 4096;

    public GroundRouteResult QueryWalkingRoute(EntityId actorId, SubunitPosition destination,
        int maximumExpansions = DefaultWalkingRouteExpansions) =>
        QueryWalkingRoute(actorId, GetActor(actorId).Feet, destination, maximumExpansions);

    public GroundRouteResult QueryWalkingRoute(EntityId actorId, SubunitPosition from, SubunitPosition destination,
        int maximumExpansions = DefaultWalkingRouteExpansions) =>
        new WalkingRouteSearch(_movement, actorId, maximumExpansions).Find(from, destination);

    public GroundRouteResult QueryGroundRoute(EntityId actorId, SubunitPosition destination,
        int maximumExpansions = GroundRouteSearch.MaximumExpansions) =>
        QueryGroundRoute(actorId, GetActor(actorId).Feet, destination, maximumExpansions);

    /// <summary>
    /// Geometric route for this actor's real body from explicit anchors. It does not relocate the actor,
    /// grant control, or prove routes beyond this complete admitted local geometry and search budget.
    /// </summary>
    public GroundRouteResult QueryGroundRoute(EntityId actorId, SubunitPosition from, SubunitPosition destination,
        int maximumExpansions = GroundRouteSearch.MaximumExpansions) =>
        GroundSupport == GroundSupportMode.SteppedTerrainAndConstruction
            ? QueryWalkingRoute(actorId, from, destination, maximumExpansions)
            : new GroundRouteSearch(_movement, actorId, maximumExpansions).Find(from, destination);

    internal SpatialOccupancy QueryGroundSegment(EntityId actorId, SubunitPosition from, SubunitPosition to) =>
        _movement.QueryGroundSegment(actorId, from, to);
}
