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

    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();

        if (args.Contains("--selftest"))
        {
            bool ok = SelfTest.RunAll(ProjectSettings.GlobalizePath("res://data"), GD.Print);
            GetTree().Quit(ok ? 0 : 1);
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
            GetTree().CreateTimer(10.0).Timeout += () =>
            {
                var img = GetViewport().GetTexture().GetImage();
                img.SavePng("user://auto_screenshot.png");
                GD.Print("[auto] screenshot saved to user://auto_screenshot.png");
                GetTree().Quit(0);
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

        var bg = new ColorRect { Color = new Color("14182c") };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var panel = UiTheme.MakePanel();
        panel.SetAnchorsPreset(LayoutPreset.Center);
        panel.CustomMinimumSize = new Vector2(420, 380);
        panel.Position = new Vector2(-210, -190);
        AddChild(panel);

        var box = new VBoxContainer();
        box.SetAnchorsPreset(LayoutPreset.FullRect);
        box.AddThemeConstantOverride("separation", 12);
        box.OffsetLeft = 24;
        box.OffsetRight = -24;
        box.OffsetTop = 20;
        box.OffsetBottom = -20;
        panel.AddChild(box);

        var title = UiTheme.MakeLabel("OPENFANTASY", 32);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(title);
        var subtitle = UiTheme.MakeLabel("~ Verdance March ~", 14);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        subtitle.Modulate = new Color(0.7f, 0.75f, 0.9f);
        box.AddChild(subtitle);

        box.AddChild(UiTheme.MakeLabel("Name", 13));
        _nameEdit = new LineEdit { Text = "Wanderer", MaxLength = 16 };
        box.AddChild(_nameEdit);

        box.AddChild(UiTheme.MakeLabel("Class", 13));
        _classPicker = new OptionButton();
        foreach (string classId in _data!.ClassOrder)
            _classPicker.AddItem($"{_data.Classes[classId].Name}");
        box.AddChild(_classPicker);

        box.AddChild(UiTheme.MakeLabel("Server (for Join)", 13));
        _hostEdit = new LineEdit { Text = $"127.0.0.1:{ServerNode.DefaultPort}" };
        box.AddChild(_hostEdit);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 12);
        buttons.Alignment = BoxContainer.AlignmentMode.Center;
        box.AddChild(buttons);

        var solo = new Button { Text = "Play Solo", CustomMinimumSize = new Vector2(150, 40) };
        solo.Pressed += () => StartGame(solo: true);
        buttons.AddChild(solo);

        var join = new Button { Text = "Join World", CustomMinimumSize = new Vector2(150, 40) };
        join.Pressed += () => StartGame(solo: false);
        buttons.AddChild(join);

        _status = UiTheme.MakeLabel("", 12);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        _status.Modulate = new Color(1f, 0.6f, 0.6f);
        box.AddChild(_status);
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
        };
        GetTree().Root.AddChild(game);
        GetTree().CurrentScene = game;
        QueueFree();
    }
}
