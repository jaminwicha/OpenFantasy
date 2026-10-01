using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Content.Trading;

/// <summary>Production market-world composition, authored windows and explicit save selection.</summary>
public sealed class NativeMarketWorldTests
{
    [Fact]
    public void Composition_binds_wrens_own_admitted_berries_without_minting_stock_money_or_people()
    {
        WorldSnapshot ordinary = RestorativeJourneyAdmission.CreateEmbedded(NativeMarketTestWorld.SeedText, NativeMarketTestWorld.PlayerName);
        WorldSnapshot paid = WorldPaidExchangeAdmission.CreateFresh(ordinary);
        WorldSnapshot market = NativeTradingWorld.Compose(ordinary);

        Assert.Equal(WorldMarketAdmission.ProfileVersion, market.ProfileVersion);
        Assert.Equal((0UL, 0UL), (market.Revision, market.AdvancingSteps));
        Assert.Equal(WorldSnapshotCodec.Encode(market), WorldSnapshotCodec.Encode(NativeMarketTestWorld.Genesis));
        // Only the explicit market interpretation is added: every owner of goods, money and people is unchanged.
        Assert.Equal(paid.Inventory.ContentHash, market.Inventory.ContentHash);
        Assert.Equal(paid.Progression!.ContentHash, market.Progression!.ContentHash);
        Assert.Equal(paid.Residents.Select(value => value.ActorId), market.Residents.Select(value => value.ActorId));
        Assert.Equal(ordinary.Progression!.Currency.AdmittedSupply, market.Progression.Currency.AdmittedSupply);

        MarketBinding stall = Assert.Single(market.Coordination!.Markets!.Markets);
        Assert.Same(stall, NativeTradingWorld.FindStall(market));
        EntityId wren = market.Quests!.Definitions.Single().GiverActorId;
        Assert.Equal(wren, stall.MerchantActorId);
        Assert.Equal(market.GetResident(wren).InventoryCustodianId, stall.CustodianId);
        Assert.Equal(market.Progression.Currency.GetActorAccount(wren).AccountId, stall.AccountId);
        Assert.Equal(MarketCircuitPolicies.Berries, stall.Policy.Source);
        Assert.Equal(CareJourneyAdmission.InitialBerryQuantity, stall.CountUsableStock(market.Inventory, market.Production!.Lots));
        CountedSourceLot berries = Assert.Single(market.Inventory.Custody.Lots, lot => lot.Definition == stall.Commodity);
        Assert.Equal(stall.CustodianId, berries.CustodianId);
        Assert.Contains(market.Production.Recipes, recipe => recipe.Inputs.Any(input => recipe.ItemFor(input) == stall.Commodity));
        EntityId player = market.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        Assert.Equal(0UL, market.Progression.Currency.GetActorAccount(player).Balance);
        Assert.True(WorldMarketLocality.FindAvailable(market, player, stall.MarketId) is not null,
            "Wren's admitted place is within the player's in-person reach.");
        Assert.Empty(market.Coordination.PaidExchange!.Open);
        Assert.Empty(market.Coordination.Markets.Disclosures);
    }

    [Fact]
    public void Composition_refuses_lived_or_other_roots_and_recognizes_only_its_own_stall()
    {
        WorldSnapshot ordinary = RestorativeJourneyAdmission.CreateEmbedded(NativeMarketTestWorld.SeedText, NativeMarketTestWorld.PlayerName);
        Assert.Throws<ArgumentException>(() => NativeTradingWorld.Compose(NativeMarketTestWorld.Genesis));
        Assert.Throws<ArgumentException>(() => NativeTradingWorld.Compose(WorldPaidExchangeAdmission.CreateFresh(ordinary)));
        WorldSnapshot gifts = WorldMaterialProposalAdmission.CreateFresh(ordinary);
        Assert.Null(NativeTradingWorld.FindStall(gifts));
        Assert.Throws<InvalidDataException>(() => NativeTradingPolicy.Configuration(gifts));
        // Another seed composes its own world and stall identity; neither recognizes the other's stall.
        WorldSnapshot other = NativeTradingWorld.CreateEmbedded("another-native-market", NativeMarketTestWorld.PlayerName);
        Assert.NotEqual(NativeTradingWorld.FindStall(other)!.MarketId, NativeTradingWorld.FindStall(NativeMarketTestWorld.Genesis)!.MarketId);
    }

