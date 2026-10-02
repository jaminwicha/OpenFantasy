using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Hosting.Simulation;

/// <summary>Owned immutable boolean cells; no caller retains the array behind the readonly collection.</summary>
internal sealed class LocalTerrainOccupancy(IEnumerable<bool> cells) : ReadOnlyCollection<bool>(cells.ToArray())
{
    private static readonly ConditionalWeakTable<TerrainRegionSnapshot, LocalTerrainOccupancy> Projections = new();

    /// <summary>
    /// All observers of the exact immutable region can share its owned public occupancy. Weak keys
    /// retain neither old regions nor world roots; equal bounds/revisions are not an identity match.
    /// Wire rehydration still uses the copying constructor and never enters this projection cache.
    /// </summary>
    internal static LocalTerrainOccupancy Project(TerrainRegionSnapshot region)
    {
        ArgumentNullException.ThrowIfNull(region);
        return Projections.GetValue(region, static source => new(source.Cells.Select(cell => cell.IsSolid)));
    }
}
