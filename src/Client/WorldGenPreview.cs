using Godot;
using OpenFantasy.Sim;

namespace OpenFantasy.Client;

/// <summary>
/// Dev tooling (`--previewgen [seed]`): renders the biome classification to a
/// PNG so terrain tuning is reviewed by eye before anything reaches gameplay.
/// Presentation-side only — reads the Sim generator, never feeds it.
/// </summary>
public static class WorldGenPreview
{
    private static readonly Dictionary<Biome, Color> Colors = new()
    {
        [Biome.Ocean] = new Color("1a3a6e"),
        [Biome.Coast] = new Color("d9c27e"),
        [Biome.Grassland] = new Color("58a14e"),
        [Biome.Forest] = new Color("2e6b34"),
        [Biome.Jungle] = new Color("1e4f28"),
        [Biome.Swamp] = new Color("4a5d3a"),
        [Biome.Desert] = new Color("d8b56a"),
        [Biome.Canyon] = new Color("b0713c"),
        [Biome.Snowfield] = new Color("e8ecf2"),
        [Biome.Glacier] = new Color("b8d4e8"),
        [Biome.Hills] = new Color("7a7f4a"),
        [Biome.Mountain] = new Color("7d7d85"),
        [Biome.CrystalWaste] = new Color("9e5fd0"),
    };

    /// <summary>±1024 tiles around the origin at 4 tiles/px → 512×512 png.</summary>
    public static void SaveBiomeMap(ulong seed, string path)
    {
        const int half = 1024, step = 4, size = half * 2 / step;
        var field = new BiomeField(seed);
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgb8);
        for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
                img.SetPixel(px, py,
                    Colors[field.Classify(px * step - half, py * step - half)]);
        img.SavePng(path);
    }
}
