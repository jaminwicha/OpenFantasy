using OpenFantasy.Core;
using OpenFantasy.Core.Actors;
using OpenFantasy.Core.Encounters;
using OpenFantasy.Core.Worlds;

namespace OpenFantasy.Hosting.Content;

/// <summary>
/// Creates a separate fresh Squire-versus-wolf encounter with every terrain-breach owner retained.
/// This explicit confrontation is not a species hostility, reward, drop or respawn policy.
/// </summary>
public static class EncounterFarmAdmission
{
    public const string StrikeKey = "strike";
    public const string WolfBiteKey = "bramble_wolf_bite";

    public static WorldSnapshot CreateEmbedded(string seedText, string playerName) =>
        Create(FarmContentFiles.Embedded(), null, seedText, playerName);

    public static WorldSnapshot Create(string contentRoot, string seedText, string playerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);
        return Create(new FarmContentFiles(contentRoot), contentRoot, seedText, playerName);
    }

    private static WorldSnapshot Create(FarmContentFiles files, string? contentRoot, string seedText, string playerName)
    {
        WorldSnapshot farm = EncounterFarmBasis.Create(files, contentRoot, seedText, playerName);
        EncounterFarmSources sources = EncounterFarmSources.Load(files);
        WorldSnapshot occupiedFarm = new EncounterFarmWolf(farm, sources).AddToFreshFarm();
        // Reuse the actual authored breach composition. The added empty actor inventory is
        // present before runoff/reservoir/cell owners bind their complete source and custody pins.
        WorldSnapshot basis = new BreachFarmBuilder(occupiedFarm).Build();
        EntityId player = basis.Residents.Single(value => value.ControllerKind == ResidentControllerKind.Player).ActorId;
        EntityId wolf = basis.Progression!.Actors.Single(value => value.Profile.Key == "bramble_wolf").ActorId;
        EntityId encounter = ConstructionFarmIdentityScope.InstanceId(basis, "encounter-farm", "encounter");
        EntityId strikeId = ConstructionFarmIdentityScope.DefinitionId("encounter-farm", "abilities/strike");
        EntityId biteId = ConstructionFarmIdentityScope.DefinitionId("encounter-farm", "abilities/bramble-wolf-bite");
        EntityId[] identities = [encounter, strikeId, biteId];
        if (identities.Distinct().Count() != identities.Length || identities.Any(basis.IdentityIndex.Contains))
        {
            throw new InvalidDataException("The encounter and ability identities must be distinct from every retained owner.");
        }
        var strike = new EncounterAbilityDefinition(strikeId, StrikeKey, sources.Strike.GetProperty("name").GetString()!,
            sources.StrikeVersion, sources.Strike.GetProperty("power").GetUInt32(),
            checked((sources.Strike.GetProperty("range").GetInt64() + 8) * 8),
            sources.Strike.GetProperty("atbCost").GetUInt32(), 300, sources.Strike.GetProperty("mpCost").GetUInt32(),
            sources.Strike.GetProperty("unlockLevel").GetUInt32());
        var attack = sources.Wolf.GetProperty("attack");
        var bite = new EncounterAbilityDefinition(biteId, WolfBiteKey, "Bramble Wolf bite", sources.WolfVersion,
            attack.GetProperty("power").GetUInt32(), checked(attack.GetProperty("range").GetInt64() * 8),
            attack.GetProperty("atbCost").GetUInt32(), 300, 0, 1, BodyFunction.NaturalWeapon);
        WorldSnapshot paced = WorldPacingAdmission.CreateFresh(basis, encounter);
        var combatants = basis.Residents.Select(resident => resident.ActorId == player
            ? new EncounterCombatant(player, 1, [strikeId])
            : resident.ActorId == wolf ? new EncounterCombatant(wolf, 2, [biteId])
            : new EncounterCombatant(resident.ActorId, 0, []));
        EncounterActionSnapshot actions = EncounterActionSnapshot.Begin(paced.Pacing!.Encounter!.Scope,
            basis.Space.Regions[0].Seed, [strike, bite], combatants);
        return WorldEncounterActionAdmission.CreateFresh(paced, actions);
    }
}
