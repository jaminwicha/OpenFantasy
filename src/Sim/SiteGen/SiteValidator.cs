namespace OpenFantasy.Sim.SiteGen;

/// <summary>
/// The Jaquays invariant validator (plan D11). Ported proofs from
/// RogueFiction's test family, run at generation time per floor, plus the
/// articulation-point check the reference lacked (invariant 5). A floor that
/// fails regenerates with salt+1 — still a pure function of the seed.
/// </summary>
public static class SiteValidator
{
    /// <summary>Returns null when the floor passes, else a reason string
    /// (surfaced by selftest; generation just retries with the next salt).</summary>
    public static string? Validate(SiteFloor f, SiteStyle style, SiteRecipe recipe, bool bossFloor)
    {
        if (f.Rooms.Count < style.MinRooms())
            return $"too few rooms ({f.Rooms.Count} < {style.MinRooms()})";

        // Honest route: secrets opaque, locks sealed, plain doors passable.
        var goal = bossFloor ? (f.Boss ?? f.StairsDown) : f.StairsDown;
        if (!GridSearch.Reaches(f, f.Entry, goal, t => t.IsHonestPassable()))
            return "goal unreachable on the honest route (a lock or secret is load-bearing)";

        if (f.Loops < style.ExtraLoops())
            return $"too few loops ({f.Loops} < {style.ExtraLoops()})";

        if (style == SiteStyle.Xandered && f.StairsDown2 is null && !bossFloor)
            return "xandered floor lacks a second way down";

        // Secrets must shortcut a real detour, and never gate anything.
        int secrets = 0;
        for (int y = 0; y < f.Height; y++)
            for (int x = 0; x < f.Width; x++)
            {
                if (f.Tile(x, y) != SiteTile.SecretDoor) continue;
                secrets++;
                // Same orientation rule as placement: strict Floor sides.
                var (a, b) = f.Tile(x - 1, y) == SiteTile.Floor && f.Tile(x + 1, y) == SiteTile.Floor
                    ? (new Pt(x - 1, y), new Pt(x + 1, y))
                    : (new Pt(x, y - 1), new Pt(x, y + 1));
                // A later pass may have disturbed a side (chest, chasm); the
                // global honest-route flood still holds, so skip the metric.
                if (!f.Tile(a.X, a.Y).IsWalkable() || !f.Tile(b.X, b.Y).IsWalkable()) continue;
                int detour = GridSearch.WalkDistance(f, a, b, doorsPassable: true);
                if (detour >= 0 && detour < recipe.SecretDetourMin)
                    return $"secret at {x},{y} shortcuts only {detour} steps";
            }
        if (secrets > style.SecretDoors())
            return $"too many secrets ({secrets} > {style.SecretDoors()})";

        // Invariant 5 — no accidental chokepoints: no single room may sever
        // the majority of the floor. Set-piece floors are exempt (their
        // linear spine is the deliberate exception that keeps pacing).
        if (style != SiteStyle.SetPiece && f.Rooms.Count >= 6)
        {
            int worst = WorstArticulationSeverance(f);
            if (worst > f.Rooms.Count / 2)
                return $"a room severs {worst}/{f.Rooms.Count} rooms";
        }

        if (bossFloor && f.Boss is null)
            return "boss floor without a boss room";

        return null;
    }

    /// <summary>
    /// For each room, flood the walkable grid with that room's tiles removed;
    /// the worst count of other rooms cut off from the entry approximates
    /// room-graph articulation on the real embedded geometry.
    /// </summary>
    private static int WorstArticulationSeverance(SiteFloor f)
    {
        int worst = 0;
        var seen = GridSearch.Flood(f, f.Entry, t => t.IsHonestPassable());
        for (int i = 1; i < f.Rooms.Count; i++) // entry room exempt: you start there
        {
            var r = f.Rooms[i];
            // Count rooms whose centers become unreachable from the entry
            // when this room's tiles are treated as walls.
            var blocked = FloodMasked(f, r);
            int cut = 0;
            for (int j = 0; j < f.Rooms.Count; j++)
            {
                if (j == i) continue;
                var c = f.Rooms[j].Center;
                if (seen[c.Y * f.Width + c.X] && !blocked[c.Y * f.Width + c.X]) cut++;
            }
            worst = Math.Max(worst, cut);
        }
        return worst;
    }

    private static bool[] FloodMasked(SiteFloor f, Room masked)
    {
        bool InMask(int x, int y)
            => x >= masked.X && y >= masked.Y && x < masked.X + masked.W && y < masked.Y + masked.H;
        var seen = new bool[f.Width * f.Height];
        var stack = new Stack<Pt>();
        if (!InMask(f.Entry.X, f.Entry.Y)) stack.Push(f.Entry);
        while (stack.Count > 0)
        {
            var p = stack.Pop();
            if (p.X < 0 || p.Y < 0 || p.X >= f.Width || p.Y >= f.Height) continue;
            if (seen[p.Y * f.Width + p.X]) continue;
            if (InMask(p.X, p.Y) || !f.Tile(p.X, p.Y).IsHonestPassable()) continue;
            seen[p.Y * f.Width + p.X] = true;
            stack.Push(new Pt(p.X + 1, p.Y));
            stack.Push(new Pt(p.X - 1, p.Y));
            stack.Push(new Pt(p.X, p.Y + 1));
            stack.Push(new Pt(p.X, p.Y - 1));
        }
        return seen;
    }
}
