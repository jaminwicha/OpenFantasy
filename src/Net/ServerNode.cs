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
    private readonly List<SimEvent> _pendingEvents = new();

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

            _pendingEvents.AddRange(_world.Events);

            if (_world.Tick % SnapshotEveryTicks == 0)
            {
                Broadcast(Protocol.WriteSnapshot(_world, _pendingEvents));
                PushDirtyPlayerStates(_pendingEvents);
                _pendingEvents.Clear();
            }
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
                var (protocol, account, classId, appearance) = Protocol.ReadHello(r);
                HandleHello(sender, protocol, account, classId, appearance);
                break;
            }
            case Op.CraftOp when session is { Ready: true }:
            {
                string recipeId = r.ReadString();
                if (_world.GetEntity(session.EntityId) is not { } player) break;
                SendTo(sender, Protocol.WriteChat("*", _world.CraftItem(player, recipeId)));
                SendTo(sender, Protocol.WritePlayerState(_world, player));
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
                if (text.Length is 0 or > 200) break;
                if (text.StartsWith('/'))
                    HandleChatCommand(sender, session, text);
                else
                    Broadcast(Protocol.WriteChat(SessionName(session), text));
                break;
            }
            case Op.Interact when session is { Ready: true }:
            {
                int npcEntityId = r.ReadInt32();
                HandleInteract(sender, session, npcEntityId);
                break;
            }
            case Op.ItemOp when session is { Ready: true }:
            {
                var (itemOp, itemId, npcEntityId) = Protocol.ReadItemOp(r);
                if (_world.GetEntity(session.EntityId) is not { } player) break;
                string result = itemOp switch
                {
                    ItemOpKind.Buy => _world.BuyItem(player, npcEntityId, itemId),
                    ItemOpKind.Sell => _world.SellItem(player, npcEntityId, itemId),
                    ItemOpKind.Use => _world.UseItem(player, itemId),
                    ItemOpKind.Equip => _world.EquipItem(player, itemId),
                    ItemOpKind.Unequip => _world.UnequipItem(player, itemId),
                    _ => "Unknown action.",
                };
                SendTo(sender, Protocol.WriteChat("*", result));
                SendTo(sender, Protocol.WritePlayerState(_world, player));
                break;
            }
            case Op.QuestOp when session is { Ready: true }:
            {
                var (questOp, questId, npcEntityId) = Protocol.ReadQuestOp(r);
                if (_world.GetEntity(session.EntityId) is not { } player) break;
                string result = questOp switch
                {
                    QuestOpKind.Accept => _world.AcceptQuest(player, npcEntityId, questId),
                    QuestOpKind.Complete => _world.CompleteQuest(player, npcEntityId, questId),
                    _ => "Unknown action.",
                };
                SendTo(sender, Protocol.WriteChat("*", result));
                SendTo(sender, Protocol.WritePlayerState(_world, player));
                break;
            }
        }
    }

    private void HandleChatCommand(int sender, Session session, string text)
    {
        if (_world.GetEntity(session.EntityId) is not { } player) return;

        string[] parts = text.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        switch (parts[0].ToLowerInvariant())
        {
            case "/party" when parts.Length >= 2 && parts[1].Equals("invite", StringComparison.OrdinalIgnoreCase) && parts.Length >= 3:
            {
                string result = _world.PartyInvite(player, parts[2].Trim());
                SendTo(sender, Protocol.WriteChat("*", result));
                // Tell the invitee too.
                var invitee = FindSessionByName(parts[2].Trim());
                if (invitee is { } inv && result.StartsWith("Invited"))
                    SendTo(inv.peerId, Protocol.WriteChat("*", $"{player.Name} invites you to a party. Type /party accept"));
                break;
            }
            case "/party" when parts.Length >= 2 && parts[1].Equals("accept", StringComparison.OrdinalIgnoreCase):
            {
                var (result, inviter) = _world.PartyAccept(player);
                SendTo(sender, Protocol.WriteChat("*", result));
                if (inviter != null)
                    PartyNotify(player.PartyId, $"{player.Name} joined the party.");
                PushPartyState(player.PartyId);
                break;
            }
            case "/party" when parts.Length >= 2 && parts[1].Equals("leave", StringComparison.OrdinalIgnoreCase):
            {
                int oldParty = player.PartyId;
                SendTo(sender, Protocol.WriteChat("*", _world.PartyLeave(player)));
                if (oldParty >= 0)
                {
                    PartyNotify(oldParty, $"{player.Name} left the party.");
                    PushPartyState(oldParty);
                }
                SendTo(sender, Protocol.WritePlayerState(_world, player));
                break;
            }
            case "/p" when parts.Length >= 2:
            {
                if (player.PartyId < 0)
                {
                    SendTo(sender, Protocol.WriteChat("*", "You are not in a party."));
                    break;
                }
                string msg = text[3..].Trim();
                foreach (int memberId in _world.PartyMembers(player.PartyId))
                    if (FindSessionByEntity(memberId) is { } member)
                        SendTo(member.peerId, Protocol.WriteChat($"[P] {player.Name}", msg));
                break;
            }
            default:
                SendTo(sender, Protocol.WriteChat("*", "Commands: /party invite <name> · /party accept · /party leave · /p <msg>"));
                break;
        }
    }

    private void HandleInteract(int sender, Session session, int npcEntityId)
    {
        if (_world.GetEntity(session.EntityId) is not { } player) return;

        // Interacting with a resource node = one gathering swing.
        if (_world.GetEntity(npcEntityId) is { IsNode: true })
        {
            string result = _world.Gather(player, npcEntityId);
            if (result.Length > 0)
                SendTo(sender, Protocol.WriteChat("*", result));
            SendTo(sender, Protocol.WritePlayerState(_world, player));
            return;
        }

        var npc = _world.NpcInRange(player, npcEntityId);
        if (npc is null) return;

        var quests = new List<(string, string, string, NpcQuestStatus)>();
        foreach (string questId in npc.Quests)
        {
            var quest = _world.Data.Quests[questId];
            NpcQuestStatus status;
            string questText;
            if (player.QuestLog.TryGetValue(questId, out var progress))
            {
                if (progress.State == QuestState.Completed)
                {
                    status = NpcQuestStatus.Done;
                    questText = "Done.";
                }
                else if (progress.Kills >= quest.TargetCount)
                {
                    status = NpcQuestStatus.ReadyToTurnIn;
                    questText = quest.TurnIn;
                }
                else
                {
                    status = NpcQuestStatus.Active;
                    questText = $"{progress.Kills}/{quest.TargetCount} {_world.Data.Monsters[quest.TargetMonster].Name}s";
                }
            }
            else if (player.Level >= quest.MinLevel)
            {
                status = NpcQuestStatus.Offerable;
                questText = quest.Offer;
            }
            else
            {
                status = NpcQuestStatus.Locked;
                questText = $"(Requires level {quest.MinLevel})";
            }
            quests.Add((questId, quest.Name, questText, status));
        }
        SendTo(sender, Protocol.WriteNpcInfo(npcEntityId, npc, quests));
    }

    private (int peerId, Session session)? FindSessionByName(string name)
    {
        foreach (var (peerId, s) in _sessions)
            if (s.Ready && string.Equals(s.Account, name, StringComparison.OrdinalIgnoreCase))
                return (peerId, s);
        return null;
    }

    private (int peerId, Session session)? FindSessionByEntity(int entityId)
    {
        foreach (var (peerId, s) in _sessions)
            if (s.Ready && s.EntityId == entityId)
                return (peerId, s);
        return null;
    }

    private void PartyNotify(int partyId, string message)
    {
        if (partyId < 0) return;
        foreach (int memberId in _world.PartyMembers(partyId))
            if (FindSessionByEntity(memberId) is { } member)
                SendTo(member.peerId, Protocol.WriteChat("*", message));
    }

    /// <summary>Push PlayerState to players whose inventory/quests changed this snapshot window.</summary>
    private void PushDirtyPlayerStates(List<SimEvent> events)
    {
        if (events.Count == 0) return;
        var dirty = new HashSet<int>();
        foreach (var ev in events)
            if (ev.Kind is SimEventKind.Loot or SimEventKind.QuestProgress or SimEventKind.LevelUp)
                dirty.Add(ev.TargetId);
        foreach (int entityId in dirty)
            if (FindSessionByEntity(entityId) is { } member && _world.GetEntity(entityId) is { } entity)
                SendTo(member.peerId, Protocol.WritePlayerState(_world, entity));
    }

    /// <summary>Refresh PlayerState for every member of a party (membership changed).</summary>
    private void PushPartyState(int partyId)
    {
        if (partyId < 0) return;
        foreach (int memberId in _world.PartyMembers(partyId))
            if (FindSessionByEntity(memberId) is { } member && _world.GetEntity(memberId) is { } entity)
                SendTo(member.peerId, Protocol.WritePlayerState(_world, entity));
    }

    private void HandleHello(int sender, int protocol, string account, string classId, Appearance appearance)
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

        var entity = _world.AddPlayer(account, classId, save, appearance);
        _sessions[sender] = new Session { Account = account, EntityId = entity.Id, Ready = true };
        SendTo(sender, Protocol.WriteWelcome(entity.Id, _world.Tick));
        SendTo(sender, Protocol.WritePlayerState(_world, entity));
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
