using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.NativeCargo;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Client.Cargo;

/// <summary>
/// Authenticates the native cargo route against its exported genesis and the actual journal: exact actors, markets,
/// quantities, prices, balances, the carried lot's source interval, the player's authority coordinates at each trade
/// and after reopening, and the ordinary movement samples between them. Rendered reports alone prove nothing. The
/// native save is read only through a disposable byte-identical copy.
/// </summary>
public sealed class NativeCargoResultTests
{
    [Fact]
    public void Opt_in_native_cargo_results_authenticate_the_exact_route_trades_and_reopening()
    {
        string? manifestPath = Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_CARGO_RESULTS");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(manifestPath), "The actual native cargo results manifest is required.");
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath!));
        JsonElement result = Assert.Single(manifest.RootElement.GetProperty("results").EnumerateArray().ToArray());
        Assert.Equal("keyboard", Text(result, "mode"));
        string copy = Path.Combine(Path.GetTempPath(), "openfantasy-native-cargo-result-" + Guid.NewGuid().ToString("N") + ".sqlite");
        string save = AbsolutePath(result, "save");
        byte[] closed = File.ReadAllBytes(save);
        Assert.Equal(Text(result, "finalDatabaseSha256"), Convert.ToHexString(SHA256.HashData(closed)).ToLowerInvariant());
        Assert.False(File.Exists(save + "-wal") || File.Exists(save + "-shm"), "The native save must be closed.");
        File.Copy(save, copy);
        try
        {
            Verify(result, copy);
        }
        finally
        {
            foreach (string path in new[] { copy, copy + "-wal", copy + "-shm" }) if (File.Exists(path)) File.Delete(path);
        }
        Assert.Equal(closed, File.ReadAllBytes(save));
        VerifyOnboarding(manifest.RootElement.GetProperty("onboarding"));
    }

    private static void Verify(JsonElement result, string save)
    {
        byte[] genesisBytes = File.ReadAllBytes(AbsolutePath(result, "genesis"));
        // The fixture genesis is exactly the production cargo composition of the fixture's seed.
        Assert.Equal(WorldSnapshotCodec.Encode(NativeCargoWorld.CreateEmbedded(NativeCargoFixtureTests.SeedText,
            NativeCargoFixtureTests.PlayerName)), genesisBytes);
        WorldSnapshot genesis = WorldSnapshotCodec.Decode(genesisBytes);
        byte[] beforeBytes = File.ReadAllBytes(AbsolutePath(result, "before"));
        WorldSnapshot fixture = WorldSnapshotCodec.Decode(beforeBytes);
        using var metadataDocument = JsonDocument.Parse(File.ReadAllBytes(AbsolutePath(result, "metadata")));
        JsonElement metadata = metadataDocument.RootElement;
        Assert.Equal("native-cargo-fixture-v1", Text(metadata, "Kind"));
        Assert.Equal((Text(metadata, "GenesisHash"), Text(metadata, "BeforeHash")), (genesis.ContentHash.Value, fixture.ContentHash.Value));
        CargoCircuitMarkets markets = CargoWorldAdmission.Find(fixture, NativeCargoWorld.Sources)!;
        MarketBinding marla = markets.Origin, kegga = markets.Destination;
        EntityId player = fixture.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        Assert.Equal((Text(metadata, "PlayerActorId"), Text(metadata, "MarlaMarketId"), Text(metadata, "KeggaMarketId")),
            (player.ToString(), marla.MarketId.ToString(), kegga.MarketId.ToString()));
        // The fixture's money was earned: the genesis player had none.
        Assert.Equal(0UL, genesis.Progression!.Currency.GetActorAccount(player).Balance);
        ulong earned = fixture.Progression!.Currency.GetActorAccount(player).Balance;
        Assert.True(earned > 0);
        Assert.Empty(fixture.Coordination!.PaidExchange!.Open);
        Assert.Empty(fixture.Coordination.PaidExchange.Outcomes);
        CargoCorridor corridor = NativeCargoWorld.Corridor(genesis);

        using var activeDocument = JsonDocument.Parse(File.ReadAllBytes(AbsolutePath(result, "report")));
        using var reopenDocument = JsonDocument.Parse(File.ReadAllBytes(AbsolutePath(result, "reopenReport")));
        JsonElement active = activeDocument.RootElement, reopen = reopenDocument.RootElement;
        VerifyReport(active, "keyboard");
        VerifyReport(reopen, "reopen");
        // A complete verified replay ran in the background history slot while frames kept rendering and the world held.
        JsonElement history = active.GetProperty("HistoryRead");
        Assert.True(history.GetProperty("Frames").GetInt32() >= 5 && history.GetProperty("LongestFrameSeconds").GetDouble() < 1.0);
        Assert.True(history.GetProperty("Revision").GetUInt64() > fixture.Revision);
        Assert.True(active.GetProperty("Rendered").GetArrayLength() >= 4 && reopen.GetProperty("Rendered").GetArrayLength() >= 1);
        Assert.Equal(PublicHash(fixture, player), Text(active, "InitialHash"));
        Assert.Equal(Text(active, "FinalHash"), Text(reopen, "InitialHash"));
        Assert.Equal(Text(reopen, "InitialHash"), Text(reopen, "FinalHash"));
        Assert.Equal((marla.MarketId.ToString(), kegga.MarketId.ToString()), (Text(active, "MarlaMarketId"), Text(active, "KeggaMarketId")));
        EntityId buyId = EntityId.Parse(Text(active, "BuyQuoteId"));
        EntityId sellId = EntityId.Parse(Text(active, "SellQuoteId"));
        EntityId carried = EntityId.Parse(Text(active, "CargoLotId"));
        Assert.Equal((Text(active, "BuyQuoteId"), Text(active, "SellQuoteId")), (Text(reopen, "BuyQuoteId"), Text(reopen, "SellQuoteId")));
        ulong units = active.GetProperty("TradeUnits").GetUInt64();
        Assert.Equal(NativeCargoFixtureTests.CargoRoutePlayProbeUnits, units);
        JsonElement[] walks = active.GetProperty("Walks").EnumerateArray().ToArray();
        Assert.Equal(new[] { "farm-to-brasshollow", "brasshollow-to-gearford", "gearford-to-brasshollow" },
            walks.Select(walk => Text(walk, "Label")).ToArray());
        Assert.Equal(13, walks[1].GetProperty("Transitions").GetInt32());
        Assert.Equal(13, walks[2].GetProperty("Transitions").GetInt32());

        StoredWorldCommit[] commits;
        StoredWorldCommandOutcome[] rows;
        using (var database = WorldDatabase.Open(save, create: false))
        {
            Assert.Equal(genesisBytes, database.ReadGenesis());
            commits = database.ReadJournal(fixture.Revision).ToArray();
            rows = database.ReadCommandOutcomes(fixture.Revision).ToArray();
        }
        var commands = rows.OrderBy(row => row.Sequence).Select(row => (Row: row, Command: WorldActionCodec.Decode(row.Command))).ToArray();
        // The player's only commands after the fixture: accept Marla's offer, show the carried lot, accept Kegga's bid.
        var playerCommands = commands.Where(value => value.Command.ActorId == player).ToArray();
        Assert.Equal(3, playerCommands.Length);
        var buyAccept = Assert.IsType<AcceptSpotQuoteIntent>(playerCommands[0].Command.Intent);
        var showing = Assert.IsType<DiscloseMarketLotIntent>(playerCommands[1].Command.Intent);
        var sellAccept = Assert.IsType<AcceptSpotQuoteIntent>(playerCommands[2].Command.Intent);
        // Each merchant answered once, under its own control, through its own market.
        var answers = commands.Where(value => value.Command.Intent is IssueMarketQuoteIntent).ToArray();
        Assert.Equal(new[] { marla.MerchantActorId, kegga.MerchantActorId }, answers.Select(value => value.Command.ActorId).ToArray());
        var marlaOffer = (IssueMarketQuoteIntent)answers[0].Command.Intent;
        var keggaOffer = (IssueMarketQuoteIntent)answers[1].Command.Intent;
        Assert.Equal((marla.MarketId, buyId, SpotQuoteSide.IssuerSells, units), (marlaOffer.MarketId, marlaOffer.Quote.QuoteId,
            marlaOffer.Quote.Side, marlaOffer.Quote.QuantityAllowance));
        Assert.Equal((kegga.MarketId, sellId, SpotQuoteSide.IssuerBuys, units, carried), (keggaOffer.MarketId, keggaOffer.Quote.QuoteId,
            keggaOffer.Quote.Side, keggaOffer.Quote.QuantityAllowance, keggaOffer.Quote.LotId));
        Assert.All(commands.Where(value => value.Command.ActorId != player && value.Command.Intent is not IssueMarketQuoteIntent),
            value => Assert.False(value.Command.Intent is PaidExchangeIntent or MarketIntent, "Only the player and the answering merchants trade."));
        // Every movement sample of the player after the fixture is one ordinary cardinal step under its own control.
        ActorControlBinding control = fixture.Inventory.Controls.Single(value => value.ActorId == player);
        WorldMovementInput[] steps = commits.SelectMany(commit => WorldStepCodec.Decode(commit.AcceptedBatch).Movement)
            .Where(input => input.ActorId == player).ToArray();
        Assert.All(steps, input => Assert.Equal((control.PrincipalId, control.SessionId), (input.Delivery.PrincipalId, input.Delivery.SessionId)));
        AssertCardinalSteps(steps);

        using var recovered = SqliteWorldAuthority.OpenExisting(save, genesisBytes);
        WorldSnapshot final = recovered.ReadStateForHost();
        // Each recorded root is one complete verified replay; read each needed commit once.
        Dictionary<ulong, WorldSnapshot> recorded = [];
        WorldSnapshot State(ulong sequence) => recorded.TryGetValue(sequence, out WorldSnapshot? known) ? known
            : recorded[sequence] = recovered.ReadRecordedStateForHost(sequence).State;
        Assert.Equal(beforeBytes, WorldSnapshotCodec.Encode(State(fixture.Revision)));
        Assert.Equal(commits[^1].AfterHash, final.ContentHash);
        Assert.Equal(PublicHash(final, player), Text(active, "FinalHash"));
        WorldSnapshot atBuy = State(playerCommands[0].Row.Sequence);
        WorldSnapshot atSell = State(playerCommands[2].Row.Sequence);
        // Each trade happened in person, at its own merchant, with the other market out of reach.
        Assert.NotNull(WorldMarketLocality.FindAvailable(State(playerCommands[0].Row.Sequence - 1), player, marla.MarketId));
        Assert.Null(WorldMarketLocality.FindAvailable(State(playerCommands[0].Row.Sequence - 1), player, kegga.MarketId));
        Assert.NotNull(WorldMarketLocality.FindAvailable(State(playerCommands[2].Row.Sequence - 1), player, kegga.MarketId));
        Assert.Null(WorldMarketLocality.FindAvailable(State(playerCommands[2].Row.Sequence - 1), player, marla.MarketId));
        int lastRegion = corridor.AllRegions.Count() - 1;
        Assert.Equal(lastRegion, corridor.RegionIndexOf(atSell.Space.GetActor(player).Feet.Cell));
        Assert.True(corridor.RegionIndexOf(atBuy.Space.GetActor(player).Feet.Cell) < 2);
        // Ordinary travel: enough recorded cardinal samples to cover farm → Brasshollow → Gearford → Brasshollow.
        long Distance(SubunitPosition from, SubunitPosition to) => Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y);
        SubunitPosition start = fixture.Space.GetActor(player).Feet, buyAt = atBuy.Space.GetActor(player).Feet,
            sellAt = atSell.Space.GetActor(player).Feet, end = final.Space.GetActor(player).Feet;
        long minimum = Distance(start, buyAt) + Distance(buyAt, sellAt) + Distance(sellAt, end);
        Assert.True(steps.LongLength * WorldMovementInput.DisplacementPerStep >= minimum,
            $"{steps.Length} samples cannot cover {minimum} subunits of travel.");
        Assert.True(atSell.AdvancingSteps - atBuy.AdvancingSteps >= (ulong)(Distance(buyAt, sellAt) / WorldMovementInput.DisplacementPerStep));
        JsonElement feet = active.GetProperty("FinalFeet");
        Assert.Equal(new SubunitPosition(feet.GetProperty("X").GetInt64(), feet.GetProperty("Y").GetInt64(), feet.GetProperty("Z").GetInt64()), end);
        Assert.NotNull(WorldMarketLocality.FindAvailable(final, player, marla.MarketId));

        // Exact terms, parties and balances.
        SpotQuote sold = marlaOffer.Quote, bought = keggaOffer.Quote;
        WorldSnapshot beforeBuy = State(playerCommands[0].Row.Sequence - 1);
        Assert.Equal(marla.Policy.Ask(marla.CountUsableStock(beforeBuy.Inventory, beforeBuy.Production!.Lots), units), sold.UnitPrice);
        WorldSnapshot beforeSell = State(playerCommands[2].Row.Sequence - 1);
        Assert.Equal(kegga.Policy.Bid(kegga.CountUsableStock(beforeSell.Inventory, beforeSell.Production!.Lots), units), bought.UnitPrice);
        Assert.Equal((buyId, sold.ContentHash, units, sold.PaymentFor(units)), (buyAccept.Acceptance.QuoteId, buyAccept.Acceptance.QuoteHash,
            buyAccept.Acceptance.Quantity, buyAccept.Acceptance.Payment));
        Assert.Equal((sellId, bought.ContentHash, units, bought.PaymentFor(units)), (sellAccept.Acceptance.QuoteId,
            sellAccept.Acceptance.QuoteHash, sellAccept.Acceptance.Quantity, sellAccept.Acceptance.Payment));
        Assert.Equal((carried, units), (showing.LotId, showing.Quantity));
        var before = fixture.Progression.Currency;
        var after = final.Progression!.Currency;
        Assert.Equal(before.AdmittedSupply, after.AdmittedSupply);
        Assert.Equal(earned - sold.PaymentFor(units) + bought.PaymentFor(units), after.GetActorAccount(player).Balance);
        Assert.Equal(before.GetActorAccount(marla.MerchantActorId).Balance + sold.PaymentFor(units), after.GetActorAccount(marla.MerchantActorId).Balance);
        Assert.Equal(before.GetActorAccount(kegga.MerchantActorId).Balance - bought.PaymentFor(units), after.GetActorAccount(kegga.MerchantActorId).Balance);
        Assert.Equal(CargoCircuitPolicy.BrasshollowProvision.OpeningUnits - units, marla.CountUsableStock(final.Inventory, final.Production!.Lots));
        Assert.Equal(units, kegga.CountUsableStock(final.Inventory, final.Production.Lots));
        // The carried interval is Marla's shelf interval [0, units), now in Kegga's store; Marla keeps the rest.
        CountedSourceLot shelf = fixture.Inventory.Custody.Lots.Single(lot => lot.CustodianId == marla.CustodianId
            && lot.Definition.Equals(marla.Commodity));
        Assert.Equal(new SourceInterval(shelf.Source.SourceId, 0, units), atBuy.Inventory.Custody.GetLot(carried).Source);
        Assert.Equal(new SourceInterval(shelf.Source.SourceId, 0, units), Assert.Single(final.Inventory.Custody.Lots,
            lot => lot.CustodianId == kegga.CustodianId && lot.Definition.Equals(kegga.Commodity)).Source);
        Assert.Equal(new SourceInterval(shelf.Source.SourceId, units, shelf.Quantity), Assert.Single(final.Inventory.Custody.Lots,
            lot => lot.CustodianId == marla.CustodianId && lot.Definition.Equals(marla.Commodity)).Source);
        // Both outcomes are filled, and the canonical market records name each stall.
        Assert.Equal(new[] { buyId, sellId }.Order(), final.Coordination!.PaidExchange!.Outcomes.Select(value => value.Quote.QuoteId).Order());
        Assert.All(final.Coordination.PaidExchange.Outcomes, value => Assert.Equal(WorldPaidQuoteOutcomeKind.Filled, value.Kind));
        Assert.Equal((marla.MarketId, kegga.MarketId), (final.Coordination.Markets!.FindQuote(buyId)!.MarketId,
            final.Coordination.Markets.FindQuote(sellId)!.MarketId));
    }

    private static void VerifyReport(JsonElement report, string mode, string kind = "native-cargo-v1", long limitMilliseconds = 719_999)
    {
        Assert.Equal(kind, Text(report, "Kind"));
        Assert.Equal("passed", Text(report, "Status"));
        Assert.Equal(mode, Text(report, "Mode"));
        // Observed window state sampled every frame; the absence of focus requests is the tool's separate source audit.
        JsonElement focus = report.GetProperty("Focus");
        Assert.True(focus.GetProperty("Unfocusable").GetBoolean());
        Assert.False(focus.GetProperty("NativeFocused").GetBoolean());
        Assert.False(focus.GetProperty("ObservedNativeFocus").GetBoolean());
        Assert.Equal(0, focus.GetProperty("ObservedFocusTransitions").GetInt32());
        Assert.True(focus.GetProperty("FramesObserved").GetInt32() > 0);
        Assert.True(report.GetProperty("IsLocallyPaused").GetBoolean());
        Assert.InRange(report.GetProperty("DurationMilliseconds").GetInt64(), 0, limitMilliseconds);
        Assert.All(report.GetProperty("Rendered").EnumerateArray(), sample =>
        {
            Assert.NotEqual(JsonValueKind.Null, sample.GetProperty("Pixels").ValueKind);
            Assert.NotEqual(JsonValueKind.Null, sample.GetProperty("Drawn").ValueKind);
        });
        if (kind == "native-cargo-v1") Assert.NotEqual("headless", Text(report, "Display"));
    }

    /// <summary>Every recorded player movement sample is one cardinal step of exactly 32 subunits.</summary>
    private static void AssertCardinalSteps(IReadOnlyList<WorldMovementInput> steps)
    {
        Assert.NotEmpty(steps);
        Assert.All(steps, input => Assert.True(
            Math.Abs(input.DeltaX) == WorldMovementInput.DisplacementPerStep && input.DeltaY == 0
            || input.DeltaX == 0 && Math.Abs(input.DeltaY) == WorldMovementInput.DisplacementPerStep,
            $"A recorded player sample moved ({input.DeltaX},{input.DeltaY})."));
    }

    /// <summary>
    /// The onboarding run's own save: the production composition of the production seed, then only the player's
    /// conversation, quest acceptance and delivery, three mining works and ordinary cardinal steps; the earned reward,
    /// opened divider, released water and arrival at Marla's stall all come from that journal.
    /// </summary>
    private static void VerifyOnboarding(JsonElement onboarding)
    {
        string save = AbsolutePath(onboarding, "save");
        byte[] closed = File.ReadAllBytes(save);
        Assert.Equal(Text(onboarding, "finalDatabaseSha256"), Convert.ToHexString(SHA256.HashData(closed)).ToLowerInvariant());
        Assert.Equal(NativeCargoWorld.SeedText, Text(onboarding, "seedText"));
        using var reportDocument = JsonDocument.Parse(File.ReadAllBytes(AbsolutePath(onboarding, "report")));
        JsonElement report = reportDocument.RootElement;
        VerifyReport(report, "onboarding", "native-cargo-onboarding-v1", 299_999);
        Assert.Equal("--cargo", Text(report, "Entry"));
        string copy = Path.Combine(Path.GetTempPath(), "openfantasy-native-cargo-onboarding-" + Guid.NewGuid().ToString("N") + ".sqlite");
        File.Copy(save, copy);
        try
        {
            byte[] genesisBytes = WorldSnapshotCodec.Encode(NativeCargoWorld.CreateEmbedded(NativeCargoWorld.SeedText, "Traveler"));
            WorldSnapshot genesis = WorldSnapshotCodec.Decode(genesisBytes);
            EntityId player = genesis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
            Assert.Equal(player.ToString(), Text(report, "PlayerActorId"));
            StoredWorldCommit[] commits;
            StoredWorldCommandOutcome[] rows;
            using (var database = WorldDatabase.Open(copy, create: false))
            {
                Assert.Equal(genesisBytes, database.ReadGenesis());
                commits = database.ReadJournal(0).ToArray();
                rows = database.ReadCommandOutcomes(0).ToArray();
            }
            WorldActionIntent[] intents = rows.OrderBy(row => row.Sequence).Select(row => WorldActionCodec.Decode(row.Command))
                .Where(command => command.ActorId == player).Select(command => command.Intent).ToArray();
            Assert.IsType<SpeakToActorIntent>(intents[0]);
            Assert.Equal(2, intents.Count(intent => intent is OpenFantasy.Core.Quests.DeliveryQuestIntent));
            Assert.Equal(3, intents.Count(intent => intent is ExcavateIntent));
            Assert.DoesNotContain(intents, intent => intent is PaidExchangeIntent or MarketIntent);
            ActorControlBinding control = genesis.Inventory.Controls.Single(value => value.ActorId == player);
            WorldMovementInput[] steps = commits.SelectMany(commit => WorldStepCodec.Decode(commit.AcceptedBatch).Movement)
                .Where(input => input.ActorId == player).ToArray();
            Assert.All(steps, input => Assert.Equal((control.PrincipalId, control.SessionId), (input.Delivery.PrincipalId, input.Delivery.SessionId)));
            AssertCardinalSteps(steps);
            using var recovered = SqliteWorldAuthority.OpenExisting(copy, genesisBytes);
            WorldSnapshot final = recovered.ReadStateForHost();
            Assert.Equal(commits[^1].AfterHash, final.ContentHash);
            ulong balance = final.Progression!.Currency.GetActorAccount(player).Balance;
            Assert.True(balance > 0 && balance == report.GetProperty("FinalBalance").GetUInt64(), "The reward was not earned exactly.");
            ExcavationSite site = Assert.Single(final.Excavation!.Sites);
            Assert.False(final.Space.Regions.Single(region => region.Bounds.Contains(site.Cell)).GetCell(site.Cell).IsSolid);
            Assert.Contains(final.LiquidCells!.Quantities, pair => pair.Key.Coordinate.X > site.Cell.X && pair.Value > 0);
            Assert.Equal(genesis.LiquidCells!.Quantities.Values.Aggregate(0UL, (sum, units) => sum + units),
                final.LiquidCells.Quantities.Values.Aggregate(0UL, (sum, units) => sum + units));
            CargoCircuitMarkets markets = CargoWorldAdmission.Find(final, NativeCargoWorld.Sources)!;
            Assert.NotNull(WorldMarketLocality.FindAvailable(final, player, markets.Origin.MarketId));
            JsonElement feet = report.GetProperty("FinalFeet");
            Assert.Equal(new SubunitPosition(feet.GetProperty("X").GetInt64(), feet.GetProperty("Y").GetInt64(),
                feet.GetProperty("Z").GetInt64()), final.Space.GetActor(player).Feet);
            Assert.Contains(report.GetProperty("Rendered").EnumerateArray(), sample =>
                sample.GetProperty("Pixels").GetProperty("Water").ValueKind != JsonValueKind.Null);
        }
        finally
        {
            foreach (string path in new[] { copy, copy + "-wal", copy + "-shm" }) if (File.Exists(path)) File.Delete(path);
        }
        Assert.Equal(closed, File.ReadAllBytes(save));
    }

    private static string AbsolutePath(JsonElement value, string name)
    {
        string path = Text(value, name);
        Assert.True(Path.IsPathFullyQualified(path), $"Native evidence requires an absolute {name} path.");
        Assert.True(File.Exists(path), $"Native evidence file is absent: {path}");
        return path;
    }

    private static string Text(JsonElement value, string name) => value.GetProperty(name).GetString()
        ?? throw new InvalidDataException($"Native cargo verification requires {name}.");

    private static string PublicHash(WorldSnapshot state, EntityId player) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new LocalWorldView(state, player)))));
}
