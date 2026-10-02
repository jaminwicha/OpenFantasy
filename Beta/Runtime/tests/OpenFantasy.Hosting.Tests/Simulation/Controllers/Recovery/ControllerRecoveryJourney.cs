using System.Buffers.Binary;
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

namespace OpenFantasy.Hosting.Tests.Simulation.Controllers.Recovery;

/// <summary>
/// Explicit recoverable storage over a fresh calibrated world. Both wolves are human controlled,
/// permitting genuine accepted player work to survive while the opposing human decision holds.
/// Every opening owns an independent monotonic clock; UTC can change without advancing that clock.
/// </summary>
internal sealed class ControllerRecoveryJourney : IAsyncDisposable
{
    private static readonly Lazy<WorldSnapshot> ControllerGenesis = new(CreateGenesis);
    private readonly SessionFiles _files = new();
    private readonly List<PlayerMessageTransport> _clients = [];
    private readonly List<AuthenticatedPlayerChannel> _channels = [];
    private readonly ManualSessionTimeProvider _networkTime = new();
    private PlayerAdmissionService _admission = null!;
    private PlayerJoinCredential _credential = null!;
    private bool _stopped;

    private ControllerRecoveryJourney(ushort profile, bool combat)
    {
        Genesis = profile switch
        {
            36 => ControllerGenesis.Value,
            37 => CreateRecoveryGenesis(),
            38 => WorldCareDispositionAdmission.CreateFresh(CreateRecoveryGenesis()),
            _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };
        var scenario = Genesis.EncounterLifecycle!.Scenarios.Single(row => row.Key == EncounterJourneyAdmission.FirstScenarioKey);
        Actor = scenario.InitiatingActorId;
        Opponent = scenario.TargetActorId;
        Authority = SqliteWorldAuthority.CreateRecoverable(Path, Genesis);
        if (combat) Enter(scenario);
        Start(new RecoverySessionClock());
    }

    internal string Path => _files.Path;
    internal WorldSnapshot Genesis { get; }
    internal EntityId Actor { get; }
    internal EntityId Opponent { get; }
    internal SqliteWorldAuthority Authority { get; private set; }
    internal LocalWorldSession Host { get; private set; } = null!;
    internal RecoverySessionClock Time { get; private set; } = null!;
    internal EncounterJourneyWorldFixture Commands { get; } = new();
    internal WorldSnapshot State => Authority.ReadStateForHost();
    internal WorldControllerAssignment Control => State.Controllers!.Get(Actor);
    internal TrustedCommandDelivery Delivery(EntityId actor) => ConstructionUpgradeFixture.Delivery(Genesis, actor);

    internal static async Task<ControllerRecoveryJourney> Open(ushort profile = 36, bool combat = false)
    {
        var journey = new ControllerRecoveryJourney(profile, combat);
        await journey.RemoveInitialPort();
        return journey;
    }

    internal async Task Reopen(RecoverySessionClock clock)
    {
        Assert.True(_stopped, "An opening follows a completed shutdown; it never races an existing writer.");
        Authority = SqliteWorldAuthority.OpenExisting(Path, WorldSnapshotCodec.Encode(Genesis));
        Start(clock);
        await RemoveInitialPort();
    }

    private void Start(RecoverySessionClock clock)
    {
        Assert.True(Authority.SupportsControllerRecovery);
        Time = clock;
        Host = new LocalWorldSession(Authority, Actor, Delivery(Actor), Time);
        _admission = new PlayerAdmissionService(Host, [], _networkTime);
        _credential = _admission.Issue(Actor, Delivery(Actor), TimeSpan.FromHours(1));
        _stopped = false;
    }

    private async Task RemoveInitialPort()
    {
        await Host.Player.DisposeAsync();
        await SessionFixture.Until(() => Time.Monotonic.HasPendingTimer);
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
        Assert.Equal(WorldControllerMode.ReconnectAllowance, Control.Mode);
        Assert.Equal(LocalWorldRunState.Unavailable, joined.Channel.Player.ReadRunState());
    }

    internal Task Tick(double milliseconds = 50) => LiveControllerHostTests.Tick(Time.Monotonic, TimeSpan.FromMilliseconds(milliseconds));

    internal ControllerAllowanceProgress Allowance(ulong elapsed)
    {
        var row = Assert.Single(Authority.ReadControllerAllowancesForHost());
        Assert.Equal(Actor, row.ActorId);
        Assert.Equal(Control.Revision, row.ControlRevision);
        Assert.Equal(Control.DisconnectId, row.DisconnectId);
        Assert.Equal(elapsed, row.ElapsedHostMilliseconds);
        return row;
    }

    internal async Task<(WorldActionCommand Command, WorldActionReceipt Receipt)> Wait(LocalPlayerConnection port)
    {
        port.SetReady(true);
        var command = Commands.Wait(State, port.ActorId);
        Task<WorldActionReceipt> pending = port.SubmitAction(command);
        await Tick();
        var receipt = await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(WorldActionRejection.None, receipt.Rejection);
        return (command, receipt);
    }

    internal async Task Stop(TimeSpan? advanceAfterRequest = null)
    {
        if (_stopped) return;
        Task stopped = Host.StopAsync();
        if (advanceAfterRequest is { } after) Time.Monotonic.Advance(after);
        await stopped;
        _stopped = true;
    }

