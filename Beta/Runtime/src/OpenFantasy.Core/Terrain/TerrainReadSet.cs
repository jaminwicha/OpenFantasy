using System.Collections.ObjectModel;

namespace OpenFantasy.Core.Terrain;

/// <summary>Resolved immutable terrain dependencies; actors, structures and other world facets remain separate.</summary>
public sealed class TerrainReadSet
{
    internal TerrainReadSet(TerrainReadPlan plan, TerrainRegionSnapshot[] regions)
    {
        Plan = plan;
        Regions = Array.AsReadOnly((TerrainRegionSnapshot[])regions.Clone());
    }

    public TerrainReadPlan Plan { get; }
    public ReadOnlyCollection<TerrainRegionSnapshot> Regions { get; }
}
