using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Tests.Worlds.Controllers;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Networking.ControllerTransport;

internal sealed class ControllerTransportFixture : IAsyncDisposable
{
    private static readonly Lazy<WorldSnapshot> Basis = new(() =>
        CargoOutcomeWorldAdmission.CreateEmbedded("controller-transport", "Traveler"));
    private readonly SessionFiles _files = new();
    private readonly List<PlayerMessageTransport> _clients = [];
    private readonly List<AuthenticatedPlayerChannel> _channels = [];
    internal static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private ControllerTransportFixture(bool legacy, TimeProvider? admissionTime, TimeSpan? credentialLifetime)
    {
        Genesis = legacy ? Basis.Value : WorldControllerAdmission.CreateFresh(Basis.Value);
        Actor = Genesis.Residents.Single(row => row.ControllerKind == ResidentControllerKind.Player).ActorId;
        var control = Genesis.Inventory.Controls.Single(row => row.ActorId == Actor);
        Delivery = new(control.PrincipalId, control.SessionId);
        Authority = SqliteWorldAuthority.CreateNew(_files.Path, Genesis);
        Host = new LocalWorldSession(Authority, Actor, Delivery, HostTime);
        Admission = new PlayerAdmissionService(Host, [], admissionTime ?? NetworkTime);
        Credential = Admission.Issue(Actor, Delivery, credentialLifetime ?? TimeSpan.FromHours(1));
    }

    internal WorldSnapshot Genesis { get; }
    internal EntityId Actor { get; }
    internal TrustedCommandDelivery Delivery { get; }
    internal SqliteWorldAuthority Authority { get; }
    internal LocalWorldSession Host { get; }
    internal PlayerAdmissionService Admission { get; }
    internal PlayerJoinCredential Credential { get; }
    internal ManualSessionTimeProvider HostTime { get; } = new();
    internal ManualSessionTimeProvider NetworkTime { get; } = new();
    internal CancellationTokenSource Stop { get; } = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
    internal CancellationTokenSource RequestAborted { get; } = new();

    internal static async Task<ControllerTransportFixture> Open(bool legacy = false, TimeProvider? admissionTime = null,
        TimeSpan? credentialLifetime = null)
    {
        var fixture = new ControllerTransportFixture(legacy, admissionTime, credentialLifetime);
        await fixture.Host.Player.DisposeAsync();
        return fixture;
    }

    internal async Task<(PlayerMessageTransport Client, AuthenticatedPlayerChannel Channel)> Connect(
        Func<PlayerMessageTransport, PlayerMessageTransport>? wrap = null)
    {
        var pair = InMemoryPlayerTransport.CreatePair();
        return await Connect(pair.Client, wrap?.Invoke(pair.Server) ?? pair.Server);
    }

    internal async Task<(PlayerMessageTransport Client, AuthenticatedPlayerChannel Channel)> Connect(
        PlayerMessageTransport client, PlayerMessageTransport server)
    {
        _clients.Add(client);
        await Send(client, PlayerHandshakeCodec.Hello(1, Admission.Compatibility, Credential.Export()));
        AuthenticatedPlayerChannel channel = await Admission.AcceptAsync(server, Stop.Token, RequestAborted.Token);
        _channels.Add(channel);
        Assert.Equal(PlayerMessageKind.Welcome, (await Receive(client)).Kind);
        Assert.Equal(LocalWorldRunState.Paused, channel.Player.ReadRunState());
        Assert.False(channel.Player.Ready);
        return (client, channel);
    }

    internal Task Run(AuthenticatedPlayerChannel channel) =>
        new PlayerCommandSession(channel, NetworkTime).RunAsync(Stop.Token, RequestAborted.Token);

    internal static async Task Send(PlayerMessageTransport transport, PlayerMessage message) =>
        await transport.SendAsync(PlayerMessageCodec.Encode(message), Cancellation);

    internal static async Task<PlayerMessage> Receive(PlayerMessageTransport transport)
    {
        byte[]? bytes = await transport.ReceiveAsync(Cancellation).AsTask().WaitAsync(TimeSpan.FromSeconds(5), Cancellation);
        Assert.NotNull(bytes);
        return PlayerMessageCodec.Decode(bytes);
    }

    internal static Task Finish(Task run) => run.WaitAsync(TimeSpan.FromSeconds(5), Cancellation);

    internal async Task AssertDisconnected(WorldControllerDisconnectReason reason)
    {
        WorldSnapshot state = Authority.ReadStateForHost();
        Assert.Equal(1UL, state.Revision);
        Assert.Equal(0UL, state.AdvancingSteps);
        Assert.Equal(WorldControllerMode.ReconnectAllowance, state.Controllers!.Get(Actor).Mode);
        ControllerJournalTests.AssertGameplayUnchanged(Genesis, state);
        Assert.Equal(0UL, Authority.InspectForHost().Journal.CommandOutcomeCount);
        await Host.StopAsync();
        using (var database = WorldDatabase.Open(_files.Path, false))
        {
            var commit = Assert.Single(database.ReadJournal());
            WorldStepBatch batch = WorldStepCodec.Decode(commit.AcceptedBatch);
            Assert.Equal(WorldBatchKind.ControllerLifecycle, batch.Kind);
            Assert.Equal(Actor, batch.ControllerEvent!.ActorId);
            Assert.Equal(reason, batch.ControllerEvent.Reason);
        }
        using var reopened = SqliteWorldAuthority.OpenExisting(_files.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(state), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
    }

    internal void AssertUnchanged() => Assert.Equal(Genesis.ContentHash, Authority.ReadStateForHost().ContentHash);

    internal async Task AssertUnchangedAfterStop()
    {
        await Host.StopAsync();
        using var reopened = SqliteWorldAuthority.OpenExisting(_files.Path);
        Assert.Equal(WorldSnapshotCodec.Encode(Genesis), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
    }

    public async ValueTask DisposeAsync()
    {
        Stop.Cancel();
        foreach (var channel in _channels) await channel.DisposeAsync();
        foreach (var client in _clients) await client.DisposeAsync();
        await Host.DisposeAsync();
        Stop.Dispose();
        RequestAborted.Dispose();
        _files.Dispose();
    }
}

internal sealed class ControlledControllerTransport(PlayerMessageTransport inner,
    Func<byte[], CancellationToken, ValueTask>? send = null,
    Func<CancellationToken, ValueTask<byte[]?>>? receive = null)
    : PlayerMessageTransport(PlayerMessageCodec.MaximumMessageBytes)
{
    protected override ValueTask SendMessageAsync(byte[] message, CancellationToken cancellationToken) =>
        send is null ? inner.SendAsync(message, cancellationToken) : send(message, cancellationToken);
    protected override ValueTask<byte[]?> ReceiveMessageAsync(CancellationToken cancellationToken) =>
        receive is null ? inner.ReceiveAsync(cancellationToken) : receive(cancellationToken);
    protected override void CloseTransport() => inner.DisposeAsync().GetAwaiter().GetResult();
}
