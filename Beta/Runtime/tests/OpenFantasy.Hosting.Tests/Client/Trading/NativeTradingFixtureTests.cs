using System.Text.Json;
using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Content.Trading;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Client.Trading;

/// <summary>
/// Earns the native market fixture in the production world: Wren's ordinary quest pays its reward, she builds her
/// funded project through actual session cutoffs and walks home beside the player. The exported closed save
/// begins before any market action. The same managed continuation then proves the journey the native routes drive.
/// </summary>
public sealed class NativeTradingFixtureTests
{
    internal const ulong BuyUnits = 4;
    internal const ulong SellUnits = 1;

    [Fact]
    public async Task Earned_reward_and_wrens_finished_work_support_one_native_purchase_decline_and_sale()
    {
        await using var world = new NativeMarketTestWorld();
        await world.EarnReward();
        await world.WaitForWrenAtHome();
        world.Session.SetReady(false);
        await world.Tick();
        WorldSnapshot before = world.State;
        ulong reward = before.Quests!.Definitions.Single().Reward.CurrencyAmount;
        Assert.Equal(WorldMarketAdmission.ProfileVersion, before.ProfileVersion);
        Assert.Equal(reward, before.Progression!.Currency.GetActorAccount(world.Player).Balance);
        Assert.Equal(CareJourneyAdmission.InitialBerryQuantity, world.Stall.CountUsableStock(before.Inventory, before.Production!.Lots));
        Assert.Equal(0UL, world.Berries(world.Player));
        Assert.NotNull(WorldMarketLocality.FindAvailable(before, world.Player, world.Stall.MarketId));
        Assert.All(before.Construction!.Projects, project => Assert.True(project.IsCompleted));
        Assert.Empty(before.Coordination!.PaidExchange!.Open);
        Assert.Empty(before.Coordination.PaidExchange.Outcomes);
        Assert.Empty(before.Coordination.Markets!.Disclosures);
        Assert.False(before.HasReadingHold);
        Assert.Null(before.PendingExcavation);
        Assert.True(WorldActorAvailability.CanAct(before, world.Player));
        Assert.True(WorldActorAvailability.CanAct(before, world.Wren));
        Export(world, before, reward);

        // The same journey the native routes drive, independently at managed cutoffs from the exported state.
        ulong wrenPurse = world.Balance(world.Wren);
        world.Session.SetReady(true);
        Assert.Equal(LocalTradeInquiryState.Offered, (await world.Ask(LocalTradeDirection.Buy, BuyUnits)).State);
        PaidQuoteView buy = Assert.Single(world.View.PaidQuotes);
        Assert.Equal(world.Stall.Policy.Ask(CareJourneyAdmission.InitialBerryQuantity, BuyUnits), buy.UnitPrice);
        var draft = new FarmMarketDraft();
        draft.Begin(world.View, world.Stall.MarketId, LocalTradeDirection.Buy);
        Assert.True(draft.SetQuantity(BuyUnits, world.StallView.MaximumUnitsPerOffer));
        Assert.True(draft.CaptureReview(world.View, world.StallView, buy));
        Assert.Equal(WorldActionRejection.None, (await world.Submit(world.Offers(draft)[FarmSemanticAction.ConfirmTrade])).Rejection);
        Assert.Equal(LocalTradeInquiryState.Declined, (await world.Ask(LocalTradeDirection.Buy, BuyUnits)).State);

        draft.SetDirection(LocalTradeDirection.Sell);
        LocalMarketLotView bought = Assert.Single(world.StallView.OwnLots);
        Assert.Equal(BuyUnits, bought.Quantity);
        Assert.True(draft.SelectLot(world.StallView, bought.LotId));
        Assert.Equal(WorldActionRejection.None, (await world.Submit(world.Offers(draft)[FarmSemanticAction.ShowTradeLot])).Rejection);
        LocalMarketShowingView shown = world.StallView.OwnShowing!;
        Assert.Equal((bought.LotId, SellUnits, true, true), (shown.LotId, shown.Units, shown.IsLive, shown.MatchesOwnLot));
        Assert.Equal(NativeTradingPolicy.ShowingWindowSteps, shown.ExpiresAtStep - shown.ShownAtStep);
        Assert.Equal(LocalTradeInquiryState.Offered, (await world.Ask(LocalTradeDirection.Sell, SellUnits, bought.LotId)).State);
        PaidQuoteView sell = MarketActionOffers.OpenOffer(world.View, world.StallView)!;
        Assert.Equal(SpotQuoteSide.IssuerBuys, sell.Side);
        Assert.Equal(world.Stall.Policy.Bid(CareJourneyAdmission.InitialBerryQuantity - BuyUnits, SellUnits), sell.UnitPrice);
        Assert.True(sell.ExpiresAtStep <= shown.ExpiresAtStep);
        Assert.True(draft.CaptureReview(world.View, world.StallView, sell));
        Assert.Equal(WorldActionRejection.None, (await world.Submit(world.Offers(draft)[FarmSemanticAction.ConfirmTrade])).Rejection);
        Assert.Equal(reward - buy.TotalFor(BuyUnits) + sell.UnitPrice * SellUnits, world.Balance(world.Player));
        Assert.Equal(wrenPurse + buy.TotalFor(BuyUnits) - sell.UnitPrice * SellUnits, world.Balance(world.Wren));
        Assert.Equal((BuyUnits - SellUnits, CareJourneyAdmission.InitialBerryQuantity - BuyUnits + SellUnits),
            (world.Berries(world.Player), world.Berries(world.Wren)));
        Assert.Equal(before.Progression.Currency.AdmittedSupply, world.State.Progression!.Currency.AdmittedSupply);
    }

