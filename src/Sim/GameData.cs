using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenFantasy.Sim;

// ---------------------------------------------------------------------------
// Content data model. Loaded from data/*.json and validated before the world
// is allowed to boot. Pure data — no behavior.
// ---------------------------------------------------------------------------

public sealed class StatBlock
{
    public int Hp { get; set; }
    public int Mp { get; set; }
    public int Attack { get; set; }
    public int Magic { get; set; }
    public int Defense { get; set; }
    public int Speed { get; set; }
}

public sealed class ClassDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public StatBlock BaseStats { get; set; } = new();
    public StatBlock Growth { get; set; } = new();
    /// <summary>Movement speed in world units per tick.</summary>
    public int MoveSpeed { get; set; }
    /// <summary>Ability ids in hotbar slot order.</summary>
    public List<string> Abilities { get; set; } = new();
}

public enum AbilityKind
{
    Melee,
    Projectile,
    Heal,
}

public sealed class AbilityDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AbilityKind Kind { get; set; }
    /// <summary>Damage/heal multiplier ×10 (10 = 1.0× stat).</summary>
    public int Power { get; set; }
    /// <summary>Max use distance in world units.</summary>
    public int Range { get; set; }
    /// <summary>ATB drained per use (gauge is 0..1000).</summary>
    public int AtbCost { get; set; }
    public int MpCost { get; set; }
    public int UnlockLevel { get; set; } = 1;
    /// <summary>0 = single target; otherwise hits all enemies within this radius (units) of the primary point.</summary>
    public int AoeRadius { get; set; }
    /// <summary>True: scales with Magic (¼-def mitigation); false: scales with Attack (½-def).</summary>
    public bool UsesMagic { get; set; }
}

public enum ItemKind
{
    Consumable,
    Weapon,
    Armor,
    Material,
}

public sealed class ItemDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ItemKind Kind { get; set; }
    /// <summary>Shop buy price in gil; sell price is half.</summary>
    public int Price { get; set; }
    public int HealHp { get; set; }
    public int HealMp { get; set; }
    public int AttackBonus { get; set; }
    public int MagicBonus { get; set; }
    public int DefenseBonus { get; set; }
    public int HpBonus { get; set; }
}

public sealed class DropDef
{
    public string Item { get; set; } = "";
    public int ChancePercent { get; set; }
}

public sealed class NpcDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Sprite { get; set; } = "";
    public int TileX { get; set; }
    public int TileY { get; set; }
    public string Dialog { get; set; } = "";
    public List<string> Shop { get; set; } = new();
    public List<string> Quests { get; set; } = new();
}

public sealed class QuestDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Giver { get; set; } = "";
    public string Offer { get; set; } = "";
    public string TurnIn { get; set; } = "";
    public string TargetMonster { get; set; } = "";
    public int TargetCount { get; set; }
    public int RewardXp { get; set; }
    public int RewardGil { get; set; }
    public string RewardItem { get; set; } = "";
    public int MinLevel { get; set; } = 1;
}

public sealed class MonsterAttackDef
{
    public int Power { get; set; }
    public int Range { get; set; }
    public int AtbCost { get; set; }
}

public sealed class MonsterDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Level { get; set; } = 1;
    public StatBlock Stats { get; set; } = new();
    public int MoveSpeed { get; set; }
    public int XpReward { get; set; }
    public int GilMin { get; set; }
    public int GilMax { get; set; }
    /// <summary>Distance (units) at which the monster aggros a player.</summary>
    public int AggroRange { get; set; }
    /// <summary>Distance (units) from home beyond which the monster gives up and returns.</summary>
    public int LeashRange { get; set; }
    public MonsterAttackDef Attack { get; set; } = new();
    public List<DropDef> Drops { get; set; } = new();
}

public sealed class ClassesFile { public List<ClassDef> Classes { get; set; } = new(); }
public sealed class AbilitiesFile { public List<AbilityDef> Abilities { get; set; } = new(); }
public sealed class MonstersFile { public List<MonsterDef> Monsters { get; set; } = new(); }
public sealed class ItemsFile { public List<ItemDef> Items { get; set; } = new(); }
public sealed class NpcsFile { public List<NpcDef> Npcs { get; set; } = new(); }
public sealed class QuestsFile { public List<QuestDef> Quests { get; set; } = new(); }

