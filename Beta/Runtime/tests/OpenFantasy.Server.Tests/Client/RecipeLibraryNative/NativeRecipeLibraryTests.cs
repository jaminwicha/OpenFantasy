using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Host;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Server.Tests.SelectedProductionNative;

namespace OpenFantasy.Server.Tests.RecipeLibraryNative;

public sealed partial class NativeRecipeLibraryTests
{
    public static bool Configured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENFANTASY_GODOT"));

    [Fact(SkipUnless = nameof(Configured), Skip = "Requires qualified Godot and retained native evidence.")]
    public async Task Native_library_crafts_both_recipes_from_real_regrowth_without_private_or_healing_scope()
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(ServerWorld.Cancellation);
        bound.CancelAfter(TimeSpan.FromSeconds(180));
        var elapsed = Stopwatch.StartNew();
        string output = Path.Combine(Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_PLAYER_EVIDENCE")
            ?? throw new InvalidOperationException("Native recipe evidence must be retained."), "recipe-library");
        Directory.CreateDirectory(output);
        using var files = new ServerFiles();
        await using var world = await SelectedProductionWorld.Open(excavation: false, output);
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
        EntityId potion = world.Genesis.Production!.Recipes.Single(recipe => recipe.PhysicalRecipe.Key == "craft_potion").DefinitionId;
        WorldControlCapabilities scopes = WorldControlCapabilities.Production | WorldControlCapabilities.Traversal;
        double allowanceSeconds = 0;
        double regrowthSeconds = 0;
        try
        {
            foreach (EntityId actor in world.Actors) peers.Add(actor, Start(actor, "first", actor == world.Grantee));
            await Task.WhenAll(peers.Values.Select(peer => peer.Opened()));
            await Idle(force: true);
            var owner = peers[world.Owner];
            var grantee = peers[world.Grantee];
            await Send(owner, "control-offer", scopes.ToString(), world.Grantee);
            await Send(owner, "control-close");
            await Send(grantee, "control-open");
            await Send(grantee, "control-accept", target: world.Owner);
            var allowance = Stopwatch.StartNew();
            await Send(owner, "close");
            peers.Remove(world.Owner);
            while (true)
            {
                await Send(grantee, "control-refresh");
                bound.Token.ThrowIfCancellationRequested();
                Assert.True(allowance.Elapsed < TimeSpan.FromSeconds(35));
                Assert.Equal(world.Prepared.Calendar, grantee.View.Calendar);
                if (Slot(grantee, world.Owner, world.Grantee).GetProperty("AcquisitionAvailable").GetBoolean()) break;
                await Task.Delay(500, bound.Token);
            }
            allowanceSeconds = allowance.Elapsed.TotalSeconds;
            Assert.True(allowanceSeconds >= 30);
            await Send(grantee, "control-acquire", target: world.Owner);
            var witness = WorldControlWitnessCodecV1.Decode(Convert.FromBase64String(
                grantee.Last.GetProperty("ControlBoard").GetProperty("HeldWitness").GetString()!));
            Assert.Equal(scopes, witness.Capabilities);
            await Send(grantee, "control-select", target: world.Owner);
            await Send(grantee, "control-close");
            await Send(grantee, "recipe-open");
            foreach (EntityId recipe in new[] { world.Recipe, potion })
            {
                await Send(grantee, "recipe-select", target: recipe);
                await Send(grantee, "capture-recipes", recipe == potion ? "paused-potion" : "paused-rations");
                Assert.Equal(recipe.ToString(), Recipes(grantee).GetProperty("Selected").GetString());
                Assert.True(grantee.Last.GetProperty("LocallyPaused").GetBoolean());
                Assert.Empty(grantee.Submissions);
                Assert.Equal(world.Prepared.Calendar, grantee.View.Calendar);
                AssertPrivateWithheld(grantee);
            }
            Mark("paused-two-recipes-no-command");
            await Send(grantee, "recipe-cancel");
            await Send(grantee, "resume");
            for (int harvest = 0; harvest < 4; harvest++) await Gather();

            // Biological regrowth has its own authored700-step schedule, not a relaxed action timeout.
            // Keep all genuine peers serviced while ordinary ready world time earns that regrowth.
            var regrowth = Stopwatch.StartNew();
            while (true)
            {
                await Send(grantee, "observe");
                bound.Token.ThrowIfCancellationRequested();
                Assert.True(regrowth.Elapsed < TimeSpan.FromSeconds(45), "The ordinary700-step berry regrowth exceeded45seconds.");
                if (grantee.Last.GetProperty("Production").GetProperty("Nodes").EnumerateArray().Any(node =>
                    node.GetProperty("Id").GetString() == world.BerryNode.ToString()
                    && node.GetProperty("RemainingSwings").GetUInt32() == 4 && node.GetProperty("Visibility").GetString() == "Ready")) break;
                await Task.Delay(250, bound.Token);
            }
            regrowthSeconds = regrowth.Elapsed.TotalSeconds;
            Mark("ordinary-regrowth-observed");
            await Gather();
            await Craft(potion, "prepared-potion");
            await Craft(world.Recipe, "prepared-rations");
            Assert.Equal(7, grantee.Submissions.Count);
            AssertPrivateWithheld(grantee);
            await Gather();
            await Gather();
            await Send(grantee, "recipe-open");
            await Send(grantee, "recipe-select", target: world.Recipe);
            await Send(grantee, "recipe-prepare");
            await Send(grantee, "capture-recipes", "unconsumed-preparation");
            Assert.Equal(JsonValueKind.Object, Recipes(grantee).GetProperty("Prepared").ValueKind);
            Mark("two-batches-and-unconsumed-capture");
            await Idle(force: true);
            var returned = Start(world.Owner, "return", rendered: false);
            peers.Add(world.Owner, returned);
            await returned.Opened();
            await Await(() => Selection(grantee).GetProperty("Actor").GetString() == world.Grantee.ToString(),
                "Owner return did not invalidate the operated selection.", grantee);
            Assert.True(grantee.Last.GetProperty("LocallyPaused").GetBoolean());
            Assert.False(Recipes(grantee).GetProperty("Open").GetBoolean());
            Assert.Equal(JsonValueKind.Null, Recipes(grantee).GetProperty("Prepared").ValueKind);
            await Send(returned, "control-revoke", target: world.Grantee);
            await Send(returned, "control-close");
            int beforeBlocked = grantee.Submissions.Count;
            await Send(grantee, "recipe-open");
            await Send(grantee, "recipe-select", target: potion);
            await Send(grantee, "capture-recipes", "home-after-return");
            Assert.Equal(world.Grantee.ToString(), Recipes(grantee).GetProperty("Actor").GetString());
            Assert.Equal(JsonValueKind.Null, Recipes(grantee).GetProperty("Prepared").ValueKind);
            await Send(grantee, "recipe-confirm", "blocked");
            Assert.Equal(beforeBlocked, grantee.Submissions.Count);
            await Send(grantee, "recipe-cancel");
            await Send(grantee, "control-open");
            Assert.Equal("Revoked", Slot(grantee, world.Owner, world.Grantee).GetProperty("Status").GetString());
            await Send(grantee, "control-close");
            Mark("return-revoke-invalidates-unconsumed-batch");
            foreach (var peer in peers.Values) await Send(peer, "close");
            Assert.False(world.Host.Session.Completion.IsCompleted);
            await world.Stop();
            Verify(output, world, all, witness, potion);
            bound.Token.ThrowIfCancellationRequested();
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(180));
            Write(output, "journey.json", new { Status = "passed", AllowanceSeconds = allowanceSeconds,
                RegrowthSeconds = regrowthSeconds, ElapsedMilliseconds = elapsed.ElapsedMilliseconds, Stages = stages });

