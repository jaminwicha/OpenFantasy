using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
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

namespace OpenFantasy.Hosting.Tests.Simulation.Controllers.Aftermath;

/// <summary>Declared fresh health/control calibration followed only by real movement, entry and combat.</summary>
internal sealed class AutonomousAftermathFixture : IAsyncDisposable
{
    private readonly SessionFiles _files = new();
    private readonly List<PlayerMessageTransport> _clients = [];
    private readonly List<AuthenticatedPlayerChannel> _channels = [];
    private readonly PlayerAdmissionService _admission;
    private readonly PlayerJoinCredential _credential;
    private readonly ManualSessionTimeProvider _networkTime = new();

    private AutonomousAftermathFixture(bool defeated, bool humanHelper, bool humanOpponent)
    {
        WorldSnapshot source = RoadThreatWorldAdmission.CreateEmbedded("autonomous-aftermath-calibration", "Traveler");
        var first = source.EncounterLifecycle!.Scenarios.Single(row => row.Key == EncounterJourneyAdmission.FirstScenarioKey);
        Actor = first.InitiatingActorId;
        Opponent = first.TargetActorId;
        Observer = source.EncounterLifecycle.Scenarios.Single(row => row.Key == EncounterJourneyAdmission.SecondScenarioKey).TargetActorId;
        Agreement = source.Care!.Agreements.Single(row => row.PatientActorId == Actor);
        List<EntityId> humans = [Observer];
        if (humanHelper) humans.Add(Agreement.HelperActorId);
        if (humanOpponent) humans.Add(Opponent);
        Genesis = WorldControllerAdmission.CreateFresh(WorldEncounterOutcomeAdmission.CreateCalibratedFresh(source,
            new EncounterOutcomeCalibration(new Dictionary<EntityId, int> { [defeated ? Actor : Opponent] = 1 },
                humans)));
        Authority = SqliteWorldAuthority.CreateNew(Path, Genesis);
        PrepareOutcome(first, defeated);
        Host = new LocalWorldSession(Authority, Actor, Delivery(Actor), Time);
        _admission = new PlayerAdmissionService(Host, [], _networkTime);
        _credential = _admission.Issue(Actor, Delivery(Actor), TimeSpan.FromHours(1));
    }

    internal string Path => _files.Path;
    internal WorldSnapshot Genesis { get; }
    internal EntityId Actor { get; }
    internal EntityId Opponent { get; }
    internal EntityId Observer { get; }
    internal CareAgreement Agreement { get; }
    internal SqliteWorldAuthority Authority { get; }
    internal LocalWorldSession Host { get; }
    internal ManualSessionTimeProvider Time { get; } = new();
    internal EncounterJourneyWorldFixture Commands { get; } = new();
    internal WorldSnapshot State => Authority.ReadStateForHost();
    internal WorldControllerMode Mode => State.Controllers!.Get(Actor).Mode;
    internal TrustedCommandDelivery Delivery(EntityId actor) => ConstructionUpgradeFixture.Delivery(Genesis, actor);

    internal static async Task<AutonomousAftermathFixture> Open(bool defeated = true, bool humanHelper = false, bool humanOpponent = false)
    {
        var fixture = new AutonomousAftermathFixture(defeated, humanHelper, humanOpponent);
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

    internal async Task Lose((PlayerMessageTransport Client, AuthenticatedPlayerChannel Channel) joined)
    {
        Task run = new PlayerCommandSession(joined.Channel, _networkTime).RunAsync(TestContext.Current.CancellationToken);
        await joined.Client.DisposeAsync();
        await ControllerTransportFixture.Finish(run);
        Assert.Equal(WorldControllerMode.ReconnectAllowance, Mode);
    }

    internal async Task<LocalPlayerConnection> ConnectObserver(bool ready = true)
    {
        var observer = await Host.ConnectPlayerAsync(Observer, Delivery(Observer));
        observer.SetReady(ready);
        return observer;
    }

    internal Task Tick(TimeSpan? elapsed = null) => LiveControllerHostTests.Tick(Time, elapsed);
    internal AcknowledgeCareIntent CareIntent() => new(Agreement.AgreementId, State.EncounterActions!.Scope.EncounterId,
        State.EncounterLifecycle!.Revision, State.EncounterActions.ContentHash);

    private void PrepareOutcome(EncounterScenarioDefinition scenario, bool defeated)
    {
        var movement = new ConstructionUpgradeFixture();
        for (int step = 0; State.Space.QueryInteraction(Actor, Opponent) != SpatialInteraction.Reachable; step++)
        {
            Assert.True(step < 512);
            var here = State.Space.GetActor(Actor).Feet;
            var target = State.Space.GetActor(Opponent).Feet;
            long dx = Math.Sign(target.X - here.X) * WorldMovementInput.DisplacementPerStep;
            long dy = dx == 0 ? Math.Sign(target.Y - here.Y) * WorldMovementInput.DisplacementPerStep : 0;
            Authority.Commit(movement.Batch(State, [], [new(Delivery(Actor), Actor, dx, dy)]));
            Assert.Equal(here.Offset(dx, dy, 0), State.Space.GetActor(Actor).Feet);
        }
        Commit(WorldBatchKind.EncounterLifecycle, [Commands.Enter(State, scenario.DefinitionId)]);
        for (int step = 0; State.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Active; step++)
        {
            Assert.True(step < 3);
            Commit(WorldBatchKind.EncounterLifecycle);
        }
        var npc = new LocalEncounterController();
        for (int step = 0; State.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Outcome; step++)
        {
            Assert.True(step < 800);
            if (WorldEncounterActions.HasRequiredDecisions(State))
            {
                var choices = WorldEncounterActions.DueActors(State).Where(actor => actor.Control == EncounterActorControl.RequiredHuman
                    && !State.Pacing!.Encounter!.Decisions.Any(choice => choice.ActorId == actor.ActorId))
                    .Select(actor => actor.ActorId == Actor
                        ? defeated ? Commands.Wait(State, Actor) : Commands.Attack(State, Actor, Opponent,
                            State.EncounterActions!.GetCombatant(Actor).AbilityIds[0])
                        : defeated ? Commands.Attack(State, actor.ActorId, Actor,
                            State.EncounterActions!.GetCombatant(actor.ActorId).AbilityIds[0]) : Commands.Wait(State, actor.ActorId)).ToArray();
                Commit(WorldBatchKind.EncounterDecision, choices);
            }
            var automatic = npc.SelectChoices(State, Commands.NextId);
            if (automatic.Count != 0) Commit(WorldBatchKind.EncounterDecision, automatic);
            Commit(WorldEncounterActions.DueActors(State).Count == 0 ? WorldBatchKind.TacticalAdvance : WorldBatchKind.EncounterResolution);
        }
        Assert.Equal(defeated, State.Progression!.GetActor(Actor).CurrentHp == 0);
        Assert.Null(State.Care!.Pending);
        Assert.Empty(State.EncounterOutcome!.Acknowledgments);
    }

    private void Commit(WorldBatchKind kind, IEnumerable<WorldActionCommand>? commands = null)
    {
        var committed = Authority.Commit(Commands.Batch(State, kind, commands));
        Assert.All(committed.Receipts, receipt => Assert.Equal(WorldActionRejection.None, receipt.Rejection));
    }

    public async ValueTask DisposeAsync()
    {
        await Host.DisposeAsync();
        foreach (var channel in _channels) await channel.DisposeAsync();
        foreach (var client in _clients) await client.DisposeAsync();
        _files.Dispose();
    }
}
