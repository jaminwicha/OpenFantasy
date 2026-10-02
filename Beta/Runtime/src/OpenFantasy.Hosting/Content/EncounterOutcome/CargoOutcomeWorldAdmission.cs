using OpenFantasy.Core;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;
using OpenFantasy.Hosting.Content.RoadThreat;

namespace OpenFantasy.Hosting.Content.EncounterOutcome;

/// <summary>
/// Explicit fresh selection of the outcome-completion cargo world: the accepted threatened cargo composition (farm,
/// cargo circuit, the camp's Bandit Chief, paid exchange, counters and the profile-34 retreat law), unchanged, followed
/// by the profile-35 completion law. It never opens, upgrades or relabels a save: threatened, retreat and plain cargo
/// saves keep their own profiles and meanings, and a lived profile-34 save needs its own explicit replayed transition.
/// </summary>
public static class CargoOutcomeWorldAdmission
{
    /// <summary>The production seed of a fresh native outcome-completion cargo world.</summary>
    public const string SeedText = "cargo-outcome-world";

    public static WorldSnapshot CreateEmbedded(string seedText, string playerName) =>
        WorldEncounterOutcomeAdmission.CreateFresh(RoadThreatWorldAdmission.CreateEmbedded(seedText, playerName));

    /// <summary>Fresh production cooperative roster; no lived save or existing NPC control is changed.</summary>
    public static WorldSnapshot CreateEmbedded(string seedText, IReadOnlyList<string> playerNames) =>
        WorldEncounterOutcomeAdmission.CreateFresh(RoadThreatWorldAdmission.CreateEmbedded(seedText, playerNames));

    /// <summary>
    /// A declared fresh calibration of the same composition for qualification only: the chief starts wounded at the
    /// given HP. Its admission identity differs from the production selection; it is not balance or lived history.
    /// </summary>
    public static WorldSnapshot CreateWoundedChief(string seedText, string playerName, int chiefHp)
    {
        WorldSnapshot threatened = RoadThreatWorldAdmission.CreateEmbedded(seedText, playerName);
        RoadThreatBinding threat = RoadThreatWorldAdmission.Find(threatened, CargoCircuitSources.LoadEmbedded(), RoadThreatSources.LoadEmbedded())
            ?? throw new InvalidDataException("The threatened genesis lost its chief.");
        return WorldEncounterOutcomeAdmission.CreateCalibratedFresh(threatened,
            new EncounterOutcomeCalibration(new Dictionary<EntityId, int> { [threat.ChiefActorId] = chiefHp }, []));
    }

    /// <summary>
    /// A declared fresh two-human managed calibration: both inherited Bramble Wolves are bound to human controllers (their
    /// existing control bindings), and the first wolf starts wounded at the given HP. The first wolf scenario then has a
    /// required human on each positive side and the second wolf is an unrelated side-zero human bystander.
    /// </summary>
    public static WorldSnapshot CreateTwoHumanWolves(string seedText, string playerName, int firstWolfHp)
    {
        WorldSnapshot threatened = RoadThreatWorldAdmission.CreateEmbedded(seedText, playerName);
        var lifecycle = threatened.EncounterLifecycle!;
        EntityId first = lifecycle.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.FirstScenarioKey).TargetActorId;
        EntityId second = lifecycle.Scenarios.Single(value => value.Key == EncounterJourneyAdmission.SecondScenarioKey).TargetActorId;
        return WorldEncounterOutcomeAdmission.CreateCalibratedFresh(threatened,
            new EncounterOutcomeCalibration(new Dictionary<EntityId, int> { [first] = firstWolfHp }, [first, second]));
    }

    /// <summary>The road threat of an outcome-completion cargo world of these sources, otherwise null.</summary>
    public static RoadThreatBinding? Find(WorldSnapshot world, CargoCircuitSources cargo, RoadThreatSources threat)
    {
        ArgumentNullException.ThrowIfNull(world);
        return world.ProfileVersion == WorldEncounterOutcomeAdmission.ProfileVersion
            ? RoadThreatWorldAdmission.FindInProfile(world, cargo, threat, WorldEncounterOutcomeAdmission.ProfileVersion) : null;
    }

    /// <summary>Whether the world is an outcome-completion cargo world of these sources.</summary>
    public static bool IsOutcomeCargoWorld(WorldSnapshot world, CargoCircuitSources sources)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(sources);
        return world.ProfileVersion == WorldEncounterOutcomeAdmission.ProfileVersion && CargoWorldAdmission.Find(world, sources) is not null;
    }
}
