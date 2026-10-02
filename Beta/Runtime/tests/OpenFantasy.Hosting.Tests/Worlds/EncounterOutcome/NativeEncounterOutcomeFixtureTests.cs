using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Custody;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Spatial;
using OpenFantasy.Core.Terrain;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.EncounterOutcome;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Tests.Simulation.Retreat;
using OpenFantasy.Hosting.Tests.Simulation.RoadThreat;
using OpenFantasy.Hosting.Worlds;

namespace OpenFantasy.Hosting.Tests.Worlds.EncounterOutcome;

/// <summary>
/// Managed fixture preparation for the native outcome journeys, exported only when
/// OPENFANTASY_NATIVE_ENCOUNTER_OUTCOME_FIXTURES names a fresh directory. Each route is a fresh profile-35 cargo world
/// selected explicitly, with its declared calibration recorded so its genesis can be re-derived byte for byte:
/// <c>victory</c> (the chief declared wounded at 1 HP) and <c>defeat</c> (the uncalibrated production selection) earn
/// Wren's reward, buy one ration from Marla and walk the lane to road cell (346,318) at ordinary session cutoffs, outside
/// the chief's reach, and stop in exploration before any encounter. <c>care</c> (the player declared wounded at 1 HP) walks
/// beside the first wolf near Wren and performs the player's own entry as a genuine managed command, stopping at the Active
/// episode's first decision boundary. No outcome, reading, completion or care exists before native input, which owns every
/// combat choice, the resolution, the reading (declining the offered care) and the completion. The care fixture therefore
/// does not prove a native, care-specific approach or entry or its timing; that obligation stays open.
/// </summary>
public sealed class NativeEncounterOutcomeFixtureTests
{
    internal const string Variable = "OPENFANTASY_NATIVE_ENCOUNTER_OUTCOME_FIXTURES";
    internal const string VictorySeed = "native-encounter-outcome-victory";
    internal const string DefeatSeed = "native-encounter-outcome-defeat";
    internal const string CareSeed = "native-encounter-outcome-care";

