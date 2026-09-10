using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Spatial;

/// <summary>Nonblocking resource interaction anchors; no invented NPC body or resource collider.</summary>
public static class ResourceInteractionQueries
{
    /// <summary>One-cell point-anchor clearance and direct support, without introducing a resource collider.</summary>
    public static SpatialOccupancy QueryAnchor(LocalSpatialSnapshot space, SubunitPosition resourceFeet)
    {
        ArgumentNullException.ThrowIfNull(space);
        var terrain = new TerrainVolumeQueries(space.Regions);
        CellCoordinate coordinate = resourceFeet.Cell;
        TerrainCell? occupied = terrain.FindCell(coordinate);
        TerrainCell? support = terrain.FindCell(coordinate.Offset(0, 0, -1));
        if (occupied is null || support is null)
        {
            return SpatialOccupancy.Unavailable;
        }
        if (occupied.IsSolid)
        {
            return SpatialOccupancy.BlockedTerrain;
        }
        if (resourceFeet.Z % SubunitPosition.SubunitsPerCell != 0 || !support.IsSolid)
        {
            return SpatialOccupancy.Unsupported;
        }
        return SpatialOccupancy.Clear;
    }

    public static SpatialInteraction Query(LocalSpatialSnapshot space, EntityId actorId, SubunitPosition resourceFeet)
    {
        ArgumentNullException.ThrowIfNull(space);
        ActorPresence actor = space.GetActor(actorId);
        if (actor.Feet.Z != resourceFeet.Z)
        {
            return SpatialInteraction.DifferentFloor;
        }
        if (QueryAnchor(space, resourceFeet) != SpatialOccupancy.Clear)
        {
            return SpatialInteraction.Unavailable;
        }
        var center = BodyVolume.At(actor.Feet, actor.Condition.Definition.ClearanceDimensions).DoubledCenter;
        // The anchor shares the actor's exact center height; a ground-plane endpoint would graze the support cube.
        var target = ((Int128)resourceFeet.X * 2, (Int128)resourceFeet.Y * 2, center.Z);
        return TerrainSightline.QueryCenters(new TerrainVolumeQueries(space.Regions), center, target, GatherRulesV1.ReachSubunits);
    }
}
