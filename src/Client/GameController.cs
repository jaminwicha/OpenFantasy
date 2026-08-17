using Godot;
using OpenFantasy.Net;
using OpenFantasy.Sim;

namespace OpenFantasy.Client;

/// <summary>
/// The in-world scene: owns the client connection (and the local server in
/// solo mode), builds the world renderer, syncs EntityViews to snapshots,
/// captures input, and drives the HUD. Thin glue — no gameplay decisions.
/// </summary>
public partial class GameController : Node2D
{
    public string Account = "";
    public string ClassId = "";
    public string Host = "127.0.0.1";
    public int Port = ServerNode.DefaultPort;
    public bool HostLocalServer;
    public Appearance Appearance = Appearance.Default;

    private ClientNode _client = null!;
    private Hud _hud = null!;
    private Camera2D _camera = null!;
    private Node2D _entityLayer = null!;
    private Node2D _fxLayer = null!;
    private GameData _data = null!;

    private readonly Dictionary<int, EntityView> _views = new();
    private GameWindows _windows = null!;
    private CanvasModulate _dayNight = null!;
    private WorldRenderer _renderer = null!;
    private Minimap _minimap = null!;
    private int _targetId = -1;
    private sbyte _pendingAbility = -1;
    private double _inputTimer;

    public override void _Ready()
    {
        _data = GameData.LoadFromDirectory(ProjectSettings.GlobalizePath("res://data"));
        SpriteComposer.Init(_data.Appearance);

        if (HostLocalServer)
        {
            AddChild(new ServerNode { Port = Port, LogToConsole = false });
        }

        _renderer = new WorldRenderer();
        _renderer.Build(_data.Map);
        AddChild(_renderer);

        _entityLayer = new Node2D { YSortEnabled = false };
        AddChild(_entityLayer);
        _fxLayer = new Node2D { ZIndex = 4000 };
        AddChild(_fxLayer);

        _camera = new Camera2D { Zoom = new Vector2(3, 3), PositionSmoothingEnabled = true, PositionSmoothingSpeed = 8 };
        AddChild(_camera);
        _camera.MakeCurrent();
        var spawn = WorldRenderer.UnitsToPixels(
            _data.Map.PlayerSpawnX * SimConstants.UnitsPerTile,
            _data.Map.PlayerSpawnY * SimConstants.UnitsPerTile);
        _camera.Position = spawn;

        _dayNight = new CanvasModulate { Color = Colors.White };
        AddChild(_dayNight);

        _hud = new Hud();
        AddChild(_hud);
        _hud.Setup(_data, ClassId);
        _hud.ChatSubmitted += text => _client.SendChat(text);

        _client = new ClientNode();
        AddChild(_client);
        _client.ChatReceived += (speaker, text) => _hud.AddChatLine(speaker, text);
        _client.Disconnected += OnDisconnected;
        _client.GameEvent += OnGameEvent;
        _client.SnapshotApplied += _ => SyncViews();
        _client.PlayerStateChanged += () => _hud.UpdateParty(_client);
        _client.MapChanged += OnMapChanged;
        _client.Connected += _ =>
        {
            // Frontier chunks generate locally from the server's seed (D5);
            // TileAt on the client map now agrees with the sim everywhere.
            var frontier = new FrontierGen(_data.Map, _client.WorldSeed);
            _data.Map.SetFrontierSource(frontier.GenerateChunk);
        };

        _windows = new GameWindows();
        AddChild(_windows);
        _windows.Setup(_client, _data);

        _minimap = new Minimap();
        AddChild(_minimap);
        _minimap.Setup(_client, _data.Map);

        // Solo: give the in-process server a beat to bind before connecting.
        GetTree().CreateTimer(HostLocalServer ? 0.25 : 0.0).Timeout += () =>
        {
            var err = _client.Connect(Host, Port, Account, ClassId, Appearance);
            if (err != Error.Ok)
                OnDisconnected($"could not open connection: {err}");
        };
        _hud.AddChatLine("*", HostLocalServer ? "Starting local world…" : $"Connecting to {Host}:{Port}…");
    }

    // ------------------------------------------------------------------ input

