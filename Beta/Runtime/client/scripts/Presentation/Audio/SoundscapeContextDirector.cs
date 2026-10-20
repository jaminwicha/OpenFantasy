namespace OpenFantasy.Client.Presentation.Audio;

/// <summary>The sustained musical context. Encounter music is a context, not an automatic battle anthem.</summary>
internal enum SoundscapeScore { Exploration = 1, Encounter = 2, Aftermath = 3 }

/// <summary>
/// The sustained ambience bed. Only the open outdoor bed is bound so far; shelter, cave and portal enclosure
/// need listener enclosure in the presented view, and daylight variation needs an accepted creator decision.
/// </summary>
internal enum SoundscapeAmbience { Outdoor = 1 }

/// <summary>Why the sustained context last changed. Restoration rebuilds the context without a transition.</summary>
internal enum SoundscapeContextChange { Restoration = 1, EncounterStarted = 2, EncounterEnded = 3, AftermathElapsed = 4, Ducking = 5 }

/// <summary>The sustained context a listener should hear now and how to reach it.</summary>
internal sealed record SoundscapeContext(SoundscapeScore Score, SoundscapeAmbience Ambience, bool IsDucked,
    SoundscapeContextChange Change, double FadeSeconds);

/// <summary>
/// Chooses the sustained score, ambience and speech ducking from permitted observations with presentation-only dwell.
/// An encounter start is urgent and immediate. Leaving it waits for a short minimum so a very brief engagement does not
/// flash, passes through a quiet aftermath and then returns to exploration. A new visit restores the current context
/// without a fade. Durations are tuning values validated by listening; they never delay a legal action or a world clock.
/// </summary>
internal sealed class SoundscapeContextDirector
{
    internal const double MinimumEncounterSeconds = 3.0;
    internal const double AftermathSeconds = 10.0;
    internal const double EncounterFadeSeconds = 0.6;
    internal const double AftermathFadeSeconds = 2.5;
    internal const double ExplorationFadeSeconds = 4.0;
    internal const double DuckingFadeSeconds = 0.3;

    private SoundscapeListener? _listener;
    private SoundscapeEncounterPhase _phase;
    private double _sinceScoreChange;
    private SoundscapeContext? _current;

    /// <summary>The context to present, or null before the first observation of a visit.</summary>
    internal SoundscapeContext? Current => _current;

    internal void Reset()
    {
        _listener = null;
        _current = null;
        _phase = SoundscapeEncounterPhase.None;
        _sinceScoreChange = 0;
    }

    /// <summary>Records an observation; returns the new context when it changed, otherwise null.</summary>
    internal SoundscapeContext? Observe(SoundscapeObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        _phase = observation.Encounter;
        if (_current is null || _listener != observation.Listener)
        {
            _listener = observation.Listener;
            _sinceScoreChange = 0;
            // A restored visit hears the current situation; it does not replay how it got there.
            SoundscapeScore restored = _phase == SoundscapeEncounterPhase.Engaged ? SoundscapeScore.Encounter
                : _phase == SoundscapeEncounterPhase.Concluded ? SoundscapeScore.Aftermath
                : SoundscapeScore.Exploration;
            return Set(new SoundscapeContext(restored, SoundscapeAmbience.Outdoor, observation.IsConversing,
                SoundscapeContextChange.Restoration, 0));
        }
        if (_phase == SoundscapeEncounterPhase.Engaged && _current.Score != SoundscapeScore.Encounter)
        {
            _sinceScoreChange = 0;
            return Set(_current with { Score = SoundscapeScore.Encounter, IsDucked = observation.IsConversing,
                Change = SoundscapeContextChange.EncounterStarted, FadeSeconds = EncounterFadeSeconds });
        }
        if (observation.IsConversing != _current.IsDucked)
            return Set(_current with { IsDucked = observation.IsConversing, Change = SoundscapeContextChange.Ducking,
                FadeSeconds = DuckingFadeSeconds });
        return ApplyDwell();
    }

    /// <summary>Advances presentation time; returns the new context when a dwell completes, otherwise null.</summary>
    internal SoundscapeContext? Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (_current is null) return null;
        _sinceScoreChange += seconds;
        return ApplyDwell();
    }

    private SoundscapeContext? ApplyDwell()
    {
        if (_current is null) return null;
        if (_current.Score == SoundscapeScore.Encounter && _phase != SoundscapeEncounterPhase.Engaged
            && _sinceScoreChange >= MinimumEncounterSeconds)
        {
            _sinceScoreChange = 0;
            return Set(_current with { Score = SoundscapeScore.Aftermath, Change = SoundscapeContextChange.EncounterEnded,
                FadeSeconds = AftermathFadeSeconds });
        }
        if (_current.Score == SoundscapeScore.Aftermath && _phase == SoundscapeEncounterPhase.None
            && _sinceScoreChange >= AftermathSeconds)
        {
            _sinceScoreChange = 0;
            return Set(_current with { Score = SoundscapeScore.Exploration, Change = SoundscapeContextChange.AftermathElapsed,
                FadeSeconds = ExplorationFadeSeconds });
        }
        return null;
    }

    private SoundscapeContext Set(SoundscapeContext context)
    {
        _current = context;
        return context;
    }
}
