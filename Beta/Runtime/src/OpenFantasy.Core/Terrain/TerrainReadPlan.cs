using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Terrain;

/// <summary>
/// A complete, unambiguous terrain read rectangle prepared from scoped region references.
/// The caller establishes directory authority and chooses the full query footprint, including support.
/// </summary>
public sealed class TerrainReadPlan
{
    // Per-preparation metadata bound, not a world population, directory or terrain size limit.
    public const int MaximumCandidateRegions = 4096;

    public TerrainReadPlan(TerrainScope scope, TerrainRegionBounds requestedBounds,
        IEnumerable<TerrainRegionReference> candidates, int maximumLoadedRegions)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(requestedBounds);
        ArgumentNullException.ThrowIfNull(candidates);
        if (maximumLoadedRegions is < 1 or > MaximumCandidateRegions)
            throw new ArgumentOutOfRangeException(nameof(maximumLoadedRegions));
        Scope = scope;
        RequestedBounds = requestedBounds;
        MaximumLoadedRegions = maximumLoadedRegions;
        var selected = new List<TerrainRegionReference>();
        int examined = 0;
        int coveredCells = 0;
        foreach (TerrainRegionReference reference in candidates)
        {
            ArgumentNullException.ThrowIfNull(reference);
            if (++examined > MaximumCandidateRegions)
                throw new ArgumentException("Terrain candidate metadata exceeds the preparation bound.", nameof(candidates));
            if (reference.Scope != scope)
                throw new ArgumentException("Terrain references must belong to the requested world, branch and realm.", nameof(candidates));
            TerrainRegionBounds? intersection = Intersect(requestedBounds, reference.Bounds);
            if (intersection is null) continue;
            if (selected.Count == maximumLoadedRegions)
                throw new InvalidOperationException("Terrain dependencies exceed the declared residency budget.");
            if (selected.Any(prior => Intersect(prior.Bounds, reference.Bounds) is not null))
                throw new ArgumentException("Terrain dependency coverage is ambiguous or duplicated.", nameof(candidates));
            selected.Add(reference);
            coveredCells = checked(coveredCells + intersection.CellCount);
        }
        if (coveredCells != requestedBounds.CellCount)
            throw new InvalidOperationException("Terrain dependencies do not cover the complete requested volume.");
        References = Array.AsReadOnly(selected.OrderBy(reference => reference.Bounds.Minimum.Z)
            .ThenBy(reference => reference.Bounds.Minimum.Y).ThenBy(reference => reference.Bounds.Minimum.X).ToArray());
        TotalLoadedCells = References.Sum(reference => reference.Bounds.CellCount);
    }

    public TerrainScope Scope { get; }
    public TerrainRegionBounds RequestedBounds { get; }
    public int MaximumLoadedRegions { get; }
    public int TotalLoadedCells { get; }
    public ReadOnlyCollection<TerrainRegionReference> References { get; }

    /// <summary>
    /// Checks a fresh scoped directory reading. This is not an atomic publication check: the authority
    /// must keep these dependencies stable through commit or repeat preparation under its own boundary.
    /// </summary>
    public void ValidateCurrentReferences(IEnumerable<TerrainRegionReference> current)
    {
        var refreshed = new TerrainReadPlan(Scope, RequestedBounds, current, MaximumLoadedRegions);
        if (!References.SequenceEqual(refreshed.References))
            throw new InvalidOperationException("A pinned terrain dependency changed after preparation.");
    }

    public TerrainReadSet Resolve(IEnumerable<TerrainRegionSnapshot> regions)
    {
        ArgumentNullException.ThrowIfNull(regions);
        var remaining = References.ToDictionary(reference => reference.Bounds);
        var resolved = new Dictionary<TerrainRegionBounds, TerrainRegionSnapshot>();
        foreach (TerrainRegionSnapshot region in regions)
        {
            ArgumentNullException.ThrowIfNull(region);
            if (!remaining.Remove(region.Bounds, out TerrainRegionReference? expected)
                || TerrainRegionReference.Capture(region) != expected)
                throw new ArgumentException("Loaded terrain is unrequested, duplicated or differs from its exact pin.", nameof(regions));
            resolved.Add(region.Bounds, region);
        }
        if (remaining.Count != 0)
            throw new InvalidOperationException("Required terrain is missing; no complete read set can be admitted.");
        return new(this, References.Select(reference => resolved[reference.Bounds]).ToArray());
    }

    private static TerrainRegionBounds? Intersect(TerrainRegionBounds left, TerrainRegionBounds right)
    {
        long x = Math.Max(left.Minimum.X, right.Minimum.X);
        long y = Math.Max(left.Minimum.Y, right.Minimum.Y);
        long z = Math.Max(left.Minimum.Z, right.Minimum.Z);
        long endX = Math.Min(left.Maximum.X, right.Maximum.X);
        long endY = Math.Min(left.Maximum.Y, right.Maximum.Y);
        long endZ = Math.Min(left.Maximum.Z, right.Maximum.Z);
        if (x > endX || y > endY || z > endZ) return null;
        return new(new(x, y, z), checked((int)(endX - x + 1)),
            checked((int)(endY - y + 1)), checked((int)(endZ - z + 1)));
    }
}
