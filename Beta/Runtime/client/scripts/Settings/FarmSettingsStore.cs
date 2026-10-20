using Godot;

namespace OpenFantasy.Client.Settings;

/// <summary>
/// Reads and writes client settings in one user file, separate from every world save. A missing or unreadable
/// file yields defaults; writes replace the whole file. Fixtures pass an explicit absolute path.
/// </summary>
internal sealed class FarmSettingsStore(string path)
{
    internal const string DefaultPath = "user://settings.cfg";
    private const string Section = "settings";

    internal string Path { get; } = !string.IsNullOrWhiteSpace(path) ? path
        : throw new ArgumentException("A settings path is required.", nameof(path));

    internal FarmSettings Load()
    {
        var file = new ConfigFile();
        if (file.Load(Path) != Error.Ok || !file.HasSection(Section)) return FarmSettings.Default;
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string key in file.GetSectionKeys(Section)) entries[key] = file.GetValue(Section, key).AsString();
        return FarmSettings.Parse(entries);
    }

    internal Error Save(FarmSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var file = new ConfigFile();
        foreach ((string key, string value) in settings.ToEntries()) file.SetValue(Section, key, value);
        return file.Save(Path);
    }
}
