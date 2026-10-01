using System.Text.Json;
using OpenFantasy.Client;
using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.Retreat;
using OpenFantasy.Hosting.Tests.Simulation.Retreat;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Client.Retreat;

/// <summary>
/// Earns the native retreat fixture in a fresh retreat-capable cargo world through ordinary session cutoffs: Wren's
/// quest pays the only money, the player walks to Brasshollow, buys one actual ration lot from Marla's shelf and walks
/// back beside the real first wolf. The exported closed save is paused in exploration, carrying that lot, so the native
/// budget proves engagement, retreat, travel and trade rather than repeating the road. Exports only when requested.
/// </summary>
public sealed class NativeRetreatFixtureTests
{
    internal const string SeedText = "native-retreat-fixture";
    internal const string PlayerName = "Traveler";

    [Fact]
    public async Task An_earned_and_bought_lot_is_carried_back_beside_the_real_wolf_before_any_encounter()
    {
        WorldSnapshot genesis = CargoRetreatWorldAdmission.CreateEmbedded(SeedText, PlayerName);
        CargoCircuitSources sources = CargoCircuitSources.LoadEmbedded();
        string directory = Path.Combine(Path.GetTempPath(), "openfantasy-native-retreat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var world = new RetreatJourneyWorld(Path.Combine(directory, "world.sqlite"), genesis, reopen: false);
            EntityId player = world.Player;
            CellCoordinate standing = genesis.Space.GetActor(player).Feet.Cell;
            CargoCorridor corridor = CargoCorridor.CreateCircuit(genesis.Space.Regions.Single(region => region.Bounds.Contains(standing)).Bounds, sources);
            CellCoordinate farmStart = new(standing.X, standing.Y, standing.Z - 1);
            CellCoordinate[] toMarla = [.. CargoCorridor.Lane([farmStart, new CellCoordinate(farmStart.X, corridor.OriginApproach.Y, farmStart.Z), corridor.OriginApproach])];
            foreach (FarmSemanticAction action in new[] { FarmSemanticAction.Speak, FarmSemanticAction.Give, FarmSemanticAction.Give, FarmSemanticAction.CloseDialogue })
            {
                Assert.Equal(WorldActionRejection.None, (await world.SubmitOffer(action)).Rejection);
            }
            ulong reward = world.Balance(player);
            Assert.True(reward > 0, "Delivering Wren's quest must pay its reward.");
            await world.Walk(toMarla, 8_000);
            var (bought, total) = await world.Buy(world.Markets.Origin.MarketId, 1);
            Assert.Equal(WorldActionRejection.None, bought.Rejection);
            await world.Walk([.. toMarla.Reverse()], 8_000);
            await world.ApproachFirstWolf();
            world.Session.SetReady(false);
            await world.Tick();
            WorldSnapshot before = world.State;
            Assert.Equal(WorldEncounterRetreatAdmission.ProfileVersion, before.ProfileVersion);
            Assert.Equal(EncounterLifecyclePhase.Exploration, before.EncounterLifecycle!.Phase);
            Assert.Equal(0UL, before.EncounterLifecycle.EncounterCount);
            Assert.Equal(0UL, before.Retreat!.AcceptedCount);
            Assert.True(WorldActorAvailability.CanAct(before, player));
            Assert.Equal(reward - total, world.Balance(player));
            var rations = world.Markets.Origin.Commodity;
            var carried = Assert.Single(before.Inventory.Custody.Lots, lot => lot.Definition == rations
                && lot.CustodianId == before.GetResident(player).InventoryCustodianId);
            Assert.Equal(1UL, carried.Quantity);
            Assert.True(world.Session.ReadView().EncounterLifecycle!.Entries
                .Single(value => value.ScenarioId == world.FirstScenario.DefinitionId).CanEnter);
            Export(world, genesis, before, reward, total, carried.LotId);
            await world.StopAsync();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void Export(RetreatJourneyWorld world, WorldSnapshot genesis, WorldSnapshot before, ulong reward, ulong paid, EntityId lot)
    {
        if (Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_RETREAT_FIXTURES") is not { Length: > 0 } requested) return;
        string root = Path.GetFullPath(requested);
        Assert.False(Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any(),
            "Native retreat fixture export requires a fresh or empty directory; existing evidence is never overwritten.");
        Directory.CreateDirectory(root);
        string save = Path.Combine(root, "save.sqlite");
        world.Authority.BackupForHost(save);
        File.WriteAllBytes(Path.Combine(root, "genesis.ofc"), WorldSnapshotCodec.Encode(genesis));
        File.WriteAllBytes(Path.Combine(root, "before.ofc"), WorldSnapshotCodec.Encode(before));
        EncounterScenarioDefinition scenario = world.FirstScenario;
        File.WriteAllText(Path.Combine(root, "metadata.json"), JsonSerializer.Serialize(new
        {
            Kind = "native-retreat-fixture-v1", before.ProfileVersion, SeedText, PlayerName,
            Setup = "Fresh retreat-capable cargo world; Wren's quest reward earned, one ration bought from Marla in person, "
                + "the lot carried back beside the first wolf at ordinary session cutoffs. Native begins paused in exploration.",
            WorldId = before.WorldId.ToString(), BranchId = before.BranchId.ToString(), GenesisHash = genesis.ContentHash.Value,
            BeforeHash = before.ContentHash.Value, BeforeRevision = before.Revision, BeforeAdvancingSteps = before.AdvancingSteps,
            PlayerActorId = world.Player.ToString(), WolfActorId = scenario.TargetActorId.ToString(),
            ScenarioId = scenario.DefinitionId.ToString(), MarlaActorId = world.Markets.Origin.MerchantActorId.ToString(),
            MarlaMarketId = world.Markets.Origin.MarketId.ToString(), CarriedLotId = lot.ToString(), RewardCurrency = reward,
            PaidCurrency = paid, PlayerBalance = world.Balance(world.Player),
            Limits = "Fresh retreat-capable cargo world only: no retained-save migration, bandit-camp threat, party retreat, "
                + "pursuit, physical device or human acceptance claim."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(RecordedWorldReader.ReadForHost(save, before.Revision).State));
    }
}
