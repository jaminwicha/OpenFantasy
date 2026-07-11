namespace OpenFantasy.Sim;

/// <summary>
/// The authoritative deterministic simulation. Pure C#: no Godot types, no
/// wall-clock, no floats in gameplay state. The server steps it in whole
/// ticks; identical (seed, join order, inputs) always yields identical state.
/// M2 systems (items, quests, parties, NPCs) live in World.Systems.cs.
/// </summary>
public sealed partial class World
{
    public GameData Data { get; }
    public int Tick { get; private set; }
    public ulong Seed { get; }

    /// <summary>Presentation events produced by the most recent tick.</summary>
    public List<SimEvent> Events { get; } = new();

    private readonly XorShift64 _rng;
    private readonly Dictionary<int, Entity> _entities = new();
    private readonly List<int> _entityOrder = new(); // stable iteration order
    private readonly List<SpawnerState> _spawners = new();
    private int _nextEntityId = 1;

    private sealed class SpawnerState
    {
        public required SpawnerDef Def;
        public required MonsterDef Monster;
        public readonly List<int> PendingRespawnTicks = new();
    }

    public World(GameData data, ulong seed)
    {
        Data = data;
        Seed = seed;
        _rng = new XorShift64(seed);

        foreach (var def in data.Map.Spawners)
        {
            var state = new SpawnerState { Def = def, Monster = data.Monsters[def.Monster] };
            _spawners.Add(state);
            for (int i = 0; i < def.Count; i++)
                SpawnMonster(state);
        }

        SpawnNpcs();
        InitNodes();
    }

    public Entity? GetEntity(int id) => _entities.GetValueOrDefault(id);

    public IEnumerable<Entity> EntitiesInOrder()
    {
        foreach (int id in _entityOrder)
            yield return _entities[id];
    }

    public int EntityCount => _entityOrder.Count;

    // ------------------------------------------------------------------ players

    public Entity AddPlayer(string name, string classId, CharacterSave? save = null, Appearance? appearance = null)
    {
        var cls = Data.Classes[classId];
        var e = new Entity
        {
            Id = _nextEntityId++,
            Kind = EntityKind.Player,
            Name = name,
            ArchetypeId = classId,
            MoveSpeed = cls.MoveSpeed,
            HasAppearance = true,
            Appearance = (appearance ?? Appearance.Default).Sanitized(Data.Appearance),
        };

        e.Level = Math.Clamp(save?.Level ?? 1, 1, SimConstants.MaxLevel);
        e.Xp = Math.Max(0, save?.Xp ?? 0);
        e.Gil = Math.Max(0, save?.Gil ?? 0);
        RestoreM2State(e, save);
        RecomputeStats(e);
        e.Hp = e.MaxHp;
        e.Mp = e.MaxMp;

        (e.X, e.Y) = SpawnPointUnits();
        if (save is { } s)
        {
            var savedVehicle = (VehicleKind)Math.Clamp(s.Vehicle, 0, (int)VehicleKind.Airship);
            if (savedVehicle != VehicleKind.None && UnitsAllow(s.X, s.Y, savedVehicle))
            {
                e.Vehicle = savedVehicle;
                e.X = s.X;
                e.Y = s.Y;
            }
            else if (Data.Map.IsWalkableUnits(s.X, s.Y))
            {
                e.X = s.X;
                e.Y = s.Y;
            }
        }

        _entities[e.Id] = e;
        _entityOrder.Add(e.Id);
        return e;
    }

    public void RemovePlayer(int id)
    {
        if (_entities.TryGetValue(id, out var e))
        {
            if (e.PartyId >= 0) PartyLeave(e);
            _entities.Remove(id);
            _entityOrder.Remove(id);
        }
    }

    public void SetPlayerInput(int id, InputCommand input)
    {
        if (_entities.TryGetValue(id, out var e) && e.IsPlayer && input.Seq > e.LastInputSeq)
        {
            e.Input = input;
            e.LastInputSeq = input.Seq;
        }
    }

