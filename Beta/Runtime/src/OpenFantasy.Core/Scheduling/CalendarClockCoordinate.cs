namespace OpenFantasy.Core.Scheduling;

/// <summary>An exact instant in one explicitly named calendar; different clocks are incomparable.</summary>
public sealed record CalendarClockCoordinate : IComparable<CalendarClockCoordinate>
{
    public CalendarClockCoordinate(CalendarQueueScope scope, WorldInstant instant)
    {
        ArgumentNullException.ThrowIfNull(scope);
        Scope = scope;
        Instant = instant;
    }

    public CalendarQueueScope Scope { get; }
    public WorldInstant Instant { get; }

    public int CompareTo(CalendarClockCoordinate? other)
    {
        if (other is null)
        {
            return 1;
        }
        if (Scope != other.Scope)
        {
            throw new ArgumentException("Independent calendar coordinates cannot be compared.", nameof(other));
        }
        return Instant.CompareTo(other.Instant);
    }
}