    [Fact]
    public void Native_windows_are_new_authored_configuration_and_earlier_defaults_stay_unchanged()
    {
        LocalTradingConfiguration configuration = NativeTradingPolicy.Configuration(NativeMarketTestWorld.Genesis);
        MarketBinding stall = NativeTradingWorld.FindStall(NativeMarketTestWorld.Genesis)!;
        LocalMerchantPreference wren = Assert.Single(configuration.Merchants);
        Assert.Equal(stall.MerchantActorId, wren.MerchantActorId);
        Assert.Equal(1_200UL, wren.OfferLifetimeSteps);
        Assert.Equal(600UL, configuration.InquiryLifetimeSteps);
        Assert.Empty(configuration.Shoppers);
        Assert.Equal(2_400UL, stall.Policy.QuoteLifetimeSteps);
        Assert.True(NativeTradingPolicy.OfferWindowSteps <= stall.Policy.QuoteLifetimeSteps);
        Assert.True(NativeTradingPolicy.ShowingWindowSteps >= NativeTradingPolicy.OfferWindowSteps,
            "A live showing outlasts an offer answered while it is live.");
        Assert.Equal(TimeSpan.FromMinutes(1), LocalWorldSession.StepInterval * (double)NativeTradingPolicy.OfferWindowSteps);
        // Issued terms of earlier worlds keep their authored rules and defaults.
        Assert.Equal(64UL, MarketCircuitPolicies.BerryStall().QuoteLifetimeSteps);
        Assert.Equal(32UL, new LocalMerchantPreference(stall.MerchantActorId).OfferLifetimeSteps);
        Assert.Equal(32UL, LocalTradingConfiguration.DefaultInquiryLifetimeSteps);
        Assert.Equal(32UL, LocalWorldSession.TradeInquiryLifetimeSteps);
        Assert.NotEqual(MarketCircuitPolicies.BerryStall().Version, stall.Policy.Version);
    }

    [Fact]
    public async Task Market_selection_creates_and_reopens_only_its_own_save_and_never_migrates_another_world()
    {
        using var files = new StorageFiles();
        string markets = files.Path;
        string gifts = Path.Combine(Path.GetDirectoryName(markets)!, "gifts.sqlite");
        await using (FarmClientSession created = await FarmClientSession.OpenMarketsAsync(markets,
            NativeMarketTestWorld.SeedText, NativeMarketTestWorld.PlayerName))
        {
            Assert.NotNull(created.World.ReadView().Markets);
        }
        byte[] saved = File.ReadAllBytes(markets);
        await using (FarmClientSession reopened = await FarmClientSession.OpenMarketsAsync(markets,
            NativeMarketTestWorld.SeedText, NativeMarketTestWorld.PlayerName))
        {
            Assert.Equal(NativeTradingWorld.FindStall(NativeMarketTestWorld.Genesis)!.MarketId,
                Assert.Single(reopened.World.ReadView().Markets!.Stalls).MarketId);
        }
        await using (FarmClientSession giftWorld = await FarmClientSession.OpenGiftsAsync(gifts,
            NativeMarketTestWorld.SeedText, NativeMarketTestWorld.PlayerName))
        {
            Assert.Null(giftWorld.World.ReadView().Markets);
        }
        byte[] giftBytes = File.ReadAllBytes(gifts);
        byte[] marketBytes = File.ReadAllBytes(markets);
        // Each explicit selection refuses the other's save instead of upgrading, resetting or relabeling it.
        await Assert.ThrowsAnyAsync<Exception>(() => FarmClientSession.OpenMarketsAsync(gifts,
            NativeMarketTestWorld.SeedText, NativeMarketTestWorld.PlayerName));
        await Assert.ThrowsAnyAsync<Exception>(() => FarmClientSession.OpenGiftsAsync(markets,
            NativeMarketTestWorld.SeedText, NativeMarketTestWorld.PlayerName));
        Assert.Equal(giftBytes, File.ReadAllBytes(gifts));
        Assert.Equal(marketBytes, File.ReadAllBytes(markets));
        Assert.NotEmpty(saved);
    }
}
