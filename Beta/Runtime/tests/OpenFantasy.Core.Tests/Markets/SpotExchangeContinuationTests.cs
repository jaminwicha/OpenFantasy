using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Production;
using static OpenFantasy.Core.Tests.Markets.SpotExchangeFixture;

namespace OpenFantasy.Core.Tests.Markets;

public sealed class SpotExchangeContinuationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Continued_goods_payment_crafting_and_cut_replay_keep_exact_owners(bool retainGiftCapability)
    {
        var market = new SpotExchangeFixture();
        if (retainGiftCapability)
            market.Production = market.Production.ContinueConsentedMaterial(market.Inventory);
        market.Production = market.Production.ContinueSpotExchange(market.Inventory);
        Assert.Equal(retainGiftCapability, market.Production.Continuation!.SupportsConsentedTransfer);
        var opening = market.Production;
        var actors = market.Progression.Actors.ToArray();
        SpotQuote raw = market.BerryOffer();
        market.Issue(raw, MerchantADelivery);
        var bought = market.Decide(Accept(raw, 3));
        market.Commit(bought);
        var rations = market.CraftRations();
        SpotQuote bid = market.RationBid(rations);
        market.Issue(bid, MerchantBDelivery);
        var sold = market.Decide(Accept(bid, 1, 951));
        market.Commit(sold);

        Assert.Equal((94UL, 62UL, 71UL, 3UL, Supply), (market.Balance(TraderAccount), market.Balance(AAccount),
            market.Balance(BAccount), market.Balance(GuildAccount), market.TotalBalance()));
        Assert.Equal(actors, market.Progression.Actors);
        Assert.Equal(new[] { MaterialOperationKind.SpotExchange, MaterialOperationKind.Craft, MaterialOperationKind.SpotExchange },
            market.Production.MaterialHistory!.Operations.Select(value => value.Kind));
        Assert.Equal(3UL, market.Production.Revision);
        Assert.Equal(rations.Output.BatchId, market.Production.Lots.Single(value => value.LotId == sold.Fill!.MovedLotId).Output.BatchId);
        Assert.Equal(StallB, market.Production.Lots.Single(value => value.LotId == sold.Fill!.MovedLotId).CustodianId);
        market.Production.ValidateForAuthority(market.Inventory);
        byte[] encoded = ProductionCodecV12.Encode(market.Production);
        var decoded = ProductionCodecV12.Decode(encoded, opening.Continuation!, [bought.Evidence!, sold.Evidence!]);
        decoded.ValidateForAuthority(market.Inventory);
        Assert.Equal(encoded, ProductionCodecV12.Encode(decoded));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV12.Decode(encoded, opening.Continuation!, []));
        Assert.Throws<ArgumentException>(() => ProductionCodecV10.Encode(market.Production));
        Assert.Throws<ArgumentException>(() => ProductionCodecV4.Encode(market.Production));
        Assert.Throws<ProductionFormatException>(() => ProductionCodecV10.Decode(encoded, opening.Continuation!, []));

        // A balanced but unrecorded raw move still fails ordered replay.
        var lots = market.Inventory.Custody.Lots.Select(lot => lot.LotId == StallBerries
            ? new CountedSourceLot(lot.LotId, Pack, lot.Definition, lot.Source, lot.Revision) : lot);
        var altered = WithCustody(market.Inventory, new CustodySnapshot(World, Branch, market.Inventory.Custody.Revision,
            market.Inventory.Custody.Storages, lots));
        Assert.Throws<ArgumentException>(() => decoded.ValidateForAuthority(altered));

        var closing = market.Production;
        market.History.UnionWith(closing.MaterialHistory.Operations.Select(value => value.CommandId));
        market.History.UnionWith(closing.MaterialHistory.Operations.OfType<MaterialSpotExchangeOperation>()
            .Select(value => value.Evidence.Quote.QuoteId));
        market.Production = closing.Continue(market.Inventory);
        Assert.True(market.Production.Continuation!.SupportsSpotExchange);
        Assert.Equal(retainGiftCapability, market.Production.Continuation.SupportsConsentedTransfer);
        Assert.Empty(market.Production.MaterialHistory!.Operations);
        Assert.Equal(closing.TotalMaterialOperationCount, market.Production.TotalMaterialOperationCount);
        Assert.Equal(closing.Lots, market.Production.Lots);
        var remaining = market.Production.Lots.Single(value => value.LotId == rations.LotId);
        var next = market.RationBid(remaining, 902, 1);
        market.Issue(next, MerchantBDelivery);
        var onward = market.Decide(Accept(next, 1, 952));
        market.Commit(onward);
        Assert.Equal((103UL, 62UL, 62UL, 3UL, Supply), (market.Balance(TraderAccount), market.Balance(AAccount),
            market.Balance(BAccount), market.Balance(GuildAccount), market.TotalBalance()));
        var reopened = ProductionCodecV12.Decode(ProductionCodecV12.Encode(market.Production),
            market.Production.Continuation!, [onward.Evidence!]);
        reopened.ValidateForAuthority(market.Inventory);
        Assert.Equal(4UL, reopened.TotalMaterialOperationCount);
        Assert.Equal(SpotExchangeOutcome.Duplicate, market.Decide(Accept(next, 1, 952)).Outcome);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Earlier_ordered_profiles_refuse_raw_and_produced_paid_edits_without_mutation(bool produced)
    {
        var market = new SpotExchangeFixture();
        var rations = market.CraftRations();
        market.Production = market.Production.ContinueConsentedMaterial(market.Inventory);
        byte[] before = ProductionCodecV10.Encode(market.Production);
        var quote = produced ? market.RationBid(rations) : market.BerryOffer();
        market.Issue(quote, produced ? MerchantBDelivery : MerchantADelivery);
        var refused = market.Decide(Accept(quote, 1));
        Assert.Equal(SpotExchangeRejection.UnsupportedHistory, refused.Rejection);
        Assert.Same(market.Inventory, refused.CandidateInventory);
        Assert.Same(market.Production, refused.CandidateProduction);
        Assert.Same(market.Progression, refused.CandidateProgression);
        Assert.Same(market.Book, refused.CandidateBook);
        Assert.Equal(before, ProductionCodecV10.Encode(market.Production));
        ProductionCodecV10.Decode(before, market.Production.Continuation!, []).ValidateForAuthority(market.Inventory);
    }
}
