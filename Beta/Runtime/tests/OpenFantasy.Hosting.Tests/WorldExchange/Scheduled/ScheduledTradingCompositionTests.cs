using OpenFantasy.Core;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;

namespace OpenFantasy.Hosting.Tests.WorldExchange.Scheduled;

/// <summary>
/// Saved gifts and ordinary care beside configured trading in the profile-33 market world. The host binds the
/// actions Core already admits there, so arbitration between gift settlements, merchant answers and ordinary work
/// is exercised by controllers and inputs that can actually act in this world, not by ordering alone.
/// </summary>
public sealed class ScheduledTradingCompositionTests
{
    /// <summary>
    /// Wren's own saved offer and the player's trade inquiry meet at one cutoff. The player's valid acceptance spends
    /// the sender's opportunity, so Wren neither answers nor works in that step; the inquiry keeps its place and she
    /// answers it at the next free cutoff.
    /// </summary>
    [Fact]
    public async Task A_valid_gift_acceptance_reserves_the_sending_merchant_and_her_answer_follows()
    {
        using var world = new ScheduledTradingWorld();
        world.WalkWrenToWork();
        world.WalkBesideWork(world.Player);
        WorldMaterialProposal gift = world.Propose(world.Wren, world.Player, world.Fixture.Paid.OrdinaryLot, 1);
        MarketBinding market = world.Fixture.WrenMarket;
        var time = new ManualSessionTimeProvider();
        LocalWorldSession session = world.Open(time, ScheduledTradingWorld.Merchants(world.Wren));
        try
        {
            session.SetReady(true);
            await SessionFixture.Tick(time);
            // The market world exposes the player's saved offers and its creation form like the proposal world does.
            LocalMaterialProposalView proposals = session.ReadView().MaterialProposals!;
            Assert.NotNull(proposals.Preparation);
            MaterialProposalView incoming = Assert.Single(proposals.Offers);
            Assert.Equal((gift.ProposalId, MaterialProposalStatus.Open, false), (incoming.ProposalId, incoming.Status, incoming.ViewerIsSender));

            Assert.Equal(LocalTradeInquiryState.Waiting, (await session.SubmitTradeInquiryAsync(
                new LocalTradeInquiry(market.MarketId, world.Berries, LocalTradeDirection.Buy, 1))).State);
            ulong progress = world.Progress;
            WorldActionCommand accept = world.PlayerCommand(incoming.PrepareAcceptance(), world.Fixture.NextId());
            WorldActionReceipt received = await ScheduledTradingWorld.Submit(session, time, accept);
            Assert.Equal(WorldActionRejection.None, received.Rejection);
            WorldStepBatch settled = world.RecordedBatch(world.State.Revision);
            Assert.Equal(accept.CommandId, Assert.Single(settled.Actions).CommandId);
            Assert.Equal(progress, world.Progress);
            Assert.Equal(LocalTradeInquiryState.Waiting, session.ReadTradeInquiry()!.State);
            Assert.Equal(MaterialProposalStatus.Accepted, session.ReadView().MaterialProposals!.Offers.Single().Status);

            await SessionFixture.Tick(time);
            Assert.Equal(LocalTradeInquiryState.Offered, session.ReadTradeInquiry()!.State);
            Assert.Equal(progress, world.Progress);
            await SessionFixture.Tick(time);
            Assert.True(world.Progress > progress, "After the gift and the answer, the merchant works again.");
        }
        finally
        {
            session.SetReady(false);
            await session.StopAsync();
        }
    }

    /// <summary>
    /// Wren carries an explicit gift preference and a merchant preference in one market session. The player's saved
    /// offer of berries reaches her through an actual cutoff; at the next cutoff her own policy declines it, which
    /// spends her opportunity, so the player's waiting inquiry keeps its place and is answered one cutoff later.
    /// Declining grants nothing. The patient's own withdrawal of her assistance permission is also admitted here.
    /// </summary>
    [Fact]
    public async Task A_gift_policy_and_trading_compose_in_the_market_world_and_ordinary_care_is_bound()
    {
        using var world = new ScheduledTradingWorld();
        (SpotQuote _, EntityId bought) = world.BuyOneFromWrenDirectly();
        world.WalkWrenToWork();
        world.WalkBesideWork(world.Player);
        MarketBinding market = world.Fixture.WrenMarket;
        LocalMaterialGiftPolicy policy = ScheduledMaterialNpcTests.Policy(world.State, world.Wren, world.Player, offers: false);
        var time = new ManualSessionTimeProvider();
        await using var session = new LocalWorldSession(world.Authority, world.Player, world.DeliveryOf(world.Player), time,
            [policy], ScheduledTradingWorld.Merchants(world.Wren));
        session.SetReady(true);
        ProposeMaterialIntent offer = session.ReadView().MaterialProposals!.Preparation!.Prepare(
            OpenFantasy.Core.Authority.ConsentedMaterial.ConsentedMaterialLotKind.CountedSource, bought, world.Wren, 1)!;
        Assert.Equal(WorldActionRejection.None, (await ScheduledTradingWorld.Submit(session, time,
            world.PlayerCommand(offer, world.Fixture.NextId()))).Rejection);
        MaterialProposalView saved = Assert.Single(session.ReadView().MaterialProposals!.Offers);
        Assert.Equal((MaterialProposalStatus.Open, true), (saved.Status, saved.ViewerIsSender));

        Assert.Equal(LocalTradeInquiryState.Waiting, (await session.SubmitTradeInquiryAsync(
            new LocalTradeInquiry(market.MarketId, world.Berries, LocalTradeDirection.Buy, 1))).State);
        WorldSnapshot before = world.State;
        ulong progress = world.Progress;
        await SessionFixture.Tick(time);
        WorldStepBatch declined = world.RecordedBatch(world.State.Revision);
        Assert.IsType<DeclineMaterialProposalIntent>(Assert.Single(declined.Actions, value => value.ActorId == world.Wren).Intent);
        Assert.Equal(MaterialProposalStatus.Declined, Assert.Single(session.ReadView().MaterialProposals!.Offers).Status);
        Assert.Equal(LocalTradeInquiryState.Waiting, session.ReadTradeInquiry()!.State);
        Assert.Equal(progress, world.Progress);
        Assert.Equal((before.Inventory.ContentHash, before.Progression!.ContentHash),
            (world.State.Inventory.ContentHash, world.State.Progression!.ContentHash));
        await SessionFixture.Tick(time);
        Assert.Equal(LocalTradeInquiryState.Offered, session.ReadTradeInquiry()!.State);

        // Ordinary care is bound like in the proposal world: the patient may withdraw the helper's permission.
        LocalCarePermissionView permission = Assert.Single(session.ReadView().Restorative!.PermissionOffers);
        Assert.True(permission.IsActive && permission.CanRevoke);
        Assert.Equal(WorldActionRejection.None, (await ScheduledTradingWorld.Submit(session, time,
            world.PlayerCommand(permission.Intent!, world.Fixture.NextId()))).Rejection);
        Assert.False(Assert.Single(session.ReadView().Restorative!.PermissionOffers).IsActive);
        Assert.False(world.State.Care!.Ordinary!.Permissions.Single().IsActive);
    }
}
