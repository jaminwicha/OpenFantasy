namespace OpenFantasy.Sim.SiteGen;

/// <summary>
/// Grid search primitives for the topology safety proofs (ported from
/// RogueFiction `map.rs` per the porting brief): BFS walk distance with
/// secrets opaque, and configurable flood reachability for the
/// "honest route" invariants.
/// </summary>
public static class GridSearch
{
    /// <summary>
    /// BFS step count between two tiles. Secrets and closed doors are opaque
    /// unless <paramref name="doorsPassable"/> — this is the detour metric
    /// that decides whether a secret shortcut is worth existing.
    /// </summary>
    public static int WalkDistance(SiteFloor f, Pt from, Pt to, bool doorsPassable = false)
    {
        if (from == to) return 0;
        var dist = new int[f.Width * f.Height];
        Array.Fill(dist, -1);
        var queue = new Queue<Pt>();
        dist[from.Y * f.Width + from.X] = 0;
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            int d = dist[p.Y * f.Width + p.X];
            foreach (var n in Neighbors(p))
            {
                if (n.X < 0 || n.Y < 0 || n.X >= f.Width || n.Y >= f.Height) continue;
                if (dist[n.Y * f.Width + n.X] >= 0) continue;
                var t = f.Tile(n.X, n.Y);
                if (!(t.IsWalkable() || (doorsPassable && t == SiteTile.Door))) continue;
                dist[n.Y * f.Width + n.X] = d + 1;
                if (n == to) return d + 1;
                queue.Enqueue(n);
            }
        }
        return -1;
    }

    /// <summary>
    /// Flood fill from <paramref name="from"/>: which tiles are reachable.
    /// Passability is configurable so the same flood proves all three
    /// invariant variants (honest route / locks sealed / secrets revealed).
    /// </summary>
    public static bool[] Flood(SiteFloor f, Pt from, Func<SiteTile, bool> passable)
    {
        var seen = new bool[f.Width * f.Height];
        var stack = new Stack<Pt>();
        stack.Push(from);
        if (passable(f.Tile(from.X, from.Y)))
            seen[from.Y * f.Width + from.X] = true;
        while (stack.Count > 0)
        {
            var p = stack.Pop();
            foreach (var n in Neighbors(p))
            {
                if (n.X < 0 || n.Y < 0 || n.X >= f.Width || n.Y >= f.Height) continue;
                if (seen[n.Y * f.Width + n.X] || !passable(f.Tile(n.X, n.Y))) continue;
                seen[n.Y * f.Width + n.X] = true;
                stack.Push(n);
            }
        }
        return seen;
    }

    public static bool Reaches(SiteFloor f, Pt from, Pt to, Func<SiteTile, bool> passable)
        => Flood(f, from, passable)[to.Y * f.Width + to.X];

    private static Pt[] Neighbors(Pt p)
        => new[] { new Pt(p.X + 1, p.Y), new Pt(p.X - 1, p.Y), new Pt(p.X, p.Y + 1), new Pt(p.X, p.Y - 1) };
}