/// <summary>All static content, indexed and validated.</summary>
public sealed class GameData
{
    public Dictionary<string, ClassDef> Classes { get; } = new();
    public Dictionary<string, AbilityDef> Abilities { get; } = new();
    public Dictionary<string, MonsterDef> Monsters { get; } = new();
    public Dictionary<string, ItemDef> Items { get; } = new();
    public Dictionary<string, NpcDef> Npcs { get; } = new();
    public Dictionary<string, QuestDef> Quests { get; } = new();
    public WorldMap Map { get; }

    /// <summary>Stable, sorted class id list (menu order, determinism).</summary>
    public List<string> ClassOrder { get; } = new();
    /// <summary>Stable, sorted npc id list (spawn order determinism).</summary>
    public List<string> NpcOrder { get; } = new();

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private GameData(WorldMap map) { Map = map; }

    /// <summary>
    /// Loads and validates all content from a data directory.
    /// Throws <see cref="DataValidationException"/> with a readable message on any problem.
    /// </summary>
    public static GameData LoadFromDirectory(string dataDir)
    {
        var classes = LoadFile<ClassesFile>(Path.Combine(dataDir, "classes.json"));
        var abilities = LoadFile<AbilitiesFile>(Path.Combine(dataDir, "abilities.json"));
        var monsters = LoadFile<MonstersFile>(Path.Combine(dataDir, "monsters.json"));
        var items = LoadFile<ItemsFile>(Path.Combine(dataDir, "items.json"));
        var npcs = LoadFile<NpcsFile>(Path.Combine(dataDir, "npcs.json"));
        var quests = LoadFile<QuestsFile>(Path.Combine(dataDir, "quests.json"));
        var map = WorldMap.LoadFromFile(Path.Combine(dataDir, "world.json"));
        return FromParts(classes, abilities, monsters, map, items, npcs, quests);
    }

    /// <summary>Assemble + validate from already-parsed parts (used by tests).</summary>
    public static GameData FromParts(ClassesFile classes, AbilitiesFile abilities, MonstersFile monsters, WorldMap map,
        ItemsFile? items = null, NpcsFile? npcs = null, QuestsFile? quests = null)
    {
        var data = new GameData(map);
        foreach (var it in (items ?? new ItemsFile()).Items)
        {
            if (!data.Items.TryAdd(it.Id, it))
                throw new DataValidationException($"items.json: duplicate item id '{it.Id}'");
        }
        foreach (var n in (npcs ?? new NpcsFile()).Npcs)
        {
            if (!data.Npcs.TryAdd(n.Id, n))
                throw new DataValidationException($"npcs.json: duplicate npc id '{n.Id}'");
        }
        foreach (var q in (quests ?? new QuestsFile()).Quests)
        {
            if (!data.Quests.TryAdd(q.Id, q))
                throw new DataValidationException($"quests.json: duplicate quest id '{q.Id}'");
        }
        data.NpcOrder.AddRange(data.Npcs.Keys.OrderBy(k => k, StringComparer.Ordinal));
        foreach (var a in abilities.Abilities)
        {
            if (!data.Abilities.TryAdd(a.Id, a))
                throw new DataValidationException($"abilities.json: duplicate ability id '{a.Id}'");
        }
        foreach (var c in classes.Classes)
        {
            if (!data.Classes.TryAdd(c.Id, c))
                throw new DataValidationException($"classes.json: duplicate class id '{c.Id}'");
        }
        foreach (var m in monsters.Monsters)
        {
            if (!data.Monsters.TryAdd(m.Id, m))
                throw new DataValidationException($"monsters.json: duplicate monster id '{m.Id}'");
        }
        data.ClassOrder.AddRange(data.Classes.Keys.OrderBy(k => k, StringComparer.Ordinal));
        DataValidator.Validate(data);
        return data;
    }

    private static T LoadFile<T>(string path)
    {
        if (!File.Exists(path))
            throw new DataValidationException($"missing content file: {path}");
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)
                   ?? throw new DataValidationException($"{path}: file parsed to null");
        }
        catch (JsonException e)
        {
            throw new DataValidationException($"{path}: invalid JSON — {e.Message}");
        }
    }
}

public sealed class DataValidationException : Exception
{
    public DataValidationException(string message) : base(message) { }
}

