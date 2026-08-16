using OpenFantasy.Sim;
using OpenFantasy.Sim.SiteGen;

namespace OpenFantasy.Net;

public enum Op : byte
{
    Hello = 1,      // C→S: protocol, account name, class id
    Welcome = 2,    // S→C: your entity id, current tick
    Reject = 3,     // S→C: reason string, then disconnect
    Input = 4,      // C→S: InputCommand
    Snapshot = 5,   // S→C: tick, hash, entities, events
    Chat = 6,       // C→S: text · S→C: speaker name + text
    PlayerState = 7, // S→C (owner only): inventory, equipment, quests, party
    Interact = 8,    // C→S: npc entity id
    NpcInfo = 9,     // S→C: dialog, shop, quest offers for this player
    ItemOp = 10,     // C→S: buy/sell/use/equip/unequip
    QuestOp = 11,    // C→S: accept/complete
    CraftOp = 12,    // C→S: craft recipe id
    VehicleOp = 13,  // C→S: toggle embark/disembark
    SiteOp = 14,     // C→S: use surroundings (enter site, stairs, chest, search)
    MapData = 15,    // S→C: current map space tiles (instance floors; 0 = overworld)
}

public enum ItemOpKind : byte { Buy = 0, Sell = 1, Use = 2, Equip = 3, Unequip = 4 }
public enum QuestOpKind : byte { Accept = 0, Complete = 1 }

/// <summary>Quest status as shown by an NPC for one player.</summary>
public enum NpcQuestStatus : byte { Locked = 0, Offerable = 1, Active = 2, ReadyToTurnIn = 3, Done = 4 }

/// <summary>What a client knows about one entity, as described by snapshots.</summary>
public sealed class NetEntityState
{
    public int Id;
    public EntityKind Kind;
    public Facing Facing;
    public string Name = "";
    public string ArchetypeId = "";
    public int X, Y;
    public int Hp, MaxHp, Mp, MaxMp;
    public int Atb;
    public int Level, Xp, Gil;
    public bool Alive;
    public bool HasAppearance;
    public Appearance Appearance;
    public VehicleKind Vehicle;
}

public readonly struct NetEvent
{
    public readonly SimEventKind Kind;
    public readonly int SourceId, TargetId, Value;
    public readonly string AbilityId;

    public NetEvent(SimEventKind kind, int sourceId, int targetId, int value, string abilityId)
    {
        Kind = kind;
        SourceId = sourceId;
        TargetId = targetId;
        Value = value;
        AbilityId = abilityId;
    }
}

