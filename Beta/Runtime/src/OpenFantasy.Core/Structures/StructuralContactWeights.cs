using OpenFantasy.Core.Geography;

namespace OpenFantasy.Core.Structures;

internal sealed record StructuralContactSurface(EntityId ElementId, BoundaryRectangle Bounds,
    long StandingZ, Int128 PhysicalTop);

/// <summary>Partitions an exact footprint onto its uppermost eligible physical contacts without counting overlap twice.</summary>
internal static class StructuralContactWeights
{
    internal static IReadOnlyDictionary<EntityId, ulong> Resolve(BoundaryRectangle footprint,
        IEnumerable<StructuralContactSurface> surfaces)
    {
        var patches = surfaces.Select(value => (Surface: value, Bounds: Intersect(footprint, value.Bounds)))
            .Where(value => value.Bounds is not null).ToArray();
        var xs = patches.SelectMany(value => new[] { value.Bounds!.MinU, value.Bounds.MaxU })
            .Append(footprint.MinU).Append(footprint.MaxU).Distinct().Order().ToArray();
        SortedDictionary<EntityId, ulong> weights = [];
        for (int x = 1; x < xs.Length; x++)
        {
            Int128 left = xs[x - 1];
            Int128 right = xs[x];
            var events = new SortedDictionary<Int128, List<(int Index, bool Start)>>();
            for (int index = 0; index < patches.Length; index++)
            {
                BoundaryRectangle bounds = patches[index].Bounds!;
                if (bounds.MinU > left || bounds.MaxU < right) { continue; }
                AddEvent(bounds.MinV, index, true);
                AddEvent(bounds.MaxV, index, false);
            }
            var active = new SortedSet<(Int128 Height, EntityId Element, int Index)>();
            Int128 previous = footprint.MinV;
            foreach (var entry in events)
            {
                if (entry.Key > previous)
                {
                    if (active.Count == 0) { throw new ArgumentException("The structural footprint has an unqualified support gap."); }
                    EntityId element = active.Min.Element;
                    ulong area = checked((ulong)((right - left) * (entry.Key - previous)));
                    weights[element] = checked(weights.GetValueOrDefault(element) + area);
                }
                foreach (var change in entry.Value)
                {
                    var surface = patches[change.Index].Surface;
                    var key = (-surface.PhysicalTop, surface.ElementId, change.Index);
                    if (change.Start) { active.Add(key); } else { active.Remove(key); }
                }
                previous = entry.Key;
            }
            if (previous != footprint.MaxV) { throw new ArgumentException("The structural footprint lacks complete rated contact."); }

            void AddEvent(Int128 y, int index, bool start)
            {
                if (!events.TryGetValue(y, out var list)) { events.Add(y, list = []); }
                list.Add((index, start));
            }
        }
        return weights;
    }

    private static BoundaryRectangle? Intersect(BoundaryRectangle left, BoundaryRectangle right)
    {
        Int128 minX = Int128.Max(left.MinU, right.MinU);
        Int128 minY = Int128.Max(left.MinV, right.MinV);
        Int128 maxX = Int128.Min(left.MaxU, right.MaxU);
        Int128 maxY = Int128.Min(left.MaxV, right.MaxV);
        return minX < maxX && minY < maxY ? new(minX, minY, maxX, maxY) : null;
    }
}