    public override void _Process(double delta)
    {
        if (_client.LocalEntityId < 0 || _hud.ChatFocused)
        {
            if (!_hud.ChatFocused) return;
        }

        if (!_hud.ChatFocused)
        {
            if (Input.IsActionJustPressed("ability_1")) _pendingAbility = 0;
            else if (Input.IsActionJustPressed("ability_2")) _pendingAbility = 1;
            else if (Input.IsActionJustPressed("ability_3")) _pendingAbility = 2;
            else if (Input.IsActionJustPressed("ability_4")) _pendingAbility = 3;

            if (Input.IsActionJustPressed("cycle_target")) CycleTarget();
            if (Input.IsActionJustPressed("chat_toggle")) _hud.FocusChat();
            if (Input.IsActionJustPressed("toggle_inventory")) _windows.ToggleInventory();
            if (Input.IsActionJustPressed("toggle_quests")) _windows.ToggleQuests();
            if (Input.IsActionJustPressed("toggle_craft")) _windows.ToggleCraft();
            if (Input.IsActionJustPressed("toggle_vehicle")) _client.SendVehicleToggle();
            if (Input.IsActionJustPressed("toggle_map")) _minimap.Toggle();
            if (Input.IsActionJustPressed("toggle_character")) _windows.ToggleCharacter();
            if (Input.IsActionJustPressed("interact")) TryInteract();
            if (Input.IsActionJustPressed("ui_cancel")) { _windows.CloseAll(); _minimap.Close(); }
        }

        UpdateDayNight();

        // Send input at the sim rate, not the render rate.
        _inputTimer += delta;
        if (_inputTimer >= SimConstants.MsPerTick / 1000.0)
        {
            _inputTimer = 0;
            SendInput();
        }
    }

    private void SendInput()
    {
        if (_client.LocalEntityId < 0) return;

        sbyte mx = 0, my = 0;
        if (!_hud.ChatFocused)
        {
            if (Input.IsActionPressed("move_left")) mx -= 1;
            if (Input.IsActionPressed("move_right")) mx += 1;
            if (Input.IsActionPressed("move_up")) my -= 1;
            if (Input.IsActionPressed("move_down")) my += 1;
        }

        _client.SendInput(mx, my, _pendingAbility, _targetId);
        _pendingAbility = -1;
    }

    private void TryInteract()
    {
        if (!_client.Entities.TryGetValue(_client.LocalEntityId, out var me)) return;
        var target = _client.Entities.Values
            .Where(e => e.Kind is EntityKind.Npc or EntityKind.Node && e.Alive)
            .OrderBy(e => (long)(e.X - me.X) * (e.X - me.X) + (long)(e.Y - me.Y) * (e.Y - me.Y))
            .FirstOrDefault();
        if (target != null)
        {
            long dx = target.X - me.X, dy = target.Y - me.Y;
            long r = SimConstants.InteractRange;
            if (dx * dx + dy * dy <= r * r)
            {
                _client.SendInteract(target.Id);
                return;
            }
        }
        // No NPC or node in reach: use the surroundings (delve entrances,
        // stairs, chests, suspicious walls). The server decides what's here.
        _client.SendSiteOp();
    }

    private void OnMapChanged(string label)
    {
        _renderer.SetInstance(_client.CurrentMap);
        // Views for the old map's entities die on the next SyncViews; clear
        // now so nothing flashes at stale positions.
        foreach (var view in _views.Values) view.QueueFree();
        _views.Clear();
        _targetId = -1;
        if (label.Length > 0) _hud.AddChatLine("*", label);
        _minimap.Refresh();
    }

    /// <summary>Deterministic dawn/day/dusk/night tint from the server tick.</summary>
    private void UpdateDayNight()
    {
        if (_client.CurrentMapId != 0)
        {
            // Underground: steady torch-lit gloom, no sky.
            _dayNight.Color = _dayNight.Color.Lerp(new Color(0.66f, 0.62f, 0.72f), 0.05f);
            return;
        }
        int permille = (_client.ServerTick % SimConstants.TicksPerDay) * 1000 / SimConstants.TicksPerDay;
        // 0 dawn → 250 day → 500 dusk → 750 night → 1000 dawn
        Color day = Colors.White;
        Color dawn = new(1.0f, 0.85f, 0.75f);
        Color dusk = new(0.9f, 0.7f, 0.65f);
        Color night = new(0.45f, 0.5f, 0.72f);
        Color tint = permille switch
        {
            < 125 => dawn.Lerp(day, permille / 125f),
            < 500 => day,
            < 625 => day.Lerp(dusk, (permille - 500) / 125f),
            < 750 => dusk.Lerp(night, (permille - 625) / 125f),
            < 875 => night,
            _ => night.Lerp(dawn, (permille - 875) / 125f),
        };
        _dayNight.Color = _dayNight.Color.Lerp(tint, 0.02f);
    }

    private void CycleTarget()
    {
        if (!_client.Entities.TryGetValue(_client.LocalEntityId, out var me)) return;

        var monsters = _client.Entities.Values
            .Where(e => e.Kind == EntityKind.Monster && e.Alive)
            .OrderBy(e => (long)(e.X - me.X) * (e.X - me.X) + (long)(e.Y - me.Y) * (e.Y - me.Y))
            .Take(8)
            .ToList();
        if (monsters.Count == 0)
        {
            _targetId = -1;
            return;
        }
        int idx = monsters.FindIndex(e => e.Id == _targetId);
        _targetId = monsters[(idx + 1) % monsters.Count].Id;
    }

