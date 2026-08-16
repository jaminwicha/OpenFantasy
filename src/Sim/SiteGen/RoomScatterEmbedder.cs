namespace OpenFantasy.Sim.SiteGen;

/// <summary>
/// Room-scatter structural embedder, ported from RogueFiction `map.rs` per
/// the porting brief: scatter rooms → connect each to its nearest carved
/// neighbor (local links read as architecture, and their crossings become
/// loops) → deliberate loop edges (the first targets the goal room, so the
/// objective is reachable more than one way) → doors with lock-safety proofs
/// → secret shortcuts gated on the detour metric → terrain passes.
/// All randomness from the caller's floor-scoped RNG (salt-retry pure).
/// </summary>
public static class RoomScatterEmbedder
{
    public static SiteFloor Embed(XorShift64 rng, SiteRecipe recipe, SiteStyle style, bool bossFloor, bool allowChasm)
    {
        int w = recipe.Width, h = recipe.Height;
        var f = new SiteFloor
        {
            Width = w,
            Height = h,
            Tiles = new SiteTile[w * h], // all Wall (enum default 0)
        };

        var edges = new List<(int a, int b)>();

        // ---- scatter + nearest-connect --------------------------------
        for (int attempt = 0; attempt < 130 && f.Rooms.Count < style.RoomTarget(); attempt++)
        {
            var room = new Room
            {
                W = rng.Range(4, 9),
                H = rng.Range(3, 6),
            };
            room.X = rng.Range(1, w - room.W - 2);
            room.Y = rng.Range(1, h - room.H - 2);
            bool overlaps = false;
            foreach (var r in f.Rooms)
                if (r.Intersects(room)) { overlaps = true; break; }
            if (overlaps) continue;

            CarveRoom(f, room);
            var c = room.Center;
            if (f.Rooms.Count > 0)
            {
                int nearest = 0;
                long best = long.MaxValue;
                for (int i = 0; i < f.Rooms.Count; i++)
                {
                    var p = f.Rooms[i].Center;
                    long d = Math.Max(Math.Abs(p.X - c.X), Math.Abs(p.Y - c.Y));
                    if (d < best) { best = d; nearest = i; }
                }
                edges.Add((nearest, f.Rooms.Count));
                Connect(f, f.Rooms[nearest].Center, c, rng);
            }
            f.Rooms.Add(room);
        }
        if (f.Rooms.Count < 2) return f; // caller's validator rejects; salt-retry

        // ---- entry + goal room (farthest from entry) ------------------
        var entry = f.Rooms[0].Center;
        int goalRoom = 1;
        long far = -1;
        for (int i = 1; i < f.Rooms.Count; i++)
        {
            var p = f.Rooms[i].Center;
            long d = Math.Max(Math.Abs(p.X - entry.X), Math.Abs(p.Y - entry.Y));
            if (d > far) { far = d; goalRoom = i; }
        }

        // ---- deliberate loops -----------------------------------------
        int n = f.Rooms.Count;
        for (int attempt = 0; attempt < 40 && f.Loops < style.ExtraLoops(); attempt++)
        {
            int a = f.Loops == 0 && style == SiteStyle.Xandered ? goalRoom : rng.Range(0, n - 1);
            int b = rng.Range(0, n - 1);
            if (a == b || edges.Contains((a, b)) || edges.Contains((b, a))) continue;
            edges.Add((a, b));
            Connect(f, f.Rooms[a].Center, f.Rooms[b].Center, rng);
            f.Loops++;
        }

        // ---- stairs / boss --------------------------------------------
        var goal = f.Rooms[goalRoom].Center;
        f.StairsDown = goal;
        if (bossFloor)
        {
            f.Boss = goal;
            // Reward chests behind the boss.
            for (int i = 0; i < 2; i++)
            {
                var r = f.Rooms[goalRoom];
                var p = new Pt(
                    Math.Clamp(goal.X - 1 + i * 2, r.X, r.X + r.W - 1),
                    Math.Clamp(goal.Y - 1, r.Y, r.Y + r.H - 1));
                if (f.Tile(p.X, p.Y) == SiteTile.Floor && p != goal)
                {
                    f.Set(p.X, p.Y, SiteTile.Chest);
                    f.Chests.Add(p);
                }
            }
        }
        else
        {
            f.Set(goal.X, goal.Y, SiteTile.StairsDown);
            // Xandered floors get a second, half-collapsed way down, spread
            // away from both the entry and the main stair.
            if (style == SiteStyle.Xandered)
            {
                int second = -1;
                long bestScore = -1;
                for (int i = 1; i < n; i++)
                {
                    if (i == goalRoom) continue;
                    var p = f.Rooms[i].Center;
                    long dEntry = Math.Max(Math.Abs(p.X - entry.X), Math.Abs(p.Y - entry.Y));
                    long dGoal = Math.Max(Math.Abs(p.X - goal.X), Math.Abs(p.Y - goal.Y));
                    long score = Math.Min(dEntry, dGoal);
                    if (score > bestScore) { bestScore = score; second = i; }
                }
                if (second >= 0)
                {
                    var p = f.Rooms[second].Center;
                    f.StairsDown2 = p;
                    f.Set(p.X, p.Y, SiteTile.StairsDown);
                }
            }
        }
        f.Entry = entry;
        f.Set(entry.X, entry.Y, SiteTile.StairsUp);

        // ---- landmark: the largest room (orientation anchor) ----------
        int landmark = 0, area = 0;
        for (int i = 0; i < n; i++)
        {
            int a2 = f.Rooms[i].W * f.Rooms[i].H;
            if (a2 > area) { area = a2; landmark = i; }
        }
        f.Landmark = f.Rooms[landmark].Center;

        // Order matters: terrain (erosion, channels) reshapes the geometry,
        // so secrets place AFTER it — the detour metric must hold on what the
        // player actually walks, and the validator re-proves it there.
        // (RogueFiction placed secrets first and only spared their jambs;
        // its detour numbers were pre-terrain. Ours are final-geometry.)
        PlaceDoors(f, rng, style);
        ApplyTerrain(f, rng, recipe.Terrain);
        PlaceSecretDoors(f, rng, style.SecretDoors(), recipe.SecretDetourMin);
        if (allowChasm && recipe.Chasms && style == SiteStyle.Xandered)
            PlaceChasm(f, rng, goalRoom);

        PlaceSpawnsAndChests(f, rng, goalRoom, bossFloor);
        return f;
    }

