namespace OpenFantasy.Sim;

public enum EntityKind : byte
{
    Player = 0,
    Monster = 1,
    Npc = 2,
    /// <summary>Gatherable resource node (oak, ore vein, berry bush).</summary>
    Node = 3,
}

public enum QuestState : byte
{
    Active = 0,
    Completed = 1,
}

public sealed class ItemStack
{
    public string ItemId = "";
    public int Count;
}

public sealed class QuestProgress
{
    public QuestState State;
    public int Kills;
}

public enum Facing : byte
{
    Down = 0,
    Up = 1,
    Left = 2,
    Right = 3,
}

public enum AiState : byte
{
    Idle = 0,
    Aggro = 1,
    Return = 2,
}

/// <summary>
/// One live thing in the world: a player character or a monster.
/// Plain mutable state — the World owns all behavior.
/// </summary>
public sealed class Entity
{
    public int Id;
    public EntityKind Kind;
    public string Name = "";

    /// <summary>Class id for players, monster id for monsters.</summary>
    public string ArchetypeId = "";

    // Position in world units (32 per tile).
    public int X;
    public int Y;
    public Facing Facing = Facing.Down;

    /// <summary>Which map space this entity is in: 0 = overworld, >0 = an
    /// instanced adventure-site floor (see World.Instances).</summary>
    public int MapId;

    public int Level = 1;
    public int Xp;
    public int Gil;

    public int Hp;
    public int MaxHp;
    public int Mp;
    public int MaxMp;
    public int Attack;
    public int Magic;
    public int Defense;
    public int Speed;

    /// <summary>Movement speed in units per tick.</summary>
    public int MoveSpeed;

    /// <summary>ATB gauge, 0..SimConstants.AtbMax.</summary>
    public int Atb;

    public bool Alive = true;

    /// <summary>Last tick this entity took damage (out-of-combat regen gate).</summary>
    public int LastDamagedTick = -1000;

    /// <summary>Tick at which a dead entity respawns (players) or its spawner may respawn it (monsters).</summary>
    public int RespawnAtTick = -1;

    // --- Monster-only state ---
    public AiState Ai = AiState.Idle;
    public int SpawnerId = -1;
    public int HomeX;
    public int HomeY;
    public int AggroTargetId = -1;
    /// <summary>Next tick this monster may pick a new wander direction.</summary>
    public int NextWanderTick;
    public int WanderDirX;
    public int WanderDirY;

    // --- Player-only state ---
    /// <summary>Latest input applied each tick until replaced.</summary>
    public InputCommand Input = InputCommand.Neutral;
    public int LastInputSeq = -1;
    /// <summary>Entity ids of players who damaged this monster (XP tagging).</summary>
    public HashSet<int> Taggers = new();

    // --- M2 player state ---
    /// <summary>Stacked inventory in stable slot order (max SimConstants.InventorySlots).</summary>
    public List<ItemStack> Inventory = new();
    public string WeaponId = "";
    public string ArmorId = "";
    /// <summary>Quest id → progress. Iterate sorted by key for determinism.</summary>
    public Dictionary<string, QuestProgress> QuestLog = new();
    public int PartyId = -1;

    /// <summary>NPC definition id (Kind == Npc only).</summary>
    public string NpcDefId = "";

    /// <summary>Composited look (players and dressed NPCs).</summary>
    public bool HasAppearance;
    public Appearance Appearance = Appearance.Default;

    /// <summary>Node type key (Kind == Node only): oak / ore / berry.</summary>
    public string NodeType = "";

    /// <summary>Vehicle the player is currently aboard.</summary>
    public VehicleKind Vehicle = VehicleKind.None;

    /// <summary>Chest ids this player has looted (persisted; D9 loot).</summary>
    public HashSet<string> OpenedChests = new();

    public bool IsPlayer => Kind == EntityKind.Player;
    public bool IsMonster => Kind == EntityKind.Monster;
    public bool IsNpc => Kind == EntityKind.Npc;
    public bool IsNode => Kind == EntityKind.Node;

    public long DistanceSquaredTo(Entity other)
    {
        long dx = X - other.X;
        long dy = Y - other.Y;
        return dx * dx + dy * dy;
    }
}
