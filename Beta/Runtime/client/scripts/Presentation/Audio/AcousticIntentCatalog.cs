using System.Collections.ObjectModel;

namespace OpenFantasy.Client.Presentation.Audio;

/// <summary>The mixer bus a sound plays on; each has independent player volume control.</summary>
internal enum AcousticBus { Music = 1, Ambience = 2, Effects = 3, Voice = 4 }

/// <summary>
/// Whether an intent has accepted sound. A missing asset is never treated as finished design: it stays
/// <see cref="AwaitingCreatorAsset"/> until a creator supplies one, while a deliberate silence is declared explicitly.
/// </summary>
internal enum AcousticIntentStatus { AwaitingCreatorAsset = 1, IntentionallySilent = 2, Mapped = 3 }

/// <summary>
/// Acoustic intent for one semantic sound: bus, accepted stream, caption, repetition spacing and simultaneous voices.
/// A caption states only what a legitimate audible cue conveys; music and ambience beds carry none.
/// </summary>
internal sealed record AcousticIntent(string Key, AcousticBus Bus, AcousticIntentStatus Status, string? StreamPath,
    string? Caption, double MinimumIntervalSeconds, int MaximumVoices)
{
    internal bool IsPlayable => Status == AcousticIntentStatus.Mapped;
}

/// <summary>
/// The binding from semantic cues and contexts to acoustic intent. Every cue kind and material the deriver can emit
/// and every sustained context has an entry; an unknown pairing throws rather than silently playing nothing.
/// The default catalog maps no assets: accepted recordings and music come from a designated audio creator.
/// </summary>
internal sealed class AcousticIntentCatalog
{
    private readonly Dictionary<(SoundCueKind, SoundMaterial), AcousticIntent> _cues;
    private readonly Dictionary<SoundscapeScore, AcousticIntent> _scores;
    private readonly Dictionary<SoundscapeAmbience, AcousticIntent> _ambiences;

    internal AcousticIntentCatalog(IReadOnlyDictionary<(SoundCueKind, SoundMaterial), AcousticIntent> cues,
        IReadOnlyDictionary<SoundscapeScore, AcousticIntent> scores,
        IReadOnlyDictionary<SoundscapeAmbience, AcousticIntent> ambiences)
    {
        ArgumentNullException.ThrowIfNull(cues);
        ArgumentNullException.ThrowIfNull(scores);
        ArgumentNullException.ThrowIfNull(ambiences);
        _cues = cues.ToDictionary(pair => pair.Key, pair => Validate(pair.Value, AcousticBus.Effects));
        _scores = scores.ToDictionary(pair => pair.Key, pair => Validate(pair.Value, AcousticBus.Music));
        _ambiences = ambiences.ToDictionary(pair => pair.Key, pair => Validate(pair.Value, AcousticBus.Ambience));
        foreach (SoundscapeScore score in Enum.GetValues<SoundscapeScore>())
            if (!_scores.ContainsKey(score)) throw new ArgumentException($"Score context {score} has no acoustic intent.", nameof(scores));
        foreach (SoundscapeAmbience ambience in Enum.GetValues<SoundscapeAmbience>())
            if (!_ambiences.ContainsKey(ambience)) throw new ArgumentException($"Ambience {ambience} has no acoustic intent.", nameof(ambiences));
        Entries = _cues.Values.Concat(_scores.Values).Concat(_ambiences.Values).OrderBy(intent => intent.Key, StringComparer.Ordinal)
            .ToList().AsReadOnly();
        if (Entries.Select(intent => intent.Key).Distinct(StringComparer.Ordinal).Count() != Entries.Count)
            throw new ArgumentException("Acoustic intent keys must be unique.");
    }

    internal ReadOnlyCollection<AcousticIntent> Entries { get; }

    /// <summary>The cue/material pairings the cue deriver can produce; each must have an intent.</summary>
    internal static IReadOnlyList<(SoundCueKind Kind, SoundMaterial Material)> DerivablePairings { get; } =
    [
        (SoundCueKind.Footstep, SoundMaterial.Ground),
        (SoundCueKind.GatherContact, SoundMaterial.Oak),
        (SoundCueKind.GatherContact, SoundMaterial.Berry),
        (SoundCueKind.ConstructionContact, SoundMaterial.Joinery),
        (SoundCueKind.ExcavationContact, SoundMaterial.Earth),
    ];