    public CharacterSave SnapshotSave(Entity e) => new()
    {
        ClassId = e.ArchetypeId,
        Level = e.Level,
        Xp = e.Xp,
        Gil = e.Gil,
        X = e.X,
        Y = e.Y,
        Inventory = e.Inventory.Select(s => new ItemStack { ItemId = s.ItemId, Count = s.Count }).ToList(),
        WeaponId = e.WeaponId,
        ArmorId = e.ArmorId,
        Quests = e.QuestLog.ToDictionary(
            kv => kv.Key,
            kv => new QuestProgress { State = kv.Value.State, Kills = kv.Value.Kills }),
        Appearance = e.Appearance.ToBytes().Select(b => (int)b).ToList(),
        Vehicle = (int)e.Vehicle,
    };

    /// <summary>Race identity: (name, maxHp, maxMp, attack, magic, defense, speed) flat modifiers.</summary>
    public static readonly (string Name, int Hp, int Mp, int Atk, int Mag, int Def, int Spd)[] RaceMods =
    {
        ("Human", 0, 0, 0, 0, 0, 0),
        ("Elf", -8, 4, 0, 3, 0, 2),
        ("Dwarf", 12, 0, 1, 0, 2, -2),
        ("Gnome", -4, 8, -2, 1, 0, 2),
    };

    /// <summary>Effective stats: class base + level growth + race + equipment bonuses.</summary>
    public void RecomputeStats(Entity e)
    {
        var cls = Data.Classes[e.ArchetypeId];
        int lvls = e.Level - 1;
        e.MaxHp = cls.BaseStats.Hp + cls.Growth.Hp * lvls;
        e.MaxMp = cls.BaseStats.Mp + cls.Growth.Mp * lvls;
        e.Attack = cls.BaseStats.Attack + cls.Growth.Attack * lvls;
        e.Magic = cls.BaseStats.Magic + cls.Growth.Magic * lvls;
        e.Defense = cls.BaseStats.Defense + cls.Growth.Defense * lvls;
        e.Speed = cls.BaseStats.Speed + cls.Growth.Speed * lvls;

        var race = RaceMods[Math.Min(e.Appearance.Race, (byte)(RaceMods.Length - 1))];
        e.MaxHp = Math.Max(1, e.MaxHp + race.Hp);
        e.MaxMp = Math.Max(0, e.MaxMp + race.Mp);
        e.Attack = Math.Max(1, e.Attack + race.Atk);
        e.Magic = Math.Max(0, e.Magic + race.Mag);
        e.Defense = Math.Max(0, e.Defense + race.Def);
        e.Speed = Math.Max(1, e.Speed + race.Spd);

        foreach (string equipped in new[] { e.WeaponId, e.ArmorId })
        {
            if (equipped.Length == 0 || !Data.Items.TryGetValue(equipped, out var item)) continue;
            e.Attack += item.AttackBonus;
            e.Magic += item.MagicBonus;
            e.Defense += item.DefenseBonus;
            e.MaxHp += item.HpBonus;
        }
        e.Hp = Math.Min(e.Hp, e.MaxHp);
        e.Mp = Math.Min(e.Mp, e.MaxMp);
    }

    private (int x, int y) SpawnPointUnits()
    {
        int u = SimConstants.UnitsPerTile;
        return (Data.Map.PlayerSpawnX * u + u / 2, Data.Map.PlayerSpawnY * u + u / 2);
    }

    // ------------------------------------------------------------------ monsters

