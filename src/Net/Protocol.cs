using OpenFantasy.Sim;

namespace OpenFantasy.Net;

public enum Op : byte
{
    Hello = 1,      // C→S: protocol, account name, class id
    Welcome = 2,    // S→C: your entity id, current tick
    Reject = 3,     // S→C: reason string, then disconnect
    Input = 4,      // C→S: InputCommand
    Snapshot = 5,   // S→C: tick, hash, entities, events
    Chat = 6,       // C→S: text · S→C: speaker name + text
}

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
    public static byte[] WriteHello(string account, string classId)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((byte)Op.Hello);
        w.Write(SimConstants.ProtocolVersion);
        w.Write(account);
        w.Write(classId);
        return ms.ToArray();
    }

    public static (int protocol, string account, string classId) ReadHello(BinaryReader r)
        => (r.ReadInt32(), r.ReadString(), r.ReadString());

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

    public static byte[] WriteSnapshot(World world)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((byte)Op.Snapshot);
        w.Write(world.Tick);
        w.Write(world.StateHash());

        w.Write((ushort)world.EntityCount);
        foreach (var e in world.EntitiesInOrder())
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
        }

        w.Write((ushort)world.Events.Count);
        foreach (var ev in world.Events)
        {
            w.Write((byte)ev.Kind);
            w.Write(ev.SourceId);
            w.Write(ev.TargetId);
            w.Write(ev.Value);
            w.Write(ev.AbilityId);
        }
        return ms.ToArray();
    }

    public static (int tick, ulong hash, List<NetEntityState> entities, List<NetEvent> events) ReadSnapshot(BinaryReader r)
    {
        int tick = r.ReadInt32();
        ulong hash = r.ReadUInt64();

        int count = r.ReadUInt16();
        var entities = new List<NetEntityState>(count);
        for (int i = 0; i < count; i++)
        {
            entities.Add(new NetEntityState
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
            });
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
        return (tick, hash, entities, events);
    }
}
