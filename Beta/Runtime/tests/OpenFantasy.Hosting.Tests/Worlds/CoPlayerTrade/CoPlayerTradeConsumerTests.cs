using OpenFantasy.Core;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.CoPlayerTrade;

public sealed class CoPlayerTradeConsumerTests
{
    [Fact]
    public void Actually_crafted_goods_pass_delegated_gift_and_paid_sale_then_archive_and_reopen_with_exact_output_rights()
    {
        var fixture = new CoPlayerTradeFixture();
        using var files = new StorageFiles();
        WorldSnapshot final;
        WorldSnapshot closing;
        WorldMaterialProposalOutcome gift;
        WorldPaidQuoteOutcome sale;
        ProductionOutputInterval rights;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            var beforeCraft = fixture.Prepare(authority, fund: true);
            var recipe = beforeCraft.Production!.Recipes.Single(value => value.PhysicalRecipe.Key == CareJourneyAdmission.RecipeKey);
            var input = Assert.Single(recipe.Inputs);
            var inputLot = beforeCraft.Inventory.Custody.Lots.Single(value =>
                value.CustodianId == beforeCraft.GetResident(fixture.Helper).InventoryCustodianId && value.Definition == recipe.ItemFor(input));
            var state = fixture.Control.Journey.CraftDose(authority);
            Assert.Equal(inputLot.Quantity - input.Count, state.Inventory.Custody.Lots.Where(value =>
                value.CustodianId == inputLot.CustodianId && value.Definition == inputLot.Definition)
                .Aggregate(0UL, (sum, value) => checked(sum + value.Quantity)));
            Assert.Equal(beforeCraft.Inventory.AdmittedUnitCapacity, state.Inventory.AdmittedUnitCapacity);
            var crafted = Assert.Single(state.Production!.Lots);
            rights = crafted.Output;
            var craftingRevision = state.Production.Revision;
            var batch = Assert.Single(state.Production.Batches);
            Assert.Equal(batch.BatchId, rights.BatchId);
            Assert.Equal(1UL, crafted.Quantity);
            state = fixture.Ready(authority);
            var (funding, fundingQuote) = fixture.Quote(state, fixture.Helper, fixture.Owner, fixture.Lot(state, fixture.Helper));
            fixture.Submit(authority, funding);
            fixture.Submit(authority, fixture.Accept(authority.ReadStateForHost(), fundingQuote));
            state = fixture.Ready(authority);
            var offer = fixture.Offer(state, fixture.Helper, fixture.Owner, crafted);
            fixture.Submit(authority, offer);
            state = authority.ReadStateForHost();
            fixture.Submit(authority, fixture.Accept(state, Assert.Single(state.Coordination!.Proposals!.Open)));
            state = authority.ReadStateForHost();
            gift = Assert.Single(state.Coordination!.Proposals!.Outcomes);
            Assert.NotNull(gift.Settlement!.ControlWitness);
            var received = Assert.Single(state.Production!.Lots);
            Assert.Equal(rights, received.Output);
            Assert.Equal(state.GetResident(fixture.Owner).InventoryCustodianId, received.CustodianId);
            Assert.Equal(craftingRevision, state.Production.Revision);
            state = fixture.Ready(authority);
            var (issue, quote) = fixture.ProducedQuote(state, received);
            fixture.Submit(authority, issue);
            fixture.Submit(authority, fixture.Accept(authority.ReadStateForHost(), quote));
            state = authority.ReadStateForHost();
            sale = state.Coordination!.PaidExchange!.FindOutcome(quote.QuoteId)!;
            Assert.NotNull(sale.Quote.ControlWitness);
            Assert.Equal(rights.BatchId, sale.Settlement!.RightsOwnerId);
            Assert.Equal(rights.Start, sale.Settlement.MovedStart);
            Assert.Equal(rights.End, sale.Settlement.MovedEnd);
            Assert.Equal(rights, Assert.Single(state.Production!.Lots).Output);
            Assert.Equal(state.GetResident(fixture.Helper).InventoryCustodianId, Assert.Single(state.Production.Lots).CustodianId);
            Assert.Equal(craftingRevision, state.Production.Revision);
            fixture.Control.Revoke(authority);
            var cut = fixture.Cut(authority);
            closing = cut.Closing;
            Assert.Equal(WorldSnapshotCodec.Encode(closing), Assert.IsType<WorldMaterialCareArchiveSegment>(cut.Publication.SealedMaterialCareSegment).Encode());
            final = authority.ReadStateForHost();
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.Equal(WorldSnapshotCodec.Encode(closing), reopened.ReadMaterialCareArchiveForHost(0).Segment.Encode());
        Assert.Equal(rights, Assert.Single(reopened.ReadStateForHost().Production!.Lots).Output);
        Assert.Equal(gift.Encode(), reopened.ReadRecordedProposalForController(gift.Proposal.ProposalId, fixture.Owner,
            fixture.Control.Delivery(fixture.Owner), (_, outcome) => Assert.IsType<WorldMaterialProposalOutcome>(outcome).Encode()));
        Assert.Equal(sale.Encode(), reopened.ReadRecordedPaidQuoteForController(sale.Quote.QuoteId, fixture.Owner,
            fixture.Control.Delivery(fixture.Owner), (_, outcome) => Assert.IsType<WorldPaidQuoteOutcome>(outcome).Encode()));
    }

