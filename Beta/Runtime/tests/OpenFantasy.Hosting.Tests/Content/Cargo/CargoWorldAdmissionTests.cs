using OpenFantasy.Core;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.Cargo;

namespace OpenFantasy.Hosting.Tests.Content.Cargo;

/// <summary>Fresh cargo-world admission: retained merchants, declared finite stock and purse, and the farm unchanged.</summary>
public sealed class CargoWorldAdmissionTests
{
    internal static readonly Lazy<WorldSnapshot> Genesis = new(() => CargoWorldAdmission.CreateEmbedded("cargo-world", "Traveler"));

    [Fact]
    public void The_fresh_world_admits_both_merchants_at_their_tiles_with_declared_stock_and_purse_only()
    {
        WorldSnapshot world = Genesis.Value;
        CargoCircuitSources sources = CargoCircuitSources.LoadEmbedded();
        CargoCircuitMarkets markets = CargoWorldAdmission.Find(world, sources)!;
        Assert.NotNull(markets);
        Assert.Equal(WorldMarketAdmission.ProfileVersion, world.ProfileVersion);
        Assert.Equal((0UL, 0UL), (world.Revision, world.AdvancingSteps));
        Assert.Equal(6, world.Residents.Count);
        Assert.Equal(15, world.Space.Regions.Count);
        EntityId marla = markets.Origin.MerchantActorId;
        EntityId kegga = markets.Destination.MerchantActorId;
        Assert.Equal(("Marla", "Kegga Toolwright"), (world.GetResident(marla).DisplayName, world.GetResident(kegga).DisplayName));
        Assert.Equal(new CellCoordinateFacts(96, 350), CellCoordinateFacts.Of(world.Space.GetActor(marla).Feet.Cell));
        Assert.Equal(new CellCoordinateFacts(347, 140), CellCoordinateFacts.Of(world.Space.GetActor(kegga).Feet.Cell));
        Assert.NotEqual(world.Space.GetActor(marla).HomePlaceId, world.Space.GetActor(kegga).HomePlaceId);

        CountedItemDefinition rations = markets.Origin.Commodity;
        Assert.Equal(rations, markets.Destination.Commodity);
        Assert.Equal(8UL, markets.Origin.CountUsableStock(world.Inventory, world.Production!.Lots));
        Assert.Equal(0UL, markets.Destination.CountUsableStock(world.Inventory, world.Production!.Lots));
        CountedSourceLot shelf = Assert.Single(world.Inventory.Custody.Lots, lot => lot.Definition == rations);
        Assert.Equal(world.GetResident(marla).InventoryCustodianId, shelf.CustodianId);
        Assert.Contains(world.Inventory.Sources, source => source.SourceId == shelf.Source.SourceId && source.UnitCapacity == 8);

        var currency = world.Progression!.Currency;
        EntityId player = world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        Assert.Equal((0UL, 0UL, 80UL), (currency.GetActorAccount(player).Balance, currency.GetActorAccount(marla).Balance,
            currency.GetActorAccount(kegga).Balance));
        WorldSnapshot farm = OpenFantasy.Hosting.Content.RestorativeJourneyAdmission.CreateEmbedded("cargo-world", "Traveler");
        Assert.Equal(farm.Progression!.Currency.AdmittedSupply + 80, currency.AdmittedSupply);
        // Wren's quest, crafting recipes and care meaning are carried unchanged.
        Assert.Equal((farm.Quests!.Definitions.Single().GiverActorId, farm.Quests.Definitions.Single().Reward.CurrencyAmount),
            (world.Quests!.Definitions.Single().GiverActorId, world.Quests.Definitions.Single().Reward.CurrencyAmount));
        Assert.Equal(farm.Production!.Recipes.Select(recipe => recipe.Version), world.Production.Recipes.Select(recipe => recipe.Version));
        Assert.Equal(farm.Care!.Remedies.Single().Version, world.Care!.Remedies.Single().Version);
    }

    private readonly record struct CellCoordinateFacts(long X, long Y)
    {
        internal static CellCoordinateFacts Of(OpenFantasy.Core.Terrain.CellCoordinate cell) => new(cell.X, cell.Y);
    }
}
