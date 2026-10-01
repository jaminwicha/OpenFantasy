using System.Text;
using System.Text.Json;
using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Progression;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.Retreat;
using OpenFantasy.Hosting.Content.RoadThreat;
using OpenFantasy.Hosting.Simulation;
using OpenFantasy.Hosting.Simulation.RoadThreat;

namespace OpenFantasy.Hosting.Tests.Content.RoadThreat;

/// <summary>Source binding and fresh admission of the camp's Bandit Chief into the threatened cargo world.</summary>
public sealed class RoadThreatAdmissionTests
{
    private const string Seed = "road-threat-admission";

    [Fact]
    public void The_bandit_chief_record_camp_and_spawner_bind_with_exact_pointers_and_retained_fields()
    {
        RoadThreatSources sources = RoadThreatSources.LoadEmbedded();
        RoadThreatSourceMonster monster = sources.Monster;
        Assert.Equal(("bandit_chief", "Bandit Chief", 7, "/monsters/33"), (monster.SourceId, monster.Name, monster.Level, monster.Pointer));
        Assert.Equal(new ActorStatValues(800, 0, 27, 0, 15, 10), monster.Stats);
        Assert.Equal((15, 50, 480), (monster.AttackPower, monster.AttackRangeUnits, monster.AttackAtbCost));
        // Retained-only fields stay exactly as authored.
        Assert.Equal((4, 1400, 260, 480, 140, 400), (monster.MoveSpeedUnits, monster.ExperienceReward, monster.GilMinimum,
            monster.GilMaximum, monster.AggroRangeUnits, monster.LeashRangeUnits));
        Assert.Equal(["ancient_coin:70", "steel_blade:10", "hi_potion:40"], monster.Drops.Select(drop => drop.ItemId + ":" + drop.ChancePercent));
        Assert.Equal(("/regions/16", new CargoTileRect(345, 305, 360, 317)), (sources.Camp.Pointer, sources.Camp.Tiles));
        Assert.Equal((34, "bandit_chief", 1, 3600, "/spawners/33"), (sources.Spawner.SpawnerId, sources.Spawner.MonsterId,
            sources.Spawner.Count, sources.Spawner.RespawnTicks, sources.Spawner.Pointer));
        Assert.Equal(sources.Camp.Tiles, sources.Spawner.Tiles);
        Assert.Equal(400L, sources.AttackRangeSubunits);
        Assert.Equal(3, new[] { monster.PayloadHash, sources.Camp.PayloadHash, sources.Spawner.PayloadHash }.Distinct().Count());
        Assert.Equal(sources.Version, RoadThreatSources.LoadEmbedded().Version);
    }

    [Fact]
    public void An_edited_monster_record_is_refused_rather_than_reinterpreted()
    {
        FarmContentFiles files = FarmContentFiles.Embedded();
        string monsters = Encoding.UTF8.GetString(files.Read("legacy/aldermere-first-loop/source-support/data/monsters.json",
            32_768, RoadThreatSources.MonstersDigest));
        byte[] world = files.Read("legacy/aldermere-first-loop/source-support/data/world.json", 131_072, CargoCircuitSources.WorldDigest);
        const string original = "\"id\": \"bandit_chief\",\n      \"name\": \"Bandit Chief\",\n      \"level\": 7,";
        Assert.Contains(original, monsters);
        using JsonDocument edited = JsonDocument.Parse(monsters.Replace(original, original.Replace("7,", "6,")));
        using JsonDocument places = JsonDocument.Parse(world);
        Assert.Throws<InvalidDataException>(() => RoadThreatSources.Bind(edited.RootElement, places.RootElement));
        using JsonDocument unchanged = JsonDocument.Parse(monsters);
        Assert.Equal(RoadThreatSources.LoadEmbedded().Version, RoadThreatSources.Bind(unchanged.RootElement, places.RootElement).Version);
    }