    private void SpawnMonster(SpawnerState spawner)
    {
        var def = spawner.Def;
        var m = spawner.Monster;
        int u = SimConstants.UnitsPerTile;

        // Pick a walkable, non-safe tile in the region (bounded retries, then scan).
        int tx = -1, ty = -1;
        for (int attempt = 0; attempt < 32; attempt++)
        {
            int cx = def.Region.X + _rng.Next(def.Region.W);
            int cy = def.Region.Y + _rng.Next(def.Region.H);
            if (Data.Map.IsWalkableTile(cx, cy) && !Data.Map.IsSafeZoneTile(cx, cy))
            {
                tx = cx;
                ty = cy;
                break;
            }
        }
        if (tx < 0)
        {
            for (int cy = def.Region.Y; cy < def.Region.Y + def.Region.H && tx < 0; cy++)
                for (int cx = def.Region.X; cx < def.Region.X + def.Region.W; cx++)
                    if (Data.Map.IsWalkableTile(cx, cy) && !Data.Map.IsSafeZoneTile(cx, cy))
                    {
                        tx = cx;
                        ty = cy;
                        break;
                    }
        }
        if (tx < 0) return; // validator guarantees walkable tiles; safe-zone overlap could still exclude all

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
            SpawnerId = def.Id,
            X = tx * u + u / 2,
            Y = ty * u + u / 2,
        };
        e.HomeX = e.X;
        e.HomeY = e.Y;
        _entities[e.Id] = e;
        _entityOrder.Add(e.Id);
    }

    // ------------------------------------------------------------------ tick

    public void Step()
    {
        Tick++;
        Events.Clear();

        // Spawner respawns.
        foreach (var spawner in _spawners)
        {
            for (int i = spawner.PendingRespawnTicks.Count - 1; i >= 0; i--)
            {
                if (spawner.PendingRespawnTicks[i] <= Tick)
                {
                    spawner.PendingRespawnTicks.RemoveAt(i);
                    SpawnMonster(spawner);
                }
            }
        }
        TickNodeSpawners();

        // Entity updates over a stable copy (spawn/despawn mutates the order list).
        var order = new List<int>(_entityOrder);
        var toRemove = new List<int>();
        foreach (int id in order)
        {
            if (!_entities.TryGetValue(id, out var e)) continue;

            if (!e.Alive)
            {
                if (e.IsPlayer && Tick >= e.RespawnAtTick)
                    RespawnPlayer(e);
                else if ((e.IsMonster || e.IsNode) && Tick >= e.RespawnAtTick)
                    toRemove.Add(id); // corpse cleanup; spawner already has the respawn scheduled
                continue;
            }

            if (e.IsNode) continue;
            if (e.IsNpc)
            {
                TickNpc(e);
                continue;
            }

            // ATB fill.
            e.Atb = Math.Min(SimConstants.AtbMax, e.Atb + 6 + e.Speed / 2);

            if (e.IsPlayer) TickPlayer(e);
            else TickMonster(e);
        }

        foreach (int id in toRemove)
        {
            _entities.Remove(id);
            _entityOrder.Remove(id);
        }
    }

    private void TickPlayer(Entity e)
    {
        var input = e.Input;

        // Movement.
        MoveEntity(e, input.MoveX, input.MoveY, allowSafeZone: true);

        // Explore-quest region checks (cheap, staggered by entity id).
        if ((Tick + e.Id) % 10 == 0)
            CheckExploreQuests(e);

        // Regen: MP trickle always; HP when out of combat ≥ 5 s.
        if (Tick % SimConstants.TicksPerSecond == 0)
        {
            if (e.Mp < e.MaxMp) e.Mp++;
            if (e.Hp < e.MaxHp && Tick - e.LastDamagedTick >= 5 * SimConstants.TicksPerSecond)
                e.Hp = Math.Min(e.MaxHp, e.Hp + 1 + e.MaxHp / 50);
        }

        // Ability (consume the request so it fires once). Not from the deck of a vehicle.
        if (input.AbilitySlot >= 0)
        {
            if (e.Vehicle == VehicleKind.None)
                TryUsePlayerAbility(e, input.AbilitySlot, input.TargetId);
            e.Input.AbilitySlot = -1;
        }
    }

    private void TryUsePlayerAbility(Entity e, int slot, int targetId)
    {
        var cls = Data.Classes[e.ArchetypeId];
        if (slot >= cls.Abilities.Count) return;
        var ability = Data.Abilities[cls.Abilities[slot]];

        if (e.Level < ability.UnlockLevel) return;
        if (e.Atb < ability.AtbCost) return;
        if (e.Mp < ability.MpCost) return;

        if (ability.Kind == AbilityKind.Heal)
        {
            e.Atb -= ability.AtbCost;
            e.Mp -= ability.MpCost;
            int amount = Vary(Math.Max(1, e.Magic * ability.Power / 10));
            e.Hp = Math.Min(e.MaxHp, e.Hp + amount);
            Events.Add(new SimEvent(SimEventKind.Heal, e.Id, e.Id, amount, ability.Id));
            Events.Add(new SimEvent(SimEventKind.AbilityUsed, e.Id, e.Id, slot, ability.Id));
            return;
        }

        // Damage abilities need a valid monster target in range, outside safe zones.
        var target = GetEntity(targetId);
        if (target is null || !target.Alive || !target.IsMonster) return;
        long range = ability.Range + 8; // small grace for latency
        if (e.DistanceSquaredTo(target) > range * range) return;
        if (Data.Map.IsSafeZoneUnits(e.X, e.Y) || Data.Map.IsSafeZoneUnits(target.X, target.Y)) return;

        e.Atb -= ability.AtbCost;
        e.Mp -= ability.MpCost;
        FaceToward(e, target.X, target.Y);
        Events.Add(new SimEvent(SimEventKind.AbilityUsed, e.Id, target.Id, slot, ability.Id));

        DealDamage(e, target, ability.Power, magical: ability.UsesMagic);

        if (ability.AoeRadius > 0)
        {
            long r2 = (long)ability.AoeRadius * ability.AoeRadius;
            foreach (var other in EntitiesInOrder())
            {
                if (other.Id == target.Id || !other.IsMonster || !other.Alive) continue;
                long dx = other.X - target.X, dy = other.Y - target.Y;
                if (dx * dx + dy * dy <= r2)
                    DealDamage(e, other, ability.Power, magical: ability.UsesMagic);
            }
        }
    }

    private void TickMonster(Entity e)
    {
        var def = Data.Monsters[e.ArchetypeId];

        switch (e.Ai)
        {
            case AiState.Idle:
            {
                var target = FindAggroTarget(e, def);
                if (target != null)
                {
                    e.Ai = AiState.Aggro;
                    e.AggroTargetId = target.Id;
                    break;
                }
                // Occasional short wander near home.
                if (Tick >= e.NextWanderTick)
                {
                    e.NextWanderTick = Tick + _rng.Range(40, 120);
                    e.WanderDirX = _rng.Range(-1, 1);
                    e.WanderDirY = _rng.Range(-1, 1);
                    long dhx = e.X - e.HomeX, dhy = e.Y - e.HomeY;
                    long half = def.LeashRange / 2;
                    if (dhx * dhx + dhy * dhy > half * half)
                    {
                        e.WanderDirX = Math.Sign(e.HomeX - e.X);
                        e.WanderDirY = Math.Sign(e.HomeY - e.Y);
                    }
                }
                if (Tick % 2 == 0) // half-speed amble
                    MoveEntity(e, e.WanderDirX, e.WanderDirY, allowSafeZone: false);
                break;
            }
            case AiState.Aggro:
            {
                var target = GetEntity(e.AggroTargetId);
                long dhx2 = e.X - e.HomeX, dhy2 = e.Y - e.HomeY;
                bool leashed = dhx2 * dhx2 + dhy2 * dhy2 > (long)def.LeashRange * def.LeashRange;
                if (target is null || !target.Alive || Data.Map.IsSafeZoneUnits(target.X, target.Y) || leashed)
                {
                    e.Ai = AiState.Return;
                    e.AggroTargetId = -1;
                    break;
                }

                long atkRange = def.Attack.Range;
                if (e.DistanceSquaredTo(target) <= atkRange * atkRange)
                {
                    FaceToward(e, target.X, target.Y);
                    if (e.Atb >= def.Attack.AtbCost)
                    {
                        e.Atb -= def.Attack.AtbCost;
                        Events.Add(new SimEvent(SimEventKind.AbilityUsed, e.Id, target.Id, -1, e.ArchetypeId));
                        DealDamage(e, target, def.Attack.Power, magical: false);
                    }
                }
                else
                {
                    MoveEntity(e, Math.Sign(target.X - e.X), Math.Sign(target.Y - e.Y), allowSafeZone: false);
                }
                break;
            }
            case AiState.Return:
            {
                // Classic leash reset: sprint home, heal up fast.
                if (Tick % 5 == 0)
                    e.Hp = Math.Min(e.MaxHp, e.Hp + Math.Max(1, e.MaxHp / 20));
                int dx = e.HomeX - e.X, dy = e.HomeY - e.Y;
                if (Math.Abs(dx) < SimConstants.UnitsPerTile / 2 && Math.Abs(dy) < SimConstants.UnitsPerTile / 2)
                {
                    e.Ai = AiState.Idle;
                    e.Hp = e.MaxHp;
                }
                else
                {
                    MoveEntity(e, Math.Sign(dx), Math.Sign(dy), allowSafeZone: false);
                }
                break;
            }
        }
    }

    private Entity? FindAggroTarget(Entity monster, MonsterDef def)
    {
        if (def.AggroRange <= 0) return null;
        long best = (long)def.AggroRange * def.AggroRange;
        Entity? bestTarget = null;
        foreach (var other in EntitiesInOrder())
        {
            if (!other.IsPlayer || !other.Alive) continue;
            if (other.Vehicle != VehicleKind.None) continue; // riders are ignored
            if (Data.Map.IsSafeZoneUnits(other.X, other.Y)) continue;
            long d2 = monster.DistanceSquaredTo(other);
            if (d2 <= best)
            {
                best = d2;
                bestTarget = other;
            }
        }
        return bestTarget;
    }

    // ------------------------------------------------------------------ combat

    /// <summary>Pre-variance damage: stat·power/10 minus defense mitigation (½ physical, ¼ magical).</summary>
    public static int BaseDamage(int stat, int defense, int power, bool magical)
        => Math.Max(1, stat * power / 10 - (magical ? defense / 4 : defense / 2));

    private void DealDamage(Entity attacker, Entity victim, int power, bool magical)
    {
        int damage = Vary(BaseDamage(magical ? attacker.Magic : attacker.Attack, victim.Defense, power, magical));

        victim.Hp -= damage;
        victim.LastDamagedTick = Tick;
        Events.Add(new SimEvent(SimEventKind.Damage, attacker.Id, victim.Id, damage));

        if (victim.IsMonster && attacker.IsPlayer)
            victim.Taggers.Add(attacker.Id);

        if (victim.Hp <= 0)
        {
            victim.Hp = 0;
            victim.Alive = false;
            Events.Add(new SimEvent(SimEventKind.Death, attacker.Id, victim.Id, 0));

            if (victim.IsMonster)
            {
                var def = Data.Monsters[victim.ArchetypeId];
                foreach (var credited in KillCreditSet(victim))
                {
                    GrantXpAndGil(credited, def);
                    RollDrops(credited, def);
                    CountQuestKill(credited, def.Id);
                }
                victim.Taggers.Clear();
                victim.RespawnAtTick = Tick + 40; // corpse lingers ~2 s for the death anim

                var spawner = _spawners.Find(s => s.Def.Id == victim.SpawnerId);
                spawner?.PendingRespawnTicks.Add(Tick + spawner.Def.RespawnTicks);
            }
            else
            {
                victim.RespawnAtTick = Tick + SimConstants.PlayerRespawnTicks;
                victim.Atb = 0;
            }
        }
    }

    private void GrantXpAndGil(Entity player, MonsterDef def)
    {
        player.Gil += _rng.Range(def.GilMin, def.GilMax);
        player.Xp += def.XpReward;

        while (player.Level < SimConstants.MaxLevel && player.Xp >= XpToNextLevel(player.Level))
        {
            player.Xp -= XpToNextLevel(player.Level);
            player.Level++;
            RecomputeStats(player);
            player.Hp = player.MaxHp; // classic full heal on level-up
            player.Mp = player.MaxMp;
            Events.Add(new SimEvent(SimEventKind.LevelUp, player.Id, player.Id, player.Level));
        }
    }

    /// <summary>XP required to advance from `level` to `level`+1: 100·level^1.5, integer-exact.</summary>
    public static int XpToNextLevel(int level)
        => (int)ISqrt((ulong)level * (ulong)level * (ulong)level * 10000UL);

    private static ulong ISqrt(ulong n)
    {
        if (n == 0) return 0;
        ulong x = n, y = (x + 1) / 2;
        while (y < x)
        {
            x = y;
            y = (x + n / x) / 2;
        }
        return x;
    }

    /// <summary>±10% deterministic variance.</summary>
    private int Vary(int amount) => Math.Max(1, amount * (900 + _rng.Next(201)) / 1000);

    private void RespawnPlayer(Entity e)
    {
        e.Alive = true;
        (e.X, e.Y) = SpawnPointUnits();
        e.Hp = e.MaxHp;
        e.Mp = e.MaxMp;
        e.Atb = 0;
        e.Facing = Facing.Down;
        Events.Add(new SimEvent(SimEventKind.Respawn, e.Id, e.Id, 0));
    }

    // ------------------------------------------------------------------ movement

    private void MoveEntity(Entity e, int dirX, int dirY, bool allowSafeZone)
    {
        dirX = Math.Sign(dirX);
        dirY = Math.Sign(dirY);
        if (dirX == 0 && dirY == 0) return;

        int speed = e.Vehicle == VehicleKind.None ? e.MoveSpeed : VehicleMoveSpeed(e);
        int stepX = dirX * speed, stepY = dirY * speed;
        if (dirX != 0 && dirY != 0)
        {
            stepX = stepX * 5 / 7; // ≈ 1/√2 without floats
            stepY = stepY * 5 / 7;
            if (stepX == 0) stepX = dirX;
            if (stepY == 0) stepY = dirY;
        }

        // Per-axis slide.
        int nx = e.X + stepX;
        if (CanStand(e, nx, e.Y, allowSafeZone)) e.X = nx;
        int ny = e.Y + stepY;
        if (CanStand(e, e.X, ny, allowSafeZone)) e.Y = ny;

        // Facing prefers horizontal (FF6 sprites read better side-on).
        if (dirX < 0) e.Facing = Facing.Left;
        else if (dirX > 0) e.Facing = Facing.Right;
        else if (dirY < 0) e.Facing = Facing.Up;
        else e.Facing = Facing.Down;
    }

    private bool Walkable(int ux, int uy, bool allowSafeZone)
    {
        if (!Data.Map.IsWalkableUnits(ux, uy)) return false;
        if (!allowSafeZone && Data.Map.IsSafeZoneUnits(ux, uy)) return false;
        return true;
    }

    private bool CanStand(Entity e, int ux, int uy, bool allowSafeZone)
        => e.Vehicle == VehicleKind.None
            ? Walkable(ux, uy, allowSafeZone)
            : UnitsAllow(ux, uy, e.Vehicle);

    private static void FaceToward(Entity e, int tx, int ty)
    {
        int dx = tx - e.X, dy = ty - e.Y;
        if (Math.Abs(dx) >= Math.Abs(dy))
            e.Facing = dx < 0 ? Facing.Left : Facing.Right;
        else
            e.Facing = dy < 0 ? Facing.Up : Facing.Down;
    }

    // ------------------------------------------------------------------ hashing

    /// <summary>FNV-1a 64 over canonical state; equal hashes ⇒ equal gameplay state.</summary>
    public ulong StateHash()
    {
        ulong h = 14695981039346656037UL;
        void Mix(long v)
        {
            for (int i = 0; i < 8; i++)
            {
                h ^= (byte)(v >> (i * 8));
                h *= 1099511628211UL;
            }
        }

        Mix(Tick);
        Mix((long)_rng.State);
        Mix(_entityOrder.Count);
        foreach (int id in _entityOrder)
        {
            var e = _entities[id];
            Mix(e.Id);
            Mix((int)e.Kind);
            Mix(e.X);
            Mix(e.Y);
            Mix(e.Hp);
            Mix(e.Mp);
            Mix(e.Atb);
            Mix(e.Level);
            Mix(e.Xp);
            Mix(e.Gil);
            Mix(e.Alive ? 1 : 0);
            Mix((int)e.Ai);
            Mix((int)e.Facing);
            Mix(e.PartyId);
            Mix((int)e.Vehicle);
            if (e.HasAppearance)
                foreach (byte ab in e.Appearance.ToBytes())
                    Mix(ab);
            MixString(e.NodeType);
            MixString(e.WeaponId);
            MixString(e.ArmorId);
            Mix(e.Inventory.Count);
            foreach (var stack in e.Inventory)
            {
                MixString(stack.ItemId);
                Mix(stack.Count);
            }
            Mix(e.QuestLog.Count);
            foreach (var questId in e.QuestLog.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                var q = e.QuestLog[questId];
                MixString(questId);
                Mix((int)q.State);
                Mix(q.Kills);
            }
        }
        return h;

        void MixString(string s)
        {
            foreach (char c in s) Mix(c);
            Mix(s.Length);
        }
    }
}

/// <summary>Persisted character state (per account, saved server-side).</summary>
public sealed class CharacterSave
{
    public string ClassId { get; set; } = "";
    public int Level { get; set; } = 1;
    public int Xp { get; set; }
    public int Gil { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public List<ItemStack> Inventory { get; set; } = new();
    public string WeaponId { get; set; } = "";
    public string ArmorId { get; set; } = "";
    public Dictionary<string, QuestProgress> Quests { get; set; } = new();
    /// <summary>Appearance bytes (race, skin, hairStyle, hairColor, outfit, outfitA, outfitB, acc).</summary>
    public List<int> Appearance { get; set; } = new();
    public int Vehicle { get; set; }
}
