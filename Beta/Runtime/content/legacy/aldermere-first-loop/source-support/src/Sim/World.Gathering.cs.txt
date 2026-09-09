namespace OpenFantasy.Sim;

/// <summary>
/// M3 survival-sandbox systems: resource nodes, gathering, and crafting.
/// Deterministic and server-side, like everything else in the sim.
/// </summary>
public sealed partial class World
{
    public sealed record NodeArchetype(string Name, string Sprite, ToolKind Tool, int Hp, string YieldItem);

    public static readonly Dictionary<string, NodeArchetype> NodeArchetypes = new()
    {
        ["oak"] = new NodeArchetype("Oak", "node_oak", ToolKind.Axe, 5, "wood"),
        ["ore"] = new NodeArchetype("Ore Vein", "node_ore", ToolKind.Pickaxe, 6, "iron_ore"),
        ["berry"] = new NodeArchetype("Berry Bush", "node_berry", ToolKind.Sickle, 4, "berries"),
    };

    private const int GatherAtbCost = 400;
    private const int GatherRange = 56; // units

    private sealed class NodeSpawnerState
    {
        public required NodeDef Def;
        public required NodeArchetype Archetype;
        public readonly List<int> PendingRespawnTicks = new();
    }

    private readonly List<NodeSpawnerState> _nodeSpawners = new();

    private void InitNodes()
    {
        foreach (var def in Data.Map.Nodes)
        {
            var state = new NodeSpawnerState { Def = def, Archetype = NodeArchetypes[def.Type] };
            _nodeSpawners.Add(state);
            for (int i = 0; i < def.Count; i++)
                SpawnNode(state);
        }
    }

    private void SpawnNode(NodeSpawnerState spawner)
    {
        var def = spawner.Def;
        var arch = spawner.Archetype;
        int u = SimConstants.UnitsPerTile;

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
        if (tx < 0) return;

        var e = new Entity
        {
            Id = _nextEntityId++,
            Kind = EntityKind.Node,
            Name = arch.Name,
            ArchetypeId = arch.Sprite,
            NodeType = spawner.Def.Type,
            Hp = arch.Hp,
            MaxHp = arch.Hp,
            Level = 1,
            Speed = 1,
            SpawnerId = def.Id,
            X = tx * u + u / 2,
            Y = ty * u + u / 2,
        };
        _entities[e.Id] = e;
        _entityOrder.Add(e.Id);
    }

    private void TickNodeSpawners()
    {
        foreach (var spawner in _nodeSpawners)
        {
            for (int i = spawner.PendingRespawnTicks.Count - 1; i >= 0; i--)
            {
                if (spawner.PendingRespawnTicks[i] <= Tick)
                {
                    spawner.PendingRespawnTicks.RemoveAt(i);
                    SpawnNode(spawner);
                }
            }
        }
    }

    /// <summary>Best matching tool in the player's pack for a node type, or null.</summary>
    public ItemDef? BestTool(Entity player, ToolKind kind)
    {
        ItemDef? best = null;
        foreach (var stack in player.Inventory)
        {
            if (!Data.Items.TryGetValue(stack.ItemId, out var item)) continue;
            if (item.Kind != ItemKind.Tool || item.Tool != kind) continue;
            if (best == null || item.ToolPower > best.ToolPower) best = item;
        }
        return best;
    }

    /// <summary>One gathering swing at a node. Returns user-facing result text.</summary>
    public string Gather(Entity player, int nodeEntityId)
    {
        var node = GetEntity(nodeEntityId);
        if (node is not { IsNode: true, Alive: true } || node.MapId != player.MapId)
            return "There is nothing to gather there.";
        long r = GatherRange;
        if (player.DistanceSquaredTo(node) > r * r) return "Too far away.";
        if (!player.Alive) return "The fallen gather nothing.";
        if (player.Vehicle != VehicleKind.None) return "Not from up here — disembark first.";

        var arch = NodeArchetypes[node.NodeType];
        var tool = BestTool(player, arch.Tool);
        if (tool == null)
            return arch.Tool switch
            {
                ToolKind.Axe => "You need an axe to fell that.",
                ToolKind.Pickaxe => "You need a pickaxe to break that.",
                _ => "You need a sickle to harvest that.",
            };
        if (player.Atb < GatherAtbCost) return ""; // silently wait for the gauge

        player.Atb -= GatherAtbCost;
        FaceToward(player, node.X, node.Y);
        node.Hp--;
        node.LastDamagedTick = Tick;

        int amount = tool.ToolPower;
        var yieldItem = Data.Items[arch.YieldItem];
        bool added = AddItem(player, arch.YieldItem, amount);
        if (added)
            Events.Add(new SimEvent(SimEventKind.Loot, player.Id, player.Id, amount, arch.YieldItem));
        Events.Add(new SimEvent(SimEventKind.Damage, player.Id, node.Id, 1));

        if (node.Hp <= 0)
        {
            node.Hp = 0;
            node.Alive = false;
            node.RespawnAtTick = Tick + 20; // brief stump before removal
            Events.Add(new SimEvent(SimEventKind.Death, player.Id, node.Id, 0));
            var spawner = _nodeSpawners.Find(s => s.Def.Id == node.SpawnerId);
            spawner?.PendingRespawnTicks.Add(Tick + (spawner?.Def.RespawnTicks ?? 600));
        }
        return added ? $"+{amount} {yieldItem.Name}" : "Your pack is full!";
    }

    // ------------------------------------------------------------------ crafting

    /// <summary>Tile id of the forge in the loaded tileset, or -1.</summary>
    private int ForgeTileId => _forgeTileId ??= Data.Map.Tileset.Find(t => t.Name == "forge")?.Id ?? -1;
    private int? _forgeTileId;

    public bool IsNearForge(Entity player)
    {
        int id = ForgeTileId;
        if (id < 0) return false;
        int ptx = player.X / SimConstants.UnitsPerTile;
        int pty = player.Y / SimConstants.UnitsPerTile;
        for (int ty = pty - 2; ty <= pty + 2; ty++)
            for (int tx = ptx - 2; tx <= ptx + 2; tx++)
                if (Data.Map.TileAt(tx, ty) == id)
                    return true;
        return false;
    }

    /// <summary>Craft a recipe. Returns user-facing result text.</summary>
    public string CraftItem(Entity player, string recipeId)
    {
        if (!Data.Recipes.TryGetValue(recipeId, out var recipe)) return "No such recipe.";
        if (recipe.Station == CraftStation.Forge && !IsNearForge(player))
            return "You need to stand at the forge for that.";

        foreach (var input in recipe.Inputs)
            if (CountItem(player, input.Item) < input.Count)
                return $"Missing {input.Count}× {Data.Items[input.Item].Name}.";

        foreach (var input in recipe.Inputs)
            RemoveItem(player, input.Item, input.Count);

        if (!AddItem(player, recipe.Output, recipe.OutputCount))
        {
            foreach (var input in recipe.Inputs)          // refund
                AddItem(player, input.Item, input.Count);
            return "Your pack is too full to craft that.";
        }
        Events.Add(new SimEvent(SimEventKind.Loot, player.Id, player.Id, recipe.OutputCount, recipe.Output));
        return $"Crafted {recipe.OutputCount}× {Data.Items[recipe.Output].Name}.";
    }
}
