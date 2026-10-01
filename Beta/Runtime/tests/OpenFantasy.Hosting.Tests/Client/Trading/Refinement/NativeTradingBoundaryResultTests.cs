using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Content.Trading;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Client.Trading.Refinement;

/// <summary>
/// Authenticates the native refusal journeys from their saved journals: the prepared fixture replays exactly, the
/// recorded state at the attempted confirmation independently shows the intended refusal reason with every other
/// condition met, no acceptance precedes it, and the only acceptance is the lawful recovery with exact deltas.
/// </summary>
public sealed class NativeTradingBoundaryResultTests
{
    [Fact]
    public void Opt_in_native_boundary_results_authenticate_refusals_preserved_goods_and_lawful_recovery()
    {
        string? manifestPath = Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_TRADING_BOUNDARY_RESULTS");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(manifestPath), "The actual native boundary results manifest is required.");
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath!));
        JsonElement[] results = manifest.RootElement.GetProperty("results").EnumerateArray().ToArray();
        Assert.Equal(NativeTradingBoundaryFixtureTests.Scenarios.Order(StringComparer.Ordinal),
            results.Select(result => Text(result, "scenario")).Order(StringComparer.Ordinal));
        foreach (JsonElement result in results) Verify(result);
    }

    private static void Verify(JsonElement result)
    {
        string scenario = Text(result, "scenario");
        string save = AbsolutePath(result, "save");
        byte[] genesisBytes = File.ReadAllBytes(AbsolutePath(result, "genesis"));
        byte[] beforeBytes = File.ReadAllBytes(AbsolutePath(result, "before"));
        Assert.Equal(WorldSnapshotCodec.Encode(NativeMarketTestWorld.Genesis), genesisBytes);
        WorldSnapshot fixture = WorldSnapshotCodec.Decode(beforeBytes);
        using var metadataDocument = JsonDocument.Parse(File.ReadAllBytes(AbsolutePath(result, "metadata")));
        using var reportDocument = JsonDocument.Parse(File.ReadAllBytes(AbsolutePath(result, "report")));
        JsonElement metadata = metadataDocument.RootElement;
        JsonElement report = reportDocument.RootElement;
        Assert.Equal(("native-trading-boundary-fixture-v1", scenario), (Text(metadata, "kind"), Text(metadata, "scenario")));
        Assert.Equal(("native-trading-boundary-v1", "passed", scenario, "keyboard"),
            (Text(report, "Kind"), Text(report, "Status"), Text(report, "Scenario"), Text(report, "Route")));
        Assert.Equal(Text(metadata, "beforeHash"), fixture.ContentHash.Value);
        MarketBinding stall = NativeTradingWorld.FindStall(fixture)!;
        EntityId player = fixture.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        EntityId wren = stall.MerchantActorId;
        EntityId quoteId = EntityId.Parse(Text(metadata, "quoteId"));
        Assert.Equal(Text(metadata, "quoteId"), Text(report, "QuoteId"));
        WorldPaidQuote prepared = fixture.Coordination!.PaidExchange!.FindOpen(quoteId)!;
        SpotQuote offer = prepared.Quote;
        ulong units = NativeTradingBoundaryFixtureTests.Units;
        // The prepared offer is Wren's genuine answer with the authored window, never shortened or rewritten.
        Assert.Equal((wren, player, SpotQuoteSide.IssuerSells, units), (offer.IssuerActorId, offer.CounterpartyActorId, offer.Side, offer.QuantityAllowance));
        Assert.Equal(NativeTradingPolicy.OfferWindowSteps, offer.ExpiresAtStep - offer.IssuedAtStep);
        Assert.Equal(Text(metadata, "quoteHash"), offer.ContentHash.Value);
        Assert.Equal(PublicHash(fixture, player), Text(report, "InitialHash"));
        ulong attemptStep = report.GetProperty("AttemptStep").GetUInt64();
        ulong reviewed = report.GetProperty("ReviewedIssuerRevision").GetUInt64();
        // The review happened paused on the fixture itself, so it captured Wren's fixture presence.
        Assert.Equal(fixture.Space.GetActor(wren).Revision, reviewed);

        StoredWorldCommandOutcome[] rows;
        StoredWorldCommit[] commits;
        using (var database = WorldDatabase.Open(save, create: false))
        {
            Assert.Equal(genesisBytes, database.ReadGenesis());
            commits = database.ReadJournal(fixture.Revision).ToArray();
            rows = database.ReadCommandOutcomes(fixture.Revision).ToArray();
        }
        var commands = rows.OrderBy(row => row.Sequence).Select(row => (Row: row, Command: WorldActionCodec.Decode(row.Command))).ToArray();
        Assert.All(commands, entry => Assert.Equal(CommandReceiptStatus.Applied, WorldReceiptCodecV1.Decode(entry.Row.Receipt).Status));
        var accepts = commands.Where(entry => entry.Command.Intent is AcceptSpotQuoteIntent).ToArray();
        // Exactly one acceptance anywhere after preparation: the player's lawful recovery.
        var (acceptRow, acceptCommand) = Assert.Single(accepts);
        Assert.Equal(player, acceptCommand.ActorId);
        var accept = (AcceptSpotQuoteIntent)acceptCommand.Intent;
        EntityId recovered = EntityId.Parse(Text(report, "RecoveredQuoteId"));
        Assert.Equal(recovered, accept.Acceptance.QuoteId);
        Assert.All(commands.Where(entry => entry.Command.ActorId == wren), entry => Assert.IsNotType<AcceptSpotQuoteIntent>(entry.Command.Intent));

        byte[] closedBytes = File.ReadAllBytes(save);
        using (var authority = SqliteWorldAuthority.OpenExisting(save, genesisBytes))
        {
            Assert.Equal(beforeBytes, WorldSnapshotCodec.Encode(authority.ReadRecordedStateForHost(fixture.Revision).State));
            WorldSnapshot final = authority.ReadStateForHost();
            Assert.Equal(commits[^1].AfterHash, final.ContentHash);
            WorldSnapshot State(ulong sequence) => authority.ReadRecordedStateForHost(sequence).State;
            WorldSnapshot attempt = AtStep(commits, State, fixture, attemptStep);
            WorldSnapshot acceptBefore = State(acceptRow.Sequence - 1);
            Assert.True(acceptBefore.AdvancingSteps >= attemptStep, "An acceptance preceded the refused attempt.");
            ulong total = checked((offer.UnitPrice + offer.UnitFees) * units);
            ulong purseAtAttempt = attempt.Progression!.Currency.GetActorAccount(player).Balance;
            WorldPaidQuote? openAtAttempt = attempt.Coordination!.PaidExchange!.FindOpen(quoteId);
            bool reachable = WorldMarketLocality.FindAvailable(attempt, player, stall.MarketId) is not null;
            switch (scenario)
            {
                case "expired":
                    // Due at the attempt (open but due, or already closed as expired); otherwise funded and in person.
                    Assert.True(openAtAttempt is null ? attempt.Coordination.PaidExchange.FindOutcome(quoteId)?.Kind == WorldPaidQuoteOutcomeKind.Expired
                        : openAtAttempt.IsDue(attempt.AdvancingSteps));
                    Assert.True(purseAtAttempt >= total && reachable);
                    Assert.NotEqual(quoteId, recovered);
                    Assert.Equal(WorldPaidQuoteOutcomeKind.Expired, final.Coordination!.PaidExchange!.FindOutcome(quoteId)!.Kind);
                    var reissue = commands.Single(entry => entry.Command.Intent is IssueMarketQuoteIntent issue && issue.Quote.QuoteId == recovered);
                    Assert.Equal(wren, reissue.Command.ActorId);
                    Assert.True(reissue.Row.Sequence > fixture.Revision);
                    break;
                case "unfunded":
                    Assert.True(purseAtAttempt < total, "The purse could pay at the attempt.");
                    Assert.True(openAtAttempt is not null && !openAtAttempt.IsDue(attempt.AdvancingSteps) && reachable);
                    Assert.True(stall.CountUsableStock(attempt.Inventory, attempt.Production!.Lots) >= units);
                    Assert.Equal(quoteId, recovered);
                    Assert.True(acceptBefore.Progression!.Currency.GetActorAccount(player).Balance >= total, "Recovery was not funded by earning.");
                    string[] own = commands.Where(entry => entry.Command.ActorId == player).Select(entry => entry.Command.Intent.GetType().Name).ToArray();
                    Assert.Equal("SpeakToActorIntent", own[0]);
                    Assert.Contains("CloseDialogueIntent", own);
                    Assert.Equal(nameof(AcceptSpotQuoteIntent), own[^1]);
                    Assert.Equal(5, own.Length);
                    break;
                case "moved":
                    Assert.NotEqual(reviewed, attempt.Space.GetActor(wren).Revision);
                    Assert.True(purseAtAttempt >= total && openAtAttempt is not null && !openAtAttempt.IsDue(attempt.AdvancingSteps));
                    Assert.Equal(quoteId, recovered);
                    Assert.Equal(new[] { nameof(AcceptSpotQuoteIntent) },
                        commands.Where(entry => entry.Command.ActorId == player).Select(entry => entry.Command.Intent.GetType().Name));
                    break;
                default:
                    throw new InvalidDataException("Unknown boundary scenario " + scenario);
            }
            // The recovery restates the re-reviewed terms and the presence it actually observed, then moves exact amounts.
            SpotQuote filled = (State(acceptRow.Sequence - 1).Coordination!.PaidExchange!.FindOpen(recovered)
                ?? throw new InvalidDataException("The recovered offer was not open when accepted.")).Quote;
            Assert.Equal((filled.ContentHash, units, filled.PaymentFor(units)), (accept.Acceptance.QuoteHash, accept.Acceptance.Quantity, accept.Acceptance.Payment));
            Assert.Equal(acceptBefore.Space.GetActor(wren).Revision, accept.ExpectedIssuerRevision);
            Assert.Equal(report.GetProperty("RecoveredIssuerRevision").GetUInt64(), accept.ExpectedIssuerRevision);
            if (scenario == "moved") Assert.NotEqual(reviewed, accept.ExpectedIssuerRevision);
            WorldSnapshot afterAccept = State(acceptRow.Sequence);
            Assert.Equal(acceptBefore.Progression!.Currency.GetActorAccount(player).Balance - filled.PaymentFor(units),
                afterAccept.Progression!.Currency.GetActorAccount(player).Balance);
            Assert.Equal(Units(acceptBefore, player, stall) + units, Units(afterAccept, player, stall));
            Assert.Equal(fixture.Progression!.Currency.AdmittedSupply, final.Progression!.Currency.AdmittedSupply);
            Assert.Equal(WorldPaidQuoteOutcomeKind.Filled, final.Coordination!.PaidExchange!.FindOutcome(recovered)!.Kind);
            Assert.Equal(PublicHash(final, player), Text(report, "FinalHash"));
        }
        Assert.Equal(closedBytes, File.ReadAllBytes(save));
    }

    /// <summary>The last recorded state whose advancing step is at most the attempt's: what the player saw then.</summary>
    private static WorldSnapshot AtStep(StoredWorldCommit[] commits, Func<ulong, WorldSnapshot> state, WorldSnapshot fixture, ulong step)
    {
        Assert.True(fixture.AdvancingSteps <= step, "The attempt precedes the fixture.");
        int low = 0, high = commits.Length - 1, found = -1;
        while (low <= high)
        {
            int middle = (low + high) / 2;
            if (state(commits[middle].Sequence).AdvancingSteps <= step) { found = middle; low = middle + 1; }
            else high = middle - 1;
        }
        WorldSnapshot result = found < 0 ? fixture : state(commits[found].Sequence);
        Assert.Equal(step, result.AdvancingSteps);
        return result;
    }

    private static ulong Units(WorldSnapshot state, EntityId actor, MarketBinding stall)
    {
        EntityId bag = state.GetResident(actor).InventoryCustodianId;
        return state.Inventory.Custody.Lots.Where(lot => lot.CustodianId == bag && lot.Definition == stall.Commodity)
            .Aggregate(0UL, (sum, lot) => sum + lot.Quantity);
    }

    private static string AbsolutePath(JsonElement value, string name)
    {
        string path = Text(value, name);
        Assert.True(Path.IsPathFullyQualified(path), $"Native evidence requires an absolute {name} path.");
        Assert.True(File.Exists(path), $"Native evidence file is absent: {path}");
        return path;
    }

    private static string Text(JsonElement value, string name) => value.GetProperty(name).GetString()
        ?? throw new InvalidDataException($"Native boundary verification requires {name}.");

    private static string PublicHash(WorldSnapshot state, EntityId player) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new LocalWorldView(state, player)))));
}
