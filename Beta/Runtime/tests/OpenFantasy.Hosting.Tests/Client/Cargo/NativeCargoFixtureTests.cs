using System.Text.Json;
using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Authority;
using OpenFantasy.Core.Markets;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.NativeCargo;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Tests.Content.Cargo;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Client.Cargo;

/// <summary>
/// Earns the native cargo fixture in a fresh production cargo world: the player talks to Wren, accepts and delivers
/// her quest through ordinary session cutoffs, and is paid its reward. Nothing else funds the player. The exported
/// closed save begins at the farm before any travel or market action; its origin and the trade it can afford are
/// proved here. Without its explicit export directory the test still proves the origin and exports nothing.
/// </summary>
public sealed class NativeCargoFixtureTests
{
    internal const string SeedText = "native-cargo-fixture";
    internal const string PlayerName = "Traveler";

    [Fact]
    public async Task An_earned_quest_reward_funds_a_fresh_cargo_world_before_any_travel_or_trade()
    {
        CargoCircuitSources sources = NativeCargoWorld.Sources;
        WorldSnapshot genesis = NativeCargoWorld.CreateEmbedded(SeedText, PlayerName);
        string directory = Path.Combine(Path.GetTempPath(), "openfantasy-native-cargo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var world = new CargoJourneyWorld(Path.Combine(directory, "world.sqlite"), genesis, sources, reopen: false);
            SubunitPositionFacts start = SubunitPositionFacts.Of(world.State, world.Player);
            Assert.Equal(0UL, genesis.Progression!.Currency.GetActorAccount(world.Player).Balance);
            foreach (FarmSemanticAction action in new[] { FarmSemanticAction.Speak, FarmSemanticAction.Give,
                FarmSemanticAction.Give, FarmSemanticAction.CloseDialogue })
            {
                Assert.Equal(WorldActionRejection.None, (await world.SubmitOffer(action)).Rejection);
            }
            world.Session.SetReady(false);
            await world.Tick();
            WorldSnapshot before = world.State;
            // The quest payment is the player's only money: it started with none.
            ulong reward = world.Balance(world.Player);
            Assert.True(reward > 0, "Delivering Wren's quest must pay its reward.");
            CargoCircuitMarkets markets = world.Markets;
            ulong marlaStock = markets.Origin.CountUsableStock(before.Inventory, before.Production!.Lots);
            ulong buyTotal = markets.Origin.Policy.Ask(marlaStock, CargoRoutePlayProbeUnits)!.Value * CargoRoutePlayProbeUnits;
            Assert.Equal(WorldMarketAdmission.ProfileVersion, before.ProfileVersion);
            Assert.True(reward >= buyTotal, $"The earned reward {reward} must cover Marla's {buyTotal}.");
            Assert.Equal(CargoCircuitPolicy.BrasshollowProvision.OpeningUnits, marlaStock);
            Assert.Equal(0UL, markets.Destination.CountUsableStock(before.Inventory, before.Production.Lots));
            Assert.NotNull(markets.Destination.Policy.Bid(0, CargoRoutePlayProbeUnits));
            Assert.Equal(start, SubunitPositionFacts.Of(before, world.Player));
            Assert.Empty(before.Coordination!.PaidExchange!.Open);
            Assert.Empty(before.Coordination.PaidExchange.Outcomes);
            Assert.Empty(before.Coordination.Markets!.Disclosures);
            Assert.Empty(before.Coordination.Markets.Knowledge);
            Assert.Null(WorldMarketLocality.FindAvailable(before, world.Player, markets.Origin.MarketId));
            Assert.Null(WorldMarketLocality.FindAvailable(before, world.Player, markets.Destination.MarketId));
            Assert.False(before.HasReadingHold);
            Assert.True(WorldActorAvailability.CanAct(before, world.Player));
            Assert.Equal("Exploration", EncounterPhaseName(before));
            Export(world, genesis, before, reward);
            await world.StopAsync();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    internal const ulong CargoRoutePlayProbeUnits = 2;

    private static string EncounterPhaseName(WorldSnapshot state) => state.EncounterLifecycle!.Phase.ToString();

    private static void Export(CargoJourneyWorld world, WorldSnapshot genesis, WorldSnapshot before, ulong reward)
    {
        if (Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_CARGO_FIXTURES") is not { Length: > 0 } requested) return;
        string root = Path.GetFullPath(requested);
        Assert.False(Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any(),
            "Native cargo fixture export requires a fresh or empty directory; existing evidence is never overwritten.");
        Directory.CreateDirectory(root);
        string save = Path.Combine(root, "save.sqlite");
        string genesisPath = Path.Combine(root, "genesis.ofc");
        string beforePath = Path.Combine(root, "before.ofc");
        string metadataPath = Path.Combine(root, "metadata.json");
        world.Authority.BackupForHost(save);
        File.WriteAllBytes(genesisPath, WorldSnapshotCodec.Encode(genesis));
        File.WriteAllBytes(beforePath, WorldSnapshotCodec.Encode(before));
        CargoCircuitMarkets markets = world.Markets;
        File.WriteAllText(metadataPath, JsonSerializer.Serialize(new
        {
            Kind = "native-cargo-fixture-v1", before.ProfileVersion, SeedText, PlayerName,
            Setup = "Fresh production cargo world; talk, accept and deliver Wren's quest for its earned reward at ordinary "
                + "session cutoffs. Native begins paused at the farm before any travel, observation or market action.",
            WorldId = before.WorldId.ToString(), BranchId = before.BranchId.ToString(),
            GenesisHash = genesis.ContentHash.Value, BeforeHash = before.ContentHash.Value, BeforeRevision = before.Revision,
            BeforeAdvancingSteps = before.AdvancingSteps, PlayerActorId = world.Player.ToString(), WrenActorId = world.Wren.ToString(),
            MarlaActorId = markets.Origin.MerchantActorId.ToString(), MarlaMarketId = markets.Origin.MarketId.ToString(),
            KeggaActorId = markets.Destination.MerchantActorId.ToString(), KeggaMarketId = markets.Destination.MarketId.ToString(),
            RewardCurrency = reward, PlayerBalance = world.Balance(world.Player), TradeUnits = CargoRoutePlayProbeUnits,
            Limits = "Fresh production cargo world only: no retained-save migration, threat, escort, streaming, physical device or "
                + "human acceptance claim. Requests are session memory and leave no journal record."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(RecordedWorldReader.ReadForHost(save, before.Revision).State));
    }

    /// <summary>The player's exact public feet, compared by value.</summary>
    private sealed record SubunitPositionFacts(long X, long Y, long Z)
    {
        internal static SubunitPositionFacts Of(WorldSnapshot state, EntityId actor)
        {
            var feet = state.Space.GetActor(actor).Feet;
            return new SubunitPositionFacts(feet.X, feet.Y, feet.Z);
        }
    }
}
