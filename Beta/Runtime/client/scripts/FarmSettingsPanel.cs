using Godot;
using OpenFantasy.Client.Settings;

namespace OpenFantasy.Client;

/// <summary>
/// Scene-owned client settings: mixer levels and captions. It raises preference changes only; it submits no
/// world command and never changes readiness, pause or timing.
/// </summary>
internal sealed partial class FarmSettingsPanel : CanvasLayer
{
    private readonly Dictionary<string, HSlider> _levels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _keyButtons = new(StringComparer.Ordinal);
    private Control _root = null!;
    private CheckButton _captions = null!;
    private Button _close = null!;
    private Control? _returnFocus;
    private Button _resetKeys = null!;
    private Label _notice = null!;
    private FarmSettings _presented = FarmSettings.Default;
    private string? _capturing;

    internal event Action<string, double>? LevelChanged;
    internal event Action<bool>? CaptionsChanged;
    internal event Action? CloseRequested;
    internal event Action<string, long>? KeyChosen;
    internal event Action? KeysReset;

    internal bool IsOpen => _root.Visible;
    internal HSlider Level(string bus) => _levels[bus];
    internal CheckButton CaptionsToggle => _captions;
    internal Button CloseButton => _close;
    internal Button KeyButton(string action) => _keyButtons[action];
    internal Button ResetKeysButton => _resetKeys;
    internal string? CapturingAction => _capturing;
    internal string NoticeText => _notice.Text;

    public override void _Ready()
    {
        _root = GetNode<Control>("%SettingsRoot");
        foreach (string bus in FarmSettings.Buses)
        {
            HSlider slider = GetNode<HSlider>("%" + bus + "Level");
            slider.ValueChanged += value => LevelChanged?.Invoke(bus, value / 100.0);
            _levels[bus] = slider;
        }
        _captions = GetNode<CheckButton>("%Captions");
        _captions.Toggled += enabled => CaptionsChanged?.Invoke(enabled);
        _close = GetNode<Button>("%Close");
        _close.Pressed += () => CloseRequested?.Invoke();
        _resetKeys = GetNode<Button>("%ResetKeys");
        _resetKeys.Pressed += () => { EndCapture(); KeysReset?.Invoke(); Notice("Keys restored to their defaults."); };
        _notice = GetNode<Label>("%Notice");
        GridContainer keys = GetNode<GridContainer>("%Keys");
        foreach (KeyBindingAction action in KeyBindings.Actions)
        {
            keys.AddChild(new Label { Text = action.Label });
            var button = new Button { FocusMode = Control.FocusModeEnum.All, CustomMinimumSize = new Vector2(120, 0),
                TooltipText = "Choose a new key for " + action.Label.ToLowerInvariant() + "." };
            button.Pressed += () => BeginCapture(action.Action);
            keys.AddChild(button);
            _keyButtons[action.Action] = button;
        }
    }

    /// <summary>
    /// While choosing a key, the next key press belongs to the panel and never reaches gameplay routing.
    /// Escape keeps the current key.
    /// </summary>
    public override void _Input(InputEvent input)
    {
        if (_capturing is null || !_root.Visible || input is not InputEventKey { Pressed: true, Echo: false } key) return;
        GetViewport().SetInputAsHandled();
        string action = _capturing;
        EndCapture();
        long code = (long)(key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode);
        if (code == KeyBindings.Escape)
        {
            Notice("Kept the current key.");
            return;
        }
        KeyChosen?.Invoke(action, code);
    }

    internal void Notice(string text) => _notice.Text = text;

    private void BeginCapture(string action)
    {
        EndCapture();
        _capturing = action;
        _keyButtons[action].Text = "Press a key…";
        Notice("Press the new key, or Escape to keep the current one.");
    }

    private void EndCapture()
    {
        _capturing = null;
        Present(_presented);
    }

    internal void Open(FarmSettings settings)
    {
        Present(settings);
        _returnFocus = GetViewport().GuiGetFocusOwner();
        _root.Visible = true;
        _levels[FarmSettings.Buses[0]].GrabFocus();
    }

    internal void Present(FarmSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        foreach (string bus in FarmSettings.Buses) _levels[bus].SetValueNoSignal(Math.Round(settings.Sound[bus] * 100));
        _captions.SetPressedNoSignal(settings.Captions);
        _presented = settings;
        _close.Text = $"Close  [{FarmKeyHints.Current.Settings}]";
        foreach (KeyBindingAction action in KeyBindings.Actions)
            if (action.Action != _capturing) _keyButtons[action.Action].Text = OS.GetKeycodeString((Key)settings.Keys[action.Action]);
    }

    internal void Close()
    {
        _capturing = null;
        Notice("");
        _root.Visible = false;
        if (IsInstanceValid(_returnFocus) && _returnFocus!.IsVisibleInTree()) _returnFocus.GrabFocus();
        _returnFocus = null;
    }
}
