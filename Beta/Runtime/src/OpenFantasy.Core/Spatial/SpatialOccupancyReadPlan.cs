using System.Collections.ObjectModel;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Spatial;

/// <summary>Bounded complete-page reads with phantom detection; the caller supplies directory authority and stability.</summary>
public sealed class SpatialOccupancyReadPlan
{
    public const int MaximumPages = 32;

    public SpatialOccupancyReadPlan(TerrainScope scope, TerrainRegionBounds bounds,
        Func<SpatialOccupancyPageKey, SpatialOccupancyPage?> readPage, int maximumPages,
        int maximumActors, int maximumStructures)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(readPage);
        if (maximumPages is < 1 or > MaximumPages || maximumActors is < 1 or > LocalSpatialSnapshot.MaximumActors
            || maximumStructures is < 0 or > LocalSpatialSnapshot.MaximumStructures)
            throw new ArgumentOutOfRangeException(nameof(maximumPages), "Query budgets must fit the current movement kernel.");
        var keys = SpatialOccupancyPageKey.Cover(bounds).ToArray();
        if (keys.Length > maximumPages) throw new InvalidOperationException("Occupancy query exceeds its page budget.");
        var pages = new Dictionary<SpatialOccupancyPageKey, SpatialOccupancyPage>();
        var references = new SortedDictionary<EntityId, SpatialOccupancyReference>();
        foreach (var key in keys)
        {
            var page = readPage(key) ?? throw new InvalidOperationException("Required occupancy metadata is missing; absence is not empty space.");
            if (page.Scope != scope || page.Key != key) throw new ArgumentException("Occupancy provider returned the wrong page or scope.", nameof(readPage));
            pages.Add(key, page);
            foreach (var entry in page.Entries)
            {
                if (references.TryGetValue(entry.EntityId, out var prior) && prior != entry)
                    throw new InvalidOperationException("Cross-page occupancy references disagree about one entity.");
                references[entry.EntityId] = entry;
            }
        }
        // A supplied reference must appear identically in every queried page it overlaps.
        // This cannot discover an entity omitted from all supplied pages; completeness is an authority contract.
        foreach (var entry in references.Values)
            foreach (var key in SpatialOccupancyPageKey.Cover(entry.Bounds))
                if (pages.TryGetValue(key, out var page) && !page.Contains(entry))
                    throw new InvalidOperationException("A cross-page collider is missing from an overlapping queried page.");
        var selected = references.Values.Where(entry => SpatialOccupancyReference.Intersects(entry.Bounds, bounds)).ToArray();
        var actors = selected.Where(entry => entry.Kind == SpatialOccupancyKind.Actor).ToArray();
        var structures = selected.Where(entry => entry.Kind == SpatialOccupancyKind.Structure).ToArray();
        if (actors.Length > maximumActors || structures.Length > maximumStructures)
            throw new InvalidOperationException("Complete occupancy exceeds the selected detail budget; no collider may be omitted.");
        Scope = scope; Bounds = bounds;
        Pages = Array.AsReadOnly(keys.Select(key => new SpatialOccupancyPageReference(key, pages[key].Revision, pages[key].ContentHash)).ToArray());
        Actors = Array.AsReadOnly(actors); Structures = Array.AsReadOnly(structures);
    }

    public TerrainScope Scope { get; }
    public TerrainRegionBounds Bounds { get; }
    public ReadOnlyCollection<SpatialOccupancyPageReference> Pages { get; }
    public ReadOnlyCollection<SpatialOccupancyReference> Actors { get; }
    public ReadOnlyCollection<SpatialOccupancyReference> Structures { get; }

    public void ValidateCurrent(Func<SpatialOccupancyPageKey, SpatialOccupancyPage?> readPage)
    {
        ArgumentNullException.ThrowIfNull(readPage);
        foreach (var expected in Pages)
        {
            var page = readPage(expected.Key);
            if (page is null || page.Scope != Scope || page.Key != expected.Key
                || page.Revision != expected.Revision || page.ContentHash != expected.ContentHash)
                throw new InvalidOperationException("A required occupancy page changed or disappeared after preparation.");
        }
    }

    public SpatialOccupancyReadSet Resolve(IEnumerable<ActorPresence> actors, IEnumerable<SpatialStructure> structures)
    {
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(structures);
        var requiredActors = Actors.ToDictionary(entry => entry.EntityId);
        var requiredStructures = Structures.ToDictionary(entry => entry.EntityId);
        var resolvedActors = new SortedDictionary<EntityId, ActorPresence>();
        var resolvedStructures = new SortedDictionary<EntityId, SpatialStructure>();
        foreach (var actor in actors)
        {
            ArgumentNullException.ThrowIfNull(actor);
            if (!requiredActors.Remove(actor.ActorId, out var expected) || SpatialOccupancyReference.Capture(actor) != expected)
                throw new ArgumentException("Actor detail is unrequested, duplicated or differs from its exact reference.", nameof(actors));
            resolvedActors.Add(actor.ActorId, actor);
        }
        foreach (var structure in structures)
        {
            ArgumentNullException.ThrowIfNull(structure);
            if (!requiredStructures.Remove(structure.StructureId, out var expected) || SpatialOccupancyReference.Capture(Scope, structure) != expected)
                throw new ArgumentException("Structure detail is unrequested, duplicated or differs from its exact reference.", nameof(structures));
            resolvedStructures.Add(structure.StructureId, structure);
        }
        if (requiredActors.Count != 0 || requiredStructures.Count != 0)
            throw new InvalidOperationException("Required occupancy detail is missing; no complete read set can be returned.");
        return new(this, resolvedActors.Values.ToArray(), resolvedStructures.Values.ToArray());
    }
}

public sealed class SpatialOccupancyReadSet
{
    internal SpatialOccupancyReadSet(SpatialOccupancyReadPlan plan, ActorPresence[] actors, SpatialStructure[] structures)
    {
        Plan = plan; Actors = Array.AsReadOnly(actors); Structures = Array.AsReadOnly(structures);
    }
    public SpatialOccupancyReadPlan Plan { get; }
    public ReadOnlyCollection<ActorPresence> Actors { get; }
    public ReadOnlyCollection<SpatialStructure> Structures { get; }
}