    [Fact]
    public async Task Fresh_outcome_worlds_are_prepared_up_to_the_native_encounters()
    {
        if (Environment.GetEnvironmentVariable(Variable) is not { Length: > 0 } requested) return;
        string root = Path.GetFullPath(requested);
        Assert.False(Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any(),
            "Native outcome fixture export requires a fresh or empty directory; existing evidence is never overwritten.");
        Directory.CreateDirectory(root);
        string scratch = Path.Combine(Path.GetTempPath(), "openfantasy-native-outcome-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            List<object> routes = [];
            routes.Add(await RoadRoute(root, scratch, "victory", VictorySeed,
                CargoOutcomeWorldAdmission.CreateWoundedChief(VictorySeed, RoadThreatJourney.PlayerName, 1), chiefHp: 1));
            routes.Add(await RoadRoute(root, scratch, "defeat", DefeatSeed,
                CargoOutcomeWorldAdmission.CreateEmbedded(DefeatSeed, RoadThreatJourney.PlayerName), chiefHp: null));
            routes.Add(await CareRoute(root, scratch));
            File.WriteAllText(Path.Combine(root, "manifest.json"), JsonSerializer.Serialize(new
            {
                Kind = "native-encounter-outcome-fixtures-v1", Routes = routes,
                Boundary = "Managed preparation only. Road routes: ordinary session cutoffs earn, buy and walk, stopping in "
                    + "exploration before any encounter. Care: ordinary cutoffs walk beside the first wolf and the player's own "
                    + "managed entry command begins the encounter, stopping at the Active first decision. Nothing resolves, reads, "
                    + "completes or cares before the native journey; native care-specific approach and entry timing are not exercised."
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { Directory.Delete(scratch, recursive: true); }
    }

    private static async Task<object> RoadRoute(string root, string scratch, string route, string seed, WorldSnapshot genesis, int? chiefHp)
    {
        RoadThreatBinding threat = EncounterOutcomeJourneyTests.Threat(genesis);
        await using var world = new RetreatJourneyWorld(Path.Combine(scratch, route + ".sqlite"), genesis, reopen: false);
        EntityId player = world.Player;
        var (reward, paid, lot) = await RoadThreatJourney.EarnAndBuy(world, 1);
        CargoCorridor corridor = RoadThreatJourney.Corridor(genesis, player);
        CellCoordinate road = new(346, 318, corridor.Corner.Z);
        Assert.False(await RoadThreatJourney.WalkUntilEngaged(world, [.. CargoCorridor.Lane([corridor.OriginApproach,
            corridor.Corner, road])], 8_000), "The fixture road cell must stay outside the chief's reach.");
        await world.TickUntil(() => WorldActorAvailability.CanAct(world.State, player), 400, "settle");
        world.Session.SetReady(false);
        await world.Tick();
        WorldSnapshot before = world.State;
        Assert.Equal(WorldEncounterOutcomeAdmission.ProfileVersion, before.ProfileVersion);
        Assert.Equal((EncounterLifecyclePhase.Exploration, 0UL), (before.EncounterLifecycle!.Phase, before.EncounterLifecycle.EncounterCount));
        Assert.Equal(EncounterOutcomeState.Fresh().ContentHash, before.EncounterOutcome!.ContentHash);
        Assert.Equal((road.X, road.Y), (before.Space.GetActor(player).Feet.Cell.X, before.Space.GetActor(player).Feet.Cell.Y));
        Assert.NotEqual(SpatialInteraction.Reachable, before.Space.QueryInteraction(threat.ChiefActorId, player, threat.Scenario.EntryRangeSubunits));
        Assert.Equal(chiefHp ?? genesis.Progression!.GetActor(threat.ChiefActorId).CurrentHp, before.Progression!.GetActor(threat.ChiefActorId).CurrentHp);
        CountedSourceLot carried = Assert.Single(before.Inventory.Custody.Lots, value => value.LotId == lot.LotId);
        Dictionary<string, int> wounded = chiefHp is { } hp ? new() { [threat.ChiefActorId.ToString()] = hp } : [];
        return Export(root, route, seed, world, genesis, before, threat.ChiefActorId, wounded, carried.LotId.ToString(), reward, paid,
            chiefHp is null ? "Uncalibrated production selection (CargoOutcomeWorldAdmission.CreateEmbedded)."
                : "Declared fresh calibration: the Bandit Chief starts at 1 HP (CreateWoundedChief); qualification only, not balance.",
            "Wren's quest reward earned, one ration bought from Marla in person and carried along the authored lane to road cell "
            + "(346,318) at ordinary session cutoffs, outside the chief's reach; paused in exploration.",
            "Fixture preparation ends here in exploration, before any encounter; the native journey owns the chief's own entry, "
            + "every combat choice, the resolution, the outcome reading, the world's completion, any continuation and leaving.");
    }

    private static async Task<object> CareRoute(string root, string scratch)
    {
        WorldSnapshot threatened = RoadThreatWorldAdmission.CreateEmbedded(CareSeed, RoadThreatJourney.PlayerName);
        EntityId player = threatened.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        WorldSnapshot genesis = WorldEncounterOutcomeAdmission.CreateCalibratedFresh(threatened,
            new EncounterOutcomeCalibration(new Dictionary<EntityId, int> { [player] = 1 }, []));
        await using var world = new RetreatJourneyWorld(Path.Combine(scratch, "care.sqlite"), genesis, reopen: false);
        EntityId wolf = world.FirstScenario.TargetActorId;
        await world.ApproachFirstWolf();
        // The player's own entry is part of this declared preparation (entered at once, as the managed care proofs do);
        // the export stops at the player's first decision boundary, before any combat choice or resolution.
        Assert.Equal(WorldActionRejection.None, (await world.EnterFirstWolf()).Rejection);
        await world.TickUntil(() => world.Session.ReadView().EncounterActions?.CanWait == true, 400, "first boundary");
        world.Session.SetReady(false);
        await world.Tick();
        WorldSnapshot before = world.State;
        Assert.Equal((EncounterLifecyclePhase.Active, 1UL), (before.EncounterLifecycle!.Phase, before.EncounterLifecycle.EncounterCount));
        Assert.Equal(1, before.Progression!.GetActor(player).CurrentHp);
        return Export(root, "care", CareSeed, world, genesis, before, wolf, new() { [player.ToString()] = 1 }, null, 0, 0,
            "Declared fresh calibration: the player starts at 1 HP (CreateCalibratedFresh); qualification only, not balance.",
            "Walked by ordinary session cutoffs beside the first inherited wolf near Wren and entered by the player's own entry "
            + "command; paused at the player's first decision boundary, before any combat choice or resolution.",
            "Fixture preparation ends here at the Active episode's first decision boundary, after the genuine managed approach "
            + "and the player's own managed entry; the native journey owns every combat choice, the resolution, the outcome "
            + "reading that declines the offered care, and the world's completion. Native care-specific approach and entry "
            + "timing are not exercised by this fixture.");
    }

    private static object Export(string root, string route, string seed, RetreatJourneyWorld world, WorldSnapshot genesis,
        WorldSnapshot before, EntityId opponent, Dictionary<string, int> wounded, string? lot, ulong reward, ulong paid,
        string calibration, string setup, string boundary)
    {
        string directory = Path.Combine(root, route);
        Directory.CreateDirectory(directory);
        string save = Path.Combine(directory, "save.sqlite");
        world.Authority.BackupForHost(save);
        File.WriteAllBytes(Path.Combine(directory, "genesis.ofc"), WorldSnapshotCodec.Encode(genesis));
        File.WriteAllBytes(Path.Combine(directory, "before.ofc"), WorldSnapshotCodec.Encode(before));
        File.WriteAllText(Path.Combine(directory, "metadata.json"), JsonSerializer.Serialize(new
        {
            Kind = "native-encounter-outcome-fixture-v1", Route = route, before.ProfileVersion, SeedText = seed,
            PlayerName = RoadThreatJourney.PlayerName, Calibration = new { WoundedHp = wounded, HumanControlled = Array.Empty<string>() },
            CalibrationText = calibration, Setup = setup, Boundary = boundary,
            WorldId = before.WorldId.ToString(), BranchId = before.BranchId.ToString(), GenesisHash = genesis.ContentHash.Value,
            BeforeHash = before.ContentHash.Value, BeforeRevision = before.Revision, PlayerActorId = world.Player.ToString(),
            OpponentActorId = opponent.ToString(), WrenActorId = world.Wren.ToString(), CarriedLotId = lot,
            RewardCurrency = reward, PaidCurrency = paid, PlayerBalance = world.Balance(world.Player),
            Limits = "Fresh profile-35 cargo world only: no retained-save migration, multiplayer, network, physical device, balance "
                + "or human acceptance claim."
        }, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal(WorldSnapshotCodec.Encode(before), WorldSnapshotCodec.Encode(RecordedWorldReader.ReadForHost(save, before.Revision).State));
        return new { Route = route, Directory = route };
    }
}
