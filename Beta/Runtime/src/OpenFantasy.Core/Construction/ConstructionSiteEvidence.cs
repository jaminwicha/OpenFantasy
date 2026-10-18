using OpenFantasy.Core.Content;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Construction;

/// <summary>
/// Exact construction geometry qualification with no retained terrain or scene. Current owner metadata
/// must revalidate the complete captured population; this is neither work permission nor world admission.
/// </summary>
public sealed class ConstructionSiteEvidence : IConstructionGeometry
{
    private readonly EntityId _projectId;
    private readonly EntityId _builderId;
    private readonly ContentHash _definitionVersion;
    private readonly SubunitPosition _workFeet;
    private readonly SubunitPosition _siteFeet;
    private readonly GroundAccessRequirement[] _originalAccess;
    private readonly GroundAccessRequirement[] _evaluatedAccess;
    private readonly ActorPresence[] _actors;
    private readonly SpatialStructure[] _structures;
    private readonly TerrainRegionReference[] _terrain;
    private readonly SpatialOccupancyReference[] _occupancy;
    private readonly SpatialStructure _shape;
    private readonly SpatialInteraction _reach;
    private readonly StructurePlacementStatus _placement;

    private ConstructionSiteEvidence(LocalSpatialSnapshot space, ConstructionProject project, EntityId structureId)
    {
        Scope = space.Scope;
        GroundSupport = space.GroundSupport;
        StructureSupport = space.StructureSupport;
        _projectId = project.ProjectId;
        _builderId = project.BuilderActorId;
        _definitionVersion = project.Definition.Version;
        _workFeet = project.WorkFeet;
        _siteFeet = project.SiteFeet;
        _originalAccess = project.RequiredAccess.ToArray();
        _actors = space.Actors.ToArray();
        _structures = space.Structures.ToArray();
        _terrain = space.Regions.Select(TerrainRegionReference.Capture).ToArray();
        _occupancy = ReadOccupancy(space);
        _evaluatedAccess = project.RequiredAccess.Concat(project.RequiredAccess.Select(value =>
            new GroundAccessRequirement(value.ActorId, space.GetActor(value.ActorId).Feet, value.To))).Distinct().ToArray();
        _shape = project.Definition.CreateStructure(structureId, project.SiteFeet, GroundSupport != GroundSupportMode.TerrainOnly);
        _reach = space.QueryPointInteraction(project.BuilderActorId, project.SiteFeet, ConstructionDefinition.WorkReachSubunits);
        _placement = space.PrepareStructurePlacement(_shape, _evaluatedAccess).Status;
    }

    public TerrainScope Scope { get; }
    public GroundSupportMode GroundSupport { get; }
    public StructureSupportMode StructureSupport { get; }

    internal static ConstructionSiteEvidence Capture(LocalSpatialSnapshot space, ConstructionProject project,
        EntityId structureId) => new(space, project, structureId);

    public static ConstructionSiteEvidence Capture(WorldSnapshot world, WorldActionCommand command)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(command);
        if (command.Intent is not WorkConstructionIntent intent
            || world.Construction?.FindProject(intent.ProjectId) is not { } project)
        {
            throw new ArgumentException("Site evidence requires an actual admitted construction project.", nameof(command));
        }
        EntityId structureId = EntityId.DeriveSpawned(command.CommandId, checked((ulong)project.Definition.MaterialTransform.Outputs.Count + 1));
        return new(world.Space, project, structureId);
    }

    /// <summary>Revalidates geometry/population only; the current publisher still owns directory completeness.</summary>
    public void ValidateCurrent(SpatialOccupancyDirectory occupancy, IEnumerable<TerrainRegionReference> terrain,
        GroundSupportMode groundSupport, StructureSupportMode structureSupport)
    {
        ArgumentNullException.ThrowIfNull(occupancy);
        ValidateTerrain(occupancy.Scope, terrain, groundSupport, structureSupport);
        if (occupancy.EntityCount != _occupancy.Length || _occupancy.Any(value => occupancy.Find(value.EntityId) != value))
        {
            throw new InvalidOperationException("Construction site evidence requires its exact complete occupancy population.");
        }
    }

    public void ValidateCurrent(LocalSpatialSnapshot space)
    {
        ArgumentNullException.ThrowIfNull(space);
        ValidateTerrain(space.Scope, space.Regions.Select(TerrainRegionReference.Capture), space.GroundSupport, space.StructureSupport);
        if (!_occupancy.SequenceEqual(ReadOccupancy(space)))
        {
            throw new InvalidOperationException("Construction site actors or structures changed.");
        }
    }

    internal void ValidateProject(ConstructionProject project)
    {
        if (project.ProjectId != _projectId || project.BuilderActorId != _builderId
            || project.Definition.Version != _definitionVersion || project.WorkFeet != _workFeet
            || project.SiteFeet != _siteFeet || !_originalAccess.SequenceEqual(project.RequiredAccess))
        {
            throw new InvalidOperationException("Construction site geometry or required access changed.");
        }
    }

    private void ValidateTerrain(TerrainScope scope, IEnumerable<TerrainRegionReference> terrain,
        GroundSupportMode groundSupport, StructureSupportMode structureSupport)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        var current = terrain.Take(LocalSpatialSnapshot.MaximumRegions + 1).ToArray();
        if (scope != Scope || groundSupport != GroundSupport || structureSupport != StructureSupport
            || current.Length != _terrain.Length || _terrain.Any(value => current.Count(other => other == value) != 1))
        {
            throw new InvalidOperationException("Construction site terrain, scope or support interpretation changed.");
        }
    }

    private static SpatialOccupancyReference[] ReadOccupancy(LocalSpatialSnapshot space) =>
        space.Actors.Select(SpatialOccupancyReference.Capture)
            .Concat(space.Structures.Select(value => SpatialOccupancyReference.Capture(space.Scope, value)))
            .OrderBy(value => value.EntityId).ToArray();

    ActorPresence IConstructionGeometry.GetActor(EntityId actorId) => _actors.FirstOrDefault(value => value.ActorId == actorId)
        ?? throw new ArgumentException("The actor is outside the captured construction site.", nameof(actorId));

    SpatialInteraction IConstructionGeometry.QueryPointInteraction(EntityId actorId, SubunitPosition target, long maximumRangeSubunits)
    {
        if (actorId != _builderId || target != _siteFeet || maximumRangeSubunits != ConstructionDefinition.WorkReachSubunits)
        {
            throw new InvalidOperationException("Site evidence does not qualify this interaction.");
        }
        return _reach;
    }

    StructurePlacementResult IConstructionGeometry.PrepareStructurePlacement(SpatialStructure proposed,
        IEnumerable<GroundAccessRequirement> access)
    {
        // Geometry is reusable across work commands; newly derived identities are checked every time.
        SpatialStructureQueries.ReadStructures(Scope, _actors, _structures.Append(proposed));
        if (!_shape.Boxes.SequenceEqual(proposed.Boxes) || !_shape.BearingSurfaces.SequenceEqual(proposed.BearingSurfaces)
            || !_evaluatedAccess.SequenceEqual(access))
        {
            throw new InvalidOperationException("Site evidence does not qualify this structure or access set.");
        }
        return new(_placement);
    }
}
