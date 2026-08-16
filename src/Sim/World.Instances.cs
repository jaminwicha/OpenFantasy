using OpenFantasy.Sim.SiteGen;

namespace OpenFantasy.Sim;

/// <summary>
/// M7 instanced adventure sites: multi-floor Jaquayed delves generated pure
/// from (WorldSeed, siteId, floor) and hosted alongside the overworld. Each
/// floor is a map space (MapId > 0) created lazily on first entry; layouts
/// mutate only through player action (pried locks, revealed secrets), which
/// is input-driven and therefore deterministic.
/// </summary>
public sealed partial class World
{
    public sealed class MapInstance
    {
        public required int MapId;
        public required string SiteId;
        public required int Floor;
        /// <summary>Mutable copy of the generated layout (doors pried, secrets found).</summary>
        public required SiteFloor Layout;
        /// <summary>Overworld tile this site exits to (per entrance index).</summary>
        public required List<(int tx, int ty)> Entrances;
        /// <summary>Respawn queue: (tick due, spawn point index).</summary>
        public readonly List<(int tick, int spawnIndex)> PendingRespawns = new();
        public bool BossAlivePending; // boss respawn scheduled flag
        public int BossRespawnTick = -1;
        /// <summary>Set when layout tiles changed since last broadcast (secrets/locks).</summary>
        public bool Dirty;
    }

    private readonly Dictionary<int, MapInstance> _instances = new();
    private readonly Dictionary<(string siteId, int floor), int> _instanceIds = new();
    private int _nextMapId = 1;

    public MapInstance? GetInstance(int mapId) => _instances.GetValueOrDefault(mapId);

    /// <summary>Ticks between trash respawns / boss respawns in instances.</summary>
    private const int InstanceRespawnTicks = 1200;   // 60 s
    private const int BossRespawnTicks = 6000;       // 5 min

    // ------------------------------------------------------------- entrances

    /// <summary>Overworld entrances per site: the four dungeon regions use
    /// their region rect (enter from anywhere inside); the sewers use grate
    /// tiles resolved around the Brasshollow plaza.</summary>
    private List<(int tx, int ty)> SiteEntrances(string siteId)
    {
        if (siteId == "sewer_brasshollow")
            return SewerGrates();
        var region = Data.Map.Region(siteId)!;
        return new List<(int, int)> { (region.X + region.W / 2, region.Y + region.H / 2) };
    }

    /// <summary>Three grate tiles spread across Brasshollow: gate, plaza, far
    /// corner — each a distinct way down into the same sewer floor.</summary>
    public List<(int tx, int ty)> SewerGrates()
    {
        var r = Data.Map.Region("brasshollow")!;
        var candidates = new (int tx, int ty)[]
        {
            (r.X + 3, r.Y + 3),
            (r.X + r.W / 2 + 4, r.Y + r.H / 2),
            (r.X + r.W - 4, r.Y + r.H - 4),
        };
        var grates = new List<(int, int)>();
        foreach (var (cx, cy) in candidates)
        {
            // Nudge to the nearest walkable tile (deterministic ring scan).
            for (int radius = 0; radius < 6; radius++)
            {
                bool found = false;
                for (int dy = -radius; dy <= radius && !found; dy++)
                    for (int dx = -radius; dx <= radius && !found; dx++)
                        if (Data.Map.IsWalkableTile(cx + dx, cy + dy))
                        {
                            grates.Add((cx + dx, cy + dy));
                            found = true;
                        }
                if (found) break;
            }
        }
        return grates;
    }

    // ------------------------------------------------------------- lifecycle

    private MapInstance GetOrCreateInstance(string siteId, int floor)
    {
        if (_instanceIds.TryGetValue((siteId, floor), out int existing))
            return _instances[existing];

        var recipe = SiteRegistry.Sites[siteId];
        var layout = SiteGenerator.GenerateFloor(Seed, siteId, floor, recipe);
        var inst = new MapInstance
        {
            MapId = _nextMapId++,
            SiteId = siteId,
            Floor = floor,
            Layout = layout,
            Entrances = SiteEntrances(siteId),
        };
        _instances[inst.MapId] = inst;
        _instanceIds[(siteId, floor)] = inst.MapId;

        // Populate: trash at every spawn point, boss on the final floor.
        for (int i = 0; i < layout.Spawns.Count; i++)
            SpawnInstanceMonster(inst, i);
        if (layout.Boss is { } bossAt)
            SpawnInstanceBoss(inst, bossAt);
        return inst;
    }

