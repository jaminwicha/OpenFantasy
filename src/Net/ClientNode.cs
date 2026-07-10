using Godot;
using OpenFantasy.Sim;

namespace OpenFantasy.Net;

/// <summary>
/// Client-side connection: connects, handshakes, sends inputs, and exposes
/// the latest snapshot state + events to the presentation layer via signals
/// and the <see cref="Entities"/> dictionary. Never simulates.
/// </summary>
public partial class ClientNode : Node
{
    [Signal] public delegate void ConnectedEventHandler(int localEntityId);
    [Signal] public delegate void DisconnectedEventHandler(string reason);
    [Signal] public delegate void ChatReceivedEventHandler(string speaker, string text);
    [Signal] public delegate void SnapshotAppliedEventHandler(int tick);
    [Signal] public delegate void GameEventEventHandler(int kind, int sourceId, int targetId, int value, string abilityId);
    [Signal] public delegate void PlayerStateChangedEventHandler();
    [Signal] public delegate void NpcInfoReceivedEventHandler();

    public int LocalEntityId { get; private set; } = -1;
    public int ServerTick { get; private set; }
    public readonly Dictionary<int, NetEntityState> Entities = new();

    /// <summary>Latest owner-only state (inventory/equipment/quests/party).</summary>
    public Protocol.PlayerStateMsg PlayerState { get; private set; } = new();
    /// <summary>Latest NPC interaction payload (valid after NpcInfoReceived).</summary>
    public Protocol.NpcInfoMsg? NpcInfo { get; private set; }

    private ENetMultiplayerPeer _peer = new();
    private string _account = "";
    private string _classId = "";
    private Appearance _appearance = Appearance.Default;
    private bool _helloSent;
    private bool _wasConnected;
    private int _inputSeq;

    public Error Connect(string host, int port, string account, string classId, Appearance appearance = default)
    {
        _account = account;
        _classId = classId;
        _appearance = appearance;
        _helloSent = false;
        _wasConnected = false;
        LocalEntityId = -1;
        Entities.Clear();
        _peer = new ENetMultiplayerPeer();
        return _peer.CreateClient(host, port);
    }

    public void Disconnect()
    {
        _peer.Close();
        LocalEntityId = -1;
        Entities.Clear();
    }

    public override void _Process(double delta)
    {
        if (_peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Disconnected)
        {
            if (_wasConnected)
            {
                _wasConnected = false;
                EmitSignal(SignalName.Disconnected, "connection lost");
            }
            return;
        }

        _peer.Poll();

        if (!_helloSent && _peer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected)
        {
            _helloSent = true;
            _wasConnected = true;
            _peer.PutPacket(Protocol.WriteHello(_account, _classId, _appearance));
        }

        while (_peer.GetAvailablePacketCount() > 0)
        {
            byte[] packet = _peer.GetPacket();
            try
            {
                HandlePacket(packet);
            }
            catch (Exception e)
            {
                GD.PrintErr($"[client] bad packet: {e.Message}");
            }
        }
    }

    public void SendInput(sbyte moveX, sbyte moveY, sbyte abilitySlot, int targetId)
    {
        if (LocalEntityId < 0) return;
        _peer.PutPacket(Protocol.WriteInput(new InputCommand
        {
            Seq = _inputSeq++,
            MoveX = moveX,
            MoveY = moveY,
            AbilitySlot = abilitySlot,
            TargetId = targetId,
        }));
    }

    public void SendChat(string text)
    {
        if (LocalEntityId < 0 || text.Length == 0) return;
        _peer.PutPacket(Protocol.WriteChat(_account, text));
    }

    private void HandlePacket(byte[] packet)
    {
        using var r = new BinaryReader(new MemoryStream(packet));
        var op = (Op)r.ReadByte();
        switch (op)
        {
            case Op.Welcome:
            {
                var (entityId, tick) = Protocol.ReadWelcome(r);
                LocalEntityId = entityId;
                ServerTick = tick;
                EmitSignal(SignalName.Connected, entityId);
                break;
            }
            case Op.Reject:
            {
                string reason = r.ReadString();
                _wasConnected = false;
                _peer.Close();
                EmitSignal(SignalName.Disconnected, reason);
                break;
            }
            case Op.Snapshot:
            {
                var (tick, _, entities, events) = Protocol.ReadSnapshot(r);
                if (tick < ServerTick) break; // stale
                ServerTick = tick;
                Entities.Clear();
                foreach (var e in entities)
                    Entities[e.Id] = e;
                EmitSignal(SignalName.SnapshotApplied, tick);
                foreach (var ev in events)
                    EmitSignal(SignalName.GameEvent, (int)ev.Kind, ev.SourceId, ev.TargetId, ev.Value, ev.AbilityId);
                break;
            }
            case Op.Chat:
            {
                var (speaker, text) = Protocol.ReadChat(r);
                EmitSignal(SignalName.ChatReceived, speaker, text);
                break;
            }
            case Op.PlayerState:
            {
                PlayerState = Protocol.ReadPlayerState(r);
                EmitSignal(SignalName.PlayerStateChanged);
                break;
            }
            case Op.NpcInfo:
            {
                NpcInfo = Protocol.ReadNpcInfo(r);
                EmitSignal(SignalName.NpcInfoReceived);
                break;
            }
        }
    }

    public void SendInteract(int npcEntityId)
    {
        if (LocalEntityId < 0) return;
        _peer.PutPacket(Protocol.WriteInteract(npcEntityId));
    }

    public void SendItemOp(ItemOpKind op, string itemId, int npcEntityId = -1)
    {
        if (LocalEntityId < 0) return;
        _peer.PutPacket(Protocol.WriteItemOp(op, itemId, npcEntityId));
    }

    public void SendQuestOp(QuestOpKind op, string questId, int npcEntityId)
    {
        if (LocalEntityId < 0) return;
        _peer.PutPacket(Protocol.WriteQuestOp(op, questId, npcEntityId));
    }

    public void SendCraft(string recipeId)
    {
        if (LocalEntityId < 0) return;
        _peer.PutPacket(Protocol.WriteCraftOp(recipeId));
    }

    public void SendVehicleToggle()
    {
        if (LocalEntityId < 0) return;
        _peer.PutPacket(Protocol.WriteVehicleOp());
    }
}
