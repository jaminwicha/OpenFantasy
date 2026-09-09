using System.Globalization;
using System.Text.Json;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;

namespace OpenFantasy.Core.Tests.Production;

internal static class ProductionFixture
{
    private static readonly JsonElement _fixture = Load();
    internal static JsonElement Read() => _fixture;
    internal static JsonElement Calibration => _fixture.GetProperty("calibration");
    internal static PackageId Package => new(Calibration.GetProperty("package").GetString()!);

    internal static MaterialDefinition[] Materials() => Calibration.GetProperty("materials").EnumerateArray()
        .Select(value => new MaterialDefinition(Package, EntityId.Parse(value.GetProperty("id").GetString()),
            value.GetProperty("key").GetString()!, new MassMilligrams(Number(value, "massMilligrams")),
            new VolumeMicrolitres(Number(value, "volumeMicrolitres")), new HardnessRating(value.GetProperty("hardness").GetUInt32())))
        .ToArray();

    internal static RecipeTerm[] Terms() => Calibration.GetProperty("terms").EnumerateArray()
        .Select(value => new RecipeTerm((RecipeMaterialRole)value.GetProperty("role").GetInt32(),
            new CountedItemDefinition(EntityId.Parse(value.GetProperty("materialId").GetString()),
                new ContentHash(value.GetProperty("version").GetString()!)), Number(value, "count"))).ToArray();

    internal static MaterialRecipeDefinition Create(IEnumerable<RecipeTerm>? terms = null,
        MaterialCatalog? catalog = null, WorkUnits? work = null, ContentHash? sourceHash = null) =>
        new(Package, EntityId.Parse(Calibration.GetProperty("definitionId").GetString()), "craft_rations",
            work ?? new WorkUnits(120), sourceHash ?? SourceHash(), catalog ?? new MaterialCatalog(Materials()), terms ?? Terms());

    internal static ContentHash SourceHash() => new(_fixture.GetProperty("sourceRecipe").GetProperty("payloadSha256").GetString()!);
    internal static byte[] Bytes(string name) => Convert.FromHexString(_fixture.GetProperty(name).GetString()!);
    private static ulong Number(JsonElement element, string name) => ulong.Parse(element.GetProperty(name).GetString()!, CultureInfo.InvariantCulture);

    private static JsonElement Load()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "production-v1.json")));
        return document.RootElement.Clone();
    }
}
