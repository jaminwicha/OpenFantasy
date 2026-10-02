using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Networking.ControllerTransport;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.Controllers.Conservative;

/// <summary>
/// Named fresh calibration: both inherited wolves are human controlled, so the first can hold the
/// opponent boundary and the second is an unrelated ready bystander. The optional ally is a newly
/// admitted production roster player. Calibration precedes save creation; all approach and entry
/// thereafter use ordinary committed actions. No live resident, position or encounter is rewritten.
/// </summary>
internal sealed class ConservativeControllerFixture : IAsyncDisposable
{
    private readonly SessionFiles _files = new();
    private readonly List<PlayerMessageTransport> _clients = [];
    private readonly List<AuthenticatedPlayerChannel> _channels = [];
    private readonly List<LocalPlayerConnection> _ports = [];
    private readonly PlayerAdmissionService _admission;
    private readonly PlayerJoinCredential _credential;
    private readonly ManualSessionTimeProvider _networkTime = new();
    private readonly ConstructionUpgradeFixture _walking = new();

    private ConservativeControllerFixture(bool party)
    {
        WorldSnapshot source = RoadThreatWorldAdmission.CreateEmbedded("conservative-controller-calibration",
            party ? new[] { "Traveler", "Companion" } : new[] { "Traveler" });
        var first = source.EncounterLifecycle!.Scenarios.Single(row => row.Key == EncounterJourneyAdmission.FirstScenarioKey);
        Actor = first.InitiatingActorId;
        Opponent = first.TargetActorId;
        Observer = source.EncounterLifecycle.Scenarios.Single(row => row.Key == EncounterJourneyAdmission.SecondScenarioKey).TargetActorId;
        Ally = source.Residents.Where(row => row.ControllerKind == ResidentControllerKind.Player && row.ActorId != Actor)
            .Select(row => (EntityId?)row.ActorId).SingleOrDefault();
        Genesis = WorldControllerAdmission.CreateFresh(WorldEncounterOutcomeAdmission.CreateCalibratedFresh(source,
            new EncounterOutcomeCalibration(new Dictionary<EntityId, int>(), [Opponent, Observer])));
        Authority = SqliteWorldAuthority.CreateNew(_files.Path, Genesis);
        Enter(first);
        Host = new LocalWorldSession(Authority, Actor, Delivery(Actor), Time);
        _admission = new PlayerAdmissionService(Host, [], _networkTime);
        _credential = _admission.Issue(Actor, Delivery(Actor), TimeSpan.FromHours(1));
    }

    internal string Path => _files.Path;
    internal WorldSnapshot Genesis { get; }
    internal EntityId Actor { get; }
    internal EntityId Opponent { get; }
    internal EntityId Observer { get; }
    internal EntityId? Ally { get; }
    internal SqliteWorldAuthority Authority { get; }
    internal LocalWorldSession Host { get; }
    internal ManualSessionTimeProvider Time { get; } = new();
    internal EncounterJourneyWorldFixture Commands { get; } = new();
    internal WorldSnapshot State => Authority.ReadStateForHost();
    internal WorldControllerAssignment Control => State.Controllers!.Get(Actor);
    internal TrustedCommandDelivery Delivery(EntityId actor) => ConstructionUpgradeFixture.Delivery(Genesis, actor);

    internal static async Task<ConservativeControllerFixture> Open(bool party = false)
    {
        var fixture = new ConservativeControllerFixture(party);
        await fixture.Host.Player.DisposeAsync();
        await SessionFixture.Until(() => fixture.Time.HasPendingTimer);
        return fixture;
    }

    internal async Task<(PlayerMessageTransport Client, AuthenticatedPlayerChannel Channel)> Join(EntityId? actorId = null)
    {
        var pair = InMemoryPlayerTransport.CreatePair();
        _clients.Add(pair.Client);
        var credential = actorId is { } actor && actor != Actor
            ? _admission.Issue(actor, Delivery(actor), TimeSpan.FromHours(1)) : _credential;
        await ControllerTransportFixture.Send(pair.Client,
            PlayerHandshakeCodec.Hello(1, _admission.Compatibility, credential.Export()));
        var channel = await _admission.AcceptAsync(pair.Server);
        _channels.Add(channel);
        Assert.Equal(PlayerMessageKind.Welcome, (await ControllerTransportFixture.Receive(pair.Client)).Kind);
        return (pair.Client, channel);
    }

    internal async Task LoseTransport((PlayerMessageTransport Client, AuthenticatedPlayerChannel Channel) joined)
    {
        Task run = new PlayerCommandSession(joined.Channel, _networkTime).RunAsync(TestContext.Current.CancellationToken);
        await joined.Client.DisposeAsync();
        await ControllerTransportFixture.Finish(run);
        Assert.Equal(WorldControllerMode.ReconnectAllowance, State.Controllers!.Get(joined.Channel.Player.ActorId).Mode);
        Assert.Equal(LocalWorldRunState.Unavailable, joined.Channel.Player.ReadRunState());
    }

