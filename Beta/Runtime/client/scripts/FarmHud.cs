using Godot;
using OpenFantasy.Core.Gathering;
using OpenFantasy.Hosting.Simulation;

namespace OpenFantasy.Client;

/// <summary>Readable local play controls; all displayed inventory belongs to the current player.</summary>
internal sealed partial class FarmHud : CanvasLayer
{
    private readonly Label _clock = new();
    private readonly Label _wood = new();
    private readonly Label _message = new() { Text = "Find Wren by the rows.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _speech = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly PanelContainer _dialogue = new();
    private readonly Button _talk = new() { Text = "Talk  [E]" };
    private readonly Button _give = new() { Text = "Give wood  [G]" };
    private readonly Button _gather = new() { Text = "Gather  [F]" };
    private readonly Button _craft = new() { Text = "Pack rations  [C]" };
    private readonly Button _close = new() { Text = "Continue  [E]" };
    private readonly Button _pause = new() { Text = "Pause  [Esc]" };
    private bool _wasSpeaking;

    internal event Action? TalkRequested;
    internal event Action? GiveRequested;
    internal event Action? GatherRequested;
    internal event Action? CraftRequested;
    internal event Action? CloseRequested;
    internal event Action? PauseRequested;

    public override void _Ready()
    {
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Theme = CreateTheme() };
        AddChild(root);
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var header = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.AddChild(header);
        header.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopWide);
        header.OffsetBottom = 64;
        header.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color("192a2e"), ContentMarginLeft = 18, ContentMarginRight = 18,
            ContentMarginTop = 12, ContentMarginBottom = 12
        });
        var top = new HBoxContainer();
        header.AddChild(top);
        top.AddChild(new Label { Text = "Wren's farm", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        top.AddChild(_clock);
        top.AddChild(_wood);
        top.AddChild(_pause);
        top.AddThemeConstantOverride("separation", 22);

        var bottom = new PanelContainer();
        root.AddChild(bottom);
        bottom.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        bottom.OffsetLeft = 18;
        bottom.OffsetRight = -18;
        bottom.OffsetTop = -102;
        bottom.OffsetBottom = -18;
        var column = new VBoxContainer();
        bottom.AddChild(column);
        column.AddChild(_message);
        var actions = new HBoxContainer();
        column.AddChild(actions);
        actions.AddChild(new Label { Text = "Move: WASD / arrows", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        actions.AddChild(_talk);
        actions.AddChild(_give);
        actions.AddChild(_gather);
        actions.AddChild(_craft);

        root.AddChild(_dialogue);
        _dialogue.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        _dialogue.OffsetLeft = 80;
        _dialogue.OffsetRight = -80;
        _dialogue.OffsetTop = -244;
        _dialogue.OffsetBottom = -124;
        var conversation = new VBoxContainer();
        _dialogue.AddChild(conversation);
        conversation.AddChild(_speech);
        conversation.AddChild(_close);
        _dialogue.Hide();
        _talk.Pressed += () => TalkRequested?.Invoke();
        _give.Pressed += () => GiveRequested?.Invoke();
        _gather.Pressed += () => GatherRequested?.Invoke();
        _craft.Pressed += () => CraftRequested?.Invoke();
        _close.Pressed += () => CloseRequested?.Invoke();
        _pause.Pressed += () => PauseRequested?.Invoke();
    }

    internal void Present(LocalWorldView view, ulong wood, ulong berries, bool pending, bool paused)
    {
        int hour = view.Calendar.MillisecondsWithinDay / 3_600_000;
        int minute = view.Calendar.MillisecondsWithinDay / 60_000 % 60;
        _clock.Text = $"Day {view.Calendar.Days + 1} · {hour:D2}:{minute:D2}";
        _wood.Text = view.PlayerGatherEffort.HasValue ? $"Wood: {wood} · Berries: {berries}" : $"Wood: {wood}";
        if (view.Crafting is not null)
        {
            _wood.Text += $"\nRations: {FarmCrafting.Quantity(view, OpenFantasy.Core.Production.RecipeMaterialRole.Output)}"
                + $" · Offcuts: {FarmCrafting.Quantity(view, OpenFantasy.Core.Production.RecipeMaterialRole.Waste)}";
        }
        _craft.Visible = view.Crafting is not null;
        _craft.Disabled = pending || paused || view.HasReadingHold || !FarmCrafting.HasIngredients(view);
        _talk.Disabled = pending || paused || view.HasReadingHold;
        _give.Disabled = pending || paused || wood == 0;
        _gather.Visible = view.PlayerGatherEffort.HasValue;
        var target = FarmClientSession.NearestGatherNode(view);
        _gather.Text = target?.RequiredTool == GatherToolKind.Axe ? "Chop oak  [F]" : "Pick berries  [F]";
        _gather.Disabled = pending || paused || view.HasReadingHold || view.PlayerGatherEffort < 400
            || target is null || !FarmClientSession.CanReachGatherNode(view, target);
        _close.Disabled = pending || paused;
        _pause.Text = paused ? "Resume  [Esc]" : "Pause  [Esc]";
        bool speaking = view.Dialogue is not null;
        _dialogue.Visible = speaking;
        if (view.Dialogue is { } dialogue)
        {
            _speech.Text = dialogue.SpeakerName + "\n" + dialogue.Text;
        }
        if (speaking && !_wasSpeaking)
        {
            _close.GrabFocus();
        }
        else if (!speaking && _wasSpeaking)
        {
            _close.ReleaseFocus();
        }
        _wasSpeaking = speaking;
    }

    internal void Message(string text) => _message.Text = text;

    private static Theme CreateTheme()
    {
        var theme = new Theme { DefaultFontSize = 18 };
        var panel = new StyleBoxFlat
        {
            BgColor = new Color("192a2eee"), BorderColor = new Color("a3a17d"),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 10
        };
        theme.SetStylebox("panel", "PanelContainer", panel);
        theme.SetColor("font_color", "Label", new Color("f0e8cf"));
        theme.SetConstant("outline_size", "Label", 3);
        theme.SetColor("font_outline_color", "Label", new Color("162123"));
        return theme;
    }
}
