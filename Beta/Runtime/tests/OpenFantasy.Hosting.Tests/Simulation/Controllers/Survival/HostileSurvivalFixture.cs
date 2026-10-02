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

namespace OpenFantasy.Hosting.Tests.Simulation.Controllers.Survival;

/// <summary>
/// Fresh control calibration changes only the second wolf into an unrelated human observer. The first
/// wolf remains an actual hostile NPC. Ordinary journaled movement and entry precede live session
/// combat; there are no admitted health, equipment or accepted-action substitutions.
/// </summary>
internal sealed class HostileSurvivalFixture : IAsyncDisposable
{
    private readonly SessionFiles _files = new();
    private readonly List<PlayerMessageTransport> _clients = [];
    private readonly List<AuthenticatedPlayerChannel> _channels = [];
    private readonly PlayerAdmissionService _admission;
    private readonly PlayerJoinCredential _credential;
    private readonly ManualSessionTimeProvider _networkTime = new();

    private HostileSurvivalFixture()
    {
        WorldSnapshot basis = RoadThreatWorldAdmission.CreateEmbedded("hostile-controller-survival", "Traveler");
        var scenario = basis.EncounterLifecycle!.Scenarios.Single(row => row.Key == EncounterJourneyAdmission.FirstScenarioKey);
        Actor = scenario.InitiatingActorId;
        Opponent = scenario.TargetActorId;
        Observer = basis.EncounterLifecycle.Scenarios.Single(row => row.Key == EncounterJourneyAdmission.SecondScenarioKey).TargetActorId;
        Genesis = WorldControllerAdmission.CreateFresh(WorldEncounterOutcomeAdmission.CreateCalibratedFresh(basis,
            new EncounterOutcomeCalibration(new Dictionary<EntityId, int>(), [Observer])));
        Authority = SqliteWorldAuthority.CreateNew(Path, Genesis);
        Enter(scenario);
        Host = new LocalWorldSession(Authority, Actor, Delivery(Actor), Time);
        _admission = new PlayerAdmissionService(Host, [], _networkTime);
        _credential = _admission.Issue(Actor, Delivery(Actor), TimeSpan.FromHours(1));
    }

    internal string Path => _files.Path;
    internal WorldSnapshot Genesis { get; }
    internal EntityId Actor { get; }
    internal EntityId Opponent { get; }
    internal EntityId Observer { get; }
    internal SqliteWorldAuthority Authority { get; }
    internal LocalWorldSession Host { get; }
    internal ManualSessionTimeProvider Time { get; } = new();
    internal EncounterJourneyWorldFixture Commands { get; } = new();
    internal List<EncounterActionResolution> Resolutions { get; } = [];
    internal List<(WorldActionCommand Command, WorldActionReceipt Receipt)> Waits { get; } = [];
    internal WorldSnapshot State => Authority.ReadStateForHost();
    internal WorldControllerAssignment Control => State.Controllers!.Get(Actor);
    internal TrustedCommandDelivery Delivery(EntityId actor) => ConstructionUpgradeFixture.Delivery(Genesis, actor);

    internal static async Task<HostileSurvivalFixture> Open()
    {
        var fixture = new HostileSurvivalFixture();
        await fixture.Host.Player.DisposeAsync();
        await SessionFixture.Until(() => fixture.Time.HasPendingTimer);
        return fixture;
    }

    internal async Task<(PlayerMessageTransport Client, AuthenticatedPlayerChannel Channel)> Join()
    {
        var pair = InMemoryPlayerTransport.CreatePair();
        _clients.Add(pair.Client);
        await ControllerTransportFixture.Send(pair.Client,
            PlayerHandshakeCodec.Hello(1, _admission.Compatibility, _credential.Export()));
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
        Assert.Equal(WorldControllerMode.ReconnectAllowance, Control.Mode);
        Assert.Equal(LocalWorldRunState.Unavailable, joined.Channel.Player.ReadRunState());
    }

    internal async Task<LocalPlayerConnection> ConnectObserver()
    {
        var observer = await Host.ConnectPlayerAsync(Observer, Delivery(Observer));
        observer.SetReady(true);
        return observer;
    }

