using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Language;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;

namespace OpenFantasy.Hosting.Content;

/// <summary>One exact authored admission profile; modifying its files requires a new reviewed pin.</summary>
internal sealed class FarmAdmissionDefinition
{
    internal const string ExpectedVersion = "990d2c7ac2983ebe85a7bdac4864ebfa1dc913e8d19e2b3e1f7b26ced7a0a992";
    private readonly JsonElement _presentation;

    private FarmAdmissionDefinition(JsonElement root, JsonElement presentation)
    {
        Root = root;
        _presentation = presentation;
    }

    internal JsonElement Root { get; }
    internal JsonElement Player => Root.GetProperty("player");
    internal JsonElement Wren => Root.GetProperty("wren");
    internal ContentHash Version => new(ExpectedVersion);

    internal static FarmAdmissionDefinition Load(FarmContentFiles files)
    {
        byte[] bytes = files.Read("native-farm/admission-v1.json", 32_768, ExpectedVersion);
        using JsonDocument admission = FarmContentFiles.Parse(bytes);
        JsonElement root = admission.RootElement;
        byte[] presentationBytes = files.Read("native-farm/presentation-v1.json", 16_384,
            root.GetProperty("presentationSha256").GetString()!);
        using JsonDocument presentation = FarmContentFiles.Parse(presentationBytes);
        foreach (string collection in new[] { "assets", "sourceSemantics" })
        {
            JsonElement entries = presentation.RootElement.GetProperty(collection);
            if (entries.GetArrayLength() > 8)
            {
                throw new InvalidDataException("The farm presentation witness count exceeds its profile.");
            }
            foreach (JsonElement entry in entries.EnumerateArray())
            {
                files.Read(entry.GetProperty("path").GetString()!, 65_536, entry.GetProperty("sha256").GetString()!);
            }
        }
        return new FarmAdmissionDefinition(root.Clone(), presentation.RootElement.Clone());
    }

    internal void ValidateSources(FarmSourcePacket source)
    {
        JsonElement rectangle = Root.GetProperty("place").GetProperty("sourceRectangle");
        foreach ((string sourceName, string admittedName) in new[] { ("x", "x"), ("y", "y"), ("w", "width"), ("h", "depth") })
        {
            if (source.Farm.GetProperty(sourceName).GetInt32() != rectangle.GetProperty(admittedName).GetInt32())
            {
                throw new InvalidDataException("The admitted farm rectangle differs from the retained source rectangle.");
            }
        }
        if (source.Wren.GetProperty("id").GetString() != "farmer_wren"
            || source.Farm.GetProperty("id").GetString() != "farm_verdance_small"
            || source.Wood.GetProperty("id").GetString() != "wood"
            || source.Wood.GetProperty("name").GetString() != "Wood"
            || source.Wood.GetProperty("kind").GetString() != "Material"
            || source.Quest.GetProperty("giver").GetString() != source.Wren.GetProperty("id").GetString()
            || source.Quest.GetProperty("targetItem").GetString() != source.Wood.GetProperty("id").GetString()
            || source.Quest.GetProperty("targetCount").GetInt32() != Root.GetProperty("deliveryProof").GetProperty("count").GetInt32()
            || FarmCanonicalJson.Hash(source.Wren.GetProperty("appearance")) != FarmCanonicalJson.Hash(_presentation.GetProperty("sourceAppearance"))
            || source.Wren.GetProperty("sprite").GetString() != _presentation.GetProperty("sourceSprite").GetString())
        {
            throw new InvalidDataException("A selected source field differs from this admitted farm profile.");
        }
        RequireInsideFarm(Player.GetProperty("tileX").GetInt32(), Player.GetProperty("tileY").GetInt32(), 0);
        RequireInsideFarm(source.Wren.GetProperty("tileX").GetInt32(), source.Wren.GetProperty("tileY").GetInt32(),
            source.Wren.GetProperty("wander").GetInt32());
    }

