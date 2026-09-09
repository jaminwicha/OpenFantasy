using OpenFantasy.Core.Custody;

namespace OpenFantasy.Core.Terrain;

/// <summary>Exact material pins for three substrate roles, resolved by the admitting content catalog.</summary>
public sealed record TerrainMaterialPalette
{
    public TerrainMaterialPalette(CountedItemDefinition surface, CountedItemDefinition soil, CountedItemDefinition stone)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(soil);
        ArgumentNullException.ThrowIfNull(stone);
        CountedItemDefinition[] pins = [surface, soil, stone];
        foreach (CountedItemDefinition pin in pins)
        {
            if (pins.Any(other => other.DefinitionId == pin.DefinitionId && other.Version != pin.Version))
            {
                throw new ArgumentException("A material identity cannot have conflicting versions in one palette.");
            }
        }
        Surface = surface;
        Soil = soil;
        Stone = stone;
    }

    public CountedItemDefinition Surface { get; }
    public CountedItemDefinition Soil { get; }
    public CountedItemDefinition Stone { get; }

    internal CountedItemDefinition For(TerrainStratum stratum) => stratum switch
    {
        TerrainStratum.Surface => Surface,
        TerrainStratum.Soil => Soil,
        TerrainStratum.Stone => Stone,
        _ => throw new ArgumentOutOfRangeException(nameof(stratum))
    };
}
