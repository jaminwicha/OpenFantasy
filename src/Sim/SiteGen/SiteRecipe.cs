namespace OpenFantasy.Sim.SiteGen;

/// <summary>
/// The topology-intensity dial (RogueFiction `MapStyle`, ported per the
/// brief): pacing lives here. Set-piece floors are deliberately lean —
/// linear floors are what make xandered floors read as xandered.
/// </summary>
public enum SiteStyle
{
    /// <summary>A handful of rooms on a single spine (entrances, boss floors).</summary>
    SetPiece,
    /// <summary>Room-and-corridor with a couple of guaranteed loops.</summary>
    Standard,
    /// <summary>Densely looped: cross-corridors, secrets, two ways down.</summary>
    Xandered,
}

public static class SiteStyleExt
{
    public static int RoomTarget(this SiteStyle s)
        => s switch { SiteStyle.SetPiece => 6, SiteStyle.Standard => 12, _ => 16 };

    public static int MinRooms(this SiteStyle s)
        => s switch { SiteStyle.SetPiece => 5, SiteStyle.Standard => 7, _ => 9 };

    /// <summary>Deliberate loop corridors beyond the spanning connections.
    /// RogueFiction's tuned values: ≥2 standard, ≥4 xandered per ~16 rooms.</summary>
    public static int ExtraLoops(this SiteStyle s)
        => s switch { SiteStyle.SetPiece => 0, SiteStyle.Standard => 2, _ => 4 };

    public static int SecretDoors(this SiteStyle s)
        => s switch { SiteStyle.SetPiece => 0, SiteStyle.Standard => 1, _ => 2 };

    /// <summary>Chance (num, den) that a clean doorway gets an actual door.</summary>
    public static (int num, int den) DoorChance(this SiteStyle s)
        => s == SiteStyle.SetPiece ? (1, 4) : (1, 3);

    /// <summary>The descent's pacing: set-piece opening and finale, xandered
    /// chapter floors between, standard connective tissue.</summary>
    public static SiteStyle ForFloor(int floor, int floorCount)
        => floor == 0 || floor == floorCount - 1 ? SiteStyle.SetPiece
            : floor % 2 == 1 ? SiteStyle.Xandered : SiteStyle.Standard;
}

/// <summary>Signature terrain layered onto a floor, so recipes differ in
/// structure and not only in monsters (plan §3.4 "pass layering").</summary>
public enum TerrainPass
{
    None,
    /// <summary>Rubble heaps break up the rooms (catacombs).</summary>
    Rubble,
    /// <summary>Flooded lanes cut through walls — risky shortcuts (sewers, crypts).</summary>
    Water,
    /// <summary>Cellular erosion softens rooms into caverns (mines, caves).</summary>
    Caves,
    /// <summary>Slag channels vein the halls (emberdeep).</summary>
    Slag,
}

/// <summary>
/// A data-declared site family recipe (plan §3.5). The structural embedder is
/// shared; recipes select terrain passes, pacing, scale, and 7C theming.
/// </summary>
public sealed class SiteRecipe
{
    public required string Id;
    public required int Floors;
    public int Width = 72;
    public int Height = 42;
    public TerrainPass Terrain = TerrainPass.None;
    /// <summary>Chasm drops (level-skip links) on qualifying floors.</summary>
    public bool Chasms = true;
    /// <summary>Force every floor to one style (sewers: all Xandered).</summary>
    public SiteStyle? StyleOverride;
    /// <summary>Detour (BFS steps) a secret must shortcut to be worth placing.
    /// RogueFiction shipped 18 first — dead content on &lt;50% of floors —
    /// and relaxed to 12 measured across 200 seeds.</summary>
    public int SecretDetourMin = 12;

    // ---- 7C theming hooks (monster ids from data/monsters.json) ----
    public string[] SpawnMonsters = Array.Empty<string>();
    public string BossMonster = "";

    public SiteStyle StyleFor(int floor)
        => StyleOverride ?? SiteStyleExt.ForFloor(floor, Floors);
}
