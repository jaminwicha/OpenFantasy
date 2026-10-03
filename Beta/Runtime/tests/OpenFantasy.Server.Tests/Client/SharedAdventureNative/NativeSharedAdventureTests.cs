using System.Diagnostics;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Host;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests;

public sealed class NativeSharedAdventureTests
{
    public static bool Configured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENFANTASY_GODOT"));

    [Fact(SkipUnless = nameof(Configured), Skip = "Requires Godot and a retained native evidence directory.")]
    public async Task Two_adventurers_gather_share_craft_and_fund_Wrens_work_in_the_same_fresh_world()
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(ServerWorld.Cancellation);
        bound.CancelAfter(TimeSpan.FromSeconds(240));
        var watch = Stopwatch.StartNew();
        string output = Path.Combine(Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_PLAYER_EVIDENCE")
            ?? throw new InvalidOperationException("Retained native evidence is required."), "shared-adventure");
        Directory.CreateDirectory(output);
        string database = Path.Combine(output, "world.sqlite3");
        WorldSnapshot genesis = AdventureWorldAdmission.CreateEmbedded(AdventureWorldAdmission.SeedText, ["Rowan", "Mira"]);
        using (var authority = SqliteWorldAuthority.CreateNew(database, genesis)) { }
        File.WriteAllBytes(Path.Combine(output, "genesis.ofc"), WorldSnapshotCodec.Encode(genesis));
        EntityId lead = genesis.Excavation!.Sites.Single().OwnerActorId;
        Assert.Equal(ResidentControllerKind.Player, genesis.GetResident(lead).ControllerKind);
        EntityId helper = genesis.Residents.Single(actor => actor.ControllerKind == ResidentControllerKind.Player && actor.ActorId != lead).ActorId;
        EntityId wren = genesis.Construction!.Projects.Single().BuilderActorId;
        var berry = genesis.Gathering!.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Berry);
        var oak = genesis.Gathering.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Oak);
        EntityId wood = oak.Definition.YieldDefinition.DefinitionId;
        using var certificate = ServerWorld.CreateCertificate();
        string publicCertificate = Path.Combine(output, "public.cer");
        File.WriteAllBytes(publicCertificate, certificate.Export(X509ContentType.Cert));
        using var files = new ServerFiles();
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../client"));
        string background = NativeRosterProcess.BackgroundProject(source, files.DirectoryPath);
        var all = new List<NativeRosterProcess>();
        var peers = new Dictionary<EntityId, NativeRosterProcess>();
        var stages = new List<object>();
        string stage = "opening";
        try
        {
            await using (var host = await CargoPlayerHost.OpenExistingAsync(database, bound.Token))
            await using (var server = await PlayerWebSocketServer.StartAsync(host.Admission,
                new(IPAddress.Loopback, 0, certificate), bound.Token))
            {
                foreach (EntityId actor in new[] { lead, helper })
                {
                    var invitation = new PlayerConnectionInvitation(server.Endpoint, host.Admission.Compatibility, actor,
                        host.Issue(actor, TimeSpan.FromMinutes(5)).Export());
                    string invitationPath = Path.Combine(files.DirectoryPath, actor + ".invitation.json");
                    PlayerInvitationFile.Create(invitationPath, invitation);
                    var peer = new NativeRosterProcess(Path.Combine(output, actor.ToString()), invitationPath,
                        publicCertificate, actor == lead ? background : source, actor == lead, bound.Token);
                    all.Add(peer);
                    peers.Add(actor, peer);
                }
                await Task.WhenAll(peers.Values.Select(peer => peer.Opened()));
                Assert.All(peers.Values, peer => Assert.Equal(genesis.Calendar, peer.View.Calendar));
                Mark("unplayed-two-person-opening");

                stage = "forage";
                await WalkNear(lead, berry.Feet);
                await Send(lead, "home-target", target: berry.NodeId);
                await Send(lead, "home-action", "Gather");
                await Send(lead, "home-action", "Gather");
                await Send(lead, "pause");

                stage = "helper-wood";
                await WalkNear(helper, oak.Feet);
                await Send(helper, "home-target", target: oak.NodeId);
                await Send(helper, "home-action", "Gather");
                await Send(helper, "pause");
                var lot = peers[helper].View.PlayerInventory.Lots.Single(value => value.DefinitionId == wood);
                ulong helperWood = lot.Quantity;
                Assert.True(helperWood >= 1);
                await WalkNear(helper, Feet(lead));
                Mark("independent-earned-resources");

                stage = "shared-supplies";
                await Send(helper, "resume");
                await Send(helper, "gift-open");
                await Send(helper, "gift-lot", "CountedSource", lot.LotId);
                await Send(helper, "gift-recipient", target: lead);
                await Send(helper, "gift-quantity", "1");
                await Send(helper, "gift-prepare");
                await Send(helper, "gift-review", "ProposeMaterial");
                await Send(helper, "gift-confirm", "Applied");
                WorldActionCommand proposal = Last(peers[helper]);
                Assert.Equal(helper, proposal.ActorId);
                Assert.Equal(lead, Assert.IsType<ProposeMaterialIntent>(proposal.Intent).Terms.RecipientActorId);
                await Send(helper, "gift-close");
                await Send(lead, "resume");
                await Send(lead, "gift-open");
                await Send(lead, "gift-offer", target: WorldMaterialProposal.DeriveIdentity(proposal.CommandId));
                await Send(lead, "capture-gifts");
                await Send(lead, "gift-review", "AcceptMaterial");
                await Send(lead, "gift-confirm", "Applied");
                await Send(lead, "gift-close");
                Assert.Equal(helperWood - 1, peers[helper].View.PlayerInventory.Lots.Where(value => value.DefinitionId == wood)
                    .Aggregate(0UL, (sum, value) => sum + value.Quantity));
                Mark("two-independent-consents-transferred-one-earned-wood");

                stage = "craft-and-fund";
                await Send(lead, "home-action", "Craft");
                await WalkNear(lead, Feet(wren));
                await Send(lead, "home-target", target: wren);
                foreach (string action in new[] { "Speak", "Give", "Give", "CloseDialogue" })
                    await Send(lead, "home-action", action);
                Assert.Equal(200UL, peers[lead].View.Progression!.CurrencyBalance);
                Assert.Equal(0UL, peers[helper].View.Progression!.CurrencyBalance);
                var completion = Stopwatch.StartNew();
                while (!peers.Values.All(peer => peer.View.Construction.Any(project => project.IsCompleted)))
                {
                    bound.Token.ThrowIfCancellationRequested();
                    Assert.True(completion.Elapsed < TimeSpan.FromSeconds(30), "Wren's funded autonomous work did not complete.");
                    await Observe();
                    await Task.Delay(50, bound.Token);
                }
                await Send(lead, "capture");
                Mark("earned-crafting-quest-and-shared-autonomous-construction");
                foreach (EntityId actor in peers.Keys.ToArray())
                {
                    await peers[actor].Send("close");
                    peers.Remove(actor);
                }
            }
            using var saved = SqliteWorldAuthority.OpenExisting(database, WorldSnapshotCodec.Encode(genesis));
            WorldSnapshot final = saved.ReadStateForHost();
            Assert.Equal(genesis.AdmissionVersion, final.AdmissionVersion);
            Assert.Equal(genesis.Inventory.Controls, final.Inventory.Controls);
            Assert.Equal(ResidentControllerKind.Host, final.GetResident(wren).ControllerKind);
            Assert.True(final.Construction!.Projects.Single().IsCompleted);
            var submissions = all.SelectMany(peer => peer.Submissions).ToArray();
            var commands = submissions.Select(row => WorldActionCodec.Decode(Convert.FromHexString(row.GetProperty("Command").GetString()!))).ToArray();
            Assert.Equal(commands.Length, commands.Select(command => command.CommandId).Distinct().Count());
            Assert.All(commands, command => Assert.Null(command.ControlWitness));
            Assert.Equal(3, commands.Count(command => command.Intent is GatherIntent));
            Assert.Single(commands, command => command.Intent is ProposeMaterialIntent);
            Assert.Contains(commands, command => command.ActorId == helper && command.Intent is GatherIntent);
            foreach (var row in submissions)
            {
                var command = WorldActionCodec.Decode(Convert.FromHexString(row.GetProperty("Command").GetString()!));
                var originalControl = genesis.Inventory.Controls.Single(control => control.ActorId == command.ActorId);
                Assert.Equal((originalControl.PrincipalId, originalControl.SessionId), (command.PrincipalId, command.OriginSessionId));
                var receipt = saved.FindReceipt(command, new TrustedCommandDelivery(command.PrincipalId, command.OriginSessionId));
                Assert.NotNull(receipt);
                Assert.Equal(CommandReceiptStatus.Applied, receipt.Status);
                Assert.False(row.GetProperty("IsRetry").GetBoolean());
                Assert.Equal(Convert.FromHexString(row.GetProperty("Receipt").GetString()!), WorldReceiptCodecV1.Encode(receipt));
            }
            Assert.Equal(final.ContentHash, saved.ReadRecordedStateForHost(final.Revision).State.ContentHash);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(240), "Shared opening exceeded its absolute bound.");
            Write("journey.json", new { Status = "passed", ElapsedMilliseconds = watch.ElapsedMilliseconds,
                Commands = commands.Length, final.Revision, Hash = final.ContentHash.ToString(), Stages = stages,
                Limitations = "Two native clients; lead rendered, helper headless. Injected input, no physical device/human acceptance. No party retreat or expedition proof." });
        }
        catch (Exception error)
        {
            Write("journey.failed.json", new { Status = "failed", Stage = stage, Error = error.ToString(), Stages = stages,
                ElapsedMilliseconds = watch.ElapsedMilliseconds });
            throw;
        }
        finally { foreach (var peer in all) await peer.DisposeAsync(); }

        async Task Observe() => await Task.WhenAll(peers.Values.Select(peer => peer.Send("observe")));
        async Task Send(EntityId actor, string verb, string? direction = null, EntityId? target = null)
        {
            await peers[actor].Send(verb, direction, target);
            await Observe();
        }
        SubunitPosition Feet(EntityId actor) => peers[lead].View.Actors.Single(value => value.ActorId == actor).Feet;
        LocalSpatialSnapshot Space() => genesis.Space.WithActors(genesis.Space.Actors.Select(original =>
        {
            var row = peers[lead].View.Actors.Single(actor => actor.ActorId == original.ActorId);
            return new ActorPresence(original.Binding, original.Condition, original.Scope, row.Feet, row.PresenceRevision, original.HomePlaceId);
        }));
        async Task WalkNear(EntityId actor, SubunitPosition target)
        {
            var initial = Space();
            if (ResourceInteractionQueries.Query(initial, actor, target) == SpatialInteraction.Reachable) return;
            var candidates = (from x in new[] { -256L, 0, 256L } from y in new[] { -256L, 0, 256L }
                let goal = target.Offset(x, y, 0) let route = initial.QueryGroundRoute(actor, goal)
                where route.Status == GroundRouteStatus.Reachable && route.NextWaypoint is not null
                orderby route.Waypoints.Count, goal.X, goal.Y select goal).ToArray();
            Assert.NotEmpty(candidates);
            SubunitPosition destination = candidates[0];
            for (int steps = 0; ResourceInteractionQueries.Query(Space(), actor, target) != SpatialInteraction.Reachable; steps++)
            {
                bound.Token.ThrowIfCancellationRequested();
                Assert.True(steps < 96, "Shared opening walk exceeded its native input sample bound.");
                var space = Space();
                var from = space.GetActor(actor).Feet;
                long remaining = RouteLength(space, actor, from, destination);
                // Held input stops on actual observation. Rank safe progress across its whole
                // short feedback envelope instead of chasing an exact 32-subunit connector.
                var directions = new (string Name, long X, long Y)[]
                    { ("left", -32, 0), ("up", 0, -32), ("down", 0, 32), ("right", 32, 0) };
                var options = new List<(string Direction, long Progress)>();
                foreach (var direction in directions)
                {
                    long progress = 0;
                    bool improves = true;
                    for (int sample = 1; sample <= 4; sample++)
                    {
                        var candidate = from.Offset(direction.X * sample, direction.Y * sample, 0);
                        if (space.PrepareMove(actor, space.GetActor(actor).Revision, direction.X * sample, direction.Y * sample).Status != SpatialMoveStatus.Prepared) break;
                        long after = RouteLength(space, actor, candidate, destination);
                        if (after >= remaining) { improves = false; break; }
                        progress = remaining - after;
                    }
                    if (improves && progress > 0) options.Add((direction.Name, progress));
                }
                Assert.NotEmpty(options);
                string directionName = options.OrderByDescending(option => option.Progress).First().Direction;
                await Send(actor, "walk", directionName);
                Assert.NotEqual(from, Space().GetActor(actor).Feet);
            }
        }
        static long RouteLength(LocalSpatialSnapshot space, EntityId actor, SubunitPosition from, SubunitPosition destination)
        {
            var original = space.GetActor(actor);
            var placed = space.WithActors(space.Actors.Select(row => row.ActorId == actor
                ? new ActorPresence(original.Binding, original.Condition, original.Scope, from, original.Revision, original.HomePlaceId)
                : row));
            var route = placed.QueryGroundRoute(actor, destination);
            if (route.Status != GroundRouteStatus.Reachable) return long.MaxValue;
            long length = 0;
            foreach (var next in route.Waypoints)
            {
                length = checked(length + Math.Abs(next.X - from.X) + Math.Abs(next.Y - from.Y));
                from = next;
            }
            return length;
        }
        void Mark(string name) => stages.Add(new { Stage = name, Milliseconds = watch.ElapsedMilliseconds,
            Players = peers.Select(pair => new { Actor = pair.Key.ToString(), Commands = pair.Value.Submissions.Count,
                Currency = pair.Value.View.Progression!.CurrencyBalance }).ToArray() });
        void Write(string name, object value) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static WorldActionCommand Last(NativeRosterProcess peer) => WorldActionCodec.Decode(
        Convert.FromHexString(peer.Submissions[^1].GetProperty("Command").GetString()!));
}
