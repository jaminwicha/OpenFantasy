using Godot;
using OpenFantasy.Net;
using OpenFantasy.Sim;

namespace OpenFantasy.Client;

/// <summary>
/// The FF6-flavored HUD: player status window (HP/MP/ATB), ability hotbar,
/// chat window, target frame, and floating damage numbers.
/// </summary>
public partial class Hud : CanvasLayer
{
    [Signal] public delegate void ChatSubmittedEventHandler(string text);

    private ColorRect _hpFill = null!, _mpFill = null!, _atbFill = null!;
    private Label _statusLabel = null!, _levelLabel = null!, _gilLabel = null!;
    private Label _targetName = null!;
    private ColorRect _targetHpFill = null!;
    private NinePatchRect _targetPanel = null!;
    private RichTextLabel _chatLog = null!;
    private LineEdit _chatEntry = null!;
    private readonly List<(NinePatchRect panel, Label key, Label name, Label cost)> _hotbar = new();
    private Label _deathLabel = null!;

    private GameData _data = null!;
    private ClassDef _classDef = null!;

    public bool ChatFocused => _chatEntry.HasFocus();

    public void Setup(GameData data, string classId)
    {
        _data = data;
        _classDef = data.Classes[classId];
        BuildStatusWindow();
        BuildHotbar();
        BuildTargetFrame();
        BuildChat();
        BuildDeathOverlay();
    }

    // ------------------------------------------------------------------ build

    private void BuildStatusWindow()
    {
        var panel = UiTheme.MakePanel();
        panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        panel.Position = new Vector2(12, 12);
        panel.Size = new Vector2(230, 92);
        AddChild(panel);

        _statusLabel = UiTheme.MakeLabel("", 15);
        _statusLabel.Position = new Vector2(14, 8);
        panel.AddChild(_statusLabel);

        _levelLabel = UiTheme.MakeLabel("", 12);
        _levelLabel.Position = new Vector2(150, 10);
        panel.AddChild(_levelLabel);

        var hpText = UiTheme.MakeLabel("HP", 11);
        hpText.Position = new Vector2(14, 32);
        panel.AddChild(hpText);
        (var hpRoot, _hpFill) = UiTheme.MakeBar(UiTheme.HpColor, new Vector2(160, 8));
        hpRoot.Position = new Vector2(44, 36);
        panel.AddChild(hpRoot);

        var mpText = UiTheme.MakeLabel("MP", 11);
        mpText.Position = new Vector2(14, 48);
        panel.AddChild(mpText);
        (var mpRoot, _mpFill) = UiTheme.MakeBar(UiTheme.MpColor, new Vector2(160, 8));
        mpRoot.Position = new Vector2(44, 52);
        panel.AddChild(mpRoot);

        var atbText = UiTheme.MakeLabel("ATB", 11);
        atbText.Position = new Vector2(14, 64);
        panel.AddChild(atbText);
        (var atbRoot, _atbFill) = UiTheme.MakeBar(UiTheme.AtbColor, new Vector2(160, 8));
        atbRoot.Position = new Vector2(44, 68);
        panel.AddChild(atbRoot);

        _gilLabel = UiTheme.MakeLabel("", 11);
        _gilLabel.Position = new Vector2(150, 76);
        panel.AddChild(_gilLabel);
    }

    private void BuildHotbar()
    {
        var row = new HBoxContainer();
        row.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        row.Position = new Vector2(-((_classDef.Abilities.Count * 118) / 2f), -76);
        row.AddThemeConstantOverride("separation", 8);
        AddChild(row);

        for (int i = 0; i < _classDef.Abilities.Count; i++)
        {
            var ability = _data.Abilities[_classDef.Abilities[i]];
            var panel = UiTheme.MakePanel();
            panel.CustomMinimumSize = new Vector2(110, 46);

            var key = UiTheme.MakeLabel($"{i + 1}", 13);
            key.Position = new Vector2(8, 4);
            key.Modulate = UiTheme.AtbColor;
            panel.AddChild(key);

            var name = UiTheme.MakeLabel(ability.Name, 12);
            name.Position = new Vector2(24, 5);
            panel.AddChild(name);

            var cost = UiTheme.MakeLabel(
                ability.MpCost > 0 ? $"{ability.MpCost} MP" : "—", 10);
            cost.Position = new Vector2(24, 24);
            cost.Modulate = new Color(0.65f, 0.7f, 0.9f);
            panel.AddChild(cost);

            row.AddChild(panel);
            _hotbar.Add((panel, key, name, cost));
        }
    }

