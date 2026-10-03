using System.Net;
using System.Security.Cryptography.X509Certificates;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests.SelectedGiftsNative;

/// <summary>Unchanged Cargo starting stock, with only genuine journaled travel before native admission.</summary>
internal sealed class SelectedGiftWorld : IAsyncDisposable
{
    private readonly ServerFiles _files = new();
    private readonly List<string> _preparation = [];
    private ulong _identity = 1;
    private bool _stopped;
    internal string Database => Path.Combine(_files.DirectoryPath, "selected-gifts.sqlite3");
    internal X509Certificate2 Certificate { get; } = ServerWorld.CreateCertificate();
    internal WorldSnapshot Genesis { get; }
    internal WorldSnapshot Prepared { get; private set; }
    internal EntityId[] Actors { get; }
    internal EntityId Owner => Actors[0];
    internal EntityId Grantee => Actors[1];
    internal EntityId Recipient => Actors[2];
    internal EntityId OwnerLot { get; private set; }
    internal EntityId RecipientLot { get; private set; }
    internal CargoPlayerHost Host { get; private set; } = null!;
    internal PlayerWebSocketServer Server { get; private set; } = null!;
    internal string Description => string.Join("\n", _preparation);

    private SelectedGiftWorld()
    {
        var controllers = WorldControllerAdmission.CreateFresh(CargoOutcomeWorldAdmission.CreateEmbedded(
            CargoOutcomeWorldAdmission.SeedText, ["Amber", "Birch", "Cedar", "Dawn"]));
        var remedy = controllers.Care!.Ordinary!.Definitions.First(value =>
            value.DeliveryMode == RestorativeDeliveryMode.LivingAssistance);
        var recovery = RestorativeUseDefinition.CreateIncapacitatedAssistance(new(0xcc61467400000000, 1),
            remedy.RemedyDefinitionId, remedy.RemedyVersion, remedy.SupportedForms);
        Genesis = WorldDelegatedControlAdmission.CreateFresh(WorldControlPermissionAdmission.CreateFresh(
            WorldCareDispositionAdmission.CreateFresh(WorldRecoveryAdmission.CreateFresh(controllers, [recovery], []))));
        Prepared = Genesis;
        Actors = Genesis.Controllers!.Assignments.Select(row => row.ActorId).ToArray();
        Assert.Equal(4, Actors.Length);
    }

    internal static async Task<SelectedGiftWorld> Open()
    {
        var world = new SelectedGiftWorld();
        try
        {
            using (var authority = SqliteWorldAuthority.CreateNew(world.Database, world.Genesis))
            {
                world.PrepareReach(authority);
                world.Prepared = authority.ReadStateForHost();
                Assert.Equal(world.Genesis.Inventory.ContentHash, world.Prepared.Inventory.ContentHash);
                Assert.Equal(world.Genesis.Production!.ContentHash, world.Prepared.Production!.ContentHash);
                world.OwnerLot = world.SingleTool(world.Owner);
                world.RecipientLot = world.SingleTool(world.Recipient);
            }
            world.Host = await CargoPlayerHost.OpenExistingAsync(world.Database, ServerWorld.Cancellation);
            world.Server = await PlayerWebSocketServer.StartAsync(world.Host.Admission,
                new(IPAddress.Loopback, 0, world.Certificate), ServerWorld.Cancellation);
            return world;
        }
        catch { await world.DisposeAsync(); throw; }
    }

    private EntityId SingleTool(EntityId actor) => Prepared.Inventory.Custody.Lots
        .Where(lot => lot.CustodianId == Prepared.GetResident(actor).InventoryCustodianId
            && lot.Quantity == 1 && lot.Protection == LotProtection.None && lot.ReservedBy is null
            && Prepared.Gathering!.Tools.Any(tool => tool.Definition == lot.Definition))
        .OrderBy(lot => lot.LotId).First().LotId;

    private EntityId NextId() => new(0xcc61467500000000, _identity++);

    private void PrepareReach(SqliteWorldAuthority authority)
    {
        var state = authority.ReadStateForHost();
        bool InReach(WorldSnapshot root) => root.Space.QueryInteraction(Owner, Recipient) == SpatialInteraction.Reachable
            && root.Space.QueryInteraction(Recipient, Owner) == SpatialInteraction.Reachable;
        var origin = state.Space.GetActor(Recipient).Feet;
        int steps = 0;
        if (!InReach(state))
        {
            var center = state.Space.GetActor(Owner).Feet;
            var goals = (from x in new[] { -256L, 0L, 256L }
                from y in new[] { -256L, 0L, 256L }
                where x != 0 || y != 0
                let goal = center.Offset(x, y, 0)
                let route = state.Space.QueryGroundRoute(Recipient, goal)
                where route.Status == GroundRouteStatus.Reachable
                orderby route.Waypoints.Count, goal.X, goal.Y
                select goal).ToArray();
            Assert.NotEmpty(goals);
            var binding = state.Inventory.Controls.Single(row => row.ActorId == Recipient);
            var delivery = new TrustedCommandDelivery(binding.PrincipalId, binding.SessionId);
            while (!InReach(state))
            {
                Assert.True(steps++ < 512, "Genuine recipient travel exceeded its finite preparation budget.");
                var actor = state.Space.GetActor(Recipient);
                var route = state.Space.QueryGroundRoute(Recipient, goals[0]);
                Assert.Equal(GroundRouteStatus.Reachable, route.Status);
                var waypoint = Assert.IsType<SubunitPosition>(route.NextWaypoint);
                long dx = Math.Clamp(waypoint.X - actor.Feet.X, -32, 32);
                long dy = dx == 0 ? Math.Clamp(waypoint.Y - actor.Feet.Y, -32, 32) : 0;
                authority.Commit(new(NextId(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
                    WorldBatchKind.AdvancingStep, [new(delivery, Recipient, dx, dy)], [], state.ProfileVersion));
                state = authority.ReadStateForHost();
                Assert.Equal(actor.Feet.Offset(dx, dy, 0), state.Space.GetActor(Recipient).Feet);
            }
        }
        for (int waits = 0; Actors.Any(actor => !WorldActorAvailability.CanAct(state, actor)); waits++)
        {
            Assert.True(waits < 64, "Ordinary preparation debt did not retire within 64 advancing steps.");
            authority.Commit(new(NextId(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
                WorldBatchKind.AdvancingStep, [], [], state.ProfileVersion));
            state = authority.ReadStateForHost();
        }
        Assert.True(InReach(state));
        _preparation.Add($"Recipient {Recipient}: {origin} to {state.Space.GetActor(Recipient).Feet}, {steps} ordinary movement commits.");
        _preparation.Add($"Owner {Owner}: {state.Space.GetActor(Owner).Feet}; mutual normal interaction reach; all four humans can act.");
        _preparation.Add($"Prepared revision {state.Revision}, steps {state.AdvancingSteps}; exact admitted stock and production preserved.");
    }

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
