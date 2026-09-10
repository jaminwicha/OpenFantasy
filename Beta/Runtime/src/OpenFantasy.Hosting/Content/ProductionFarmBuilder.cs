using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>Attaches one finite physical interpretation to existing item pins without issuing any stock.</summary>
internal sealed class ProductionFarmBuilder
{
    private readonly WorldSnapshot _world;
    private readonly ProductionFarmDefinition _definition;
    private readonly Dictionary<string, CountedItemDefinition> _items = [];
    private readonly Dictionary<string, MaterialDefinition> _materials = [];
    private readonly Dictionary<EntityId, string> _newIdentities = [];

    internal ProductionFarmBuilder(WorldSnapshot world, ProductionFarmDefinition definition)
    {
        _world = world;
        _definition = definition;
    }

    internal ProductionSnapshot Build()
    {
        BindExistingItems();
        DefineOutputs();
        DefinePhysicalMaterials();
        CraftRecipeDefinition recipe = CreateRecipe();
        var production = new ProductionSnapshot(_world.WorldId, _world.BranchId, 0, [recipe], [], []);
        production.ValidateForAuthority(_world.Inventory);
        return production;
    }

    private void BindExistingItems()
    {
        foreach (JsonElement metadata in _definition.Root.GetProperty("existingItems").EnumerateArray())
        {
            CountedItemDefinition pin = ProductionFarmDefinition.ExistingItem(metadata);
            if (!_world.Inventory.Sources.Any(source => source.Definition == pin))
            {
                throw new ArgumentException("The gathered world does not contain the exact original production input item pin.");
            }
            _items.Add(metadata.GetProperty("key").GetString()!, pin);
        }
    }

    private void DefineOutputs()
    {
        foreach (JsonElement metadata in _definition.Root.GetProperty("outputItems").EnumerateArray())
        {
            string key = metadata.GetProperty("key").GetString()!;
            _items.Add(key, new CountedItemDefinition(DefinitionId(key), FarmCanonicalJson.Hash(metadata)));
        }
    }

    private void DefinePhysicalMaterials()
    {
        var package = new PackageId(_definition.Root.GetProperty("physicalPackage").GetString()!);
        foreach (JsonElement metadata in _definition.Root.GetProperty("physicalMaterials").EnumerateArray())
        {
            var material = new MaterialDefinition(package,
                DefinitionId(metadata.GetProperty("definitionKey").GetString()!), metadata.GetProperty("key").GetString()!,
                new MassMilligrams(metadata.GetProperty("massMilligrams").GetUInt64()),
                new VolumeMicrolitres(metadata.GetProperty("volumeMicrolitres").GetUInt64()),
                new HardnessRating(metadata.GetProperty("hardness").GetUInt32()));
            _materials.Add(metadata.GetProperty("itemKey").GetString()!, material);
        }
    }

    private CraftRecipeDefinition CreateRecipe()
    {
        JsonElement metadata = _definition.Recipe;
        RecipeTerm[] terms = metadata.GetProperty("terms").EnumerateArray().Select(term => new RecipeTerm(
            (RecipeMaterialRole)term.GetProperty("role").GetInt32(),
            _materials[term.GetProperty("itemKey").GetString()!].Pin(), term.GetProperty("count").GetUInt64())).ToArray();
        var physical = new MaterialRecipeDefinition(new PackageId(_definition.Root.GetProperty("physicalPackage").GetString()!),
            DefinitionId(metadata.GetProperty("physicalDefinitionKey").GetString()!), metadata.GetProperty("key").GetString()!,
            new WorkUnits(metadata.GetProperty("requiredWork").GetUInt64()),
            new ContentHash(metadata.GetProperty("sourcePayloadSha256").GetString()!), new MaterialCatalog(_materials.Values), terms);
        ItemPhysicalBinding[] bindings = _materials.Select(pair => new ItemPhysicalBinding(_items[pair.Key], pair.Value)).ToArray();
        OutputMaterialAllocation[] allocations = metadata.GetProperty("allocations").EnumerateArray()
            .Select(value => new OutputMaterialAllocation(_items[value.GetProperty("inputItemKey").GetString()!],
                value.GetProperty("outputIndex").GetUInt32(),
                new MassMilligrams(value.GetProperty("massPerOutputUnitMilligrams").GetUInt64()))).ToArray();
        return new CraftRecipeDefinition(DefinitionId(metadata.GetProperty("definitionKey").GetString()!), physical, bindings, allocations);
    }

    private EntityId DefinitionId(string key)
    {
        EntityId identity = EntityId.DeriveGenerated("openfantasy.authored-content"u8,
            "production-farm.definitions.v1", key, "definition", 0);
        if (_world.IdentityIndex.Contains(identity) || !_newIdentities.TryAdd(identity, key))
        {
            throw new InvalidDataException("A production definition identity collides with existing world content or another new definition.");
        }
        return identity;
    }
}