    internal async Task Tick(TimeSpan? elapsed = null)
    {
        await LiveControllerHostTests.Tick(Time, elapsed);
        if (State.EncounterActions?.LastResolution is { } resolution
            && Resolutions.All(row => row.WorldSequence != resolution.WorldSequence))
            Resolutions.Add(resolution);
    }

    internal async Task Until(Func<bool> predicate, int maximumCutoffs, string boundary)
    {
        for (int cutoff = 0; !predicate(); cutoff++)
        {
            Assert.True(cutoff < maximumCutoffs, $"The actual hostile journey must reach {boundary} within its finite cutoffs.");
            await Tick();
        }
    }

    internal async Task EarnBiteAndHumanDecision(LocalPlayerConnection player)
    {
        player.SetReady(true);
        for (int cutoff = 0; ; cutoff++)
        {
            Assert.True(cutoff < 160, "A real wolf bite and the next human choice must be earned through bounded combat.");
            Assert.Equal(EncounterLifecyclePhase.Active, State.EncounterLifecycle!.Phase);
            bool due = WorldEncounterActions.DueActors(State).Any(row => row.ActorId == Actor)
                && State.Pacing!.Encounter!.Decisions.All(row => row.ActorId != Actor);
            if (due && Resolutions.Any(row => row.ActorId == Opponent && row.Status == EncounterActionResolutionStatus.Damaged))
                return;
            if (due)
            {
                var command = Commands.Wait(State, Actor);
                Task<WorldActionReceipt> pending = player.SubmitAction(command);
                await Tick();
                var receipt = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                Assert.Equal(WorldActionRejection.None, receipt.Rejection);
                Waits.Add((command, receipt));
            }
            else await Tick();
        }
    }

    private void Enter(EncounterScenarioDefinition scenario)
    {
        var walking = new ConstructionUpgradeFixture();
        EntityId biteId = scenario.Combatants.Single(row => row.ActorId == Opponent).AbilityIds[0];
        long biteRange = State.EncounterLifecycle!.Abilities.Single(row => row.DefinitionId == biteId).RangeSubunits;
        for (int sample = 0; State.Space.QueryInteraction(Actor, Opponent, biteRange) != SpatialInteraction.Reachable; sample++)
        {
            Assert.True(sample < 512, "The actor must reach the actual wolf through finite ordinary movement.");
            var here = State.Space.GetActor(Actor).Feet;
            var target = State.Space.GetActor(Opponent).Feet;
            long dx = Math.Sign(target.X - here.X) * WorldMovementInput.DisplacementPerStep;
            long dy = dx == 0 ? Math.Sign(target.Y - here.Y) * WorldMovementInput.DisplacementPerStep : 0;
            Authority.Commit(walking.Batch(State, [], [new(Delivery(Actor), Actor, dx, dy)]));
            Assert.Equal(here.Offset(dx, dy, 0), State.Space.GetActor(Actor).Feet);
        }
        var entered = Authority.Commit(Commands.Batch(State, WorldBatchKind.EncounterLifecycle,
            [Commands.Enter(State, scenario.DefinitionId)]));
        Assert.Equal(WorldActionRejection.None, Assert.Single(entered.Receipts).Rejection);
        for (int cutoff = 0; State.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Active; cutoff++)
        {
            Assert.True(cutoff < 3);
            Authority.Commit(Commands.Batch(State, WorldBatchKind.EncounterLifecycle));
        }
        Assert.Equal(ResidentControllerKind.Host, State.GetResident(Opponent).ControllerKind);
        Assert.Equal(EncounterActorControl.Automatic, State.Pacing!.Encounter!.GetActor(Opponent).Control);
        Assert.Equal((ushort)0, State.EncounterActions!.GetCombatant(Observer).Side);
    }

    public async ValueTask DisposeAsync()
    {
        await Host.DisposeAsync();
        foreach (var channel in _channels) await channel.DisposeAsync();
        foreach (var client in _clients) await client.DisposeAsync();
        _files.Dispose();
    }
}
