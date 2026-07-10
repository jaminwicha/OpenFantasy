using Godot;
using OpenFantasy.Net;
using OpenFantasy.Sim;

namespace OpenFantasy.Client;

/// <summary>
/// Headless smoke-test client (`--bot NAME`): joins, wanders, attacks the
/// nearest monster, says hello in chat, and logs what it observes. Used to
/// verify multiplayer sync without a display.
/// </summary>
public partial class BotClient : Node
{
    public string Account = "Bot";
    public string ClassId = "squire";
    public string Host = "127.0.0.1";
    public int Port = ServerNode.DefaultPort;
    public double LifetimeSeconds = 12;

    private ClientNode _client = null!;
    private double _clock;
    private double _inputTimer;
    private bool _greeted;
    private int _playersSeen;
    private int _chatsHeard;
    private int _damageEventsSeen;

    public override void _Ready()
    {
        _client = new ClientNode();
        AddChild(_client);
        _client.Connected += id => GD.Print($"[bot {Account}] welcomed as entity {id}");
        _client.Disconnected += reason => GD.Print($"[bot {Account}] disconnected: {reason}");
        _client.ChatReceived += (speaker, text) =>
        {
            _chatsHeard++;
            GD.Print($"[bot {Account}] chat <{speaker}> {text}");
        };
        _client.GameEvent += (kind, _, _, _, _) =>
        {
            if ((SimEventKind)kind == SimEventKind.Damage) _damageEventsSeen++;
        };
        // Deterministic per-name appearance so bots look distinct.
        ulong h = 1469598103934665603UL;
        foreach (char ch in Account) { h ^= ch; h *= 1099511628211UL; }
        var appearance = new Appearance
        {
            Race = (byte)((h >> 56) % 4),
            Skin = (byte)(h % 8), HairStyle = (byte)((h >> 8) % 12), HairColor = (byte)((h >> 16) % 16),
            Outfit = (byte)((h >> 24) % 10), OutfitA = (byte)((h >> 32) % 16), OutfitB = (byte)((h >> 40) % 16),
            Acc = (byte)((h >> 48) % 4),
        };
        var err = _client.Connect(Host, Port, Account, ClassId, appearance);
        if (err != Error.Ok)
        {
            GD.PrintErr($"[bot {Account}] connect failed: {err}");
            GetTree().Quit(1);
        }
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        if (_clock >= LifetimeSeconds)
        {
            int otherPlayers = _playersSeen;
            GD.Print($"[bot {Account}] RESULT playersSeen={otherPlayers} chats={_chatsHeard} damageEvents={_damageEventsSeen}");
            _client.Disconnect();
            GetTree().Quit(otherPlayers >= 2 && _chatsHeard >= 1 ? 0 : 3);
            return;
        }

        if (_client.LocalEntityId < 0) return;

        if (!_greeted && _clock > 2)
        {
            _greeted = true;
            _client.SendChat($"Hello from {Account}!");
        }

        _playersSeen = Math.Max(_playersSeen,
            _client.Entities.Values.Count(e => e.Kind == EntityKind.Player));

        _inputTimer += delta;
        if (_inputTimer < SimConstants.MsPerTick / 1000.0) return;
        _inputTimer = 0;

        if (!_client.Entities.TryGetValue(_client.LocalEntityId, out var me)) return;

        // Walk toward the nearest monster and mash ability 0.
        var mob = _client.Entities.Values
            .Where(e => e.Kind == EntityKind.Monster && e.Alive)
            .OrderBy(e => (long)(e.X - me.X) * (e.X - me.X) + (long)(e.Y - me.Y) * (e.Y - me.Y))
            .FirstOrDefault();
        if (mob == null)
        {
            _client.SendInput(1, 0, -1, -1);
            return;
        }
        _client.SendInput(
            (sbyte)Math.Sign(mob.X - me.X),
            (sbyte)Math.Sign(mob.Y - me.Y),
            0,
            mob.Id);
    }
}
