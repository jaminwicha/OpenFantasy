namespace OpenFantasy.Sim.SiteGen;

/// <summary>
/// Abstract tile vocabulary for generated adventure-site floors. Embedders
/// emit these; 7C integration maps them onto the shipped tileset (rock,
/// mine_floor, …) when a floor becomes a live MapInstance. Keeping the
/// vocabulary abstract lets generation, validation, and preview run without
/// any content files.
/// </summary>
public enum SiteTile : byte
{
    Wall,
    Floor,
    /// <summary>Way down to the next floor (or the delve's boss reward exit).</summary>
    StairsDown,
    /// <summary>Way back up; on floor 0 this is the surface exit.</summary>
    StairsUp,
    /// <summary>Blocks like wall until searched; then opens into floor.</summary>
    SecretDoor,
    /// <summary>Closed door: blocks until opened (bump/interact).</summary>
    Door,
    /// <summary>Locked: needs the floor's key or a pry. Never load-bearing.</summary>
    LockedDoor,
    /// <summary>Flooded passage: walkable, risky, canoe-flavored in sewers.</summary>
    Water,
    /// <summary>Molten/ember runoff: walkable at a price (emberdeep flavor).</summary>
    Slag,
    /// <summary>Collapsed masonry: blocks passage (decor + cover).</summary>
    Rubble,
    /// <summary>A chosen drop to a deeper floor (level-skip link).</summary>
    Chasm,
    /// <summary>Treasure chest marker (contents = Hash(seed, chestId), D9).</summary>
    Chest,
}

public static class SiteTileExt
{
    public static bool IsWalkable(this SiteTile t) => t is SiteTile.Floor
        or SiteTile.StairsDown or SiteTile.StairsUp
        or SiteTile.Water or SiteTile.Slag or SiteTile.Chest;

    /// <summary>Passability for the "honest route" proofs: secrets opaque,
    /// locks sealed, plain doors passable (they open on a bump).</summary>
    public static bool IsHonestPassable(this SiteTile t)
        => t.IsWalkable() || t == SiteTile.Door;
}
