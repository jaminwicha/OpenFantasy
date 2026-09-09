using System.Text.Json;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Core.Tests.Terrain;

internal static class TerrainFixture
{
    internal static JsonElement Read()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "terrain-v1.json")));
        return document.RootElement.Clone();
    }

    internal static JsonElement Example(string name) => Read().GetProperty("examples").EnumerateArray()
        .Single(example => example.GetProperty("name").GetString() == name);

    internal static TerrainRegionSnapshot Generate(JsonElement example)
    {
        JsonElement scope = example.GetProperty("scope");
        JsonElement rules = example.GetProperty("rules");
        JsonElement bounds = example.GetProperty("bounds");
        JsonElement[] pins = rules.GetProperty("materials").EnumerateArray().ToArray();
        int[] size = bounds.GetProperty("size").EnumerateArray().Select(value => value.GetInt32()).ToArray();
        return new StratifiedTerrainGenerator().Generate(
            new TerrainScope(Id(scope, "worldId"), Id(scope, "branchId"), Id(scope, "realmId")),
            Id(example, "seed"),
            new StratifiedTerrainRules(rules.GetProperty("baseSurfaceZ").GetInt64(),
                rules.GetProperty("relief").GetInt32(), rules.GetProperty("soilDepth").GetInt32(),
                new TerrainMaterialPalette(Pin(pins[0]), Pin(pins[1]), Pin(pins[2]))),
            new TerrainRegionBounds(Coordinate(bounds.GetProperty("minimum")), size[0], size[1], size[2]));
    }

    internal static CellCoordinate Coordinate(JsonElement array)
    {
        long[] values = array.EnumerateArray().Select(value => value.GetInt64()).ToArray();
        return new CellCoordinate(values[0], values[1], values[2]);
    }

    internal static CountedItemDefinition Pin(JsonElement pin) =>
        new(Id(pin, "definitionId"), new ContentHash(pin.GetProperty("version").GetString()!));

    private static EntityId Id(JsonElement element, string property) => EntityId.Parse(element.GetProperty(property).GetString());
}