    [Fact]
    public void Delegated_market_issuance_keeps_the_original_market_command_and_exact_rule_pricing_through_fill_archive_and_reopen()
    {
        var fixture = new CoPlayerTradeFixture(ownerMarket: true);
        using var files = new StorageFiles();
        var market = fixture.OwnerMarket!;
        WorldSnapshot final;
        WorldPaidQuoteOutcome filled;
        WorldActionCommand issued;
        using (var authority = SqliteWorldAuthority.CreateNew(files.Path, fixture.Genesis))
        {
            var state = fixture.Prepare(authority, fund: true);
            Assert.Equal(0UL, market.CountUsableStock(state.Inventory, state.Production!.Lots));
            var berries = state.Inventory.Custody.Lots.Single(value => value.CustodianId == state.GetResident(fixture.Helper).InventoryCustodianId
                && value.Definition == market.Commodity);
            var (funding, fundingQuote) = fixture.Quote(state, fixture.Helper, fixture.Owner, berries, price: 6);
            fixture.Submit(authority, funding);
            fixture.Submit(authority, fixture.Accept(authority.ReadStateForHost(), fundingQuote));
            state = fixture.Ready(authority);
            Assert.Equal(6UL, state.Progression!.Currency.GetActorAccount(fixture.Helper).Balance);
            Assert.Equal(1UL, market.CountUsableStock(state.Inventory, state.Production!.Lots));
            var stock = state.Inventory.Custody.Lots.Single(value => value.CustodianId == market.CustodianId && value.Definition == market.Commodity);
            var prepared = fixture.Quote(state, fixture.Owner, fixture.Helper, stock, market: market);
            issued = prepared.Command;
            Assert.IsType<IssueMarketQuoteIntent>(issued.Intent);
            Assert.Equal(6UL, prepared.Quote.UnitPrice);
            var receipt = fixture.Submit(authority, issued);
            Assert.Equal(issued.ContentHash, receipt.CommandHash);
            state = authority.ReadStateForHost();
            var record = state.Coordination!.Markets!.FindQuote(prepared.Quote.QuoteId)!;
            Assert.Equal(market.MarketId, record.MarketId);
            Assert.Equal(market.Policy.Version, record.PolicyVersion);
            Assert.Equal(1UL, record.StockBasis);
            Assert.Equal(market.Policy.Ask(record.StockBasis, 1), prepared.Quote.UnitPrice);
            fixture.Submit(authority, fixture.Accept(state, prepared.Quote));
            state = authority.ReadStateForHost();
            filled = state.Coordination!.PaidExchange!.FindOutcome(prepared.Quote.QuoteId)!;
            Assert.Equal(fixture.Control.Delivery(fixture.Grantee), filled.Quote.IssuerDelivery);
            Assert.NotNull(filled.Quote.ControlWitness);
            Assert.Equal(0UL, state.Progression!.Currency.GetActorAccount(fixture.Helper).Balance);
            Assert.Equal(0UL, market.CountUsableStock(state.Inventory, state.Production!.Lots));
            fixture.Control.Revoke(authority);
            var cut = fixture.Cut(authority);
            Assert.NotNull(cut.Publication.SealedMaterialCareSegment);
            final = authority.ReadStateForHost();
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(files.Path, WorldSnapshotCodec.Encode(fixture.Genesis));
        Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        Assert.NotNull(reopened.FindReceipt(issued, fixture.Control.Delivery(fixture.Grantee)));
        Assert.Equal(filled.Encode(), reopened.ReadRecordedPaidQuoteForController(filled.Quote.QuoteId, fixture.Owner,
            fixture.Control.Delivery(fixture.Owner), (published, outcome) =>
            {
                Assert.Equal(market.MarketId, published.Coordination!.Markets!.FindQuote(filled.Quote.QuoteId)!.MarketId);
                return Assert.IsType<WorldPaidQuoteOutcome>(outcome).Encode();
            }));
    }
}