    internal BodyDefinition CreateBody(FarmIdentityScope ids)
    {
        JsonElement body = Root.GetProperty("body");
        BodyPartDefinition[] parts = body.GetProperty("parts").EnumerateArray().Select(part => new BodyPartDefinition(
            new BodySlotKey(part.GetProperty("slot").GetString()!),
            part.GetProperty("parent").ValueKind == JsonValueKind.Null ? null : new BodySlotKey(part.GetProperty("parent").GetString()!),
            Dimensions(part.GetProperty("dimensions")),
            part.GetProperty("functions").EnumerateArray().Select(function => (BodyFunction)function.GetInt32()))).ToArray();
        LocomotionRequirement[] locomotion = body.GetProperty("locomotion").EnumerateArray().Select(mode =>
            new LocomotionRequirement((LocomotionMode)mode.GetProperty("mode").GetInt32(), mode.GetProperty("minimumParts").GetInt32())).ToArray();
        return new BodyDefinition(ids.Definition(body.GetProperty("definitionKey").GetString()!),
            (BodyNature)body.GetProperty("nature").GetInt32(), Dimensions(body.GetProperty("clearance")), parts, locomotion);
    }

    internal TerrainRegionSnapshot CreateTerrain(TerrainScope scope, FarmIdentityScope ids)
    {
        JsonElement terrain = Root.GetProperty("terrain");
        JsonElement minimum = terrain.GetProperty("minimum");
        JsonElement size = terrain.GetProperty("size");
        CountedItemDefinition[] materials = terrain.GetProperty("materials").EnumerateArray()
            .Select(material => CountedDefinition(material, ids)).ToArray();
        var palette = new TerrainMaterialPalette(materials[0], materials[1], materials[2]);
        var rules = new StratifiedTerrainRules(terrain.GetProperty("baseSurfaceZ").GetInt64(),
            terrain.GetProperty("relief").GetInt32(), terrain.GetProperty("soilDepth").GetInt32(), palette);
        var bounds = new TerrainRegionBounds(new CellCoordinate(minimum[0].GetInt64(), minimum[1].GetInt64(), minimum[2].GetInt64()),
            size[0].GetInt32(), size[1].GetInt32(), size[2].GetInt32());
        TerrainRegionSnapshot region = new StratifiedTerrainGenerator().Generate(scope, ids.Instance("terrain-seed"), rules, bounds);
        ids.ValidateTerrainIdentities(region);
        return region;
    }

    internal CountedItemDefinition WoodDefinition(FarmIdentityScope ids) => CountedDefinition(Root.GetProperty("wood"), ids);

    internal LanguageChannelKey LanguageChannel(FarmIdentityScope ids)
    {
        JsonElement language = Root.GetProperty("language");
        return new LanguageChannelKey(ids.Definition(language.GetProperty("languageKey").GetString()!),
            ids.Definition(language.GetProperty("registerKey").GetString()!), (CommunicationMode)language.GetProperty("mode").GetInt32());
    }

    internal ContentHash LanguageVersion => FarmCanonicalJson.Hash(Root.GetProperty("language"));
    internal ContentHash ComprehensionRulesVersion => FarmCanonicalJson.Hash(Root.GetProperty("comprehensionRules"));

    internal EntityId PlaceId(FarmIdentityScope ids)
    {
        ids.Definition(Root.GetProperty("place").GetProperty("definitionKey").GetString()!);
        return ids.Instance("place.farm_verdance_small");
    }

    internal WorldInstant InitialCalendar => new(Root.GetProperty("initialCalendar").GetProperty("day").GetInt64(),
        Root.GetProperty("initialCalendar").GetProperty("millisecondsWithinDay").GetInt32());

    internal static SubunitPosition Feet(int x, int y, long z) => new(
        checked((long)x * SubunitPosition.SubunitsPerCell + SubunitPosition.SubunitsPerCell / 2),
        checked((long)y * SubunitPosition.SubunitsPerCell + SubunitPosition.SubunitsPerCell / 2), z);

    private void RequireInsideFarm(int x, int y, int radius)
    {
        JsonElement rectangle = Root.GetProperty("place").GetProperty("sourceRectangle");
        int left = rectangle.GetProperty("x").GetInt32();
        int bottom = rectangle.GetProperty("y").GetInt32();
        int right = checked(left + rectangle.GetProperty("width").GetInt32());
        int top = checked(bottom + rectangle.GetProperty("depth").GetInt32());
        if (radius < 0 || x - radius < left || x + radius >= right || y - radius < bottom || y + radius >= top)
        {
            throw new InvalidDataException("An admitted home or wandering envelope lies outside the source farm rectangle.");
        }
    }

    private static BodyDimensions Dimensions(JsonElement values) => new(values[0].GetInt64(), values[1].GetInt64(), values[2].GetInt64());

    private static CountedItemDefinition CountedDefinition(JsonElement value, FarmIdentityScope ids) =>
        new(ids.Definition(value.GetProperty("definitionKey").GetString()!), FarmCanonicalJson.Hash(value));
}
