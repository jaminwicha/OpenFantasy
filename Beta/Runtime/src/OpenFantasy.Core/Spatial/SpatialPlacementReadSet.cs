using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Spatial;

/// <summary>Bounded prospective structure geometry and access; never a fabrication grant or admitted world candidate.</summary>
public sealed class SpatialPlacementReadSet
{
    private readonly SpatialStructureQueries _queries;

    public SpatialPlacementReadSet(TerrainReadSet terrain, SpatialOccupancyReadSet occupancy,
        GroundSupportMode groundSupport, StructureSupportMode structureSupport)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(occupancy);
        if (!Enum.IsDefined(groundSupport) || !Enum.IsDefined(structureSupport)
            || terrain.Plan.Scope != occupancy.Plan.Scope || terrain.Plan.RequestedBounds != occupancy.Plan.Bounds)
            throw new ArgumentException("Placement requires the same exact terrain/occupancy scope, boundary and valid support interpretations.");
        Terrain = terrain;
        Occupancy = occupancy;
        GroundSupport = groundSupport;
        StructureSupport = structureSupport;
        var terrainQueries = new TerrainVolumeQueries(terrain.Regions, terrain.Plan.RequestedBounds);
        var actors = occupancy.Actors.ToDictionary(actor => actor.ActorId);
        var volumes = occupancy.Actors.ToDictionary(actor => actor.ActorId,
            actor => BodyVolume.At(actor.Feet, actor.Condition.Definition.ClearanceDimensions));
        var movement = new SpatialMovementQueries(terrainQueries, actors, volumes, occupancy.Actors,
            occupancy.Structures, groundSupport);
        _queries = new(terrain.Plan.Scope, terrainQueries, movement, occupancy.Actors, occupancy.Structures, structureSupport);
    }

    public TerrainReadSet Terrain { get; }
    public SpatialOccupancyReadSet Occupancy { get; }
    public GroundSupportMode GroundSupport { get; }
    public StructureSupportMode StructureSupport { get; }

    public StructurePlacementStatus Assess(SpatialStructure proposed, IEnumerable<GroundAccessRequirement> requiredAccess,
        int? maximumExpansions = null) => _queries.AssessPlacement(proposed, requiredAccess, maximumExpansions);

    public void ValidateCurrent(Func<SpatialOccupancyPageKey, SpatialOccupancyPage?> readPage,
        IEnumerable<TerrainRegionReference> terrain, GroundSupportMode groundSupport, StructureSupportMode structureSupport)
    {
        if (groundSupport != GroundSupport || structureSupport != StructureSupport)
            throw new InvalidOperationException("The placement support interpretation changed.");
        Occupancy.Plan.ValidateCurrent(readPage);
        Terrain.Plan.ValidateCurrentReferences(terrain);
    }
}