/// <summary>
/// Hand-rolled little-endian binary wire format. Both sides use these
/// helpers exclusively; the sim never sees bytes.
/// </summary>
public static class Protocol
{
    public static byte[] WriteHello(string account, string classId, Appearance appearance)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((byte)Op.Hello);
        w.Write(SimConstants.ProtocolVersion);
        w.Write(account);
        w.Write(classId);
        appearance.Write(w);
        return ms.ToArray();
    }

    public static (int protocol, string account, string classId, Appearance appearance) ReadHello(BinaryReader r)
        => (r.ReadInt32(), r.ReadString(), r.ReadString(), Appearance.Read(r));

    public static byte[] WriteVehicleOp()
    {
        return new[] { (byte)Op.VehicleOp };
    }

    public static byte[] WriteCraftOp(string recipeId)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((byte)Op.CraftOp);
        w.Write(recipeId);
        return ms.ToArray();
    }

    public static byte[] WriteWelcome(int entityId, int tick)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((byte)Op.Welcome);
        w.Write(entityId);
        w.Write(tick);
        return ms.ToArray();
    }

    public static (int entityId, int tick) ReadWelcome(BinaryReader r)
        => (r.ReadInt32(), r.ReadInt32());

    public static byte[] WriteReject(string reason)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((byte)Op.Reject);
        w.Write(reason);
        return ms.ToArray();
    }

    public static byte[] WriteInput(InputCommand input)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((byte)Op.Input);
        w.Write(input.Seq);
        w.Write(input.MoveX);
        w.Write(input.MoveY);
        w.Write(input.AbilitySlot);
        w.Write(input.TargetId);
        return ms.ToArray();
    }

    public static InputCommand ReadInput(BinaryReader r) => new()
    {
        Seq = r.ReadInt32(),
        MoveX = r.ReadSByte(),
        MoveY = r.ReadSByte(),
        AbilitySlot = r.ReadSByte(),
        TargetId = r.ReadInt32(),
    };

    public static byte[] WriteChat(string speaker, string text)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((byte)Op.Chat);
        w.Write(speaker);
        w.Write(text);
        return ms.ToArray();
    }

    public static (string speaker, string text) ReadChat(BinaryReader r)
        => (r.ReadString(), r.ReadString());

    public static byte[] WritePlayerState(World world, Entity player)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((byte)Op.PlayerState);
        w.Write(player.WeaponId);
        w.Write(player.ArmorId);
        w.Write((byte)player.Inventory.Count);
        foreach (var stack in player.Inventory)
        {
            w.Write(stack.ItemId);
            w.Write((ushort)stack.Count);
        }
        var questIds = player.QuestLog.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        w.Write((byte)questIds.Count);
        foreach (string questId in questIds)
        {
            var progress = player.QuestLog[questId];
            w.Write(questId);
            w.Write((byte)progress.State);
            w.Write((ushort)progress.Kills);
        }
        var members = player.PartyId >= 0 ? world.PartyMembers(player.PartyId) : Array.Empty<int>();
        w.Write((byte)members.Count);
        foreach (int memberId in members)
            w.Write(memberId);
        return ms.ToArray();
    }

    public sealed class PlayerStateMsg
    {
        public string WeaponId = "";
        public string ArmorId = "";
        public List<(string itemId, int count)> Inventory = new();
        public List<(string questId, byte state, int kills)> Quests = new();
        public List<int> PartyMemberIds = new();
    }

    public static PlayerStateMsg ReadPlayerState(BinaryReader r)
    {
        var msg = new PlayerStateMsg { WeaponId = r.ReadString(), ArmorId = r.ReadString() };
        int items = r.ReadByte();
        for (int i = 0; i < items; i++)
            msg.Inventory.Add((r.ReadString(), r.ReadUInt16()));
        int quests = r.ReadByte();
        for (int i = 0; i < quests; i++)
            msg.Quests.Add((r.ReadString(), r.ReadByte(), r.ReadUInt16()));
        int members = r.ReadByte();
        for (int i = 0; i < members; i++)
            msg.PartyMemberIds.Add(r.ReadInt32());
        return msg;
    }

    public static byte[] WriteInteract(int npcEntityId)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((byte)Op.Interact);
        w.Write(npcEntityId);
        return ms.ToArray();
    }

    public static byte[] WriteNpcInfo(int npcEntityId, NpcDef npc, string dialog,
        List<(string questId, string name, string text, NpcQuestStatus status)> quests)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((byte)Op.NpcInfo);
        w.Write(npcEntityId);
        w.Write(npc.Name);
        w.Write(dialog);
        w.Write((byte)npc.Shop.Count);
        foreach (string itemId in npc.Shop)
            w.Write(itemId);
        w.Write((byte)quests.Count);
        foreach (var (questId, name, text, status) in quests)
        {
            w.Write(questId);
            w.Write(name);
            w.Write(text);
            w.Write((byte)status);
        }
        return ms.ToArray();
    }

    public sealed class NpcInfoMsg
    {
        public int NpcEntityId;
        public string Name = "";
        public string Dialog = "";
        public List<string> ShopItems = new();
        public List<(string questId, string name, string text, NpcQuestStatus status)> Quests = new();
    }

    public static NpcInfoMsg ReadNpcInfo(BinaryReader r)
    {
        var msg = new NpcInfoMsg
        {
            NpcEntityId = r.ReadInt32(),
            Name = r.ReadString(),
            Dialog = r.ReadString(),
        };
        int shop = r.ReadByte();
        for (int i = 0; i < shop; i++)
            msg.ShopItems.Add(r.ReadString());
        int quests = r.ReadByte();
        for (int i = 0; i < quests; i++)
            msg.Quests.Add((r.ReadString(), r.ReadString(), r.ReadString(), (NpcQuestStatus)r.ReadByte()));
        return msg;
    }

    public static byte[] WriteItemOp(ItemOpKind op, string itemId, int npcEntityId)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((byte)Op.ItemOp);
        w.Write((byte)op);
        w.Write(itemId);
        w.Write(npcEntityId);
        return ms.ToArray();
    }

    public static (ItemOpKind op, string itemId, int npcEntityId) ReadItemOp(BinaryReader r)
        => ((ItemOpKind)r.ReadByte(), r.ReadString(), r.ReadInt32());

    public static byte[] WriteQuestOp(QuestOpKind op, string questId, int npcEntityId)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((byte)Op.QuestOp);
        w.Write((byte)op);
        w.Write(questId);
        w.Write(npcEntityId);
        return ms.ToArray();
    }

    public static (QuestOpKind op, string questId, int npcEntityId) ReadQuestOp(BinaryReader r)
        => ((QuestOpKind)r.ReadByte(), r.ReadString(), r.ReadInt32());

    public static byte[] WriteSiteOp()
    {
        return new[] { (byte)Op.SiteOp };
    }

    /// <summary>Current map space for one client. mapId 0 = overworld (no
    /// tiles — the client renders its local world.json); >0 carries the
    /// instance floor's tile bytes RLE-encoded.</summary>
    public static byte[] WriteMapData(int mapId, string label, SiteFloor? layout)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((byte)Op.MapData);
        w.Write(mapId);
        w.Write(label);
        if (mapId == 0 || layout is null)
        {
            w.Write(0);
            w.Write(0);
            return ms.ToArray();
        }
        w.Write(layout.Width);
        w.Write(layout.Height);
        // RLE: (tileByte, runLength) pairs.
        int i = 0, total = layout.Width * layout.Height;
        while (i < total)
        {
            byte t = (byte)layout.Tiles[i];
            int run = 1;
            while (i + run < total && (byte)layout.Tiles[i + run] == t && run < 65535) run++;
            w.Write(t);
            w.Write((ushort)run);
            i += run;
        }
        return ms.ToArray();
    }

    public sealed class MapDataMsg
    {
        public int MapId;
        public string Label = "";
        public int Width, Height;
        public byte[] Tiles = Array.Empty<byte>();
    }

    public static MapDataMsg ReadMapData(BinaryReader r)
    {
        var msg = new MapDataMsg { MapId = r.ReadInt32(), Label = r.ReadString() };
        msg.Width = r.ReadInt32();
        msg.Height = r.ReadInt32();
        int total = msg.Width * msg.Height;
        msg.Tiles = new byte[total];
        int i = 0;
        while (i < total)
        {
            byte t = r.ReadByte();
            int run = r.ReadUInt16();
            for (int k = 0; k < run && i < total; k++)
                msg.Tiles[i++] = t;
        }
        return msg;
    }

    public static byte[] WriteSnapshot(World world, IReadOnlyList<SimEvent> events, int mapId)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((byte)Op.Snapshot);
        w.Write(world.Tick);
        w.Write(world.StateHash());
        w.Write(mapId);

        var visible = new List<Entity>();
        foreach (var e in world.EntitiesInOrder())
            if (e.MapId == mapId)
                visible.Add(e);

        w.Write((ushort)visible.Count);
        foreach (var e in visible)
        {
            w.Write(e.Id);
            w.Write((byte)e.Kind);
            w.Write((byte)e.Facing);
            w.Write(e.Name);
            w.Write(e.ArchetypeId);
            w.Write(e.X);
            w.Write(e.Y);
            w.Write(e.Hp);
            w.Write(e.MaxHp);
            w.Write(e.Mp);
            w.Write(e.MaxMp);
            w.Write((ushort)e.Atb);
            w.Write((byte)e.Level);
            w.Write(e.Xp);
            w.Write(e.Gil);
            w.Write(e.Alive);
            w.Write((byte)e.Vehicle);
            w.Write(e.HasAppearance);
            if (e.HasAppearance)
                e.Appearance.Write(w);
        }

        // Events whose subject lives on this map (fall back to source).
        var mapEvents = new List<SimEvent>();
        foreach (var ev in events)
        {
            var subject = world.GetEntity(ev.TargetId) ?? world.GetEntity(ev.SourceId);
            if (subject is null || subject.MapId == mapId)
                mapEvents.Add(ev);
        }
        w.Write((ushort)mapEvents.Count);
        foreach (var ev in mapEvents)
        {
            w.Write((byte)ev.Kind);
            w.Write(ev.SourceId);
            w.Write(ev.TargetId);
            w.Write(ev.Value);
            w.Write(ev.AbilityId);
        }
        return ms.ToArray();
    }

    public static (int tick, ulong hash, int mapId, List<NetEntityState> entities, List<NetEvent> events) ReadSnapshot(BinaryReader r)
    {
        int tick = r.ReadInt32();
        ulong hash = r.ReadUInt64();
        int mapId = r.ReadInt32();

        int count = r.ReadUInt16();
        var entities = new List<NetEntityState>(count);
        for (int i = 0; i < count; i++)
        {
            // NB: field order must mirror WriteSnapshot exactly.
            var entity = new NetEntityState
            {
                Id = r.ReadInt32(),
                Kind = (EntityKind)r.ReadByte(),
                Facing = (Facing)r.ReadByte(),
                Name = r.ReadString(),
                ArchetypeId = r.ReadString(),
                X = r.ReadInt32(),
                Y = r.ReadInt32(),
                Hp = r.ReadInt32(),
                MaxHp = r.ReadInt32(),
                Mp = r.ReadInt32(),
                MaxMp = r.ReadInt32(),
                Atb = r.ReadUInt16(),
                Level = r.ReadByte(),
                Xp = r.ReadInt32(),
                Gil = r.ReadInt32(),
                Alive = r.ReadBoolean(),
            };
            entity.Vehicle = (VehicleKind)r.ReadByte();
            entity.HasAppearance = r.ReadBoolean();
            if (entity.HasAppearance)
                entity.Appearance = Appearance.Read(r);
            entities.Add(entity);
        }

        int evCount = r.ReadUInt16();
        var events = new List<NetEvent>(evCount);
        for (int i = 0; i < evCount; i++)
        {
            events.Add(new NetEvent(
                (SimEventKind)r.ReadByte(),
                r.ReadInt32(),
                r.ReadInt32(),
                r.ReadInt32(),
                r.ReadString()));
        }
        return (tick, hash, mapId, entities, events);
    }
}
