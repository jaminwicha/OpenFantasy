using Godot;
using OpenFantasy.Net;
using OpenFantasy.Sim;

namespace OpenFantasy.Client;

/// <summary>
/// Entry point. Headless dispatch (`--server`, `--selftest`) or the main
/// menu: name + class + host, then Solo (in-process server) or Join.
/// </summary>
public partial class Boot : Control
{
    private LineEdit _nameEdit = null!;
    private OptionButton _classPicker = null!;
    private LineEdit _hostEdit = null!;
    private Label _status = null!;
    private GameData? _data;

    // Character creator state.
    private Appearance _appearance = Appearance.Default;
    private TextureRect _preview = null!;
    private Label _presetLabel = null!;
    private readonly List<Label> _optionValueLabels = new();
    private float _previewClock;
    private int _previewDir;
    private ulong _randState = 12345;

    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();

        if (args.Contains("--selftest"))
        {
            bool ok = SelfTest.RunAll(ProjectSettings.GlobalizePath("res://data"), GD.Print);
            GetTree().Quit(ok ? 0 : 1);
            return;
        }

        int genArg = Array.IndexOf(args, "--previewgen");
        if (genArg >= 0)
        {
            ulong seed = genArg + 1 < args.Length && ulong.TryParse(args[genArg + 1], out ulong s)
                ? s : 0x5F3759DFUL;
            string path = ProjectSettings.GlobalizePath("res://docs/screenshots/worldgen-preview.png");
            WorldGenPreview.SaveBiomeMap(seed, path);
            GD.Print($"[previewgen] seed {seed}: biome map saved to {path}");
            GetTree().Quit(0);
            return;
        }

        if (args.Contains("--server"))
        {
            var server = new ServerNode { Port = ReadPortArg(args) };
            GetTree().Root.CallDeferred(Node.MethodName.AddChild, server);
            return;
        }

        // --auto NAME: skip the menu, play solo, screenshot to user://auto_screenshot.png, quit.
        int autoArg = Array.IndexOf(args, "--auto");
        if (autoArg >= 0)
        {
            string autoName = autoArg + 1 < args.Length ? args[autoArg + 1] : "Tester";
            var game = new GameController
            {
                Account = autoName,
                ClassId = "squire",
                HostLocalServer = true,
                Port = ReadPortArg(args),
            };
            GetTree().Root.CallDeferred(Node.MethodName.AddChild, game);
            // Anchor the timer to the game node — Boot frees itself below.
            game.Ready += () => game.GetTree().CreateTimer(10.0).Timeout += () =>
            {
                var img = game.GetViewport().GetTexture().GetImage();
                img.SavePng("user://auto_screenshot.png");
                GD.Print("[auto] screenshot saved to user://auto_screenshot.png");
                game.GetTree().Quit(0);
            };
            QueueFree();
            return;
        }

        int botArg = Array.IndexOf(args, "--bot");
        if (botArg >= 0)
        {
            var bot = new BotClient
            {
                Account = botArg + 1 < args.Length ? args[botArg + 1] : "Bot",
                Port = ReadPortArg(args),
            };
            GetTree().Root.CallDeferred(Node.MethodName.AddChild, bot);
            return;
        }

