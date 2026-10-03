using System.Net;
using System.Security.Cryptography.X509Certificates;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Construction;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Server.Tests.HumanConstructionNative;

/// <summary>Declared fresh human-builder scenario; preparation earns only ordinary journaled travel.</summary>
internal sealed class HumanConstructionWorld : IAsyncDisposable
{
    private readonly ServerFiles _files = new();
    private readonly List<string> _preparation = [];
    private ulong _identity = 1;
    private bool _stopped;
    internal string Database => Path.Combine(_files.DirectoryPath, "human-construction.sqlite3");
    internal X509Certificate2 Certificate { get; } = ServerWorld.CreateCertificate();
    internal WorldSnapshot Genesis { get; }
    internal WorldSnapshot Prepared { get; private set; }
    internal EntityId[] Actors { get; }
    internal EntityId Owner => Assert.Single(Genesis.Construction!.Projects).BuilderActorId;
    internal EntityId Grantee => Actors[2];
    internal EntityId Supplier => Actors[1];
    internal EntityId Observer => Actors[3];
    internal EntityId Project => Assert.Single(Genesis.Construction!.Projects).ProjectId;
    internal CargoPlayerHost Host { get; private set; } = null!;
    internal PlayerWebSocketServer Server { get; private set; } = null!;
    internal string Description => string.Join("\n", _preparation);

    private HumanConstructionWorld()
    {
        Genesis = HumanConstructionWorldAdmission.CreateEmbedded(HumanConstructionWorldAdmission.SeedText,
            ["Supplier", "Operator", "Observer"]);
        Prepared = Genesis;
        Actors = [Owner, .. new[] { "Supplier", "Operator", "Observer" }.Select(name => Genesis.Residents.Single(actor => actor.DisplayName == name).ActorId)];
        Assert.Equal(4, Actors.Length);
        Assert.All(Actors, actor => Assert.Equal(ResidentControllerKind.Player, Genesis.GetResident(actor).ControllerKind));
    }

    internal static async Task<HumanConstructionWorld> Open(string output)
    {
        var world = new HumanConstructionWorld();
        File.WriteAllBytes(Path.Combine(output, "genesis.ofc"), WorldSnapshotCodec.Encode(world.Genesis));
        try
        {
            using (var authority = SqliteWorldAuthority.CreateNew(world.Database, world.Genesis))
            {
                var project = Assert.Single(world.Genesis.Construction!.Projects);
                world.Walk(authority, world.Owner, project.WorkFeet);
                world.Walk(authority, world.Supplier, world.ConstructionApproach(authority.ReadStateForHost(), world.Supplier, project));
                world.Walk(authority, world.Observer, world.ConstructionApproach(authority.ReadStateForHost(), world.Observer, project));
                world.Prepared = authority.ReadStateForHost();
                Assert.Equal(SpatialInteraction.Reachable, world.Prepared.Space.QueryInteraction(world.Supplier, world.Owner));
                Assert.Equal(StructurePlacementStatus.Prepared, ConstructionPlacement(world.Prepared.Space, project).Status);
                world._preparation.Add("Actual post-travel placement is Prepared with every retained and current-position access route; all original actors remain present.");
                Assert.Equal(world.Genesis.Inventory.ContentHash, world.Prepared.Inventory.ContentHash);
                Assert.Equal(world.Genesis.Production!.ContentHash, world.Prepared.Production!.ContentHash);
                Assert.Equal(world.Genesis.Construction.ContentHash, world.Prepared.Construction!.ContentHash);
                var quest = world.Genesis.Quests!.Definitions.Single(value => value.DefinitionId == project.QuestId);
                Assert.True(world.Prepared.Inventory.Custody.Lots.Where(lot => lot.CustodianId == world.Prepared.GetResident(world.Supplier).InventoryCustodianId
                    && lot.Definition == quest.Item).Sum(lot => (decimal)lot.Quantity) >= quest.Quantity);
                Assert.Empty(world.Prepared.Quests!.Participations);
                Assert.Empty(world.Prepared.Production.Batches);
                File.WriteAllText(Path.Combine(output, "preparation.txt"), world.Description);
            }
            await world.Start();
            return world;
        }
        catch { await world.DisposeAsync(); throw; }
    }

