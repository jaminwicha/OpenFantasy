using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Spatial;

/// <summary>
/// Complete finite supplied geometry for common-gravity descent. Construction validates clear initial
/// volumes and discovers exact upward contacts; it does not certify initial support or world ownership.
/// Catalog preparation, contact discovery and input hashing are unsliced finite work.
/// </summary>
public sealed class VerticalMotionScene
{
    public const int MaximumBodies = LocalSpatialSnapshot.MaximumStructures + LocalSpatialSnapshot.MaximumActors;
    private readonly ReadOnlyCollection<int>[] _dependents;

    public VerticalMotionScene(TerrainScope scope, IEnumerable<TerrainRegionSnapshot> regions,
        IEnumerable<SpatialStructure> stationaryStructures, IEnumerable<ActorPresence> stationaryActors,
        IEnumerable<VerticalMotionBody> movingBodies, long maximumDropSubunits)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(stationaryStructures);
        ArgumentNullException.ThrowIfNull(stationaryActors);
        ArgumentNullException.ThrowIfNull(movingBodies);
        if (maximumDropSubunits is < 1 or > VerticalDescentQuery.MaximumDropSubunits)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDropSubunits));
        }
        VerticalMotionBody[] moving = movingBodies.Take(MaximumBodies + 1).ToArray();
        if (moving.Length > MaximumBodies || moving.Any(body => body is null)
            || moving.Select(body => body.BodyId).Distinct().Count() != moving.Length)
        {
            throw new ArgumentException("Moving bodies require finite unique nonnull identities.", nameof(movingBodies));
        }
        var fixedScene = new VerticalDescentQuery(scope, regions, stationaryStructures, stationaryActors);
        var complete = new VerticalDescentQuery(scope, fixedScene.Regions,
            fixedScene.Structures.Concat(moving.Where(body => body.Structure is not null).Select(body => body.Structure!)),
            fixedScene.Actors.Concat(moving.Where(body => body.Actor is not null).Select(body => body.Actor!)));
        Scope = scope;
        Regions = complete.Regions;
        StationaryStructures = fixedScene.Structures;
        StationaryActors = fixedScene.Actors;
        MovingBodies = Array.AsReadOnly(moving.OrderBy(body => body.BodyId).ToArray());
        MaximumDropSubunits = maximumDropSubunits;
        int boxes = MovingBodies.Sum(body => body.Boxes.Count);
        // At most N events: every event stops at least one body. Each event probes and verifies
        // at most Q colliders; each body's base support is validated only in its stopping event.
        MaximumTotalWorkUnits = checked((2 * MovingBodies.Count + 1) * boxes);
        ValidateClearance(complete);
        _dependents = DiscoverContacts();
        InputPin = ContentHash.Compute(VerticalMotionEncoding.EncodeScene(this));
    }

    public TerrainScope Scope { get; }
    public ReadOnlyCollection<TerrainRegionSnapshot> Regions { get; }
    public ReadOnlyCollection<SpatialStructure> StationaryStructures { get; }
    public ReadOnlyCollection<ActorPresence> StationaryActors { get; }
    public ReadOnlyCollection<VerticalMotionBody> MovingBodies { get; }
    public long MaximumDropSubunits { get; }
    public int MaximumTotalWorkUnits { get; }
    public ContentHash InputPin { get; }

    internal IReadOnlyList<int> Dependents(int bodyIndex) => _dependents[bodyIndex];

    private void ValidateClearance(VerticalDescentQuery complete)
    {
        var terrain = new TerrainVolumeQueries(Regions);
        var colliders = complete.Structures.SelectMany(structure => structure.Boxes.Select(box =>
            (Owner: structure.StructureId, Volume: box.Volume))).Concat(complete.Actors.Select(actor =>
            (Owner: actor.ActorId, Volume: BodyVolume.At(actor.Feet, actor.Condition.Definition.ClearanceDimensions)))).ToArray();
        for (int index = 0; index < colliders.Length; index++)
        {
            var current = colliders[index];
            if (terrain.Query(current.Volume, requireGroundSupport: false) != SpatialOccupancy.Clear
                || colliders.Take(index).Any(previous => previous.Owner != current.Owner
                    && previous.Volume.Overlaps(current.Volume)))
            {
                throw new ArgumentException("Every supplied body requires known terrain clearance and no foreign body overlap.");
            }
        }
    }

    private ReadOnlyCollection<int>[] DiscoverContacts()
    {
        // Lower faces are exact supplied material; upper footprints remain conservative collider
        // bounds. A rounded upper fringe can add a stopping dependency and cause refusal, but every
        // stopped base must subsequently obtain full exact bearing; an edge never grants support.
        var contacts = new ReadOnlyCollection<int>[MovingBodies.Count];
        const long ticks = StructureBearingSurface.TicksPerSubunit;
        for (int lower = 0; lower < MovingBodies.Count; lower++)
        {
            List<int> supported = [];
            if (MovingBodies[lower].Structure is { } structure)
            {
                for (int upper = 0; upper < MovingBodies.Count; upper++)
                {
                    if (lower == upper) { continue; }
                    if (MovingBodies[upper].Bases.Any(box => structure.BearingSurfaces.Any(surface =>
                        surface.TopTicks == (Int128)box.Minimum.Z * ticks
                        && surface.Bounds.MinU < (Int128)box.Maximum.X * ticks
                        && surface.Bounds.MaxU > (Int128)box.Minimum.X * ticks
                        && surface.Bounds.MinV < (Int128)box.Maximum.Y * ticks
                        && surface.Bounds.MaxV > (Int128)box.Minimum.Y * ticks)))
                    {
                        supported.Add(upper);
                    }
                }
            }
            contacts[lower] = supported.AsReadOnly();
        }
        return contacts;
    }
}
