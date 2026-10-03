using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.Networking.ControllerTransport;
using OpenFantasy.Hosting.Tests.Simulation.Controllers;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Client.SelectedProduction;

/// <summary>Managed client boundary: real four-human admission, journaled travel, consent, loss and acquisition.</summary>
internal sealed class SelectedProductionClientFixture : IAsyncDisposable
{
    private readonly SessionFiles _files = new();
    private readonly List<PlayerMessageTransport> _clients = [];
    private readonly List<AuthenticatedPlayerChannel> _channels = [];
    private bool _stopped;

    private SelectedProductionClientFixture(WorldControlCapabilities capabilities, bool excavation)
    {
        var basis = WorldControllerAdmission.CreateFresh(CargoOutcomeWorldAdmission.CreateEmbedded(
            CargoOutcomeWorldAdmission.SeedText, ["Traveler", "Mira", "Joss", "Lark"]));
        var living = basis.Care!.Ordinary!.Definitions.First(value =>
            value.DeliveryMode == RestorativeDeliveryMode.LivingAssistance);
        var recovery = RestorativeUseDefinition.CreateIncapacitatedAssistance(new(0xc040aaaa00000000, 1),
            living.RemedyDefinitionId, living.RemedyVersion, living.SupportedForms);
        Genesis = WorldDelegatedControlAdmission.CreateFresh(WorldControlPermissionAdmission.CreateFresh(
            WorldCareDispositionAdmission.CreateFresh(WorldRecoveryAdmission.CreateFresh(basis, [recovery], []))));
        EntityId owner = Assert.Single(Genesis.Excavation!.Sites).OwnerActorId;
        Actors = [owner, .. Genesis.Controllers!.Assignments.Where(value => value.ActorId != owner).Select(value => value.ActorId)];
        Assert.Equal(4, Actors.Count);
        Authority = SqliteWorldAuthority.CreateNew(Path, Genesis);
        try
        {
            PrepareTravel(excavation);
            Grant(capabilities);
        }
        catch
        {
            Authority.Dispose();
            _files.Dispose();
            throw;
        }
        Host = new LocalWorldSession(Authority, Owner, Delivery(Owner), Time);
        Admission = new PlayerAdmissionService(Host, [], NetworkTime);
    }