    private void BuildTargetFrame()
    {
        _targetPanel = UiTheme.MakePanel();
        _targetPanel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _targetPanel.Position = new Vector2(-110, 10);
        _targetPanel.Size = new Vector2(220, 46);
        _targetPanel.Visible = false;
        AddChild(_targetPanel);

        _targetName = UiTheme.MakeLabel("", 13);
        _targetName.Position = new Vector2(12, 6);
        _targetPanel.AddChild(_targetName);

        (var hpRoot, _targetHpFill) = UiTheme.MakeBar(UiTheme.HpColor, new Vector2(196, 8));
        hpRoot.Position = new Vector2(12, 28);
        _targetPanel.AddChild(hpRoot);
    }

    private void BuildChat()
    {
        var panel = UiTheme.MakePanel();
        panel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        panel.Position = new Vector2(12, -196);
        panel.Size = new Vector2(360, 150);
        AddChild(panel);

        _chatLog = new RichTextLabel
        {
            BbcodeEnabled = true,
            ScrollFollowing = true,
            Position = new Vector2(10, 8),
            Size = new Vector2(340, 108),
        };
        _chatLog.AddThemeFontSizeOverride("normal_font_size", 12);
        panel.AddChild(_chatLog);

        _chatEntry = new LineEdit
        {
            PlaceholderText = "Enter to chat…",
            Position = new Vector2(10, 118),
            Size = new Vector2(340, 26),
            MaxLength = 200,
        };
        _chatEntry.TextSubmitted += text =>
        {
            _chatEntry.Clear();
            _chatEntry.ReleaseFocus();
            text = text.Trim();
            if (text.Length > 0)
                EmitSignal(SignalName.ChatSubmitted, text);
        };
        panel.AddChild(_chatEntry);
    }

    private void BuildDeathOverlay()
    {
        _deathLabel = UiTheme.MakeLabel("You have fallen…\nThe crystal calls you home.", 24);
        _deathLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _deathLabel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _deathLabel.Position = new Vector2(-220, -40);
        _deathLabel.Size = new Vector2(440, 80);
        _deathLabel.Modulate = new Color(1f, 0.5f, 0.5f);
        _deathLabel.Visible = false;
        AddChild(_deathLabel);
    }

    // ------------------------------------------------------------------ update

    public void UpdateLocal(NetEntityState me)
    {
        _statusLabel.Text = me.Name;
        _levelLabel.Text = $"Lv {me.Level}";
        _gilLabel.Text = $"{me.Gil} gil";
        UiTheme.SetBarFraction(_hpFill, me.MaxHp > 0 ? (float)me.Hp / me.MaxHp : 0);
        UiTheme.SetBarFraction(_mpFill, me.MaxMp > 0 ? (float)me.Mp / me.MaxMp : 0);
        float atb = (float)me.Atb / SimConstants.AtbMax;
        UiTheme.SetBarFraction(_atbFill, atb);
        _atbFill.Color = atb >= 1f ? UiTheme.AtbReadyColor : UiTheme.AtbColor;
        _deathLabel.Visible = !me.Alive;

        for (int i = 0; i < _hotbar.Count; i++)
        {
            var ability = _data.Abilities[_classDef.Abilities[i]];
            bool locked = me.Level < ability.UnlockLevel;
            bool ready = !locked && me.Atb >= ability.AtbCost && me.Mp >= ability.MpCost;
            var (panel, key, name, cost) = _hotbar[i];
            panel.Modulate = locked ? new Color(0.5f, 0.5f, 0.55f) : Colors.White;
            name.Modulate = ready ? Colors.White : new Color(0.6f, 0.6f, 0.65f);
            key.Modulate = ready ? UiTheme.AtbReadyColor : new Color(0.6f, 0.55f, 0.4f);
            cost.Text = locked ? $"Lv {ability.UnlockLevel}" : ability.MpCost > 0 ? $"{ability.MpCost} MP" : "—";
        }
    }

    public void UpdateTarget(NetEntityState? target)
    {
        _targetPanel.Visible = target != null;
        if (target == null) return;
        _targetName.Text = $"{target.Name}  Lv {target.Level}";
        UiTheme.SetBarFraction(_targetHpFill, target.MaxHp > 0 ? (float)target.Hp / target.MaxHp : 0);
    }

    public void AddChatLine(string speaker, string text)
    {
        string safeSpeaker = speaker.Replace("[", "[lb]");
        string safeText = text.Replace("[", "[lb]");
        _chatLog.AppendText(speaker == "*"
            ? $"[color=#9aa8d0]{safeText}[/color]\n"
            : $"[color=#e8c050]{safeSpeaker}:[/color] {safeText}\n");
    }

    public void FocusChat() => _chatEntry.GrabFocus();
}
