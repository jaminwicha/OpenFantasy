using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Production;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Tests.Content;

namespace OpenFantasy.Hosting.Tests.Production;

/// <summary>
/// Finite source-backed item rights with explicitly authored physical calibration. The retained
/// Potion payload supplies item meaning and the recipe supplies counts; neither supplies mass.
/// This component fixture creates no world, health effect, treatment permission or saved game.
/// </summary>
internal sealed class RestorativeSupplyFixture
{
    internal RestorativeSupplyFixture()
    {
        string root = FarmContentFixture.InstalledContentRoot();
        byte[] records = File.ReadAllBytes(Path.Combine(root, "legacy/aldermere-first-loop/records.json"));
        Assert.Equal("9bfc3372954475402ea2fab30dab9de29b2a7c800f2fb61ab654d2efc81e973b", ContentHash.Compute(records).Value);
        using var source = JsonDocument.Parse(records);
        JsonElement Item(string key) => source.RootElement.GetProperty("records").EnumerateArray()
            .Single(value => value.GetProperty("targetKey").GetString() == "openfantasy.aldermere:items/" + key);
        JsonElement potion = Item("potion"), berries = Item("berries");
        PotionPayload = potion.GetProperty("sourcePayload").Clone();
        Potion = Pin(Id(100), potion);
        Berries = Pin(Id(101), berries);
        byte[] recipes = File.ReadAllBytes(Path.Combine(root, "legacy/aldermere-first-loop/source-support/data/recipes.json"));
        Assert.Equal("0c81bcf33088668a7837e77cf01640cbfd2070e2a8caf9fa13ebdeb1e032a6b1", ContentHash.Compute(recipes).Value);
        using var definitions = JsonDocument.Parse(recipes);
        RecipePayload = definitions.RootElement.GetProperty("recipes").EnumerateArray()
            .Single(value => value.GetProperty("id").GetString() == "craft_potion").Clone();
        RecipePin = FarmCanonicalJson.Hash(RecipePayload);
        JsonElement input = Assert.Single(RecipePayload.GetProperty("inputs").EnumerateArray());
        Assert.Equal("berries", input.GetProperty("item").GetString());
        Assert.Equal("potion", RecipePayload.GetProperty("output").GetString());
        ulong inputCount = input.GetProperty("count").GetUInt64();
        ulong outputCount = RecipePayload.GetProperty("outputCount").GetUInt64();
        var package = new PackageId("openfantasy.restorative-supply-tests");
        var berryMaterial = new MaterialDefinition(package, Id(110), "calibrated_berries", new(20_000), new(30_000), new(1));
        var potionMaterial = new MaterialDefinition(package, Id(111), "calibrated_potion", new(60_000), new(90_000), new(0));
        var physical = new MaterialRecipeDefinition(package, Id(120), "craft_potion", new(1), RecipePin,
            new MaterialCatalog([berryMaterial, potionMaterial]),
            [new(RecipeMaterialRole.Input, berryMaterial.Pin(), inputCount),
                new(RecipeMaterialRole.Output, potionMaterial.Pin(), outputCount)]);
        Recipe = new CraftRecipeDefinition(Id(121), physical,
            [new(Berries, berryMaterial), new(Potion, potionMaterial)], [new(Berries, 0, new(60_000))]);
        var custody = new CustodySnapshot(Id(1), Id(2), 0,
            [new(Storage, 16, 0), new(RecipientStorage, 16, 0)],
            [new(RawPotionLot, Storage, Potion, new SourceInterval(PotionSource, 0, 2), 0),
                new(BerryLot, Storage, Berries, new SourceInterval(BerrySource, 0, 6), 0)]);
        Inventory = new CountedAuthorityState(custody, 8, [Actor, Recipient],
            [new(Delivery.PrincipalId, Delivery.SessionId, Actor), new(Id(22), Id(32), Recipient)],
            [new(Storage, Actor, CustodyDepositRule.OwnerOnly), new(RecipientStorage, Recipient, CustodyDepositRule.OwnerOnly)],
            [new(PotionSource, Potion, 2), new(BerrySource, Berries, 6)]);
        Production = new ProductionSnapshot(Id(1), Id(2), 0, [Recipe], [], [], []);
        Production.ValidateForAuthority(Inventory);
    }

