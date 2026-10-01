using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.WorldStorage;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Content.Cargo.Refinement;

public sealed class CargoWaterIntegrationTests
{
    [Fact]
    public async Task Ordinary_cargo_excavation_opens_the_bank_and_conserves_water_and_merchants_after_reopening()
    {
        CargoCircuitSources sources = CargoCircuitSources.LoadEmbedded();
        WorldSnapshot genesis = CargoWorldAdmission.CreateEmbedded("cargo-live-water", "Traveler");
        string directory = Path.Combine(Path.GetTempPath(), "openfantasy-cargo-water-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "world.sqlite");
        var world = new CargoJourneyWorld(path, genesis, sources, reopen: false);
        try
        {
            EntityId player = world.Player;
            EntityId[] merchants = [world.Markets.Origin.MerchantActorId, world.Markets.Destination.MerchantActorId];
            var merchantFacts = merchants.Select(actor => CargoCircuitJourneyTests.MerchantFacts.Of(world, actor)).ToArray();
            ulong initialWater = Units(genesis);
            var initialMarkets = genesis.Coordination!.Markets!.ContentHash;
            ExcavationSite site = Assert.Single(genesis.Excavation!.Sites);
            SubunitPosition start = world.State.Space.GetActor(player).Feet;
            foreach (SubunitPosition target in new[]
            {
                new SubunitPosition(start.X, BreachFarmLayout.MiningStance.Y, start.Z),
                BreachFarmLayout.MiningStance
            })
            {
                for (int count = 0; world.State.Space.GetActor(player).Feet != target; count++)
                {
                    Assert.True(count < 256, "The bank must be reached by ordinary cargo-session movement.");
                    SubunitPosition before = world.State.Space.GetActor(player).Feet;
                    long dx = Math.Clamp(target.X - before.X, -32, 32);
                    long dy = dx == 0 ? Math.Clamp(target.Y - before.Y, -32, 32) : 0;
                    world.Session.SetMovement(dx, dy);
                    await world.Tick();
                    Assert.Equal(before.Offset(dx, dy, 0), world.State.Space.GetActor(player).Feet);
                }
            }
            world.Session.SetMovement(0, 0);
            EntityId wood = genesis.Quests!.Definitions[0].Item.DefinitionId;
            var offers = new FarmActionOffers(world.Delivery, world.Wren, wood, null);
            var selected = new FarmTarget(site.SiteId, "Bank face", FarmTargetKind.Excavation, site.Stance);
            for (ulong work = 1; work <= site.RequiredWork; work++)
            {
                for (int count = 0; world.State.Gathering!.GetActorWork(player).Effort < GatherRulesV1.EffortPerSwing; count++)
                {
                    Assert.True(count < 40, "Excavation effort must accrue through ordinary time.");
                    await world.Tick();
                }
                FarmActionOffer offer = offers.Create(world.Session.ReadView(), selected)[FarmSemanticAction.Excavate];
                Assert.True(offer.CanExecute, offer.BlockingReason);
                Assert.IsType<ExcavateIntent>(offer.Intent);
                WorldActionReceipt receipt = await world.Submit(offer.ToCommand(CargoJourneyWorld.NewId()));
                Assert.Equal(WorldActionRejection.None, receipt.Rejection);
                Assert.Equal(work, Assert.Single(world.State.Excavation!.Sites).CompletedWork);
                Assert.Equal(initialWater, Units(world.State));
            }
            // Resolve the admitted breach and let water enter the neighboring surface and lower receiver cells.
            for (int count = 0; count < 12; count++)
            {
                await world.Tick();
                Assert.Equal(initialWater, Units(world.State));
            }
            WorldSnapshot final = world.State;
            Assert.Null(final.PendingExcavation);
            Assert.False(final.Space.Regions.Single(region => region.Bounds.Contains(site.Cell)).GetCell(site.Cell).IsSolid);
            Assert.NotEqual(genesis.LiquidCells!.Topology!.ContentHash, final.LiquidCells!.Topology!.ContentHash);
            Assert.Equal(genesis.LiquidCells.Topology.OpenCells.Count + 1, final.LiquidCells.Topology.OpenCells.Count);
            Assert.Contains(final.LiquidCells.Quantities, pair => pair.Key.Coordinate.Z < 0 && pair.Value > 0);
            Assert.Equal(site.Quantity, final.Inventory.Custody.Lots.Where(lot => lot.Source.SourceId == site.CountedSourceId)
                .Aggregate(0UL, (sum, lot) => sum + lot.Quantity));
            Assert.Equal(initialMarkets, final.Coordination!.Markets!.ContentHash);
            Assert.Equal(merchantFacts, merchants.Select(actor => CargoCircuitJourneyTests.MerchantFacts.Of(world, actor)).ToArray());
            await world.StopAsync();
            using var reopened = SqliteWorldAuthority.OpenExisting(path, WorldSnapshotCodec.Encode(genesis));
            Assert.Equal(WorldSnapshotCodec.Encode(final), WorldSnapshotCodec.Encode(reopened.ReadStateForHost()));
        }
        finally
        {
            await world.StopAsync();
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void A_played_ordinary_care_world_cannot_be_relabelled_as_fresh_cargo()
    {
        WorldSnapshot genesis = RestorativeJourneyAdmission.CreateEmbedded("cargo-played-root", "Traveler");
        var actions = new ConstructionUpgradeFixture();
        WorldSnapshot played = new WorldStepProcessor().Decide(genesis, actions.Batch(genesis, [], [])).Candidate;
        Assert.True(played.Revision > genesis.Revision && played.AdvancingSteps > genesis.AdvancingSteps);
        Assert.Throws<ArgumentException>(() => CargoWorldAdmission.Compose(played, CargoCircuitSources.LoadEmbedded()));
    }

    private static ulong Units(WorldSnapshot state) => state.LiquidCells!.Quantities.Values.Aggregate(0UL, (sum, units) => sum + units);
}
