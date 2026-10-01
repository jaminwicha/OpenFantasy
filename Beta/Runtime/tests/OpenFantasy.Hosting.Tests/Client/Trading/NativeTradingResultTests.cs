using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Content.Trading;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Client.Trading;

/// <summary>Authenticates native market journals and deltas; rendered reports alone are never proof of trade.</summary>
public sealed class NativeTradingResultTests
{
    [Fact]
    public void Opt_in_native_market_results_authenticate_exact_purchases_showings_and_sales()
    {
        string? manifestPath = Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_TRADING_RESULTS");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(manifestPath), "The actual native trading results manifest is required.");
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath!));
        JsonElement[] results = manifest.RootElement.GetProperty("results").EnumerateArray().ToArray();
        Assert.Equal(new[] { "controller", "keyboard", "pointer" },
            results.Select(result => Text(result, "mode")).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(results.Length, results.Select(result => Text(result, "save")).Distinct(StringComparer.Ordinal).Count());
        foreach (JsonElement result in results) Verify(result);
    }

    private static void Verify(JsonElement result)
    {
        string mode = Text(result, "mode");
        string save = AbsolutePath(result, "save");
        byte[] genesisBytes = File.ReadAllBytes(AbsolutePath(result, "genesis"));
        byte[] beforeBytes = File.ReadAllBytes(AbsolutePath(result, "before"));
        // The fixture genesis is exactly the production composition, not a test scenario.
        Assert.Equal(WorldSnapshotCodec.Encode(NativeMarketTestWorld.Genesis), genesisBytes);
        WorldSnapshot genesis = WorldSnapshotCodec.Decode(genesisBytes);
        WorldSnapshot fixture = WorldSnapshotCodec.Decode(beforeBytes);
        using var metadataDocument = JsonDocument.Parse(File.ReadAllBytes(AbsolutePath(result, "metadata")));
        JsonElement metadata = metadataDocument.RootElement;
        Assert.Equal("native-trading-fixture-v1", Text(metadata, "kind"));
        Assert.Equal(Text(metadata, "genesisHash"), genesis.ContentHash.Value);
        Assert.Equal(Text(metadata, "beforeHash"), fixture.ContentHash.Value);
        MarketBinding stall = NativeTradingWorld.FindStall(fixture)!;
        EntityId player = fixture.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        EntityId wren = stall.MerchantActorId;
        Assert.Equal(Text(metadata, "playerId"), player.ToString());
        Assert.Equal(Text(metadata, "marketId"), stall.MarketId.ToString());
        ulong stock = stall.CountUsableStock(fixture.Inventory, fixture.Production!.Lots);
        Assert.Equal(CareJourneyAdmission.InitialBerryQuantity, stock);
        Assert.Empty(fixture.Coordination!.PaidExchange!.Open);
        Assert.Empty(fixture.Coordination.Markets!.Disclosures);

        using var activeDocument = JsonDocument.Parse(File.ReadAllBytes(AbsolutePath(result, "report")));
        using var reopenDocument = JsonDocument.Parse(File.ReadAllBytes(AbsolutePath(result, "reopenReport")));
        JsonElement active = activeDocument.RootElement;
        JsonElement reopen = reopenDocument.RootElement;
        VerifyReport(active, mode);
        VerifyReport(reopen, "reopen");
        Assert.Equal(PublicHash(fixture, player), Text(active, "InitialHash"));
        Assert.Equal(Text(active, "FinalHash"), Text(reopen, "InitialHash"));
        Assert.Equal(Text(reopen, "InitialHash"), Text(reopen, "FinalHash"));
        EntityId buyId = EntityId.Parse(Text(active, "BuyQuoteId"));
        EntityId sellId = EntityId.Parse(Text(active, "SellQuoteId"));
        Assert.Equal(Text(active, "BuyQuoteId"), Text(reopen, "BuyQuoteId"));
        Assert.Equal(Text(active, "SellQuoteId"), Text(reopen, "SellQuoteId"));
        ulong buyUnits = active.GetProperty("BuyUnits").GetUInt64();
        ulong sellUnits = active.GetProperty("SellUnits").GetUInt64();
        Assert.Equal((NativeTradingFixtureTests.BuyUnits, NativeTradingFixtureTests.SellUnits), (buyUnits, sellUnits));

        StoredWorldCommit[] commits;
        StoredWorldCommandOutcome[] rows;
        using (var database = WorldDatabase.Open(save, create: false))
        {
            Assert.Equal(genesisBytes, database.ReadGenesis());
            commits = database.ReadJournal(fixture.Revision).ToArray();
            rows = database.ReadCommandOutcomes(fixture.Revision).ToArray();
        }
        // Wren's two answers and the player's acceptance, showing and second acceptance; requests leave no record.
        Assert.Equal(5, rows.Length);
        var commands = rows.OrderBy(row => row.Sequence).Select(row => (Row: row, Command: WorldActionCodec.Decode(row.Command))).ToArray();
        var sellOffer = Assert.IsType<IssueMarketQuoteIntent>(commands[0].Command.Intent);
        var buyAccept = Assert.IsType<AcceptSpotQuoteIntent>(commands[1].Command.Intent);
        var showing = Assert.IsType<DiscloseMarketLotIntent>(commands[2].Command.Intent);
        var buyOffer = Assert.IsType<IssueMarketQuoteIntent>(commands[3].Command.Intent);
        var sellAccept = Assert.IsType<AcceptSpotQuoteIntent>(commands[4].Command.Intent);
        Assert.Equal(new[] { wren, player, player, wren, player }, commands.Select(value => value.Command.ActorId).ToArray());

        byte[] closedBytes = File.ReadAllBytes(save);
        using (var recovered = SqliteWorldAuthority.OpenExisting(save, genesisBytes))
        {
            WorldSnapshot final = recovered.ReadStateForHost();
            Assert.Equal(beforeBytes, WorldSnapshotCodec.Encode(recovered.ReadRecordedStateForHost(fixture.Revision).State));
            Assert.Equal(commits[^1].AfterHash, final.ContentHash);
            foreach (var entry in commands) VerifyCommand(recovered, commits, entry.Row, entry.Command);
            WorldSnapshot State(ulong sequence) => recovered.ReadRecordedStateForHost(sequence).State;

            // Wren's sale offer: her own lot at the rule's ask for her actual stock, with the authored window.
            SpotQuote sold = sellOffer.Quote;
            Assert.Equal(buyId, sold.QuoteId);
            Assert.Equal((stall.MarketId, SpotQuoteSide.IssuerSells, player, buyUnits), (sellOffer.MarketId, sold.Side, sold.CounterpartyActorId, sold.QuantityAllowance));
            Assert.Equal(stall.Policy.Ask(stock, buyUnits), sold.UnitPrice);
            Assert.Empty(sold.Fees);
            Assert.Equal(NativeTradingPolicy.OfferWindowSteps, sold.ExpiresAtStep - sold.IssuedAtStep);
            Assert.Equal(stall.CustodianId, State(commands[0].Row.Sequence - 1).Inventory.Custody.GetLot(sold.LotId).CustodianId);
            // The player's acceptance restates those exact terms and moves goods and earned money exactly.
            VerifyFill(State(commands[1].Row.Sequence - 1), State(commands[1].Row.Sequence), sold, buyAccept, commands[1].Command, buyUnits,
                buyer: player, seller: wren, stall.Commodity);

            // The showing pins the bought lot exactly and is paid into the player's own account.
            WorldSnapshot showBefore = State(commands[2].Row.Sequence - 1);
            CountedSourceLot shownLot = showBefore.Inventory.Custody.GetLot(showing.LotId);
            Assert.Equal((showBefore.GetResident(player).InventoryCustodianId, shownLot.Revision, sellUnits, buyUnits),
                (shownLot.CustodianId, showing.ExpectedLotRevision, showing.Quantity, shownLot.Quantity));
            Assert.Equal(showBefore.Progression!.Currency.GetActorAccount(player).AccountId, showing.SellerAccountId);
            Assert.Equal(showBefore.AdvancingSteps + 1 + NativeTradingPolicy.ShowingWindowSteps, showing.ExpiresAtStep);
            Assert.Equal(showBefore.Inventory.ContentHash, State(commands[2].Row.Sequence).Inventory.ContentHash);
            Assert.Equal(showBefore.Progression.ContentHash, State(commands[2].Row.Sequence).Progression!.ContentHash);

            // Wren's buy offer names only the shown lot, priced by the rule for her actual reduced stock.
            SpotQuote bought = buyOffer.Quote;
            Assert.Equal(sellId, bought.QuoteId);
            Assert.Equal((SpotQuoteSide.IssuerBuys, showing.LotId, sellUnits), (bought.Side, bought.LotId, bought.QuantityAllowance));
            Assert.Equal(stall.Policy.Bid(stock - buyUnits, sellUnits), bought.UnitPrice);
            Assert.True(bought.ExpiresAtStep <= showing.ExpiresAtStep);
            VerifyFill(State(commands[4].Row.Sequence - 1), State(commands[4].Row.Sequence), bought, sellAccept, commands[4].Command, sellUnits,
                buyer: wren, seller: player, stall.Commodity);

            ulong spent = sold.PaymentFor(buyUnits), earned = bought.PaymentFor(sellUnits);
            CurrencyLedgerBalances(fixture, final, player, wren, spent, earned);
            Assert.Equal(stock - buyUnits + sellUnits, stall.CountUsableStock(final.Inventory, final.Production!.Lots));
            Assert.Empty(final.Coordination!.PaidExchange!.Open);
            Assert.Equal(new[] { buyId, sellId }.Order(), final.Coordination.PaidExchange.Outcomes.Select(value => value.Quote.Quote.QuoteId).Order());
            Assert.All(final.Coordination.PaidExchange.Outcomes, value => Assert.Equal(WorldPaidQuoteOutcomeKind.Filled, value.Kind));
            // The open stall suppressed walking, and every settling step reserved both parties.
            Assert.Equal(fixture.Space.GetActor(player).Feet, final.Space.GetActor(player).Feet);
            Assert.Equal(fixture.Space.GetActor(wren).Feet, final.Space.GetActor(wren).Feet);
            Assert.Equal(PublicHash(final, player), Text(active, "FinalHash"));
        }
        Assert.Equal(closedBytes, File.ReadAllBytes(save));
    }

    private static void VerifyFill(WorldSnapshot before, WorldSnapshot after, SpotQuote quote, AcceptSpotQuoteIntent accept,
        WorldActionCommand command, ulong units, EntityId buyer, EntityId seller, CountedItemDefinition commodity)
    {
        Assert.Equal((quote.QuoteId, quote.ContentHash, units), (accept.Acceptance.QuoteId, accept.Acceptance.QuoteHash, accept.Acceptance.Quantity));
        Assert.Equal((quote.PaymentFor(units), quote.FeesFor(units)), (accept.Acceptance.Payment, accept.Acceptance.Fees));
        Assert.Equal(command.CommandId, accept.Acceptance.AcceptanceId);
        Assert.Equal(before.Space.GetActor(quote.IssuerActorId).Revision, accept.ExpectedIssuerRevision);
        var currency = before.Progression!.Currency;
        Assert.Equal(currency.GetActorAccount(buyer).Balance - quote.PaymentFor(units),
            after.Progression!.Currency.GetActorAccount(buyer).Balance);
        Assert.Equal(currency.GetActorAccount(seller).Balance + quote.PaymentFor(units),
            after.Progression.Currency.GetActorAccount(seller).Balance);
        Assert.Equal(Units(before, buyer, commodity) + units, Units(after, buyer, commodity));
        Assert.Equal(Units(before, seller, commodity) - units, Units(after, seller, commodity));
        WorldPaidQuoteOutcome outcome = after.Coordination!.PaidExchange!.Outcomes.Single(value => value.Quote.Quote.QuoteId == quote.QuoteId);
        Assert.Equal(WorldPaidQuoteOutcomeKind.Filled, outcome.Kind);
        Assert.Equal(command.CommandId, outcome.Settlement!.Acceptance.AcceptanceId);
    }

    private static void CurrencyLedgerBalances(WorldSnapshot fixture, WorldSnapshot final, EntityId player, EntityId wren,
        ulong spent, ulong earned)
    {
        var before = fixture.Progression!.Currency;
        var after = final.Progression!.Currency;
        Assert.Equal(before.AdmittedSupply, after.AdmittedSupply);
        Assert.Equal(before.GetActorAccount(player).Balance - spent + earned, after.GetActorAccount(player).Balance);
        Assert.Equal(before.GetActorAccount(wren).Balance + spent - earned, after.GetActorAccount(wren).Balance);
        Assert.Equal(before.Accounts.Where(value => value.ActorId != player && value.ActorId != wren).Select(value => value.Balance),
            after.Accounts.Where(value => value.ActorId != player && value.ActorId != wren).Select(value => value.Balance));
    }

    private static ulong Units(WorldSnapshot state, EntityId actor, CountedItemDefinition commodity)
    {
        EntityId bag = state.GetResident(actor).InventoryCustodianId;
        return state.Inventory.Custody.Lots.Where(lot => lot.CustodianId == bag && lot.Definition == commodity)
            .Aggregate(0UL, (sum, lot) => sum + lot.Quantity);
    }

    private static void VerifyCommand(SqliteWorldAuthority recovered, StoredWorldCommit[] commits,
        StoredWorldCommandOutcome row, WorldActionCommand command)
    {
        WorldActionReceipt receipt = WorldReceiptCodecV1.Decode(row.Receipt);
        Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
        Assert.Equal((command.CommandId, command.ContentHash, row.Sequence), (receipt.CommandId, receipt.CommandHash, receipt.CommitSequence));
        Assert.Equal(command.PrincipalId, row.PrincipalId);
        StoredWorldCommit committed = commits.Single(value => value.Sequence == row.Sequence);
        WorldStepBatch batch = WorldStepCodec.Decode(committed.AcceptedBatch);
        Assert.Equal(WorldBatchKind.AdvancingStep, batch.Kind);
        Assert.Equal(WorldActionCodec.Encode(command), WorldActionCodec.Encode(Assert.Single(batch.Actions)));
        WorldSnapshot before = recovered.ReadRecordedStateForHost(row.Sequence - 1).State;
        WorldSnapshot after = recovered.ReadRecordedStateForHost(row.Sequence).State;
        Assert.Equal((committed.BeforeHash, committed.AfterHash), (before.ContentHash, after.ContentHash));
        Assert.Equal(before.AdvancingSteps + 1, after.AdvancingSteps);
        var control = before.Inventory.Controls.Single(value => value.ActorId == command.ActorId);
        Assert.Equal((control.PrincipalId, control.SessionId), (command.PrincipalId, command.OriginSessionId));
        Assert.Equal(before.Space.GetActor(command.ActorId).Revision, command.ExpectedActorRevision);
        Assert.Equal(row.Receipt, WorldReceiptCodecV1.Encode(Assert.IsType<WorldActionReceipt>(recovered.FindReceipt(command,
            new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId)))));
    }

    private static void VerifyReport(JsonElement report, string mode)
    {
        Assert.Equal("native-trading-v1", Text(report, "Kind"));
        Assert.Equal("passed", Text(report, "Status"));
        Assert.Equal(mode, Text(report, "Mode"));
        Assert.Equal(0, report.GetProperty("FocusRequests").GetInt32());
        Assert.True(report.GetProperty("IsLocallyPaused").GetBoolean());
        Assert.InRange(report.GetProperty("DurationMilliseconds").GetInt64(), 0, 149_999);
        Assert.NotEqual("headless", Text(report, "Display"));
    }

    private static string AbsolutePath(JsonElement value, string name)
    {
        string path = Text(value, name);
        Assert.True(Path.IsPathFullyQualified(path), $"Native evidence requires an absolute {name} path.");
        Assert.True(File.Exists(path), $"Native evidence file is absent: {path}");
        return path;
    }

    private static string Text(JsonElement value, string name) => value.GetProperty(name).GetString()
        ?? throw new InvalidDataException($"Native trading verification requires {name}.");

    private static string PublicHash(WorldSnapshot state, EntityId player) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new LocalWorldView(state, player)))));
}
