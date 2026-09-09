using OpenFantasy.Core.Content;

namespace OpenFantasy.Core.Terrain;

/// <summary>A pinned small-relief generator profile, not a geological history or water model.</summary>
public sealed class StratifiedTerrainRules
{
    public const int LatticeSpacing = 8;
    public const int MaximumRelief = 8;
    public const int MaximumSoilDepth = 16;

    public StratifiedTerrainRules(long baseSurfaceZ, int relief, int soilDepth, TerrainMaterialPalette materials)
    {
        ArgumentNullException.ThrowIfNull(materials);
        if (relief is < 0 or > MaximumRelief)
        {
            throw new ArgumentOutOfRangeException(nameof(relief));
        }
        if (soilDepth is < 1 or > MaximumSoilDepth)
        {
            throw new ArgumentOutOfRangeException(nameof(soilDepth));
        }
        // Every permitted surface and its soil boundary must be representable, for every seed.
        _ = checked(baseSurfaceZ + relief);
        _ = checked(baseSurfaceZ - relief - soilDepth);
        BaseSurfaceZ = baseSurfaceZ;
        Relief = relief;
        SoilDepth = soilDepth;
        Materials = materials;
        Version = ContentHash.Compute(TerrainRegionCodecV1.EncodeRules(this));
    }

    public long BaseSurfaceZ { get; }
    public int Relief { get; }
    public int SoilDepth { get; }
    public TerrainMaterialPalette Materials { get; }
    public ContentHash Version { get; }
}
