using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Host;
using OpenFantasy.Hosting.Networking;

namespace OpenFantasy.Server.Tests.SelectedTradingNative;

public sealed partial class NativeSelectedTradingTests
{
    public static bool Configured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENFANTASY_GODOT"));

    [Fact(SkipUnless = nameof(Configured), Skip = "Requires qualified Godot and retained native player evidence.")]
    public async Task Trade_only_native_delegate_buys_shows_and_sells_exact_stock_with_separate_review_and_return_invalidation()
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(ServerWorld.Cancellation);
        bound.CancelAfter(TimeSpan.FromSeconds(210));
        var elapsed = Stopwatch.StartNew();
        string output = Path.Combine(Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_PLAYER_EVIDENCE")
            ?? throw new InvalidOperationException("Native trading evidence requires OPENFANTASY_NATIVE_PLAYER_EVIDENCE."), "selected-trading");
        Directory.CreateDirectory(output);
        using var files = new ServerFiles();
        await using var world = await SelectedTradingWorld.Open(output);
        string certificate = Path.Combine(output, "public.cer");
        File.WriteAllBytes(certificate, world.Certificate.Export(X509ContentType.Cert));
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../client"));
        string background = NativeRosterProcess.BackgroundProject(source, files.DirectoryPath);
        var processes = new List<NativeRosterProcess>();
        var active = new Dictionary<EntityId, NativeRosterProcess>();
        var stages = new List<object>();
        double allowanceSeconds = 0;
        try
        {
            foreach (EntityId actor in world.Actors) active.Add(actor, Start(actor, "first", actor == world.Grantee));
            await Task.WhenAll(active.Values.Select(process => process.Opened()));
            var owner = active[world.Owner];
            var grantee = active[world.Grantee];
            foreach (var pair in active) AssertPaused(pair.Value, pair.Key, pair.Key);
            await owner.Send("control-offer", "Trade", world.Grantee);
            await owner.Send("control-close");
            await grantee.Send("control-open");
            await grantee.Send("control-accept", target: world.Owner);
            Assert.Equal("Trade", Slot(grantee, world.Owner, world.Grantee).GetProperty("Capabilities").GetString());
            var allowance = Stopwatch.StartNew();
            await owner.Send("close");
            active.Remove(world.Owner);
            while (true)
            {
                await grantee.Send("control-refresh");
                bound.Token.ThrowIfCancellationRequested();
                Assert.True(allowance.Elapsed < TimeSpan.FromSeconds(35), "The actual 30-second allowance did not expose acquisition within 35 seconds.");
                Assert.Equal(world.Prepared.Calendar, grantee.View.Calendar);
                if (Slot(grantee, world.Owner, world.Grantee).GetProperty("AcquisitionAvailable").GetBoolean()) break;
                await Task.Delay(500, bound.Token);
            }
            allowanceSeconds = allowance.Elapsed.TotalSeconds;
            Assert.True(allowanceSeconds >= 30);
            await grantee.Send("control-acquire", target: world.Owner);
            var witness = WorldControlWitnessCodecV1.Decode(Convert.FromBase64String(
                grantee.Last.GetProperty("ControlBoard").GetProperty("HeldWitness").GetString()!));
            Assert.Equal(WorldControlCapabilities.Trade, witness.Capabilities);
            await grantee.Send("control-select", target: world.Owner);
            AssertPaused(grantee, world.Grantee, world.Owner);
            await grantee.Send("control-close");
            await grantee.Send("resume");
            await grantee.Send("trade-open");
            var trade = Trading(grantee);
            Assert.True(trade.GetProperty("TradeDisclosed").GetBoolean());
            Assert.False(trade.GetProperty("InventoryDisclosed").GetBoolean());
            Assert.False(trade.GetProperty("PrivateHistory").GetBoolean());
            Assert.Equal(world.Owner.ToString(), trade.GetProperty("Actor").GetString());
            Assert.Equal(world.Markets.Origin.MarketId.ToString(), trade.GetProperty("Draft").GetProperty("Market").GetString());
            Assert.Equal(JsonValueKind.Null, Selection(grantee).GetProperty("Hp").ValueKind);
            Assert.Empty(Selection(grantee).GetProperty("EncounterActors").EnumerateArray());
            int before = grantee.Submissions.Count;
            ulong balance = Trading(grantee).GetProperty("Wallet").GetUInt64();
            await grantee.Send("trade-ask");
            var buy = Assert.Single(Trading(grantee).GetProperty("Quotes").EnumerateArray(), row => row.GetProperty("Status").GetString() == "Open");
            EntityId buyQuote = EntityId.Parse(buy.GetProperty("Id").GetString()!);
            Assert.Equal(20UL, buy.GetProperty("UnitPrice").GetUInt64());
            await grantee.Send("trade-review");
            await grantee.Send("capture-trade", "buy-review");
            Assert.Equal(before, grantee.Submissions.Count);
            Assert.Equal(balance, Trading(grantee).GetProperty("Wallet").GetUInt64());
            await grantee.Send("trade-confirm", "Applied");
            var buyCommand = LastCommand(grantee);
            Assert.Equal(buyQuote, Assert.IsType<AcceptSpotQuoteIntent>(buyCommand.Intent).Acceptance.QuoteId);
            await Until(() => Trading(grantee).GetProperty("Wallet").GetUInt64() == balance - 20,
                "The exact selected purchase balance was not published.", [grantee], bound.Token);
            var stall = Assert.Single(Trading(grantee).GetProperty("Stalls").EnumerateArray(), row =>
                row.GetProperty("Market").GetString() == world.Markets.Origin.MarketId.ToString());
            EntityId receivedLot = EntityId.Parse(Assert.Single(stall.GetProperty("Lots").EnumerateArray()).GetProperty("Lot").GetString()!);
            Mark("trade-only-purchase-reviewed-and-filled");

            await grantee.Send("trade-direction", "Sell");
            await grantee.Send("trade-lot", target: receivedLot);
            await grantee.Send("trade-show");
            await grantee.Send("trade-stop-showing");
            Assert.IsType<WithdrawMarketLotDisclosureIntent>(LastCommand(grantee).Intent);
            await grantee.Send("trade-show");
            var showing = LastCommand(grantee);
            Assert.Equal(receivedLot, Assert.IsType<DiscloseMarketLotIntent>(showing.Intent).LotId);
            Assert.Equal(balance - 20, Trading(grantee).GetProperty("Wallet").GetUInt64());
            await grantee.Send("trade-ask");
            var sell = Assert.Single(Trading(grantee).GetProperty("Quotes").EnumerateArray(), row => row.GetProperty("Status").GetString() == "Open");
            EntityId sellQuote = EntityId.Parse(sell.GetProperty("Id").GetString()!);
            Assert.Equal(14UL, sell.GetProperty("UnitPrice").GetUInt64());
            Assert.Equal(receivedLot.ToString(), sell.GetProperty("Lot").GetString());
            await grantee.Send("trade-review");
            await grantee.Send("capture-trade", "sell-exact-received-lot");
            await grantee.Send("trade-confirm", "Applied");
            var sellCommand = LastCommand(grantee);
            Assert.Equal(sellQuote, Assert.IsType<AcceptSpotQuoteIntent>(sellCommand.Intent).Acceptance.QuoteId);
            await Until(() => Trading(grantee).GetProperty("Wallet").GetUInt64() == balance - 6,
                "The selected sale proceeds were not published.", [grantee], bound.Token);
            Mark("same-finite-lot-sold-back");

            // A third valid quote is deliberately left unconsumed; owner return invalidates this exact review.
            await grantee.Send("trade-direction", "Buy");
            await grantee.Send("trade-ask");
            await grantee.Send("trade-review");
            EntityId staleQuote = EntityId.Parse(Trading(grantee).GetProperty("Draft").GetProperty("Review").GetString()!);
            await grantee.Send("capture-trade", "before-owner-return");
            int committed = grantee.Submissions.Count;
            var returning = Start(world.Owner, "return", rendered: false);
            active.Add(world.Owner, returning);
            await returning.Opened();
            await Until(() => Selection(grantee).GetProperty("Actor").GetString() == world.Grantee.ToString(),
                "The original owner did not invalidate selected trade.", active.Values, bound.Token);
            AssertPaused(grantee, world.Grantee, world.Grantee);
            await returning.Send("control-revoke", target: world.Grantee);
            await returning.Send("control-close");
            await grantee.Send("trade-confirm", "blocked");
            Assert.Equal(committed, grantee.Submissions.Count);
            Assert.False(Trading(grantee).GetProperty("Open").GetBoolean());
            Assert.Equal(JsonValueKind.Null, Trading(grantee).GetProperty("Draft").GetProperty("Review").ValueKind);
            Mark("owner-return-refused-unconsumed-review");
            foreach (var process in active.Values) await process.Send("pause");
            foreach (var process in active.Values) await process.Send("close");
            await world.Stop();
            Verify(output, world, processes, witness, buyCommand, showing, sellCommand, receivedLot, staleQuote);
            bound.Token.ThrowIfCancellationRequested();
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(210), "The native selected trade journey exceeded its absolute 210-second bound.");
            Write(output, "journey.json", new { Kind = "native-selected-trading-v1", Status = "passed", AllowanceSeconds = allowanceSeconds,
                ElapsedMilliseconds = elapsed.ElapsedMilliseconds, Stages = stages,
                Preparation = world.Description, Scope = "Trade without Inventory or PrivateHistory; actual observed quote review, explicit lot showing, purchase/sale and owner-return invalidation. No archive discovery claim." });
        }
        catch (Exception error)
        {
            Write(output, "journey.failed.json", new { Kind = "native-selected-trading-v1", Status = "failed", Error = error.ToString(),
                AllowanceSeconds = allowanceSeconds, ElapsedMilliseconds = elapsed.ElapsedMilliseconds, Stages = stages });
            throw;
        }
        finally
        {
            try { foreach (var process in processes) await process.DisposeAsync(); }
            finally { await world.Stop(); PreserveFinal(output, world); }
        }

        NativeRosterProcess Start(EntityId actor, string suffix, bool rendered)
        {
            var invitation = new PlayerConnectionInvitation(world.Server.Endpoint, world.Host.Admission.Compatibility,
                actor, world.Host.Issue(actor, TimeSpan.FromMinutes(4)).Export());
            string path = Path.Combine(files.DirectoryPath, actor + "-" + suffix + ".invitation.json");
            PlayerInvitationFile.Create(path, invitation);
            var process = new NativeRosterProcess(Path.Combine(output, actor + "-" + suffix), path, certificate,
                rendered ? background : source, rendered, bound.Token);
            processes.Add(process);
            return process;
        }
        void Mark(string stage) => stages.Add(new { Stage = stage, ElapsedMilliseconds = elapsed.ElapsedMilliseconds,
            Peers = active.Select(pair => new { Home = pair.Key.ToString(), Actor = Selection(pair.Value).GetProperty("Actor").GetString(),
                Commands = pair.Value.Submissions.Count }).ToArray() });
    }
}