    // ------------------------------------------------------------ carving

    private static void CarveRoom(SiteFloor f, Room r)
    {
        for (int y = r.Y; y < r.Y + r.H; y++)
            for (int x = r.X; x < r.X + r.W; x++)
                f.Set(x, y, SiteTile.Floor);
    }

    /// <summary>L-corridor between two points, elbow orientation random.</summary>
    private static void Connect(SiteFloor f, Pt a, Pt b, XorShift64 rng)
    {
        if (rng.Next(2) == 0)
        {
            CarveH(f, a.X, b.X, a.Y);
            CarveV(f, a.Y, b.Y, b.X);
        }
        else
        {
            CarveV(f, a.Y, b.Y, a.X);
            CarveH(f, a.X, b.X, b.Y);
        }
    }

    private static void CarveH(SiteFloor f, int x1, int x2, int y)
    {
        for (int x = Math.Min(x1, x2); x <= Math.Max(x1, x2); x++)
            f.Set(x, y, SiteTile.Floor);
    }

    private static void CarveV(SiteFloor f, int y1, int y2, int x)
    {
        for (int y = Math.Min(y1, y2); y <= Math.Max(y1, y2); y++)
            f.Set(x, y, SiteTile.Floor);
    }

    // ------------------------------------------------------------ doors

    /// <summary>Doors at clean doorways (wall jambs, floor running through).
    /// A few lock — but only where the way around exists, so a locked door
    /// is never the sole route.</summary>
    private static void PlaceDoors(SiteFloor f, XorShift64 rng, SiteStyle style)
    {
        var (num, den) = style.DoorChance();
        var spots = new List<(Pt door, Pt a, Pt b)>();
        foreach (var r in f.Rooms)
        {
            for (int x = r.X; x < r.X + r.W; x++)
                foreach (int wy in new[] { r.Y - 1, r.Y + r.H })
                    if (f.Tile(x, wy) == SiteTile.Floor
                        && f.Tile(x - 1, wy) == SiteTile.Wall && f.Tile(x + 1, wy) == SiteTile.Wall
                        && f.Tile(x, wy - 1) == SiteTile.Floor && f.Tile(x, wy + 1) == SiteTile.Floor)
                        spots.Add((new Pt(x, wy), new Pt(x, wy - 1), new Pt(x, wy + 1)));
            for (int y = r.Y; y < r.Y + r.H; y++)
                foreach (int wx in new[] { r.X - 1, r.X + r.W })
                    if (f.Tile(wx, y) == SiteTile.Floor
                        && f.Tile(wx, y - 1) == SiteTile.Wall && f.Tile(wx, y + 1) == SiteTile.Wall
                        && f.Tile(wx - 1, y) == SiteTile.Floor && f.Tile(wx + 1, y) == SiteTile.Floor)
                        spots.Add((new Pt(wx, y), new Pt(wx - 1, y), new Pt(wx + 1, y)));
        }
        foreach (var (door, a, b) in spots)
        {
            if (f.Tile(door.X, door.Y) != SiteTile.Floor) continue;
            if (rng.Next(den) >= num) continue;
            f.Set(door.X, door.Y, SiteTile.Door);
            // Lock only with a placement-time reachability proof: with the
            // door sealed, its two sides must still connect some other way.
            if (rng.Next(4) == 0 && GridSearch.WalkDistance(f, a, b) >= 0)
                f.Set(door.X, door.Y, SiteTile.LockedDoor);
        }
    }

