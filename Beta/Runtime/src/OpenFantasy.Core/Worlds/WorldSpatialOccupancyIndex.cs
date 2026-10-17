using OpenFantasy.Core.Content;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Core.Worlds;

/// <summary>
/// Derived complete-locality index bound to exact admitted world roots. Historical reconstruction
/// replays its updates; it does not replace the world owner or make complete-world steps partially resident.
/// </summary>
public sealed class WorldSpatialOccupancyIndex
{
    private WorldSpatialOccupancyIndex(WorldSnapshot source, SpatialOccupancyDirectory directory)
    {
        SourceHash = source.ContentHash;
        WorldRevision = source.Revision;
        ProfileVersion = source.ProfileVersion;
        Directory = directory;
    }

    public ContentHash SourceHash { get; }
    public ulong WorldRevision { get; }
    public ushort ProfileVersion { get; }
    public SpatialOccupancyDirectory Directory { get; }

    public static WorldSpatialOccupancyIndex Capture(WorldSnapshot source, int maximumPages)
    {
        ArgumentNullException.ThrowIfNull(source);
        var directory = SpatialOccupancyDirectory.Create(source.Space.Scope, References(source),
            LocalSpatialSnapshot.MaximumActors + LocalSpatialSnapshot.MaximumStructures, maximumPages);
        return new(source, directory);
    }

    /// <summary>
    /// Reconciles one exact complete-world successor. The caller supplies its durable provenance;
    /// admission and adjacent revisions alone cannot authenticate a commit or its command history.
    /// </summary>
    public WorldSpatialOccupancyIndex Advance(WorldSnapshot before, WorldSnapshot after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (before.ContentHash != SourceHash || before.Revision != WorldRevision
            || before.ProfileVersion != ProfileVersion || after.ProfileVersion != ProfileVersion
            || after.Space.Scope != Directory.Scope || after.Revision != checked(WorldRevision + 1))
            throw new InvalidOperationException("Occupancy reconciliation requires its exact source and next compatible world root.");
        var remaining = References(after).ToDictionary(entry => entry.EntityId);
        var changes = new List<SpatialOccupancyChange>();
        foreach (var old in References(before))
        {
            remaining.Remove(old.EntityId, out var next);
            if (old != next) changes.Add(new(old, next));
        }
        foreach (var added in remaining.Values) changes.Add(new(null, added));
        var directory = changes.Count == 0 ? Directory : Directory.Apply(Directory.Revision, changes);
        return new(after, directory);
    }

    private static IEnumerable<SpatialOccupancyReference> References(WorldSnapshot world) =>
        world.Space.Actors.Select(SpatialOccupancyReference.Capture)
            .Concat(world.Space.Structures.Select(structure => SpatialOccupancyReference.Capture(world.Space.Scope, structure)));
}
