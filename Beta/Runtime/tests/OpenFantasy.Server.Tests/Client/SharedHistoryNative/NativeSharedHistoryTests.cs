using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Host;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Server.Tests.ConnectedControl;

namespace OpenFantasy.Server.Tests.SharedHistoryNative;

public sealed partial class NativeSharedHistoryTests
{
    public static bool Configured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENFANTASY_GODOT"));

    [Fact(SkipUnless = nameof(Configured), Skip = "Requires qualified Godot and retained native player evidence.")]
    public async Task Native_history_discovers_real_terms_and_history_only_delegate_loses_private_screen_on_owner_return()
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(ServerWorld.Cancellation);
        bound.CancelAfter(TimeSpan.FromSeconds(210));
        var elapsed = Stopwatch.StartNew();
        string output = Path.Combine(Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_PLAYER_EVIDENCE")
            ?? throw new InvalidOperationException("Native History requires OPENFANTASY_NATIVE_PLAYER_EVIDENCE."), "shared-history");
        Directory.CreateDirectory(output);
        using var files = new ServerFiles();
        await using var world = await ConnectedControlWorld.Open(prepareCombatApproach: false);
        File.WriteAllBytes(Path.Combine(output, "genesis.ofc"), WorldSnapshotCodec.Encode(world.Genesis));
        File.WriteAllText(Path.Combine(output, "preparation.txt"),
            "Unmodified fresh four-human Cargo admission. No managed movement, minted stock, prepared terminal record or replacement genesis.\n"
            + "The owner creates and withdraws one actual finite gift through native controls before browsing its history.\n");
        string certificate = Path.Combine(output, "public.cer");
        File.WriteAllBytes(certificate, world.Certificate.Export(X509ContentType.Cert));
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../client"));
        string background = NativeRosterProcess.BackgroundProject(source, files.DirectoryPath);
        var processes = new List<NativeRosterProcess>();
        var active = new Dictionary<EntityId, NativeRosterProcess>();
        var stages = new List<object>();
        HistoryBaseline? baseline = null;
        double allowanceSeconds = 0;
        try
        {
            foreach (EntityId actor in world.Actors) active.Add(actor, Start(actor, "first", actor == world.Grantee));
            await Task.WhenAll(active.Values.Select(process => process.Opened()));
            var owner = active[world.Owner];
            var grantee = active[world.Grantee];
            EntityId recipient = world.Actors[2];
            foreach (var pair in active) AssertPaused(pair.Value, pair.Key, pair.Key);

            await owner.Send("resume");
            await owner.Send("gift-open");
            var lot = owner.Last.GetProperty("Gifts").GetProperty("Lots").EnumerateArray().First();
            await owner.Send("gift-lot", lot.GetProperty("Kind").GetString(), EntityId.Parse(lot.GetProperty("Lot").GetString()!));
            await owner.Send("gift-recipient", target: recipient);
            await owner.Send("gift-quantity", "1");
            await owner.Send("gift-prepare");
            await owner.Send("gift-review", "ProposeMaterial");
            await owner.Send("gift-confirm", "Applied");
            WorldActionCommand proposal = LastCommand(owner);
            var terms = Assert.IsType<ProposeMaterialIntent>(proposal.Intent).Terms;
            EntityId subject = WorldMaterialProposal.DeriveIdentity(proposal.CommandId);
            await Until(() => owner.Last.GetProperty("Gifts").GetProperty("Offers").EnumerateArray()
                    .Any(row => row.GetProperty("Proposal").GetString() == subject.ToString()
                        && row.GetProperty("Status").GetString() == "Open"),
                "The actual native gift offer was not observed before withdrawal.", [owner], bound.Token);
            await owner.Send("gift-offer", target: subject);
            await owner.Send("gift-review", "WithdrawMaterial");
            await owner.Send("gift-confirm", "Applied");
            Assert.Equal(subject, Assert.IsType<WithdrawMaterialProposalIntent>(LastCommand(owner).Intent).ProposalId);
            await owner.Send("gift-close");
            await owner.Send("pause");
            baseline = await CaptureReadOnlyBaseline(owner, active, bound.Token);
            Write(output, "read-only-baseline.json", baseline);

            // History verbs contain no target identity. The actual first discovered row supplies the detail subject.
            await owner.Send("history-open");
            Assert.Equal(JsonValueKind.Null, History(owner).GetProperty("Selected").ValueKind);
            await owner.Send("history-select-first");
            Assert.Equal(JsonValueKind.Null, History(owner).GetProperty("Detail").ValueKind);
            await owner.Send("history-inspect");
            AssertRecord(owner, world.Owner, recipient, subject, terms);
            await owner.Send("history-close");
            Mark("native-terminal-discovered-and-explicitly-inspected");

            await grantee.Send("history-open");
            Assert.Empty(History(grantee).GetProperty("Entries").EnumerateArray());
            await grantee.Send("capture-history", "unrelated-home-empty");
            await grantee.Send("history-close");
            await owner.Send("control-offer", "PrivateHistory", world.Grantee);
            await owner.Send("control-close");
            await grantee.Send("control-open");
            await grantee.Send("control-accept", target: world.Owner);
            Assert.Equal("PrivateHistory", Slot(grantee, world.Owner, world.Grantee).GetProperty("Capabilities").GetString());
            var allowance = Stopwatch.StartNew();
            await owner.Send("close");
            active.Remove(world.Owner);
            while (true)
            {
                await grantee.Send("control-refresh");
                bound.Token.ThrowIfCancellationRequested();
                Assert.True(allowance.Elapsed < TimeSpan.FromSeconds(35), "The actual 30-second allowance did not expose acquisition within 35 seconds.");
                Assert.Equal(baseline.Calendar, grantee.View.Calendar);
                if (Slot(grantee, world.Owner, world.Grantee).GetProperty("AcquisitionAvailable").GetBoolean()) break;
                await Task.Delay(500, bound.Token);
            }
            allowanceSeconds = allowance.Elapsed.TotalSeconds;
            Assert.True(allowanceSeconds >= 30);
            await grantee.Send("control-acquire", target: world.Owner);
            var witness = WorldControlWitnessCodecV1.Decode(Convert.FromBase64String(
                grantee.Last.GetProperty("ControlBoard").GetProperty("HeldWitness").GetString()!));
            Assert.Equal(WorldControlCapabilities.PrivateHistory, witness.Capabilities);
            await grantee.Send("control-select", target: world.Owner);
            await grantee.Send("control-close");
            AssertPaused(grantee, world.Grantee, world.Owner);
            await grantee.Send("history-open", "controller");
            Assert.False(History(grantee).GetProperty("InventoryDisclosed").GetBoolean());
            Assert.False(History(grantee).GetProperty("TradeDisclosed").GetBoolean());
            Assert.Equal("PrivateHistory", History(grantee).GetProperty("Capabilities").GetString());
            await grantee.Send("history-select-first");
            await grantee.Send("history-inspect", "controller");
            AssertRecord(grantee, world.Owner, recipient, subject, terms);
            await grantee.Send("capture-history", "history-only-delegated-detail");
            Mark("history-without-inventory-or-trade");

            await grantee.Send("history-close");
            Assert.Equal(JsonValueKind.Null, History(grantee).GetProperty("Detail").ValueKind);
            await grantee.Send("history-open");
            Assert.Equal(JsonValueKind.Null, History(grantee).GetProperty("Selected").ValueKind);
            Assert.Equal(JsonValueKind.Null, History(grantee).GetProperty("Detail").ValueKind);
            await grantee.Send("history-select-first");
            await grantee.Send("history-inspect");
            AssertRecord(grantee, world.Owner, recipient, subject, terms);
            await grantee.Send("capture-history", "explicit-reopened-detail");
            Mark("close-clears-reopen-requires-explicit-row-inspection");

            var returning = Start(world.Owner, "return", rendered: false);
            active.Add(world.Owner, returning);
            await returning.Opened();
            await Until(() => Selection(grantee).GetProperty("Actor").GetString() == world.Grantee.ToString(),
                "The original owner did not invalidate the selected History scope.", active.Values, bound.Token);
            await grantee.Send("history-observe-closed");
            await returning.Send("control-revoke", target: world.Grantee);
            await returning.Send("control-close");
            await grantee.Send("history-open");
            Assert.Equal(world.Grantee.ToString(), History(grantee).GetProperty("Actor").GetString());
            Assert.Empty(History(grantee).GetProperty("Entries").EnumerateArray());
            Assert.Equal(JsonValueKind.Null, History(grantee).GetProperty("Detail").ValueKind);
            await grantee.Send("capture-history", "returned-home-empty");
            await grantee.Send("history-close");
            Mark("owner-return-clears-private-view-and-revocation-remains-durable");

            foreach (var process in active.Values) await process.Send("close");
            await world.Stop();
            Verify(output, world, processes, witness, proposal, baseline);
            bound.Token.ThrowIfCancellationRequested();
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(210), "The native History journey exceeded its absolute 210-second bound.");
            Write(output, "journey.json", new { Kind = "native-shared-history-v1", Status = "passed", AllowanceSeconds = allowanceSeconds,
                ElapsedMilliseconds = elapsed.ElapsedMilliseconds, Stages = stages,
                Scope = "Four actual TLS/Godot peers; native-created withdrawn gift discovered without supplied identity; exact terms; PrivateHistory only; close/reopen; owner-return clearing. One record, no native paging or inbound-delay claim." });
        }
        catch (Exception error)
        {
            Write(output, "journey.failed.json", new { Kind = "native-shared-history-v1", Status = "failed", Error = error.ToString(),
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

        void Mark(string stage)
        {
            foreach (var process in active.Values)
            {
                Assert.Equal(baseline!.Calendar, process.View.Calendar);
                Assert.False(Selection(process).GetProperty("Ready").GetBoolean());
            }
            stages.Add(new { Stage = stage, ElapsedMilliseconds = elapsed.ElapsedMilliseconds,
                Peers = active.Select(pair => new { Home = pair.Key.ToString(), Actor = Selection(pair.Value).GetProperty("Actor").GetString(),
                    Commands = pair.Value.Submissions.Count, Calendar = pair.Value.View.Calendar }).ToArray() });
        }
    }
}
