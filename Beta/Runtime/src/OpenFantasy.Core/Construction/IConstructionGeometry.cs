using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Construction;

internal interface IConstructionGeometry
{
    TerrainScope Scope { get; }
    GroundSupportMode GroundSupport { get; }
    ActorPresence GetActor(EntityId actorId);
    SpatialInteraction QueryPointInteraction(EntityId actorId, SubunitPosition target, long maximumRangeSubunits);
    StructurePlacementResult PrepareStructurePlacement(SpatialStructure proposed, IEnumerable<GroundAccessRequirement> access);
}

internal sealed class LiveConstructionGeometry(LocalSpatialSnapshot space) : IConstructionGeometry
{
    public TerrainScope Scope => space.Scope;
    public GroundSupportMode GroundSupport => space.GroundSupport;
    public ActorPresence GetActor(EntityId actorId) => space.GetActor(actorId);
    public SpatialInteraction QueryPointInteraction(EntityId actorId, SubunitPosition target, long maximumRangeSubunits) =>
        space.QueryPointInteraction(actorId, target, maximumRangeSubunits);
    public StructurePlacementResult PrepareStructurePlacement(SpatialStructure proposed, IEnumerable<GroundAccessRequirement> access) =>
        space.PrepareStructurePlacement(proposed, access);
}
