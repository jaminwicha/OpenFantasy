using System.Collections.ObjectModel;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Spatial;

/// <summary>
/// Relevant spatial dependencies selected from a caller-established complete scene. Evaluation reuses
/// ordinary movement geometry without admitting a partial world or authorizing any mutation.
/// </summary>
public sealed class SpatialMovementDependencies
{
    private readonly StructureSupportMode _structureSupport;
    private readonly SpatialOccupancyReadPlan? _occupancyPlan;

    private SpatialMovementDependencies(LocalSpatialSnapshot source, MovementTerrainFootprint footprint,
        TerrainReadPlan terrain)
    {
        Footprint = footprint;
        Terrain = terrain;
        _structureSupport = source.StructureSupport;
        Actors = Array.AsReadOnly(RelevantActors(source, footprint.Bounds));
        Structures = Array.AsReadOnly(RelevantStructures(source, footprint.Bounds));
    }

    private SpatialMovementDependencies(MovementTerrainFootprint footprint, TerrainReadPlan terrain,
        SpatialOccupancyReadSet occupancy, StructureSupportMode structureSupport)
    {
        Footprint = footprint;
        Terrain = terrain;
        _structureSupport = structureSupport;
        _occupancyPlan = occupancy.Plan;
        Actors = occupancy.Actors;
        // Page metadata uses a conservative structure envelope. Keep only actual intersecting boxes
        // after loading, matching the complete-scene preparation without re-admitting distant supports.
        Structures = Array.AsReadOnly(occupancy.Structures.Where(structure =>
            structure.Boxes.Any(box => Intersects(box.Volume, footprint.Bounds))).ToArray());
    }

    public MovementTerrainFootprint Footprint { get; }
    public TerrainReadPlan Terrain { get; }
    public ReadOnlyCollection<ActorPresence> Actors { get; }
    public ReadOnlyCollection<SpatialStructure> Structures { get; }

    public static SpatialMovementDependencies Capture(LocalSpatialSnapshot source, EntityId actorId,
        ulong expectedActorRevision, long deltaX, long deltaY, int maximumLoadedRegions)
    {
        ArgumentNullException.ThrowIfNull(source);
        ActorPresence actor = source.GetActor(actorId);
        if (actor.Revision != expectedActorRevision)
        {
            throw new InvalidOperationException("Movement preparation requires the selected actor revision.");
        }
        var footprint = MovementTerrainFootprint.Create(actor, source.GroundSupport, deltaX, deltaY);
        var terrain = new TerrainReadPlan(source.Scope, footprint.Bounds,
            source.Regions.Select(TerrainRegionReference.Capture), maximumLoadedRegions);
        return new(source, footprint, terrain);
    }

    /// <summary>
    /// Combines exact loaded detail with complete caller-established occupancy metadata. The caller
    /// retains placement admission and directory ownership; no partial LocalSpatialSnapshot is created.
    /// </summary>
    public static SpatialMovementDependencies FromDirectory(MovementTerrainFootprint footprint, TerrainReadPlan terrain,
        SpatialOccupancyReadSet occupancy, StructureSupportMode structureSupport)
    {
        ArgumentNullException.ThrowIfNull(footprint);
        ArgumentNullException.ThrowIfNull(terrain);
        ArgumentNullException.ThrowIfNull(occupancy);
        if (!Enum.IsDefined(structureSupport) || footprint.Actor.Scope != terrain.Scope
            || occupancy.Plan.Scope != terrain.Scope || footprint.Bounds != terrain.RequestedBounds
            || footprint.Bounds != occupancy.Plan.Bounds
            || occupancy.Actors.FirstOrDefault(actor => actor.ActorId == footprint.Actor.ActorId) is not { } mover
            || !SameActor(mover, footprint.Actor))
            throw new ArgumentException("Movement must join its exact scope, footprint and pinned mover.");
        return new(footprint, terrain, occupancy, structureSupport);
    }

    public void ValidateCurrentDirectory(Func<SpatialOccupancyPageKey, SpatialOccupancyPage?> readPage,
        IEnumerable<TerrainRegionReference> terrain, GroundSupportMode groundSupport, StructureSupportMode structureSupport)
    {
        if (_occupancyPlan is null)
            throw new InvalidOperationException("This preparation was not captured through occupancy pages.");
        if (groundSupport != Footprint.GroundSupport || structureSupport != _structureSupport)
            throw new InvalidOperationException("The movement support interpretation changed.");
        _occupancyPlan.ValidateCurrent(readPage);
        Terrain.ValidateCurrentReferences(terrain);
    }

