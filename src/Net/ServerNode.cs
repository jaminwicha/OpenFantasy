using Godot;
using OpenFantasy.Sim;

namespace OpenFantasy.Net;

/// <summary>
/// The authoritative game server: pumps ENet, applies client inputs to the
/// deterministic World at a fixed 20 Hz, broadcasts 10 Hz snapshots, and
/// persists characters. Runs headless (`--server`) or in-process for solo play.
/// </summary>
public partial class ServerNode : Node
{
    public const int DefaultPort = 24565;
    private const int SnapshotEveryTicks = 2;   // 10 Hz
    private const int AutosaveEveryTicks = 600; // 30 s
    private const int MaxClients = 32;

    private ENetMultiplayerPeer _peer = new();
    private World _world = null!;
    private SaveStore _saves = null!;
    private double _accumulator;
    private double _tickCostMs;
    private int _tickCostSamples;

    private sealed class Session
    {
        public string Account = "";
        public int EntityId = -1;
        public bool Ready;
    }

    private readonly Dictionary<int, Session> _sessions = new();

    public int Port { get; set; } = DefaultPort;
    public ulong Seed { get; set; }
    public bool LogToConsole { get; set; } = true;

    public override void _Ready()
    {
        string dataDir = ProjectSettings.GlobalizePath("res://data");
        GameData data;
        try
        {
            data = GameData.LoadFromDirectory(dataDir);
        }
        catch (DataValidationException e)
        {
            GD.PrintErr($"[server] content validation failed: {e.Message}");
            GetTree().Quit(2);
            return;
        }

        if (Seed == 0) Seed = GD.Randi() | ((ulong)GD.Randi() << 32);
        _world = new World(data, Seed);
        _saves = new SaveStore(ProjectSettings.GlobalizePath("user://save/characters.json"));

        var err = _peer.CreateServer(Port, MaxClients);
        if (err != Error.Ok)
        {
            GD.PrintErr($"[server] failed to bind port {Port}: {err}");
            GetTree().Quit(1);
            return;
        }
        _peer.PeerConnected += OnPeerConnected;
        _peer.PeerDisconnected += OnPeerDisconnected;
        Log($"[server] listening on :{Port} seed={Seed} entities={_world.EntityCount}");
    }

    public override void _Process(double delta)
    {
        _peer.Poll();
        DrainPackets();

        _accumulator += delta;
        double tickSeconds = SimConstants.MsPerTick / 1000.0;
        // Cap catch-up work so a long stall doesn't spiral.
        if (_accumulator > 0.5) _accumulator = 0.5;

        while (_accumulator >= tickSeconds)
        {
            _accumulator -= tickSeconds;
            ulong t0 = Time.GetTicksUsec();
            _world.Step();
            _tickCostMs += (Time.GetTicksUsec() - t0) / 1000.0;
            _tickCostSamples++;

            if (_world.Tick % SnapshotEveryTicks == 0)
                Broadcast(Protocol.WriteSnapshot(_world));
            if (_world.Tick % AutosaveEveryTicks == 0)
                SaveAll(flush: true);
            if (_world.Tick % 1200 == 0 && _tickCostSamples > 0)
            {
                Log($"[server] tick={_world.Tick} hash={_world.StateHash():x16} " +
                    $"players={_sessions.Count} entities={_world.EntityCount} " +
                    $"avgTickMs={_tickCostMs / _tickCostSamples:F2}");
                _tickCostMs = 0;
                _tickCostSamples = 0;
            }
        }
    }

    public override void _ExitTree()
    {
        SaveAll(flush: true);
        _peer.Close();
    }

    // ------------------------------------------------------------- networking

    private void DrainPackets()
    {
        while (_peer.GetAvailablePacketCount() > 0)
        {
            int sender = _peer.GetPacketPeer();
            byte[] packet = _peer.GetPacket();
            try
            {
                HandlePacket(sender, packet);
            }
            catch (Exception e)
            {
                Log($"[server] bad packet from peer {sender}: {e.Message}; disconnecting");
                _peer.DisconnectPeer(sender);
            }
        }
    }

