using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Host;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests.HumanConstructionNative;

public sealed partial class NativeHumanConstructionTests
{
    public static bool Configured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENFANTASY_GODOT"));

    [Fact(SkipUnless = nameof(Configured), Skip = "Requires qualified Godot and retained native evidence.")]
    public async Task Native_human_builder_and_consented_operator_earn_all_two_hundred_work_steps_and_complete_funded_guard()
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(ServerWorld.Cancellation);
        bound.CancelAfter(TimeSpan.FromSeconds(210));
        var elapsed = Stopwatch.StartNew();
        string output = Path.Combine(Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_PLAYER_EVIDENCE")
            ?? throw new InvalidOperationException("Native construction evidence must be retained."), "human-construction");
        Directory.CreateDirectory(output);
        using var files = new ServerFiles();
        await using var world = await HumanConstructionWorld.Open(output);
        File.WriteAllBytes(Path.Combine(output, "prepared.ofc"), WorldSnapshotCodec.Encode(world.Prepared));
        string certificate = Path.Combine(output, "public.cer");
        File.WriteAllBytes(certificate, world.Certificate.Export(X509ContentType.Cert));
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../client"));
        string background = NativeRosterProcess.BackgroundProject(source, files.DirectoryPath);
        var all = new List<NativeRosterProcess>();
        var peers = new Dictionary<EntityId, NativeRosterProcess>();
        var activity = new Dictionary<NativeRosterProcess, TimeSpan>();
        var live = new HashSet<NativeRosterProcess>();
        var stages = new List<object>();
        double allowanceSeconds = 0;
        try
        {
            await StartRoster("funding");
            var supplier = peers[world.Supplier];
            var owner = peers[world.Owner];
            await Send(supplier, "resume");
            foreach (string operation in new[] { "speak", "accept", "deliver", "close" })
                await Send(supplier, "construction-supplier", operation);
            await Send(supplier, "pause");
            Assert.Equal(4, supplier.Submissions.Count);
            await Send(owner, "resume");
            await Send(owner, "construction-target", target: world.Project);
            await Send(owner, "construction-batch", "1", world.Project);
            await Send(owner, "pause");
            Mark("native-funded-and-first-work");
            foreach (var peer in peers.Values) await Send(peer, "close");
            await world.Stop();
            byte[] saved;
            using (var persisted = SqliteWorldAuthority.OpenExisting(world.Database, WorldSnapshotCodec.Encode(world.Genesis)))
            {
                var partial = persisted.ReadStateForHost();
                Assert.Equal(1UL, Assert.Single(partial.Construction!.Projects).Progress.CompletedUnits);
                Assert.Empty(partial.Production!.Batches);
                Assert.Empty(partial.Space.Structures);
                Assert.Equal(6UL, partial.Inventory.Custody.Lots.Where(lot => lot.ReservedBy == Assert.Single(partial.Construction.Projects).ReservationId)
                    .Aggregate(0UL, (sum, lot) => sum + lot.Quantity));
                saved = WorldSnapshotCodec.Encode(partial);
                persisted.BackupForHost(Path.Combine(output, "partial.sqlite3"));
            }
            File.WriteAllBytes(Path.Combine(output, "partial.ofc"), saved);
            using (var reopened = SqliteWorldAuthority.OpenExisting(world.Database, WorldSnapshotCodec.Encode(world.Genesis)))
                Assert.Equal(saved, WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
            peers.Clear();
            await world.Start();
            await StartRoster("continuation");
            owner = peers[world.Owner];
            var grantee = peers[world.Grantee];
            await Send(owner, "control-offer", WorldControlCapabilities.Production.ToString(), world.Grantee);
            await Send(owner, "control-close");
            await Send(grantee, "control-open");
            await Send(grantee, "control-accept", target: world.Owner);
            var allowance = Stopwatch.StartNew();
            await Send(owner, "close");
            peers.Remove(world.Owner);
            var frozenCalendar = grantee.View.Calendar;
            while (true)
            {
                await Send(grantee, "control-refresh");
                bound.Token.ThrowIfCancellationRequested();
                Assert.True(allowance.Elapsed < TimeSpan.FromSeconds(35));
                Assert.Equal(frozenCalendar, grantee.View.Calendar);
                if (Slot(grantee, world.Owner, world.Grantee).GetProperty("AcquisitionAvailable").GetBoolean()) break;
                await Task.Delay(500, bound.Token);
            }
            allowanceSeconds = allowance.Elapsed.TotalSeconds;
            Assert.True(allowanceSeconds >= 30);
            await Send(grantee, "control-acquire", target: world.Owner);
            var witness = WorldControlWitnessCodecV1.Decode(Convert.FromBase64String(grantee.Last.GetProperty("ControlBoard")
                .GetProperty("HeldWitness").GetString()!));
            Assert.Equal(WorldControlCapabilities.Production, witness.Capabilities);
            await Send(grantee, "control-select", target: world.Owner);
            await Send(grantee, "control-close");
            await Send(grantee, "resume");
            await Send(grantee, "construction-target", target: world.Project);
            await Send(grantee, "construction-review", target: world.Project);
            await Send(grantee, "capture-construction", "delegated-first-work", world.Project);
            await Send(grantee, "construction-confirm", "Applied");
            for (int remaining = 197; remaining > 0;)
            {
                int count = Math.Min(20, remaining);
                await Send(grantee, "construction-batch", count.ToString(System.Globalization.CultureInfo.InvariantCulture), world.Project);
                remaining -= count;
            }
            Assert.Equal(198, grantee.Submissions.Count);
            Assert.False(grantee.Last.GetProperty("Construction").GetProperty("InventoryDisclosed").GetBoolean());
            Assert.False(grantee.Last.GetProperty("Construction").GetProperty("RecoveryDisclosed").GetBoolean());
            await Send(grantee, "construction-review", target: world.Project);
            await Send(grantee, "capture-construction", "final-unconsumed-capture", world.Project);
            Mark("native-work-199-before-owner-return");
            await Idle(force: true);
            var returned = Start(world.Owner, "return", rendered: false);
            peers.Add(world.Owner, returned);
            await returned.Opened();
            await Await(() => Selection(grantee).GetProperty("Actor").GetString() == world.Grantee.ToString(),
                "Owner return did not invalidate the selected construction capture.", grantee);
            await Send(returned, "control-revoke", target: world.Grantee);
            await Send(returned, "control-close");
            await Send(grantee, "construction-confirm", "blocked");
            Assert.Equal(198, grantee.Submissions.Count);
            await Send(returned, "resume");
            await Send(returned, "construction-target", target: world.Project);
            await Send(returned, "construction-batch", "1", world.Project);
            await Send(returned, "pause");
            var observer = peers[world.Observer];
            await Await(() => observer.Last.GetProperty("Construction").GetProperty("Structures").GetArrayLength() == 1,
                "Public completed geometry did not reach the ordinary home observer.", observer);
            Assert.Equal(JsonValueKind.Null, Selection(observer).GetProperty("Witness").ValueKind);
            var completion = WorldActionCodec.Decode(Convert.FromHexString(returned.Submissions[^1].GetProperty("Command").GetString()!));
            // View is the retained legacy diagnostic encoding; this facet comes from the
            // client's actual selected observation and includes current public geometry.
            var construction = observer.Last.GetProperty("Construction");
            var installed = Assert.Single(construction.GetProperty("Structures").EnumerateArray());
            var project = Assert.Single(world.Genesis.Construction!.Projects);
            Assert.Equal(EntityId.DeriveSpawned(completion.CommandId, 3).ToString(), installed.GetProperty("Id").GetString());
            Assert.Equal(JsonSerializer.Serialize(project.SiteFeet), JsonSerializer.Serialize(installed.GetProperty("Origin")));
            var expectedMembers = project.Definition.Members.Select((geometry, index) => new
            {
                MemberIndex = checked((uint)index), Origin = project.SiteFeet,
                Geometry = geometry, Collision = geometry.At(project.SiteFeet)
            }).ToArray();
            Assert.Equal(JsonSerializer.Serialize(expectedMembers), JsonSerializer.Serialize(installed.GetProperty("Members")));
            Assert.Empty(construction.GetProperty("Work").EnumerateArray());
            Assert.Empty(returned.Last.GetProperty("Construction").GetProperty("Work").EnumerateArray());
            await Send(observer, "capture-construction", "public-completed-home-observer", world.Project);
            Mark("all200-native-work-and-public-completion");
            foreach (var peer in peers.Values) await Send(peer, "close");
            await world.Stop();
            Verify(output, world, all, witness);
            bound.Token.ThrowIfCancellationRequested();
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(210));
            Write(output, "journey.json", new { Status = "passed", AllowanceSeconds = allowanceSeconds,
                ElapsedMilliseconds = elapsed.ElapsedMilliseconds, Stages = stages,
                Scope = "All 200 work commands are native; 198 delegated; completion by returning owner. Public observer is home; selected Traversal-only visibility is a separate managed proof." });
        }
        catch (Exception error)
        {
            Write(output, "journey.failed.json", new { Error = error.ToString(), AllowanceSeconds = allowanceSeconds,
                ElapsedMilliseconds = elapsed.ElapsedMilliseconds, Stages = stages });
            throw;
        }
        finally
        {
            try { foreach (var peer in all) await peer.DisposeAsync(); }
            finally
            {
                await world.Stop();
                if (!File.Exists(Path.Combine(output, "world.sqlite3")))
                {
                    using var saved = SqliteWorldAuthority.OpenExisting(world.Database, WorldSnapshotCodec.Encode(world.Genesis));
                    saved.BackupForHost(Path.Combine(output, "world.sqlite3"));
                    File.WriteAllBytes(Path.Combine(output, "final.ofc"), WorldSnapshotCodec.Encode(saved.ReadStateForHost()));
                }
            }
        }