    internal void AssertStoppedAllowance(ulong elapsed)
    {
        Assert.True(_stopped);
        using var authority = SqliteWorldAuthority.OpenExisting(Path, WorldSnapshotCodec.Encode(Genesis));
        Assert.Equal(elapsed, Assert.Single(authority.ReadControllerAllowancesForHost()).ElapsedHostMilliseconds);
    }

    internal WorldStepBatch[] StoppedBatches()
    {
        Assert.True(_stopped);
        using var database = WorldDatabase.Open(Path, false);
        Assert.Equal(WorldSnapshotCodec.Encode(Genesis), database.ReadGenesis());
        return database.ReadJournal().Select(row => WorldStepCodec.Decode(row.AcceptedBatch)).ToArray();
    }

    internal static void AssertGameplayUnchanged(WorldSnapshot before, WorldSnapshot after)
    {
        Assert.Equal(before.ProfileVersion, after.ProfileVersion);
        int expectedFields = before.ProfileVersion switch
        {
            36 => 27,
            37 => 28,
            38 => 29,
            _ => throw new ArgumentException("This comparison requires an explicit controller-world profile.", nameof(before))
        };
        byte[][] original = Fields(WorldSnapshotCodec.Encode(before));
        byte[][] current = Fields(WorldSnapshotCodec.Encode(after));
        Assert.Equal(expectedFields, original.Length);
        Assert.Equal(expectedFields, current.Length);
        // External events may change only the journal revision, controller owner and lifecycle
        // boundary revision. Recovery and disposition fields remain exact alongside every older facet.
        for (int field = 0; field < expectedFields; field++)
            if (field is not 8 and not 21 and not 26) Assert.Equal(original[field], current[field]);
        byte[][] originalLifecycle = Fields(original[21]);
        byte[][] currentLifecycle = Fields(current[21]);
        Assert.Equal(17, originalLifecycle.Length);
        Assert.Equal(17, currentLifecycle.Length);
        for (int field = 0; field < originalLifecycle.Length; field++)
            if (field != 5) Assert.Equal(originalLifecycle[field], currentLifecycle[field]);
        Assert.Equal(before.Revision, before.EncounterLifecycle!.Revision);
        Assert.Equal(after.Revision, after.EncounterLifecycle!.Revision);
    }

    private static byte[][] Fields(byte[] bytes)
    {
        int count = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(8)));
        int offset = 12;
        var fields = new byte[count][];
        for (int index = 0; index < count; index++)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset)));
            offset += 4;
            fields[index] = bytes.AsSpan(offset, length).ToArray();
            offset += length;
        }
        Assert.Equal(bytes.Length, offset);
        return fields;
    }

    private static WorldSnapshot CreateGenesis()
    {
        WorldSnapshot basis = RoadThreatWorldAdmission.CreateEmbedded("controller-recovery-journey", "Traveler");
        var first = basis.EncounterLifecycle!.Scenarios.Single(row => row.Key == EncounterJourneyAdmission.FirstScenarioKey);
        var second = basis.EncounterLifecycle.Scenarios.Single(row => row.Key == EncounterJourneyAdmission.SecondScenarioKey);
        return WorldControllerAdmission.CreateFresh(WorldEncounterOutcomeAdmission.CreateCalibratedFresh(basis,
            new EncounterOutcomeCalibration(new Dictionary<EntityId, int>(), [first.TargetActorId, second.TargetActorId])));
    }

    private static WorldSnapshot CreateRecoveryGenesis()
    {
        WorldSnapshot basis = ControllerGenesis.Value;
        var living = basis.Care!.Ordinary!.Definitions.Single(row => row.DeliveryMode == RestorativeDeliveryMode.LivingAssistance);
        var definition = RestorativeUseDefinition.CreateIncapacitatedAssistance(new EntityId(0xeaca380000000000, 1),
            living.RemedyDefinitionId, living.RemedyVersion, living.SupportedForms);
        // Fresh policy selection grants no treatment permission and adds no medicine or health.
        return WorldRecoveryAdmission.CreateFresh(basis, [definition], []);
    }

    private void Enter(EncounterScenarioDefinition scenario)
    {
        var walking = new ConstructionUpgradeFixture();
        for (int sample = 0; State.Space.QueryInteraction(Actor, Opponent) != SpatialInteraction.Reachable; sample++)
        {
            Assert.True(sample < 512);
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
    }

    public async ValueTask DisposeAsync()
    {
        await Stop();
        foreach (var channel in _channels) await channel.DisposeAsync();
        foreach (var client in _clients) await client.DisposeAsync();
        _files.Dispose();
    }
}

internal sealed class RecoverySessionClock : TimeProvider
{
    internal ManualSessionTimeProvider Monotonic { get; } = new();
    internal DateTimeOffset WallTime { get; set; } = DateTimeOffset.UnixEpoch;
    public override long TimestampFrequency => Monotonic.TimestampFrequency;
    public override long GetTimestamp() => Monotonic.GetTimestamp();
    public override DateTimeOffset GetUtcNow() => WallTime;
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        Monotonic.CreateTimer(callback, state, dueTime, period);
}