    internal async Task Start()
    {
        Host = await CargoPlayerHost.OpenExistingAsync(Database, ServerWorld.Cancellation);
        Server = await PlayerWebSocketServer.StartAsync(Host.Admission, new(IPAddress.Loopback, 0, Certificate), ServerWorld.Cancellation);
        _stopped = false;
    }

    private SubunitPosition ConstructionApproach(WorldSnapshot state, EntityId actor, ConstructionProject project)
    {
        var choices = (from x in new[] { -256L, 0L, 256L }
            from y in new[] { -256L, 0L, 256L }
            where x != 0 || y != 0
            let point = project.WorkFeet.Offset(x, y, 0)
            // Required route endpoints belong to their named actor. A clear destination
            // can nevertheless obstruct another actor's retained construction access.
            where !project.RequiredAccess.Any(access => access.ActorId != actor && (access.From == point || access.To == point))
            where state.Space.QueryOccupancy(actor, point) == SpatialOccupancy.Clear
            let route = state.Space.QueryGroundRoute(actor, point)
            where route.Status == GroundRouteStatus.Reachable
            orderby point.X, route.Waypoints.Count, point.Y
            select point).ToArray();
        foreach (SubunitPosition point in choices)
        {
            LocalSpatialSnapshot? planned = PlanTravel(state.Space, actor, point);
            if (planned is null || planned.QueryInteraction(Supplier, Owner) != SpatialInteraction.Reachable
                || ConstructionPlacement(planned, project).Status != StructurePlacementStatus.Prepared) continue;
            return point;
        }
        throw new InvalidOperationException("No bounded ordinary approach preserves supplier reach, actual bodies and every construction access route.");
    }

    // This is a private spatial counterfactual assembled from legal movement plans, never a
    // world admission or persisted actor relocation. Walk below earns the chosen route in the journal.
    private static LocalSpatialSnapshot? PlanTravel(LocalSpatialSnapshot space, EntityId actorId, SubunitPosition destination)
    {
        for (int step = 0; space.GetActor(actorId).Feet != destination; step++)
        {
            if (step == 512) return null;
            var actor = space.GetActor(actorId);
            var route = space.QueryGroundRoute(actorId, destination);
            if (route.Status != GroundRouteStatus.Reachable || route.NextWaypoint is not { } next) return null;
            long dx = Math.Clamp(next.X - actor.Feet.X, -32, 32);
            long dy = dx == 0 ? Math.Clamp(next.Y - actor.Feet.Y, -32, 32) : 0;
            if (dx == 0 && dy == 0) return null;
            var move = space.PrepareMove(actorId, actor.Revision, dx, dy);
            if (move.Status != SpatialMoveStatus.Prepared || move.Candidate is not { } moved) return null;
            space = space.WithActors(space.Actors.Select(value => value.ActorId == actorId ? moved : value));
        }
        return space;
    }

    private static StructurePlacementResult ConstructionPlacement(LocalSpatialSnapshot space, ConstructionProject project)
    {
        // The project ID is only a private geometry query key, exactly as in the work projection.
        // Actual completion derives a new installed identity from its accepted command.
        var structure = project.Definition.CreateStructure(project.ProjectId, project.SiteFeet,
            space.GroundSupport != GroundSupportMode.TerrainOnly);
        var access = project.RequiredAccess.Concat(project.RequiredAccess.Select(value =>
            new GroundAccessRequirement(value.ActorId, space.GetActor(value.ActorId).Feet, value.To))).Distinct();
        return space.PrepareStructurePlacement(structure, access);
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

    private EntityId NextId() => new(0xcc70726f20000000, _identity++);

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
