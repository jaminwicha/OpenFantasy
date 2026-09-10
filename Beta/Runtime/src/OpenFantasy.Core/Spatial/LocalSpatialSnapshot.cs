using System.Collections.ObjectModel;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Spatial;

/// <summary>
/// Complete admitted local terrain and corporeal actor occupancy. The world owner establishes
/// residency completeness and transaction ownership; this type validates supplied geometry.
/// </summary>
public sealed class LocalSpatialSnapshot
{
    public const int MaximumRegions = 16;
    public const int MaximumActors = 64;
    public const long MaximumBodyAxisSubunits = 2048;
    public const long MaximumMoveSubunits = 256;
    public const long InteractionRangeSubunits = 768;

    private readonly Dictionary<EntityId, ActorPresence> _actors = [];
    private readonly Dictionary<EntityId, BodyVolume> _volumes = [];
    private readonly TerrainVolumeQueries _terrain;

    public LocalSpatialSnapshot(TerrainScope scope, IEnumerable<TerrainRegionSnapshot> regions,
        IEnumerable<ActorPresence> actors)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(actors);
        Scope = scope;
        Regions = ReadRegions(regions);
        _terrain = new TerrainVolumeQueries(Regions);
        Actors = ReadActors(actors);
        foreach (ActorPresence actor in Actors)
        {
            if (QueryVolume(actor.ActorId, _volumes[actor.ActorId]) != SpatialOccupancy.Clear)
            {
                throw new ArgumentException("An admitted actor requires complete clear space, direct support and exclusive occupancy.", nameof(actors));
            }
        }
    }

    public TerrainScope Scope { get; }
    public ReadOnlyCollection<TerrainRegionSnapshot> Regions { get; }
    public ReadOnlyCollection<ActorPresence> Actors { get; }

    public ActorPresence GetActor(EntityId actorId) => _actors.TryGetValue(actorId, out ActorPresence? actor)
        ? actor : throw new ArgumentException("The actor is outside this admitted local spatial set.", nameof(actorId));

    /// <summary>Checks this actor's actual body at candidate feet, excluding only its own current occupancy.</summary>
    public SpatialOccupancy QueryOccupancy(EntityId actorId, SubunitPosition feet)
    {
        ActorPresence actor = GetActor(actorId);
        return QueryVolume(actorId, BodyVolume.At(feet, actor.Condition.Definition.ClearanceDimensions));
    }

    /// <summary>Prepares one bounded cardinal, fixed-elevation grounded move against all swept dependencies.</summary>
    public SpatialMoveResult PrepareMove(EntityId actorId, ulong expectedRevision, long deltaX, long deltaY)
    {
        if ((deltaX == 0) == (deltaY == 0) || deltaX < -MaximumMoveSubunits || deltaX > MaximumMoveSubunits
            || deltaY < -MaximumMoveSubunits || deltaY > MaximumMoveSubunits)
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

    /// <summary>Geometry only: same feet elevation, exact 3-cell XY center range and a clear center segment.</summary>
    public SpatialInteraction QueryInteraction(EntityId actorId, EntityId targetActorId)
    {
        if (actorId == targetActorId)
        {
            throw new ArgumentException("An interaction requires two distinct actors.", nameof(targetActorId));
        }
        ActorPresence actor = GetActor(actorId);
        ActorPresence target = GetActor(targetActorId);
        if (actor.Feet.Z != target.Feet.Z)
        {
            return SpatialInteraction.DifferentFloor;
        }
        return TerrainSightline.Query(_terrain, _volumes[actorId], _volumes[targetActorId]);
    }

    private SpatialOccupancy QueryVolume(EntityId actorId, BodyVolume volume)
    {
        SpatialOccupancy terrain = _terrain.Query(volume);
        if (terrain is SpatialOccupancy.Unavailable or SpatialOccupancy.BlockedTerrain)
        {
            return terrain;
        }
        foreach (ActorPresence other in Actors)
        {
            if (other.ActorId != actorId && volume.Overlaps(_volumes[other.ActorId]))
            {
                return SpatialOccupancy.BlockedActor;
            }
        }
        return terrain;
    }

    private ReadOnlyCollection<TerrainRegionSnapshot> ReadRegions(IEnumerable<TerrainRegionSnapshot> regions)
    {
        List<TerrainRegionSnapshot> copied = [];
        foreach (TerrainRegionSnapshot region in regions)
        {
            ArgumentNullException.ThrowIfNull(region);
            if (copied.Count == MaximumRegions)
            {
                throw new ArgumentException("The local region count exceeds its supported bound.", nameof(regions));
            }
            if (region.Scope != Scope || copied.Count > 0 &&
                (region.Seed != copied[0].Seed || region.Rules.Version != copied[0].Rules.Version))
            {
                throw new ArgumentException("Local terrain requires one exact scope, seed and generation rules pin.", nameof(regions));
            }
            if (copied.Any(other => Overlaps(region.Bounds, other.Bounds)))
            {
                throw new ArgumentException("Authoritative terrain regions cannot overlap.", nameof(regions));
            }
            copied.Add(region);
        }
        if (copied.Count == 0)
        {
            throw new ArgumentException("A local spatial set requires at least one terrain region.", nameof(regions));
        }
        copied.Sort((left, right) => left.Bounds.Minimum.CompareTo(right.Bounds.Minimum));
        return copied.AsReadOnly();
    }

    private ReadOnlyCollection<ActorPresence> ReadActors(IEnumerable<ActorPresence> actors)
    {
        var kinds = new Dictionary<EntityId, string>();
        Register(kinds, Scope.WorldId, "world");
        Register(kinds, Scope.BranchId, "branch");
        Register(kinds, Scope.RealmId, "realm");
        var definitions = new Dictionary<EntityId, ContentHash>();
        foreach (ActorPresence actor in actors)
        {
            ArgumentNullException.ThrowIfNull(actor);
            if (_actors.Count == MaximumActors || !_actors.TryAdd(actor.ActorId, actor))
            {
                throw new ArgumentException("Local actors must have unique identities within the supported bound.", nameof(actors));
            }
            if (actor.Scope != Scope || actor.Binding.Kind != ActorFormKind.Corporeal)
            {
                throw new ArgumentException("This spatial profile admits corporeal actors in its exact scope.", nameof(actors));
            }
            BodyDefinition body = actor.Condition.Definition;
            BodyDimensions size = body.ClearanceDimensions;
            if (size.WidthSubunits > MaximumBodyAxisSubunits || size.DepthSubunits > MaximumBodyAxisSubunits
                || size.HeightSubunits > MaximumBodyAxisSubunits)
            {
                throw new ArgumentException("The body's full dimensions exceed this local geometry profile.", nameof(actors));
            }
            if (definitions.TryGetValue(body.DefinitionId, out ContentHash? version) && version != body.Version)
            {
                throw new ArgumentException("One body definition identity cannot name different local versions.", nameof(actors));
            }
            definitions[body.DefinitionId] = body.Version;
            Register(kinds, actor.ActorId, "actor", unique: true);
            Register(kinds, actor.Binding.FormInstanceId, "form", unique: true);
            Register(kinds, actor.Binding.PhysicalBodyId!.Value, "body", unique: true);
            Register(kinds, actor.HomePlaceId, "place");
            Register(kinds, body.DefinitionId, "body-definition");
            _volumes.Add(actor.ActorId, BodyVolume.At(actor.Feet, size));
        }
        return _actors.Values.OrderBy(actor => actor.ActorId).ToList().AsReadOnly();
    }

    private static void Register(Dictionary<EntityId, string> kinds, EntityId identity, string kind, bool unique = false)
    {
        if (kinds.TryGetValue(identity, out string? existing) && (existing != kind || unique))
        {
            throw new ArgumentException("Known spatial identities cannot alias incompatible roles or duplicate an instance.");
        }
        kinds[identity] = kind;
    }

    private static bool Overlaps(TerrainRegionBounds left, TerrainRegionBounds right) =>
        left.Minimum.X <= right.Maximum.X && left.Maximum.X >= right.Minimum.X
        && left.Minimum.Y <= right.Maximum.Y && left.Maximum.Y >= right.Minimum.Y
        && left.Minimum.Z <= right.Maximum.Z && left.Maximum.Z >= right.Minimum.Z;
}