    private static void Export(NativeMarketTestWorld world, WorldSnapshot before, ulong reward)
    {
        if (Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_TRADING_FIXTURES") is not { Length: > 0 } requested) return;
        string root = Path.GetFullPath(requested);
        Assert.False(Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any(),
            "Native trading fixture export requires a fresh or empty directory; existing evidence is never overwritten.");
        Directory.CreateDirectory(root);
        string save = Path.Combine(root, "save.sqlite");
        string genesisPath = Path.Combine(root, "genesis.ofc");
        string beforePath = Path.Combine(root, "before.ofc");
        string metadataPath = Path.Combine(root, "metadata.json");
        world.Authority.BackupForHost(save);
        File.WriteAllBytes(genesisPath, WorldSnapshotCodec.Encode(NativeMarketTestWorld.Genesis));
        File.WriteAllBytes(beforePath, WorldSnapshotCodec.Encode(before));
        MarketBinding stall = world.Stall;
        var metadata = new
        {
            kind = "native-trading-fixture-v1", profileVersion = before.ProfileVersion,
            setup = "Fresh production native market world; talk, accept and deliver Wren's quest for its earned reward, then ordinary "
                + "session cutoffs let Wren build her funded project and walk home beside the player. Native begins before any market action.",
            worldId = before.WorldId.ToString(), branchId = before.BranchId.ToString(),
            genesisHash = NativeMarketTestWorld.Genesis.ContentHash.Value, beforeHash = before.ContentHash.Value,
            beforeRevision = before.Revision, beforeAdvancingSteps = before.AdvancingSteps,
            seedText = NativeMarketTestWorld.SeedText, playerName = NativeMarketTestWorld.PlayerName,
            playerId = world.Player.ToString(), wrenId = world.Wren.ToString(), marketId = stall.MarketId.ToString(),
            rewardCurrency = reward, playerBalance = before.Progression!.Currency.GetActorAccount(world.Player).Balance,
            wrenBalance = before.Progression.Currency.GetActorAccount(world.Wren).Balance,
            wrenBerries = stall.CountUsableStock(before.Inventory, before.Production!.Lots),
            offerWindowSteps = NativeTradingPolicy.OfferWindowSteps, showingWindowSteps = NativeTradingPolicy.ShowingWindowSteps,
            inquiryLifetimeSteps = NativeTradingPolicy.InquiryLifetimeSteps,
            expected = new
            {
                buyUnits = BuyUnits, buyUnitPrice = stall.Policy.Ask(CareJourneyAdmission.InitialBerryQuantity, BuyUnits),
                declinedBuyUnits = BuyUnits, sellUnits = SellUnits,
                sellUnitPrice = stall.Policy.Bid(CareJourneyAdmission.InitialBerryQuantity - BuyUnits, SellUnits)
            },
            limits = "Fresh production world only: no lived-save migration, legacy merchant import, archive cut, physical device, "
                + "multiplayer Wait or human acceptance claim. Requests are session memory and leave no journal record."
        };
        File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(new
        {
            kind = "native-trading-fixtures-v1", save, genesis = genesisPath, before = beforePath, metadata = metadataPath
        }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal(WorldSnapshotCodec.Encode(before),
            WorldSnapshotCodec.Encode(RecordedWorldReader.ReadForHost(save, before.Revision).State));
    }
}