    private void HandlePacket(int sender, byte[] packet)
    {
        using var r = new BinaryReader(new MemoryStream(packet));
        var op = (Op)r.ReadByte();
        _sessions.TryGetValue(sender, out var session);

        switch (op)
        {
            case Op.Hello:
            {
                var (protocol, account, classId) = Protocol.ReadHello(r);
                HandleHello(sender, protocol, account, classId);
                break;
            }
            case Op.Input when session is { Ready: true }:
            {
                var input = Protocol.ReadInput(r);
                input.TargetId = SanitizeTarget(input.TargetId);
                _world.SetPlayerInput(session.EntityId, input);
                break;
            }
            case Op.Chat when session is { Ready: true }:
            {
                var (_, text) = Protocol.ReadChat(r);
                text = text.Trim();
                if (text.Length is > 0 and <= 200)
                    Broadcast(Protocol.WriteChat(SessionName(session), text));
                break;
            }
        }
    }

    private void HandleHello(int sender, int protocol, string account, string classId)
    {
        string? reason = null;
        account = account.Trim();
        if (protocol != SimConstants.ProtocolVersion)
            reason = $"protocol mismatch (server {SimConstants.ProtocolVersion}, client {protocol})";
        else if (account.Length is < 2 or > 16)
            reason = "name must be 2–16 characters";
        else if (!_world.Data.Classes.ContainsKey(classId))
            reason = $"unknown class '{classId}'";
        else if (_sessions.Values.Any(s => s.Ready && string.Equals(s.Account, account, StringComparison.OrdinalIgnoreCase)))
            reason = $"'{account}' is already online";

        if (reason != null)
        {
            SendTo(sender, Protocol.WriteReject(reason));
            _peer.DisconnectPeer(sender);
            return;
        }

        var save = _saves.Get(account);
        if (save != null && save.ClassId != classId)
            save = null; // class change = fresh character (slice rule)

        var entity = _world.AddPlayer(account, classId, save);
        _sessions[sender] = new Session { Account = account, EntityId = entity.Id, Ready = true };
        SendTo(sender, Protocol.WriteWelcome(entity.Id, _world.Tick));
        Broadcast(Protocol.WriteChat("*", $"{account} entered Verdance March"));
        Log($"[server] {account} joined as {classId} (entity {entity.Id}, level {entity.Level})");
    }

    private void OnPeerConnected(long id)
    {
        _sessions[(int)id] = new Session();
    }

    private void OnPeerDisconnected(long id)
    {
        if (_sessions.Remove((int)id, out var session) && session.Ready)
        {
            var entity = _world.GetEntity(session.EntityId);
            if (entity != null)
            {
                _saves.Put(session.Account, _world.SnapshotSave(entity));
                _saves.Flush();
                _world.RemovePlayer(session.EntityId);
            }
            Broadcast(Protocol.WriteChat("*", $"{session.Account} left the world"));
            Log($"[server] {session.Account} disconnected");
        }
    }

    private int SanitizeTarget(int targetId)
    {
        var target = _world.GetEntity(targetId);
        return target is { IsMonster: true } ? targetId : -1;
    }

    private string SessionName(Session s)
    {
        var e = _world.GetEntity(s.EntityId);
        return e?.Name ?? s.Account;
    }

    private void SaveAll(bool flush)
    {
        foreach (var session in _sessions.Values)
        {
            if (!session.Ready) continue;
            var entity = _world.GetEntity(session.EntityId);
            if (entity != null)
                _saves.Put(session.Account, _world.SnapshotSave(entity));
        }
        if (flush) _saves.Flush();
    }

    private void SendTo(int peerId, byte[] packet)
    {
        _peer.SetTargetPeer(peerId);
        _peer.PutPacket(packet);
    }

    private void Broadcast(byte[] packet)
    {
        if (_sessions.Count == 0) return;
        _peer.SetTargetPeer(0);
        _peer.PutPacket(packet);
    }

    private void Log(string message)
    {
        if (LogToConsole) GD.Print(message);
    }
}