    /// <summary>The first binding: every entry awaits a creator asset; spacing and voices are listening-tuned defaults.</summary>
    internal static AcousticIntentCatalog Default { get; } = new(
        new Dictionary<(SoundCueKind, SoundMaterial), AcousticIntent>
        {
            [(SoundCueKind.Footstep, SoundMaterial.Ground)] = Awaiting("cue.footstep.ground", AcousticBus.Effects, "Footsteps", 0.12, 4),
            [(SoundCueKind.GatherContact, SoundMaterial.Oak)] = Awaiting("cue.gather.oak", AcousticBus.Effects, "Chopping wood", 0.2, 2),
            [(SoundCueKind.GatherContact, SoundMaterial.Berry)] = Awaiting("cue.gather.berry", AcousticBus.Effects, "Harvesting berries", 0.2, 2),
            [(SoundCueKind.ConstructionContact, SoundMaterial.Joinery)] = Awaiting("cue.construction.joinery", AcousticBus.Effects, "Joinery work", 0.25, 2),
            [(SoundCueKind.ExcavationContact, SoundMaterial.Earth)] = Awaiting("cue.excavation.earth", AcousticBus.Effects, "Digging", 0.25, 2),
        },
        new Dictionary<SoundscapeScore, AcousticIntent>
        {
            [SoundscapeScore.Exploration] = Awaiting("score.exploration", AcousticBus.Music, null, 0, 1),
            [SoundscapeScore.Encounter] = Awaiting("score.encounter", AcousticBus.Music, null, 0, 1),
            [SoundscapeScore.Aftermath] = Awaiting("score.aftermath", AcousticBus.Music, null, 0, 1),
        },
        new Dictionary<SoundscapeAmbience, AcousticIntent>
        {
            [SoundscapeAmbience.Outdoor] = Awaiting("ambience.outdoor", AcousticBus.Ambience, null, 0, 1),
        });

    internal AcousticIntent For(SoundCue cue)
    {
        ArgumentNullException.ThrowIfNull(cue);
        return _cues.TryGetValue((cue.Kind, cue.Material), out AcousticIntent? intent) ? intent
            : throw new InvalidOperationException($"No acoustic intent is declared for {cue.Kind} on {cue.Material}.");
    }

    internal AcousticIntent For(SoundscapeScore score) => _scores[score];
    internal AcousticIntent For(SoundscapeAmbience ambience) => _ambiences[ambience];

    private static AcousticIntent Awaiting(string key, AcousticBus bus, string? caption, double interval, int voices) =>
        new(key, bus, AcousticIntentStatus.AwaitingCreatorAsset, null, caption, interval, voices);

    private static AcousticIntent Validate(AcousticIntent intent, AcousticBus expectedBus)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (string.IsNullOrWhiteSpace(intent.Key)) throw new ArgumentException("An acoustic intent needs a key.");
        if (intent.Bus != expectedBus) throw new ArgumentException($"{intent.Key} must play on the {expectedBus} bus.");
        if (!double.IsFinite(intent.MinimumIntervalSeconds) || intent.MinimumIntervalSeconds < 0)
            throw new ArgumentException($"{intent.Key} has an invalid repetition interval.");
        if (intent.MaximumVoices < 1) throw new ArgumentException($"{intent.Key} must allow at least one voice.");
        if ((intent.Status == AcousticIntentStatus.Mapped) != !string.IsNullOrWhiteSpace(intent.StreamPath))
            throw new ArgumentException($"{intent.Key} must name a stream exactly when it is mapped.");
        return intent;
    }
}

/// <summary>The playback decision for one cue after its acoustic intent is known.</summary>
internal enum AcousticPlayback { Played = 1, AwaitingCreatorAsset = 2, IntentionallySilent = 3, Spaced = 4, VoiceLimited = 5, Withheld = 6 }

/// <summary>
/// Presentation-only spacing and voice limits per intent. Spacing suppresses a machine-gun repeat of one sound;
/// the voice limit bounds simultaneous copies. Neither changes which world events occurred.
/// </summary>
internal sealed class AcousticPlaybackGate
{
    private readonly Dictionary<string, double> _lastStarted = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<double>> _endings = new(StringComparer.Ordinal);
    private double _clock;

    internal double Clock => _clock;

    internal void Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        _clock += seconds;
        foreach (List<double> endings in _endings.Values) endings.RemoveAll(end => end <= _clock);
    }

    /// <summary>Decides one cue; a played cue occupies a voice for its duration.</summary>
    internal AcousticPlayback Admit(AcousticIntent intent, double durationSeconds)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (!double.IsFinite(durationSeconds) || durationSeconds < 0) throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        if (intent.Status == AcousticIntentStatus.AwaitingCreatorAsset) return AcousticPlayback.AwaitingCreatorAsset;
        if (intent.Status == AcousticIntentStatus.IntentionallySilent) return AcousticPlayback.IntentionallySilent;
        if (_lastStarted.TryGetValue(intent.Key, out double last) && _clock - last < intent.MinimumIntervalSeconds)
            return AcousticPlayback.Spaced;
        if (!_endings.TryGetValue(intent.Key, out List<double>? endings)) _endings[intent.Key] = endings = [];
        if (endings.Count >= intent.MaximumVoices) return AcousticPlayback.VoiceLimited;
        _lastStarted[intent.Key] = _clock;
        endings.Add(_clock + durationSeconds);
        return AcousticPlayback.Played;
    }
}
