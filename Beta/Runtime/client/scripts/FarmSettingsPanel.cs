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
    private Control _root = null!;
    private CheckButton _captions = null!;
    private Button _close = null!;
    private Control? _returnFocus;

    internal event Action<string, double>? LevelChanged;
    internal event Action<bool>? CaptionsChanged;
    internal event Action? CloseRequested;

    internal bool IsOpen => _root.Visible;
    internal HSlider Level(string bus) => _levels[bus];
    internal CheckButton CaptionsToggle => _captions;
    internal Button CloseButton => _close;

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
    }

    internal void Close()
    {
        _root.Visible = false;
        if (IsInstanceValid(_returnFocus) && _returnFocus!.IsVisibleInTree()) _returnFocus.GrabFocus();
        _returnFocus = null;
    }
}