    /// <summary>Thin walls with floor on opposite sides become hidden
    /// shortcuts — but only where the honest walk around is long enough for
    /// the secret to matter. Never load-bearing: both sides are already
    /// connected (or in an off-route pocket the validator re-proves).</summary>
    private static void PlaceSecretDoors(SiteFloor f, XorShift64 rng, int count, int detourMin)
    {
        var candidates = new List<Pt>();
        for (int y = 1; y < f.Height - 1; y++)
            for (int x = 1; x < f.Width - 1; x++)
            {
                if (f.Tile(x, y) != SiteTile.Wall) continue;
                bool across = f.Tile(x - 1, y) == SiteTile.Floor && f.Tile(x + 1, y) == SiteTile.Floor;
                bool down = f.Tile(x, y - 1) == SiteTile.Floor && f.Tile(x, y + 1) == SiteTile.Floor;
                if (across || down) candidates.Add(new Pt(x, y));
            }
        int placed = 0;
        while (placed < count && candidates.Count > 0)
        {
            int i = rng.Next(candidates.Count);
            var p = candidates[i];
            candidates.RemoveAt(i);
            // Orientation rule mirrored exactly by the validator.
            var (a, b) = f.Tile(p.X - 1, p.Y) == SiteTile.Floor && f.Tile(p.X + 1, p.Y) == SiteTile.Floor
                ? (new Pt(p.X - 1, p.Y), new Pt(p.X + 1, p.Y))
                : (new Pt(p.X, p.Y - 1), new Pt(p.X, p.Y + 1));
            int d = GridSearch.WalkDistance(f, a, b, doorsPassable: true);
            if (d < 0 || d >= detourMin)
            {
                f.Set(p.X, p.Y, SiteTile.SecretDoor);
                placed++;
            }
        }
    }

    // ------------------------------------------------------------ terrain

    private static void ApplyTerrain(SiteFloor f, XorShift64 rng, TerrainPass terrain)
    {
        switch (terrain)
        {
            case TerrainPass.None:
                break;
            case TerrainPass.Rubble:
                foreach (var r in f.Rooms)
                {
                    if (rng.Next(2) != 0) continue;
                    var center = r.Center;
                    int heaps = rng.Range(1, 2);
                    for (int i = 0; i < heaps; i++)
                    {
                        int x = rng.Range(r.X + 1, r.X + r.W - 2);
                        int y = rng.Range(r.Y + 1, r.Y + r.H - 2);
                        if (new Pt(x, y) != center && f.Tile(x, y) == SiteTile.Floor)
                            f.Set(x, y, SiteTile.Rubble);
                    }
                }
                break;
            case TerrainPass.Water:
            case TerrainPass.Slag:
                var fill = terrain == TerrainPass.Water ? SiteTile.Water : SiteTile.Slag;
                // Channels between far-apart rooms, cutting through the walls:
                // continuous risky lanes that bypass the honest corridors.
                for (int c = 0; c < 2; c++)
                {
                    int a = rng.Next(f.Rooms.Count);
                    var ac = f.Rooms[a].Center;
                    int b = 0;
                    long far2 = -1;
                    for (int i = 0; i < f.Rooms.Count; i++)
                    {
                        var p = f.Rooms[i].Center;
                        long d = Math.Max(Math.Abs(p.X - ac.X), Math.Abs(p.Y - ac.Y));
                        if (d > far2) { far2 = d; b = i; }
                    }
                    CarveChannel(f, ac, f.Rooms[b].Center, fill, rng);
                }
                if (fill == SiteTile.Slag)
                    foreach (var r in f.Rooms)
                    {
                        if (rng.Next(3) != 0) continue;
                        var center = r.Center;
                        int x = rng.Range(r.X + 1, r.X + r.W - 2);
                        int y = rng.Range(r.Y + 1, r.Y + r.H - 2);
                        if (new Pt(x, y) != center && f.Tile(x, y) == SiteTile.Floor)
                            f.Set(x, y, SiteTile.Slag);
                    }
                break;
            case TerrainPass.Caves:
                ErodeCaves(f, rng);
                break;
        }
    }

    /// <summary>An L-path where only wall tiles take the fill; carved floor
    /// stays floor, so the lane is continuous and connectivity is monotone —
    /// no revalidation needed.</summary>
    private static void CarveChannel(SiteFloor f, Pt a, Pt b, SiteTile fill, XorShift64 rng)
    {
        var (ex, ey) = rng.Next(2) == 0 ? (b.X, a.Y) : (a.X, b.Y);
        FillH(f, a.X, ex, a.Y, fill);
        FillV(f, a.Y, ey, ex, fill);
        FillH(f, ex, b.X, ey, fill);
        FillV(f, ey, b.Y, b.X, fill);
    }

