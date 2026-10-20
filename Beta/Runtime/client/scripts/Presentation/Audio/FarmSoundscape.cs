using Godot;
using OpenFantasy.Core.Spatial;

namespace OpenFantasy.Client.Presentation.Audio;

/// <summary>
/// Godot audio adapter for one presented view. It turns semantic cues and contexts into playback on the scene-owned
/// mixer buses and owns voices, spacing, fades and ducking. It reads permitted observations only and never submits,
/// confirms or delays a world action. Intents still awaiting creator assets stay silent and are reported as such.
/// </summary>
internal sealed partial class FarmSoundscape : Node2D
{
    /// <summary>Audible radius of positional cues: twelve cells. Presentation tuning validated by listening.</summary>
    internal const float AudibleRadiusPixels = 12 * FarmCanvasCoordinates.PixelsPerCell;
    /// <summary>Attenuation of music and ambience under speech. Mix tuning validated by listening.</summary>
    internal const float DuckingDecibels = -12f;
    private const float SilentDecibels = -60f;
    private const int EffectVoices = 8;

    private static readonly Dictionary<AcousticBus, StringName> BusNames = new()
    {
        [AcousticBus.Music] = "Music",
        [AcousticBus.Ambience] = "Ambience",
        [AcousticBus.Effects] = "Effects",
        [AcousticBus.Voice] = "Voice",
    };

    private readonly SoundscapeCueDeriver _cues = new();
    private readonly SoundscapeContextDirector _context = new();
    private readonly AcousticPlaybackGate _gate = new();
    private readonly Dictionary<string, AudioStream> _streams = new(StringComparer.Ordinal);
    private readonly List<AudioStreamPlayer2D> _voices = [];
    private Bed _music = null!;
    private Bed _ambience = null!;

    internal AcousticIntentCatalog Catalog { get; init; } = AcousticIntentCatalog.Default;
    internal SoundscapeDiagnostics Diagnostics { get; } = new();

    public override void _Ready()
    {
        foreach ((AcousticBus bus, StringName name) in BusNames)
        {
            if (AudioServer.GetBusIndex(name) < 0)
                GD.PushError($"The {bus} mixer bus is missing from the project's bus layout; its sounds play on Master.");
        }
        _music = new Bed(this, BusNames[AcousticBus.Music]);
        _ambience = new Bed(this, BusNames[AcousticBus.Ambience]);
        for (int index = 0; index < EffectVoices; index++)
        {
            var voice = new AudioStreamPlayer2D { Bus = BusNames[AcousticBus.Effects], MaxDistance = AudibleRadiusPixels };
            AddChild(voice);
            _voices.Add(voice);
        }
    }

    /// <summary>
    /// Presents one permitted observation. <paramref name="project"/> places a source on the canvas and
    /// <paramref name="admits"/> withholds sources outside the presented frame.
    /// </summary>
    internal void Present(SoundscapeObservation observation, Func<SubunitPosition, Vector2> project,
        Func<SubunitPosition, bool> admits)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(admits);
        if (_cues.Listener != observation.Listener) Diagnostics.RecordVisit();
        foreach (SoundCue cue in _cues.Observe(observation))
        {
            AcousticIntent intent = Catalog.For(cue);
            AcousticPlayback playback = admits(cue.Origin) ? Play(intent, project(cue.Origin)) : AcousticPlayback.Withheld;
            Diagnostics.Record(cue, intent, playback);
        }
        if (_context.Observe(observation) is { } context) Apply(context);
    }

    /// <summary>
    /// Another character's view is presented instead. One-shots stop deriving and the next presented view of this
    /// listener is a fresh baseline; the sustained context continues unchanged.
    /// </summary>
    internal void Suspend() => _cues.Reset();

    /// <summary>A session closed, reopened or reconnected: forget the visit and silence the beds immediately.</summary>
    internal void Reset()
    {
        _cues.Reset();
        _context.Reset();
        _music.Stop();
        _ambience.Stop();
        foreach (AudioStreamPlayer2D voice in _voices) voice.Stop();
    }

    /// <summary>Advances presentation time only: fades, dwell and voice release. World time is never read here.</summary>
    internal void Advance(double seconds)
    {
        _gate.Advance(seconds);
        if (_context.Advance(seconds) is { } context) Apply(context);
        _music.Advance(seconds);
        _ambience.Advance(seconds);
    }

    private AcousticPlayback Play(AcousticIntent intent, Vector2 origin)
    {
        if (!intent.IsPlayable) return _gate.Admit(intent, 0);
        // The shared voice pool is checked first so the gate never counts a voice that could not start.
        AudioStreamPlayer2D? voice = _voices.FirstOrDefault(candidate => !candidate.Playing);
        if (voice is null) return AcousticPlayback.VoiceLimited;
        AudioStream stream = Load(intent);
        AcousticPlayback playback = _gate.Admit(intent, stream.GetLength());
        if (playback != AcousticPlayback.Played) return playback;
        voice.Stream = stream;
        voice.Position = origin;
        voice.Play();
        return playback;
    }

    private void Apply(SoundscapeContext context)
    {
        AcousticIntent score = Catalog.For(context.Score);
        AcousticIntent ambience = Catalog.For(context.Ambience);
        float duck = context.IsDucked ? DuckingDecibels : 0f;
        double fade = context.Change == SoundscapeContextChange.Restoration ? 0 : context.FadeSeconds;
        _music.Target(score.IsPlayable ? Load(score) : null, duck, fade);
        _ambience.Target(ambience.IsPlayable ? Load(ambience) : null, duck, fade);
        Diagnostics.Record(context, score, ambience);
    }

    private AudioStream Load(AcousticIntent intent)
    {
        if (_streams.TryGetValue(intent.Key, out AudioStream? cached)) return cached;
        AudioStream stream = GD.Load<AudioStream>(intent.StreamPath!)
            ?? throw new InvalidOperationException($"Mapped acoustic intent {intent.Key} has no loadable stream.");
        _streams[intent.Key] = stream;
        return stream;
    }

    /// <summary>One sustained bed: a stream, a target level and a linear decibel fade toward it.</summary>
    private sealed class Bed
    {
        private readonly AudioStreamPlayer _player;
        private float _from = SilentDecibels;
        private float _to = SilentDecibels;
        private double _elapsed;
        private double _duration;

        internal Bed(Node owner, StringName bus)
        {
            _player = new AudioStreamPlayer { Bus = bus, VolumeDb = SilentDecibels };
            owner.AddChild(_player);
        }

        internal void Target(AudioStream? stream, float level, double fadeSeconds)
        {
            if (stream is null)
            {
                Stop();
                return;
            }
            if (_player.Stream != stream)
            {
                _player.Stream = stream;
                _player.VolumeDb = fadeSeconds > 0 ? SilentDecibels : level;
                _player.Play();
            }
            _from = _player.VolumeDb;
            _to = level;
            _elapsed = 0;
            _duration = fadeSeconds;
            if (fadeSeconds <= 0) _player.VolumeDb = level;
        }

        internal void Advance(double seconds)
        {
            if (_duration <= 0 || _elapsed >= _duration) return;
            _elapsed = Math.Min(_duration, _elapsed + seconds);
            _player.VolumeDb = Mathf.Lerp(_from, _to, (float)(_elapsed / _duration));
        }

        internal void Stop()
        {
            _player.Stop();
            _player.Stream = null;
            _player.VolumeDb = SilentDecibels;
            _from = _to = SilentDecibels;
            _duration = 0;
        }
    }
}
