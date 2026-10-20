using System.Globalization;

namespace OpenFantasy.Client.Settings;

/// <summary>Linear bus levels from silent (0) to full (1). Presentation preferences, never world state.</summary>
internal sealed record SoundLevels(double Master, double Music, double Ambience, double Effects, double Voice)
{
    internal static SoundLevels Full { get; } = new(1, 1, 1, 1, 1);

    internal double this[string bus] => bus switch
    {
        "Master" => Master,
        "Music" => Music,
        "Ambience" => Ambience,
        "Effects" => Effects,
        "Voice" => Voice,
        _ => throw new ArgumentOutOfRangeException(nameof(bus), bus, "Unknown mixer bus."),
    };

    /// <summary>The same levels with one bus changed and clamped into range.</summary>
    internal SoundLevels With(string bus, double level)
    {
        double clamped = FarmSettings.Clamp(level);
        return bus switch
        {
            "Master" => this with { Master = clamped },
            "Music" => this with { Music = clamped },
            "Ambience" => this with { Ambience = clamped },
            "Effects" => this with { Effects = clamped },
            "Voice" => this with { Voice = clamped },
            _ => throw new ArgumentOutOfRangeException(nameof(bus), bus, "Unknown mixer bus."),
        };
    }
}

/// <summary>
/// The player's client settings: mixer levels and captions. Stored per user outside every save, so changing
/// them never alters a world, its history or another participant. Unknown, missing or malformed entries fall
/// back to their defaults individually; a damaged file never prevents play.
/// </summary>
internal sealed record FarmSettings(SoundLevels Sound, bool Captions)
{
    internal const int FormatVersion = 1;
    internal static IReadOnlyList<string> Buses { get; } = ["Master", "Music", "Ambience", "Effects", "Voice"];
    internal static FarmSettings Default { get; } = new(SoundLevels.Full, Captions: false);

    internal static double Clamp(double level) => double.IsFinite(level) ? Math.Clamp(level, 0, 1) : 1;

    internal static FarmSettings Parse(IReadOnlyDictionary<string, string> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        SoundLevels levels = SoundLevels.Full;
        foreach (string bus in Buses)
        {
            if (entries.TryGetValue("sound." + bus, out string? text)
                && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double level))
                levels = levels.With(bus, level);
        }
        bool captions = entries.TryGetValue("accessibility.captions", out string? flag)
            && bool.TryParse(flag, out bool enabled) ? enabled : Default.Captions;
        return new FarmSettings(levels, captions);
    }

    internal IReadOnlyDictionary<string, string> ToEntries()
    {
        var entries = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["format"] = FormatVersion.ToString(CultureInfo.InvariantCulture),
            ["accessibility.captions"] = Captions ? "true" : "false",
        };
        foreach (string bus in Buses) entries["sound." + bus] = Sound[bus].ToString("R", CultureInfo.InvariantCulture);
        return entries;
    }
}