    private static void FillH(SiteFloor f, int x1, int x2, int y, SiteTile fill)
    {
        for (int x = Math.Min(x1, x2); x <= Math.Max(x1, x2); x++)
            if (f.Tile(x, y) == SiteTile.Wall)
                f.Set(x, y, fill);
    }

    private static void FillV(SiteFloor f, int y1, int y2, int x, SiteTile fill)
    {
        for (int y = Math.Min(y1, y2); y <= Math.Max(y1, y2); y++)
            if (f.Tile(x, y) == SiteTile.Wall)
                f.Set(x, y, fill);
    }

    /// <summary>Two passes of cellular erosion: walls flanked by open floor
    /// crumble and the rectangles soften into caverns. Only adds floor, so
    /// connectivity can't break; door and secret-door jambs are spared.</summary>
    private static void ErodeCaves(SiteFloor f, XorShift64 rng)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            var toOpen = new List<Pt>();
            for (int y = 1; y < f.Height - 1; y++)
                for (int x = 1; x < f.Width - 1; x++)
                {
                    if (f.Tile(x, y) != SiteTile.Wall) continue;
                    int floors = 0;
                    if (f.Tile(x + 1, y) == SiteTile.Floor) floors++;
                    if (f.Tile(x - 1, y) == SiteTile.Floor) floors++;
                    if (f.Tile(x, y + 1) == SiteTile.Floor) floors++;
                    if (f.Tile(x, y - 1) == SiteTile.Floor) floors++;
                    bool nearDoor = false;
                    for (int dy = -1; dy <= 1 && !nearDoor; dy++)
                        for (int dx = -1; dx <= 1 && !nearDoor; dx++)
                            if (f.Tile(x + dx, y + dy) is SiteTile.Door or SiteTile.LockedDoor or SiteTile.SecretDoor)
                                nearDoor = true;
                    if (floors >= 2 && !nearDoor && rng.Next(3) == 0)
                        toOpen.Add(new Pt(x, y));
                }
            foreach (var p in toOpen)
                f.Set(p.X, p.Y, SiteTile.Floor);
        }
    }

    /// <summary>One chasm tucked into a side room: a deliberate two-depth
    /// drop for whoever trusts themself to it (level-skip link, invariant 3).</summary>
    private static void PlaceChasm(SiteFloor f, XorShift64 rng, int goalRoom)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            int i = rng.Range(1, f.Rooms.Count - 1);
            if (i == goalRoom) continue;
            var r = f.Rooms[i];
            var center = r.Center;
            int x = rng.Range(r.X + 1, r.X + r.W - 2);
            int y = rng.Range(r.Y + 1, r.Y + r.H - 2);
            if (new Pt(x, y) == center || f.Tile(x, y) != SiteTile.Floor) continue;
            f.Set(x, y, SiteTile.Chasm);
            // A lip wide enough to read as a gap, not a pothole.
            if (new Pt(x + 1, y) != center && x + 1 < r.X + r.W - 1 && f.Tile(x + 1, y) == SiteTile.Floor)
                f.Set(x + 1, y, SiteTile.Chasm);
            return;
        }
    }

    // ------------------------------------------------------------ population

    private static void PlaceSpawnsAndChests(SiteFloor f, XorShift64 rng, int goalRoom, bool bossFloor)
    {
        for (int i = 1; i < f.Rooms.Count; i++)
        {
            if (bossFloor && i == goalRoom) continue; // boss handles its own room
            var r = f.Rooms[i];
            int spawns = Math.Min(2, (r.W * r.H) / 16);
            for (int s = 0; s < spawns; s++)
            {
                int x = rng.Range(r.X, r.X + r.W - 1);
                int y = rng.Range(r.Y, r.Y + r.H - 1);
                if (f.Tile(x, y).IsWalkable() && f.Tile(x, y) != SiteTile.StairsDown
                    && f.Tile(x, y) != SiteTile.StairsUp)
                    f.Spawns.Add(new Pt(x, y));
            }
        }
        // One chest in a side room far from the entry (plus boss chests).
        if (!bossFloor && f.Rooms.Count > 3)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                int i = rng.Range(1, f.Rooms.Count - 1);
                if (i == goalRoom) continue;
                var r = f.Rooms[i];
                int x = rng.Range(r.X + 1, r.X + r.W - 2);
                int y = rng.Range(r.Y + 1, r.Y + r.H - 2);
                if (f.Tile(x, y) == SiteTile.Floor)
                {
                    f.Set(x, y, SiteTile.Chest);
                    f.Chests.Add(new Pt(x, y));
                    break;
                }
            }
        }
    }
}