    [Fact]
    public void The_fresh_threatened_cargo_world_admits_one_chief_at_the_camp_crossing_with_its_source_attack()
    {
        WorldSnapshot world = RoadThreatWorldAdmission.CreateEmbedded(Seed, "Traveler");
        RoadThreatSources sources = RoadThreatSources.LoadEmbedded();
        RoadThreatBinding threat = RoadThreatWorldAdmission.Find(world, CargoCircuitSources.LoadEmbedded(), sources)!;
        Assert.NotNull(threat);
        Assert.Equal(WorldEncounterRetreatAdmission.ProfileVersion, world.ProfileVersion);
        Assert.Equal((0UL, 0UL), (world.Revision, world.AdvancingSteps));
        EntityId chief = threat.ChiefActorId;
        EntityId player = world.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        WorldResident resident = world.GetResident(chief);
        Assert.Equal(("Bandit Chief", ResidentControllerKind.Host), (resident.DisplayName, resident.ControllerKind));
        var cell = world.Space.GetActor(chief).Feet.Cell;
        Assert.Equal((347L, 311L), (cell.X, cell.Y));
        Assert.True(sources.Camp.Contains(cell.X, cell.Y));
        ActorProgressionState progression = world.Progression!.GetActor(chief);
        Assert.Equal((7, 800, 0), (progression.Level, progression.CurrentHp, progression.CurrentMp));
        Assert.Equal(new ActorStatValues(800, 0, 27, 0, 15, 10), progression.Stats);
        Assert.Equal(0UL, world.Progression.Currency.GetActorAccount(chief).Balance);
        Assert.DoesNotContain(world.Inventory.Custody.Lots, lot => lot.CustodianId == resident.InventoryCustodianId);
        Assert.Equal(1, world.Inventory.Controls.Count(value => value.ActorId == chief));

        EncounterLifecycleSnapshot lifecycle = world.EncounterLifecycle!;
        EncounterAbilityDefinition attack = lifecycle.Abilities.Single(value => value.Key == "bandit_chief_attack");
        Assert.Equal((15u, 400L, 480u, 300u, 0u), (attack.PowerTenths, attack.RangeSubunits, attack.ReadinessCostNumerator,
            attack.ReadinessCostDenominator, attack.ManaCost));
        Assert.Equal(sources.Monster.PayloadHash, attack.SourceVersion);
        Assert.Equal(3, lifecycle.Scenarios.Count);
        EncounterScenarioDefinition scenario = threat.Scenario;
        Assert.Equal((chief, player, 400L), (scenario.InitiatingActorId, scenario.TargetActorId, scenario.EntryRangeSubunits));
        Assert.Equal(world.Residents.Count, scenario.Combatants.Count);
        foreach (EncounterCombatant combatant in scenario.Combatants)
        {
            ushort expected = (ushort)(combatant.ActorId == player ? 1 : combatant.ActorId == chief ? 2 : 0);
            Assert.Equal(expected, combatant.Side);
            Assert.Equal(expected == 0, combatant.AbilityIds.Count == 0);
        }
        // Retained wolf scenarios stay player-initiated; the chief is a bystander in each of them.
        foreach (EncounterScenarioDefinition wolf in lifecycle.Scenarios.Where(value => value.DefinitionId != threat.ScenarioId))
        {
            Assert.Equal(player, wolf.InitiatingActorId);
            Assert.Equal((ushort)0, wolf.Combatants.Single(value => value.ActorId == chief).Side);
        }
        Assert.Single(LocalRoadThreatController.HostileScenarios(world, player));
    }

    [Fact]
    public void Composition_is_byte_deterministic_and_never_relabels_another_world()
    {
        WorldSnapshot first = RoadThreatWorldAdmission.CreateEmbedded(Seed, "Traveler");
        WorldSnapshot second = RoadThreatWorldAdmission.CreateEmbedded(Seed, "Traveler");
        Assert.Equal(WorldSnapshotCodec.Encode(first), WorldSnapshotCodec.Encode(second));
        WorldSnapshot retreat = CargoRetreatWorldAdmission.CreateEmbedded(Seed, "Traveler");
        CargoCircuitSources cargo = CargoCircuitSources.LoadEmbedded();
        RoadThreatSources sources = RoadThreatSources.LoadEmbedded();
        Assert.Null(RoadThreatWorldAdmission.Find(retreat, cargo, sources));
        EntityId player = retreat.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        Assert.Empty(LocalRoadThreatController.HostileScenarios(retreat, player));
        // A world without a hostile opportunity keeps its exact public view: the threat projection is null and omitted.
        Assert.Null(new LocalWorldView(retreat, player).RoadThreats);
        Assert.DoesNotContain("RoadThreats", JsonSerializer.Serialize(new LocalWorldView(retreat, player)), StringComparison.Ordinal);
        EntityId traveller = first.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        // The threatened world publishes its observed subset: at the farm start the chief is unobserved, so it is empty.
        Assert.Empty(new LocalWorldView(first, traveller).RoadThreats!);
        Assert.NotEqual(retreat.ContentHash, first.ContentHash);
        // An admitted (non-genesis) root cannot be composed again as this selection.
        Assert.ThrowsAny<ArgumentException>(() => RoadThreatWorldAdmission.Compose(retreat, cargo, sources));
        Assert.ThrowsAny<ArgumentException>(() => RoadThreatWorldAdmission.Compose(first, cargo, sources));
    }
}
