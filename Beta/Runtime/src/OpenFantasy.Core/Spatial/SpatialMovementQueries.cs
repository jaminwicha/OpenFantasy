using OpenFantasy.Core.Actors;

namespace OpenFantasy.Core.Spatial;

/// <summary>Shared movement geometry over supplied immutable dependencies; never world admission or publication.</summary>
internal sealed partial class SpatialMovementQueries
{
    private readonly TerrainVolumeQueries _terrain;
    private readonly IReadOnlyDictionary<EntityId, ActorPresence> _actors;
    private readonly IReadOnlyDictionary<EntityId, BodyVolume> _volumes;
    private readonly IReadOnlyList<ActorPresence> _actorList;
    private readonly IReadOnlyList<SpatialStructure> _structures;
    private readonly GroundSupportMode _groundSupport;

    internal SpatialMovementQueries(TerrainVolumeQueries terrain,
        IReadOnlyDictionary<EntityId, ActorPresence> actors, IReadOnlyDictionary<EntityId, BodyVolume> volumes,
        IReadOnlyList<ActorPresence> actorList, IReadOnlyList<SpatialStructure> structures, GroundSupportMode groundSupport)
    {
        _terrain = terrain;
        _actors = actors;
        _volumes = volumes;
        _actorList = actorList;
        _structures = structures;
        _groundSupport = groundSupport;
    }

    private ActorPresence GetActor(EntityId actorId) => _actors.TryGetValue(actorId, out ActorPresence? actor)
        ? actor : throw new ArgumentException("The actor is outside this admitted local spatial set.", nameof(actorId));

    internal SpatialMoveResult PrepareMove(EntityId actorId, ulong expectedRevision, long deltaX, long deltaY)
    {
        if ((deltaX == 0) == (deltaY == 0) || deltaX < -LocalSpatialSnapshot.MaximumMoveSubunits || deltaX > LocalSpatialSnapshot.MaximumMoveSubunits
            || deltaY < -LocalSpatialSnapshot.MaximumMoveSubunits || deltaY > LocalSpatialSnapshot.MaximumMoveSubunits)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaX), "Movement requires one nonzero cardinal displacement within the supported bound.");
        }
        ActorPresence actor = GetActor(actorId);
        if (actor.Revision != expectedRevision)
        {
            return new SpatialMoveResult(SpatialMoveStatus.RevisionConflict);
        }
        if (!actor.Condition.CanLocomote(LocomotionMode.Ground))
        {
            return new SpatialMoveResult(SpatialMoveStatus.NoGroundLocomotion);
        }
        if (_groundSupport == GroundSupportMode.SteppedTerrainAndConstruction)
        {
            return PrepareWalkFrom(actorId, actor.Feet, deltaX, deltaY);
        }
        try
        {
            SubunitPosition destination = actor.Feet.Offset(deltaX, deltaY, 0);
            BodyVolume swept = _volumes[actorId].Union(BodyVolume.At(destination,
                actor.Condition.Definition.ClearanceDimensions));
            SpatialOccupancy occupancy = QueryVolume(actorId, swept);
            SpatialMoveStatus status = occupancy switch
            {
                SpatialOccupancy.Clear => SpatialMoveStatus.Prepared,
                SpatialOccupancy.Unavailable => SpatialMoveStatus.Unavailable,
                SpatialOccupancy.BlockedTerrain => SpatialMoveStatus.BlockedTerrain,
                SpatialOccupancy.BlockedActor => SpatialMoveStatus.BlockedActor,
                SpatialOccupancy.BlockedStructure => SpatialMoveStatus.BlockedStructure,
                SpatialOccupancy.Unsupported => SpatialMoveStatus.Unsupported,
                _ => throw new InvalidOperationException("Unknown occupancy result.")
            };
            return status == SpatialMoveStatus.Prepared
                ? new SpatialMoveResult(status, actor.At(destination))
                : new SpatialMoveResult(status);
        }
        catch (OverflowException)
        {
            return new SpatialMoveResult(SpatialMoveStatus.NumericLimit);
        }
    }

    internal SpatialOccupancy QueryVolume(EntityId actorId, BodyVolume volume)
    {
        SpatialOccupancy clearance = QueryClearance(actorId, volume);
        if (clearance != SpatialOccupancy.Clear)
        {
            return clearance;
        }
        return _groundSupport switch
        {
            GroundSupportMode.TerrainOnly => clearance,
            GroundSupportMode.SteppedTerrainAndConstruction => FootingQuery.QueryStepped(_terrain, _structures, volume,
                StepHeight(GetActor(actorId))),
            _ => FootingQuery.Query(_terrain, _structures, volume)
        };
    }

    internal SpatialOccupancy QueryClearance(EntityId actorId, BodyVolume volume)
    {
        // Preserve legacy missing-support precedence even when another collider also blocks the volume.
        SpatialOccupancy terrain = _terrain.Query(volume, _groundSupport == GroundSupportMode.TerrainOnly);
        if (terrain is SpatialOccupancy.Unavailable or SpatialOccupancy.BlockedTerrain)
        {
            return terrain;
        }
        if (_structures.Any(structure => structure.Boxes.Any(box => volume.Overlaps(box.Volume))))
        {
            return SpatialOccupancy.BlockedStructure;
        }
        foreach (ActorPresence other in _actorList)
        {
            if (other.ActorId != actorId && volume.Overlaps(_volumes[other.ActorId]))
            {
                return SpatialOccupancy.BlockedActor;
            }
        }
        return _groundSupport == GroundSupportMode.TerrainOnly ? terrain : SpatialOccupancy.Clear;
    }

}