    private void SpawnInstanceMonster(MapInstance inst, int spawnIndex)
    {
        var recipe = SiteRegistry.Sites[inst.SiteId];
        // Deeper floors pull from deeper in the spawn list (difficulty band).
        ulong pick = StableId.Combine(StableId.Domain(Seed, inst.SiteId), inst.Floor, spawnIndex);
        int band = Math.Min(recipe.SpawnMonsters.Length - 1,
            inst.Floor * recipe.SpawnMonsters.Length / Math.Max(1, recipe.Floors));
        int idx = (int)(pick % (ulong)(band + 1));
        var def = Data.Monsters[recipe.SpawnMonsters[Math.Max(band, idx)]];
        var p = inst.Layout.Spawns[spawnIndex];
        SpawnMonsterAt(def, inst.MapId, p.X, p.Y, spawnerId: spawnIndex);
    }

    private void SpawnInstanceBoss(MapInstance inst, Pt at)
    {
        var recipe = SiteRegistry.Sites[inst.SiteId];
        SpawnMonsterAt(Data.Monsters[recipe.BossMonster], inst.MapId, at.X, at.Y, spawnerId: -99);
    }

    private void SpawnMonsterAt(MonsterDef m, int mapId, int tx, int ty, int spawnerId)
    {
        int u = SimConstants.UnitsPerTile;
        var e = new Entity
        {
            Id = _nextEntityId++,
            Kind = EntityKind.Monster,
            Name = m.Name,
            ArchetypeId = m.Id,
            Level = m.Level,
            MaxHp = m.Stats.Hp,
            Hp = m.Stats.Hp,
            MaxMp = m.Stats.Mp,
            Mp = m.Stats.Mp,
            Attack = m.Stats.Attack,
            Magic = m.Stats.Magic,
            Defense = m.Stats.Defense,
            Speed = m.Stats.Speed,
            MoveSpeed = m.MoveSpeed,
            SpawnerId = spawnerId,
            MapId = mapId,
            X = tx * u + u / 2,
            Y = ty * u + u / 2,
        };
        e.HomeX = e.X;
        e.HomeY = e.Y;
        _entities[e.Id] = e;
        _entityOrder.Add(e.Id);
    }

    /// <summary>Called from Step(): due instance respawns.</summary>
    private void TickInstances()
    {
        foreach (var inst in _instances.Values)
        {
            for (int i = inst.PendingRespawns.Count - 1; i >= 0; i--)
            {
                if (inst.PendingRespawns[i].tick > Tick) continue;
                int spawnIndex = inst.PendingRespawns[i].spawnIndex;
                inst.PendingRespawns.RemoveAt(i);
                SpawnInstanceMonster(inst, spawnIndex);
            }
            if (inst.BossRespawnTick >= 0 && Tick >= inst.BossRespawnTick && inst.Layout.Boss is { } at)
            {
                inst.BossRespawnTick = -1;
                SpawnInstanceBoss(inst, at);
            }
        }
    }

    /// <summary>Instance monster died: schedule its respawn.</summary>
    private void OnInstanceMonsterDeath(Entity victim)
    {
        if (GetInstance(victim.MapId) is not { } inst) return;
        if (victim.SpawnerId == -99)
            inst.BossRespawnTick = Tick + BossRespawnTicks;
        else if (victim.SpawnerId >= 0)
            inst.PendingRespawns.Add((Tick + InstanceRespawnTicks, victim.SpawnerId));
    }

    // ------------------------------------------------------------- movement glue

    /// <summary>Instance walkability at world-unit coordinates.</summary>
    private bool InstanceWalkableUnits(MapInstance inst, int ux, int uy)
    {
        if (ux < 0 || uy < 0) return false;
        return inst.Layout.Tile(ux / SimConstants.UnitsPerTile, uy / SimConstants.UnitsPerTile).IsWalkable()
            || inst.Layout.Tile(ux / SimConstants.UnitsPerTile, uy / SimConstants.UnitsPerTile) == SiteTile.Door;
    }

    // ------------------------------------------------------------- site interaction

