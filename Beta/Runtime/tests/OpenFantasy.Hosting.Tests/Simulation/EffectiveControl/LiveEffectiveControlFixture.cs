using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Networking.ControllerTransport;
using OpenFantasy.Hosting.Tests.Simulation.Controllers;
using OpenFantasy.Hosting.Tests.Worlds.EffectiveControl;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.EffectiveControl;

/// <summary>Consent is trusted journal preparation; possession uses actual credential admission and transport loss.</summary>
internal sealed class LiveEffectiveControlFixture : IAsyncDisposable
{
    private readonly SessionFiles _files = new();
    private readonly List<PlayerMessageTransport> _clients = [];
    private readonly List<AuthenticatedPlayerChannel> _channels = [];
    private bool _stopped;
    internal EffectiveControlFixture Source { get; } = new();
    internal ManualSessionTimeProvider Time { get; } = new();
    internal ManualSessionTimeProvider NetworkTime { get; } = new();
    internal SqliteWorldAuthority Authority { get; }
    internal LocalWorldSession Host { get; }
    internal PlayerAdmissionService Admission { get; }
    internal string Path => _files.Path;
    internal WorldSnapshot Genesis => Source.Genesis;
    internal WorldSnapshot State => Authority.ReadStateForHost();
    internal EntityId Owner => Source.Owner;
    internal EntityId Grantee => Source.Grantee;

    private LiveEffectiveControlFixture(bool combatApproach)
    {
        Authority = SqliteWorldAuthority.CreateNew(Path, Genesis);
        if (combatApproach)
        {
            var scenario = State.EncounterLifecycle!.GetScenario(Source.Journey.FirstScenario);
            Source.Journey.WalkTo(Authority, Owner, Source.Journey.Beside(State, Owner, scenario.TargetActorId));
        }
        Source.Grant(Authority, WorldControlCapabilities.All);
        Host = new LocalWorldSession(Authority, Owner, Source.Delivery(Owner), Time);
        Admission = new PlayerAdmissionService(Host, [], NetworkTime);
    }

    internal static async Task<LiveEffectiveControlFixture> Open(bool combatApproach = false)
    {
        var fixture = new LiveEffectiveControlFixture(combatApproach);
        await fixture.Host.Player.DisposeAsync();
        await SessionFixture.Until(() => fixture.Time.HasPendingTimer);
        return fixture;
    }

    internal async Task<(PlayerMessageTransport Client, AuthenticatedPlayerChannel Channel)> Join(EntityId actor)
    {
        var credential = Admission.Issue(actor, Source.Delivery(actor), TimeSpan.FromHours(1));
        var pair = InMemoryPlayerTransport.CreatePair();
        _clients.Add(pair.Client);
        await ControllerTransportFixture.Send(pair.Client, PlayerHandshakeCodec.Hello(1, Admission.Compatibility, credential.Export()));
        var channel = await Admission.AcceptAsync(pair.Server, TestContext.Current.CancellationToken);
        _channels.Add(channel);
        Assert.Equal(PlayerMessageKind.Welcome, (await ControllerTransportFixture.Receive(pair.Client)).Kind);
        Assert.False(channel.Player.Ready);
        return (pair.Client, channel);
    }

    internal async Task Lose((PlayerMessageTransport Client, AuthenticatedPlayerChannel Channel) joined)
    {
        Task running = new PlayerCommandSession(joined.Channel, NetworkTime).RunAsync(TestContext.Current.CancellationToken);
        await joined.Client.DisposeAsync();
        await ControllerTransportFixture.Finish(running);
        Assert.Equal(LocalWorldRunState.Unavailable, joined.Channel.Player.ReadRunState());
    }

    internal Task Tick(double milliseconds = 50) => LiveControllerHostTests.Tick(Time, TimeSpan.FromMilliseconds(milliseconds));

    internal LocalControlAcquisition Capture()
    {
        var state = State;
        var grant = state.ControlPermissions!.Find(Owner, Grantee)!;
        var owner = state.Controllers!.Get(Owner);
        return new(Owner, grant.GrantCommandId!.Value, grant.AcceptanceCommandId!.Value, grant.Revision,
            grant.Terms!.Capabilities, grant.TermsHash!, state.EffectiveControl!.Find(Owner)?.Revision ?? 0,
            owner.DisconnectId ?? Source.Commands.NextId(), owner.Revision);
    }

    internal async Task<(AuthenticatedPlayerChannel Grantee, WorldControlWitness Witness)> AcquireAfterRealLoss()
    {
        var owner = await Join(Owner);
        var grantee = await Join(Grantee);
        await Lose(owner);
        await Tick(30_000);
        Assert.Equal(WorldControllerMode.Autonomous, State.Controllers!.Get(Owner).Mode);
        var witness = await grantee.Channel.Player.AcquireControlAsync(Capture());
        return (grantee.Channel, witness);
    }

    internal WorldSnapshot EnterEncounter()
    {
        // The host timer is stopped at its pending cutoff and all ports are unready. These are explicitly
        // trusted authority preparations of real paid work, not selected-port or wire gameplay commands.
        var state = State;
        var entered = Source.Delegated(state, Source.Commands.Enter(state, Source.Journey.FirstScenario).Intent);
        Source.Apply(Authority, WorldBatchKind.EncounterLifecycle, entered);
        for (int step = 0; State.EncounterLifecycle!.Phase != EncounterLifecyclePhase.Active; step++)
        {
            Assert.True(step < 3);
            Authority.Commit(Source.Commands.Batch(State, WorldBatchKind.EncounterLifecycle));
        }
        return EffectiveControlCombatTests.ReachOwnerDecision(Authority, Source);
    }

    internal (WorldActionCommand Command, EncounterAbilityDefinition Ability, int Mp, int TargetHp) QueueAttack()
    {
        var state = EnterEncounter();
        var target = state.EncounterLifecycle!.GetScenario(Source.Journey.FirstScenario).TargetActorId;
        var ability = state.EncounterActions!.GetCombatant(Owner).AbilityIds.Select(state.EncounterActions.GetAbility)
            .Single(value => value.Key == EncounterFarmAdmission.StrikeKey);
        var command = Source.Delegated(state, new QueueEncounterAbilityIntent(state.Pacing!.Encounter!.EncounterId,
            state.Pacing.Encounter.Revision, ability.DefinitionId, target));
        Source.Apply(Authority, WorldBatchKind.EncounterDecision, command);
        Assert.Contains(State.EncounterActions!.Pending, value => value.ContentHash == command.ContentHash);
        return (command, ability, state.Progression!.GetActor(Owner).CurrentMp, state.Progression.GetActor(target).CurrentHp);
    }

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
            // A completed world stop already released durable custody and closed the authority.
            // Only transport cleanup remains; no port operation should re-enter that disposed owner.
            if (_stopped) await channel.Transport.DisposeAsync();
            else await channel.DisposeAsync();
        }
        foreach (var client in _clients) await client.DisposeAsync();
        await Stop();
        await Host.DisposeAsync();
        _files.Dispose();
    }
}
