using System.Collections.Immutable;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Spatial;

/// <summary>
/// Complete immutable geometric metadata index. Entity owners establish admitted detail and publish
/// the returned successor with their actual state; this index grants no action, ownership or durability.
/// </summary>
public sealed class SpatialOccupancyDirectory
{
    public const int MaximumChanges = 1024;
    private readonly ImmutableDictionary<EntityId, SpatialOccupancyReference> _entities;
    private readonly ImmutableDictionary<SpatialOccupancyPageKey, SpatialOccupancyPage> _pages;

    private SpatialOccupancyDirectory(TerrainScope scope, ulong revision, int maximumEntities, int maximumPages,
        ImmutableDictionary<EntityId, SpatialOccupancyReference> entities,
        ImmutableDictionary<SpatialOccupancyPageKey, SpatialOccupancyPage> pages)
    {
        Scope = scope;
        Revision = revision;
        MaximumEntities = maximumEntities;
        MaximumPages = maximumPages;
        _entities = entities;
        _pages = pages;
    }

    public TerrainScope Scope { get; }
    public ulong Revision { get; }
    public int MaximumEntities { get; }
    public int MaximumPages { get; }
    public int EntityCount => _entities.Count;
    public int RetainedPageCount => _pages.Count;

    /// <summary>Imports the complete owner-established catalog; enumeration is bounded before publication.</summary>
    public static SpatialOccupancyDirectory Create(TerrainScope scope, IEnumerable<SpatialOccupancyReference> references,
        int maximumEntities, int maximumPages)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(references);
        if (maximumEntities < 1 || maximumPages < 1)
            throw new ArgumentOutOfRangeException(nameof(maximumEntities));
        var entities = ImmutableDictionary.CreateBuilder<EntityId, SpatialOccupancyReference>();
        var buckets = new Dictionary<SpatialOccupancyPageKey, SortedDictionary<EntityId, SpatialOccupancyReference>>();
        foreach (var entry in references)
        {
            ArgumentNullException.ThrowIfNull(entry);
            if (entry.Scope != scope || entities.Count == maximumEntities || entities.ContainsKey(entry.EntityId))
                throw new ArgumentException("The complete catalog must have scoped, unique and bounded entities.", nameof(references));
            entities.Add(entry.EntityId, entry);
            foreach (var key in SpatialOccupancyPageKey.Cover(entry.Bounds))
            {
                if (!buckets.TryGetValue(key, out var bucket))
                {
                    if (buckets.Count == maximumPages)
                        throw new InvalidOperationException("The complete catalog exceeds its metadata page budget.");
                    buckets.Add(key, bucket = []);
                }
                if (bucket.Count == SpatialOccupancyPage.MaximumEntries)
                    throw new InvalidOperationException("A complete occupancy page exceeds its entry budget.");
                bucket.Add(entry.EntityId, entry);
            }
        }
        var pages = ImmutableDictionary.CreateBuilder<SpatialOccupancyPageKey, SpatialOccupancyPage>();
        foreach (var (key, bucket) in buckets)
            pages.Add(key, new(scope, key, 0, bucket.Values));
        return new(scope, 0, maximumEntities, maximumPages, entities.ToImmutable(), pages.ToImmutable());
    }

    public SpatialOccupancyReference? Find(EntityId entityId) => _entities.GetValueOrDefault(entityId);

    /// <summary>Absent keys are known empty only because this instance indexes the complete supplied catalog.</summary>
    public SpatialOccupancyPage ReadPage(SpatialOccupancyPageKey key) =>
        _pages.TryGetValue(key, out var page) ? page : new(Scope, key, 0, []);

    /// <summary>
    /// Replaces all touched pages and identity entries together, returning a separate immutable successor.
    /// Removal retains an empty page's revision so departure/return cannot resurrect a stale page pin.
    /// </summary>
    public SpatialOccupancyDirectory Apply(ulong expectedRevision, IEnumerable<SpatialOccupancyChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (expectedRevision != Revision)
            throw new InvalidOperationException("The occupancy directory revision changed.");
        var selected = new SortedDictionary<EntityId, SpatialOccupancyChange>();
        foreach (var change in changes)
        {
            ArgumentNullException.ThrowIfNull(change);
            if (selected.Count == MaximumChanges || !selected.TryAdd(change.EntityId, change))
                throw new ArgumentException("A batch must have bounded, unique entity changes.", nameof(changes));
            if ((change.Before ?? change.After!).Scope != Scope || Find(change.EntityId) != change.Before)
                throw new InvalidOperationException("An occupancy change differs from its exact current entity reference.");
        }
        if (selected.Count == 0)
            throw new ArgumentException("An occupancy update must contain a change.", nameof(changes));
        int resultingCount = checked(EntityCount + selected.Values.Count(change => change.Before is null)
            - selected.Values.Count(change => change.After is null));
        if (resultingCount > MaximumEntities)
            throw new InvalidOperationException("The successor exceeds its entity metadata budget.");
        ulong nextRevision = checked(Revision + 1);
        var entities = _entities.ToBuilder();
        var pages = _pages.ToBuilder();
        var touched = new Dictionary<SpatialOccupancyPageKey, SortedDictionary<EntityId, SpatialOccupancyReference>>();
        int addedPages = 0;
        foreach (var change in selected.Values)
        {
            foreach (var reference in new[] { change.Before, change.After })
            {
                if (reference is null) continue;
                foreach (var key in SpatialOccupancyPageKey.Cover(reference.Bounds))
                {
                    if (touched.ContainsKey(key)) continue;
                    if (!_pages.ContainsKey(key) && ++addedPages > MaximumPages - _pages.Count)
                        throw new InvalidOperationException("The successor exceeds its retained metadata page budget.");
                    touched.Add(key, new(ReadPage(key).Entries.ToDictionary(entry => entry.EntityId)));
                }
            }
        }
        // Remove every old copy before inserting any new one: final capacity, not input order,
        // decides whether simultaneous moves or a full-page swap can be represented.
        foreach (var change in selected.Values)
        {
            entities.Remove(change.EntityId);
            if (change.Before is not { } before) continue;
            foreach (var key in SpatialOccupancyPageKey.Cover(before.Bounds))
                if (!touched[key].Remove(change.EntityId))
                    throw new InvalidOperationException("The complete index lost an existing cross-page copy.");
        }
        foreach (var change in selected.Values)
        {
            if (change.After is not { } after) continue;
            entities.Add(after.EntityId, after);
            foreach (var key in SpatialOccupancyPageKey.Cover(after.Bounds))
                touched[key].Add(after.EntityId, after);
        }
        foreach (var (key, bucket) in touched)
        {
            // Constructor checks final page density; failures expose neither index nor partial pages.
            pages[key] = new(Scope, key, nextRevision, bucket.Values);
        }
        return new(Scope, nextRevision, MaximumEntities, MaximumPages, entities.ToImmutable(), pages.ToImmutable());
    }
}
