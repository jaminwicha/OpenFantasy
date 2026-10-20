namespace OpenFantasy.Client.Presentation.Audio;

/// <summary>One visible caption and how long it has been shown.</summary>
internal sealed record CaptionLine(string IntentKey, string Text, double AgeSeconds);

/// <summary>
/// Captions for notable derived sounds, when the player enables them. A caption states only what its audible
/// cue conveys and appears whether or not a creator asset is mapped, because it describes the sound event.
/// Sources withheld from the presented frame are never captioned. A repeat of a visible caption refreshes it
/// instead of stacking, and at most a few lines show at once.
/// </summary>
internal sealed class CaptionFeed
{
    internal const double LifetimeSeconds = 2.5;
    internal const int MaximumVisible = 3;

    private readonly List<CaptionLine> _lines = [];

    internal bool Enabled { get; private set; }
    internal IReadOnlyList<CaptionLine> Visible => _lines.AsReadOnly();

    internal void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        if (!enabled) _lines.Clear();
    }

    internal void Offer(AcousticIntent intent, AcousticPlayback playback)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (!Enabled || !intent.IsCaptioned || playback == AcousticPlayback.Withheld) return;
        int existing = _lines.FindIndex(line => line.IntentKey == intent.Key);
        if (existing >= 0) _lines.RemoveAt(existing);
        _lines.Add(new CaptionLine(intent.Key, intent.Caption!, 0));
        while (_lines.Count > MaximumVisible) _lines.RemoveAt(0);
    }

    internal void Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        for (int index = _lines.Count - 1; index >= 0; index--)
        {
            CaptionLine aged = _lines[index] with { AgeSeconds = _lines[index].AgeSeconds + seconds };
            if (aged.AgeSeconds >= LifetimeSeconds) _lines.RemoveAt(index);
            else _lines[index] = aged;
        }
    }

    internal void Clear() => _lines.Clear();
}
