using Godot;
using OpenFantasy.Client.Presentation.Audio;
using OpenFantasy.Client.Settings;

namespace OpenFantasy.Client;

public partial class FarmGame
{
    private FarmSettingsPanel _settingsPanel = null!;
    private FarmCaptions _captions = null!;
    private FarmSettingsStore? _settingsStore;

    /// <summary>
    /// Explicit settings file for a disposable fixture. Fixtures without one use defaults in memory and never
    /// read or write the player's own settings.
    /// </summary>
    internal string? SettingsPathOverride { get; init; }
    internal FarmSettings Settings { get; private set; } = FarmSettings.Default;
    internal bool IsSettingsOpen => _input.SettingsOpen;
    internal FarmSettingsPanel SettingsPanel => _settingsPanel;
    internal IReadOnlyList<string> ShownCaptions => _captions.Shown;

    private bool IsFixture => SavePathOverride is not null || NetworkFixtureOpening is not null;

    private void ReadySettings()
    {
        _settingsStore = SettingsPathOverride is { } path ? new FarmSettingsStore(path)
            : IsFixture ? null : new FarmSettingsStore(FarmSettingsStore.DefaultPath);
        Settings = _settingsStore?.Load() ?? FarmSettings.Default;
        _settingsPanel = GD.Load<PackedScene>("res://scenes/FarmSettingsPanel.tscn").Instantiate<FarmSettingsPanel>();
        AddChild(_settingsPanel);
        _captions = GD.Load<PackedScene>("res://scenes/FarmCaptions.tscn").Instantiate<FarmCaptions>();
        AddChild(_captions);
        _settingsPanel.CloseRequested += () => Perform(FarmSemanticAction.CloseSettings);
        _settingsPanel.LevelChanged += (bus, level) => ApplySettings(Settings with { Sound = Settings.Sound.With(bus, level) });
        _settingsPanel.CaptionsChanged += enabled => ApplySettings(Settings with { Captions = enabled });
        _settingsPanel.KeyChosen += (action, key) =>
        {
            if (!KeyBindings.IsAssignable(key))
            {
                _settingsPanel.Notice("That key is reserved for pausing, closing or navigation.");
                return;
            }
            ApplySettings(Settings with { Keys = Settings.Keys.Assign(action, key) });
            _settingsPanel.Present(Settings);
        };
        _settingsPanel.KeysReset += () =>
        {
            ApplySettings(Settings with { Keys = KeyBindings.Default });
            _settingsPanel.Present(Settings);
        };
        ApplySettings(Settings);
    }

    private void ApplySettings(FarmSettings settings)
    {
        Settings = settings;
        FarmSoundscape.ApplyLevels(settings.Sound);
        FarmInputMap.ApplyBindings(settings.Keys);
        FarmKeyHints.Apply(FarmKeyLabels.FromInputMap());
        _hud.RefreshKeyHints();
        _soundscape.Captions.SetEnabled(settings.Captions);
    }

    private void OpenSettings()
    {
        if (!_input.Permits(Situation(), FarmSemanticAction.OpenSettings)) return;
        _input.SetSettingsOpen(true);
        _settingsPanel.Open(Settings);
    }

    private void CloseSettings()
    {
        if (!_input.SettingsOpen) return;
        _input.SetSettingsOpen(false);
        _settingsPanel.Close();
        // A failed write keeps the applied preferences for this run and is reported; it never affects the world.
        if (_settingsStore?.Save(Settings) is { } result and not Error.Ok)
            _hud.Message("Settings could not be saved: " + result + ".");
    }

    private void PresentCaptions() => _captions.Present(_soundscape.Captions.Visible);
}
