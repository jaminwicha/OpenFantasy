using OpenFantasy.Client;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Content.Trading;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Client.Retreat;

/// <summary>Rendered trading may cross a cutoff between capturing a showing and submitting its exact intent.</summary>
public sealed class RetreatTradingCaptureTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public async Task A_showing_keeps_its_captured_expiry_when_publication_follows_later(int elapsedCutoffs)
    {
        await using var world = new NativeMarketTestWorld();
        await world.EarnReward();
        await world.WaitForWrenAtHome();
        Assert.Equal(LocalTradeInquiryState.Offered, (await world.Ask(LocalTradeDirection.Buy, 4)).State);
        var draft = new FarmMarketDraft();
        draft.Begin(world.View, world.Stall.MarketId, LocalTradeDirection.Buy);
        Assert.True(draft.SetQuantity(4, world.StallView.MaximumUnitsPerOffer));
        Assert.True(draft.CaptureReview(world.View, world.StallView, Assert.Single(world.View.PaidQuotes)));
        Assert.Equal(WorldActionRejection.None, (await world.Submit(world.Offers(draft)[FarmSemanticAction.ConfirmTrade])).Rejection);
        draft.SetDirection(LocalTradeDirection.Sell);
        Assert.True(draft.SelectLot(world.StallView, Assert.Single(world.StallView.OwnLots).LotId));

        ulong capturedStep = world.State.AdvancingSteps;
        FarmActionOffer offer = world.Offers(draft)[FarmSemanticAction.ShowTradeLot];
        var captured = Assert.IsType<DiscloseMarketLotIntent>(offer.Intent);
        Assert.Equal(capturedStep + 1 + NativeTradingPolicy.ShowingWindowSteps, captured.ExpiresAtStep);
        for (int cutoff = 0; cutoff < elapsedCutoffs; cutoff++) await world.Tick();
        WorldSnapshot before = world.State;
        Assert.Equal(capturedStep + (ulong)elapsedCutoffs, before.AdvancingSteps);
        Assert.Equal(WorldActionRejection.None, (await world.Submit(offer)).Rejection);
        WorldSnapshot after = world.State;
        var disclosed = Assert.Single(after.Coordination!.Markets!.Disclosures);
        Assert.Equal(captured, offer.Intent);
        Assert.Equal(before.AdvancingSteps + 1, disclosed.DisclosedAtStep);
        Assert.Equal(captured.ExpiresAtStep, disclosed.ExpiresAtStep);
        Assert.Equal(NativeTradingPolicy.ShowingWindowSteps - (ulong)elapsedCutoffs,
            disclosed.ExpiresAtStep - disclosed.DisclosedAtStep);
        Assert.Equal(before.Inventory.ContentHash, after.Inventory.ContentHash);
        Assert.Equal(before.Progression!.ContentHash, after.Progression!.ContentHash);

        byte[] expected = WorldSnapshotCodec.Encode(after);
        await world.StopAsync();
        using var recovered = SqliteWorldAuthority.OpenExisting(world.SavePath, WorldSnapshotCodec.Encode(NativeMarketTestWorld.Genesis));
        Assert.Equal(expected, WorldSnapshotCodec.Encode(recovered.ReadStateForHost()));
    }
}