        try
        {
            _data = GameData.LoadFromDirectory(ProjectSettings.GlobalizePath("res://data"));
        }
        catch (DataValidationException e)
        {
            BuildErrorUi($"Content validation failed:\n{e.Message}");
            return;
        }
        BuildMenuUi();
    }

    private static int ReadPortArg(string[] args)
    {
        int i = Array.IndexOf(args, "--port");
        return i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int p)
            ? p
            : ServerNode.DefaultPort;
    }

    // ---------------------------------------------------------------- menu ui

    private void BuildMenuUi()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        SpriteComposer.Init(_data!.Appearance);

        var bg = new ColorRect { Color = new Color("14182c") };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var panel = UiTheme.MakePanel();
        panel.SetAnchorsPreset(LayoutPreset.Center);
        panel.CustomMinimumSize = new Vector2(720, 560);
        panel.Position = new Vector2(-360, -280);
        AddChild(panel);

        var title = UiTheme.MakeLabel("OPENFANTASY", 30);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.SetAnchorsPreset(LayoutPreset.TopWide);
        title.OffsetTop = 14;
        panel.AddChild(title);
        var subtitle = UiTheme.MakeLabel("~ Create your wanderer ~", 13);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        subtitle.SetAnchorsPreset(LayoutPreset.TopWide);
        subtitle.OffsetTop = 52;
        subtitle.Modulate = new Color(0.7f, 0.75f, 0.9f);
        panel.AddChild(subtitle);

        // ---- left column: identity + connection ----
        var left = new VBoxContainer { Position = new Vector2(28, 84), CustomMinimumSize = new Vector2(200, 0) };
        left.AddThemeConstantOverride("separation", 8);
        panel.AddChild(left);

        left.AddChild(UiTheme.MakeLabel("Name", 12));
        _nameEdit = new LineEdit { Text = "Wanderer", MaxLength = 16 };
        left.AddChild(_nameEdit);

        left.AddChild(UiTheme.MakeLabel("Class", 12));
        _classPicker = new OptionButton();
        foreach (string classId in _data!.ClassOrder)
            _classPicker.AddItem($"{_data.Classes[classId].Name}");
        left.AddChild(_classPicker);

        left.AddChild(UiTheme.MakeLabel("Server (for Join)", 12));
        _hostEdit = new LineEdit { Text = $"127.0.0.1:{ServerNode.DefaultPort}" };
        left.AddChild(_hostEdit);

        left.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });

        var solo = new Button { Text = "Play Solo", CustomMinimumSize = new Vector2(190, 38) };
        solo.Pressed += () => StartGame(solo: true);
        left.AddChild(solo);
        var join = new Button { Text = "Join World", CustomMinimumSize = new Vector2(190, 38) };
        join.Pressed += () => StartGame(solo: false);
        left.AddChild(join);

        _status = UiTheme.MakeLabel("", 11);
        _status.Modulate = new Color(1f, 0.6f, 0.6f);
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _status.CustomMinimumSize = new Vector2(190, 0);
        left.AddChild(_status);

        // ---- middle column: preview ----
        var previewPanel = UiTheme.MakePanel();
        previewPanel.Position = new Vector2(252, 104);
        previewPanel.Size = new Vector2(180, 220);
        panel.AddChild(previewPanel);

        _preview = new TextureRect
        {
            Position = new Vector2(42, 22),
            Size = new Vector2(96, 144),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.Nearest,
        };
        previewPanel.AddChild(_preview);

        _presetLabel = UiTheme.MakeLabel("", 11);
        _presetLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _presetLabel.SetAnchorsPreset(LayoutPreset.BottomWide);
        _presetLabel.OffsetTop = -30;
        _presetLabel.Modulate = UiTheme.AtbReadyColor;
        previewPanel.AddChild(_presetLabel);

        var randomize = new Button { Text = "Randomize", CustomMinimumSize = new Vector2(180, 32) };
        randomize.Position = new Vector2(252, 336);
        randomize.Pressed += RandomizeAppearance;
        panel.AddChild(randomize);

        // preset gallery
        var presetsLabel = UiTheme.MakeLabel("Presets", 12);
        presetsLabel.Position = new Vector2(252, 380);
        panel.AddChild(presetsLabel);
        var presetGrid = new GridContainer { Columns = 2, Position = new Vector2(252, 402) };
        presetGrid.AddThemeConstantOverride("h_separation", 6);
        presetGrid.AddThemeConstantOverride("v_separation", 6);
        panel.AddChild(presetGrid);
        foreach (var preset in _data.Appearance.Presets)
        {
            var b = new Button { Text = preset.Name, CustomMinimumSize = new Vector2(120, 26) };
            b.AddThemeFontSizeOverride("font_size", 10);
            var captured = preset;
            b.Pressed += () =>
            {
                _appearance = captured.ToAppearance().Sanitized(_data!.Appearance);
                _presetLabel.Text = captured.Name;
                RefreshCreator();
            };
            presetGrid.AddChild(b);
        }

        // ---- right column: appearance options ----
        var options = new VBoxContainer { Position = new Vector2(470, 96), CustomMinimumSize = new Vector2(230, 0) };
        options.AddThemeConstantOverride("separation", 6);
        panel.AddChild(options);

        AddOptionRow(options, "Race", () => _appearance.Race, v => _appearance.Race = v,
            _data.Appearance.Races.Count);
        AddOptionRow(options, "Skin", () => _appearance.Skin, v => _appearance.Skin = v,
            _data.Appearance.Skins.Count);
        AddOptionRow(options, "Hair", () => _appearance.HairStyle, v => _appearance.HairStyle = v,
            _data.Appearance.HairStyles);
        AddOptionRow(options, "Hair Color", () => _appearance.HairColor, v => _appearance.HairColor = v,
            _data.Appearance.HairColors.Count);
        AddOptionRow(options, "Outfit", () => _appearance.Outfit, v => _appearance.Outfit = v,
            _data.Appearance.Outfits);
        AddOptionRow(options, "Color A", () => _appearance.OutfitA, v => _appearance.OutfitA = v,
            _data.Appearance.OutfitColors.Count);
        AddOptionRow(options, "Color B", () => _appearance.OutfitB, v => _appearance.OutfitB = v,
            _data.Appearance.OutfitColors.Count);
        AddOptionRow(options, "Extra", () => _appearance.Acc, v => _appearance.Acc = v,
            _data.Appearance.Accessories);

        RefreshCreator();
    }

    private void AddOptionRow(VBoxContainer parent, string label, Func<byte> get, Action<byte> set, int count)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        var name = UiTheme.MakeLabel(label, 12);
        name.CustomMinimumSize = new Vector2(88, 0);
        row.AddChild(name);

        var prev = new Button { Text = "<", CustomMinimumSize = new Vector2(30, 26) };
        row.AddChild(prev);
        var value = UiTheme.MakeLabel("", 12);
        value.CustomMinimumSize = new Vector2(48, 0);
        value.HorizontalAlignment = HorizontalAlignment.Center;
        row.AddChild(value);
        _optionValueLabels.Add(value);
        var next = new Button { Text = ">", CustomMinimumSize = new Vector2(30, 26) };
        row.AddChild(next);

        void Step(int dir)
        {
            set((byte)(((get() + dir) % count + count) % count));
            _presetLabel.Text = "";
            RefreshCreator();
        }
        prev.Pressed += () => Step(-1);
        next.Pressed += () => Step(+1);
        int index = _optionValueLabels.Count - 1;
        value.SetMeta("getter_index", index);
        parent.AddChild(row);
    }

    private void RefreshCreator()
    {
        _preview.Texture = SpriteComposer.Get(_appearance);
        // Update option value labels (race by name, the rest 1-based).
        byte[] values = _appearance.ToBytes();
        for (int i = 0; i < _optionValueLabels.Count && i < values.Length; i++)
            _optionValueLabels[i].Text = i == 0
                ? _data!.Appearance.Races[Math.Min(values[0], (byte)(_data.Appearance.Races.Count - 1))]
                : $"{values[i] + 1}";
    }

    private void RandomizeAppearance()
    {
        byte Roll(int count)
        {
            _randState ^= _randState << 13;
            _randState ^= _randState >> 7;
            _randState ^= _randState << 17;
            return (byte)(_randState % (ulong)count);
        }
        _randState ^= (ulong)Time.GetTicksUsec();
        var manifest = _data!.Appearance;
        _appearance = new Appearance
        {
            Race = Roll(manifest.Races.Count),
            Skin = Roll(manifest.Skins.Count),
            HairStyle = Roll(manifest.HairStyles),
            HairColor = Roll(manifest.HairColors.Count),
            Outfit = Roll(manifest.Outfits),
            OutfitA = Roll(manifest.OutfitColors.Count),
            OutfitB = Roll(manifest.OutfitColors.Count),
            Acc = Roll(manifest.Accessories),
        };
        _presetLabel.Text = "";
        RefreshCreator();
    }

    public override void _Process(double delta)
    {
        if (_preview?.Texture is not AtlasTexture && _preview?.Texture != null)
        {
            // Animate the preview: cycle walk frames and slowly rotate facing.
            _previewClock += (float)delta;
            int frame = new[] { 0, 1, 2, 1 }[(int)(_previewClock * 5) % 4];
            _previewDir = (int)(_previewClock / 2.5f) % 4;
            var atlas = new AtlasTexture
            {
                Atlas = SpriteComposer.Get(_appearance),
                Region = new Rect2(frame * 16, _previewDir * 24, 16, 24),
            };
            _preview.Texture = atlas;
        }
        else if (_preview?.Texture is AtlasTexture existing)
        {
            _previewClock += (float)delta;
            int frame = new[] { 0, 1, 2, 1 }[(int)(_previewClock * 5) % 4];
            _previewDir = (int)(_previewClock / 2.5f) % 4;
            existing.Atlas = SpriteComposer.Get(_appearance);
            existing.Region = new Rect2(frame * 16, _previewDir * 24, 16, 24);
        }
    }

    private void BuildErrorUi(string message)
    {
        var label = UiTheme.MakeLabel(message, 16);
        label.SetAnchorsPreset(LayoutPreset.Center);
        AddChild(label);
    }

    private void StartGame(bool solo)
    {
        string name = _nameEdit.Text.Trim();
        if (name.Length < 2)
        {
            _status.Text = "Name must be at least 2 characters.";
            return;
        }

        string classId = _data!.ClassOrder[_classPicker.Selected];
        string host = "127.0.0.1";
        int port = ServerNode.DefaultPort;
        if (!solo)
        {
            string[] parts = _hostEdit.Text.Trim().Split(':');
            host = parts[0];
            if (parts.Length > 1 && int.TryParse(parts[1], out int p)) port = p;
        }

        var game = new GameController
        {
            Account = name,
            ClassId = classId,
            Host = host,
            Port = port,
            HostLocalServer = solo,
            Appearance = _appearance,
        };
        GetTree().Root.AddChild(game);
        GetTree().CurrentScene = game;
        QueueFree();
    }
}
