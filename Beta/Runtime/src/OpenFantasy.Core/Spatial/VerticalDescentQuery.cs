using System.Collections.ObjectModel;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Spatial;

/// <summary>
/// Read-only vertical sweep over supplied candidate geometry, which can contain unsupported occupants.
/// The transaction owner must supply the complete affected scene and admit the final coupled world.
/// </summary>
public sealed class VerticalDescentQuery
{
    public const long MaximumDropSubunits = 16384;
    private readonly TerrainVolumeQueries _terrain;
    private readonly Dictionary<EntityId, BodyVolume> _actorVolumes = [];

    public VerticalDescentQuery(TerrainScope scope, IEnumerable<TerrainRegionSnapshot> regions,
        IEnumerable<SpatialStructure> structures, IEnumerable<ActorPresence> actors)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(structures);
        ArgumentNullException.ThrowIfNull(actors);
        if (scope.WorldId == scope.BranchId || scope.WorldId == scope.RealmId || scope.BranchId == scope.RealmId)
        {
            throw new ArgumentException("Descent scope identities must retain distinct spatial roles.", nameof(scope));
        }
        Scope = scope;
        TerrainRegionSnapshot[] terrain = regions.Take(LocalSpatialSnapshot.MaximumRegions + 1).ToArray();
        if (terrain.Length is 0 or > LocalSpatialSnapshot.MaximumRegions || terrain.Any(value => value is null))
        {
            throw new ArgumentException("A descent scene requires a finite nonempty terrain set.", nameof(regions));
        }
        Array.Sort(terrain, (left, right) => left.Bounds.Minimum.CompareTo(right.Bounds.Minimum));
        for (int index = 0; index < terrain.Length; index++)
        {
            TerrainRegionSnapshot region = terrain[index];
            if (region.Scope != scope || region.Seed != terrain[0].Seed || region.Rules.Version != terrain[0].Rules.Version
                || terrain.Take(index).Any(previous => Overlaps(previous.Bounds, region.Bounds)))
            {
                throw new ArgumentException("Descent terrain requires disjoint regions with exact scope, seed and rules.", nameof(regions));
            }
        }
        Regions = Array.AsReadOnly(terrain);
        _terrain = new TerrainVolumeQueries(Regions);