        async Task StartRoster(string suffix)
        {
            foreach (EntityId actor in world.Actors) peers.Add(actor, Start(actor, suffix, actor == world.Grantee || actor == world.Observer));
            await Task.WhenAll(peers.Values.Select(peer => peer.Opened()));
            await Idle(force: true);
        }
        NativeRosterProcess Start(EntityId actor, string suffix, bool rendered)
        {
            var invitation = new PlayerConnectionInvitation(world.Server.Endpoint, world.Host.Admission.Compatibility,
                actor, world.Host.Issue(actor, TimeSpan.FromMinutes(4)).Export());
            string path = Path.Combine(files.DirectoryPath, actor + "-" + suffix + ".invitation.json");
            PlayerInvitationFile.Create(path, invitation);
            var process = new NativeRosterProcess(Path.Combine(output, actor + "-" + suffix), path, certificate,
                rendered ? background : source, rendered, bound.Token);
            all.Add(process); activity.Add(process, elapsed.Elapsed); live.Add(process);
            return process;
        }
        async Task Send(NativeRosterProcess peer, string verb, string? direction = null, EntityId? target = null)
        {
            await Idle(peer);
            await peer.Send(verb, direction, target);
            activity[peer] = elapsed.Elapsed;
            if (verb == "close") live.Remove(peer);
        }
        async Task Idle(NativeRosterProcess? active = null, bool force = false)
        {
            foreach (var peer in peers.Values.Where(peer => peer != active && live.Contains(peer)
                && (force || elapsed.Elapsed - activity[peer] >= TimeSpan.FromSeconds(10))).ToArray())
            {
                await peer.Send("observe");
                activity[peer] = elapsed.Elapsed;
            }
        }
        async Task Await(Func<bool> condition, string message, NativeRosterProcess peer)
        {
            var waiting = Stopwatch.StartNew();
            while (true)
            {
                await Send(peer, "observe");
                bound.Token.ThrowIfCancellationRequested();
                Assert.True(waiting.Elapsed < TimeSpan.FromSeconds(5), message);
                if (condition()) return;
                await Task.Delay(25, bound.Token);
            }
        }
        void Mark(string name) => stages.Add(new { Name = name, ElapsedMilliseconds = elapsed.ElapsedMilliseconds,
            Peers = peers.Select(pair => new { Actor = pair.Key.ToString(), Sequence = pair.Value.Last.GetProperty("Sequence").GetInt32(),
                Commands = pair.Value.Submissions.Count }).ToArray() });
    }

    private static JsonElement Selection(NativeRosterProcess peer) => peer.Last.GetProperty("Selection");
    private static JsonElement Slot(NativeRosterProcess peer, EntityId owner, EntityId grantee) =>
        Assert.Single(peer.Last.GetProperty("ControlBoard").GetProperty("Slots").EnumerateArray(), slot =>
            slot.GetProperty("Owner").GetString() == owner.ToString() && slot.GetProperty("Grantee").GetString() == grantee.ToString());
    private static void Write(string output, string name, object value) => File.WriteAllText(Path.Combine(output, name),
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
}
