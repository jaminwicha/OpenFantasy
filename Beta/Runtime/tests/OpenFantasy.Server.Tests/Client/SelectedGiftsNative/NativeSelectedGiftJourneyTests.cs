using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Host;
using OpenFantasy.Hosting.Networking;

namespace OpenFantasy.Server.Tests.SelectedGiftsNative;

public sealed partial class NativeSelectedGiftJourneyTests
{
    public static bool Configured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENFANTASY_GODOT"));

    [Fact(SkipUnless = nameof(Configured), Skip = "Requires qualified Godot and retained native player evidence.")]
    public async Task Inventory_only_native_delegate_and_recipient_choose_each_raw_gift_boundary_without_changing_owner_identity()
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(ServerWorld.Cancellation);
        bound.CancelAfter(TimeSpan.FromSeconds(180));
        var elapsed = Stopwatch.StartNew();
        string output = Path.Combine(Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_PLAYER_EVIDENCE")
            ?? throw new InvalidOperationException("Native gift evidence requires OPENFANTASY_NATIVE_PLAYER_EVIDENCE."), "selected-gifts");
        Directory.CreateDirectory(output);
        using var files = new ServerFiles();
        await using var world = await SelectedGiftWorld.Open();
        File.WriteAllBytes(Path.Combine(output, "genesis.ofc"), WorldSnapshotCodec.Encode(world.Genesis));
        File.WriteAllBytes(Path.Combine(output, "prepared.ofc"), WorldSnapshotCodec.Encode(world.Prepared));
        File.WriteAllText(Path.Combine(output, "preparation.txt"), world.Description);
        string certificate = Path.Combine(output, "public.cer");
        File.WriteAllBytes(certificate, world.Certificate.Export(X509ContentType.Cert));
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../client"));
        string background = NativeRosterProcess.BackgroundProject(source, files.DirectoryPath);
        var processes = new List<NativeRosterProcess>();
        var active = new Dictionary<EntityId, NativeRosterProcess>();
        var stages = new List<object>();
        var expected = new List<GiftBoundary>();
        WorldActionCommand? refused = null;
        double expirySeconds = 0;
        try
        {
            foreach (EntityId actor in world.Actors)
                active.Add(actor, Start(actor, "first", actor == world.Grantee || actor == world.Recipient));
            await Task.WhenAll(active.Values.Select(process => process.Opened()));
            foreach (var pair in active)
            {
                Assert.Equal(pair.Key, pair.Value.View.PlayerActorId);
                AssertPaused(pair.Value, pair.Key, pair.Key);
            }
            var owner = active[world.Owner];
            var grantee = active[world.Grantee];
            var recipient = active[world.Recipient];
            await owner.Send("control-offer", "Inventory", world.Grantee);
            await owner.Send("control-close");
            await grantee.Send("control-open");
            Assert.Equal("Offered", Slot(grantee, world.Owner, world.Grantee).GetProperty("Status").GetString());
            await grantee.Send("capture-selection");
            await grantee.Send("control-accept", target: world.Owner);
            Assert.Equal("Inventory", Slot(grantee, world.Owner, world.Grantee).GetProperty("Capabilities").GetString());
            foreach (EntityId actor in world.Actors.Skip(2))
            {
                await active[actor].Send("control-open");
                Assert.Empty(active[actor].Last.GetProperty("ControlBoard").GetProperty("Slots").EnumerateArray());
                await active[actor].Send("control-close");
            }
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
            expirySeconds = allowance.Elapsed.TotalSeconds;
            Assert.True(expirySeconds >= 30);
            await grantee.Send("control-acquire", target: world.Owner);
            var witness = WorldControlWitnessCodecV1.Decode(Convert.FromBase64String(
                grantee.Last.GetProperty("ControlBoard").GetProperty("HeldWitness").GetString()!));
            Assert.Equal(WorldControlCapabilities.Inventory, witness.Capabilities);
            await grantee.Send("control-select", target: world.Owner);
            AssertPaused(grantee, world.Grantee, world.Owner);
            await grantee.Send("control-close");
            await grantee.Send("gift-open");
            Assert.True(grantee.Last.GetProperty("Gifts").GetProperty("InventoryDisclosed").GetBoolean());
            Assert.Equal(world.Owner.ToString(), grantee.Last.GetProperty("Gifts").GetProperty("Actor").GetString());
            Assert.Empty(Selection(grantee).GetProperty("EncounterActors").EnumerateArray());
            Assert.Equal(JsonValueKind.Null, Selection(grantee).GetProperty("Hp").ValueKind);
            await grantee.Send("capture-gifts");
            await grantee.Send("gift-close");
            Mark("inventory-only-selected-paused");

            // Each branch has its own deliberate creation and reply. Decline/withdraw leave the same
            // finite source lot available; acceptance moves its existing identity, never minted stock.
            var declined = await Offer(grantee, world.OwnerLot, world.Recipient);
            await Reply(recipient, declined, "DeclineMaterial", capture: true);
            expected.Add(new(declined, WorldMaterialProposalOutcomeKind.Declined));
            var withdrawn = await Offer(grantee, world.OwnerLot, world.Recipient);
            await Reply(grantee, withdrawn, "WithdrawMaterial");
            expected.Add(new(withdrawn, WorldMaterialProposalOutcomeKind.Withdrawn));
            var accepted = await Offer(grantee, world.OwnerLot, world.Recipient);
            await Reply(recipient, accepted, "AcceptMaterial", capture: true);
            expected.Add(new(accepted, WorldMaterialProposalOutcomeKind.Accepted));

            // The delegate also supplies the operated recipient's own real approval and refusal.
            var returned = await Offer(recipient, world.OwnerLot, world.Owner);
            await Reply(grantee, returned, "AcceptMaterial", capture: true);
            expected.Add(new(returned, WorldMaterialProposalOutcomeKind.Accepted));
            var reverseDeclined = await Offer(recipient, world.RecipientLot, world.Owner);
            await Reply(grantee, reverseDeclined, "DeclineMaterial");
            expected.Add(new(reverseDeclined, WorldMaterialProposalOutcomeKind.Declined));
            Mark("both-native-participants-decided");

            var oldGrant = await Offer(grantee, world.OwnerLot, world.Recipient);
            foreach (var process in active.Values) await process.Send("pause");
            var returning = Start(world.Owner, "return", rendered: false);
            active.Add(world.Owner, returning);
            await returning.Opened();
            await Until(() => Selection(grantee).GetProperty("Actor").GetString() == world.Grantee.ToString(),
                "Original owner return did not force the delegate home.", active.Values, bound.Token);
            AssertPaused(grantee, world.Grantee, world.Grantee);
            await returning.Send("control-revoke", target: world.Grantee);
            await returning.Send("control-close");
            await grantee.Send("control-open");
            Assert.Equal("Revoked", Slot(grantee, world.Owner, world.Grantee).GetProperty("Status").GetString());
            Assert.Equal(JsonValueKind.Null, grantee.Last.GetProperty("ControlBoard").GetProperty("HeldWitness").ValueKind);
            await grantee.Send("control-close");
            await Reply(recipient, oldGrant, "AcceptMaterial", expectedReceipt: "Unavailable");
            refused = LastCommand(recipient);
            await Until(() => returning.Last.GetProperty("ControlState").GetString() != "Pending",
                "The original owner did not reach a safe restoration boundary.", active.Values, bound.Token);
            await Reply(returning, oldGrant, "WithdrawMaterial");
            expected.Add(new(oldGrant, WorldMaterialProposalOutcomeKind.Withdrawn));
            Mark("old-grant-refused-owner-withdrew");
            foreach (var process in active.Values) await process.Send("pause");
            foreach (var process in active.Values) await process.Send("close");
            Assert.False(world.Host.Session.Completion.IsCompleted);
            await world.Stop();
            Verify(output, world, processes, witness, expected, refused);
            bound.Token.ThrowIfCancellationRequested();
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(180), "The native gift journey exceeded its absolute 180-second bound.");
            Write(output, "journey.json", new { Kind = "native-selected-gifts-v1", Status = "passed",
                ExpirySeconds = expirySeconds, ElapsedMilliseconds = elapsed.ElapsedMilliseconds, Stages = stages,
                Preparation = "Original-genesis ordinary journaled travel only; no stock or body changes and no native travel claim." });
        }
        catch (Exception error)
        {
            Write(output, "journey.failed.json", new { Kind = "native-selected-gifts-v1", Status = "failed",
                Error = error.ToString(), ExpirySeconds = expirySeconds, ElapsedMilliseconds = elapsed.ElapsedMilliseconds, Stages = stages });
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

        async Task<WorldActionCommand> Offer(NativeRosterProcess process, EntityId lot, EntityId recipient)
        {
            await process.Send("gift-close");
            await process.Send("resume");
            await process.Send("gift-open");
            await process.Send("gift-lot", "CountedSource", lot);
            await process.Send("gift-recipient", target: recipient);
            await process.Send("gift-quantity", "1");
            await process.Send("gift-prepare");
            await process.Send("gift-review", "ProposeMaterial");
            await process.Send("gift-confirm", "Applied");
            var command = LastCommand(process);
            var intent = Assert.IsType<ProposeMaterialIntent>(command.Intent);
            Assert.Equal(lot, intent.Terms.LotId);
            Assert.Equal(recipient, intent.Terms.RecipientActorId);
            Assert.Equal(1UL, intent.Terms.Quantity);
            await process.Send("gift-close");
            return command;
        }

        async Task Reply(NativeRosterProcess process, WorldActionCommand proposal, string action,
            bool capture = false, string expectedReceipt = "Applied")
        {
            EntityId identity = WorldMaterialProposal.DeriveIdentity(proposal.CommandId);
            await Until(() => HasOpenOffer(process, identity), "The exact native gift offer was not published to its participant.",
                [process], bound.Token);
            await process.Send("gift-close");
            await process.Send("resume");
            await process.Send("gift-open");
            await process.Send("gift-offer", target: identity);
            if (capture) await process.Send("capture-gifts");
            await process.Send("gift-review", action);
            await process.Send("gift-confirm", expectedReceipt);
            await process.Send("gift-close");
        }

        void Mark(string stage) => stages.Add(new { Stage = stage, ElapsedMilliseconds = elapsed.ElapsedMilliseconds,
            Peers = active.Select(pair => new { Home = pair.Key.ToString(), ResponseSequence = pair.Value.Last.GetProperty("Sequence").GetInt32(),
                Actor = Selection(pair.Value).GetProperty("Actor").GetString(),
                Generation = Selection(pair.Value).GetProperty("Generation").GetUInt64(),
                Commands = pair.Value.Submissions.Count }).ToArray() });
    }
}