    internal static EntityId Id(ulong value) => new(0x525354535550504c, value);
    internal static EntityId Actor => Id(10);
    internal static EntityId Recipient => Id(11);
    internal static EntityId Storage => Id(40);
    internal static EntityId RecipientStorage => Id(41);
    internal static EntityId RawPotionLot => Id(50);
    internal static EntityId BerryLot => Id(51);
    internal static EntityId PotionSource => Id(60);
    internal static EntityId BerrySource => Id(61);
    internal static TrustedCommandDelivery Delivery => new(Id(20), Id(30));
    internal CountedAuthorityState Inventory { get; }
    internal ProductionSnapshot Production { get; }
    internal CountedItemDefinition Potion { get; }
    internal CountedItemDefinition Berries { get; }
    internal CraftRecipeDefinition Recipe { get; }
    internal JsonElement PotionPayload { get; }
    internal JsonElement RecipePayload { get; }
    internal ContentHash RecipePin { get; }

    internal ConsumeItemRequest Request(CountedAuthorityState inventory, ProductionSnapshot production,
        IngredientRequest? ingredient = null, CountedItemDefinition? definition = null) => new(Storage,
        inventory.Custody.GetStorage(Storage).Revision, production.Revision, Recipient, definition ?? Potion,
        ingredient ?? new(IngredientLotKind.Source, RawPotionLot, inventory.Custody.GetLot(RawPotionLot).Revision, 1));

    internal CraftPlan Craft(CountedAuthorityState inventory, ProductionSnapshot production, EntityId command) =>
        new InstantCraftPlanner().Plan(inventory, production, Delivery, Actor, command,
            new(Recipe.DefinitionId, Recipe.Version, production.Revision, Storage,
                inventory.Custody.GetStorage(Storage).Revision,
                [new(IngredientLotKind.Source, BerryLot, inventory.Custody.GetLot(BerryLot).Revision, 3)]));

    internal UInt128 AccountedMass(CountedAuthorityState inventory, ProductionSnapshot production)
    {
        UInt128 total = 0;
        foreach (var lot in inventory.Custody.Lots)
            total += (UInt128)lot.Quantity * Recipe.BindingFor(lot.Definition).Physical.MassPerUnit.Value;
        foreach (var lot in production.Lots)
            total += (UInt128)lot.Quantity * Recipe.BindingFor(lot.Definition).Physical.MassPerUnit.Value;
        foreach (var claim in production.ConsumedItems)
            total += (UInt128)claim.Ingredient.Quantity * Recipe.BindingFor(claim.Ingredient.Definition).Physical.MassPerUnit.Value;
        // Retired recipe inputs are ancestral backing, not a second live/consumed material amount.
        return total;
    }

    internal static CountedAuthorityState WithLots(CountedAuthorityState inventory, IEnumerable<CountedSourceLot> lots) =>
        new(new CustodySnapshot(inventory.WorldId, inventory.BranchId, inventory.Custody.Revision,
            inventory.Custody.Storages, lots), inventory.AdmittedUnitCapacity, inventory.Actors, inventory.Controls,
            inventory.Policies, inventory.Sources, inventory.ReservationIds);

    private static CountedItemDefinition Pin(EntityId id, JsonElement record)
    {
        ContentHash payload = FarmCanonicalJson.Hash(record.GetProperty("sourcePayload"));
        Assert.Equal(record.GetProperty("sourceIdentity").GetProperty("payloadSha256").GetString(), payload.Value);
        return new(id, payload);
    }
}
