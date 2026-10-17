using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Spatial;

/// <summary>
/// Route geometry over exact bounded terrain and occupancy reads. Loaded cells outside the declared
/// query boundary remain unavailable; successful routes neither move an actor nor authorize work.
/// </summary>
public sealed class SpatialRouteReadSet
{
    private readonly SpatialMovementQueries _queries;

    public SpatialRouteReadSet(TerrainReadSet terrain, SpatialOccupancyReadSet occupancy, GroundSupportMode groundSupport)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(occupancy);
        if (!Enum.IsDefined(groundSupport) || terrain.Plan.Scope != occupancy.Plan.Scope
            || terrain.Plan.RequestedBounds != occupancy.Plan.Bounds)
            throw new ArgumentException("Route terrain and occupancy must have the same exact scope and declared read boundary.");
        Terrain = terrain;
        Occupancy = occupancy;
        GroundSupport = groundSupport;
        var actors = occupancy.Actors.ToDictionary(actor => actor.ActorId);
        var volumes = occupancy.Actors.ToDictionary(actor => actor.ActorId,
            actor => BodyVolume.At(actor.Feet, actor.Condition.Definition.ClearanceDimensions));
        _queries = new(new(terrain.Regions, terrain.Plan.RequestedBounds), actors, volumes,
            occupancy.Actors, occupancy.Structures, groundSupport);
    }

    public TerrainReadSet Terrain { get; }
    public SpatialOccupancyReadSet Occupancy { get; }
    public GroundSupportMode GroundSupport { get; }

    public GroundRouteResult QueryGroundRoute(EntityId actorId, SubunitPosition from, SubunitPosition destination,
        int maximumExpansions = GroundRouteSearch.MaximumExpansions) =>
        GroundSupport == GroundSupportMode.SteppedTerrainAndConstruction
            ? QueryWalkingRoute(actorId, from, destination, maximumExpansions)
            : new GroundRouteSearch(_queries, actorId, maximumExpansions).Find(from, destination);

    public GroundRouteResult QueryWalkingRoute(EntityId actorId, SubunitPosition from, SubunitPosition destination,
        int maximumExpansions = LocalSpatialSnapshot.DefaultWalkingRouteExpansions) =>
        new WalkingRouteSearch(_queries, actorId, maximumExpansions).Find(from, destination);

    public void ValidateCurrent(Func<SpatialOccupancyPageKey, SpatialOccupancyPage?> readPage,
        IEnumerable<TerrainRegionReference> terrain, GroundSupportMode groundSupport)
    {
        if (groundSupport != GroundSupport)
            throw new InvalidOperationException("The route ground-support interpretation changed.");
        Occupancy.Plan.ValidateCurrent(readPage);
        Terrain.Plan.ValidateCurrentReferences(terrain);
    }
}
