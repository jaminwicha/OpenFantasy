using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Simulation.RoadThreat;
using OpenFantasy.Hosting.Tests.Simulation.Retreat;
using OpenFantasy.Hosting.Tests.Simulation.RoadThreat;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Client.RoadThreat;

/// <summary>
/// Earns the native road-threat fixture in a fresh threatened cargo world through ordinary session cutoffs: Wren's
/// quest pays the only money, the player walks to Brasshollow, buys one actual ration lot from Marla and walks the
/// authored lane to the road cell (346,318), seven cells south of the camp's chief and outside its reach. The exported
/// closed save is paused in exploration, so the native budget proves the road encounter, escape, detour, far sale and
/// reopening rather than repeating the walk from the farm. Exports only when requested.
/// </summary>
public sealed class NativeRoadThreatFixtureTests
{
    internal const string SeedText = "native-road-threat-fixture";
    internal static readonly CellCoordinate RoadStart = new(346, 318, 0);

    [Fact]
    public async Task An_earned_and_bought_lot_is_carried_up_the_lane_to_just_outside_the_chiefs_reach()
    {
        WorldSnapshot genesis = RoadThreatJourney.Genesis(SeedText);
        RoadThreatBinding threat = RoadThreatJourney.Threat(genesis);
        string directory = Path.Combine(Path.GetTempPath(), "openfantasy-native-road-threat-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var world = new RetreatJourneyWorld(Path.Combine(directory, "world.sqlite"), genesis, reopen: false);
            EntityId player = world.Player;
            // No cutoff has run yet: the same world's untouched genesis save is the keyboard route's fresh farm fixture.
            Assert.Equal(0UL, world.State.Revision);
            ExportFarm(world, genesis, threat);
            var (reward, paid, lot) = await RoadThreatJourney.EarnAndBuy(world, 1);
            CargoCorridor corridor = RoadThreatJourney.Corridor(genesis, player);
            CellCoordinate road = new(RoadStart.X, RoadStart.Y, corridor.Corner.Z);
            Assert.False(await RoadThreatJourney.WalkUntilEngaged(world, [.. CargoCorridor.Lane([corridor.OriginApproach,
                corridor.Corner, road])], 8_000), "The fixture road cell must stay outside the chief's reach.");
            await world.TickUntil(() => WorldActorAvailability.CanAct(world.State, player), 400, "settle");
            world.Session.SetReady(false);
            await world.Tick();
            WorldSnapshot before = world.State;
            Assert.Equal(WorldEncounterRetreatAdmission.ProfileVersion, before.ProfileVersion);
            Assert.Equal(EncounterLifecyclePhase.Exploration, before.EncounterLifecycle!.Phase);
            Assert.Equal(0UL, before.EncounterLifecycle.EncounterCount);
            Assert.Equal(0UL, before.Retreat!.AcceptedCount);
            Assert.Equal((road.X, road.Y), (before.Space.GetActor(player).Feet.Cell.X, before.Space.GetActor(player).Feet.Cell.Y));
            Assert.NotEqual(SpatialInteraction.Reachable,
                before.Space.QueryInteraction(threat.ChiefActorId, player, threat.Scenario.EntryRangeSubunits));
            Assert.Equal(genesis.Space.GetActor(threat.ChiefActorId).Feet, before.Space.GetActor(threat.ChiefActorId).Feet);
            // Seven cells south the chief is beyond the three-cell observation bound: no warning detail is published.
            Assert.Empty(world.Session.ReadView().RoadThreats!);
            Assert.Equal(reward - paid, world.Balance(player));
            CountedSourceLot carried = Assert.Single(before.Inventory.Custody.Lots, value => value.LotId == lot.LotId);
            Assert.Equal(lot, carried);
            Export(world, genesis, before, threat, reward, paid, lot.LotId);
            await world.StopAsync();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// The keyboard route's fresh farm fixture: the closed, unplayed save of this threatened world at commit zero, in
    /// <c>farm/</c>. Nothing is earned, bought or walked; the native keyboard journey owns all of it.
    /// </summary>
    private static void ExportFarm(RetreatJourneyWorld world, WorldSnapshot genesis, RoadThreatBinding threat)
    {
        if (Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_ROAD_THREAT_FIXTURES") is not { Length: > 0 } requested) return;
        string root = Path.GetFullPath(requested);
        Assert.False(Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any(),
            "Native road-threat fixture export requires a fresh or empty directory; existing evidence is never overwritten.");
        string farm = Path.Combine(root, "farm");
        Directory.CreateDirectory(farm);
        string save = Path.Combine(farm, "save.sqlite");
        world.Authority.BackupForHost(save);
        File.WriteAllBytes(Path.Combine(farm, "genesis.ofc"), WorldSnapshotCodec.Encode(genesis));
        File.WriteAllBytes(Path.Combine(farm, "before.ofc"), WorldSnapshotCodec.Encode(genesis));
        File.WriteAllText(Path.Combine(farm, "metadata.json"), JsonSerializer.Serialize(new
        {
            Kind = "native-road-threat-farm-fixture-v1", genesis.ProfileVersion, SeedText, PlayerName = RoadThreatJourney.PlayerName,
            Setup = "Fresh threatened cargo world at commit zero: no reward, purchase, quote or walk. Native begins paused at the farm.",
            Boundary = "The keyboard route owns Wren's dialogue reward, the walk to Marla, the stall purchase, the outbound lane, "
                + "the chief's entry, the fight/retreat, the detour, the far sale and reopening.",
            WorldId = genesis.WorldId.ToString(), BranchId = genesis.BranchId.ToString(), GenesisHash = genesis.ContentHash.Value,
            BeforeHash = genesis.ContentHash.Value, BeforeRevision = genesis.Revision, BeforeAdvancingSteps = genesis.AdvancingSteps,
            PlayerActorId = world.Player.ToString(), ChiefActorId = threat.ChiefActorId.ToString(), WrenActorId = world.Wren.ToString(),
            ScenarioId = threat.ScenarioId.ToString(), ReachSubunits = threat.Scenario.EntryRangeSubunits,
            MarlaActorId = world.Markets.Origin.MerchantActorId.ToString(), MarlaMarketId = world.Markets.Origin.MarketId.ToString(),
            KeggaActorId = world.Markets.Destination.MerchantActorId.ToString(), KeggaMarketId = world.Markets.Destination.MarketId.ToString(),
            Limits = "Fresh threatened cargo world only: no retained-save migration, escort wage, pursuit, party retreat, "
                + "multiplayer, physical device or human acceptance claim."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal(WorldSnapshotCodec.Encode(genesis), WorldSnapshotCodec.Encode(RecordedWorldReader.ReadForHost(save, 0).State));
    }

    private static void Export(RetreatJourneyWorld world, WorldSnapshot genesis, WorldSnapshot before, RoadThreatBinding threat,
        ulong reward, ulong paid, EntityId lot)
    {
        if (Environment.GetEnvironmentVariable("OPENFANTASY_NATIVE_ROAD_THREAT_FIXTURES") is not { Length: > 0 } requested) return;
        string root = Path.GetFullPath(requested);
        // Only the farm fixture written by this same test precedes the near-road export.
        Assert.Equal(new[] { Path.Combine(root, "farm") }, Directory.EnumerateFileSystemEntries(root).ToArray());
        string save = Path.Combine(root, "save.sqlite");
        world.Authority.BackupForHost(save);
        File.WriteAllBytes(Path.Combine(root, "genesis.ofc"), WorldSnapshotCodec.Encode(genesis));
        File.WriteAllBytes(Path.Combine(root, "before.ofc"), WorldSnapshotCodec.Encode(before));
        SubunitPosition chiefFeet = before.Space.GetActor(threat.ChiefActorId).Feet;
        File.WriteAllText(Path.Combine(root, "metadata.json"), JsonSerializer.Serialize(new
        {
            Kind = "native-road-threat-fixture-v1", before.ProfileVersion, SeedText, PlayerName = RoadThreatJourney.PlayerName,
            Setup = "Fresh threatened cargo world; Wren's quest reward earned, one ration bought from Marla in person, the lot "
                + "carried along the authored lane to road cell (346,318) at ordinary session cutoffs, outside the chief's reach. "
                + "Native begins paused in exploration at that road cell.",
            Boundary = "Short native fixtures begin on the actual road after this managed preparation; the native journey owns "
                + "the approach, the chief's entry, the fight/retreat, the detour, the far sale and reopening.",
            WorldId = before.WorldId.ToString(), BranchId = before.BranchId.ToString(), GenesisHash = genesis.ContentHash.Value,
            BeforeHash = before.ContentHash.Value, BeforeRevision = before.Revision, BeforeAdvancingSteps = before.AdvancingSteps,
            PlayerActorId = world.Player.ToString(), ChiefActorId = threat.ChiefActorId.ToString(),
            ScenarioId = threat.ScenarioId.ToString(), ChiefFeet = new { chiefFeet.X, chiefFeet.Y, chiefFeet.Z },
            ReachSubunits = threat.Scenario.EntryRangeSubunits,
            MarlaActorId = world.Markets.Origin.MerchantActorId.ToString(), MarlaMarketId = world.Markets.Origin.MarketId.ToString(),
            KeggaActorId = world.Markets.Destination.MerchantActorId.ToString(), KeggaMarketId = world.Markets.Destination.MarketId.ToString(),
            CarriedLotId = lot.ToString(), RewardCurrency = reward, PaidCurrency = paid, PlayerBalance = world.Balance(world.Player),
            Limits = "Fresh threatened cargo world only: no retained-save migration, escort wage, pursuit, party retreat, "
                + "multiplayer, physical device or human acceptance claim."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(RecordedWorldReader.ReadForHost(save, before.Revision).State));
    }
}
