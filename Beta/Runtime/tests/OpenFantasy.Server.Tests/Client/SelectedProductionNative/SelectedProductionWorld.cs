using System.Net;
using System.Security.Cryptography.X509Certificates;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests.SelectedProductionNative;

/// <summary>Original Cargo admission; preparation consists solely of journaled walking and ordinary time.</summary>
internal sealed class SelectedProductionWorld : IAsyncDisposable
{
    private readonly ServerFiles _files = new();
    private readonly List<string> _preparation = [];
    private ulong _identity = 1;
    private bool _stopped;
    internal string Database => Path.Combine(_files.DirectoryPath, "selected-production.sqlite3");
    internal X509Certificate2 Certificate { get; } = ServerWorld.CreateCertificate();
    internal WorldSnapshot Genesis { get; }
    internal WorldSnapshot Prepared { get; private set; }
    internal EntityId[] Actors { get; }
    internal EntityId Owner => Actors[0];
    internal EntityId Grantee => Actors[1];
    internal EntityId Recipient => Actors[2];
    internal EntityId BerryNode => Genesis.Gathering!.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Berry).NodeId;
    internal EntityId Recipe => Genesis.Production!.Recipes.Single(recipe => recipe.PhysicalRecipe.Key == "craft_rations").DefinitionId;
    internal CargoPlayerHost Host { get; private set; } = null!;
    internal PlayerWebSocketServer Server { get; private set; } = null!;
    internal string Description => string.Join("\n", _preparation);

    private SelectedProductionWorld()
    {
        var controllers = WorldControllerAdmission.CreateFresh(CargoOutcomeWorldAdmission.CreateEmbedded(
            CargoOutcomeWorldAdmission.SeedText, ["Amber", "Birch", "Cedar", "Dawn"]));
        var remedy = controllers.Care!.Ordinary!.Definitions.First(value => value.DeliveryMode == RestorativeDeliveryMode.LivingAssistance);
        var recovery = RestorativeUseDefinition.CreateIncapacitatedAssistance(new(0xcc70726f00000000, 1),
            remedy.RemedyDefinitionId, remedy.RemedyVersion, remedy.SupportedForms);
        Genesis = WorldDelegatedControlAdmission.CreateFresh(WorldControlPermissionAdmission.CreateFresh(
            WorldCareDispositionAdmission.CreateFresh(WorldRecoveryAdmission.CreateFresh(controllers, [recovery], []))));
        Prepared = Genesis;
        EntityId owner = Assert.Single(Genesis.Excavation!.Sites).OwnerActorId;
        Actors = [owner, .. Genesis.Controllers!.Assignments.Where(row => row.ActorId != owner).Select(row => row.ActorId)];
        Assert.Equal(4, Actors.Length);
    }

    internal static async Task<SelectedProductionWorld> Open(bool excavation, string output)
    {
        var world = new SelectedProductionWorld();
        File.WriteAllBytes(Path.Combine(output, "genesis.ofc"), WorldSnapshotCodec.Encode(world.Genesis));
        try
        {
            using (var authority = SqliteWorldAuthority.CreateNew(world.Database, world.Genesis))
            {
                if (excavation)
                {
                    var waypoint = world.Genesis.Space.GetActor(world.Owner).Feet;
                    var destination = BreachFarmLayout.MiningStance;
                    Assert.Equal(waypoint.Z, destination.Z);
                    // Follow the authored north-then-east dry-bank approach in one-cell
                    // legs, keeping each query within the existing bounded ground search.
                    while (waypoint.Y != destination.Y)
                    {
                        waypoint = waypoint.Offset(0, Math.Clamp(destination.Y - waypoint.Y, -256, 256), 0);
                        world.Walk(authority, world.Owner, waypoint);
                    }
                    while (waypoint.X != destination.X)
                    {
                        waypoint = waypoint.Offset(Math.Clamp(destination.X - waypoint.X, -256, 256), 0, 0);
                        world.Walk(authority, world.Owner, waypoint);
                    }
                }
                else
                {
                    var berry = world.Genesis.Gathering!.GetNode(world.BerryNode);
                    world.Walk(authority, world.Owner, world.Near(authority.ReadStateForHost(), world.Owner, berry.Feet));
                    var state = authority.ReadStateForHost();
                    Assert.Equal(SpatialInteraction.Reachable, ResourceInteractionQueries.Query(state.Space, world.Owner, berry.Feet));
                    world.Walk(authority, world.Recipient, world.Near(state, world.Recipient, state.Space.GetActor(world.Owner).Feet));
                    state = authority.ReadStateForHost();
                    Assert.Equal(SpatialInteraction.Reachable, state.Space.QueryInteraction(world.Owner, world.Recipient));
                    Assert.Equal(SpatialInteraction.Reachable, state.Space.QueryInteraction(world.Recipient, world.Owner));
                    Assert.DoesNotContain(state.Inventory.Custody.Lots, lot => lot.CustodianId == state.GetResident(world.Owner).InventoryCustodianId
                        && lot.Definition == berry.Definition.YieldDefinition);
                }
                var ready = authority.ReadStateForHost();
                for (int step = 0; ready.Gathering!.GetActorWork(world.Owner).Effort < GatherRulesV1.MaximumEffort
                    || world.Actors.Any(actor => !WorldActorAvailability.CanAct(ready, actor)); step++)
                {
                    Assert.True(step < 128, "Normal preparation time did not earn effort and retire ordinary debts.");
                    ready = authority.Commit(new(world.NextId(), ready.WorldId, ready.BranchId, ready.Revision, ready.AdvancingSteps,
                        WorldBatchKind.AdvancingStep, [], [], ready.ProfileVersion)).Candidate;
                }
                world.Prepared = ready;
                Assert.Equal(world.Genesis.Inventory.ContentHash, ready.Inventory.ContentHash);
                Assert.Equal(world.Genesis.Production!.ContentHash, ready.Production!.ContentHash);
                Assert.Equal(world.Genesis.Excavation!.ContentHash, ready.Excavation!.ContentHash);
                Assert.Equal(world.Genesis.LiquidCells!.Portions, ready.LiquidCells!.Portions);
                world._preparation.Add($"Prepared revision {ready.Revision}, steps {ready.AdvancingSteps}; original stock, production and excavation unchanged.");
                world._preparation.Add($"Owner {world.Owner} effort {ready.Gathering!.GetActorWork(world.Owner).Effort}; no injected work or resources.");
            }
            world.Host = await CargoPlayerHost.OpenExistingAsync(world.Database, ServerWorld.Cancellation);
            world.Server = await PlayerWebSocketServer.StartAsync(world.Host.Admission,
                new(IPAddress.Loopback, 0, world.Certificate), ServerWorld.Cancellation);
            return world;
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "preparation.failed.txt"), world.Description + "\n" + error);
            try
            {
                if (File.Exists(world.Database))
                {
                    using var saved = SqliteWorldAuthority.OpenExisting(world.Database, WorldSnapshotCodec.Encode(world.Genesis));
                    saved.BackupForHost(Path.Combine(output, "preparation.failed.sqlite3"));
                    File.WriteAllBytes(Path.Combine(output, "preparation.failed.ofc"), WorldSnapshotCodec.Encode(saved.ReadStateForHost()));
                }
            }
            finally { await world.DisposeAsync(); }
            throw;
        }
    }

    private SubunitPosition Near(WorldSnapshot state, EntityId actor, SubunitPosition center)
    {
        var choices = (from x in new[] { -256L, 0L, 256L }
            from y in new[] { -256L, 0L, 256L }
            where x != 0 || y != 0
            let point = center.Offset(x, y, 0)
            where state.Space.QueryOccupancy(actor, point) == SpatialOccupancy.Clear
            let route = state.Space.QueryGroundRoute(actor, point)
            where route.Status == GroundRouteStatus.Reachable
            orderby route.Waypoints.Count, point.X, point.Y
            select point).ToArray();
        Assert.NotEmpty(choices);
        return choices[0];
    }

    private void Walk(SqliteWorldAuthority authority, EntityId actorId, SubunitPosition destination)
    {
        var state = authority.ReadStateForHost();
        var origin = state.Space.GetActor(actorId).Feet;
        var binding = state.Inventory.Controls.Single(row => row.ActorId == actorId);
        var delivery = new TrustedCommandDelivery(binding.PrincipalId, binding.SessionId);
        int steps = 0;
        while (state.Space.GetActor(actorId).Feet != destination)
        {
            Assert.True(steps++ < 512, "Ordinary preparation travel exceeded its finite route bound.");
            var actor = state.Space.GetActor(actorId);
            var route = state.Space.QueryGroundRoute(actorId, destination);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            var next = Assert.IsType<SubunitPosition>(route.NextWaypoint);
            long dx = Math.Clamp(next.X - actor.Feet.X, -32, 32);
            long dy = dx == 0 ? Math.Clamp(next.Y - actor.Feet.Y, -32, 32) : 0;
            state = authority.Commit(new(NextId(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
                WorldBatchKind.AdvancingStep, [new(delivery, actorId, dx, dy)], [], state.ProfileVersion)).Candidate;
            Assert.Equal(actor.Feet.Offset(dx, dy, 0), state.Space.GetActor(actorId).Feet);
        }
        _preparation.Add($"Ordinary travel {actorId}: {origin} → {destination}, {steps} journaled movement samples.");
    }

    private EntityId NextId() => new(0xcc70726f10000000, _identity++);

    internal async Task Stop()
    {
        if (_stopped) return;
        if (Server is not null) await Server.DisposeAsync();
        if (Host is not null) await Host.DisposeAsync();
        _stopped = true;
    }

    public async ValueTask DisposeAsync()
    {
        try { await Stop(); }
        finally { Certificate.Dispose(); _files.Dispose(); }
    }
}
