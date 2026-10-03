using OpenFantasy.Core;
using OpenFantasy.Core.Content;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Progression.ResourceHistory;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Networking;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.RoadThreat;
using OpenFantasy.Hosting.Tests.Networking.ControllerTransport;
using OpenFantasy.Hosting.Tests.Simulation.Controllers;
using OpenFantasy.Hosting.Tests.Care;
using OpenFantasy.Hosting.Tests.Worlds.ControllerPermissions;
using OpenFantasy.Hosting.Tests.Worlds.EffectiveControl;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.SelectedQueries;

/// <summary>A fresh helper market with unchanged finite stock; travel, consent, loss and takeover are real journal work.</summary>
internal sealed class SelectedQueryTradingFixture : IAsyncDisposable
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
    internal MarketBinding Market { get; }
    internal WorldSnapshot Genesis { get; }
    internal WorldSnapshot State => Authority.ReadStateForHost();
    internal string Path => _files.Path;

    private SelectedQueryTradingFixture(bool keepGranteeNearHelper)
    {
        (Genesis, Market) = CreateHelperMarketGenesis();
        Authority = SqliteWorldAuthority.CreateNew(Path, Genesis);
        var approach = Source.Journey.Beside(State, Source.Owner, Market.MerchantActorId);
        Source.Journey.WalkTo(Authority, Source.Owner, approach);
        Assert.Equal(approach, State.Space.GetActor(Source.Owner).Feet);
        Assert.Equal(SpatialInteraction.Reachable, State.Space.QueryInteraction(Source.Owner, Market.MerchantActorId));
        Assert.Equal(SpatialInteraction.Reachable, State.Space.QueryInteraction(Market.MerchantActorId, Source.Owner));
        for (int step = 0; !WorldActorAvailability.CanAct(State, Source.Owner)
            || !WorldActorAvailability.CanAct(State, Market.MerchantActorId); step++)
        {
            Assert.True(step < 64);
            Source.Journey.Commit(Authority, WorldBatchKind.AdvancingStep);
        }
        if (!keepGranteeNearHelper) MoveGranteeBeyondHelperEncounter();
        Source.Grant(Authority, WorldControlCapabilities.Trade | WorldControlCapabilities.PrivateHistory);
        Assert.Equal(Market.MarketId, WorldMarketLocality.FindAvailable(State, Source.Owner, Market.MarketId)!.MarketId);
        if (!keepGranteeNearHelper) Assert.Null(LocalRoadThreatController.SelectPlayerEntry(State, Source.Commands.NextId));
        Host = new(Authority, Source.Owner, Source.Delivery(Source.Owner), Time,
            trading: new LocalTradingConfiguration([new LocalMerchantPreference(Market.MerchantActorId)]));
        Admission = new(Host, [], NetworkTime);
    }

    private void MoveGranteeBeyondHelperEncounter()
    {
        var entry = Assert.IsType<WorldActionCommand>(LocalRoadThreatController.SelectPlayerEntry(State, Source.Commands.NextId));
        var intent = Assert.IsType<EnterEncounterIntent>(entry.Intent);
        Assert.Equal(Source.Journey.LaterScenario, intent.ScenarioId);
        Assert.Equal(Source.Other, entry.ActorId);
        var scenario = State.EncounterLifecycle!.GetScenario(intent.ScenarioId);
        Assert.Equal(Source.Grantee, scenario.TargetActorId);
        var original = State.Space.GetActor(Source.Grantee).Feet;
        long distance = 4 * SubunitPosition.SubunitsPerCell;
        var goal = new[] { original.Offset(distance, 0, 0), original.Offset(-distance, 0, 0),
            original.Offset(0, distance, 0), original.Offset(0, -distance, 0) }
            .First(feet => State.Space.QueryPointInteraction(Source.Other, feet, scenario.EntryRangeSubunits)
                    != SpatialInteraction.Reachable
                && State.Space.QueryGroundRoute(Source.Grantee, feet).Status == GroundRouteStatus.Reachable);
        // This human retains the original hostile scenario. Its own ordinary movement leaves the helper's entry range.
        Source.Journey.WalkTo(Authority, Source.Grantee, goal);
        Assert.Equal(goal, State.Space.GetActor(Source.Grantee).Feet);
        Assert.NotEqual(original, goal);
        for (int step = 0; new[] { Source.Owner, Source.Grantee, Source.Other }
            .Any(actor => !WorldActorAvailability.CanAct(State, actor)); step++)
        {
            Assert.True(step < 64);
            Source.Journey.Commit(Authority, WorldBatchKind.AdvancingStep);
        }
        Assert.NotEqual(SpatialInteraction.Reachable,
            State.Space.QueryInteraction(Source.Other, Source.Grantee, scenario.EntryRangeSubunits));
        Assert.False(LocalRoadThreatController.CanInitiate(State, scenario));
    }

    private (WorldSnapshot Genesis, MarketBinding Market) CreateHelperMarketGenesis()
    {
        var basis = new PermissionStorageFixture().Genesis;
        var recipe = basis.Production!.Recipes.Single(value => value.PhysicalRecipe.Key == CareJourneyAdmission.RecipeKey);
        var berries = recipe.ItemFor(Assert.Single(recipe.Inputs));
        Assert.Equal(ResidentControllerKind.Host, basis.GetResident(Source.Other).ControllerKind);
        var market = MarketCircuitPolicies.BindResident(basis, new(0x5e1ec7ed00000000, 1), Source.Other,
            berries, MarketCircuitPolicies.BerryStall());
        ulong originalStock = market.CountUsableStock(basis.Inventory, basis.Production.Lots);
        Assert.True(originalStock > 0);
        var markets = new WorldMarketBook(basis.Coordination!.Markets!.Markets.Append(market).OrderBy(value => value.MarketId), [], []);
        var admission = ContentHash.Compute(basis.ContentHash.ToByteArray().Concat(markets.ContentHash.ToByteArray()).ToArray());
        // Only the unplayed catalog gains a market, following the existing co-player trade calibration pattern.
        // The helper retains its original berries, body, purse and permanent controller; nothing is replenished.
        var fields = WorldResourceJourneyTests.ReadFrame(WorldSnapshotCodec.Encode(basis));
        var coordination = WorldResourceJourneyTests.ReadFrame(fields[22]);
        coordination[7] = WorldMarketCodec.Encode(markets);
        fields[22] = WorldResourceJourneyTests.WriteFrame(coordination);
        var marketBasis = WorldSnapshotCodec.Decode(WorldResourceJourneyTests.WriteFrame(fields));
        var authored = new WorldSnapshot(basis.Inventory, basis.Space, basis.Residents, admission, basis.Calendar, 0, 0,
            basis.Dialogues, basis.Gathering, basis.Production, basis.Progression, basis.Quests, basis.Construction,
            reservoirs: basis.Reservoirs, reservoirSeals: basis.ReservoirSeals, liquidCells: basis.LiquidCells,
            excavation: basis.Excavation, pacing: basis.Pacing, encounterLifecycle: basis.EncounterLifecycle,
            care: basis.Care, resourceHistory: new WorldResourceHistory(admission,
                new ActorResourceHistory(basis.Progression!, 0, []), []),
            coordination: marketBasis.Coordination, retreat: basis.Retreat, encounterOutcome: basis.EncounterOutcome,
            controllers: basis.Controllers, recovery: basis.Recovery, controlPermissions: basis.ControlPermissions);
        Assert.Equal(basis.Inventory.ContentHash, authored.Inventory.ContentHash);
        Assert.Same(basis.Space, authored.Space);
        Assert.Equal(basis.Progression!.ContentHash, authored.Progression!.ContentHash);
        Assert.Equal(originalStock, market.CountUsableStock(authored.Inventory, authored.Production!.Lots));
        return (WorldDelegatedControlAdmission.CreateFresh(authored), market);
    }

    internal static async Task<SelectedQueryTradingFixture> Open(bool keepGranteeNearHelper = false)
    {
        var fixture = new SelectedQueryTradingFixture(keepGranteeNearHelper);
        await fixture.Host.Player.DisposeAsync();
        await SessionFixture.Until(() => fixture.Time.HasPendingTimer);
        return fixture;
    }

    private async Task<(PlayerMessageTransport Client, AuthenticatedPlayerChannel Channel)> Join(EntityId actor)
    {
        var credential = Admission.Issue(actor, Source.Delivery(actor), TimeSpan.FromHours(1));
        var pair = InMemoryPlayerTransport.CreatePair();
        _clients.Add(pair.Client);
        await ControllerTransportFixture.Send(pair.Client, PlayerHandshakeCodec.Hello(1, Admission.Compatibility, credential.Export()));
        var channel = await Admission.AcceptAsync(pair.Server, TestContext.Current.CancellationToken);
        _channels.Add(channel);
        Assert.Equal(PlayerMessageKind.Welcome, (await ControllerTransportFixture.Receive(pair.Client)).Kind);
        return (pair.Client, channel);
    }

    internal async Task<LocalPlayerConnection> Acquire()
    {
        var owner = await Join(Source.Owner);
        var grantee = await Join(Source.Grantee);
        Task session = new PlayerCommandSession(owner.Channel, NetworkTime).RunAsync(TestContext.Current.CancellationToken);
        await owner.Client.DisposeAsync();
        await ControllerTransportFixture.Finish(session);
        await LiveControllerHostTests.Tick(Time, TimeSpan.FromMilliseconds(30_000));
        var grant = State.ControlPermissions!.Find(Source.Owner, Source.Grantee)!;
        var control = State.Controllers!.Get(Source.Owner);
        Assert.Equal(WorldControllerMode.Autonomous, control.Mode);
        await grantee.Channel.Player.AcquireControlAsync(new(Source.Owner, grant.GrantCommandId!.Value,
            grant.AcceptanceCommandId!.Value, grant.Revision, grant.Terms!.Capabilities, grant.TermsHash!,
            State.EffectiveControl!.Find(Source.Owner)?.Revision ?? 0, control.DisconnectId!.Value, control.Revision));
        return grantee.Channel.Player;
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
            if (_stopped) await channel.Transport.DisposeAsync();
            else await channel.DisposeAsync();
        }
        foreach (var client in _clients) await client.DisposeAsync();
        await Stop();
        await Host.DisposeAsync();
        _files.Dispose();
    }
}
