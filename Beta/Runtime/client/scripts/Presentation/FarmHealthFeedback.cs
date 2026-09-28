namespace OpenFantasy.Client.Presentation;

internal enum FarmHealthChange { None, Damage, Restoration }

/// <summary>Transient difference between consecutive permitted HP observations, never a world effect.</summary>
internal sealed class FarmHealthFeedback
{
    internal const double LifetimeSeconds = 0.8;
    private int? _previous;

    internal FarmHealthChange Change { get; private set; }
    internal int Amount { get; private set; }
    internal double AgeSeconds { get; private set; } = LifetimeSeconds;
    internal bool IsVisible => Change != FarmHealthChange.None && AgeSeconds < LifetimeSeconds;

    internal void Observe(int? health)
    {
        if (health is < 0) throw new ArgumentOutOfRangeException(nameof(health));
        if (health is null)
        {
            _previous = null;
            Change = FarmHealthChange.None;
            Amount = 0;
            AgeSeconds = LifetimeSeconds;
            return;
        }
        if (_previous is { } before && health.Value != before)
        {
            Change = health.Value < before ? FarmHealthChange.Damage : FarmHealthChange.Restoration;
            Amount = Math.Abs(health.Value - before);
            AgeSeconds = 0;
        }
        _previous = health;
    }

    internal void Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        AgeSeconds = Math.Min(LifetimeSeconds, AgeSeconds + seconds);
    }
}
