using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.NativeCargo;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.CargoPresentation;
using OpenFantasy.Hosting.Tests.Client.Cargo;
using OpenFantasy.Hosting.Tests.Simulation;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Simulation.CargoPresentation;

/// <summary>
/// The regional observation is published with the same immutable root as the session's view, so frame reads never take
/// the authority's writer gate, and it presents the inherited breach farm faithfully: the wet upper channel, the dry
/// lower receiver, the standing divider and the player's own work target, and then the ordinary earned excavation and
/// released water. Unsupported stances yield an explicit absence rather than a guess, and legal floors stay presentable.
/// </summary>
public sealed class RegionalFramePublicationTests
{
    [Fact]
    public async Task Frame_reads_stay_responsive_while_a_real_recorded_inspection_holds_the_writer_gate()
    {
        await using var world = await RegionalWorld.CreateAsync("regional-gate");
        LocalWorldView view = world.Session.ReadView();
        Assert.True(view.IsRegional);
        LocalRegionObservation observation = Assert.IsType<LocalRegionObservation>(view.Regional);
        Assert.True(observation.Contains(view.Actors.Single(actor => actor.ActorId == view.PlayerActorId).Feet));
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        CancellationToken cancellation = TestContext.Current.CancellationToken;
        world.Authority.RecordedQuoteReadHoldsGate = () =>
        {
            entered.Set();
            Assert.True(release.Wait(TimeSpan.FromSeconds(30), cancellation), "The test never released the held inspection.");
        };
        Task<PaidQuoteView?> inspection = world.Session.InspectPaidQuoteAsync(CargoTestWorld.NewId());
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken),
            "The real recorded inspection never reached the writer gate.");
        try
        {
            // Counterexample: the read the old frame path made waits for the writer gate the inspection holds.
            Task<WorldSnapshot> gated = Task.Run(() => world.Authority.ReadStateForHost(), TestContext.Current.CancellationToken);
            await Task.WhenAny(gated, Task.Delay(TimeSpan.FromMilliseconds(400), TestContext.Current.CancellationToken));
            Assert.False(gated.IsCompleted, "The writer gate was not actually held.");
            // Every frame read stays immediate: the published view, its regional observation and the session's own state.
            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (int frame = 0; frame < 240; frame++)
            {
                LocalWorldView current = world.Session.ReadView();
                Assert.Same(view, current);
                Assert.Same(observation, current.Regional);
                Assert.NotEqual(LocalWorldRunState.Unavailable, world.Session.ReadRunState());
                Assert.Null(world.Session.ReadTradeInquiry());
            }
            Assert.True(watch.Elapsed < TimeSpan.FromMilliseconds(250), $"240 frame reads took {watch.Elapsed.TotalMilliseconds} ms.");
            Assert.False(gated.IsCompleted, "The gated read finished while the inspection still held the gate.");
            release.Set();
            Assert.Null(await inspection);
            Assert.Equal(view.WorldId, (await gated).WorldId);
        }
        finally
        {
            release.Set();
            world.Authority.RecordedQuoteReadHoldsGate = null;
        }
    }

    [Fact]
    public void The_cargo_genesis_observation_presents_the_breach_channel_water_divider_and_work_target()
    {
        WorldSnapshot genesis = CargoTestWorld.Genesis;
        EntityId player = CargoTestWorld.Player(genesis);
        var view = new LocalWorldView(genesis, player, false, new LocalRegionObserver(player));
        LocalRegionObservation observation = Assert.IsType<LocalRegionObservation>(view.Regional);
        Assert.NotNull(view.Liquids);
        LocalExcavationView site = Assert.Single(view.Worksite!.ExcavationTargets);
        Assert.Equal(BreachFarmLayout.DividerCell, site.Cell);
        Assert.Equal(BreachFarmLayout.MiningStance, site.Stance);
        Assert.Equal(256, observation.FloorFeetZ);
        CellCoordinate divider = BreachFarmLayout.DividerCell;
        // The intact divider is standing ground and dry; it holds the upper channel's water back from the receiver.
        Assert.Contains(observation.RegionAt(divider.X, divider.Y)!.GroundAt(divider.X, divider.Y),
            new[] { LocalGroundKind.Surface, LocalGroundKind.Soil, LocalGroundKind.Stone });
        Assert.Null(observation.WaterAt(divider.X, divider.Y));
        for (long x = BreachFarmLayout.FirstUpperX; x <= BreachFarmLayout.LastUpperX; x++)
        {
            LocalObservedRegion region = observation.RegionAt(x, BreachFarmLayout.ChannelY)!;
            Assert.Equal(LocalGroundKind.Open, region.GroundAt(x, BreachFarmLayout.ChannelY));
            Assert.Equal(1, region.OpeningDepthAt(x, BreachFarmLayout.ChannelY));
            LocalObservedWater water = observation.WaterAt(x, BreachFarmLayout.ChannelY)!;
            Assert.Equal(LocalLiquidView.CellCapacityMicrolitres, water.VolumeMicrolitres);
            Assert.Equal(observation.FloorFeetZ, water.SurfaceSubunits);
        }
        foreach ((long x, long y) in new[] { (117L, 329L), (118L, 329L), (119L, 329L), (117L, 330L), (118L, 330L), (119L, 330L) })
        {
            LocalObservedRegion region = observation.RegionAt(x, y)!;
            Assert.Equal(LocalGroundKind.Open, region.GroundAt(x, y));
            Assert.Equal(2, region.OpeningDepthAt(x, y));
            Assert.Null(observation.WaterAt(x, y));
        }
        // The banks the player works and walks from are intact ground, never drawn as openings.
        foreach (SubunitPosition bank in new[] { BreachFarmLayout.MiningStance, BreachFarmLayout.NorthBank, BreachFarmLayout.ReceiverBank })
            Assert.True(observation.Contains(bank) && observation.RegionAt(bank.Cell.X, bank.Cell.Y)!.GroundAt(bank.Cell.X, bank.Cell.Y)
                is not LocalGroundKind.Open and not LocalGroundKind.Raised);
        AssertClassificationMatchesTerrain(genesis.Space, observation);
        Assert.Equal(genesis.LiquidCells!.Quantities.Values.Aggregate(0UL, (sum, units) => sum + units)
            * genesis.LiquidCells.VolumePerUnit.Value, observation.Water.Aggregate(0UL, (sum, water) => sum + water.VolumeMicrolitres));
    }

    [Fact]
    public void Legal_floors_stay_presentable_and_unsupported_stances_are_an_explicit_absence()
    {
        WorldSnapshot genesis = CargoTestWorld.Genesis;
        EntityId player = CargoTestWorld.Player(genesis);
        LocalSpatialSnapshot At(SubunitPosition feet) => genesis.Space.WithActors(genesis.Space.Actors.Select(actor =>
            actor.ActorId == player ? new ActorPresence(actor.Binding, actor.Condition, actor.Scope, feet, actor.Revision + 1,
                actor.HomePlaceId) : actor));
        // The receiver's lower floor: the stacked lower region alone is observed at that floor, and its water is local.
        var lower = new SubunitPosition(118 * 256 + 128, 329 * 256 + 128, -256);
        LocalRegionObservation below = LocalRegionObserver.Project(genesis, At(lower), player)!;
        Assert.Equal(-256, below.FloorFeetZ);
        Assert.True(below.Contains(lower));
        Assert.All(below.Regions, region => Assert.InRange(-2L, region.Bounds.Minimum.Z, region.Bounds.Maximum.Z));
        Assert.DoesNotContain(below.Regions, region => region.Bounds.Contains(BreachFarmLayout.DividerCell));
        // The upper channel's water stands two cells above this floor: it belongs to another floor and is not shown here.
        Assert.Empty(below.Water);
        // This world admits terrain support only: standing over an opening, between cell floors or outside every region
        // is refused by spatial admission itself, so those stances never reach a published view in this world.
        Assert.Equal(GroundSupportMode.TerrainOnly, genesis.Space.GroundSupport);
        Assert.Throws<ArgumentException>(() => At(new SubunitPosition(BreachFarmLayout.FirstUpperX * 256 + 128,
            BreachFarmLayout.ChannelY * 256 + 128, 256)));
        Assert.Throws<ArgumentException>(() => At(BreachFarmLayout.MiningStance.Offset(0, 0, 128)));
        // An observer whose player is absent has no supported observation: an explicit null, never a guessed frame.
        Assert.Null(LocalRegionObserver.Project(genesis, CargoTestWorld.NewId()));
    }

    [Fact]
    public async Task Ordinary_earned_mining_and_released_water_reach_the_published_regional_view()
    {
        await using var world = await RegionalWorld.CreateAsync("regional-mining");
        WorldSnapshot genesis = world.Authority.ReadStateForHost();
        ExcavationSite site = Assert.Single(genesis.Excavation!.Sites);
        ulong revision = world.Session.ReadView().Regional!.RegionAt(site.Cell.X, site.Cell.Y)!.Revision;
        world.Session.SetReady(true);
        SubunitPosition start = world.Feet;
        foreach (SubunitPosition target in new[] { new SubunitPosition(start.X, BreachFarmLayout.MiningStance.Y, start.Z), BreachFarmLayout.MiningStance })
        {
            for (int count = 0; world.Feet != target; count++)
            {
                Assert.True(count < 256, "The bank must be reached by ordinary movement.");
                long dx = Math.Clamp(target.X - world.Feet.X, -32, 32);
                world.Session.SetMovement(dx, dx == 0 ? Math.Clamp(target.Y - world.Feet.Y, -32, 32) : 0);
                await world.Tick();
                // Every published view carries an observation of its own root that holds the player's own feet.
                LocalWorldView published = world.Session.ReadView();
                Assert.True(published.Regional!.Contains(published.Actors.Single(actor => actor.ActorId == world.Player).Feet));
            }
        }
        world.Session.SetMovement(0, 0);
        var offers = new FarmActionOffers(world.Delivery, world.Wren, genesis.Quests!.Definitions[0].Item.DefinitionId, null);
        var selected = new FarmTarget(site.SiteId, "Bank face", FarmTargetKind.Excavation, site.Stance);
        for (ulong work = 1; work <= site.RequiredWork; work++)
        {
            for (int count = 0; world.Authority.ReadStateForHost().Gathering!.GetActorWork(world.Player).Effort < GatherRulesV1.EffortPerSwing; count++)
            {
                Assert.True(count < 40, "Excavation effort must accrue through ordinary time.");
                await world.Tick();
            }
            FarmActionOffer offer = offers.Create(world.Session.ReadView(), selected)[FarmSemanticAction.Excavate];
            Assert.True(offer.CanExecute, offer.BlockingReason);
            Task<WorldActionReceipt> receipt = world.Session.SubmitAction(offer.ToCommand(CargoTestWorld.NewId()));
            for (int count = 0; !receipt.IsCompleted; count++)
            {
                Assert.True(count < 64, "The mining action did not reach its terminal receipt.");
                await world.Tick();
            }
            Assert.Equal(WorldActionRejection.None, (await receipt).Rejection);
        }
        for (int count = 0; count < 16; count++) await world.Tick();
        LocalWorldView final = world.Session.ReadView();
        WorldSnapshot head = world.Authority.ReadStateForHost();
        LocalRegionObservation observed = final.Regional!;
        LocalObservedRegion holding = observed.RegionAt(site.Cell.X, site.Cell.Y)!;
        Assert.False(final.IsResolvingConsequences);
        Assert.Equal(LocalGroundKind.Open, holding.GroundAt(site.Cell.X, site.Cell.Y));
        Assert.NotEqual(revision, holding.Revision);
        Assert.Equal(head.Space.Regions.Single(region => region.Bounds == holding.Bounds).Revision, holding.Revision);
        Assert.Contains(observed.Water, water => water.CellX > site.Cell.X && water.SurfaceSubunits < observed.FloorFeetZ);
        AssertClassificationMatchesTerrain(head.Space, observed);
        Assert.Equal(head.LiquidCells!.Quantities.Values.Aggregate(0UL, (sum, units) => sum + units) * head.LiquidCells.VolumePerUnit.Value,
            observed.Water.Aggregate(0UL, (sum, water) => sum + water.VolumeMicrolitres));
    }

    /// <summary>Independent check of every observed column against the actual terrain: ground, raised blocker or opening.</summary>
    private static void AssertClassificationMatchesTerrain(LocalSpatialSnapshot space, LocalRegionObservation observation)
    {
        long floor = observation.FloorFeetZ / SubunitPosition.SubunitsPerCell - 1;
        bool? Solid(long x, long y, long z) => space.Regions.FirstOrDefault(region => region.Bounds.Contains(new CellCoordinate(x, y, z)))
            ?.GetCell(new CellCoordinate(x, y, z)).IsSolid;
        foreach (LocalObservedRegion region in observation.Regions)
        {
            for (long y = region.Bounds.Minimum.Y; y <= region.Bounds.Maximum.Y; y++)
            {
                for (long x = region.Bounds.Minimum.X; x <= region.Bounds.Maximum.X; x++)
                {
                    LocalGroundKind kind = region.GroundAt(x, y);
                    Assert.Equal(Solid(x, y, floor) == true, kind != LocalGroundKind.Open);
                    if (kind != LocalGroundKind.Open) Assert.Equal(Solid(x, y, floor + 1) == true, kind == LocalGroundKind.Raised);
                }
            }
        }
    }

    /// <summary>A fresh production cargo save with a regional session on manual time; nothing is installed.</summary>
    private sealed class RegionalWorld : IAsyncDisposable
    {
        private readonly string _directory;

        private RegionalWorld(string directory, SqliteWorldAuthority authority, LocalWorldSession session, EntityId player,
            EntityId wren, TrustedCommandDelivery delivery, ManualSessionTimeProvider time)
        {
            _directory = directory;
            Authority = authority;
            Session = session;
            Player = player;
            Wren = wren;
            Delivery = delivery;
            Time = time;
        }

        internal SqliteWorldAuthority Authority { get; }
        internal LocalWorldSession Session { get; }
        internal EntityId Player { get; }
        internal EntityId Wren { get; }
        internal TrustedCommandDelivery Delivery { get; }
        internal ManualSessionTimeProvider Time { get; }
        internal SubunitPosition Feet => Session.ReadView().Actors.Single(actor => actor.ActorId == Player).Feet;

        internal static Task<RegionalWorld> CreateAsync(string label)
        {
            string directory = Path.Combine(Path.GetTempPath(), "openfantasy-" + label + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            WorldSnapshot genesis = CargoTestWorld.Genesis;
            var authority = SqliteWorldAuthority.CreateNew(Path.Combine(directory, "world.sqlite"), genesis);
            EntityId player = CargoTestWorld.Player(genesis);
            ActorControlBinding control = genesis.Inventory.Controls.Single(value => value.ActorId == player);
            var delivery = new TrustedCommandDelivery(control.PrincipalId, control.SessionId);
            var time = new ManualSessionTimeProvider();
            var session = new LocalWorldSession(authority, player, delivery, time, null, NativeCargoWorld.Configuration(genesis),
                new LocalRegionObserver(player));
            EntityId wren = genesis.Quests!.Definitions.Select(value => value.GiverActorId).Distinct().Single();
            return Task.FromResult(new RegionalWorld(directory, authority, session, player, wren, delivery, time));
        }

        internal Task Tick() => SessionFixture.Tick(Time);

        public async ValueTask DisposeAsync()
        {
            await Session.StopAsync();
            Directory.Delete(_directory, recursive: true);
        }
    }
}
