namespace OpenFantasy.Sim.SiteGen;

public readonly record struct Pt(int X, int Y);

public sealed class Room
{
    public int X, Y, W, H;
    public Pt Center => new(X + W / 2, Y + H / 2);

    /// <summary>One-tile padding so rooms keep separating walls.</summary>
    public bool Intersects(Room o)
        => X <= o.X + o.W && X + W >= o.X - 1 && Y <= o.Y + o.H && Y + H >= o.Y - 1;
}

/// <summary>
/// One generated floor of an adventure site: abstract tile grid plus the
/// feature positions 7C needs to wire a MapInstance (stairs, spawns, chests).
/// Produced pure — identical for identical (worldSeed, siteId, floor, salt).
/// </summary>
public sealed class SiteFloor
{
    public required int Width;
    public required int Height;
    public required SiteTile[] Tiles;

    public List<Room> Rooms = new();
    /// <summary>Entry point (stairs up / surface exit position).</summary>
    public Pt Entry;
    /// <summary>Primary way down (default target of StairsDown).</summary>
    public Pt StairsDown;
    /// <summary>Secondary way down on xandered floors (may be Chasm-flavored).</summary>
    public Pt? StairsDown2;
    /// <summary>Deliberate loop corridors carved beyond spanning connections.</summary>
    public int Loops;
    /// <summary>Landmark room center (orientation anchor).</summary>
    public Pt Landmark;
    /// <summary>Boss room center (final floor only).</summary>
    public Pt? Boss;
    /// <summary>Monster spawn points with a difficulty weight per point.</summary>
    public List<Pt> Spawns = new();
    /// <summary>Chest positions; chest id = index (stable per derivation).</summary>
    public List<Pt> Chests = new();

    public SiteTile Tile(int x, int y)
        => x < 0 || y < 0 || x >= Width || y >= Height ? SiteTile.Wall : Tiles[y * Width + x];

    public void Set(int x, int y, SiteTile t)
    {
        if (x >= 0 && y >= 0 && x < Width && y < Height)
            Tiles[y * Width + x] = t;
    }

    /// <summary>FNV-1a over the tile grid + features — determinism fingerprint.</summary>
    public ulong ContentHash()
    {
        ulong h = 1469598103934665603UL;
        void Mix(long v) { h ^= (ulong)v; h *= 1099511628211UL; }
        foreach (var t in Tiles) Mix((byte)t);
        Mix(Entry.X); Mix(Entry.Y);
        Mix(StairsDown.X); Mix(StairsDown.Y);
        foreach (var s in Spawns) { Mix(s.X); Mix(s.Y); }
        foreach (var c in Chests) { Mix(c.X); Mix(c.Y); }
        return h;
    }
}