            async Task Gather()
            {
                await Send(grantee, "production-target", target: world.BerryNode);
                await Send(grantee, "production-review", "Gather", world.BerryNode);
                await Send(grantee, "production-confirm", "Applied");
                AssertPrivateWithheld(grantee);
            }

            async Task Craft(EntityId recipe, string capture)
            {
                await Send(grantee, "recipe-open");
                await Send(grantee, "recipe-select", target: recipe);
                await Send(grantee, "recipe-prepare");
                Assert.Equal(recipe.ToString(), Recipes(grantee).GetProperty("Prepared").GetProperty("Intent").GetProperty("Recipe").GetString());
                await Send(grantee, "capture-recipes", capture);
                await Send(grantee, "recipe-confirm", "Applied");
                await Send(grantee, "recipe-close");
            }
        }
        catch (Exception error)
        {
            Write(output, "journey.failed.json", new { Error = error.ToString(), AllowanceSeconds = allowanceSeconds,
                RegrowthSeconds = regrowthSeconds, ElapsedMilliseconds = elapsed.ElapsedMilliseconds, Stages = stages });
            throw;
        }
        finally
        {
            try { foreach (var peer in all) await peer.DisposeAsync(); }
            finally { await world.Stop(); Preserve(output, world); }
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
    private static JsonElement Recipes(NativeRosterProcess peer) => peer.Last.GetProperty("Recipes");
    private static JsonElement Slot(NativeRosterProcess peer, EntityId owner, EntityId grantee) =>
        Assert.Single(peer.Last.GetProperty("ControlBoard").GetProperty("Slots").EnumerateArray(), slot =>
            slot.GetProperty("Owner").GetString() == owner.ToString() && slot.GetProperty("Grantee").GetString() == grantee.ToString());

    private static void AssertPrivateWithheld(NativeRosterProcess peer)
    {
        Assert.True(Recipes(peer).GetProperty("ProductionDisclosed").GetBoolean());
        Assert.False(Recipes(peer).GetProperty("InventoryDisclosed").GetBoolean());
        Assert.False(Recipes(peer).GetProperty("RecoveryDisclosed").GetBoolean());
        Assert.False(Recipes(peer).GetProperty("CombatDisclosed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, Selection(peer).GetProperty("Hp").ValueKind);
        Assert.False(peer.Last.GetProperty("Gifts").GetProperty("InventoryDisclosed").GetBoolean());
    }

    private static void Write(string output, string name, object value) => File.WriteAllText(Path.Combine(output, name),
        JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
}
