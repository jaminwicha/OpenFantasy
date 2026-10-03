using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Care;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Networking.ControllerTransport;
using OpenFantasy.Hosting.Tests.Simulation.Controllers;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.ActorSelection;

/// <summary>Four original humans; consent is journal preparation and possession requires real admission and loss.</summary>
internal sealed class ActorSelectionFixture : IAsyncDisposable
{
    private readonly SessionFiles _files = new();
    private readonly List<PlayerMessageTransport> _clients = [];
    private readonly List<AuthenticatedPlayerChannel> _channels = [];
    private bool _stopped;

    private ActorSelectionFixture()
    {
        var basis = WorldControllerAdmission.CreateFresh(CargoOutcomeWorldAdmission.CreateEmbedded(
            "actor-selection-four-player", ["Traveler", "Mira", "Joss", "Lark"]));
        var living = basis.Care!.Ordinary!.Definitions.First(value =>
            value.DeliveryMode == RestorativeDeliveryMode.LivingAssistance);
        var recovery = RestorativeUseDefinition.CreateIncapacitatedAssistance(new(0xc040aaaa00000000, 1),
            living.RemedyDefinitionId, living.RemedyVersion, living.SupportedForms);
        Genesis = WorldDelegatedControlAdmission.CreateFresh(WorldControlPermissionAdmission.CreateFresh(
            WorldCareDispositionAdmission.CreateFresh(WorldRecoveryAdmission.CreateFresh(basis, [recovery], []))));
        Actors = Genesis.Controllers!.Assignments.Select(value => value.ActorId).ToArray();
        Assert.Equal(4, Actors.Count);
        Authority = SqliteWorldAuthority.CreateNew(Path, Genesis);
        Grant();
        Host = new LocalWorldSession(Authority, Owner, Delivery(Owner), Time);
        Admission = new PlayerAdmissionService(Host, [], NetworkTime);
    }

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

    internal static async Task<ActorSelectionFixture> Open()
    {
        var fixture = new ActorSelectionFixture();
        await fixture.Host.Player.DisposeAsync();
        await SessionFixture.Until(() => fixture.Time.HasPendingTimer);
        return fixture;
    }

    internal TrustedCommandDelivery Delivery(EntityId actor)
    {
        var binding = Genesis.Inventory.Controls.Single(value => value.ActorId == actor);
        return new(binding.PrincipalId, binding.SessionId);
    }

    private void Grant()
    {
        var offer = Commands.Command(State, Owner,
            new OfferControlPermissionIntent(Grantee, 0, WorldControlCapabilities.All));
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