/// <summary>
/// Schema validation for all editor-authored content. The server refuses to
/// boot on any violation; errors name the file, entity, and field.
/// </summary>
public static class DataValidator
{
    public static void Validate(GameData data)
    {
        if (data.Classes.Count == 0) Fail("classes.json: no classes defined");
        if (data.Monsters.Count == 0) Fail("monsters.json: no monsters defined");

        foreach (var (id, c) in data.Classes)
        {
            Require(id.Length > 0, "classes.json: class with empty id");
            Require(c.Name.Length > 0, $"classes.json[{id}]: name is required");
            ValidateStats($"classes.json[{id}].baseStats", c.BaseStats, minHp: 1);
            RequireRange(c.MoveSpeed, 1, 16, $"classes.json[{id}].moveSpeed");
            Require(c.Abilities.Count is >= 1 and <= 4, $"classes.json[{id}]: needs 1–4 abilities (hotbar size)");
            foreach (var abilityId in c.Abilities)
                Require(data.Abilities.ContainsKey(abilityId),
                    $"classes.json[{id}]: unknown ability '{abilityId}'");
        }

        foreach (var (id, a) in data.Abilities)
        {
            Require(a.Name.Length > 0, $"abilities.json[{id}]: name is required");
            RequireRange(a.Power, 1, 100, $"abilities.json[{id}].power");
            RequireRange(a.Range, 8, SimConstants.MaxAbilityRange, $"abilities.json[{id}].range");
            RequireRange(a.AtbCost, 1, SimConstants.AtbMax, $"abilities.json[{id}].atbCost");
            RequireRange(a.MpCost, 0, 999, $"abilities.json[{id}].mpCost");
            RequireRange(a.UnlockLevel, 1, SimConstants.MaxLevel, $"abilities.json[{id}].unlockLevel");
            RequireRange(a.AoeRadius, 0, 8 * SimConstants.UnitsPerTile, $"abilities.json[{id}].aoeRadius");
        }

        foreach (var (id, m) in data.Monsters)
        {
            Require(m.Name.Length > 0, $"monsters.json[{id}]: name is required");
            RequireRange(m.Level, 1, SimConstants.MaxLevel, $"monsters.json[{id}].level");
            ValidateStats($"monsters.json[{id}].stats", m.Stats, minHp: 1);
            RequireRange(m.MoveSpeed, 1, 16, $"monsters.json[{id}].moveSpeed");
            RequireRange(m.XpReward, 1, 100000, $"monsters.json[{id}].xpReward");
            Require(m.GilMin >= 0 && m.GilMax >= m.GilMin, $"monsters.json[{id}]: gil range invalid");
            RequireRange(m.AggroRange, 0, 20 * SimConstants.UnitsPerTile, $"monsters.json[{id}].aggroRange");
            Require(m.LeashRange > m.AggroRange, $"monsters.json[{id}]: leashRange must exceed aggroRange");
            RequireRange(m.Attack.Power, 1, 100, $"monsters.json[{id}].attack.power");
            RequireRange(m.Attack.Range, 8, SimConstants.MaxAbilityRange, $"monsters.json[{id}].attack.range");
            RequireRange(m.Attack.AtbCost, 1, SimConstants.AtbMax, $"monsters.json[{id}].attack.atbCost");
        }

        foreach (var (id, m) in data.Monsters)
        {
            foreach (var drop in m.Drops)
            {
                Require(data.Items.ContainsKey(drop.Item),
                    $"monsters.json[{id}]: drop references unknown item '{drop.Item}'");
                RequireRange(drop.ChancePercent, 1, 100, $"monsters.json[{id}].drops[{drop.Item}].chancePercent");
            }
        }

        foreach (var (id, it) in data.Items)
        {
            Require(it.Name.Length > 0, $"items.json[{id}]: name is required");
            RequireRange(it.Price, 1, 100000, $"items.json[{id}].price");
            Require(it.HealHp >= 0 && it.HealMp >= 0, $"items.json[{id}]: heal values must be ≥ 0");
            if (it.Kind == ItemKind.Consumable)
                Require(it.HealHp + it.HealMp > 0, $"items.json[{id}]: consumable must heal something");
        }

        foreach (var (id, n) in data.Npcs)
        {
            Require(n.Name.Length > 0, $"npcs.json[{id}]: name is required");
            Require(n.Sprite.Length > 0, $"npcs.json[{id}]: sprite is required");
            Require(data.Map.IsWalkableTile(n.TileX, n.TileY),
                $"npcs.json[{id}]: tile ({n.TileX},{n.TileY}) is not walkable");
            foreach (var itemId in n.Shop)
                Require(data.Items.ContainsKey(itemId), $"npcs.json[{id}]: unknown shop item '{itemId}'");
            foreach (var questId in n.Quests)
                Require(data.Quests.ContainsKey(questId), $"npcs.json[{id}]: unknown quest '{questId}'");
        }

        foreach (var (id, q) in data.Quests)
        {
            Require(q.Name.Length > 0, $"quests.json[{id}]: name is required");
            Require(data.Npcs.ContainsKey(q.Giver), $"quests.json[{id}]: unknown giver '{q.Giver}'");
            Require(data.Npcs[q.Giver].Quests.Contains(id),
                $"quests.json[{id}]: giver '{q.Giver}' does not list this quest");
            Require(data.Monsters.ContainsKey(q.TargetMonster),
                $"quests.json[{id}]: unknown target monster '{q.TargetMonster}'");
            RequireRange(q.TargetCount, 1, 999, $"quests.json[{id}].targetCount");
            Require(q.RewardXp >= 0 && q.RewardGil >= 0, $"quests.json[{id}]: rewards must be ≥ 0");
            if (q.RewardItem.Length > 0)
                Require(data.Items.ContainsKey(q.RewardItem),
                    $"quests.json[{id}]: unknown reward item '{q.RewardItem}'");
            RequireRange(q.MinLevel, 1, SimConstants.MaxLevel, $"quests.json[{id}].minLevel");
        }

        ValidateMap(data);
    }

