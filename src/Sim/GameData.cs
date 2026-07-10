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
    Tool,
    Vehicle,
}

public enum ToolKind
{
    None,
    Axe,
    Pickaxe,
    Sickle,
}

public enum VehicleKind : byte
{
    None = 0,
    Canoe = 1,
    Boat = 2,
    Ship = 3,
    Tank = 4,
    Airship = 5,
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
    /// <summary>For Kind == Tool: which resource nodes this can work.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ToolKind Tool { get; set; } = ToolKind.None;
    /// <summary>Tool quality: material yielded per swing.</summary>
    public int ToolPower { get; set; } = 1;
    /// <summary>For Kind == Vehicle: which vehicle this key item summons.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VehicleKind Vehicle { get; set; } = VehicleKind.None;
}

public sealed class RecipeInput
{
    public string Item { get; set; } = "";
    public int Count { get; set; } = 1;
}

public enum CraftStation
{
    None,
    Forge,
}

public sealed class RecipeDef
{
    public string Id { get; set; } = "";
    public string Output { get; set; } = "";
    public int OutputCount { get; set; } = 1;
    public List<RecipeInput> Inputs { get; set; } = new();
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public CraftStation Station { get; set; } = CraftStation.None;
}

public sealed class RecipesFile { public List<RecipeDef> Recipes { get; set; } = new(); }

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
    /// <summary>Extra flavor lines; interactions cycle through Dialog + Dialogs.</summary>
    public List<string> Dialogs { get; set; } = new();
    public List<string> Shop { get; set; } = new();
    public List<string> Quests { get; set; } = new();
    /// <summary>Composited look (preferred over Sprite when set).</summary>
    public AppearancePreset? Appearance { get; set; }
}

public enum QuestKind
{
    Kill,
    /// <summary>Reach a named map region.</summary>
    Explore,
    /// <summary>Deliver word to another NPC (turn in at TalkNpc).</summary>
    Talk,
}

public sealed class QuestDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public QuestKind Kind { get; set; } = QuestKind.Kill;
    /// <summary>Main-story quests chain; side quests flavor a town.</summary>
    public bool Main { get; set; }
    /// <summary>Quest id that must be Completed before this can be accepted.</summary>
    public string Prerequisite { get; set; } = "";
    public string Giver { get; set; } = "";
    public string Offer { get; set; } = "";
    public string TurnIn { get; set; } = "";
    public string TargetMonster { get; set; } = "";
    /// <summary>Explore: region id to reach.</summary>
    public string TargetRegion { get; set; } = "";
    /// <summary>Talk: npc id to deliver to (turn-in happens there).</summary>
    public string TalkNpc { get; set; } = "";
    public int TargetCount { get; set; } = 1;
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
    public Dictionary<string, RecipeDef> Recipes { get; } = new();
    public WorldMap Map { get; }
    public AppearanceData Appearance { get; private set; } = new();

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
        var recipes = LoadFile<RecipesFile>(Path.Combine(dataDir, "recipes.json"));
        var appearance = AppearanceData.LoadFromFile(Path.Combine(dataDir, "appearance.json"));
        var map = WorldMap.LoadFromFile(Path.Combine(dataDir, "world.json"));
        return FromParts(classes, abilities, monsters, map, items, npcs, quests, recipes, appearance);
    }

    /// <summary>Assemble + validate from already-parsed parts (used by tests).</summary>
    public static GameData FromParts(ClassesFile classes, AbilitiesFile abilities, MonstersFile monsters, WorldMap map,
        ItemsFile? items = null, NpcsFile? npcs = null, QuestsFile? quests = null,
        RecipesFile? recipes = null, AppearanceData? appearance = null)
    {
        var data = new GameData(map);
        if (appearance != null) data.Appearance = appearance;
        foreach (var rec in (recipes ?? new RecipesFile()).Recipes)
        {
            if (!data.Recipes.TryAdd(rec.Id, rec))
                throw new DataValidationException($"recipes.json: duplicate recipe id '{rec.Id}'");
        }
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
            if (it.Kind == ItemKind.Tool)
            {
                Require(it.Tool != ToolKind.None, $"items.json[{id}]: tool items need a tool type");
                RequireRange(it.ToolPower, 1, 10, $"items.json[{id}].toolPower");
            }
            if (it.Kind == ItemKind.Vehicle)
                Require(it.Vehicle != VehicleKind.None, $"items.json[{id}]: vehicle items need a vehicle type");
        }

        foreach (var (id, rec) in data.Recipes)
        {
            Require(data.Items.ContainsKey(rec.Output), $"recipes.json[{id}]: unknown output '{rec.Output}'");
            RequireRange(rec.OutputCount, 1, 99, $"recipes.json[{id}].outputCount");
            Require(rec.Inputs.Count is >= 1 and <= 4, $"recipes.json[{id}]: needs 1–4 inputs");
            foreach (var input in rec.Inputs)
            {
                Require(data.Items.ContainsKey(input.Item), $"recipes.json[{id}]: unknown input '{input.Item}'");
                RequireRange(input.Count, 1, 99, $"recipes.json[{id}].inputs[{input.Item}].count");
                Require(input.Item != rec.Output, $"recipes.json[{id}]: output cannot be its own input");
            }
        }

        foreach (var (id, n) in data.Npcs)
        {
            Require(n.Name.Length > 0, $"npcs.json[{id}]: name is required");
            Require(n.Sprite.Length > 0 || n.Appearance != null,
                $"npcs.json[{id}]: needs a sprite or an appearance");
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
            switch (q.Kind)
            {
                case QuestKind.Kill:
                    Require(data.Monsters.ContainsKey(q.TargetMonster),
                        $"quests.json[{id}]: unknown target monster '{q.TargetMonster}'");
                    break;
                case QuestKind.Explore:
                    Require(data.Map.Region(q.TargetRegion) != null,
                        $"quests.json[{id}]: unknown target region '{q.TargetRegion}'");
                    break;
                case QuestKind.Talk:
                    Require(data.Npcs.ContainsKey(q.TalkNpc),
                        $"quests.json[{id}]: unknown talk npc '{q.TalkNpc}'");
                    Require(q.TalkNpc != q.Giver, $"quests.json[{id}]: talk target is the giver");
                    break;
            }
            if (q.Prerequisite.Length > 0)
                Require(data.Quests.ContainsKey(q.Prerequisite),
                    $"quests.json[{id}]: unknown prerequisite '{q.Prerequisite}'");
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
        Require(map.IsSafeZoneTile(map.PlayerSpawnX, map.PlayerSpawnY),
            "world.json: playerSpawn must be inside the safe zone");

        var nodeTypes = new HashSet<string> { "oak", "ore", "berry" };
        var nodeIds = new HashSet<int>();
        foreach (var n in map.Nodes)
        {
            Require(nodeIds.Add(n.Id), $"world.json: duplicate node id {n.Id}");
            Require(nodeTypes.Contains(n.Type), $"world.json: node {n.Id} has unknown type '{n.Type}'");
            RequireRange(n.Count, 1, 64, $"world.json: node {n.Id}.count");
            RequireRange(n.RespawnTicks, 1, 100000, $"world.json: node {n.Id}.respawnTicks");
            Require(n.Region.W > 0 && n.Region.H > 0 &&
                    n.Region.X >= 0 && n.Region.Y >= 0 &&
                    n.Region.X + n.Region.W <= map.Width && n.Region.Y + n.Region.H <= map.Height,
                $"world.json: node {n.Id} region out of bounds");
        }

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
