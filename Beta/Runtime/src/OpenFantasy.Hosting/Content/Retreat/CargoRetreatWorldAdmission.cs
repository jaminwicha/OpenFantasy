using OpenFantasy.Core.Worlds;
using OpenFantasy.Hosting.Content.Cargo;

namespace OpenFantasy.Hosting.Content.Retreat;

/// <summary>
/// Explicit fresh selection of the retreat-capable cargo world: the accepted two-market cargo composition, unchanged,
/// followed by the profile-34 retreat admission. Every merchant, stock, purse, wolf, corridor and water owner keeps its
/// cargo meaning. It never opens, upgrades or relabels a save; a lived cargo save needs its own explicit transition.
/// </summary>
public static class CargoRetreatWorldAdmission
{
    /// <summary>The production seed of a fresh native retreat-capable cargo world.</summary>
    public const string SeedText = "cargo-retreat-world";

    public static WorldSnapshot CreateEmbedded(string seedText, string playerName) =>
        WorldEncounterRetreatAdmission.CreateFresh(CargoWorldAdmission.CreateEmbedded(seedText, playerName));

    /// <summary>Whether the world is a retreat-capable cargo world of these sources.</summary>
    public static bool IsRetreatCargoWorld(WorldSnapshot world, CargoCircuitSources sources)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(sources);
        return world.ProfileVersion == WorldEncounterRetreatAdmission.ProfileVersion && CargoWorldAdmission.Find(world, sources) is not null;
    }
}
