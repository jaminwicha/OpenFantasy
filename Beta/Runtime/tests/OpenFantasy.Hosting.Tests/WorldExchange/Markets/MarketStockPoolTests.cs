using OpenFantasy.Core;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;

namespace OpenFantasy.Hosting.Tests.WorldExchange.Markets;

public sealed class MarketStockPoolTests
{
    [Fact]
    public void Fresh_admission_refuses_competing_rules_for_the_same_stock_pool_but_keeps_distinct_markets()
    {
        var fixture = new LocalMarketWorldFixture(LocalMarketWorldFixture.Gentle);
        WorldSnapshot paid = fixture.Paid.Genesis;
        MarketBinding low = MarketCircuitPolicies.BindResident(paid, fixture.NextId(), fixture.Wren,
            fixture.Berries, ConstantPrice());
        MarketBinding high = MarketCircuitPolicies.BindResident(paid, fixture.NextId(), fixture.Wren,
            fixture.Berries, LocalMarketWorldFixture.Steep(fixture.WrenGenesisStock));

        // Both policies cite the same source price. Their targets nevertheless permit a profitable
        // purchase and resale if both are admitted over the very same merchant-owned stock pool.
        ulong stock = fixture.WrenGenesisStock;
        Assert.True(stock > 0);
        Assert.Equal(low.Policy.Source, high.Policy.Source);
        ulong purchase = Assert.IsType<ulong>(low.Policy.Ask(stock, 1));
        ulong resale = Assert.IsType<ulong>(high.Policy.Bid(stock - 1, 1));
        Assert.True(resale > purchase);
        Assert.Equal(low.CustodianId, high.CustodianId);
        Assert.Equal(low.Commodity, high.Commodity);
        var before = paid.ContentHash;
        Assert.Throws<ArgumentException>(() => WorldMarketAdmission.CreateFresh(paid, [low, high]));
        Assert.Equal(before, paid.ContentHash);

        // Distinct actual stores may price the same commodity differently: trading between them
        // changes the stock distribution and remains a lawful part of the wider economy.
        WorldSnapshot admitted = WorldMarketAdmission.CreateFresh(paid, [low, fixture.TraderMarket]);
        Assert.NotEqual(low.CustodianId, fixture.TraderMarket.CustodianId);
        using var files = new StorageFiles();
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, admitted))
        {
            Assert.Equal(admitted.ContentHash, authority.ReadStateForHost().ContentHash);
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(admitted));
        Assert.Equal(admitted.ContentHash, reopened.ReadStateForHost().ContentHash);
    }

    private static MarketPricePolicy ConstantPrice() => new(MarketCircuitPolicies.Berries,
        stockingTarget: 8, holdingLimit: 64, softening: 4, minimumUnitPrice: 5, maximumUnitPrice: 5,
        askMarkup: 1, bidMarkdown: 1, unitFee: 0, feePayeeAccountId: null, maximumLot: 4,
        quoteLifetimeSteps: 64);
}
