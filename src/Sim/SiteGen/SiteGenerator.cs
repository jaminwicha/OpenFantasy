namespace OpenFantasy.Sim.SiteGen;

/// <summary>
/// Floor factory: pure derivation per plan D3/D8 — a fresh RNG per
/// (worldSeed, siteId, floor, salt), so any floor regenerates identically on
/// demand in any order. Validation failures retry with salt+1 (D11); a floor
/// that exhausts its salts throws, which the selftest treats as a generator
/// bug, never a runtime hazard.
/// </summary>
public static class SiteGenerator
{
    public const int MaxSalt = 32;

    public static SiteFloor GenerateFloor(ulong worldSeed, string siteId, int floor, SiteRecipe recipe)
    {
        var style = recipe.StyleFor(floor);
        bool bossFloor = floor == recipe.Floors - 1;
        // Chasms skip a level; only floors with a floor+2 below qualify.
        bool allowChasm = floor + 2 < recipe.Floors;
        ulong siteSeed = StableId.Domain(worldSeed, "site:" + siteId);
        for (int salt = 0; salt < MaxSalt; salt++)
        {
            var rng = new XorShift64(StableId.Combine(siteSeed, floor, salt));
            var f = RoomScatterEmbedder.Embed(rng, recipe, style, bossFloor, allowChasm);
            if (SiteValidator.Validate(f, style, recipe, bossFloor) is null)
                return f;
        }
        throw new InvalidOperationException(
            $"site {siteId} floor {floor}: no valid layout in {MaxSalt} salts — generator bug");
    }

    /// <summary>Last validation failure reasons for tooling/tests.</summary>
    public static List<string> ValidationSurvey(ulong worldSeed, string siteId, SiteRecipe recipe)
    {
        var reasons = new List<string>();
        for (int floor = 0; floor < recipe.Floors; floor++)
        {
            var style = recipe.StyleFor(floor);
            bool bossFloor = floor == recipe.Floors - 1;
            bool allowChasm = floor + 2 < recipe.Floors;
            ulong siteSeed = StableId.Domain(worldSeed, "site:" + siteId);
            for (int salt = 0; salt < MaxSalt; salt++)
            {
                var rng = new XorShift64(StableId.Combine(siteSeed, floor, salt));
                var f = RoomScatterEmbedder.Embed(rng, recipe, style, bossFloor, allowChasm);
                string? reason = SiteValidator.Validate(f, style, recipe, bossFloor);
                if (reason is null) break;
                reasons.Add($"{siteId}[{floor}] salt {salt}: {reason}");
            }
        }
        return reasons;
    }
}

/// <summary>
/// The shipped adventure sites (7C): the four existing dungeon regions become
/// gateways to multi-floor delves, plus the Brasshollow sewers — the
/// multiple-entrance proof site under the anchor capital.
/// Monster/boss ids reference data/monsters.json (validated at load).
/// </summary>
public static class SiteRegistry
{
    public static readonly Dictionary<string, SiteRecipe> Sites = new()
    {
        ["hollow_mine"] = new SiteRecipe
        {
            Id = "hollow_mine",
            Floors = 5,
            Terrain = TerrainPass.Caves,
            SpawnMonsters = new[] { "cave_bat", "ore_slime", "rock_scuttler", "gold_slime" },
            BossMonster = "mine_foreman",
        },
        ["sunken_crypt"] = new SiteRecipe
        {
            Id = "sunken_crypt",
            Floors = 5,
            Terrain = TerrainPass.Rubble,
            SpawnMonsters = new[] { "crypt_wight", "gloom_wisp", "blood_wisp" },
            BossMonster = "crypt_king",
        },
        ["emberdeep"] = new SiteRecipe
        {
            Id = "emberdeep",
            Floors = 5,
            Terrain = TerrainPass.Slag,
            SpawnMonsters = new[] { "ember_wisp", "ash_hound", "obsidian_golem" },
            BossMonster = "magitek_warden",
        },
        ["storm_spire"] = new SiteRecipe
        {
            Id = "storm_spire",
            Floors = 5,
            Width = 48,
            Height = 36,
            Terrain = TerrainPass.None,
            Chasms = false, // a tower: stories, not drops
            SpawnMonsters = new[] { "storm_bat", "storm_elemental", "gloom_wisp" },
            BossMonster = "storm_warden",
        },
        ["sewer_brasshollow"] = new SiteRecipe
        {
            Id = "sewer_brasshollow",
            Floors = 2,
            Terrain = TerrainPass.Water,
            Chasms = false,
            StyleOverride = SiteStyle.Xandered, // loop-lattice under the capital
            SpawnMonsters = new[] { "cave_bat", "bog_slime", "gold_slime" },
            BossMonster = "bandit_chief",
        },
    };
}