    internal EntityId BerryNode => Genesis.Gathering!.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Berry).NodeId;
    internal EntityId WoodDefinition => Genesis.Gathering!.Nodes.Single(node => node.Definition.Kind == GatherNodeKind.Oak).Definition.YieldDefinition.DefinitionId;
    internal EntityId BerryDefinition => Genesis.Gathering!.GetNode(BerryNode).Definition.YieldDefinition.DefinitionId;
    internal WorldSnapshot Genesis { get; }
    internal IReadOnlyList<EntityId> Actors { get; }
    internal EntityId Owner => Actors[0];
    internal EntityId Grantee => Actors[1];
    internal EntityId Third => Actors[2];
    internal EntityId Fourth => Actors[3];
    internal EncounterJourneyWorldFixture Commands { get; } = new();
    internal ManualSessionTimeProvider Time { get; } = new();
    internal ManualSessionTimeProvider NetworkTime { get; } = new();
    internal SqliteWorldAuthority Authority { get; }
    internal LocalWorldSession Host { get; }
    internal PlayerAdmissionService Admission { get; }
    internal string Path => _files.Path;
    internal WorldSnapshot State => Authority.ReadStateForHost();

    internal static async Task<SelectedProductionClientFixture> Open(WorldControlCapabilities capabilities, bool excavation = false)
    {
        var fixture = new SelectedProductionClientFixture(capabilities, excavation);
        await fixture.Host.Player.DisposeAsync();
        await SessionFixture.Until(() => fixture.Time.HasPendingTimer);
        return fixture;
    }

    // Preparation changes only position and ordinary elapsed world time, through recorded authority commits.
    private void PrepareTravel(bool excavation)
    {
        if (excavation)
        {
            var waypoint = State.Space.GetActor(Owner).Feet;
            var destination = BreachFarmLayout.MiningStance;
            Assert.Equal(waypoint.Z, destination.Z);
            // The authored approach goes north, then east along the dry bank. Keep each
            // route query within one cell instead of exhausting BFS on the full bank leg.
            while (waypoint.Y != destination.Y)
            {
                waypoint = waypoint.Offset(0, Math.Clamp(destination.Y - waypoint.Y, -256, 256), 0);
                Walk(waypoint);
            }
            while (waypoint.X != destination.X)
            {
                waypoint = waypoint.Offset(Math.Clamp(destination.X - waypoint.X, -256, 256), 0, 0);
                Walk(waypoint);
            }
        }
        else
        {
            var center = Genesis.Gathering!.GetNode(BerryNode).Feet;
            var choices = (from x in new[] { -256L, 0L, 256L }
                from y in new[] { -256L, 0L, 256L }
                where x != 0 || y != 0
                let point = center.Offset(x, y, 0)
                where State.Space.QueryOccupancy(Owner, point) == SpatialOccupancy.Clear
                let route = State.Space.QueryGroundRoute(Owner, point)
                where route.Status == GroundRouteStatus.Reachable
                orderby route.Waypoints.Count, point.X, point.Y
                select point).ToArray();
            Assert.NotEmpty(choices);
            Walk(choices[0]);
            Assert.Equal(SpatialInteraction.Reachable, ResourceInteractionQueries.Query(State.Space, Owner, center));
        }
        for (int step = 0; State.Gathering!.GetActorWork(Owner).Effort < GatherRulesV1.MaximumEffort
            || Actors.Any(actor => !WorldActorAvailability.CanAct(State, actor)); step++)
        {
            Assert.True(step < 128, "Ordinary preparation must earn readiness and effort within its finite bound.");
            Authority.Commit(new(Commands.NextId(), State.WorldId, State.BranchId, State.Revision,
                State.AdvancingSteps, WorldBatchKind.AdvancingStep, [], [], State.ProfileVersion));
        }
        Assert.NotEqual(Genesis.Space.GetActor(Owner).Feet, State.Space.GetActor(Owner).Feet);
        Assert.Equal(Genesis.Inventory.ContentHash, State.Inventory.ContentHash);
        Assert.Equal(Genesis.Production!.ContentHash, State.Production!.ContentHash);
        Assert.Equal(Genesis.Excavation!.ContentHash, State.Excavation!.ContentHash);
    }

    private void Walk(SubunitPosition destination)
    {
        for (int step = 0; State.Space.GetActor(Owner).Feet != destination; step++)
        {
            Assert.True(step < 512, "Preparation uses finite ordinary ground travel.");
            var before = State;
            var actor = before.Space.GetActor(Owner);
            var route = before.Space.QueryGroundRoute(Owner, destination);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            var next = Assert.IsType<SubunitPosition>(route.NextWaypoint);
            long dx = Math.Clamp(next.X - actor.Feet.X, -32, 32);
            long dy = dx == 0 ? Math.Clamp(next.Y - actor.Feet.Y, -32, 32) : 0;
            Authority.Commit(new(Commands.NextId(), before.WorldId, before.BranchId, before.Revision,
                before.AdvancingSteps, WorldBatchKind.AdvancingStep, [new(Delivery(Owner), Owner, dx, dy)], [], before.ProfileVersion));
            Assert.Equal(actor.Feet.Offset(dx, dy, 0), State.Space.GetActor(Owner).Feet);
        }
    }

    internal TrustedCommandDelivery Delivery(EntityId actor)
    {
        var binding = Genesis.Inventory.Controls.Single(value => value.ActorId == actor);
        return new(binding.PrincipalId, binding.SessionId);
    }

    private void Grant(WorldControlCapabilities capabilities)
    {
        var offer = Commands.Command(State, Owner,
            new OfferControlPermissionIntent(Grantee, 0, capabilities));
        Apply(offer);
        var slot = State.ControlPermissions!.Find(Owner, Grantee)!;
        Apply(Commands.Command(State, Grantee,
            new AcceptControlPermissionIntent(Owner, slot.Revision, offer.CommandId, slot.TermsHash!)));
    }

    internal WorldSnapshot Revoke() => Apply(Commands.Command(State, Owner,
        new RevokeControlPermissionIntent(Grantee, State.ControlPermissions!.Find(Owner, Grantee)!.Revision)));

    private WorldSnapshot Apply(WorldActionCommand command)
    {
        var decision = Authority.Commit(Commands.Batch(State, WorldBatchKind.ControlPermission, [command]));
        Assert.Equal(WorldActionRejection.None, Assert.Single(decision.Receipts).Rejection);
        return decision.Candidate;
    }

    internal async Task<JoinedPlayer> Join(EntityId actor)
    {
        var credential = Admission.Issue(actor, Delivery(actor), TimeSpan.FromHours(1));
        var pair = InMemoryPlayerTransport.CreatePair();
        _clients.Add(pair.Client);
        await ControllerTransportFixture.Send(pair.Client,
            PlayerHandshakeCodec.Hello(1, Admission.Compatibility, credential.Export()));
        var channel = await Admission.AcceptAsync(pair.Server, TestContext.Current.CancellationToken);
        _channels.Add(channel);
        Assert.Equal(PlayerMessageKind.Welcome, (await ControllerTransportFixture.Receive(pair.Client)).Kind);
        Assert.True(channel.Player.NetworkEstablished);
        Assert.False(channel.Player.Ready);
        return new(pair.Client, channel);
    }

    internal async Task Lose(JoinedPlayer joined)
    {
        Task running = new PlayerCommandSession(joined.Channel, NetworkTime).RunAsync(TestContext.Current.CancellationToken);
        await joined.Client.DisposeAsync();
        await ControllerTransportFixture.Finish(running);
        Assert.Equal(LocalWorldRunState.Unavailable, joined.Player.ReadRunState());
    }

    internal async Task<(JoinedPlayer Grantee, JoinedPlayer Third, JoinedPlayer Fourth, WorldControlWitness Witness)> Acquire()
    {
        var owner = await Join(Owner);
        var grantee = await Join(Grantee);
        var third = await Join(Third);
        var fourth = await Join(Fourth);
        await Lose(owner);
        await Tick(30_000);
        Assert.Equal(WorldControllerMode.Autonomous, State.Controllers!.Get(Owner).Mode);
        var grant = State.ControlPermissions!.Find(Owner, Grantee)!;
        var controller = State.Controllers.Get(Owner);
        var observed = new LocalControlAcquisition(Owner, grant.GrantCommandId!.Value,
            grant.AcceptanceCommandId!.Value, grant.Revision, grant.Terms!.Capabilities, grant.TermsHash!,
            State.EffectiveControl!.Find(Owner)?.Revision ?? 0, controller.DisconnectId!.Value, controller.Revision);
        var witness = await grantee.Player.AcquireControlAsync(observed);
        return (grantee, third, fourth, witness);
    }

    internal Task Tick(double milliseconds = 50) =>
        LiveControllerHostTests.Tick(Time, TimeSpan.FromMilliseconds(milliseconds));

    internal async Task Stop()
    {
        if (_stopped) return;
        await Host.StopAsync();
        _stopped = true;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var channel in _channels)
        {
            if (_stopped) await channel.Transport.DisposeAsync();
            else await channel.DisposeAsync();
        }
        foreach (var client in _clients) await client.DisposeAsync();
        await Stop();
        await Host.DisposeAsync();
        _files.Dispose();
    }

    internal sealed record JoinedPlayer(PlayerMessageTransport Client, AuthenticatedPlayerChannel Channel)
    {
        internal LocalPlayerConnection Player => Channel.Player;
    }
}
