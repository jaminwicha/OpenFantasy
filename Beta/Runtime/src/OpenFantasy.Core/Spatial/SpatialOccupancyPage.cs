using System.Collections.ObjectModel;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Core.Spatial;

/// <summary>Fixed 16-cell index addresses, independent of terrain archive and mutable ownership boundaries.</summary>
public readonly record struct SpatialOccupancyPageKey(long X, long Y, long Z)
{
    public const int AxisCells = 16;
    public TerrainRegionBounds Bounds => new(new(checked(X * AxisCells), checked(Y * AxisCells), checked(Z * AxisCells)),
        AxisCells, AxisCells, AxisCells);

    public static SpatialOccupancyPageKey At(CellCoordinate cell) => new(Floor(cell.X), Floor(cell.Y), Floor(cell.Z));

    internal static IEnumerable<SpatialOccupancyPageKey> Cover(TerrainRegionBounds bounds)
    {
        var minimum = At(bounds.Minimum);
        var maximum = At(bounds.Maximum);
        for (long z = minimum.Z; z <= maximum.Z; z++)
            for (long y = minimum.Y; y <= maximum.Y; y++)
                for (long x = minimum.X; x <= maximum.X; x++)
                    yield return new(x, y, z);
    }

    private static long Floor(long value) => value / AxisCells - (value % AxisCells < 0 ? 1 : 0);
}

/// <summary>
/// Caller-established complete occupancy metadata for one page, including explicit emptiness.
/// Cross-page copies are references to one entity, never duplicate owners. Construction authenticates nothing.
/// </summary>
public sealed class SpatialOccupancyPage
{
    public const int MaximumEntries = 1024;
    private readonly SortedDictionary<EntityId, SpatialOccupancyReference> _entries;

    public SpatialOccupancyPage(TerrainScope scope, SpatialOccupancyPageKey key, ulong revision,
        IEnumerable<SpatialOccupancyReference> entries)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(entries);
        var bounds = key.Bounds;
        var indexed = new SortedDictionary<EntityId, SpatialOccupancyReference>();
        foreach (var entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry);
            if (indexed.Count == MaximumEntries || entry.Scope != scope
                || !SpatialOccupancyReference.Intersects(entry.Bounds, bounds) || !indexed.TryAdd(entry.EntityId, entry))
                throw new ArgumentException("Page entries must be scoped, intersecting, unique and bounded.", nameof(entries));
        }
        Scope = scope; Key = key; Revision = revision;
        _entries = indexed;
        Entries = Array.AsReadOnly(indexed.Values.ToArray());
        ContentHash = ContentHash.Compute(CanonicalEncodingV1.Frame("spatial.occupancy-page.v1",
            scope.WorldId.ToByteArray(), scope.BranchId.ToByteArray(), scope.RealmId.ToByteArray(),
            WorldSpatialCodecV1.Signed(key.X), WorldSpatialCodecV1.Signed(key.Y), WorldSpatialCodecV1.Signed(key.Z),
            CanonicalEncodingV1.Unsigned64(revision), CanonicalEncodingV1.Frame("entries", Entries.Select(entry =>
                (ReadOnlyMemory<byte>)CanonicalEncodingV1.Frame("reference", CanonicalEncodingV1.Unsigned64((ulong)entry.Kind),
                    entry.EntityId.ToByteArray(), WorldSpatialCodecV1.Signed(entry.Bounds.Minimum.X),
                    WorldSpatialCodecV1.Signed(entry.Bounds.Minimum.Y), WorldSpatialCodecV1.Signed(entry.Bounds.Minimum.Z),
                    CanonicalEncodingV1.Unsigned64((ulong)entry.Bounds.Width), CanonicalEncodingV1.Unsigned64((ulong)entry.Bounds.Depth),
                    CanonicalEncodingV1.Unsigned64((ulong)entry.Bounds.Height), entry.ContentHash.ToByteArray())).ToArray())));
    }

    public TerrainScope Scope { get; }
    public SpatialOccupancyPageKey Key { get; }
    public ulong Revision { get; }
    public ReadOnlyCollection<SpatialOccupancyReference> Entries { get; }
    public ContentHash ContentHash { get; }
    internal bool Contains(SpatialOccupancyReference entry) => _entries.TryGetValue(entry.EntityId, out var actual) && actual == entry;
}

public sealed record SpatialOccupancyPageReference(SpatialOccupancyPageKey Key, ulong Revision, ContentHash ContentHash);
