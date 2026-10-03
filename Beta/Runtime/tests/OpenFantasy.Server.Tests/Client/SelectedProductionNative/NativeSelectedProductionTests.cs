using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Host;
using OpenFantasy.Hosting.Networking;

namespace OpenFantasy.Server.Tests.SelectedProductionNative;

public sealed partial class NativeSelectedProductionTests
{
    public static bool Configured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENFANTASY_GODOT"));

    [Theory(SkipUnless = nameof(Configured), Skip = "Requires qualified Godot and retained native evidence.")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Selected_native_production_preserves_real_inputs_scope_operator_and_original_history(bool excavation)
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(ServerWorld.Cancellation);
        bound.CancelAfter(TimeSpan.FromSeconds(180));
        var elapsed = Stopwatch.StartNew();
        string mode = excavation ? "excavation" : "gather-craft-gift";
        string output = Path.Combine(Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_PLAYER_EVIDENCE")
            ?? throw new InvalidOperationException("Native production evidence must be retained."), "selected-production", mode);
        Directory.CreateDirectory(output);
        using var files = new ServerFiles();
        await using var world = await SelectedProductionWorld.Open(excavation, output);
        File.WriteAllBytes(Path.Combine(output, "genesis.ofc"), WorldSnapshotCodec.Encode(world.Genesis));
        File.WriteAllBytes(Path.Combine(output, "prepared.ofc"), WorldSnapshotCodec.Encode(world.Prepared));
        File.WriteAllText(Path.Combine(output, "preparation.txt"), world.Description);
        string certificate = Path.Combine(output, "public.cer");
        File.WriteAllBytes(certificate, world.Certificate.Export(X509ContentType.Cert));
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../client"));
        string background = NativeRosterProcess.BackgroundProject(source, files.DirectoryPath);
        var all = new List<NativeRosterProcess>();
        var peers = new Dictionary<EntityId, NativeRosterProcess>();
        var mailboxActivity = new Dictionary<NativeRosterProcess, TimeSpan>();
        var liveMailboxes = new HashSet<NativeRosterProcess>();
        var stages = new List<object>();
        var productionCommands = new List<WorldActionCommand>();
        WorldControlCapabilities scopes = WorldControlCapabilities.Production | WorldControlCapabilities.Traversal
            | (excavation ? 0 : WorldControlCapabilities.Inventory);
        double expirySeconds = 0;
        try
        {
            foreach (EntityId actor in world.Actors)
                peers.Add(actor, Start(actor, "first", actor == world.Grantee || !excavation && actor == world.Recipient));
            await Task.WhenAll(peers.Values.Select(peer => peer.Opened()));
            await ObserveIdlePeers(force: true);
            foreach (var pair in peers) AssertPaused(pair.Value, pair.Key, pair.Key);
            var owner = peers[world.Owner];
            var grantee = peers[world.Grantee];
            await Send(owner, "control-offer", scopes.ToString(), world.Grantee);
            await Send(owner, "control-close");
            await Send(grantee, "control-open");
            await Send(grantee, "capture-selection");
            await Send(grantee, "control-accept", target: world.Owner);
            var allowance = Stopwatch.StartNew();
            await Send(owner, "close");
            peers.Remove(world.Owner);
            while (true)
            {
                await Send(grantee, "control-refresh");
                bound.Token.ThrowIfCancellationRequested();
                Assert.True(allowance.Elapsed < TimeSpan.FromSeconds(35), "The actual 30-second allowance did not expose acquisition within 35 seconds.");
                Assert.Equal(world.Prepared.Calendar, grantee.View.Calendar);
                if (Slot(grantee, world.Owner, world.Grantee).GetProperty("AcquisitionAvailable").GetBoolean()) break;
                await Task.Delay(500, bound.Token);
            }
            expirySeconds = allowance.Elapsed.TotalSeconds;
            Assert.True(expirySeconds >= 30);
            await Send(grantee, "control-acquire", target: world.Owner);
            var witness = WorldControlWitnessCodecV1.Decode(Convert.FromBase64String(
                grantee.Last.GetProperty("ControlBoard").GetProperty("HeldWitness").GetString()!));
            Assert.Equal(scopes, witness.Capabilities);
            await Send(grantee, "control-select", target: world.Owner);
            AssertPaused(grantee, world.Grantee, world.Owner);
            await Send(grantee, "control-close");
            Assert.Equal(!excavation, grantee.Last.GetProperty("Gifts").GetProperty("InventoryDisclosed").GetBoolean());
            Assert.Equal(JsonValueKind.Null, Selection(grantee).GetProperty("Hp").ValueKind);
            Mark("selected-paused");
            await Send(grantee, "resume");

            if (excavation)
            {
                EntityId site = Assert.Single(world.Prepared.Excavation!.Sites).SiteId;
                for (int work = 0; work < 3; work++)
                    await Production("Excavate", site, work is 0 or 2 ? $"excavate-{work + 1}" : null);
                await Send(grantee, "capture-selection");
                Mark("opened-breach");
            }
            else
            {
                await Production("Gather", world.BerryNode, "berry-first");
                await Production("Gather", world.BerryNode);
                var craft = await Production("Craft", world.Recipe, "craft-rations");
                EntityId outputLot = EntityId.DeriveSpawned(craft.CommandId, 1);
                await Until(() => grantee.Last.GetProperty("Gifts").GetProperty("Lots").EnumerateArray().Any(lot =>
                    lot.GetProperty("Lot").GetString() == outputLot.ToString() && lot.GetProperty("Kind").GetString() == "Produced"
                    && lot.GetProperty("Quantity").GetUInt64() == 2), "The actual ration batch did not reach its operated inventory.", [grantee], bound.Token);
                await Send(grantee, "gift-open");
                await Send(grantee, "gift-lot", "Produced", outputLot);
                await Send(grantee, "gift-recipient", target: world.Recipient);
                await Send(grantee, "gift-quantity", "1");
                await Send(grantee, "gift-prepare");
                await Send(grantee, "capture-gifts");
                await Send(grantee, "gift-review", "ProposeMaterial");
                await Send(grantee, "gift-confirm", "Applied");
                EntityId proposal = WorldMaterialProposal.DeriveIdentity(LastCommand(grantee).CommandId);
                await Send(grantee, "gift-close");
                var recipient = peers[world.Recipient];
                await Until(() => recipient.Last.GetProperty("Gifts").GetProperty("Offers").EnumerateArray().Any(offer =>
                    offer.GetProperty("Proposal").GetString() == proposal.ToString() && offer.GetProperty("Status").GetString() == "Open"),
                    "The exact produced gift did not reach its real recipient.", [recipient], bound.Token);
                await Send(recipient, "resume");
                await Send(recipient, "gift-open");
                await Send(recipient, "gift-offer", target: proposal);
                await Send(recipient, "capture-gifts");
                await Send(recipient, "gift-review", "AcceptMaterial");
                await Send(recipient, "gift-confirm", "Applied");
                await Send(recipient, "gift-close");
            }
            Mark("production-completed");
            foreach (var peer in peers.Values) await Send(peer, "pause");
            await ObserveIdlePeers(force: true);
            var returned = Start(world.Owner, "return", rendered: false);
            peers.Add(world.Owner, returned);
            await returned.Opened();
            await Until(() => Selection(grantee).GetProperty("Actor").GetString() == world.Grantee.ToString(),
                "Owner return did not force the producer home.", peers.Values, bound.Token);
            AssertPaused(grantee, world.Grantee, world.Grantee);
            await Send(returned, "control-revoke", target: world.Grantee);
            await Send(returned, "control-close");
            await Send(grantee, "control-open");
            Assert.Equal("Revoked", Slot(grantee, world.Owner, world.Grantee).GetProperty("Status").GetString());
            Assert.Equal(JsonValueKind.Null, grantee.Last.GetProperty("ControlBoard").GetProperty("HeldWitness").ValueKind);
            await Send(grantee, "control-close");
            Mark("returned-revoked-paused");
            foreach (var peer in peers.Values) await Send(peer, "close");
            Assert.False(world.Host.Session.Completion.IsCompleted);
            await world.Stop();
            Verify(output, world, all, witness, productionCommands, excavation);
            bound.Token.ThrowIfCancellationRequested();
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(180), "The native production journey exceeded its absolute 180-second bound.");
            Write(output, "journey.json", new { Kind = "native-selected-production-v1", Mode = mode, Status = "passed",
                ExpirySeconds = expirySeconds, ElapsedMilliseconds = elapsed.ElapsedMilliseconds, Stages = stages,
                Preparation = "Original-genesis journaled walking and ordinary time, not native travel or fabricated effort." });

            async Task<WorldActionCommand> Production(string action, EntityId target, string? capture = null)
            {
                if (action != "Craft") await Send(grantee, "production-target", target: target);
                await Send(grantee, "production-review", action, target);
                if (capture is not null) await Send(grantee, "capture-production", capture);
                await Send(grantee, "production-confirm", "Applied");
                var command = LastCommand(grantee);
                Assert.Equal(world.Owner, command.ActorId);
                Assert.Equal(witness, command.ControlWitness);
                productionCommands.Add(command);
                return command;
            }
        }
        catch (Exception error)
        {
            Write(output, "journey.failed.json", new { Kind = "native-selected-production-v1", Mode = mode, Status = "failed",
                Error = error.ToString(), ExpirySeconds = expirySeconds, ElapsedMilliseconds = elapsed.ElapsedMilliseconds, Stages = stages });
            throw;
        }
        finally
        {
            try { foreach (var peer in all) await peer.DisposeAsync(); }
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
            all.Add(process);
            mailboxActivity.Add(process, elapsed.Elapsed);
            liveMailboxes.Add(process);
            return process;
        }