    /// <summary>
    /// The "use surroundings" verb (E with no NPC/node in range): overworld →
    /// enter a site; in an instance → stairs, chasm drop, chest, pried lock,
    /// or secret search. Returns user-facing text ("" = nothing here).
    /// </summary>
    public string SiteInteract(Entity player)
    {
        if (!player.Alive) return "";
        if (player.MapId == 0) return TryEnterSite(player);

        if (GetInstance(player.MapId) is not { } inst) return "";
        int tx = player.X / SimConstants.UnitsPerTile;
        int ty = player.Y / SimConstants.UnitsPerTile;
        var tile = inst.Layout.Tile(tx, ty);

        switch (tile)
        {
            case SiteTile.StairsDown:
                return Descend(player, inst, viaSecondStair:
                    inst.Layout.StairsDown2 is { } s2 && s2.X == tx && s2.Y == ty);
            case SiteTile.StairsUp:
                return Ascend(player, inst);
            case SiteTile.Chasm:
                return DropChasm(player, inst);
        }

        // Adjacent-tile interactions: chest, locked door, suspicious wall.
        foreach (var (dx, dy) in new[] { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            int ax = tx + dx, ay = ty + dy;
            switch (inst.Layout.Tile(ax, ay))
            {
                case SiteTile.Chest:
                    return OpenChest(player, inst, ax, ay);
                case SiteTile.LockedDoor:
                    inst.Layout.Set(ax, ay, SiteTile.Floor);
                    inst.Dirty = true;
                    return "You force the lock open.";
                case SiteTile.SecretDoor:
                    inst.Layout.Set(ax, ay, SiteTile.Floor);
                    inst.Dirty = true;
                    Events.Add(new SimEvent(SimEventKind.QuestProgress, player.Id, player.Id, 0, ""));
                    return "You find a seam in the stone — a hidden way opens!";
            }
        }
        return "";
    }

    private string TryEnterSite(Entity player)
    {
        if (player.Vehicle != VehicleKind.None) return "Not from the deck of a vehicle.";
        int tx = player.X / SimConstants.UnitsPerTile;
        int ty = player.Y / SimConstants.UnitsPerTile;

        foreach (var (siteId, _) in SiteRegistry.Sites.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (siteId == "sewer_brasshollow")
            {
                var grates = SewerGrates();
                for (int i = 0; i < grates.Count; i++)
                {
                    var (gx, gy) = grates[i];
                    if (Math.Abs(tx - gx) <= 1 && Math.Abs(ty - gy) <= 1)
                        return EnterSite(player, siteId, entranceIndex: i);
                }
            }
            else if (Data.Map.Region(siteId) is { } region && region.Rect.Contains(tx, ty))
            {
                return EnterSite(player, siteId, entranceIndex: 0);
            }
        }
        return "";
    }

    private string EnterSite(Entity player, string siteId, int entranceIndex)
    {
        var inst = GetOrCreateInstance(siteId, 0);
        // Distinct entrances arrive at distinct floor-0 points (multi-entrance
        // topology): Entry, second stair room, landmark.
        var arrivals = new List<Pt> { inst.Layout.Entry };
        if (inst.Layout.StairsDown2 is { } s2) arrivals.Add(s2);
        arrivals.Add(inst.Layout.Landmark);
        var at = arrivals[Math.Min(entranceIndex, arrivals.Count - 1)];

        PlaceInInstance(player, inst, at);
        return $"You descend into {SiteDisplayName(siteId)}.";
    }

    private string Descend(Entity player, MapInstance inst, bool viaSecondStair)
    {
        if (inst.Floor + 1 >= SiteRegistry.Sites[inst.SiteId].Floors) return "The way down is sealed.";
        var below = GetOrCreateInstance(inst.SiteId, inst.Floor + 1);
        // Arrival semantics: the stair you took decides where you stand —
        // the collapsed second stair emerges by the lower floor's own
        // second stair when it has one.
        var at = viaSecondStair && below.Layout.StairsDown2 is { } s2 ? s2 : below.Layout.Entry;
        PlaceInInstance(player, below, at);
        return $"{SiteDisplayName(inst.SiteId)} — floor {below.Floor + 1}.";
    }

    private string Ascend(Entity player, MapInstance inst)
    {
        if (inst.Floor == 0)
        {
            var (tx, ty) = inst.Entrances[0];
            PlaceInOverworld(player, tx, ty);
            return "You climb back into daylight.";
        }
        var above = GetOrCreateInstance(inst.SiteId, inst.Floor - 1);
        PlaceInInstance(player, above, above.Layout.StairsDown);
        return $"{SiteDisplayName(inst.SiteId)} — floor {above.Floor + 1}.";
    }

    private string DropChasm(Entity player, MapInstance inst)
    {
        var recipe = SiteRegistry.Sites[inst.SiteId];
        int target = Math.Min(inst.Floor + 2, recipe.Floors - 1);
        var below = GetOrCreateInstance(inst.SiteId, target);
        // A collapse dumps you somewhere unplanned: the landmark room.
        PlaceInInstance(player, below, below.Layout.Landmark);
        int fall = Math.Max(1, player.MaxHp / 4);
        player.Hp = Math.Max(1, player.Hp - fall);
        player.LastDamagedTick = Tick;
        Events.Add(new SimEvent(SimEventKind.Damage, player.Id, player.Id, fall));
        return $"You drop into the dark… ({SiteDisplayName(inst.SiteId)} — floor {below.Floor + 1})";
    }

    private string OpenChest(Entity player, MapInstance inst, int tx, int ty)
    {
        int chestIndex = inst.Layout.Chests.FindIndex(c => c.X == tx && c.Y == ty);
        if (chestIndex < 0) return "";
        string chestId = $"{inst.SiteId}:{inst.Floor}:{chestIndex}";
        if (!player.OpenedChests.Add(chestId)) return "You already emptied this chest.";

        // D9: contents are a pure function of (seed, chestId) — fixed at world
        // creation, independent of who opens it or when.
        ulong roll = StableId.Combine(StableId.Domain(Seed, "chest"), StableId.Domain(0, chestId));
        int gil = 25 + (int)(roll % (ulong)(35 * (inst.Floor + 1)));
        player.Gil += gil;

        int lo = 15 * (inst.Floor + 1), hi = 120 * (inst.Floor + 2);
        var candidates = Data.Items.Values
            .Where(i => i.Price >= lo && i.Price <= hi && i.Kind != ItemKind.Vehicle)
            .Select(i => i.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        string lootText = $"{gil} gil";
        if (candidates.Count > 0)
        {
            string itemId = candidates[(int)(StableId.Mix(roll) % (ulong)candidates.Count)];
            if (AddItem(player, itemId))
            {
                Events.Add(new SimEvent(SimEventKind.Loot, player.Id, player.Id, 1, itemId));
                lootText += $" and {Data.Items[itemId].Name}";
            }
        }
        return $"The chest holds {lootText}.";
    }

    private void PlaceInInstance(Entity player, MapInstance inst, Pt tile)
    {
        int u = SimConstants.UnitsPerTile;
        player.MapId = inst.MapId;
        player.X = tile.X * u + u / 2;
        player.Y = tile.Y * u + u / 2;
        Events.Add(new SimEvent(SimEventKind.MapChange, player.Id, player.Id, inst.MapId));
    }

    private void PlaceInOverworld(Entity player, int tx, int ty)
    {
        int u = SimConstants.UnitsPerTile;
        player.MapId = 0;
        player.X = tx * u + u / 2;
        player.Y = ty * u + u / 2;
        Events.Add(new SimEvent(SimEventKind.MapChange, player.Id, player.Id, 0));
    }

    public static string SiteDisplayName(string siteId) => siteId switch
    {
        "hollow_mine" => "the Hollow Mine",
        "sunken_crypt" => "the Sunken Crypt",
        "emberdeep" => "Emberdeep",
        "storm_spire" => "the Storm Spire",
        "sewer_brasshollow" => "the Brasshollow Sewers",
        _ => siteId,
    };

    /// <summary>Overworld exit position for saves: players logging out inside
    /// a delve reappear at its surface entrance.</summary>
    public (int x, int y) SaveExitUnits(Entity e)
    {
        if (e.MapId == 0 || GetInstance(e.MapId) is not { } inst)
            return (e.X, e.Y);
        int u = SimConstants.UnitsPerTile;
        var (tx, ty) = inst.Entrances[0];
        return (tx * u + u / 2, ty * u + u / 2);
    }
}