        SpatialStructure[] obstacles = structures.Take(LocalSpatialSnapshot.MaximumStructures + 1).ToArray();
        ActorPresence[] occupants = actors.Take(LocalSpatialSnapshot.MaximumActors + 1).ToArray();
        if (obstacles.Length > LocalSpatialSnapshot.MaximumStructures || obstacles.Any(value => value is null)
            || occupants.Length > LocalSpatialSnapshot.MaximumActors || occupants.Any(value => value is null))
        {
            throw new ArgumentException("A descent scene exceeds its finite supplied obstacle catalog.");
        }
        HashSet<EntityId> identities = [scope.WorldId, scope.BranchId, scope.RealmId];
        foreach (SpatialStructure structure in obstacles)
        {
            if (!identities.Add(structure.StructureId))
            {
                throw new ArgumentException("Descent obstacle identities must be unique.", nameof(structures));
            }
        }
        foreach (ActorPresence actor in occupants)
        {
            if (!identities.Add(actor.ActorId) || actor.Scope != scope || actor.Binding.Kind != ActorFormKind.Corporeal)
            {
                throw new ArgumentException("Descent actors require distinct identities and corporeal forms in the exact scope.", nameof(actors));
            }
            BodyVolume volume = BodyVolume.At(actor.Feet, actor.Condition.Definition.ClearanceDimensions);
            ValidateSize(volume);
            _actorVolumes.Add(actor.ActorId, volume);
        }
        Structures = Array.AsReadOnly(obstacles.OrderBy(value => value.StructureId).ToArray());
        Actors = Array.AsReadOnly(occupants.OrderBy(value => value.ActorId).ToArray());
    }

    public TerrainScope Scope { get; }
    public ReadOnlyCollection<TerrainRegionSnapshot> Regions { get; }
    public ReadOnlyCollection<SpatialStructure> Structures { get; }
    public ReadOnlyCollection<ActorPresence> Actors { get; }

    /// <summary>Excludes only this supplied actor's own starting body; stationary actors cannot become floors.</summary>
    public VerticalDescentResult QueryActor(EntityId actorId, long maximumDropSubunits)
    {
        if (!_actorVolumes.TryGetValue(actorId, out BodyVolume volume))
        {
            throw new ArgumentException("The descending actor is outside the supplied candidate scene.", nameof(actorId));
        }
        return Query(volume, maximumDropSubunits, actorId);
    }

    /// <summary>
    /// Excludes no material or actor. The caller supplies the actual remaining obstacles; ignoring a whole
    /// assembly to remove one falling member would discard real collision dependencies.
    /// </summary>
    public VerticalDescentResult QueryBox(SubunitBox volume, long maximumDropSubunits)
    {
        ArgumentNullException.ThrowIfNull(volume);
        ValidateSize(volume.Volume);
        return Query(volume.Volume, maximumDropSubunits, null);
    }

    private VerticalDescentResult Query(BodyVolume volume, long maximumDrop, EntityId? descendingActor)
    {
        if (maximumDrop is < 1 or > MaximumDropSubunits)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDrop));
        }
        try
        {
            long minimumZ = checked(volume.MinimumZ - maximumDrop);
            SpatialOccupancy initial = Clearance(volume, descendingActor);
            if (initial != SpatialOccupancy.Clear)
            {
                return new(initial switch
                {
                    SpatialOccupancy.Unavailable => VerticalDescentStatus.Unavailable,
                    SpatialOccupancy.BlockedTerrain => VerticalDescentStatus.InitialTerrainOverlap,
                    SpatialOccupancy.BlockedStructure => VerticalDescentStatus.InitialStructureOverlap,
                    SpatialOccupancy.BlockedActor => VerticalDescentStatus.InitialActorOverlap,
                    _ => throw new InvalidOperationException("Descent clearance returned an unsupported result.")
                });
            }
            long? contact = FirstContact(volume, minimumZ, descendingActor);
            long destinationZ = contact ?? minimumZ;
            long drop = checked(volume.MinimumZ - destinationZ);
            var destination = volume with { MinimumZ = destinationZ, MaximumZ = checked(volume.MaximumZ - drop) };
            SpatialOccupancy swept = Clearance(volume.Union(destination), descendingActor);
            // Every earlier collider top participates in FirstContact. A non-clear prefix is not a safe descent.
            if (swept != SpatialOccupancy.Clear)
            {
                return new(VerticalDescentStatus.Unavailable);
            }
            var reached = new SubunitBox(new(destination.MinimumX, destination.MinimumY, destination.MinimumZ),
                new(destination.MaximumX, destination.MaximumY, destination.MaximumZ));
            if (contact is null)
            {
                return new(VerticalDescentStatus.NoContactWithinLimit, drop, reached);
            }
            if (_actorVolumes.Any(value => value.Key != descendingActor && value.Value.MaximumZ == destinationZ
                && OverlapsFootprint(volume, value.Value)))
            {
                return new(VerticalDescentStatus.ActorContact, drop, reached);
            }
            SpatialOccupancy support = FootingQuery.Query(_terrain, Structures, destination);
            return support switch
            {
                SpatialOccupancy.Clear => new(VerticalDescentStatus.Landed, drop, reached),
                SpatialOccupancy.Unsupported => new(VerticalDescentStatus.IncompleteSupport, drop, reached),
                SpatialOccupancy.Unavailable => new(VerticalDescentStatus.Unavailable),
                _ => throw new InvalidOperationException("Descent footing returned an unsupported result.")
            };
        }
        catch (OverflowException)
        {
            return new(VerticalDescentStatus.NumericLimit);
        }
    }

    private long? FirstContact(BodyVolume volume, long minimumZ, EntityId? descendingActor)
    {
        long? highest = null;
        void Include(long top)
        {
            if (top >= minimumZ && top <= volume.MinimumZ && (highest is null || top > highest.Value))
            {
                highest = top;
            }
        }
        // Missing cells below an earlier contact are irrelevant. The selected swept prefix and exact
        // landing footprint are checked separately; unknown cells are never treated as clear motion.
        for (long z = SubunitPosition.CellOf(minimumZ) - 1; z <= volume.MinimumCell.Z; z++)
        {
            Int128 top = ((Int128)z + 1) * SubunitPosition.SubunitsPerCell;
            if (top < minimumZ || top > volume.MinimumZ) { continue; }
            for (long y = volume.MinimumCell.Y; y <= volume.MaximumCell.Y; y++)
            {
                for (long x = volume.MinimumCell.X; x <= volume.MaximumCell.X; x++)
                {
                    if (_terrain.FindCell(new(x, y, z))?.IsSolid == true) { Include(checked((long)top)); }
                }
            }
        }
        foreach (SubunitBox box in Structures.SelectMany(value => value.Boxes))
        {
            if (OverlapsFootprint(volume, box.Volume)) { Include(box.Maximum.Z); }
        }
        foreach (var actor in _actorVolumes)
        {
            if (actor.Key != descendingActor && OverlapsFootprint(volume, actor.Value)) { Include(actor.Value.MaximumZ); }
        }
        return highest;
    }

    private SpatialOccupancy Clearance(BodyVolume volume, EntityId? descendingActor)
    {
        SpatialOccupancy terrain = _terrain.Query(volume, requireGroundSupport: false);
        if (terrain != SpatialOccupancy.Clear) { return terrain; }
        if (Structures.Any(value => value.Boxes.Any(box => volume.Overlaps(box.Volume))))
        {
            return SpatialOccupancy.BlockedStructure;
        }
        return _actorVolumes.Any(value => value.Key != descendingActor && volume.Overlaps(value.Value))
            ? SpatialOccupancy.BlockedActor : SpatialOccupancy.Clear;
    }

    private static void ValidateSize(BodyVolume volume)
    {
        Int128 width = (Int128)volume.MaximumX - volume.MinimumX;
        Int128 depth = (Int128)volume.MaximumY - volume.MinimumY;
        Int128 height = (Int128)volume.MaximumZ - volume.MinimumZ;
        if (width <= 0 || width > LocalSpatialSnapshot.MaximumBodyAxisSubunits
            || depth <= 0 || depth > LocalSpatialSnapshot.MaximumBodyAxisSubunits
            || height <= 0 || height > LocalSpatialSnapshot.MaximumBodyAxisSubunits)
        {
            throw new ArgumentException("Descent queries require positive boxes within the existing body-axis limits.");
        }
    }

    private static bool OverlapsFootprint(BodyVolume left, BodyVolume right) =>
        left.MinimumX < right.MaximumX && left.MaximumX > right.MinimumX
        && left.MinimumY < right.MaximumY && left.MaximumY > right.MinimumY;

    private static bool Overlaps(TerrainRegionBounds left, TerrainRegionBounds right) =>
        left.Minimum.X <= right.Maximum.X && left.Maximum.X >= right.Minimum.X
        && left.Minimum.Y <= right.Maximum.Y && left.Maximum.Y >= right.Minimum.Y
        && left.Minimum.Z <= right.Maximum.Z && left.Maximum.Z >= right.Minimum.Z;
}
