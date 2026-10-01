using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;

namespace OpenFantasy.Hosting.Content.RoadThreat;

/// <summary>The admitted road threat of one world: the chief resident and its hostile scenario.</summary>
public sealed record RoadThreatBinding(EntityId ChiefActorId, EntityId ScenarioId, EncounterScenarioDefinition Scenario);

/// <summary>
/// Explicit fresh selection of the threatened cargo world: the accepted two-market cargo composition with the retained
/// camp's Bandit Chief added to its unplayed genesis, then paid exchange, the two counters and the profile-34 retreat
/// law, all unchanged. It never opens, upgrades or relabels a save; the plain cargo and retreat worlds keep their own
/// genesis bytes and meaning, and a played root cannot become this genesis.
/// </summary>
public static class RoadThreatWorldAdmission
{
    /// <summary>The production seed of a fresh native threatened cargo world.</summary>
    public const string SeedText = "road-threat-world";

    public static WorldSnapshot CreateEmbedded(string seedText, string playerName) =>
        Compose(RestorativeJourneyAdmission.CreateEmbedded(seedText, playerName), CargoCircuitSources.LoadEmbedded(),
            RoadThreatSources.LoadEmbedded());

    /// <summary>Admits the circuit, the chief and the retreat law into an unplayed ordinary-care root.</summary>
    public static WorldSnapshot Compose(WorldSnapshot ordinaryCareGenesis, CargoCircuitSources cargo, RoadThreatSources threat)
    {
        ArgumentNullException.ThrowIfNull(ordinaryCareGenesis);
        ArgumentNullException.ThrowIfNull(cargo);
        ArgumentNullException.ThrowIfNull(threat);
        WorldSnapshot circuit = CargoWorldAdmission.Compose(ordinaryCareGenesis, cargo,
            extended => new RoadThreatGenesisExtension(extended, threat).Build());
        return WorldEncounterRetreatAdmission.CreateFresh(circuit);
    }

    /// <summary>
    /// The world's road threat when it is a retreat-capable cargo world of these sources whose catalog holds the
    /// chief's scenario with the source attack, otherwise null. The identities are derived from the world itself.
    /// </summary>
    public static RoadThreatBinding? Find(WorldSnapshot world, CargoCircuitSources cargo, RoadThreatSources threat)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(cargo);
        ArgumentNullException.ThrowIfNull(threat);
        if (world.ProfileVersion != WorldEncounterRetreatAdmission.ProfileVersion || CargoWorldAdmission.Find(world, cargo) is null
            || world.EncounterLifecycle is not { } lifecycle)
        {
            return null;
        }
        EntityId chief = ConstructionFarmIdentityScope.InstanceId(world, RoadThreatGenesisExtension.IdentityProfile, "chief.actor");
        EntityId scenarioId = ConstructionFarmIdentityScope.InstanceId(world, RoadThreatGenesisExtension.IdentityProfile,
            "scenario.bandit-chief");
        EncounterAbilityDefinition attack = RoadThreatGenesisExtension.Attack(threat);
        EncounterScenarioDefinition? scenario = lifecycle.Scenarios.SingleOrDefault(value => value.DefinitionId == scenarioId);
        bool matches = scenario is not null && scenario.InitiatingActorId == chief
            && scenario.EntryRangeSubunits == attack.RangeSubunits
            && lifecycle.Abilities.Any(value => value.DefinitionId == attack.DefinitionId && value.Version == attack.Version)
            && world.Residents.Any(resident => resident.ActorId == chief && resident.ControllerKind == ResidentControllerKind.Host);
        return matches ? new RoadThreatBinding(chief, scenarioId, scenario!) : null;
    }
}
