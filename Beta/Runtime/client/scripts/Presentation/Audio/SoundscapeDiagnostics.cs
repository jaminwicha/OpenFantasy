using System.Collections.ObjectModel;
using OpenFantasy.Core;

namespace OpenFantasy.Client.Presentation.Audio;

/// <summary>One derived cue and what playback did with it.</summary>
internal sealed record SoundscapeCueRecord(SoundCueKey Key, string IntentKey, AcousticPlayback Playback);

/// <summary>One sustained context change and the intent statuses of its beds.</summary>
internal sealed record SoundscapeContextRecord(SoundscapeContext Context, AcousticIntentStatus ScoreStatus,
    AcousticIntentStatus AmbienceStatus);

/// <summary>
/// Bounded presentation diagnostics for probes and developer inspection: recent cues and context changes, plus totals
/// per playback outcome. It is never persisted or sent to the world, and it exposes no source the view withheld.
/// </summary>
internal sealed class SoundscapeDiagnostics(int capacity = SoundscapeDiagnostics.DefaultCapacity)
{
    internal const int DefaultCapacity = 128;

    private readonly int _capacity = capacity >= 1 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
    private readonly Queue<SoundscapeCueRecord> _cues = new();
    private readonly Queue<SoundscapeContextRecord> _contexts = new();
    private readonly Dictionary<AcousticPlayback, long> _totals = [];
    private readonly Dictionary<SoundCueKind, long> _derived = [];
    private readonly Dictionary<(SoundCueKind, EntityId), long> _derivedBySource = [];

    internal ReadOnlyCollection<SoundscapeCueRecord> RecentCues => _cues.ToList().AsReadOnly();
    internal ReadOnlyCollection<SoundscapeContextRecord> RecentContexts => _contexts.ToList().AsReadOnly();
    internal long Visits { get; private set; }

    internal long Total(AcousticPlayback playback) => _totals.GetValueOrDefault(playback);
    internal long Derived(SoundCueKind kind) => _derived.GetValueOrDefault(kind);
    /// <summary>Cues of one kind derived from one visible source during this adapter's lifetime.</summary>
    internal long Derived(SoundCueKind kind, EntityId source) => _derivedBySource.GetValueOrDefault((kind, source));

    internal void RecordVisit() => Visits++;

    internal void Record(SoundCue cue, AcousticIntent intent, AcousticPlayback playback)
    {
        ArgumentNullException.ThrowIfNull(cue);
        ArgumentNullException.ThrowIfNull(intent);
        _derived[cue.Kind] = Derived(cue.Kind) + 1;
        _derivedBySource[(cue.Kind, cue.Key.SourceId)] = Derived(cue.Kind, cue.Key.SourceId) + 1;
        _totals[playback] = Total(playback) + 1;
        Enqueue(_cues, new SoundscapeCueRecord(cue.Key, intent.Key, playback));
    }

    internal void Record(SoundscapeContext context, AcousticIntent score, AcousticIntent ambience)
    {
        ArgumentNullException.ThrowIfNull(context);
        Enqueue(_contexts, new SoundscapeContextRecord(context, score.Status, ambience.Status));
    }

    private void Enqueue<T>(Queue<T> queue, T value)
    {
        queue.Enqueue(value);
        while (queue.Count > _capacity) queue.Dequeue();
    }
}