    internal async Task<LocalPlayerConnection> Connect(EntityId actor, bool ready = true)
    {
        var port = await Host.ConnectPlayerAsync(actor, Delivery(actor));
        _ports.Add(port);
        port.SetReady(ready);
        return port;
    }

    internal Task Tick(TimeSpan? elapsed = null) => LiveControllerHostTests.Tick(Time, elapsed);

    internal async Task Expire()
    {
        await Tick(TimeSpan.FromSeconds(30));
        Assert.Equal(WorldControllerMode.Autonomous, Control.Mode);
    }

    internal async Task Until(Func<bool> predicate, int maximumCutoffs = 8)
    {
        for (int cutoff = 0; !predicate(); cutoff++)
        {
            Assert.True(cutoff < maximumCutoffs, "The expected conservative control boundary must be bounded.");
            await Tick();
        }
    }

    internal async Task<WorldActionReceipt[]> ChooseWaits(params LocalPlayerConnection[] ports)
    {
        WorldSnapshot captured = State;
        Task<WorldActionReceipt>[] pending = ports.Select(port => port.SubmitAction(Commands.Wait(captured, port.ActorId))).ToArray();
        await Tick();
        var receipts = await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.All(receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
        return receipts;
    }

    private void Enter(EncounterScenarioDefinition scenario)
    {
        bool InReach(EntityId actor) => actor == Actor || State.Space.QueryInteraction(Actor, actor,
            scenario.EntryRangeSubunits) == SpatialInteraction.Reachable;
        var target = State.Space.GetActor(Opponent).Feet;
        var approach = new[] { target.Offset(-256, 0, 0), target.Offset(256, 0, 0),
                target.Offset(0, -256, 0), target.Offset(0, 256, 0) }
            .Select(feet => (Feet: feet, Route: State.Space.QueryGroundRoute(Actor, feet)))
            .Where(row => row.Route.Status == GroundRouteStatus.Reachable)
            .OrderBy(row => row.Route.Waypoints.Count).First().Feet;
        Walk(Actor, approach, () => InReach(Opponent));
        if (Ally is { } ally && !InReach(ally))
        {
            var center = State.Space.GetActor(Actor).Feet;
            var destination = (from x in new[] { -256L, 0L, 256L }
                from y in new[] { -256L, 0L, 256L }
                where x != 0 || y != 0
                let feet = center.Offset(x, y, 0)
                let route = State.Space.QueryGroundRoute(ally, feet)
                where route.Status == GroundRouteStatus.Reachable
                orderby route.Waypoints.Count, x, y
                select feet).First();
            Walk(ally, destination, () => State.Space.GetActor(ally).Feet == destination);
            Assert.True(InReach(ally));
        }
        var entered = Authority.Commit(Commands.Batch(State, WorldBatchKind.EncounterLifecycle,
            [Commands.Enter(State, scenario.DefinitionId)]));
        Assert.Equal(WorldActionRejection.None, Assert.Single(entered.Receipts).Rejection);
        for (int cutoff = 0; State.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Active; cutoff++)
        {
            Assert.True(cutoff < 3);
            Authority.Commit(Commands.Batch(State, WorldBatchKind.EncounterLifecycle));
        }
        Assert.True(WorldEncounterActions.HasRequiredDecisions(State));
        Assert.All(State.Pacing!.Encounter!.Actors.Where(row => row.Control == EncounterActorControl.RequiredHuman),
            row => Assert.Equal(WorldControllerMode.Human, State.Controllers!.Get(row.ActorId).Mode));
    }

    private void Walk(EntityId actor, SubunitPosition destination, Func<bool> arrived)
    {
        for (int step = 0; !arrived(); step++)
        {
            Assert.True(step < 512, "The calibrated human must assemble through actual bounded ground movement.");
            var route = State.Space.QueryGroundRoute(actor, destination);
            Assert.Equal(GroundRouteStatus.Reachable, route.Status);
            Assert.NotNull(route.NextWaypoint);
            var feet = State.Space.GetActor(actor).Feet;
            var waypoint = route.NextWaypoint!.Value;
            long dx = Math.Clamp(waypoint.X - feet.X, -32, 32);
            long dy = dx == 0 ? Math.Clamp(waypoint.Y - feet.Y, -32, 32) : 0;
            Authority.Commit(_walking.Batch(State, [], [new(Delivery(actor), actor, dx, dy)]));
            Assert.Equal(feet.Offset(dx, dy, 0), State.Space.GetActor(actor).Feet);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Host.DisposeAsync();
        foreach (var channel in _channels) await channel.DisposeAsync();
        foreach (var client in _clients) await client.DisposeAsync();
        foreach (var port in _ports) await port.DisposeAsync();
        _files.Dispose();
    }
}
