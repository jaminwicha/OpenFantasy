using System.Net;
using System.Security.Cryptography.X509Certificates;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Commands;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Production;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests.RestorativeChoicesNative;

/// <summary>Unchanged four-human cargo admission, real source-stat injury, and a crafted second finite dose.</summary>
internal sealed partial class RestorativeChoicesWorld : IAsyncDisposable
{
    private readonly ServerFiles _files = new();
    private readonly List<string> _preparation = [];
    private ulong _identity = 1;
    private bool _stopped;
    internal string Database => Path.Combine(_files.DirectoryPath, "restorative-choices.sqlite3");
    internal X509Certificate2 Certificate { get; } = ServerWorld.CreateCertificate();
    internal WorldSnapshot Genesis { get; }
    internal WorldSnapshot Prepared { get; private set; }
    internal EntityId[] Actors { get; }
    internal EntityId Owner => Actors[0];
    internal EntityId Grantee => Actors[1];
    internal EntityId UseDefinition => Genesis.Care!.Ordinary!.Definitions.Single(value => value.DeliveryMode == RestorativeDeliveryMode.SelfUse).DefinitionId;
    internal EntityId Permission => Genesis.Care!.Ordinary!.Permissions.Single(value => value.PatientActorId == Owner).PermissionId;
    internal CargoPlayerHost Host { get; private set; } = null!;
    internal PlayerWebSocketServer Server { get; private set; } = null!;
    internal string Description => string.Join("\n", _preparation);

    private RestorativeChoicesWorld()
    {
        var controllers = WorldControllerAdmission.CreateFresh(CargoOutcomeWorldAdmission.CreateEmbedded(
            CargoOutcomeWorldAdmission.SeedText, ["Amber", "Birch", "Cedar", "Dawn"]));
        var remedy = controllers.Care!.Ordinary!.Definitions.First(value => value.DeliveryMode == RestorativeDeliveryMode.LivingAssistance);
        var recovery = RestorativeUseDefinition.CreateIncapacitatedAssistance(new(0xcc63617251000000, 1),
            remedy.RemedyDefinitionId, remedy.RemedyVersion, remedy.SupportedForms);
        Genesis = WorldDelegatedControlAdmission.CreateFresh(WorldControlPermissionAdmission.CreateFresh(
            WorldCareDispositionAdmission.CreateFresh(WorldRecoveryAdmission.CreateFresh(controllers, [recovery], []))));
        Prepared = Genesis;
        EntityId owner = Assert.Single(Genesis.Excavation!.Sites).OwnerActorId;
        Actors = [owner, .. Genesis.Controllers!.Assignments.Where(row => row.ActorId != owner).Select(row => row.ActorId)];
        Assert.Equal(4, Actors.Length);
    }

    internal static async Task<RestorativeChoicesWorld> Open(string output)
    {
        var world = new RestorativeChoicesWorld();
        File.WriteAllBytes(Path.Combine(output, "genesis.ofc"), WorldSnapshotCodec.Encode(world.Genesis));
        try
        {
            using (var authority = SqliteWorldAuthority.CreateNew(world.Database, world.Genesis))
            {
                world.EarnInjury(authority);
                world.CraftSecondDose(authority);
                world.Prepared = authority.ReadStateForHost();
                Assert.InRange(world.Prepared.Progression!.GetActor(world.Owner).CurrentHp, 1, 19);
                Assert.Empty(world.Prepared.Care!.Ordinary!.Uses);
                Assert.True(world.Prepared.Care.Ordinary.Permissions.Single(value => value.PermissionId == world.Permission).IsActive);
                var remedy = world.Prepared.Care.Remedies.Single().Item;
                EntityId bag = world.Prepared.GetResident(world.Owner).InventoryCustodianId;
                Assert.Equal(1UL, Assert.Single(world.Prepared.Inventory.Custody.Lots,
                    lot => lot.CustodianId == bag && lot.Definition == remedy).Quantity);
                Assert.Equal(1UL, Assert.Single(world.Prepared.Production!.Lots,
                    lot => lot.CustodianId == bag && lot.Definition == remedy).Quantity);
                Assert.Empty(new LocalRestorativeController().SelectActions(world.Prepared, authority.IsIdentityOccupiedForHost, new HashSet<EntityId>()));
                var use = WorldRestorativeActions.Prepare(world.Prepared, world.Owner, world.Owner, world.UseDefinition);
                Assert.NotNull(use);
                Assert.Equal(IngredientLotKind.Source, use.Ingredient.Kind);
                Assert.Equal(WorldActionRejection.None, WorldRestorativeActions.Evaluate(world.Prepared, world.Owner, use));
                File.WriteAllBytes(Path.Combine(output, "prepared.ofc"), WorldSnapshotCodec.Encode(world.Prepared));
                File.WriteAllText(Path.Combine(output, "preparation.txt"), world.Description);
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

    private EntityId NextId() => new(0xcc63617252000000, _identity++);
    private WorldActionCommand Command(WorldSnapshot state, EntityId actor, WorldActionIntent intent)
    {
        var binding = state.Inventory.Controls.Single(value => value.ActorId == actor);
        return new(NextId(), binding.PrincipalId, binding.SessionId, actor, state.WorldId, state.BranchId,
            state.Space.GetActor(actor).Revision, intent);
    }
    private WorldSnapshot Commit(SqliteWorldAuthority authority, WorldBatchKind kind, params WorldActionCommand[] commands)
    {
        var state = authority.ReadStateForHost();
        var decision = authority.Commit(new(NextId(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
            kind, [], commands, state.ProfileVersion));
        Assert.All(decision.Receipts, receipt => Assert.Equal(CommandReceiptStatus.Applied, receipt.Status));
        return decision.Candidate;
    }
    private void WaitReady(SqliteWorldAuthority authority)
    {
        for (int count = 0; Actors.Any(actor => !WorldActorAvailability.CanAct(authority.ReadStateForHost(), actor)); count++)
        {
            Assert.True(count < 64, "Original action debts exceeded the ordinary 64-step preparation bound.");
            Commit(authority, WorldBatchKind.AdvancingStep);
        }
    }
    private void Walk(SqliteWorldAuthority authority, EntityId actorId, SubunitPosition destination)
    {
        var state = authority.ReadStateForHost();
        var origin = state.Space.GetActor(actorId).Feet;
        var binding = state.Inventory.Controls.Single(value => value.ActorId == actorId);
        int steps = 0;
        while (state.Space.GetActor(actorId).Feet != destination)
        {
            Assert.True(steps++ < 512, "Ordinary care preparation travel exceeded its finite leg budget.");
            var actor = state.Space.GetActor(actorId);
            var route = state.Space.QueryGroundRoute(actorId, destination);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            var next = Assert.IsType<SubunitPosition>(route.NextWaypoint);
            long dx = Math.Clamp(next.X - actor.Feet.X, -32, 32);
            long dy = dx == 0 ? Math.Clamp(next.Y - actor.Feet.Y, -32, 32) : 0;
            state = authority.Commit(new(NextId(), state.WorldId, state.BranchId, state.Revision, state.AdvancingSteps,
                WorldBatchKind.AdvancingStep, [new(new(binding.PrincipalId, binding.SessionId), actorId, dx, dy)], [], state.ProfileVersion)).Candidate;
            Assert.Equal(actor.Feet.Offset(dx, dy, 0), state.Space.GetActor(actorId).Feet);
        }
        _preparation.Add($"Journaled ordinary travel {actorId}: {origin} → {destination}, {steps} samples; no native travel claim.");
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