    /// <summary>Returns geometry only; call current-dependency validation inside the owning publication boundary.</summary>
    public SpatialMoveResult Evaluate(TerrainReadSet terrain)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        if (!ReferenceEquals(terrain.Plan, Terrain))
        {
            throw new ArgumentException("Movement requires a read set resolved for its exact prepared terrain plan.", nameof(terrain));
        }
        var actors = Actors.ToDictionary(actor => actor.ActorId);
        var volumes = Actors.ToDictionary(actor => actor.ActorId,
            actor => BodyVolume.At(actor.Feet, actor.Condition.Definition.ClearanceDimensions));
        var queries = new SpatialMovementQueries(new(terrain.Regions), actors, volumes, Actors, Structures, Footprint.GroundSupport);
        return queries.PrepareMove(Footprint.Actor.ActorId, Footprint.Actor.Revision, Footprint.DeltaX, Footprint.DeltaY);
    }

    /// <summary>
    /// Re-enumerates relevant colliders, including arrivals. Agreement here is not authentication or a
    /// lock; the authority must prevent intervening changes and include nonspatial action dependencies.
    /// </summary>
    public void ValidateCurrent(LocalSpatialSnapshot current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.Scope != Terrain.Scope || current.GroundSupport != Footprint.GroundSupport
            || current.StructureSupport != _structureSupport)
        {
            throw new InvalidOperationException("The movement scope or support interpretation changed.");
        }
        ActorPresence[] actors = RelevantActors(current, Footprint.Bounds);
        SpatialStructure[] structures = RelevantStructures(current, Footprint.Bounds);
        if (actors.Length != Actors.Count || structures.Length != Structures.Count
            || actors.Where((actor, index) => !SameActor(actor, Actors[index])).Any()
            || structures.Where((structure, index) => !SameStructure(structure, Structures[index])).Any())
        {
            throw new InvalidOperationException("A required movement collider changed, arrived or departed.");
        }
        Terrain.ValidateCurrentReferences(current.Regions.Select(TerrainRegionReference.Capture));
    }

    private static ActorPresence[] RelevantActors(LocalSpatialSnapshot source, TerrainRegionBounds bounds) =>
        source.Actors.Where(actor => Intersects(BodyVolume.At(actor.Feet, actor.Condition.Definition.ClearanceDimensions), bounds))
            .OrderBy(actor => actor.ActorId).ToArray();

    private static SpatialStructure[] RelevantStructures(LocalSpatialSnapshot source, TerrainRegionBounds bounds) =>
        source.Structures.Where(structure => structure.Boxes.Any(box => Intersects(box.Volume, bounds)))
            .OrderBy(structure => structure.StructureId).ToArray();

    private static bool Intersects(BodyVolume volume, TerrainRegionBounds bounds)
    {
        CellCoordinate minimum = volume.MinimumCell;
        CellCoordinate maximum = volume.MaximumCell;
        return minimum.X <= bounds.Maximum.X && maximum.X >= bounds.Minimum.X
            && minimum.Y <= bounds.Maximum.Y && maximum.Y >= bounds.Minimum.Y
            && minimum.Z <= bounds.Maximum.Z && maximum.Z >= bounds.Minimum.Z;
    }

    private static bool SameActor(ActorPresence left, ActorPresence right) =>
        left.ActorId == right.ActorId && left.Scope == right.Scope && left.Feet == right.Feet
        && left.Revision == right.Revision && left.HomePlaceId == right.HomePlaceId
        && BodyContractEncodingV1.EncodeBinding(left.Binding).AsSpan().SequenceEqual(BodyContractEncodingV1.EncodeBinding(right.Binding))
        && BodyContractEncodingV1.EncodeCondition(left.Condition).AsSpan().SequenceEqual(BodyContractEncodingV1.EncodeCondition(right.Condition));

    private static bool SameStructure(SpatialStructure left, SpatialStructure right) =>
        left.StructureId == right.StructureId && left.Boxes.SequenceEqual(right.Boxes)
        && left.BearingSurfaces.SequenceEqual(right.BearingSurfaces);
}