    // ------------------------------------------------------------------ sync

    private void SyncViews()
    {
        foreach (var (id, state) in _client.Entities)
        {
            if (!_views.TryGetValue(id, out var view))
            {
                view = new EntityView();
                _entityLayer.AddChild(view);
                view.Setup(state, id == _client.LocalEntityId);
                _views[id] = view;
            }
            else
            {
                view.ApplyState(state);
            }
        }

        var gone = _views.Keys.Where(id => !_client.Entities.ContainsKey(id)).ToList();
        foreach (int id in gone)
        {
            _views[id].QueueFree();
            _views.Remove(id);
        }

        if (_targetId >= 0 &&
            (!_client.Entities.TryGetValue(_targetId, out var target) || !target.Alive))
            _targetId = -1;

        foreach (var (id, view) in _views)
            view.SetTargeted(id == _targetId);

        if (_client.Entities.TryGetValue(_client.LocalEntityId, out var me))
        {
            _hud.UpdateLocal(me);
            _camera.Position = WorldRenderer.UnitsToPixels(me.X, me.Y);
            if (_client.CurrentMapId == 0)
                _renderer.StreamFrontierAround(SimConstants.TileOf(me.X), SimConstants.TileOf(me.Y));
        }
        if (_client.PlayerState.PartyMemberIds.Count > 0)
            _hud.UpdateParty(_client); // live member HP
        _hud.UpdateTarget(_targetId >= 0 ? _client.Entities.GetValueOrDefault(_targetId) : null);
        _minimap.Refresh();
    }

    // ------------------------------------------------------------------ events

    private void OnGameEvent(int kind, int sourceId, int targetId, int value, string abilityId)
    {
        var simKind = (SimEventKind)kind;
        switch (simKind)
        {
            case SimEventKind.Damage:
                SpawnFloatingText(targetId, value.ToString(), new Color("ffffff"));
                break;
            case SimEventKind.Heal:
                SpawnFloatingText(targetId, value.ToString(), new Color("70e878"));
                break;
            case SimEventKind.LevelUp:
                SpawnFloatingText(targetId, $"LEVEL {value}!", new Color("f8e050"));
                if (targetId == _client.LocalEntityId && _views.TryGetValue(targetId, out var v))
                    _hud.AddChatLine("*", $"You reached level {value}!");
                break;
            case SimEventKind.Death when targetId == _client.LocalEntityId:
                _hud.AddChatLine("*", "You have fallen. The crystal calls you home…");
                break;
            case SimEventKind.Loot when targetId == _client.LocalEntityId:
                _hud.AddChatLine("*", $"Looted: {_data.Items[abilityId].Name}");
                SpawnFloatingText(targetId, _data.Items[abilityId].Name, new Color("a0e0ff"));
                break;
            case SimEventKind.QuestProgress when targetId == _client.LocalEntityId:
            {
                var quest = _data.Quests[abilityId];
                string turnInName = _data.Npcs[quest.Kind == QuestKind.Talk ? quest.TalkNpc : quest.Giver].Name;
                if (value < 0)
                    _hud.AddChatLine("*", $"Quest complete: {quest.Name}!");
                else if (value == 0)
                    _hud.AddChatLine("*", $"Quest accepted: {quest.Name}");
                else if (value >= quest.TargetCount)
                    _hud.AddChatLine("*", $"{quest.Name}: done! Return to {turnInName}.");
                else
                    _hud.AddChatLine("*", $"{quest.Name}: {value}/{quest.TargetCount}");
                break;
            }
        }
    }

    private void SpawnFloatingText(int entityId, string text, Color color)
    {
        if (!_views.TryGetValue(entityId, out var view)) return;

        var label = UiTheme.MakeLabel(text, 10);
        label.Modulate = color;
        label.Position = view.Position + new Vector2(-14, -30);
        label.ZIndex = 4000;
        _fxLayer.AddChild(label);

        var tween = CreateTween();
        tween.SetParallel();
        tween.TweenProperty(label, "position:y", label.Position.Y - 18, 0.8);
        tween.TweenProperty(label, "modulate:a", 0.0, 0.8).SetEase(Tween.EaseType.In);
        tween.Chain().TweenCallback(Callable.From(label.QueueFree));
    }

    private void OnDisconnected(string reason)
    {
        _hud.AddChatLine("*", $"Disconnected: {reason}");
        // Back to the menu after a beat so the message is readable.
        GetTree().CreateTimer(2.0).Timeout += () =>
        {
            GetTree().ChangeSceneToFile("res://scenes/Boot.tscn");
        };
    }
}
