using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>
/// Creates a fresh exploration world with two source-backed wolves and explicit encounter opportunities.
/// Neither opportunity grants hostility, earned work, rewards, drops or a completed encounter.
/// </summary>
public static class EncounterJourneyAdmission
{
    public const string FirstScenarioKey = "bramble_wolf_first";
    public const string SecondScenarioKey = "bramble_wolf_second";

    public static WorldSnapshot CreateEmbedded(string seedText, string playerName) =>
        Create(FarmContentFiles.Embedded(), null, seedText, playerName);

    public static WorldSnapshot Create(string contentRoot, string seedText, string playerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);
        return Create(new FarmContentFiles(contentRoot), contentRoot, seedText, playerName);
    }

    internal static WorldSnapshot Create(FarmContentFiles files, string? contentRoot, string seedText, string playerName,
        Func<WorldSnapshot, WorldSnapshot>? composeFreshFarm = null)
    {
        WorldSnapshot farm = EncounterFarmBasis.Create(files, contentRoot, seedText, playerName);
        EncounterFarmSources sources = EncounterFarmSources.Load(files);
        WorldSnapshot first = new EncounterFarmWolf(farm, sources, "wolf-first", 256).AddToFreshFarm();
        WorldSnapshot occupied = new EncounterFarmWolf(first, sources, "wolf-second", -256).AddToFreshFarm();
        if (composeFreshFarm is not null)
        {
            occupied = composeFreshFarm(occupied);
        }
        // All four resident inventories and actor access witnesses exist before the actual
        // source, reservoir and liquid-cell owners bind their complete custody interpretation.
        WorldSnapshot basis = new BreachFarmBuilder(occupied).Build();
        EntityId player = basis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        EntityId wolfFirst = ConstructionFarmIdentityScope.InstanceId(basis, "encounter-farm", "wolf-first.actor");
        EntityId wolfSecond = ConstructionFarmIdentityScope.InstanceId(basis, "encounter-farm", "wolf-second.actor");
        EntityId firstScenario = ConstructionFarmIdentityScope.InstanceId(basis, "encounter-journey", "scenario.first");
        EntityId secondScenario = ConstructionFarmIdentityScope.InstanceId(basis, "encounter-journey", "scenario.second");
        EntityId strikeId = ConstructionFarmIdentityScope.DefinitionId("encounter-farm", "abilities/strike");
        EntityId biteId = ConstructionFarmIdentityScope.DefinitionId("encounter-farm", "abilities/bramble-wolf-bite");
        EntityId[] identities = [firstScenario, secondScenario, strikeId, biteId];
        if (identities.Distinct().Count() != identities.Length || identities.Any(basis.IdentityIndex.Contains))
        {
            throw new InvalidDataException("Journey scenarios and ability definitions must not alias retained authority.");
        }
        var strike = new EncounterAbilityDefinition(strikeId, EncounterFarmAdmission.StrikeKey,
            sources.Strike.GetProperty("name").GetString()!, sources.StrikeVersion,
            sources.Strike.GetProperty("power").GetUInt32(), checked((sources.Strike.GetProperty("range").GetInt64() + 8) * 8),
            sources.Strike.GetProperty("atbCost").GetUInt32(), 300, sources.Strike.GetProperty("mpCost").GetUInt32(),
            sources.Strike.GetProperty("unlockLevel").GetUInt32());
        var attack = sources.Wolf.GetProperty("attack");
        var bite = new EncounterAbilityDefinition(biteId, EncounterFarmAdmission.WolfBiteKey, "Bramble Wolf bite",
            sources.WolfVersion, attack.GetProperty("power").GetUInt32(), checked(attack.GetProperty("range").GetInt64() * 8),
            attack.GetProperty("atbCost").GetUInt32(), 300, 0, 1, BodyFunction.NaturalWeapon);
        EncounterScenarioDefinition Scenario(EntityId identity, string key, string name, EntityId wolf) =>
            new(identity, key, name, player, wolf, strike.RangeSubunits,
                basis.Residents.Select(resident => resident.ActorId == player
                    ? new EncounterCombatant(player, 1, [strikeId])
                    : resident.ActorId == wolf ? new EncounterCombatant(wolf, 2, [biteId])
                    : new EncounterCombatant(resident.ActorId, 0, [])));
        var scenarios = new[]
        {
            Scenario(firstScenario, FirstScenarioKey, "East Bramble Wolf confrontation", wolfFirst),
            Scenario(secondScenario, SecondScenarioKey, "West Bramble Wolf confrontation", wolfSecond)
        };
        WorldSnapshot paced = WorldPacingAdmission.CreateFresh(basis);
        var lifecycle = EncounterLifecycleSnapshot.BeginFresh(paced.WorldId, paced.BranchId,
            paced.Space.Regions[0].Seed, [strike, bite], scenarios, paced.Residents.Select(resident => resident.ActorId));
        return WorldEncounterLifecycleAdmission.CreateFresh(paced, lifecycle);
    }
}
