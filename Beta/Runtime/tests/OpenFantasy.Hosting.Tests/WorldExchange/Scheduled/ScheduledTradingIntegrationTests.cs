using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;

namespace OpenFantasy.Hosting.Tests.WorldExchange.Scheduled;

/// <summary>Contrasts across scheduled choices, intervening world publication and other admitted work.</summary>
public sealed class ScheduledTradingIntegrationTests
{
    [Fact]
    public async Task Changing_the_owned_lot_without_changing_total_stock_does_not_publish_two_showings_in_one_attempt()
    {
        using var world = new ScheduledTradingWorld();
        EntityId firstLot = world.Fixture.Gift(world.Authority, world.Wren, world.Trader, world.Fixture.Paid.OrdinaryLot, 1);
        world.WalkWrenToWork();
        world.WalkBesideWork(world.Trader);
        world.WalkBesideWork(world.Player);
        const ulong interval = 16;
        var market = world.Fixture.WrenMarket;
        var trading = new LocalTradingConfiguration([new LocalMerchantPreference(world.Wren)],
            [new LocalShopperPreference(world.Trader, market.MarketId, desiredUnits: 0, reconsiderAfterSteps: interval)]);
        var time = new ManualSessionTimeProvider();
        await using var session = world.Open(time, trading);
        session.SetReady(true);
        ulong start = world.State.Revision;
        await SessionFixture.Tick(time);
        MarketLotDisclosure shown = world.State.Coordination!.Markets!.FindDisclosure(world.Trader, market.MarketId)!;
        Assert.Equal(firstLot, shown.LotId);
        ulong holding = ScheduledTradingReconsiderationTests.LocalMarketUnits(world, world.Trader);
        ulong balance = world.Balance(world.Trader);

        // Two separately consented gifts change which actual lot the shopper owns while its aggregate desire
        // remains unchanged. The session's manual clock has not selected another cutoff in between them.
        world.Fixture.Gift(world.Authority, world.Trader, world.Player, firstLot, 1);
        EntityId replacement = world.Fixture.Gift(world.Authority, world.Wren, world.Trader, world.Fixture.Paid.OrdinaryLot, 1);
        Assert.NotEqual(firstLot, replacement);
        Assert.Equal((holding, balance), (ScheduledTradingReconsiderationTests.LocalMarketUnits(world, world.Trader), world.Balance(world.Trader)));
        Assert.True(world.State.AdvancingSteps < shown.DisclosedAtStep + interval - 1);
        await SessionFixture.Tick(time);
        Assert.Single(Showings());
        Assert.Equal(firstLot, world.State.Coordination!.Markets!.FindDisclosure(world.Trader, market.MarketId)!.LotId);

        // The next bounded attempt can disclose the replacement. No refused or decided command is renamed.
        for (int tick = 0; Showings().Count == 1; tick++)
        {
            Assert.True(tick <= (int)interval, "The replacement may be shown at the next bounded reconsideration.");
            await SessionFixture.Tick(time);
        }
        var second = world.State.Coordination!.Markets!.FindDisclosure(world.Trader, market.MarketId)!;
        Assert.Equal((replacement, shown.DisclosedAtStep + interval), (second.LotId, second.DisclosedAtStep));
        Assert.Equal(2, Showings().Count);
        Assert.NotEqual(Showings()[0].CommandId, Showings()[1].CommandId);

        List<WorldActionCommand> Showings() => world.RecordedBatches(start + 1, world.State.Revision)
            .SelectMany(batch => batch.Actions).Where(command => command.ActorId == world.Trader
                && command.Intent is DiscloseMarketLotIntent).ToList();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_external_reading_hold_is_presented_and_any_queued_paid_action_is_refused(bool queueBeforeHold)
    {
        using var world = new ScheduledTradingWorld();
        world.WalkWrenToWork();
        world.WalkBesideWork(world.Player);
        var time = new ManualSessionTimeProvider();
        await using var session = world.Open(time, ScheduledTradingWorld.Merchants(world.Wren));
        session.SetReady(true);
        var inquiry = await ScheduledTradingWorld.Ask(session, time,
            new LocalTradeInquiry(world.Fixture.WrenMarket.MarketId, world.Berries, LocalTradeDirection.Buy, 1));
        var quote = session.ReadView().PaidQuotes.Single(value => value.QuoteId == inquiry.QuoteId);
        WorldActionCommand accept = world.PlayerAcceptance(quote, 1);
        Task<WorldActionReceipt>? waiting = queueBeforeHold ? session.SubmitAction(accept) : null;
        if (waiting is not null) Assert.False(waiting.IsCompleted);

        // A second legitimate authority caller publishes after input admission but before the manual cutoff.
        var current = world.State;
        var speak = world.Speak(world.Wren);
        var decision = world.Authority.Commit(new WorldStepBatch(world.Fixture.NextId(), current.WorldId,
            current.BranchId, current.Revision, current.AdvancingSteps, WorldBatchKind.AdvancingStep,
            [], [speak], current.ProfileVersion));
        Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
        var held = world.State;
        Assert.True(held.HasReadingHold);
        await SessionFixture.Tick(time);
        if (waiting is not null)
        {
            await SessionFixture.Until(() => waiting.IsCompleted);
            await Assert.ThrowsAsync<AuthorityDispatchException>(() => waiting);
        }
        Assert.False(session.Completion.IsCompleted);
        Assert.Equal(held.ContentHash, world.State.ContentHash);
        Assert.Null(world.Authority.FindReceipt(accept, world.DeliveryOf(world.Player)));
        Assert.True(session.ReadView().HasReadingHold, "The caller must be shown the actual hold even though this cutoff publishes nothing.");
        var dialogue = Assert.IsType<LocalDialogueView>(session.ReadView().Dialogue);
        Assert.Equal(held.Dialogues.Single().DialogueId, dialogue.DialogueId);

        Assert.Equal(WorldActionRejection.None, (await ScheduledTradingWorld.Submit(session, time,
            world.PlayerCommand(new CloseDialogueIntent(dialogue.DialogueId), world.Fixture.NextId()))).Rejection);
        Assert.Equal(WorldActionRejection.None, (await ScheduledTradingWorld.Submit(session, time, accept)).Rejection);
        Assert.Equal(PaidQuoteStatus.Filled, session.ReadView().PaidQuotes.Single(value => value.QuoteId == quote.QuoteId).Status);
    }

    [Fact]
    public async Task The_market_session_preserves_ordinary_excavation_as_well_as_trading()
    {
        using var world = new ScheduledTradingWorld();
        world.Fixture.StepTraderAside(world.Authority);
        var site = world.State.Excavation!.Sites.Single(value => value.OwnerActorId == world.Player);
        var start = world.State.Space.GetActor(world.Player).Feet;
        int movementSteps = 0;
        foreach (var waypoint in new[] { new SubunitPosition(start.X, site.Stance.Y, start.Z), site.Stance })
        {
            while (world.State.Space.GetActor(world.Player).Feet != waypoint)
            {
                Assert.True(movementSteps++ < 512, "The authored approach consists of bounded, real ground steps.");
                var feet = world.State.Space.GetActor(world.Player).Feet;
                long dx = Math.Clamp(waypoint.X - feet.X, -32, 32);
                long dy = dx == 0 ? Math.Clamp(waypoint.Y - feet.Y, -32, 32) : 0;
                var destination = feet.Offset(dx, dy, 0);
                world.Fixture.Walk(world.Authority, world.Player, destination,
                    state => state.Space.GetActor(world.Player).Feet == destination);
            }
        }
        var time = new ManualSessionTimeProvider();
        await using var session = world.Open(time, ScheduledTradingWorld.Merchants(world.Wren));
        session.SetReady(true);
        for (int tick = 0; world.State.Gathering!.GetActorWork(world.Player).Effort < GatherRulesV1.EffortPerSwing; tick++)
        {
            Assert.True(tick < 128, "Excavation effort must come from advancing the actual world.");
            await SessionFixture.Tick(time);
        }
        var work = session.ReadView().Worksite!;
        var target = work.ExcavationTargets.Single(value => value.SiteId == site.SiteId);
        var tool = work.Tools.Single(value => value.DefinitionId == site.RequiredTool.DefinitionId);
        var before = world.State;
        var command = world.PlayerCommand(new ExcavateIntent(target.SiteId, target.SiteRevision,
            target.TerrainRevision, tool.LotId, tool.Revision, target.DestinationRevision), world.Fixture.NextId());
        var receipt = await ScheduledTradingWorld.Submit(session, time, command);
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
        var after = world.State;
        Assert.Equal(site.CompletedWork + 1, after.Excavation!.Sites.Single(value => value.SiteId == site.SiteId).CompletedWork);
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
        Assert.Equal(before.Coordination!.Markets!.ContentHash, after.Coordination!.Markets!.ContentHash);
        Assert.Equal(before.Coordination!.PaidExchange!.ContentHash, after.Coordination!.PaidExchange!.ContentHash);
        Assert.Null(after.PendingExcavation);
    }
}
