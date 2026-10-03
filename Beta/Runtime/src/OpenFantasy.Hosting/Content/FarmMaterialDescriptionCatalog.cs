using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Hosting.Content;

/// <summary>Public labels joined only to exact validated authored item pins, never to a tool kind or held stock.</summary>
internal static class FarmMaterialDescriptionCatalog
{
    private static readonly Lazy<IReadOnlyDictionary<CountedItemDefinition, LocalMaterialDescription>> Catalog = new(
        () => Load(FarmContentFiles.Embedded()));

    internal static LocalMaterialDescription? Find(CountedItemDefinition item) => Catalog.Value.GetValueOrDefault(item);

    internal static bool Matches(LocalMaterialDescription item) =>
        Find(new(item.DefinitionId, item.ItemVersion)) == item;

    internal static IReadOnlyDictionary<CountedItemDefinition, LocalMaterialDescription> Load(FarmContentFiles files)
    {
        var production = ProductionFarmDefinition.Load(files);
        ProductionFarmSourceValidation.Validate(files, production);
        using var gathered = FarmContentFiles.Parse(files.Read(GatheringFarmDefinition.RelativePath, 16_384, GatheringFarmDefinition.AcceptedHash));
        var original = GatheringFarmSources.Load(files, gathered.RootElement);
        var result = new Dictionary<CountedItemDefinition, LocalMaterialDescription>();
        foreach (var item in production.Root.GetProperty("existingItems").EnumerateArray())
        {
            var pin = ProductionFarmDefinition.ExistingItem(item);
            string key = item.GetProperty("key").GetString()!;
            Add(pin, key, original.Record(item.GetProperty("sourceTargetKey").GetString()!).GetProperty("name").GetString()!);
        }
        foreach (var item in gathered.RootElement.GetProperty("items").EnumerateArray())
        {
            string key = item.GetProperty("key").GetString()!;
            var source = original.Record(item.GetProperty("sourceTargetKey").GetString()!);
            if (FarmCanonicalJson.Hash(source).Value != item.GetProperty("sourcePayloadSha256").GetString()
                || "items/" + source.GetProperty("id").GetString() != key)
                throw new InvalidDataException("A material description disagrees with its exact gathering source.");
            var id = EntityId.DeriveGenerated("openfantasy.authored-content"u8, "adventure-farm.definitions.v1", key, "definition", 0);
            Add(new(id, FarmCanonicalJson.Hash(item)), key, source.GetProperty("name").GetString()!);
        }
        foreach (var item in production.Root.GetProperty("outputItems").EnumerateArray())
        {
            string key = item.GetProperty("key").GetString()!;
            var id = EntityId.DeriveGenerated("openfantasy.authored-content"u8, "production-farm.definitions.v1", key, "definition", 0);
            Add(new(id, FarmCanonicalJson.Hash(item)), key, item.GetProperty("displayName").GetString()!);
        }
        var care = CareFarmSources.Load(files);
        using var items = FarmContentFiles.Parse(files.Read("legacy/aldermere-first-loop/source-support/data/items.json", 32_768,
            "ac0944b98fbfc10474ae1d1309bd7ad6b74c511fb2da9c314ab41f6c8a655609"));
        var potion = items.RootElement.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("id").GetString() == "potion");
        if (FarmCanonicalJson.Hash(potion) != care.PotionVersion)
            throw new InvalidDataException("The Potion label requires its original exact item payload.");
        Add(new(CareJourneyAdmission.Definition("items/potion"), care.PotionVersion), "items/potion", potion.GetProperty("name").GetString()!);
        return new System.Collections.ObjectModel.ReadOnlyDictionary<CountedItemDefinition, LocalMaterialDescription>(result);

        void Add(CountedItemDefinition pin, string key, string name)
        {
            var descriptor = new LocalMaterialDescription(pin.DefinitionId, pin.Version, key, name);
            if (result.TryGetValue(pin, out var prior))
            {
                if (prior != descriptor) throw new InvalidDataException("An item pin has contradictory public metadata.");
                return;
            }
            if (result.Keys.Any(existing => existing.DefinitionId == pin.DefinitionId))
                throw new InvalidDataException("Public content contains conflicting versions for one item identity.");
            result.Add(pin, descriptor);
        }
    }
}
