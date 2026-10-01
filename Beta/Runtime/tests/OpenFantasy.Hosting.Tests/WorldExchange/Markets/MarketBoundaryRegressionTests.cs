using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.WorldExchange.Markets;

public sealed class MarketBoundaryRegressionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Changed_shown_stock_has_one_refusal_regardless_of_remaining_quantity(bool moveAll, bool bilateral)
    {
        var fixture = new LocalMarketWorldFixture(LocalMarketWorldFixture.Gentle);
        using var files = new StorageFiles();
        using SqliteWorldAuthority authority = fixture.Create(files);
        fixture.EarnReward(authority);
        EntityId lot = fixture.Gift(authority, fixture.Wren, fixture.Player, fixture.Paid.OrdinaryLot, 2);
        var service = new MarketTradingService(authority);
        WorldSnapshot state = authority.ReadStateForHost();
        TrustedCommandDelivery seller = LocalMarketWorldFixture.DeliveryOf(state, fixture.Player);
        TrustedCommandDelivery merchant = LocalMarketWorldFixture.DeliveryOf(state, fixture.Wren);
        WorldActionCommand show = service.PrepareDisclosure(seller, fixture.Player, fixture.WrenMarket.MarketId,
            lot, 2, 64, fixture.NextId())!;
        Assert.Equal(WorldActionRejection.None, service.Submit(show, seller, fixture.NextId()).Rejection);

        // The exact same showing genuinely permits issuance while its pins still describe the seller's stock.
        WorldActionCommand valid = BuyCommand(service, fixture, merchant, lot, 1, bilateral);
        Assert.Equal(WorldActionRejection.None, Submit(authority, fixture, valid, bilateral).Rejection);
        fixture.Gift(authority, fixture.Player, fixture.Trader, lot, moveAll ? 2UL : 1UL);

        // Preparation uses only the showing. Neither submission route may reveal whether one or zero units
        // remain by distinguishing a quantity failure from a revision failure. Passive world time may advance.
        foreach (ulong allowance in new[] { 1UL, 2UL })
        {
            state = authority.ReadStateForHost();
            WorldActionCommand stale = BuyCommand(service, fixture, merchant, lot, allowance, bilateral);
            Assert.Equal(WorldActionRejection.Unavailable, Submit(authority, fixture, stale, bilateral).Rejection);
            WorldSnapshot after = authority.ReadStateForHost();
            Assert.Equal(state.Inventory.ContentHash, after.Inventory.ContentHash);
            Assert.Equal(state.Production!.ContentHash, after.Production!.ContentHash);
            Assert.Equal(state.Progression!.ContentHash, after.Progression!.ContentHash);
            Assert.Equal(state.Coordination!.PaidExchange!.ContentHash, after.Coordination!.PaidExchange!.ContentHash);
            Assert.Equal(state.Coordination.Markets!.ContentHash, after.Coordination.Markets!.ContentHash);
            Assert.Equal(state.AdvancingSteps + 1, after.AdvancingSteps);
            Assert.Equal(state.Revision + 1, after.Revision);
        }
    }

    [Theory]
    [InlineData(SpotQuoteSide.IssuerSells)]
    [InlineData(SpotQuoteSide.IssuerBuys)]
    public void Fee_payee_counterparty_is_unavailable_during_preparation(SpotQuoteSide side)
    {
        var fixture = new LocalMarketWorldFixture(LocalMarketWorldFixture.ConstantWithDue);
        using var files = new StorageFiles();
        using SqliteWorldAuthority authority = fixture.Create(files);
        fixture.EarnReward(authority);
        var service = new MarketTradingService(authority);
        EntityId traderLot = fixture.Gift(authority, fixture.Wren, fixture.Trader, fixture.Paid.OrdinaryLot, 1);
        EntityId lawfulLot = fixture.Paid.OrdinaryLot;
        if (side == SpotQuoteSide.IssuerBuys)
        {
            lawfulLot = fixture.Gift(authority, fixture.Wren, fixture.Player, fixture.Paid.OrdinaryLot, 1);
            Show(fixture.Trader, traderLot);
            Show(fixture.Player, lawfulLot);
        }
        WorldSnapshot state = authority.ReadStateForHost();
        TrustedCommandDelivery merchant = LocalMarketWorldFixture.DeliveryOf(state, fixture.Wren);
        Assert.Equal(state.Progression!.Currency.GetActorAccount(fixture.Trader).AccountId,
            fixture.WrenMarket.Policy.FeePayeeAccountId);
        EntityId offeredLot = side == SpotQuoteSide.IssuerBuys ? traderLot : fixture.Paid.OrdinaryLot;
        Assert.Null(service.PrepareOffer(merchant, fixture.WrenMarket.MarketId, fixture.Trader, side,
            offeredLot, 1, 16, fixture.NextId()));
        WorldActionCommand lawful = service.PrepareOffer(merchant, fixture.WrenMarket.MarketId, fixture.Player, side,
            lawfulLot, 1, 16, fixture.NextId())!;
        Assert.NotNull(lawful);
        Assert.Equal(state.ContentHash, authority.ReadStateForHost().ContentHash);
        Assert.Equal(WorldActionRejection.None, service.Submit(lawful, merchant, fixture.NextId()).Rejection);

        void Show(EntityId actor, EntityId lot)
        {
            TrustedCommandDelivery delivery = LocalMarketWorldFixture.DeliveryOf(authority.ReadStateForHost(), actor);
            WorldActionCommand show = service.PrepareDisclosure(delivery, actor, fixture.WrenMarket.MarketId,
                lot, 1, 64, fixture.NextId())!;
            Assert.Equal(WorldActionRejection.None, service.Submit(show, delivery, fixture.NextId()).Rejection);
        }
    }

    private static WorldActionCommand BuyCommand(MarketTradingService service, LocalMarketWorldFixture fixture,
        TrustedCommandDelivery merchant, EntityId lot, ulong allowance, bool bilateral)
    {
        WorldActionCommand command = service.PrepareOffer(merchant, fixture.WrenMarket.MarketId, fixture.Player,
            SpotQuoteSide.IssuerBuys, lot, allowance, 16, fixture.NextId())!;
        Assert.NotNull(command);
        SpotQuote quote = Assert.IsType<IssueMarketQuoteIntent>(command.Intent).Quote;
        return bilateral ? new WorldActionCommand(command.CommandId, command.PrincipalId, command.OriginSessionId,
            command.ActorId, command.WorldId, command.BranchId, command.ExpectedActorRevision, new IssueSpotQuoteIntent(quote))
            : command;
    }

    private static WorldActionReceipt Submit(SqliteWorldAuthority authority, LocalMarketWorldFixture fixture,
        WorldActionCommand command, bool bilateral) => bilateral
            ? authority.SubmitPaidExchangeCommand(command, LocalMarketWorldFixture.Delivery(command), fixture.NextId())
            : authority.SubmitMarketCommand(command, LocalMarketWorldFixture.Delivery(command), fixture.NextId());
}