        async Task Send(NativeRosterProcess process, string verb, string? direction = null, EntityId? target = null)
        {
            // The untouched humans remain real native peers. Observing their mailbox does not
            // select, resume, or submit a choice; each requested operation still runs exactly once.
            await ObserveIdlePeers(process);
            await process.Send(verb, direction, target);
            mailboxActivity[process] = elapsed.Elapsed;
            if (verb == "close") liveMailboxes.Remove(process);
        }

        async Task ObserveIdlePeers(NativeRosterProcess? active = null, bool force = false)
        {
            var due = peers.Values.Where(peer => peer != active && liveMailboxes.Contains(peer)
                && (force || elapsed.Elapsed - mailboxActivity[peer] >= TimeSpan.FromSeconds(10))).ToArray();
            await Task.WhenAll(due.Select(peer => peer.Send("observe")));
            foreach (var peer in due) mailboxActivity[peer] = elapsed.Elapsed;
        }

        void Mark(string stage) => stages.Add(new { Stage = stage, ElapsedMilliseconds = elapsed.ElapsedMilliseconds,
            Peers = peers.Select(pair => new { Home = pair.Key.ToString(), Sequence = pair.Value.Last.GetProperty("Sequence").GetInt32(),
                Selected = Selection(pair.Value).GetProperty("Actor").GetString(),
                Generation = Selection(pair.Value).GetProperty("Generation").GetUInt64(), Commands = pair.Value.Submissions.Count }).ToArray() });
    }
}