    private static void ValidateMap(GameData data)
    {
        var map = data.Map;
        Require(map.Width is >= 16 and <= 1024 && map.Height is >= 16 and <= 1024,
            $"world.json: size {map.Width}x{map.Height} outside 16..1024");
        Require(map.IsWalkableTile(map.PlayerSpawnX, map.PlayerSpawnY),
            $"world.json: playerSpawn ({map.PlayerSpawnX},{map.PlayerSpawnY}) is not walkable");
        Require(map.SafeZone.Contains(map.PlayerSpawnX, map.PlayerSpawnY),
            "world.json: playerSpawn must be inside the safe zone");

        var spawnerIds = new HashSet<int>();
        foreach (var s in map.Spawners)
        {
            Require(spawnerIds.Add(s.Id), $"world.json: duplicate spawner id {s.Id}");
            Require(data.Monsters.ContainsKey(s.Monster),
                $"world.json: spawner {s.Id} references unknown monster '{s.Monster}'");
            RequireRange(s.Count, 1, 64, $"world.json: spawner {s.Id}.count");
            RequireRange(s.RespawnTicks, 1, 100000, $"world.json: spawner {s.Id}.respawnTicks");
            Require(s.Region.W > 0 && s.Region.H > 0 &&
                    s.Region.X >= 0 && s.Region.Y >= 0 &&
                    s.Region.X + s.Region.W <= map.Width && s.Region.Y + s.Region.H <= map.Height,
                $"world.json: spawner {s.Id} region out of bounds");
            bool anyWalkable = false;
            for (int ty = s.Region.Y; ty < s.Region.Y + s.Region.H && !anyWalkable; ty++)
                for (int tx = s.Region.X; tx < s.Region.X + s.Region.W && !anyWalkable; tx++)
                    anyWalkable = map.IsWalkableTile(tx, ty);
            Require(anyWalkable, $"world.json: spawner {s.Id} region has no walkable tile");
        }
    }

    private static void ValidateStats(string where, StatBlock s, int minHp)
    {
        Require(s.Hp >= minHp, $"{where}.hp must be ≥ {minHp}");
        Require(s.Mp >= 0, $"{where}.mp must be ≥ 0");
        Require(s.Attack >= 0 && s.Magic >= 0 && s.Defense >= 0, $"{where}: attack/magic/defense must be ≥ 0");
        Require(s.Speed >= 1, $"{where}.speed must be ≥ 1");
    }

    private static void RequireRange(int value, int min, int max, string field)
        => Require(value >= min && value <= max, $"{field} = {value}, expected {min}..{max}");

    private static void Require(bool cond, string message)
    {
        if (!cond) Fail(message);
    }

    private static void Fail(string message) => throw new DataValidationException(message);
}
